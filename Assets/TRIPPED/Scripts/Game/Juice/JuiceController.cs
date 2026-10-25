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
    /// stretch, carried-item bob, lean, jump, jetpack and landing, appliance wobble, score popups and coins, and the last-30-s
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
        private readonly Dictionary<string, string> _loopNames = new Dictionary<string, string>();
        private Transform _characterRoot;
        private readonly List<(Light light, float intensity)> _lights = new List<(Light, float)>();

        private AudioSource _musicA;
        private AudioSource _musicB;
        private string _musicWanted = "";
        private string _musicPlaying = "";
        private float _musicFade = 1f;

        private string _lastCarry = "";
        private bool _interactAccepted;
        private int _combo;
        private float _lastCompleteTime = -100f;
        private int _stepParity;
        private float _characterSquashAt = -100f;
        private float _hopAt = -100f;
        private bool _wasAirborne;
        private bool _wasJumping;
        private AudioSource _runLoop;
        private AudioSource _clockLoop;
        private float _puffTimer;
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

            // The clip is set when a track starts (UpdateMusic); not picking one here keeps the synthesis off the boot frame.
            _musicA = MusicSource(feedback);
            _musicB = MusicSource(feedback);

            controller.LevelLoaded += OnLevelLoaded;
            controller.LevelUnloaded += OnLevelUnloaded;
            controller.TickEvents += OnTickEvents;
            controller.CommandAccepted += OnCommandAccepted;
            controller.CommandRefused += OnCommandRefused;
            controller.Hopped += OnHopped;
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
            _characterRoot = null;
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
            _characterRoot = character;
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
            // Room lights follow their room's power themselves (RoomView); only the scene lights are dimmed here.
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.GetComponentInParent<RoomView>() == null) _lights.Add((l, l.intensity));
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

        /// <summary>Space with stairs: a hop in place (not while one is under way), with the jump sound.</summary>
        private void OnHopped()
        {
            if (Time.unscaledTime - _hopAt < _juice.hopSeconds) return;
            _hopAt = Time.unscaledTime;
            _feedback.Play(SynthBank.Jump);
        }

        /// <summary>A jump with nowhere to land, or a drop with no room below, gets the reject buzz instead of a toast.</summary>
        private void OnCommandRefused(Command command, string reason)
        {
            if (command.Type == CommandType.Jump || command.Type == CommandType.Drop) _feedback.Play(SynthBank.Reject);
        }

        private void OnCommandAccepted(Command command)
        {
            var sim = _controller.Sim;
            if (sim == null || !sim.IsDayMode) return;
            // F has its feedback on the PluggedIn / Unplugged events (React), which name the appliance.
            if (command.Type != CommandType.Interact && command.Type != CommandType.Throw) return;
            _interactAccepted = true;
            string now = sim.Character.CarryingId ?? "";
            if (_lastCarry.Length == 0 && now.Length > 0)
            {
                _feedback.Play(SynthBank.Pickup);
                _characterSquashAt = Time.unscaledTime;
            }
            else if (_lastCarry.Length > 0 && now.Length == 0)
            {
                var app = sim.FindAppliance(_lastCarry);
                _characterSquashAt = Time.unscaledTime;
                if (command.Type == CommandType.Throw && app != null)
                {
                    // Fly from above the character's head to where it landed.
                    var view = _controller.House != null ? _controller.House.FindApplianceView(app.Id) : null;
                    var feet = HouseLayout.CharacterPoint(sim, 0f, _config.roomHeight, _config.jumpArc);
                    if (view != null)
                        view.FlyFrom(new Vector3(feet.x, feet.y + _config.playerWorldHeight, _config.dayFrontZ), Time.unscaledTime);
                    _feedback.Play(SynthBank.Drop);
                }
                else if (app != null && !app.Unplugged && app.SocketId.Length > 0) PlugJuice(sim, app);
                else
                {
                    // Put down unplugged: a set-down, no sparks.
                    _feedback.Play(SynthBank.Drop);
                    if (app != null) _applianceSquash[app.Id] = Time.unscaledTime;
                }
            }
            _lastCarry = now;
        }

        /// <summary>Plug-in feedback: the plug sound, a squash, a small shake and sparks at the socket.</summary>
        private void PlugJuice(HouseSim sim, ApplianceState app)
        {
            _feedback.Play(SynthBank.PlugIn);
            _characterSquashAt = Time.unscaledTime;
            _applianceSquash[app.Id] = Time.unscaledTime;
            if (_shake != null) _shake.Kick(_juice.plugShake);
            var socket = app.SocketId.Length > 0 ? sim.FindSocket(app.SocketId) : null;
            if (socket != null && _fx != null) _fx.Sparks(SocketPoint(sim, socket), 6, 0.5f);
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
                case SimEventType.PluggedIn:
                {
                    var plugged = sim.FindAppliance(e.Appliance);
                    if (plugged != null) PlugJuice(sim, plugged);
                    break;
                }
                case SimEventType.Unplugged:
                    _feedback.Play(SynthBank.Pickup);
                    if (e.Appliance != null) _applianceSquash[e.Appliance] = now;
                    break;
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

        private readonly List<float> _voiceAt = new List<float>();

        private void StartBlackout(HouseSim sim, string circuit)
        {
            // The family complains, one after another (a little apart so the voices do not stack).
            _voiceAt.Clear();
            int people = sim.Level.day?.people?.Count ?? 0;
            for (int p = 0; p < people && p < 4; p++) _voiceAt.Add(Time.unscaledTime + _juice.familyVoiceDelay + p * _juice.familyVoiceGap);
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
            bool ticking = false, fast = false;
            int tps = sim.Rules.ticksPerSecond;
            for (int r = 0; sim.Phase == LevelPhase.Running && r < sim.Requests.Count; r++)
            {
                var req = sim.Requests[r];
                if (!req.IsActive || req.ProgressTicks > 0 || DayRules.ParseUrgency(req.Urgency) != UrgencyLevel.Urgent) continue;
                int dueIn = req.DueTicks - sim.Now;
                if (dueIn <= 0 || dueIn > _juice.urgentTickSeconds * tps) continue;
                ticking = true;
                fast |= JuiceMath.UrgentTickFast(sim.Now, req.DueTicks, tps, _juice.urgentFastTickSeconds);
            }
            SetLoop(ref _clockLoop, SynthBank.Clock, ticking, fast ? _juice.urgentFastTickPitch : 1f);
            if (sim.Phase != LevelPhase.Running) return;
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
            for (int ri = 0; ri < _rooms.Count; ri++)
            {
                var r = _rooms[ri];
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
                    {
                        var sockets = sim.Sockets;
                        for (int si = 0; si < sockets.Count; si++)
                        {
                            var s = sockets[si];
                            if (s.RoomId == r.Room.Id && s.OccupantId.Length > 0) _fx.Sparks(SocketPoint(sim, s), n, 0.6f + frac);
                        }
                    }
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
            UpdateFlashlight(sim, dark);
            UpdateVoices(now, dark);
            float factor = dark ? _juice.blackoutLightFactor
                : relighting && _rooms.Count > 0 ? Mathf.Lerp(_juice.blackoutLightFactor, 1f, lit / (float)_rooms.Count)
                : 1f;
            SetLights(factor);
        }

        private Light _flashlight;

        /// <summary>Plays the staggered family complaints queued by a blackout; a relight cancels the rest.</summary>
        private void UpdateVoices(float now, bool dark)
        {
            if (!dark)
            {
                _voiceAt.Clear();
                return;
            }
            for (int i = 0; i < _voiceAt.Count; i++)
            {
                if (_voiceAt[i] < 0f || now < _voiceAt[i]) continue;
                _voiceAt[i] = -1f;
                // Each person a different pitch and word, so it reads as several voices.
                var pitches = _juice.familyVoicePitches;
                float pitch = pitches != null && pitches.Length > 0 ? pitches[i % pitches.Length] : 1f;
                _feedback.Play(i % 2 == 0 ? SynthBank.VoiceAngry : SynthBank.VoiceHey, pitch, 1f);
            }
        }

        /// <summary>
        /// While the house is dark the player carries a flashlight: a spot light at chest height, pointing ahead the
        /// way they face and a little down, so the way to the breaker panel is lit and the rest stays dark.
        /// </summary>
        private void UpdateFlashlight(HouseSim sim, bool dark)
        {
            if (!dark)
            {
                if (_flashlight != null && _flashlight.enabled) _flashlight.enabled = false;
                return;
            }
            if (_flashlight == null)
            {
                var go = new GameObject("Flashlight");
                go.transform.SetParent(_house.transform, false);
                _flashlight = go.AddComponent<Light>();
                _flashlight.type = LightType.Spot;
                _flashlight.shadows = LightShadows.None;
            }
            _flashlight.color = _juice.flashlightColor;
            _flashlight.intensity = _juice.flashlightIntensity;
            _flashlight.range = _juice.flashlightRange;
            _flashlight.spotAngle = _juice.flashlightAngle;
            if (!_flashlight.enabled) _flashlight.enabled = true;
            var ch = sim.Character;
            var p = CharacterPoint(sim);
            var t = _flashlight.transform;
            t.localPosition = new Vector3(p.x, p.y + _juice.flashlightHeight, _config.dayFrontZ + ch.ZCm * 0.01f - 0.2f);
            t.localRotation = Quaternion.LookRotation(new Vector3(ch.Facing >= 0 ? 1f : -1f, -0.3f, 0.35f));
        }

        private void UpdateFootsteps(HouseSim sim, bool running)
        {
            var ch = sim.Character;
            // Jetpack: puffs while Space is held. Landing: a heavier footstep and a squash.
            if (running && ch.Thrusting)
            {
                _puffTimer -= Time.unscaledDeltaTime;
                if (_puffTimer <= 0f)
                {
                    _puffTimer = _juice.jetpackPuffInterval;
                    _feedback.Play(SynthBank.Jetpack, 1f + (_stepParity == 0 ? -_juice.stepPitchSpread : _juice.stepPitchSpread), 1f);
                    _stepParity ^= 1;
                }
            }
            else _puffTimer = 0f;
            if (ch.IsJumping != _wasJumping)
            {
                _wasJumping = ch.IsJumping;
                if (running && ch.IsJumping) _feedback.Play(SynthBank.Jump);
            }
            if (ch.IsAirborne != _wasAirborne)
            {
                _wasAirborne = ch.IsAirborne;
                if (running && !ch.IsAirborne)
                {
                    _feedback.Play(SynthBank.Step, _juice.landPitch, 1f);
                    _characterSquashAt = Time.unscaledTime;
                }
            }
            SetLoop(ref _runLoop, SynthBank.Running, running && ch.WalkDir != 0 && !ch.IsAirborne, 1f);
        }

        /// <summary>Starts or stops a looped bank sound (made on first use), at its configured volume.</summary>
        private void SetLoop(ref AudioSource loop, string sound, bool on, float pitch)
        {
            if (!on)
            {
                if (loop != null && loop.isPlaying) loop.Stop();
                return;
            }
            if (loop == null) loop = _feedback.MakeLoop(sound);
            loop.mute = _feedback.Muted;
            loop.volume = _feedback.VolumeFor(sound);
            loop.pitch = pitch;
            if (!loop.isPlaying)
            {
                loop.Play();
                _feedback.LogPlay(sound);
            }
        }

        private void UpdateAppliances(HouseSim sim, float now, bool running)
        {
            _stale.Clear();
            foreach (var kv in _applianceLoops) _stale.Add(kv.Key);
            var appliances = sim.Appliances;
            for (int ai = 0; ai < appliances.Count; ai++) // indexed: no boxed enumerator every frame
            {
                var app = appliances[ai];
                var view = _house.FindApplianceView(app.Id);
                bool working = running && IsWorking(sim, app);
                if (working)
                {
                    _stale.Remove(app.Id);
                    string loop = LoopName(app.Type);
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

        /// <summary><see cref="SynthBank.LoopOf"/>, cached: it is asked every frame for every working appliance (G7).</summary>
        private string LoopName(string type)
        {
            if (!_loopNames.TryGetValue(type, out var loop))
            {
                loop = SynthBank.LoopOf(type);
                _loopNames[type] = loop;
            }
            return loop;
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
            var character = _characterRoot;
            if (character == null) return;
            var ch = sim.Character;
            float target = running ? -ch.WalkDir * _juice.leanDegrees : 0f;
            _lean = Mathf.MoveTowards(_lean, target, _juice.leanSpeed * _juice.leanDegrees * Time.unscaledDeltaTime);
            character.localRotation = Quaternion.Euler(0f, 0f, _lean);

            JuiceMath.Squash(now - _characterSquashAt, _juice.squashSeconds, _juice.squashAmount, out float sx, out float sy);
            character.localScale = new Vector3(sx, sy, 1f);

            // The hop: a parabola over hopSeconds on top of where the house view put the character; squash on landing.
            float hop = (now - _hopAt) / Mathf.Max(0.01f, _juice.hopSeconds);
            if (hop >= 0f && hop < 1f) character.localPosition += new Vector3(0f, _juice.hopHeight * 4f * hop * (1f - hop), 0f);
            else if (hop >= 1f && _hopAt > 0f)
            {
                _hopAt = -100f;
                _characterSquashAt = now;
                _feedback.Play(SynthBank.Step, _juice.landPitch, 1f);
            }

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

            // A generated track still being synthesised in the background starts when it is ready (a frame or so later)
            // instead of stalling this frame; the old track keeps playing meanwhile.
            if (wanted != _musicWanted && (wanted.Length == 0 || _feedback.Bank.IsReady(wanted)))
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

        /// <summary>A looping music source under the feedback object (as <see cref="FeedbackController.MakeLoop"/>, no clip yet).</summary>
        private static AudioSource MusicSource(FeedbackController feedback)
        {
            var go = new GameObject("Loop music");
            go.transform.SetParent(feedback.transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.mute = feedback.Muted;
            return src;
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
            return HouseLayout.CharacterPoint(sim, _controller.TickFraction, _config.roomHeight, _config.jumpArc);
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
            _controller.CommandRefused -= OnCommandRefused;
            _controller.Hopped -= OnHopped;
        }
    }
}
