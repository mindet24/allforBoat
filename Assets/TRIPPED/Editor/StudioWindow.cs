using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Trouble.Sim;
using UnityEditor;
using UnityEngine;

namespace Trouble.EditorTools
{
    /// <summary>
    /// TRIPPED Studio (Tools > TRIPPED > Studio): every tunable of the game in one window. JSON data (rules, levels with
    /// their day randomisation, catalog, strings) is edited field by field and saved back to its file; the config assets
    /// (view, UI, juice, rig) are edited in place, so in Play mode a change shows at once. The Keys tab gathers every
    /// hotkey from every config. A search box filters fields by name on every tab.
    /// </summary>
    public sealed class StudioWindow : EditorWindow
    {
        private const string ConfigFolder = "Assets/TRIPPED/Resources/Config";
        private const string LevelFolder = "Assets/TRIPPED/Resources/Levels";

        /// <summary>Config assets by class name and Resources file name; a missing one can be created from the tab.</summary>
        private static readonly (string tab, string type, string file)[] ConfigAssets =
        {
            ("View", "Trouble.Game.TrippedViewConfig", "TrippedViewConfig"),
            ("UI", "Trouble.Game.TrippedUiConfig", "TrippedUiConfig"),
            ("Juice", "Trouble.Game.JuiceConfig", "JuiceConfig"),
            ("Rig", "Trouble.Game.RigConfig", "RigConfig"),
        };

        /// <summary>Known choices for string rules, shown as drop-downs.</summary>
        private static readonly Dictionary<string, string[]> Choices = new Dictionary<string, string[]>
        {
            { "placeMode", new[] { "socket", "floor" } },
            { "counterAfterTrip", new[] { "keep", "reset" } },
            { "breakerHudMode", new[] { "Always", "AtPanelOnly" } },
        };

        private static readonly string[] Tabs = { "Rules", "Level", "Catalog", "Strings", "View", "UI", "Juice", "Rig", "Keys" };

        private int _tab;
        private string _search = "";
        private Vector2 _scroll;

        private JsonDoc<RulesConfig> _rules;
        private JsonDoc<CatalogData> _catalog;
        private JsonDoc<StringsData> _strings;
        private JsonDoc<LevelData> _level;
        private string[] _levelFiles = Array.Empty<string>();
        private int _levelIndex;

        private readonly Dictionary<string, UnityEditor.Editor> _editors = new Dictionary<string, UnityEditor.Editor>();
        private readonly HashSet<string> _open = new HashSet<string>();

        [MenuItem("Tools/TRIPPED/Studio %#t")]
        public static void Open()
        {
            var w = GetWindow<StudioWindow>("TRIPPED Studio");
            w.minSize = new Vector2(420, 300);
        }

        private void OnEnable()
        {
            _rules = new JsonDoc<RulesConfig>(ConfigFolder + "/rules.json");
            _catalog = new JsonDoc<CatalogData>(ConfigFolder + "/catalog.json");
            _strings = new JsonDoc<StringsData>(ConfigFolder + "/strings.json");
            RefreshLevels();
        }

        private void OnDisable()
        {
            foreach (var e in _editors.Values)
                if (e != null) DestroyImmediate(e);
            _editors.Clear();
        }

        private void RefreshLevels()
        {
            _levelFiles = Directory.Exists(LevelFolder)
                ? Directory.GetFiles(LevelFolder, "*.json").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray()
                : Array.Empty<string>();
            // Day mode first: it is the level most people tune.
            int demo = Array.FindIndex(_levelFiles, p => p.EndsWith("level_demo.json"));
            _levelIndex = Mathf.Clamp(_level != null ? _levelIndex : Math.Max(0, demo), 0, Math.Max(0, _levelFiles.Length - 1));
            _level = _levelFiles.Length > 0 ? new JsonDoc<LevelData>(_levelFiles[_levelIndex]) : null;
        }

        private void OnGUI()
        {
            DrawToolbar();
            _tab = GUILayout.Toolbar(_tab, Tabs);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Search", GUILayout.Width(48));
                _search = EditorGUILayout.TextField(_search);
                if (GUILayout.Button("x", GUILayout.Width(22))) { _search = ""; GUI.FocusControl(null); }
            }
            EditorGUILayout.Space(2);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (Tabs[_tab])
            {
                case "Rules": DrawJson(_rules, "Global rules. A level can override any of them (Level tab, rulesOverride). Applies on the next Play or level load."); break;
                case "Level": DrawLevel(); break;
                case "Catalog": DrawJson(_catalog, "Appliances: load, size, use time. Applies on the next Play."); break;
                case "Strings": DrawJson(_strings, "Every message the game shows. Applies on the next Play."); break;
                case "Keys": DrawKeys(); break;
                default: DrawAsset(Tabs[_tab]); break;
            }
            EditorGUILayout.EndScrollView();
        }

        // ---- toolbar ---------------------------------------------------------------------

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(EditorApplication.isPlaying ? "Stop" : "Play", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    EditorApplication.isPlaying = !EditorApplication.isPlaying;
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("Restart level", EditorStyles.toolbarButton, GUILayout.Width(90))) RestartLevel();
                }
                GUILayout.FlexibleSpace();
                bool dirty = _rules.Dirty || _catalog.Dirty || _strings.Dirty || (_level != null && _level.Dirty);
                using (new EditorGUI.DisabledScope(!dirty))
                {
                    if (GUILayout.Button("Save all", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    {
                        _rules.Save(); _catalog.Save(); _strings.Save(); _level?.Save();
                    }
                }
            }
        }

        /// <summary>Play mode: rebuilds the current level from its (saved) JSON, through the game's own Restart.</summary>
        private static void RestartLevel()
        {
            var type = Type.GetType("Trouble.Game.LevelController, Trouble.Game");
            var controller = type != null ? FindFirstObjectByType(type) : null;
            type?.GetMethod("Restart")?.Invoke(controller, null);
        }

        // ---- JSON documents --------------------------------------------------------------

        private void DrawJson<T>(JsonDoc<T> doc, string help) where T : class, new()
        {
            EditorGUILayout.HelpBox(help, MessageType.None);
            DrawSaveRow(doc);
            if (doc.Data == null)
            {
                EditorGUILayout.HelpBox(doc.Path + " could not be read.", MessageType.Error);
                return;
            }
            if (DrawObject(doc.Data, doc.Path, 0)) doc.Dirty = true;
        }

        private void DrawLevel()
        {
            if (_levelFiles.Length == 0)
            {
                EditorGUILayout.HelpBox("No level files in " + LevelFolder, MessageType.Warning);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                int pick = EditorGUILayout.Popup("Level file", _levelIndex, _levelFiles.Select(Path.GetFileName).ToArray());
                if (pick != _levelIndex)
                {
                    if (_level != null && _level.Dirty && !EditorUtility.DisplayDialog("Unsaved level", "Discard the changes to " + Path.GetFileName(_level.Path) + "?", "Discard", "Keep editing"))
                        pick = _levelIndex;
                    _levelIndex = pick;
                    _level = new JsonDoc<LevelData>(_levelFiles[_levelIndex]);
                }
                if (GUILayout.Button("Rescan", GUILayout.Width(60))) RefreshLevels();
            }
            DrawJson(_level, "The whole level: rooms (capRandom, capMin, capMax roll each room's capacity), sockets, appliances, and day: seed (0 = new every run), hearts, request timing, urgencies and people. Save, then Restart level in Play mode.");
        }

        private static void DrawSaveRow<T>(JsonDoc<T> doc) where T : class, new()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(Path.GetFileName(doc.Path) + (doc.Dirty ? "  (changed)" : ""), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!doc.Dirty))
                {
                    if (GUILayout.Button("Save", GUILayout.Width(60))) doc.Save();
                    if (GUILayout.Button("Revert", GUILayout.Width(60))) doc.Load();
                }
            }
        }

        /// <summary>Draws every public field of <paramref name="obj"/>; true if anything changed.</summary>
        private bool DrawObject(object obj, string path, int depth)
        {
            bool changed = false;
            foreach (var f in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.IsInitOnly || f.IsNotSerialized) continue;
                string key = path + "/" + f.Name;
                if (!IsComplex(f.FieldType) && !Matches(f.Name)) continue;
                var value = f.GetValue(obj);
                var next = DrawValue(Nicify(f.Name), f.Name, f.FieldType, value, key, depth, out bool fieldChanged);
                if (fieldChanged)
                {
                    f.SetValue(obj, next);
                    changed = true;
                }
            }
            return changed;
        }

        private object DrawValue(string label, string name, Type type, object value, string key, int depth, out bool changed)
        {
            changed = false;
            EditorGUI.BeginChangeCheck();
            object next = value;
            if (type == typeof(int)) next = EditorGUILayout.IntField(label, (int)value);
            else if (type == typeof(float)) next = EditorGUILayout.FloatField(label, (float)value);
            else if (type == typeof(double)) next = EditorGUILayout.DoubleField(label, (double)value);
            else if (type == typeof(bool)) next = EditorGUILayout.Toggle(label, (bool)value);
            else if (type == typeof(string))
            {
                string s = (string)value ?? "";
                if (Choices.TryGetValue(name, out var options))
                {
                    int i = Array.IndexOf(options, s);
                    int pick = EditorGUILayout.Popup(label, i < 0 ? 0 : i, options);
                    next = i < 0 && pick == 0 ? s : options[pick];
                }
                else next = EditorGUILayout.TextField(label, s);
            }
            else if (type.IsEnum) next = EditorGUILayout.EnumPopup(label, (Enum)value);
            else if (type == typeof(Vector2)) next = EditorGUILayout.Vector2Field(label, (Vector2)value);
            else if (type == typeof(Color)) next = EditorGUILayout.ColorField(label, (Color)value);
            else if (typeof(IList).IsAssignableFrom(type) && type.IsGenericType)
            {
                EditorGUI.EndChangeCheck();
                next = DrawList(label, type, (IList)value, key, depth, out changed);
                return next;
            }
            else if (type.IsClass && type.GetCustomAttribute<SerializableAttribute>() != null)
            {
                EditorGUI.EndChangeCheck();
                if (value == null) value = Activator.CreateInstance(type);
                if (Foldout(key, label, depth) || _search.Length > 0)
                {
                    EditorGUI.indentLevel++;
                    changed = DrawObject(value, key, depth + 1);
                    EditorGUI.indentLevel--;
                }
                return value;
            }
            else EditorGUILayout.LabelField(label, "(" + type.Name + ")");
            changed = EditorGUI.EndChangeCheck();
            return next;
        }

        private IList DrawList(string label, Type type, IList list, string key, int depth, out bool changed)
        {
            changed = false;
            var item = type.GetGenericArguments()[0];
            if (list == null) { list = (IList)Activator.CreateInstance(type); changed = true; }
            if (!Foldout(key, label + "  [" + list.Count + "]", depth) && _search.Length == 0) return list;
            EditorGUI.indentLevel++;
            int remove = -1;
            for (int i = 0; i < list.Count; i++)
            {
                string itemLabel = ItemName(list[i], i);
                if (IsComplex(item))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool open = Foldout(key + "/" + i, itemLabel, depth + 1);
                        if (GUILayout.Button("-", GUILayout.Width(22))) remove = i;
                        if (!open && _search.Length == 0) continue;
                    }
                    EditorGUI.indentLevel++;
                    var element = list[i] ?? Activator.CreateInstance(item);
                    if (DrawObject(element, key + "/" + i, depth + 2)) { list[i] = element; changed = true; }
                    EditorGUI.indentLevel--;
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var next = DrawValue(itemLabel, "", item, list[i], key + "/" + i, depth + 1, out bool c);
                        if (c) { list[i] = next; changed = true; }
                        if (GUILayout.Button("-", GUILayout.Width(22))) remove = i;
                    }
                }
            }
            if (remove >= 0) { list.RemoveAt(remove); changed = true; }
            if (GUILayout.Button("+ Add " + item.Name, GUILayout.Width(160)))
            {
                list.Add(item == typeof(string) ? "" : Activator.CreateInstance(item));
                _open.Add(key);
                changed = true;
            }
            EditorGUI.indentLevel--;
            return list;
        }

        private bool Foldout(string key, string label, int depth)
        {
            bool open = _open.Contains(key);
            bool now = EditorGUILayout.Foldout(open, label, true);
            if (now != open)
            {
                if (now) _open.Add(key);
                else _open.Remove(key);
            }
            return now;
        }

        private static string ItemName(object item, int i)
        {
            if (item == null) return "#" + i;
            if (item is string s) return "#" + i;
            foreach (var n in new[] { "id", "name", "key" })
            {
                var f = item.GetType().GetField(n);
                if (f != null && f.GetValue(item) is string v && v.Length > 0) return "#" + i + "  " + v;
            }
            return "#" + i;
        }

        private static bool IsComplex(Type t) =>
            (t.IsClass && t != typeof(string)) || (typeof(IList).IsAssignableFrom(t) && t != typeof(string));

        private bool Matches(string name) =>
            _search.Length == 0 || name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                                || Nicify(name).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Nicify(string name) => ObjectNames.NicifyVariableName(name);

        // ---- config assets -----------------------------------------------------------------

        private void DrawAsset(string tab)
        {
            var entry = ConfigAssets.First(a => a.tab == tab);
            var asset = LoadAsset(entry.file);
            if (asset == null)
            {
                EditorGUILayout.HelpBox(entry.file + ".asset does not exist yet, so the game uses built-in defaults.", MessageType.Info);
                if (GUILayout.Button("Create " + entry.file + ".asset")) CreateAsset(entry.type, entry.file);
                return;
            }
            EditorGUILayout.HelpBox(AssetDatabase.GetAssetPath(asset) + " — changes apply at once, also while playing.", MessageType.None);
            if (_search.Length == 0)
            {
                if (!_editors.TryGetValue(entry.file, out var editor) || editor == null || editor.target != asset)
                {
                    if (editor != null) DestroyImmediate(editor);
                    editor = UnityEditor.Editor.CreateEditor(asset);
                    _editors[entry.file] = editor;
                }
                editor.OnInspectorGUI();
                return;
            }
            var so = new SerializedObject(asset);
            var prop = so.GetIterator();
            bool any = false;
            for (bool enter = true; prop.NextVisible(enter); enter = false)
            {
                if (prop.name == "m_Script" || !Matches(prop.name)) continue;
                EditorGUILayout.PropertyField(prop, true);
                any = true;
            }
            if (!any) EditorGUILayout.LabelField("No field matches '" + _search + "'.");
            so.ApplyModifiedProperties();
        }

        private static ScriptableObject LoadAsset(string file) =>
            AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigFolder + "/" + file + ".asset");

        private static void CreateAsset(string typeName, string file)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
            if (type == null) return;
            var asset = CreateInstance(type);
            AssetDatabase.CreateAsset(asset, ConfigFolder + "/" + file + ".asset");
            AssetDatabase.SaveAssets();
        }

        // ---- keys ----------------------------------------------------------------------

        /// <summary>Every field of type InputSystem.Key in every config asset, in one list.</summary>
        private void DrawKeys()
        {
            EditorGUILayout.HelpBox("Every hotkey the game reads from its configs. Changes apply at once, also while playing.", MessageType.None);
            foreach (var entry in ConfigAssets)
            {
                var asset = LoadAsset(entry.file);
                if (asset == null) continue;
                var keys = asset.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => f.FieldType.FullName == "UnityEngine.InputSystem.Key" || (f.FieldType.IsArray && f.FieldType.GetElementType()?.FullName == "UnityEngine.InputSystem.Key"))
                    .Where(f => Matches(f.Name)).ToList();
                if (keys.Count == 0) continue;
                EditorGUILayout.LabelField(entry.tab, EditorStyles.boldLabel);
                var so = new SerializedObject(asset);
                foreach (var f in keys)
                {
                    var prop = so.FindProperty(f.Name);
                    if (prop != null) EditorGUILayout.PropertyField(prop, new GUIContent(Nicify(f.Name)), true);
                }
                so.ApplyModifiedProperties();
                EditorGUILayout.Space(4);
            }
        }

        /// <summary>A JSON file under Resources read into its data class, edited, and written back.</summary>
        private sealed class JsonDoc<T> where T : class, new()
        {
            public readonly string Path;
            public T Data;
            public bool Dirty;

            public JsonDoc(string path)
            {
                Path = path;
                Load();
            }

            public void Load()
            {
                Data = File.Exists(Path) ? JsonUtility.FromJson<T>(File.ReadAllText(Path)) : null;
                Dirty = false;
            }

            public void Save()
            {
                if (!Dirty || Data == null) return;
                File.WriteAllText(Path, JsonUtility.ToJson(Data, true));
                AssetDatabase.ImportAsset(Path);
                Dirty = false;
            }
        }
    }
}
