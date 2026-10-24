using System;
using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Trouble.Game
{
    /// <summary>
    /// Runs one level: owns the <see cref="HouseSim"/>, steps it on a fixed tick clock, routes its events to toasts
    /// and the result panel, handles keyboard shortcuts, and builds and tears down the house view and HUD
    /// (GDD v1.0 §8.5, v1.1 §2.6). Every player action goes through <see cref="Submit"/>; views never change sim state.
    /// </summary>
    /// <remarks>
    /// Example (driving a level from code, as the play-mode check does):
    /// <code>
    /// var lc = Object.FindAnyObjectByType&lt;LevelController&gt;();
    /// lc.Submit(Command.Plug("kettle", "K1"));
    /// lc.StartOrTogglePause();   // planning -> running
    /// </code>
    /// The tick clock uses unscaled real time and at most maxStepsPerFrame steps per frame, so the result of a given
    /// command sequence never depends on frame rate.
    /// </remarks>
    public sealed class LevelController : MonoBehaviour
    {
        private TrippedViewConfig _config;
        private LevelLoader _loader;
        private HouseCamera _camera;
        private ArtLibrary _art;
        private HudText _text;
        private CauseFormatter _formatter;
        private GameObject _houseObject;
        private GameObject _hudObject;
        private float _accumulator;
        private int _sentWalk;
        private string _lastTripText = "";
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _toastLog = new List<string>();

        /// <summary>The running sim. Null when the level could not be loaded.</summary>
        public HouseSim Sim { get; private set; }

        /// <summary>Number of the level being played.</summary>
        public int LevelIndex { get; private set; }

        /// <summary>What the player holds between two clicks: an appliance, the cord, or the cord waiting for a room.</summary>
        public Held Holding { get; private set; } = Held.None;

        /// <summary>Selected appliance id (click a socket to plug it), or null.</summary>
        public string Selection => Holding.ApplianceId;

        /// <summary>How far the clock is into the next tick, 0..1, for smooth animation only.</summary>
        public float TickFraction { get; private set; }

        /// <summary>The house view of the current level.</summary>
        public HouseView House { get; private set; }

        /// <summary>The camera rig (for alert arrows and panning).</summary>
        public HouseCamera HouseCam => _camera;

        /// <summary>Pans the camera to a room (an alert arrow was clicked). The character does not move.</summary>
        public void PanToRoom(string roomId)
        {
            var room = Sim?.FindRoom(roomId);
            if (room == null || _camera == null) return;
            _camera.PanTo(new Vector2(room.CenterX, HouseLayout.FloorY(room.Floor, _config.roomHeight) + _config.roomHeight * 0.5f));
        }

        /// <summary>The HUD of the current level.</summary>
        public Hud Hud { get; private set; }

        /// <summary>Every toast text shown since the level was loaded, oldest first (for checks and debugging).</summary>
        public IReadOnlyList<string> ToastLog => _toastLog;

        // ---- G3 hooks: feedback, screens and the F1 panel listen here; the controller does not know them. ----

        /// <summary>Raised after a level was loaded (also on restart) with its number.</summary>
        public event Action<int> LevelLoaded;

        /// <summary>Raised after <see cref="Unload"/> removed the level.</summary>
        public event Action LevelUnloaded;

        /// <summary>Raised with the events of every sim tick, after the controller handled them. The list is reused; copy to keep.</summary>
        public event Action<IReadOnlyList<SimEvent>> TickEvents;

        /// <summary>Raised when the sim accepted a player command (at once in planning, or into the queue).</summary>
        public event Action<Command> CommandAccepted;

        /// <summary>Raised when Esc is pressed while no appliance is selected (the screen flow goes to Level Select).</summary>
        public event Action EscapePressed;

        /// <summary>true = keyboard shortcuts are ignored (a text field of the F1 panel has focus). The clock still runs.</summary>
        public bool KeysSuspended { get; set; }

        /// <summary>Speed of the tick clock (debug time scale, 1 = normal).</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>The loader that builds every level; its <see cref="LevelLoader.Rules"/> are used on the next load.</summary>
        public LevelLoader Loader => _loader;

        /// <summary>Shift is held: actions join the end of the character's queue.</summary>
        public bool ShiftHeld => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

        /// <summary>Connects the controller to the config and camera. Call once before <see cref="LoadLevel"/>.</summary>
        public void Setup(TrippedViewConfig config, HouseCamera houseCamera)
        {
            _config = config;
            _camera = houseCamera;
            _loader = new LevelLoader();
            _text = new HudText(_loader.Strings);
            _formatter = new CauseFormatter(_loader.Strings);
        }

        /// <summary>True if level n exists in Resources.</summary>
        public bool HasLevel(int n) => LevelLoader.Exists(n);

        /// <summary>
        /// Loads level n from scratch: a new sim, a new house and a new HUD. Anything from the previous run is
        /// destroyed first. Returns false (and keeps the current level) if level n cannot be loaded.
        /// </summary>
        public bool LoadLevel(int n)
        {
            _errors.Clear();
            var sim = _loader.Build(n, _errors);
            if (sim == null)
            {
                foreach (var e in _errors) Debug.LogError($"[TRIPPED] Level {n}: {e}");
                if (Hud != null) Hud.Toast($"Level {n} cannot be loaded (see Console).");
                return false;
            }

            TearDown();
            Sim = sim;
            LevelIndex = n;
            Holding = Held.None;
            _accumulator = 0f;
            _sentWalk = 0;
            TickFraction = 0f;
            _lastTripText = "";
            _toastLog.Clear();
            _art = new ArtLibrary(_config);

            _houseObject = new GameObject("House");
            _houseObject.transform.SetParent(transform, false);
            House = _houseObject.AddComponent<HouseView>();
            House.Build(this, _config, _art);

            _hudObject = new GameObject("HUD", typeof(RectTransform));
            _hudObject.transform.SetParent(transform, false);
            Hud = _hudObject.AddComponent<Hud>();
            Hud.Build(this, _config, _text, _art);

            if (_camera != null)
            {
                _camera.UseArea(Sim.IsDayMode ? _config.dayHouseArea : _config.houseArea);
                // Day mode: A/D/W/S and the arrows move the character, not the camera.
                _camera.KeyPan = !Sim.IsDayMode;
                // Day mode: the house fills nearly all of the screen the HUD leaves free.
                _camera.HomeMargin = Sim.IsDayMode ? _config.dayHomeMargin : -1f;
                _camera.Frame(House.Bounds, () => HouseLayout.CharacterPoint(Sim, TickFraction, _config.roomHeight, _config.stairsHalfRun, _config.stairsLanding));
            }
            LevelLoaded?.Invoke(n);
            return true;
        }

        /// <summary>Removes the current level (house, HUD and sim), e.g. when going back to a menu.</summary>
        public void Unload()
        {
            TearDown();
            LevelUnloaded?.Invoke();
        }

        /// <summary>Restarts the current level (R).</summary>
        public void Restart() => LoadLevel(LevelIndex);

        /// <summary>Loads the next level if there is one.</summary>
        public void LoadNextLevel()
        {
            if (HasLevel(LevelIndex + 1)) LoadLevel(LevelIndex + 1);
        }

        /// <summary>
        /// Space / P: the first press starts the level (planning → running); after that it pauses and resumes.
        /// </summary>
        public void StartOrTogglePause()
        {
            if (Sim == null) return;
            switch (Sim.Phase)
            {
                case LevelPhase.Planning:
                    Sim.Start();
                    _accumulator = 0f;
                    break;
                case LevelPhase.Running:
                    Sim.SetPaused(true);
                    break;
                case LevelPhase.Paused:
                    Sim.SetPaused(false);
                    break;
            }
        }

        /// <summary>
        /// Hands a player command to the sim. If the sim refuses it now, the reason is shown as a toast.
        /// Later refusals (when the character arrives) come back as CommandRejected events.
        /// </summary>
        /// <returns>false if refused now.</returns>
        public bool Submit(Command command)
        {
            if (Sim == null) return false;
            if (Sim.Submit(command, out var reason))
            {
                CommandAccepted?.Invoke(command);
                return true;
            }
            Toast(reason);
            return false;
        }

        /// <summary>Submit from a HUD button (same as <see cref="Submit"/>; kept separate for clarity of call sites).</summary>
        public void SubmitFromUi(Command command) => Submit(command);

        /// <summary>Applies the click rules (<see cref="ClickResolver"/>) to a click on the house or the tray.</summary>
        public void HandleClick(ClickInput click)
        {
            if (Sim == null || Sim.Phase == LevelPhase.Won || Sim.Phase == LevelPhase.Lost) return;
            // Day mode is played with the keyboard (walk, W/S stairs, E); the sim rejects click commands there.
            if (Sim.IsDayMode) return;
            if (click.Kind == ClickKind.TrayAppliance) click.Shift = ShiftHeld;
            click.DayMode = Sim.IsDayMode;
            var outcome = ClickResolver.Resolve(Holding, click);
            Holding = outcome.Selection;
            if (!outcome.HasCommand) return;
            // During free planning the sim refuses MoveTo and the toast says "Unpause to walk.".
            Submit(outcome.Command);
        }

        /// <summary>Drops the selected appliance (Esc).</summary>
        public void ClearSelection() => Holding = Held.None;

        /// <summary>Shows a toast and records it in <see cref="ToastLog"/>.</summary>
        public void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _toastLog.Add(message);
            if (Hud != null) Hud.Toast(message);
        }

        private void Update()
        {
            if (Sim == null) return;
            if (!KeysSuspended) HandleKeys();
            if (Sim == null) return;
            Tick();
        }

        private void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) StartOrTogglePause();
            if (kb.rKey.wasPressedThisFrame)
            {
                Restart();
                return;
            }
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (!Holding.IsNone) ClearSelection();
                else
                {
                    EscapePressed?.Invoke();
                    if (Sim == null) return;
                }
            }
            for (int n = 1; n <= 9; n++)
            {
                if (!kb[Key.Digit1 + (n - 1)].wasPressedThisFrame) continue;
                if (HasLevel(n)) LoadLevel(n);
                return;
            }
            if (Sim != null && Sim.IsDayMode) HandleDayKeys(kb);
        }

        /// <summary>
        /// Day mode: A/D or Left/Right hold a walking direction (sent once when it changes, 0 when released),
        /// W/Up climbs, S/Down goes down, E interacts. Keys only act while the day is running.
        /// </summary>
        private void HandleDayKeys(Keyboard kb)
        {
            if (Sim.Phase != LevelPhase.Running) return;
            int dir = 0;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir += 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir -= 1;
            if (dir != _sentWalk && Submit(Command.Walk(dir))) _sentWalk = dir;
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Submit(Command.Climb(1));
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Submit(Command.Climb(-1));
            if (kb.eKey.wasPressedThisFrame) Submit(Command.Interact());
        }

        /// <summary>Steps the sim every 1/ticksPerSecond of real time while running, at most maxStepsPerFrame per frame.</summary>
        private void Tick()
        {
            if (Sim.Phase != LevelPhase.Running)
            {
                TickFraction = 0f;
                return;
            }
            float tickSeconds = 1f / Mathf.Max(1, Sim.Rules.ticksPerSecond);
            _accumulator += Time.unscaledDeltaTime * Mathf.Max(0f, TimeScale);
            int steps = 0;
            int maxSteps = Mathf.Max(1, _config.maxStepsPerFrame);
            while (_accumulator >= tickSeconds && steps < maxSteps && Sim.Phase == LevelPhase.Running)
            {
                _accumulator -= tickSeconds;
                steps++;
                HandleEvents(Sim.Step());
            }
            // After a long hitch, drop the backlog instead of fast-forwarding.
            if (_accumulator > tickSeconds) _accumulator = tickSeconds;
            TickFraction = Sim.Phase == LevelPhase.Running ? Mathf.Clamp01(_accumulator / tickSeconds) : 0f;
        }

        /// <summary>
        /// Debug and test helper (the "skip to tick" of GDD v1.1 §4.4): runs up to <paramref name="count"/> ticks
        /// right now, through the same event handling as the clock. Does nothing unless the level is running.
        /// </summary>
        /// <returns>Ticks actually run (fewer if the level ended).</returns>
        public int StepTicks(int count)
        {
            int done = 0;
            while (Sim != null && Sim.Phase == LevelPhase.Running && done < count)
            {
                HandleEvents(Sim.Step());
                done++;
            }
            _accumulator = 0f;
            return done;
        }

        /// <summary>Turns the events of one tick into toasts and, at the end, the result panel (GDD v1.0 §7.1).</summary>
        private void HandleEvents(IReadOnlyList<SimEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                switch (e.Type)
                {
                    case SimEventType.CircuitTripped:
                    case SimEventType.MainTripped:
                    {
                        string text = _formatter.Describe(e, Sim);
                        _lastTripText = text ?? "";
                        Toast(text);
                        Toast(_formatter.Hint(e)); // "Breaker A is still hot..." when it was reset while hot
                        break;
                    }
                    case SimEventType.ApplianceInterrupted:
                        Toast(_formatter.Describe(e, Sim));
                        if (e.ProgressLost && Hud != null)
                        {
                            var request = Sim.FindRequest(e.Appliance);
                            if (request != null) Hud.AddInterrupt(request.Owner, request.RoomId);
                        }
                        break;
                    case SimEventType.RequestMissed:
                        _lastTripText = _formatter.Describe(e, Sim) ?? _lastTripText;
                        Toast(_formatter.Describe(e, Sim));
                        break;
                    case SimEventType.CommandRejected:
                    case SimEventType.SocketRevealed:
                        Toast(_formatter.Describe(e, Sim));
                        break;
                    case SimEventType.LevelWon:
                        ShowResult(true, _formatter.Describe(e, Sim));
                        break;
                    case SimEventType.LevelLost:
                        ShowResult(false, _formatter.Describe(e, Sim));
                        break;
                }
            }
            TickEvents?.Invoke(events);
        }

        private void ShowResult(bool won, string cause)
        {
            Holding = Held.None;
            if (Hud == null) return;
            string time = HudFormat.Clock(Sim.Now, Sim.Rules.ticksPerSecond);
            if (Sim.IsDayMode)
            {
                Hud.ShowResult(won, cause, won ? "" : _lastTripText, time, Sim.TripCount, false,
                    _text.Format("result_day_stats", ("score", Sim.Score), ("done", Sim.RequestsDone), ("missed", Sim.RequestsMissed)),
                    _text.Get(won ? "result_day_won" : "result_day_lost"));
                return;
            }
            Hud.ShowResult(won, cause, won ? "" : _lastTripText, time, Sim.TripCount, won && HasLevel(LevelIndex + 1));
        }

        private void TearDown()
        {
            // Deactivate first so nothing old (colliders, UI) reacts during the rest of this frame.
            if (_houseObject != null)
            {
                _houseObject.SetActive(false);
                Destroy(_houseObject);
            }
            if (_hudObject != null)
            {
                _hudObject.SetActive(false);
                Destroy(_hudObject);
            }
            _art?.Dispose();
            _houseObject = null;
            _hudObject = null;
            House = null;
            Hud = null;
            Sim = null;
        }

        private void OnDestroy()
        {
            TearDown();
        }
    }
}
