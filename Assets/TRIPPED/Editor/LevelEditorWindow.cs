using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Trouble.Sim;
using UnityEditor;
using UnityEngine;

namespace Trouble.EditorTools
{
    /// <summary>
    /// Tools > TRIPPED > Level Editor (GDD v1.1 §4.3). Edits level files and the global config files,
    /// and runs every check on the level as it is on screen, saved or not.
    /// </summary>
    public sealed class LevelEditorWindow : EditorWindow
    {
        private const string LevelsDir = "Assets/TRIPPED/Resources/Levels";
        private const string ConfigDir = "Assets/TRIPPED/Resources/Config";
        private const string RulesPath = ConfigDir + "/rules.json";
        private const string CatalogPath = ConfigDir + "/catalog.json";
        private const string StringsPath = ConfigDir + "/strings.json";

        private static readonly string[] Tabs = { "Verify", "Rules", "House", "Wiring", "People", "Solutions", "Global" };

        private LevelHolder _level;
        private GlobalHolder _global;
        private SerializedObject _levelSO;
        private SerializedObject _globalSO;

        private string[] _levelFiles = Array.Empty<string>();
        private int _levelIndex;
        private string _savedLevelJson = "";
        private string _savedRulesJson = "", _savedCatalogJson = "", _savedStringsJson = "";

        private int _tab;
        private Vector2 _scroll;
        private List<string> _validation;
        private List<VerifyRow> _verify;
        private string _verifiedAt = "";
        private string _script = "";
        private string _scenarioName = "new scenario";
        private int _scenarioExpect;
        private static readonly string[] Expectations = { Verifier.Won, Verifier.Lost, Verifier.NotFasterThanReference };

        [MenuItem("Tools/TRIPPED/Level Editor")]
        public static void Open()
        {
            GetWindow<LevelEditorWindow>("TRIPPED Levels").Show();
        }

        /// <summary>Opens the editor on its Verify tab and runs every check on the level shown.</summary>
        [MenuItem("Tools/TRIPPED/Run Sim (open level)")]
        public static void OpenAndRunSim()
        {
            var window = GetWindow<LevelEditorWindow>("TRIPPED Levels");
            window.Show();
            window._tab = 0;
            if (window._levelFiles.Length > 0) window.RunSim();
            window.Repaint();
        }

        private void OnEnable()
        {
            if (_level == null) _level = CreateInstance<LevelHolder>();
            if (_global == null) _global = CreateInstance<GlobalHolder>();
            _level.hideFlags = HideFlags.DontSave;
            _global.hideFlags = HideFlags.DontSave;
            ReloadGlobal();
            RefreshLevelList();
            if (_levelFiles.Length > 0) LoadLevel(Mathf.Clamp(_levelIndex, 0, _levelFiles.Length - 1));
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            DestroyImmediate(_level);
            DestroyImmediate(_global);
        }

        // ---- loading and saving --------------------------------------------------------

        private void RefreshLevelList()
        {
            var files = new List<string>();
            if (Directory.Exists(LevelsDir))
                foreach (var f in Directory.GetFiles(LevelsDir, "level*.json")) files.Add(f.Replace('\\', '/'));
            files.Sort((a, b) => LevelNumber(a).CompareTo(LevelNumber(b)));
            _levelFiles = files.ToArray();
        }

        private static int LevelNumber(string path)
        {
            var m = Regex.Match(Path.GetFileNameWithoutExtension(path), @"\d+");
            return m.Success ? int.Parse(m.Value) : int.MaxValue;
        }

        private void LoadLevel(int index)
        {
            _levelIndex = index;
            _level.level = JsonUtility.FromJson<LevelData>(File.ReadAllText(_levelFiles[index]));
            _savedLevelJson = JsonUtility.ToJson(_level.level, true);
            _levelSO = new SerializedObject(_level);
            _validation = null;
            _verify = null;
        }

        private void SaveLevel()
        {
            var path = _levelFiles[_levelIndex];
            var json = JsonUtility.ToJson(_level.level, true);
            File.WriteAllText(path, json + "\n");
            _savedLevelJson = json;
            AssetDatabase.ImportAsset(path);
        }

        private void NewLevel()
        {
            int n = _levelFiles.Length == 0 ? 1 : LevelNumber(_levelFiles[_levelFiles.Length - 1]) + 1;
            var level = new LevelData { id = n, title = "New Level", dayLengthSec = 60 };
            level.rooms.Add(new RoomData { id = "kitchen", name = "Kitchen" });
            level.circuits.Add(new CircuitData { id = "A", label = "KITCHEN", capacity = 10 });
            level.sockets.Add(new SocketData { id = "K1", room = "kitchen", circuit = "A", revealed = true });
            var path = $"{LevelsDir}/level{n}.json";
            File.WriteAllText(path, JsonUtility.ToJson(level, true) + "\n");
            AssetDatabase.ImportAsset(path);
            RefreshLevelList();
            LoadLevel(Array.IndexOf(_levelFiles, path));
        }

        private void ReloadGlobal()
        {
            _global.rules = File.Exists(RulesPath) ? JsonUtility.FromJson<RulesConfig>(File.ReadAllText(RulesPath)) : new RulesConfig();
            _global.catalog = File.Exists(CatalogPath) ? JsonUtility.FromJson<CatalogData>(File.ReadAllText(CatalogPath)) : new CatalogData();
            _global.strings = File.Exists(StringsPath) ? JsonUtility.FromJson<StringsData>(File.ReadAllText(StringsPath)) : new StringsData();
            _savedRulesJson = JsonUtility.ToJson(_global.rules, true);
            _savedCatalogJson = JsonUtility.ToJson(_global.catalog, true);
            _savedStringsJson = JsonUtility.ToJson(_global.strings, true);
            _globalSO = new SerializedObject(_global);
        }

        private static void WriteJson(string path, object data, ref string saved)
        {
            saved = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, saved + "\n");
            AssetDatabase.ImportAsset(path);
        }

        private bool LevelDirty => _level != null && JsonUtility.ToJson(_level.level, true) != _savedLevelJson;
        private bool RulesDirty => JsonUtility.ToJson(_global.rules, true) != _savedRulesJson;
        private bool CatalogDirty => JsonUtility.ToJson(_global.catalog, true) != _savedCatalogJson;
        private bool StringsDirty => JsonUtility.ToJson(_global.strings, true) != _savedStringsJson;

        // ---- window --------------------------------------------------------------------

        private void OnGUI()
        {
            if (_level == null || _global == null) OnEnable();
            DrawHeader();
            if (_levelFiles.Length == 0)
            {
                EditorGUILayout.HelpBox($"No level files in {LevelsDir}.", MessageType.Info);
                return;
            }

            _tab = GUILayout.Toolbar(_tab, Tabs);
            EditorGUILayout.Space();
            _levelSO.Update();
            _globalSO.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (Tabs[_tab])
            {
                case "Verify": DrawVerify(); break;
                case "Rules": DrawRules(); break;
                case "House": DrawHouse(); break;
                case "Wiring": DrawWiring(); break;
                case "People": DrawPeople(); break;
                case "Solutions": DrawSolutions(); break;
                case "Global": DrawGlobal(); break;
            }
            EditorGUILayout.EndScrollView();
            _levelSO.ApplyModifiedProperties();
            _globalSO.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var names = new string[_levelFiles.Length];
                for (int i = 0; i < names.Length; i++) names[i] = Path.GetFileNameWithoutExtension(_levelFiles[i]);
                int picked = EditorGUILayout.Popup(_levelIndex, names, EditorStyles.toolbarPopup, GUILayout.Width(120));
                if (picked != _levelIndex && ConfirmDiscardLevel()) LoadLevel(picked);

                GUILayout.Label(LevelDirty ? "  unsaved changes" : "", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("New level", EditorStyles.toolbarButton)) NewLevel();
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton) && ConfirmDiscardLevel()) LoadLevel(_levelIndex);
                using (new EditorGUI.DisabledScope(!LevelDirty))
                    if (GUILayout.Button("Save level", EditorStyles.toolbarButton)) SaveLevel();
            }
        }

        private bool ConfirmDiscardLevel()
        {
            return !LevelDirty || EditorUtility.DisplayDialog("Unsaved level", "Throw away the changes to this level?", "Throw away", "Keep editing");
        }

        // ---- Verify --------------------------------------------------------------------

        private void DrawVerify()
        {
            EditorGUILayout.HelpBox("Checks use the level and the global files as they are in this window, saved or not.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate", GUILayout.Height(28))) _validation = Validate();
                if (GUILayout.Button("Run Sim", GUILayout.Height(28))) RunSim();
            }

            if (_validation != null)
            {
                EditorGUILayout.Space();
                if (_validation.Count == 0) EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
                foreach (var e in _validation) EditorGUILayout.HelpBox(e, MessageType.Error);
            }

            if (_verify == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Run Sim — {_verifiedAt}", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("", GUILayout.Width(44));
                GUILayout.Label("Run", EditorStyles.miniBoldLabel, GUILayout.Width(170));
                GUILayout.Label("Expect", EditorStyles.miniBoldLabel, GUILayout.Width(150));
                GUILayout.Label("Result", EditorStyles.miniBoldLabel, GUILayout.Width(60));
                GUILayout.Label("Ends", EditorStyles.miniBoldLabel, GUILayout.Width(55));
                GUILayout.Label("Trips", EditorStyles.miniBoldLabel, GUILayout.Width(40));
                GUILayout.Label("Refused", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                GUILayout.Label("Cause", EditorStyles.miniBoldLabel);
            }
            foreach (var row in _verify)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var old = GUI.color;
                        GUI.color = row.MetExpectation ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f);
                        GUILayout.Label(row.MetExpectation ? "OK" : "CHECK", EditorStyles.boldLabel, GUILayout.Width(44));
                        GUI.color = old;
                        GUILayout.Label(row.Name, GUILayout.Width(170));
                        GUILayout.Label(row.Expect, GUILayout.Width(150));
                        GUILayout.Label(row.Phase.ToString(), GUILayout.Width(60));
                        GUILayout.Label(TimeText(row.EndTick), GUILayout.Width(55));
                        GUILayout.Label(row.TripCount.ToString(), GUILayout.Width(40));
                        GUILayout.Label(row.Rejections.ToString(), GUILayout.Width(50));
                        GUILayout.Label(row.Cause, EditorStyles.wordWrappedLabel);
                    }
                    if (row.Trips.Length > 0) EditorGUILayout.LabelField("Trips: " + row.Trips, EditorStyles.wordWrappedMiniLabel);
                    if (row.Problem.Length > 0) EditorGUILayout.HelpBox(row.Problem, MessageType.Warning);
                }
            }
        }

        private List<string> Validate()
        {
            return LevelValidator.Validate(_level.level, new Catalog(_global.catalog), _global.rules);
        }

        private void RunSim()
        {
            _validation = Validate();
            _verify = Verifier.RunAll(_level.level, _global.rules, new Catalog(_global.catalog), new Strings(_global.strings));
            _verifiedAt = DateTime.Now.ToString("HH:mm:ss");
        }

        private string TimeText(int ticks)
        {
            int tps = Mathf.Max(1, _global.rules.ticksPerSecond);
            return $"{CauseFormatter.Clock(ticks, tps)}.{ticks % tps * 10 / tps}";
        }

        // ---- Rules (per-level overrides) -------------------------------------------------

        private void DrawRules()
        {
            EditorGUILayout.HelpBox("Tick a rule to give this level its own value. Unticked rules use rules.json (the Global tab).", MessageType.None);
            var overrides = _level.level.rulesOverride;
            foreach (var field in typeof(RulesConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                int at = overrides.FindIndex(o => o.key == field.Name);
                string global = Convert.ToString(field.GetValue(_global.rules), System.Globalization.CultureInfo.InvariantCulture);
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool on = EditorGUILayout.ToggleLeft(field.Name, at >= 0, GUILayout.Width(220));
                    GUILayout.Label("global: " + global, EditorStyles.miniLabel, GUILayout.Width(130));
                    if (on && at < 0)
                    {
                        Undo.RecordObject(_level, "Override rule");
                        overrides.Add(new RuleOverrideData { key = field.Name, value = global });
                        at = overrides.Count - 1;
                    }
                    else if (!on && at >= 0)
                    {
                        Undo.RecordObject(_level, "Remove rule override");
                        overrides.RemoveAt(at);
                        at = -1;
                    }
                    using (new EditorGUI.DisabledScope(at < 0))
                    {
                        string current = at >= 0 ? overrides[at].value : global;
                        string edited = EditorGUILayout.DelayedTextField(current);
                        if (at >= 0 && edited != current)
                        {
                            Undo.RecordObject(_level, "Change rule override");
                            overrides[at].value = edited;
                        }
                    }
                }
            }
            _levelSO.Update();
        }

        // ---- House ---------------------------------------------------------------------

        private void DrawHouse()
        {
            Prop("id");
            Prop("title");
            Prop("teaches");
            Prop("intro");
            Prop("dayLengthSec");
            RoomPopup("startRoom", "Start room (empty = first room)");
            RoomPopup("panelRoom", "Panel room (empty = start room)");
            EditorGUILayout.HelpBox("Rooms run left to right. If every room has width 0 they are laid out on one floor automatically. " +
                                    "No doors = an open door between each pair of neighbouring rooms.", MessageType.None);
            Prop("rooms");
            Prop("doors");
            using (new EditorGUI.DisabledScope(true))
                GUILayout.Button("Build Preview (needs HouseBuilder from Trouble.Game)");
        }

        private void RoomPopup(string field, string label)
        {
            var prop = _levelSO.FindProperty("level." + field);
            var ids = new List<string> { "" };
            foreach (var r in _level.level.rooms) ids.Add(r.id);
            int current = Mathf.Max(0, ids.IndexOf(prop.stringValue));
            var shown = ids.ConvertAll(id => id.Length == 0 ? "(default)" : id).ToArray();
            prop.stringValue = ids[EditorGUILayout.Popup(label, current, shown)];
        }

        // ---- Wiring --------------------------------------------------------------------

        private void DrawWiring()
        {
            Prop("mainCapacity");
            Prop("fridgeSocket");
            Prop("fridgeSpoilSec");
            Prop("lampCount");
            Prop("cordCount");
            Prop("circuits");
            // The grid below edits the data directly; keep the fields above first.
            _levelSO.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Which circuit each socket is really on", EditorStyles.boldLabel);
            var circuits = _level.level.circuits;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Socket (room)", EditorStyles.miniBoldLabel, GUILayout.Width(150));
                foreach (var c in circuits) GUILayout.Label(c.id, EditorStyles.miniBoldLabel, GUILayout.Width(30));
                GUILayout.Label("Shown at start", EditorStyles.miniBoldLabel, GUILayout.Width(95));
                GUILayout.Label("Labels suggest", EditorStyles.miniBoldLabel);
            }
            var assumedChoices = new List<string> { "" };
            foreach (var c in circuits) assumedChoices.Add(c.id);
            var assumedShown = assumedChoices.ConvertAll(id => id.Length == 0 ? "(none)" : id).ToArray();
            foreach (var s in _level.level.sockets)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{s.id} ({s.room})", GUILayout.Width(150));
                    foreach (var c in circuits)
                    {
                        bool on = GUILayout.Toggle(s.circuit == c.id, "", GUILayout.Width(30));
                        if (on && s.circuit != c.id)
                        {
                            Undo.RecordObject(_level, "Rewire socket");
                            s.circuit = c.id;
                        }
                    }
                    bool revealed = GUILayout.Toggle(s.revealed, "", GUILayout.Width(95));
                    int assumed = EditorGUILayout.Popup(Mathf.Max(0, assumedChoices.IndexOf(s.assumed)), assumedShown, GUILayout.Width(80));
                    if (revealed != s.revealed || assumedChoices[assumed] != s.assumed)
                    {
                        Undo.RecordObject(_level, "Edit socket");
                        s.revealed = revealed;
                        s.assumed = assumedChoices[assumed];
                    }
                }
            }
            _levelSO.Update();
            EditorGUILayout.Space();
            Prop("sockets");
        }

        // ---- People --------------------------------------------------------------------

        private void DrawPeople()
        {
            DrawTimeline();
            Prop("requests");
            Prop("routines");
        }

        /// <summary>One bar per request (shortest possible run up to its deadline) and per routine, on a 0..day axis.</summary>
        private void DrawTimeline()
        {
            var level = _level.level;
            float day = Mathf.Max(1f, level.dayLengthSec);
            int rows = level.requests.Count + level.routines.Count;
            var area = GUILayoutUtility.GetRect(0, 18 * rows + 20, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area, new Color(0.12f, 0.12f, 0.14f));
            float labelW = 150f;
            float x0 = area.x + labelW, w = area.width - labelW - 8f;
            float X(float sec) => x0 + w * Mathf.Clamp01(sec / day);

            for (int s = 0; s <= day; s += 10)
                GUI.Label(new Rect(X(s) - 10, area.yMax - 16, 40, 16), $"{s / 60}:{s % 60:00}", EditorStyles.miniLabel);

            float y = area.y + 2;
            foreach (var r in level.requests)
            {
                GUI.Label(new Rect(area.x + 4, y, labelW, 16), $"{r.owner}: {r.appliance}", EditorStyles.miniLabel);
                EditorGUI.DrawRect(new Rect(X(0), y + 4, X(r.dueSec) - X(0), 8), new Color(0.35f, 0.35f, 0.4f));
                EditorGUI.DrawRect(new Rect(X(0), y + 4, X(r.durationSec) - X(0), 8), new Color(0.3f, 0.7f, 0.35f));
                EditorGUI.DrawRect(new Rect(X(r.dueSec) - 1, y, 2, 16), Color.white);
                y += 18;
            }
            foreach (var r in level.routines)
            {
                GUI.Label(new Rect(area.x + 4, y, labelW, 16), $"{r.owner}: {r.appliance} (routine)", EditorStyles.miniLabel);
                EditorGUI.DrawRect(new Rect(X(r.startSec), y + 4, X(r.startSec + r.durationSec) - X(r.startSec), 8), new Color(0.9f, 0.6f, 0.25f));
                y += 18;
            }
        }

        // ---- Solutions -----------------------------------------------------------------

        private void DrawSolutions()
        {
            EditorGUILayout.HelpBox("Script format: commands separated by ';', each 'tick cmd a [b]'. tick -1 = during planning.\n" +
                                    "cmd: plug, unplug, on, off, breaker, cord, uncord, door, move, cancel.\n" +
                                    "Commands join the end of the character's queue; '!' before cmd clears the queue first (a plain click).\n" +
                                    "Example: -1 plug kettle K1; 0 on kettle; 80 on toaster", MessageType.None);
            _script = EditorGUILayout.TextArea(_script, GUILayout.MinHeight(60));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Show reference as script")) _script = Replay.ToScript(_level.level.reference, ";\n");
                if (GUILayout.Button("Replace reference with script") && TryParse(out var commands))
                {
                    Undo.RecordObject(_level, "Replace reference");
                    _level.level.reference = commands;
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _scenarioName = EditorGUILayout.TextField(_scenarioName);
                _scenarioExpect = EditorGUILayout.Popup(_scenarioExpect, Expectations, GUILayout.Width(170));
                if (GUILayout.Button("Add script as scenario", GUILayout.Width(170)) && TryParse(out var commands))
                {
                    Undo.RecordObject(_level, "Add scenario");
                    _level.level.scenarios.Add(new ScenarioData { name = _scenarioName, expect = Expectations[_scenarioExpect], commands = commands });
                }
            }
            _levelSO.Update();
            EditorGUILayout.Space();
            Prop("reference");
            Prop("scenarios");
        }

        private bool TryParse(out List<CommandData> commands)
        {
            try
            {
                commands = Replay.Parse(_script);
                return true;
            }
            catch (FormatException e)
            {
                EditorUtility.DisplayDialog("Script", e.Message, "OK");
                commands = null;
                return false;
            }
        }

        // ---- Global --------------------------------------------------------------------

        private void DrawGlobal()
        {
            EditorGUILayout.HelpBox("These files apply to every level. Run Sim uses the values here even before they are saved.", MessageType.None);
            if (GUILayout.Button("Reload all from disk") &&
                (!(RulesDirty || CatalogDirty || StringsDirty) || EditorUtility.DisplayDialog("Unsaved", "Throw away changes to the global files?", "Throw away", "Keep")))
                ReloadGlobal();

            GlobalSection("rules", "rules.json", RulesDirty, () => WriteJson(RulesPath, _global.rules, ref _savedRulesJson));
            GlobalSection("catalog", "catalog.json", CatalogDirty, () => WriteJson(CatalogPath, _global.catalog, ref _savedCatalogJson));
            GlobalSection("strings", "strings.json", StringsDirty, () => WriteJson(StringsPath, _global.strings, ref _savedStringsJson));
        }

        private void GlobalSection(string field, string file, bool dirty, Action save)
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(file + (dirty ? "  (unsaved)" : ""), EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!dirty))
                    if (GUILayout.Button("Save " + file, GUILayout.Width(150))) save();
            }
            EditorGUILayout.PropertyField(_globalSO.FindProperty(field), true);
        }

        // ---- helpers -------------------------------------------------------------------

        private void Prop(string field)
        {
            EditorGUILayout.PropertyField(_levelSO.FindProperty("level." + field), true);
        }
    }
}
