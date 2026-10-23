#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.IO;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// The in-game tuning panel (GDD v1.1 §4.4), opened with F1. Only exists in the Editor and in Development
    /// builds. Edit any rule and press Apply &amp; Restart; the changes become this level's rulesOverride for the
    /// session (rules.json on disk is untouched until Export). Also debug toggles, time scale, jump to tick,
    /// the last 30 sim events and the Record box.
    /// </summary>
    /// <remarks>
    /// Drawn with IMGUI so it adds nothing to the player-facing uGUI. A transparent uGUI blocker behind the panel
    /// stops clicks on the panel from reaching the house. Its texts are English literals on purpose: it is a
    /// developer tool, not player-facing.
    /// </remarks>
    public sealed class TuningPanel : MonoBehaviour
    {
        private const float PanelWidth = 500f;

        private LevelController _controller;
        private FeedbackController _feedback;
        private TrippedViewConfig _config;
        private RulesConfig _rules;
        private readonly Dictionary<string, string> _buffers = new Dictionary<string, string>();
        private Vector2 _scroll;
        private bool _open;
        private bool _showWiring;
        private string _message = "";
        private string _jumpText = "100";
        private string _recordText = "";
        private GameObject _blocker;
        private RectTransform _blockerRect;
        private GUIStyle _wireStyle;
        private GUIStyle _bannerStyle;

        /// <summary>The panel is open.</summary>
        public bool IsOpen => _open;

        /// <summary>The true wiring overlay is on.</summary>
        public bool ShowWiring => _showWiring;

        /// <summary>Text in the Record box (after pressing Record).</summary>
        public string RecordText => _recordText;

        /// <summary>Connects the panel. Call once after the controller and feedback are set up.</summary>
        public void Setup(LevelController controller, FeedbackController feedback, TrippedViewConfig config)
        {
            _controller = controller;
            _feedback = feedback;
            _config = config;
            controller.LevelLoaded += OnLevelLoaded;

            var canvasGo = new GameObject("TuningPanelBlocker", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            canvasGo.AddComponent<GraphicRaycaster>();
            var rt = UIFactory.Anchored(canvasGo.transform, "Blocker", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(PanelWidth, 1080f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
            _blockerRect = rt;
            _blocker = canvasGo;
            _blocker.SetActive(false);
        }

        /// <summary>Opens or closes the panel.</summary>
        public void SetOpen(bool open)
        {
            if (open == _open) return;
            _open = open;
            _blocker.SetActive(open);
            if (open) LoadRules();
            else
            {
                GUI.FocusControl("");
                _controller.KeysSuspended = false;
            }
        }

        /// <summary>Applies the edited rules to the current level as its rulesOverride and restarts it.</summary>
        /// <returns>false (with <see cref="_message"/> set) if a value cannot be read or no level is loaded.</returns>
        public bool ApplyAndRestart()
        {
            if (_controller.Sim == null)
            {
                _message = "No level is loaded.";
                return false;
            }
            if (!ParseEdited(out var edited)) return false;
            _controller.Loader.OverrideReplacements[_controller.LevelIndex] = RulesEditing.Diff(edited, _controller.Loader.Rules);
            _controller.Restart();
            _message = "Applied to level " + _controller.LevelIndex + " and restarted.";
            return true;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) SetOpen(!_open);
            if (_open && _blockerRect != null)
                _blockerRect.sizeDelta = new Vector2(PanelWidth * Scale(), Screen.height);
            if (!_open) _controller.KeysSuspended = false;
        }

        private void OnLevelLoaded(int level)
        {
            if (_open) LoadRules();
        }

        private void LoadRules()
        {
            _rules = _controller.Sim != null ? _controller.Sim.Rules.Clone() : _controller.Loader.Rules.Clone();
            _buffers.Clear();
            foreach (var f in RulesEditing.Fields()) _buffers[f.Name] = RulesEditing.ValueText(_rules, f);
        }

        private bool ParseEdited(out RulesConfig edited)
        {
            edited = _controller.Loader.Rules.Clone();
            foreach (var f in RulesEditing.Fields())
            {
                if (!_buffers.TryGetValue(f.Name, out var text)) continue;
                if (!RulesEditing.TrySet(edited, f.Name, text, out var error))
                {
                    _message = error;
                    return false;
                }
            }
            return true;
        }

        /// <summary>IMGUI scale so the panel stays readable on large screens.</summary>
        private static float Scale() => Mathf.Max(1f, Screen.height / 900f);

        private void OnGUI()
        {
            if (_controller == null) return;
            if (_showWiring) DrawWiring();
            if (!_open) return;

            float s = Scale();
            var saved = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            var area = new Rect(Screen.width / s - PanelWidth, 0f, PanelWidth, Screen.height / s);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 6f, area.y + 6f, area.width - 12f, area.height - 12f));
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawContents();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = saved;

            if (Event.current.type == EventType.Repaint)
                _controller.KeysSuspended = GUI.GetNameOfFocusedControl().StartsWith("tp_");
        }

        private void DrawContents()
        {
            GUILayout.Label("F1 TUNING PANEL  (DEBUG ONLY)");
            var sim = _controller.Sim;
            GUILayout.Label(sim == null ? "No level loaded." : $"Level {_controller.LevelIndex}   tick {sim.Now}   {sim.Phase}   trips {sim.TripCount}");
            if (sim != null && sim.IsDayMode)
                GUILayout.Label($"DAY seed {sim.Seed}   hearts {sim.Hearts}   score {sim.Score}   done {sim.RequestsDone}   missed {sim.RequestsMissed}   next request tick {sim.NextRequestTick}");
            if (_message.Length > 0) GUILayout.Label(_message);

            GUILayout.Space(6f);
            GUILayout.Label("--- Rules (this level) ---");
            if (_rules == null) LoadRules();
            foreach (var f in RulesEditing.Fields())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(f.Name, GUILayout.Width(210f));
                if (f.FieldType == typeof(bool))
                {
                    bool current = _buffers[f.Name] == "true";
                    bool now = GUILayout.Toggle(current, current ? "true" : "false");
                    if (now != current) _buffers[f.Name] = now ? "true" : "false";
                }
                else
                {
                    GUI.SetNextControlName("tp_" + f.Name);
                    _buffers[f.Name] = GUILayout.TextField(_buffers[f.Name]);
                }
                GUILayout.EndHorizontal();
            }
            if (sim != null)
            {
                var file = new List<string>();
                foreach (var o in sim.Level.rulesOverride) file.Add(o.key + "=" + o.value);
                GUILayout.Label("Level file overrides: " + (file.Count == 0 ? "none" : string.Join(", ", file)));
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply & Restart")) ApplyAndRestart();
            if (GUILayout.Button("Reset to files"))
            {
                _controller.Loader.OverrideReplacements.Remove(_controller.LevelIndex);
                if (_controller.Sim != null) _controller.Restart();
                _message = "Back to the rules in the files.";
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Export rules (write rules.json)")) ExportRules();

            GUILayout.Space(6f);
            GUILayout.Label("--- Debug ---");
            _showWiring = GUILayout.Toggle(_showWiring, "DEBUG: show true wiring (circuit letter on every socket)");
            _feedback.Tips.Enabled = GUILayout.Toggle(_feedback.Tips.Enabled, "First-time tips");
            if (GUILayout.Button("Show tips again")) _feedback.Tips.Reset();
            bool muted = GUILayout.Toggle(_feedback.Muted, "Mute (M)");
            if (muted != _feedback.Muted) _feedback.SetMuted(muted);
            _feedback.LogToConsole = GUILayout.Toggle(_feedback.LogToConsole, "Log every sound to the Console");

            GUILayout.Label($"Time scale x{_controller.TimeScale:0.##}");
            _controller.TimeScale = GUILayout.HorizontalSlider(_controller.TimeScale, 0.5f, 8f);
            GUILayout.BeginHorizontal();
            foreach (float v in new[] { 0.5f, 1f, 2f, 4f, 8f })
                if (GUILayout.Button("x" + v)) _controller.TimeScale = v;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Jump to tick", GUILayout.Width(100f));
            GUI.SetNextControlName("tp_jump");
            _jumpText = GUILayout.TextField(_jumpText, GUILayout.Width(80f));
            if (GUILayout.Button("Jump")) JumpToTick();
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("--- Last 30 sim events ---");
            GUILayout.Label(_feedback.Events.Count == 0 ? "(none yet)" : string.Join("\n", _feedback.Events.Lines));

            GUILayout.Space(6f);
            GUILayout.Label("--- Record (commands played by hand) ---");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Record"))
                _recordText = sim != null ? Replay.ToScript(sim.Recorded) : "";
            if (GUILayout.Button("Copy")) GUIUtility.systemCopyBuffer = _recordText;
            GUILayout.EndHorizontal();
            GUI.SetNextControlName("tp_record");
            _recordText = GUILayout.TextArea(_recordText, GUILayout.MinHeight(70f));
        }

        private void JumpToTick()
        {
            var sim = _controller.Sim;
            if (sim == null || !int.TryParse(_jumpText, out int target))
            {
                _message = "Enter a tick number with a level loaded.";
                return;
            }
            if (sim.Phase == LevelPhase.Planning) _controller.StartOrTogglePause();
            if (sim.Phase == LevelPhase.Paused) sim.SetPaused(false);
            int steps = target - sim.Now;
            int done = steps > 0 ? _controller.StepTicks(steps) : 0;
            _message = $"Ran {done} ticks, now tick {sim.Now}.";
        }

        private void ExportRules()
        {
            if (!ParseEdited(out var edited)) return;
            string path = Application.isEditor
                ? Path.Combine(Application.dataPath, "TRIPPED/Resources/Config/rules.json")
                : Path.Combine(Application.persistentDataPath, "rules.json");
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(edited, true));
                _message = "Wrote " + path;
            }
            catch (System.Exception e)
            {
                _message = "Export failed: " + e.Message;
            }
        }

        /// <summary>Draws each socket's real circuit letter over it, clearly marked as debug.</summary>
        private void DrawWiring()
        {
            var sim = _controller.Sim;
            var house = _controller.House;
            var cam = Camera.main;
            if (sim == null || house == null || cam == null) return;
            if (_wireStyle == null)
            {
                _wireStyle = new GUIStyle(GUI.skin.box) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _bannerStyle = new GUIStyle(GUI.skin.box) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            var saved = GUI.color;
            GUI.color = new Color(1f, 0.3f, 0.3f, 1f);
            GUI.Box(new Rect(Screen.width / 2f - 230f, 8f, 460f, 34f), "DEBUG: TRUE WIRING (F1 to turn off)", _bannerStyle);
            foreach (var socket in sim.Sockets)
            {
                var view = house.FindSocketView(socket.Id);
                if (view == null) continue;
                var p = cam.WorldToScreenPoint(view.transform.position);
                if (p.z <= 0f) continue;
                GUI.color = _config.CircuitColor(socket.CircuitId);
                GUI.Box(new Rect(p.x - 18f, Screen.height - p.y - 18f, 36f, 36f), socket.CircuitId, _wireStyle);
            }
            GUI.color = saved;
        }

        private void OnDestroy()
        {
            if (_controller != null) _controller.LevelLoaded -= OnLevelLoaded;
        }
    }
}
#endif
