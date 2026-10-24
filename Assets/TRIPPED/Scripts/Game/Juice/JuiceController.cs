using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Trouble.Game
{
    /// <summary>
    /// The game-feel layer (G5). Listens to the <see cref="LevelController"/> and makes every important day-mode
    /// moment felt: the day sound table, footsteps, overload hum, appliance loops and music (through
    /// <see cref="FeedbackController"/>, so every play is in its PlayLog), plus overload pulses and sparks, the
    /// blackout (dark rooms, dimmed lights, flash, slow motion, shake) and its room-by-room relight, squash and
    /// stretch, carried-item bob, lean and stairs hop, appliance wobble, score popups and coins, and the last-30-s
    /// vignette. Never changes sim state. All numbers come from <see cref="JuiceConfig"/>.
    /// </summary>
    /// <remarks>
    /// Runs after the controller (Update, to see whether this frame's E was accepted) and after the house view
    /// (LateUpdate, to add its offsets on top of the poses the views just set).
    /// </remarks>
    [DefaultExecutionOrder(1000)]
    public sealed class JuiceController : MonoBehaviour
    {
        private sealed class RoomFx
        {
            public RoomState Room;
            public SpriteRenderer Red;
            public SpriteRenderer Dark;
            public int RelightIndex;
            public AudioSource Hum;
            public float SparkAccumulator;
            public float NextSparkSound;
        }

        private LevelController _controller;
        private TrippedViewConfig _config;
        private JuiceConfig _juice;
        private FeedbackController _feedback;
        private CameraShake _shake;
        private ScreenFx _screen;

        private HouseSim _sim;
        private HouseView _house;
        private JuiceFx _fx;
        private readonly List<RoomFx> _rooms = new List<RoomFx>();
        private readonly Dictionary<string, AudioSource> _applianceLoops = new Dictionary<string, AudioSource>();
        private readonly Dictionary<string, float> _applianceSquash = new Dictionary<string, float>();
        private readonly List<string> _scratchSounds = new List<string>();
        private readonly HashSet<SimEventType> _playedThisTick = new HashSet<SimEventType>();
        private readonly List<string> _stale = new List<string>();
        private readonly List<(Light light, float intensity)> _lights = new List<(Light, float)>();

        private AudioSource _musicA;
        private AudioSource _musicB;
        private string _musicWanted = "";
        private string _musicPlaying = "";
        private float _musicFade = 1f;

        private string _lastCarry = "";
        private bool _interactAccepted;
        private bool _climbAccepted;
        private int _combo;
        private float _lastCompleteTime = -100f;
        private float _stepTimer;
        private int _stepParity;
        private float _characterSquashAt = -100f;
        private float _lean;
        private bool _wasBlackout;
        private float _resetAt = -100f;
        private float _slowAt = -100f;
        private bool _slowing;
        private float _savedTimeScale = 1f;
        private float _savedSimScale = 1f;
        private LevelPhase _lastPhase;
        private Transform _carrySprite;
        private Transform _carryCard;
        private Vector3 _carrySpriteBase;
        private Vector3 _carryCardBase;

        /// <summary>The world effects of the current level (null between levels).</summary>
        public JuiceFx Fx => _fx;

        /// <summary>The camera shake component.</summary>
        public CameraShake Shake => _shake;

        /// <summary>The screen flash and vignette.</summary>
        public ScreenFx Screen => _screen;

        /// <summary>Highest overload fraction (0..1) of any room this frame.</summary>
        public float WorstOverload { get; private set; }

        /// <summary>Current combo of quick completions (0 = none yet).</summary>
        public int Combo => _combo;

        /// <summary>The music loop playing now ("" = none).</summary>
        public string MusicPlaying => _musicPlaying;

        /// <summary>Connects to the controller. Called by <see cref="FeedbackController.Setup"/>.</summary>
        public void Setup(LevelController controller, TrippedViewConfig config, JuiceConfig juice, FeedbackController feedback)
        {
            _controller = controller;
            _config = config;
            _juice = juice;
            _feedback = feedback;
            _shake = CameraShake.On(Camera.main, juice);

            var screenGo = new GameObject("JuiceScreenFx", typeof(RectTransform));
            screenGo.transform.SetParent(transform, false);
            _screen = screenGo.AddComponent<ScreenFx>();
            _screen.Build(juice);

            _musicA = feedback.MakeLoop(SynthBank.MusicDay);
            _musicB = feedback.MakeLoop(SynthBank.MusicDay);

            controller.LevelLoaded += OnLevelLoaded;
            controller.LevelUnloaded += OnLevelUnloaded;
            controller.TickEvents += OnTickEvents;
            controller.CommandAccepted += OnCommandAccepted;
        }

        // ---- level lifetime ----------------------------------------------------------------

        private void OnLevelLoaded(int level)
        {
            ClearLevel();
            _sim = _controller.Sim;
            _lastPhase = _sim != null ? _sim.Phase : LevelPhase.Planning;
            CaptureLights();
        }

        private void OnLevelUnloaded() => ClearLevel();

        private void ClearLevel()
        {
            EndSlowMo();
            RestoreLights();
            foreach (var r in _rooms)
                if (r.Hum != null) Destroy(r.Hum.gameObject);
            _rooms.Clear();
            foreach (var kv in _applianceLoops)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _applianceLoops.Clear();
            _applianceSquash.Clear();
            _house = null;
            _fx = null;
            _sim = null;
            _lastCarry = "";
            _combo = 0;
            _lastCompleteTime = -100f;
            _wasBlackout = false;
            _resetAt = -100f;
            _carrySprite = null;
            _carryCard = null;
            WorstOverload = 0f;
        }

        /// <summary>Builds the per-room overlays the first frame the house exists.</summary>
        private bool EnsureHouse()
        {
            if (_sim == null || _controller.Sim != _sim) return false;
            if (_house != null) return true;
            _house = _controller.House;
            if (_house == null) return false;
            _fx = _house.gameObject.AddComponent<JuiceFx>();
            _fx.Setup(_juice);

            var order = new List<RoomState>(_sim.Rooms);
            // Power comes back from the panel upward: basement first, then floor by floor, left to right.
            order.Sort((a, b) => a.Floor != b.Floor ? a.Floor.CompareTo(b.Floor) : a.X.CompareTo(b.X));
            var root = new GameObject("JuiceRooms").transform;
            root.SetParent(_house.transform, false);
            foreach (var room in _sim.Rooms)
            {
                var rect = HouseLayout.RoomRect(room, _config.roomHeight);
                var fx = new RoomFx
                {
                    Room = room,
                    RelightIndex = order.IndexOf(room),
                    Red = Overlay(root, "Overload " + room.Id, rect, _juice.overloadColor, _juice.fxSortingOrder - 3),
                    Dark = Overlay(root, "Dark " + room.Id, rect, _juice.blackoutRoomColor, _juice.fxSortingOrder - 2),
                };
                _rooms.Add(fx);
            }
            var character = _house.transform.Find("Character");
            if (character != null)
            {
                _carrySprite = character.Find("CarriedSprite");
                _carryCard = character.Find("Carrying");
                if (_carrySprite != null) _carrySpriteBase = _carrySprite.localPosition;
                if (_carryCard != null) _carryCardBase = _carryCard.localPosition;
            }
            return true;
        }

        private SpriteRenderer Overlay(Transform parent, string name, Rect rect, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(rect.center.x, rect.center.y, _juice.fxZ * 0.5f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.SoftRect;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = rect.size;
            sr.color = new Color(color.r, color.g, color.b, 0f);
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        // ---- lights ------------------------------------------------------------------------

        private void CaptureLights()
        {
            RestoreLights();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) _lights.Add((l, l.intensity));
        }

        private void SetLights(float factor)
        {
            for (int i = 0; i < _lights.Count; i++)
                if (_lights[i].light != null) _lights[i].light.intensity = _lights[i].intensity * factor;
        }

        private void RestoreLights()
        {
            SetLights(1f);
            _lights.Clear();
        }

        // ---- events -------------------------------------------------------------------------

        private void OnCommandAccepted(Command command)
        {
            var sim = _controller.Sim;
            if (sim == null || !sim.IsDayMode) return;
            if (command.Type == CommandType.Climb)
            {
                _climbAccepted = true;
                return;
            }
            if (command.Type != CommandType.Interact) return;
            _interactAccepted = true;
            string now = sim.Character.CarryingId ?? "";
            if (_lastCarry.Length == 0 && now.Length > 0)
            {
                _feedback.Play(SynthBank.Pickup);
                _characterSquashAt = Time.unscaledTime;
            }
            else if (_lastCarry.Length > 0 && now.Length == 0)
            {
                string plugged = _lastCarry;
                _feedback.Play(SynthBank.PlugIn);
                _characterSquashAt = Time.unscaledTime;
                _applianceSquash[plugged] = Time.unscaledTime;
                if (_shake != null) _shake.Kick(_juice.plugShake);
                var app = sim.FindAppliance(plugged);
                var socket = app != null && app.SocketId.Length > 0 ? sim.FindSocket(app.SocketId) : null;
                if (socket != null && _fx != null) _fx.Sparks(SocketPoint(sim, socket), 6, 0.5f);
            }
            _lastCarry = now;
        }

        private void OnTickEvents(IReadOnlyList<SimEvent> events)
        {
            var sim = _controller.Sim;
            if (sim == null || !sim.IsDayMode) return;
            EnsureHouse();
            _playedThisTick.Clear();
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                bool first = _playedThisTick.Add(e.Type);
                if (DaySounds.OncePerTick(e.Type) && !first) continue;
                React(sim, e);
            }
            TickSounds(sim);
        }

        private void React(HouseSim sim, SimEvent e)
        {
            _scratchSounds.Clear();
            var request = e.Appliance != null ? FindRequest(sim, e.Appliance, e.Room, e.Type == SimEventType.RequestCompleted) : null;
            var urgency = request != null ? DayRules.ParseUrgency(request.Urgency) : UrgencyLevel.Normal;
            DaySounds.For(e.Type, urgency, _scratchSounds);
            float now = Time.unscaledTime;

            switch (e.Type)
            {
                case SimEventType.RequestSpawned:
                    PlayAll(1f);
                    if (urgency == UrgencyLevel.Urgent || Random.value < _juice.heyChance) _feedback.Play(SynthBank.VoiceHey);
                    return;
                case SimEventType.RequestCompleted:
                {
                    _combo = JuiceMath.NextCombo(_combo, _lastCompleteTime, now, _juice.comboWindow);
                    _lastCompleteTime = now;
                    float pitch = JuiceMath.ComboPitch(_combo, _juice.comboPitchStep, _juice.comboPitchMax);
                    PlayAll(pitch);
                    if (_combo >= 2) _feedback.Play(SynthBank.ComboUp, pitch, 1f);
                    _feedback.Play(SynthBank.VoiceHappy);
                    var app = sim.FindAppliance(e.Appliance);
                    if (app != null && app.Type == "toaster") _feedback.Play(SynthBank.ToasterPop);
                    _applianceSquash[e.Appliance] = now;
                    var at = RoomTop(sim, e.Room);
                    int points = request != null ? request.Points : 0;
                    if (_fx != null)
                    {
                        string text = "+" + points + (_combo >= 2 ? "  x" + _combo : "");
                        _fx.Text(at, text, _juice.scorePopupColor);
                        _fx.Coins(at, _juice.coinBurst);
                    }
                    return;
                }
                case SimEventType.RequestMissed:
                    PlayAll(1f);
                    _feedback.Play(SynthBank.VoiceAngry);
                    _combo = 0;
                    if (_fx != null) _fx.Text(RoomTop(sim, e.Room), _juice.missPopupText, _juice.missPopupColor);
                    return;
                case SimEventType.HeartLost:
                {
                    PlayAll(1f);
                    if (_shake != null) _shake.Kick(_juice.heartShake);
                    if (_fx != null)
                    {
                        string heart = _fx.FontHas('♥') ? "♥" : "HEART";
                        var p = CharacterPoint(sim) + new Vector2(0f, _config.playerWorldHeight + 0.6f);
                        _fx.Text(p, string.Format(_juice.heartPopupText, heart), _juice.heartPopupColor);
                    }
                    return;
                }
                case SimEventType.CircuitTripped:
                case SimEventType.MainTripped:
                    PlayAll(1f);
                    StartBlackout(sim, e.Circuit);
                    return;
                case SimEventType.BreakerSwitched:
                    PlayAll(1f);
                    if (_resetAt < 0f) _resetAt = now;
                    if (_fx != null) _fx.Sparks(PanelPoint(sim), _juice.panelSparkBurst / 3, 0.6f);
                    return;
                case SimEventType.LevelWon:
                    PlayAll(1f);
                    StopLoops();
                    if (_fx != null) _fx.Coins(CharacterPoint(sim) + new Vector2(0f, 1.5f), _juice.coinBurst * 3);
                    return;
                case SimEventType.LevelLost:
                    PlayAll(1f);
                    StopLoops();
                    return;
                default:
                    PlayAll(1f);
                    return;
            }
        }

        private void PlayAll(float pitch)
        {
            for (int i = 0; i < _scratchSounds.Count; i++) _feedback.Play(_scratchSounds[i], pitch, 1f);
        }

        private void StartBlackout(HouseSim sim, string circuit)
        {
            if (_shake != null) _shake.Kick(_juice.blackoutShake);
            if (_screen != null) _screen.Flash(_juice.flashColor);
            StartSlowMo();
            if (_fx == null) return;
            _fx.Sparks(PanelPoint(sim), _juice.panelSparkBurst, 1.4f);
            foreach (var s in sim.Sockets)
                if (s.RoomId == circuit && s.OccupantId.Length > 0) _fx.Sparks(SocketPoint(sim, s), _juice.panelSparkBurst / 3, 1.2f);
        }

        /// <summary>Once-per-tick sounds: urgent ticking and the end-of-day countdown.</summary>
        private void TickSounds(HouseSim sim)
        {
            if (sim.Phase != LevelPhase.Running) return;
            int tps = sim.Rules.ticksPerSecond;
            for (int r = 0; r < sim.Requests.Count; r++)
            {
                var req = sim.Requests[r];
                if (!req.IsActive || req.ProgressTicks > 0 || DayRules.ParseUrgency(req.Urgency) != UrgencyLevel.Urgent) continue;
                if (!JuiceMath.UrgentTickDue(sim.Now, req.DueTicks, tps, _juice.urgentTickSeconds, _juice.urgentFastTickSeconds)) continue;
                bool fast = JuiceMath.UrgentTickFast(sim.Now, req.DueTicks, tps, _juice.urgentFastTickSeconds);
                _feedback.Play(SynthBank.UrgentTick, fast ? _juice.urgentFastTickPitch : 1f, 1f);
                break;
            }
            int left = DayRules.TicksLeft(sim.DayLengthTicks, sim.Now);
            if (tps > 0 && left > 0 && left <= _juice.countdownSeconds * tps && left % tps == 0) _feedback.Play(SynthBank.CountdownBeep);
        }

        private static RequestState FindRequest(HouseSim sim, string appliance, string room, bool done)
        {
            RequestState best = null;
            for (int i = 0; i < sim.Requests.Count; i++)
            {
                var r = sim.Requests[i];
                if (r.ApplianceId != appliance || (room != null && r.RoomId != room)) continue;
                if (done && !r.Done) continue;
                if (best == null || r.Serial > best.Serial) best = r;
            }
            return best;
        }

        // ---- slow motion --------------------------------------------------------------------

        private void StartSlowMo()
        {
            if (!_slowing)
            {
                _savedTimeScale = Time.timeScale;
                _savedSimScale = _controller.TimeScale;
            }
            _slowing = true;
            _slowAt = Time.unscaledTime;
        }

        private void UpdateSlowMo()
        {
            if (!_slowing) return;
            float k = JuiceMath.SlowMoScale(Time.unscaledTime - _slowAt, _juice.slowMoSeconds, _juice.slowMoScale);
            if (k >= 1f)
            {
                EndSlowMo();
                return;
            }
            Time.timeScale = _savedTimeScale * k;
            _controller.TimeScale = _savedSimScale * k;
        }

        private void EndSlowMo()
        {
            if (!_slowing) return;
            _slowing = false;
            Time.timeScale = _savedTimeScale;
            if (_controller != null) _controller.TimeScale = _savedSimScale;
        }

        // ---- per frame -----------------------------------------------------------------------

        private void Update()
        {
            UpdateSlowMo();
            var sim = _controller != null ? _controller.Sim : null;
            if (sim != null && sim.IsDayMode && sim.Phase == LevelPhase.Running && !_controller.KeysSuspended)
                DetectRejects(sim);
            _interactAccepted = false;
            _climbAccepted = false;
        }

        /// <summary>
        /// Day keys act at once; a refused key never becomes a sim event, so it is caught here: the key went down this
        /// frame and the controller (which ran first) did not get the command accepted.
        /// </summary>
        private void DetectRejects(HouseSim sim)
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.eKey.wasPressedThisFrame && !_interactAccepted)
            {
                _feedback.Play(SynthBank.Reject);
                if (_shake != null) _shake.Kick(_juice.rejectShake);
            }
            bool climbKey = kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame
                || kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame;
            if (climbKey && !_climbAccepted && !sim.Character.IsClimbing) _feedback.Play(SynthBank.Reject, 1.25f, 0.5f);
        }

        private void LateUpdate()
        {
            var sim = _controller != null ? _controller.Sim : null;
            UpdateMusic(sim);
            if (sim == null || !sim.IsDayMode || !EnsureHouse())
            {
                if (_screen != null) _screen.VignetteAlpha = 0f;
                return;
            }

            float now = Time.unscaledTime;
            if (sim.Phase == LevelPhase.Running && _lastPhase == LevelPhase.Planning) _feedback.Play(SynthBank.DayStart);
            _lastPhase = sim.Phase;
            if (sim.Phase != LevelPhase.Running && sim.Phase != LevelPhase.Paused) _lastCarry = sim.Character.CarryingId ?? "";

            bool running = sim.Phase == LevelPhase.Running;
            UpdateRooms(sim, now, running);
            UpdateBlackout(sim, now);
            UpdateFootsteps(sim, running);
            UpdateAppliances(sim, now, running);
            UpdateCharacter(sim, now, running);

            float stretch = JuiceMath.FinalStretch(DayRules.TicksLeft(sim.DayLengthTicks, sim.Now), sim.Rules.ticksPerSecond, _juice.finalSeconds);
            float pulse = JuiceMath.Pulse(now, _juice.vignettePulseHz);
            float vignette = stretch * _juice.vignetteAlphaMax * (0.5f + 0.5f * pulse);
            vignette = Mathf.Max(vignette, WorstOverload * _juice.vignetteOverloadAlpha * pulse);
            if (sim.Phase == LevelPhase.Won || sim.Phase == LevelPhase.Lost) vignette = 0f;
            _screen.VignetteAlpha = vignette;
        }

        private void UpdateRooms(HouseSim sim, float now, bool running)
        {
            float worst = 0f;
            int trip = sim.Rules.overTripTicks;
            foreach (var r in _rooms)
            {
                var c = sim.FindCircuit(r.Room.Id);
                bool over = running && c != null && c.IsOn && c.Overloading;
                float frac = over ? Mathf.Max(0.05f, JuiceMath.OverloadFraction(c.OverTicks, trip)) : 0f;
                if (over)
                {
                    worst = Mathf.Max(worst, frac);
                    float hz = Mathf.Lerp(_juice.overloadPulseMinHz, _juice.overloadPulseMaxHz, frac);
                    float a = Mathf.Lerp(_juice.overloadAlphaMin, _juice.overloadAlphaMax, JuiceMath.Pulse(now, hz));
                    var col = _juice.overloadColor;
                    col.a = a * Mathf.Lerp(0.6f, 1f, frac);
                    r.Red.color = col;
                    if (!r.Red.enabled) r.Red.enabled = true;

                    r.SparkAccumulator += _juice.overloadSparksPerSecond * (0.3f + frac) * Time.unscaledDeltaTime;
                    int n = JuiceMath.TakeWhole(ref r.SparkAccumulator);
                    if (n > 0 && _fx != null)
                        foreach (var s in sim.Sockets)
                            if (s.RoomId == r.Room.Id && s.OccupantId.Length > 0) _fx.Sparks(SocketPoint(sim, s), n, 0.6f + frac);
                    if (now >= r.NextSparkSound)
                    {
                        _feedback.Play(SynthBank.Spark, Random.Range(0.85f, 1.25f), 0.5f + frac * 0.5f);
                        r.NextSparkSound = now + _juice.sparkSoundInterval * Random.Range(0.6f, 1.4f) * (1.3f - frac);
                    }

                    if (r.Hum == null)
                    {
                        r.Hum = _feedback.MakeLoop(SynthBank.OverloadHum);
                        r.Hum.Play();
                        _feedback.LogPlay(SynthBank.OverloadHum);
                    }
                    r.Hum.mute = _feedback.Muted;
                    r.Hum.pitch = JuiceMath.HumPitch(c.OverTicks, trip, _juice.humPitchMin, _juice.humPitchMax);
                    r.Hum.volume = JuiceMath.ByOverload(c.OverTicks, trip, _juice.humVolumeMin, _juice.humVolumeMax) * _feedback.VolumeFor(SynthBank.OverloadHum);
                }
                else
                {
                    if (r.Red.enabled) r.Red.enabled = false;
                    r.SparkAccumulator = 0f;
                    if (r.Hum != null)
                    {
                        Destroy(r.Hum.gameObject);
                        r.Hum = null;
                    }
                }
            }
            WorstOverload = worst;
            if (_shake != null && worst > 0f)
                _shake.Steady = Mathf.Max(_shake.Steady, Mathf.Lerp(_juice.overloadShakeMin, _juice.overloadShakeMax, worst * worst));
        }

        private void UpdateBlackout(HouseSim sim, float now)
        {
            bool dark = DayRules.IsBlackout(sim);
            if (dark) _resetAt = -100f;
            if (dark != _wasBlackout && !dark && _resetAt < 0f) _resetAt = now;
            _wasBlackout = dark;

            float since = now - _resetAt;
            float relightEnd = (_rooms.Count - 1) * _juice.relightStagger + _juice.relightFlicker;
            bool relighting = !dark && _resetAt >= 0f && since < relightEnd;
            int lit = 0;
            foreach (var r in _rooms)
            {
                float d = dark ? 1f
                    : relighting ? JuiceMath.RelightDarkness(since, r.RelightIndex, _juice.relightStagger, _juice.relightFlicker, _juice.flickerHz)
                    : 0f;
                if (d <= 0f) lit++;
                // During the relight the house overlay is already gone, so a dark room is drawn fully dark here.
                float alpha = dark ? _juice.blackoutRoomAlpha : d * _juice.relightDarkAlpha;
                var col = _juice.blackoutRoomColor;
                col.a = alpha;
                r.Dark.color = col;
                bool show = alpha > 0.001f;
                if (r.Dark.enabled != show) r.Dark.enabled = show;
            }
            float factor = dark ? _juice.blackoutLightFactor
                : relighting && _rooms.Count > 0 ? Mathf.Lerp(_juice.blackoutLightFactor, 1f, lit / (float)_rooms.Count)
                : 1f;
            SetLights(factor);
        }

        private void UpdateFootsteps(HouseSim sim, bool running)
        {
            var ch = sim.Character;
            bool moving = running && (ch.WalkDir != 0 || ch.IsClimbing);
            if (!moving)
            {
                _stepTimer = 0f;
                return;
            }
            _stepTimer -= Time.unscaledDeltaTime;
            if (_stepTimer > 0f) return;
            _stepTimer = ch.IsClimbing ? _juice.climbStepInterval : _juice.stepInterval;
            _stepParity ^= 1;
            float pitch = 1f + (_stepParity == 0 ? -_juice.stepPitchSpread : _juice.stepPitchSpread);
            _feedback.Play(ch.IsClimbing ? SynthBank.StairsStep : SynthBank.Step, pitch, 1f);
        }

        private void UpdateAppliances(HouseSim sim, float now, bool running)
        {
            _stale.Clear();
            foreach (var kv in _applianceLoops) _stale.Add(kv.Key);
            foreach (var app in sim.Appliances)
            {
                var view = _house.FindApplianceView(app.Id);
                bool working = running && IsWorking(sim, app);
                if (working)
                {
                    _stale.Remove(app.Id);
                    string loop = SynthBank.LoopOf(app.Type);
                    if (!_applianceLoops.TryGetValue(app.Id, out var src) && _feedback.Bank != null && _feedback.Bank.Has(loop))
                    {
                        src = _feedback.MakeLoop(loop);
                        src.Play();
                        _applianceLoops[app.Id] = src;
                        _feedback.LogPlay(loop);
                    }
                    if (src != null)
                    {
                        src.mute = _feedback.Muted;
                        src.volume = _feedback.VolumeFor(loop);
                    }
                }
                if (view == null) continue;
                var tr = view.transform;
                float sx = 1f, sy = 1f, rot = 0f;
                if (_applianceSquash.TryGetValue(app.Id, out float at))
                {
                    JuiceMath.Squash(now - at, _juice.squashSeconds, _juice.squashAmount, out sx, out sy);
                    if (now - at >= _juice.squashSeconds) _applianceSquash.Remove(app.Id);
                }
                if (working)
                {
                    rot = Mathf.Sin(now * _juice.wobbleHz * Mathf.PI * 2f) * _juice.wobbleDegrees;
                    float p = 1f + _juice.workPulse * JuiceMath.Pulse(now, _juice.wobbleHz * 0.5f);
                    sx *= p;
                    sy *= p;
                }
                tr.localScale = new Vector3(sx, sy, 1f);
                tr.localRotation = Quaternion.Euler(0f, 0f, rot);
            }
            foreach (var id in _stale)
            {
                if (_applianceLoops[id] != null) Destroy(_applianceLoops[id].gameObject);
                _applianceLoops.Remove(id);
            }
        }

        /// <summary>Powered, in a socket of the room that wants it, and that request is still open.</summary>
        private static bool IsWorking(HouseSim sim, ApplianceState app)
        {
            if (!app.Powered || app.SocketId.Length == 0) return false;
            var socket = sim.FindSocket(app.SocketId);
            if (socket == null) return false;
            for (int i = 0; i < sim.Requests.Count; i++)
            {
                var r = sim.Requests[i];
                if (r.IsActive && r.ApplianceId == app.Id && r.RoomId == socket.RoomId) return true;
            }
            return false;
        }

        private void UpdateCharacter(HouseSim sim, float now, bool running)
        {
            var character = _house.transform.Find("Character");
            if (character == null) return;
            var ch = sim.Character;
            float target = running && !ch.IsClimbing ? -ch.WalkDir * _juice.leanDegrees : 0f;
            _lean = Mathf.MoveTowards(_lean, target, _juice.leanSpeed * _juice.leanDegrees * Time.unscaledDeltaTime);
            character.localRotation = Quaternion.Euler(0f, 0f, _lean);

            JuiceMath.Squash(now - _characterSquashAt, _juice.squashSeconds, _juice.squashAmount, out float sx, out float sy);
            character.localScale = new Vector3(sx, sy, 1f);

            if (running && ch.IsClimbing)
                character.localPosition += new Vector3(0f, Mathf.Abs(Mathf.Sin(now * _juice.stairsHopHz * Mathf.PI)) * _juice.stairsHopHeight, 0f);

            float bob = Mathf.Sin(now * _juice.carryBobHz * Mathf.PI * 2f) * _juice.carryBobHeight;
            if (_carrySprite != null) _carrySprite.localPosition = _carrySpriteBase + new Vector3(0f, bob, 0f);
            if (_carryCard != null) _carryCard.localPosition = _carryCardBase + new Vector3(0f, bob, 0f);
        }

        // ---- music -----------------------------------------------------------------------------

        private void UpdateMusic(HouseSim sim)
        {
            if (_feedback.Bank == null) return;
            string wanted;
            float pitch = 1f;
            float duck = 1f;
            if (sim == null) wanted = SynthBank.MusicMenu;
            else if (sim.Phase == LevelPhase.Won || sim.Phase == LevelPhase.Lost) wanted = "";
            else
            {
                wanted = SynthBank.MusicDay;
                if (sim.IsDayMode)
                {
                    float stretch = JuiceMath.FinalStretch(DayRules.TicksLeft(sim.DayLengthTicks, sim.Now), sim.Rules.ticksPerSecond, _juice.finalSeconds);
                    if (stretch > 0f)
                    {
                        // A real final track if one was supplied, or the generated one when the day track is generated
                        // too; a supplied day track without a final one is sped up instead.
                        bool swap = _feedback.Bank.IsFromFile(SynthBank.MusicDayFinal) || !_feedback.Bank.IsFromFile(SynthBank.MusicDay);
                        if (swap) wanted = SynthBank.MusicDayFinal;
                        else pitch = JuiceMath.MusicPitch(stretch, _juice.finalMusicPitch);
                    }
                    if (DayRules.IsBlackout(sim)) duck = _juice.blackoutMusicDuck;
                }
                if (sim.Phase == LevelPhase.Paused || sim.Phase == LevelPhase.Planning) duck *= _juice.blackoutMusicDuck;
            }

            if (wanted != _musicWanted)
            {
                _musicWanted = wanted;
                // Crossfade: the old track moves to B and fades out; the new one starts on A.
                var old = _musicA;
                _musicA = _musicB;
                _musicB = old;
                _musicFade = 0f;
                if (wanted.Length > 0)
                {
                    _musicA.clip = _feedback.Bank.Pick(wanted);
                    _musicA.volume = 0f;
                    _musicA.Play();
                    _feedback.LogPlay(wanted);
                }
                else _musicA.Stop();
                _musicPlaying = wanted;
            }
            _musicFade = Mathf.MoveTowards(_musicFade, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, _juice.musicFade));
            float full = wanted.Length > 0 ? _feedback.VolumeFor(wanted) * duck : 0f;
            _musicA.mute = _feedback.Muted;
            _musicB.mute = _feedback.Muted;
            _musicA.volume = full * _musicFade;
            _musicA.pitch = pitch;
            _musicB.volume = _musicB.isPlaying ? _musicB.volume * (1f - _musicFade) : 0f;
            if (_musicFade >= 1f && _musicB.isPlaying) _musicB.Stop();
        }

        private void StopLoops()
        {
            foreach (var r in _rooms)
                if (r.Hum != null)
                {
                    Destroy(r.Hum.gameObject);
                    r.Hum = null;
                }
            foreach (var kv in _applianceLoops)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _applianceLoops.Clear();
        }

        // ---- positions ---------------------------------------------------------------------------

        private Vector2 SocketPoint(HouseSim sim, SocketState s)
        {
            var room = sim.FindRoom(s.RoomId);
            if (room == null) return Vector2.zero;
            return new Vector2(HouseLayout.SocketX(room.X, room.Width, s.Pos), HouseLayout.FloorY(room.Floor, _config.roomHeight) + _config.socketHeight);
        }

        private Vector2 PanelPoint(HouseSim sim)
        {
            var room = sim.FindRoom(sim.PanelRoomId);
            if (room == null) return CharacterPoint(sim);
            return new Vector2(sim.PanelX, HouseLayout.FloorY(room.Floor, _config.roomHeight) + _config.panelMountHeight + _config.panelWorldHeight * 0.5f);
        }

        private Vector2 RoomTop(HouseSim sim, string roomId)
        {
            var room = roomId != null ? sim.FindRoom(roomId) : null;
            if (room == null) return CharacterPoint(sim) + new Vector2(0f, _config.playerWorldHeight);
            return new Vector2(room.CenterX, HouseLayout.FloorY(room.Floor, _config.roomHeight) + _config.roomHeight * 0.6f);
        }

        private Vector2 CharacterPoint(HouseSim sim)
        {
            return HouseLayout.CharacterPoint(sim, _controller.TickFraction, _config.roomHeight, _config.stairsHalfRun, _config.stairsLanding);
        }

        private void OnDestroy()
        {
            EndSlowMo();
            RestoreLights();
            if (_controller == null) return;
            _controller.LevelLoaded -= OnLevelLoaded;
            _controller.LevelUnloaded -= OnLevelUnloaded;
            _controller.TickEvents -= OnTickEvents;
            _controller.CommandAccepted -= OnCommandAccepted;
        }
    }
}
