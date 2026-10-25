using System;
using System.Collections.Generic;

namespace Trouble.Sim
{
    /// <summary>
    /// The whole game state of one level and every rule that changes it.
    /// Pure C#: no UnityEngine, no randomness, no real time. One call to <see cref="Step"/> = one tick.
    /// Tick order: GDD v1.0 §3.4 with the v1.1 changes in §2.8–2.9.
    /// </summary>
    /// <remarks>
    /// Outside planning, every player command goes into the character's action queue: the character walks
    /// to the right room, spends <see cref="RulesConfig.interactTicks"/>, then the command is checked and applied.
    /// The character's position and Sight are sim state on purpose (docs/architecture/adr-0001).
    /// </remarks>
    public sealed class HouseSim
    {
        public const string FridgeId = "fridge";
        public const string CordSocketPrefix = "CORD-";

        private readonly List<RoomState> _rooms = new List<RoomState>();
        private readonly List<DoorState> _doors = new List<DoorState>();
        private readonly List<CircuitState> _circuits = new List<CircuitState>();
        private readonly List<CircuitState> _circuitsById = new List<CircuitState>();
        private readonly List<SocketState> _sockets = new List<SocketState>();
        private readonly List<ApplianceState> _appliances = new List<ApplianceState>();
        private readonly List<ApplianceState> _appliancesById = new List<ApplianceState>();
        private readonly List<RequestState> _requests = new List<RequestState>();
        private readonly List<RoutineState> _routines = new List<RoutineState>();

        private readonly List<SimEvent> _events = new List<SimEvent>();
        /// <summary>Rooms still to walk through after the current segment, for the action at the front of the queue.</summary>
        private readonly List<string> _path = new List<string>();
        private readonly HashSet<string> _visibleBefore = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<SimEvent> _pending = new List<SimEvent>();
        private readonly HashSet<string> _prevPowered = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _lostPowerByCommand = new HashSet<string>(StringComparer.Ordinal);
        private readonly Strings _strings;
        private bool _inStep;
        private int _mainCool;
        private readonly SimRandom _rng;
        private readonly RequestGenerator _generator;

        /// <param name="level">Level data as loaded from JSON.</param>
        /// <param name="rules">Global rules. The level's rulesOverride is applied on top.</param>
        /// <param name="catalog">Appliance types.</param>
        /// <param name="strings">Rejection texts. Null = rejections carry their string keys.</param>
        /// <exception cref="ArgumentException">The level refers to something that does not exist.</exception>
        public HouseSim(LevelData level, RulesConfig rules, Catalog catalog, Strings strings = null)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _strings = strings ?? new Strings(null);

            var ruleErrors = new List<string>();
            Rules = rules.WithOverrides(level.rulesOverride, ruleErrors);
            if (ruleErrors.Count > 0) throw new ArgumentException(string.Join(" ", ruleErrors));

            DayLengthTicks = Rules.SecondsToTicks(level.dayLengthSec);
            IsDayMode = level.day != null && level.day.enabled;
            Seed = IsDayMode && level.day.seed != 0 ? level.day.seed : 1;
            _rng = new SimRandom(Seed);
            Hearts = IsDayMode ? level.day.hearts : 0;
            MainCapacity = IsDayMode ? 0 : level.mainCapacity;
            SpoilLimitTicks = Rules.SecondsToTicks(level.fridgeSpoilSec);
            CordsLeft = level.cordCount;
            Phase = LevelPhase.Planning;

            BuildRooms();
            BuildDoors();
            BuildCircuits();
            BuildSockets();
            BuildAppliances();
            BuildCharacter();
            if (IsDayMode) _generator = new RequestGenerator(level.day, _rng, Rules, DayLengthTicks);
            RecomputePower();
            UpdateSight();
        }

        // ---- configuration -------------------------------------------------------------

        public LevelData Level { get; }
        /// <summary>Global rules with this level's overrides applied.</summary>
        public RulesConfig Rules { get; }
        public Catalog Catalog { get; }

        // ---- time and outcome ----------------------------------------------------------

        /// <summary>Current tick.</summary>
        public int Now { get; private set; }
        public LevelPhase Phase { get; private set; }
        public int DayLengthTicks { get; }
        public LoseReason LoseReason { get; private set; }
        /// <summary>Breaker trips so far, main breaker included. Flipping a breaker by hand is not a trip.</summary>
        public int TripCount { get; private set; }

        // ---- day mode ------------------------------------------------------------------

        /// <summary>The level's day mode is on: generated requests, room circuits, hearts, carrying.</summary>
        public bool IsDayMode { get; }
        /// <summary>Seed actually used for this day's rolls (level.day.seed, or 1 when that is 0).</summary>
        public int Seed { get; }
        public int Hearts { get; private set; }
        public int Score { get; private set; }
        public int RequestsDone { get; private set; }
        public int RequestsMissed { get; private set; }
        /// <summary>Tick the next request is due to appear. -1 outside day mode.</summary>
        public int NextRequestTick => _generator?.NextTick ?? -1;

        // ---- state for views -----------------------------------------------------------

        /// <summary>Rooms in level order (left to right per floor).</summary>
        public IReadOnlyList<RoomState> Rooms => _rooms;
        public IReadOnlyList<DoorState> Doors => _doors;
        public IReadOnlyList<CircuitState> Circuits => _circuits;
        /// <summary>Real sockets in level order, then cord sockets.</summary>
        public IReadOnlyList<SocketState> Sockets => _sockets;
        public IReadOnlyList<ApplianceState> Appliances => _appliances;
        public IReadOnlyList<RequestState> Requests => _requests;
        public IReadOnlyList<RoutineState> Routines => _routines;
        /// <summary>
        /// The character the sim is working on: while a command or a tick is applied, the player it is for; otherwise
        /// the one the view follows (<see cref="ViewPlayer"/>). Single player: always the only character.
        /// </summary>
        public CharacterState Character => _active ?? _characters[Math.Min(ViewPlayer, _characters.Count - 1)];

        /// <summary>Every keyboard player, in player order (index 0 = the first). One unless <see cref="AddPlayer"/> was used.</summary>
        public IReadOnlyList<CharacterState> Characters => _characters;

        /// <summary>
        /// Which player <see cref="Character"/> shows outside the sim's own work (the local player's view in co-op). It
        /// never changes game state: commands and ticks always use their own player.
        /// </summary>
        public int ViewPlayer { get; set; }

        private readonly List<CharacterState> _characters = new List<CharacterState> { new CharacterState() };
        private CharacterState _active;

        /// <summary>
        /// Day mode co-op: adds a keyboard player standing where the first one starts; returns its index (for
        /// <see cref="Command.ForPlayer"/>). Call before <see cref="Start"/>.
        /// </summary>
        public int AddPlayer()
        {
            var first = _characters[0];
            var ch = new CharacterState
            {
                RoomId = first.RoomId, FromRoomId = first.FromRoomId, ToRoomId = first.ToRoomId,
                XCm = first.XCm, PrevXCm = first.PrevXCm, ZCm = first.ZCm, PrevZCm = first.PrevZCm,
            };
            _characters.Add(ch);
            return _characters.Count - 1;
        }

        /// <summary>Runs <paramref name="work"/> with <see cref="Character"/> = player <paramref name="player"/>.</summary>
        private T As<T>(int player, Func<T> work)
        {
            var before = _active;
            _active = _characters[Math.Max(0, Math.Min(player, _characters.Count - 1))];
            try { return work(); }
            finally { _active = before; }
        }

        public string PanelRoomId { get; private set; }
        /// <summary>Total load of every circuit whose breaker is ON.</summary>
        public int MainLoad { get; private set; }
        /// <summary>0 = no main breaker.</summary>
        public int MainCapacity { get; }
        public int MainOverTicks { get; private set; }
        public bool MainOverloading { get; private set; }
        /// <summary>Ticks the fridge has been without power, in total.</summary>
        public int SpoilTicks { get; private set; }
        public int SpoilLimitTicks { get; }
        public int CordsLeft { get; private set; }

        public RoomState FindRoom(string id) => Find(_rooms, id, r => r.Id);
        public DoorState FindDoor(string id) => Find(_doors, id, d => d.Id);
        public CircuitState FindCircuit(string id) => Find(_circuits, id, c => c.Id);
        public SocketState FindSocket(string id) => Find(_sockets, id, s => s.Id);
        public ApplianceState FindAppliance(string id) => Find(_appliances, id, a => a.Id);
        /// <summary>The request that uses this appliance, or null.</summary>
        public RequestState FindRequest(string applianceId)
        {
            RequestState last = null;
            foreach (var r in _requests)
            {
                if (r.ApplianceId != applianceId) continue;
                if (r.IsActive) return r;
                last = r;
            }
            return last;
        }

        public bool IsRoomVisible(string roomId) => FindRoom(roomId)?.Visible ?? false;

        // ---- control -------------------------------------------------------------------

        /// <summary>Planning → Running. Called once, when the player first presses Space. Sight narrows to the character from here on.</summary>
        public void Start()
        {
            if (Phase != LevelPhase.Planning) return;
            Phase = LevelPhase.Running;
            UpdateSight();
        }

        /// <summary>Running ↔ Paused. Ignored in Planning, Won and Lost.</summary>
        public void SetPaused(bool paused)
        {
            if (paused && Phase == LevelPhase.Running) Phase = LevelPhase.Paused;
            else if (!paused && Phase == LevelPhase.Paused) Phase = LevelPhase.Running;
        }

        /// <summary>
        /// Hands a player command to the sim.
        /// Planning with planningIsFree: happens at once, no walking (breakers and walking are refused).
        /// Otherwise: goes into the character's queue (replacing it unless <see cref="Command.Append"/>); the
        /// character walks there and does it while time runs. If it is refused then, a CommandRejected event says why.
        /// CancelAction removes one queued action at once.
        /// </summary>
        /// <param name="reason">Rejection text for the toast when this returns false.</param>
        /// <returns>false if the command was refused now; state did not change.</returns>
        public bool Submit(Command command, out string reason)
        {
            string why = "";
            bool accepted = As(command.Player, () => SubmitInternal(command, out why));
            reason = why;
            if (accepted) _recorded.Add(Replay.ToData(command, Phase == LevelPhase.Planning ? -1 : Now));
            return accepted;
        }

        /// <summary>
        /// Every command the sim accepted, with the tick it was given at (-1 = planning). Played back with
        /// <see cref="Replay.Run"/> on a fresh sim of the same level and rules, it gives the same game.
        /// Use it to turn a hand-played run into a reference solution or scenario.
        /// </summary>
        public IReadOnlyList<CommandData> Recorded => _recorded;

        private readonly List<CommandData> _recorded = new List<CommandData>();

        private bool SubmitInternal(Command command, out string reason)
        {
            reason = "";
            if (Phase == LevelPhase.Won || Phase == LevelPhase.Lost)
                return Reject("reject_level_over", out reason);
            if (command.Type == CommandType.CancelAction)
                return CancelAction(command.Index, out reason);

            if (IsDayMode)
            {
                // Day mode is played with the keyboard: walk, jump (or fly) and interact happen at once while time runs.
                bool keyboard = command.Type == CommandType.Walk || command.Type == CommandType.WalkDepth || command.Type == CommandType.Interact
                                || command.Type == CommandType.TogglePlug || command.Type == CommandType.Throw
                                || command.Type == CommandType.Jump || command.Type == CommandType.Thrust || command.Type == CommandType.Drop;
                if (!keyboard) return Reject("reject_unknown", out reason);
                if (Phase != LevelPhase.Running) return Reject("reject_unpause_walk", out reason);
                bool done = Apply(command, out reason);
                if (done)
                {
                    RecomputePower();
                    UpdateSight();
                }
                return done;
            }

            if (Phase == LevelPhase.Planning && Rules.planningIsFree)
            {
                if (command.Type == CommandType.ToggleBreaker) return Reject("reject_unpause_breaker", out reason);
                if (command.Type == CommandType.MoveTo) return Reject("reject_unpause_walk", out reason);
                bool ok = Apply(command, out reason);
                if (ok)
                {
                    RecomputePower();
                    UpdateSight();
                }
                return ok;
            }

            var queue = Character.QueueList;
            if (!command.Append) ClearQueue();
            else if (queue.Count >= Rules.actionQueueSize) return Reject("reject_queue_full", out reason);
            queue.Add(new CharacterAction { Command = command });
            return true;
        }

        private bool CancelAction(int index, out string reason)
        {
            var queue = Character.QueueList;
            if (index < 0 || index >= queue.Count) return Reject("reject_unknown", out reason);
            if (index == 0)
            {
                // The character finishes the door it is in, then stops.
                _path.Clear();
                Character.InteractTicksLeft = 0;
            }
            queue.RemoveAt(index);
            reason = "";
            return true;
        }

        private void ClearQueue()
        {
            Character.QueueList.Clear();
            _path.Clear();
            Character.InteractTicksLeft = 0;
        }

        /// <summary>
        /// Advances one tick if the phase is Running. Returns the events of this tick, plus any raised by
        /// commands applied at once since the last tick. The list is reused by the next call; copy it to keep it.
        /// </summary>
        public IReadOnlyList<SimEvent> Step()
        {
            _events.Clear();
            if (Phase != LevelPhase.Running) return _events;
            _events.AddRange(_pending);
            _pending.Clear();
            _inStep = true;
            // Everything in a tick works on the first player unless it says otherwise (CharacterTick does each).
            _active = _characters[0];
            try
            {
                StepInternal();
            }
            finally
            {
                _inStep = false;
                _active = null;
            }
            return _events;
        }

        private void StepInternal()
        {
            // 1–2. Player commands are already in the character's queue; the character walks and acts.
            CharacterTick();

            // 3. Routines switch themselves on and off.
            foreach (var r in _routines)
            {
                var app = FindAppliance(r.ApplianceId);
                if (Now == r.StartTick) app.SwitchedOn = true;
                if (Now == r.EndTick) app.SwitchedOn = false;
            }

            // 4. Loads.
            RecomputePower();

            // 5. Each breaker, in id order.
            foreach (var c in _circuitsById)
            {
                bool over = c.IsOn && c.Load > c.Capacity;
                UpdateOverloadFlag(c, over);
                int count = c.OverTicks;
                bool trip = CountOverload(ref count, ref c.CoolTicks, over);
                c.OverTicks = count;
                if (count == 0) c.ResetWhileHot = false;
                // Only an overloaded tick trips: a hot counter on a breaker that is already OFF must not.
                if (trip && over) TripCircuit(c);
            }

            // 6. Main breaker, on the load of the circuits still ON.
            if (MainCapacity > 0)
            {
                int total = 0;
                foreach (var c in _circuits)
                    if (c.IsOn) total += c.Load;
                MainLoad = total;
                bool over = total > MainCapacity;
                if (over && !MainOverloading) Emit(new SimEvent { Type = SimEventType.OverloadStarted, Circuit = "", Load = total, Capacity = MainCapacity });
                if (!over && MainOverloading) Emit(new SimEvent { Type = SimEventType.OverloadCleared, Circuit = "" });
                MainOverloading = over;
                int count = MainOverTicks;
                bool trip = CountOverload(ref count, ref _mainCool, over);
                MainOverTicks = count;
                if (trip && over) TripMain(total);
            }

            RecomputePower();

            // 7. Lost power since the last tick.
            foreach (var app in _appliancesById)
            {
                bool lost = (_prevPowered.Contains(app.Id) && !app.Powered) || _lostPowerByCommand.Contains(app.Id);
                if (!lost) continue;
                var req = FindRequest(app.Id);
                if (req == null || req.Done) continue;
                bool progressLost = app.Fragile && req.ProgressTicks > 0;
                if (app.Fragile) req.ProgressTicks = 0;
                Emit(new SimEvent { Type = SimEventType.ApplianceInterrupted, Appliance = app.Id, Socket = app.SocketId, ProgressLost = progressLost });
            }
            _lostPowerByCommand.Clear();

            // 8. Progress.
            foreach (var app in _appliancesById)
            {
                if (!app.Powered) continue;
                var req = FindRequest(app.Id);
                if (req == null || !req.IsActive || !ServesRequest(app, req)) continue;
                req.ProgressTicks++;
                if (req.ProgressTicks >= req.DurationTicks)
                {
                    req.Done = true;
                    if (!IsDayMode) app.SwitchedOn = false;
                    if (IsDayMode)
                    {
                        Score += req.Points;
                        RequestsDone++;
                    }
                    Emit(new SimEvent { Type = SimEventType.RequestCompleted, Appliance = app.Id, Room = req.RoomId });
                }
            }
            RecomputePower();
            _prevPowered.Clear();
            foreach (var app in _appliances)
                if (app.Powered) _prevPowered.Add(app.Id);

            // 9. Fridge.
            var fridge = FindAppliance(FridgeId);
            if (fridge != null && !fridge.Powered) SpoilTicks++;

            // 10. Sight, and reveals that need it.
            UpdateSight();
            if (Rules.revealRequiresSight) RevealSeenDeadSockets();

            // 11. Time.
            Now++;

            if (IsDayMode)
            {
                DayEndOfTick();
                return;
            }

            // 12. Lose, checked before win (fridge, then deadline, then day).
            if (fridge != null && SpoilTicks > SpoilLimitTicks)
            {
                Emit(new SimEvent { Type = SimEventType.FridgeSpoiled, Appliance = FridgeId });
                Lose(LoseReason.FridgeSpoiled, FridgeId);
                return;
            }
            foreach (var req in _requests)
            {
                if (!req.Done && Now > req.DueTicks)
                {
                    Lose(LoseReason.Deadline, req.ApplianceId);
                    return;
                }
            }
            if (Now > DayLengthTicks)
            {
                Lose(LoseReason.DayOver, "");
                return;
            }

            // 13. Win.
            foreach (var req in _requests)
                if (!req.Done) return;
            Phase = LevelPhase.Won;
            Emit(new SimEvent { Type = SimEventType.LevelWon });
        }

        // ---- overload ------------------------------------------------------------------

        /// <summary>Advances one overload counter by one tick (R3, v1.1 §2.8). Returns true when it should trip.</summary>
        private bool CountOverload(ref int count, ref int cool, bool over)
        {
            if (over)
            {
                count++;
                cool = 0;
            }
            else if (Rules.overCoolEvery <= 0)
            {
                count = 0;
                cool = 0;
            }
            else if (count > 0)
            {
                cool++;
                if (cool >= Rules.overCoolEvery)
                {
                    count--;
                    cool = 0;
                }
            }
            else
            {
                cool = 0;
            }
            return count >= Rules.overTripTicks;
        }

        private void UpdateOverloadFlag(CircuitState c, bool over)
        {
            if (over && !c.Overloading) Emit(new SimEvent { Type = SimEventType.OverloadStarted, Circuit = c.Id, Load = c.Load, Capacity = c.Capacity });
            if (!over && c.Overloading) Emit(new SimEvent { Type = SimEventType.OverloadCleared, Circuit = c.Id });
            c.Overloading = over;
        }

        private void TripCircuit(CircuitState c)
        {
            Emit(new SimEvent
            {
                Type = SimEventType.CircuitTripped,
                Circuit = c.Id,
                Load = c.Load,
                Capacity = c.Capacity,
                Terms = TermsOn(c.Id),
                WasHot = c.ResetWhileHot,
            });
            TripCount++;
            c.ResetWhileHot = false;
            if (IsDayMode) PopNewestPlugs(c);
            TurnOff(c);
            if (IsDayMode)
            {
                // One room over its capacity blacks out the whole house.
                foreach (var other in _circuitsById)
                    if (other.IsOn) TurnOff(other);
                LoseHeart();
            }
        }

        private void TripMain(int total)
        {
            Emit(new SimEvent
            {
                Type = SimEventType.MainTripped,
                Circuit = "",
                Load = total,
                Capacity = MainCapacity,
                Terms = TermsOn(null),
            });
            TripCount++;
            if (!KeepCounterAfterTrip) MainOverTicks = 0;
            _mainCool = 0;
            MainOverloading = false;
            foreach (var c in _circuitsById)
                if (c.IsOn) TurnOff(c);
        }

        private bool KeepCounterAfterTrip => Rules.counterAfterTrip == "keep";

        /// <summary>A breaker goes OFF by tripping. Its counter stays hot or restarts, per counterAfterTrip.</summary>
        private void TurnOff(CircuitState c)
        {
            c.IsOn = false;
            if (!KeepCounterAfterTrip) c.OverTicks = 0;
            c.CoolTicks = 0;
            c.Overloading = false;
            if (!Rules.revealRequiresSight) RevealSwitchedOnSockets(c.Id);
        }

        /// <summary>Powered appliances on one circuit (or the whole house when null), sorted by socket id.</summary>
        private List<LoadTerm> TermsOn(string circuitId)
        {
            var powered = new List<ApplianceState>();
            foreach (var app in _appliances)
            {
                if (!app.Powered) continue;
                var circuit = FindCircuit(FindSocket(app.SocketId).CircuitId);
                if (circuitId != null ? circuit.Id != circuitId : !circuit.IsOn) continue;
                powered.Add(app);
            }
            powered.Sort((x, y) => string.CompareOrdinal(x.SocketId, y.SocketId));
            var terms = new List<LoadTerm>(powered.Count);
            foreach (var app in powered) terms.Add(new LoadTerm(app.Id, app.Load));
            return terms;
        }

        // ---- commands ------------------------------------------------------------------

        /// <summary>Checks one command against the rules (v1.0 §3.5) and applies it if allowed.</summary>
        private bool Apply(Command cmd, out string reason)
        {
            reason = "";
            bool ok;
            switch (cmd.Type)
            {
                case CommandType.Plug: ok = ApplyPlug(cmd.A, cmd.B, out reason); break;
                case CommandType.Unplug: ok = ApplyUnplug(cmd.A, out reason); break;
                case CommandType.SetSwitch: ok = ApplySwitch(cmd.A, cmd.On, out reason); break;
                case CommandType.ToggleBreaker: ok = ApplyBreaker(cmd.A, out reason); break;
                case CommandType.PlaceCord: ok = ApplyPlaceCord(cmd.A, cmd.B, out reason); break;
                case CommandType.RemoveCord: ok = ApplyRemoveCord(cmd.A, out reason); break;
                case CommandType.ToggleDoor: ok = ApplyDoor(cmd.A, out reason); break;
                case CommandType.MoveTo: ok = FindRoom(cmd.A) != null || Reject("reject_no_path", out reason); break;
                case CommandType.Walk: ok = ApplyWalk(cmd.Index, out reason); break;
                case CommandType.WalkDepth: ok = ApplyWalkDepth(cmd.Index, out reason); break;
                case CommandType.Interact: ok = ApplyInteract(out reason); break;
                case CommandType.TogglePlug: ok = ApplyTogglePlug(out reason); break;
                case CommandType.Throw: ok = ApplyThrow(out reason); break;
                case CommandType.Jump: ok = ApplyJump(cmd.Index, out reason); break;
                case CommandType.Thrust: ok = ApplyThrust(cmd.Index, out reason); break;
                case CommandType.Drop: ok = ApplyDrop(out reason); break;
                default: ok = Reject("reject_unknown", out reason); break;
            }
            if (ok) NoteLostPower();
            return ok;
        }

        private bool ApplyPlug(string applianceId, string socketId, out string reason)
        {
            var app = FindAppliance(applianceId);
            var socket = FindSocket(socketId);
            if (app == null || socket == null) return Reject("reject_unknown", out reason);
            if (app.Fixed) return Reject("reject_not_yours", out reason);
            if (socket.Reserved)
            {
                var holder = FindAppliance(socket.OccupantId);
                return Reject(out reason, "reject_reserved", ("owner", holder.Owner), ("appliance", NameOf(holder)));
            }
            if (socket.OccupantId.Length > 0) return Reject("reject_socket_in_use", out reason);
            if (socket.CordSocketId.Length > 0) return Reject("reject_cord_here", out reason);
            if (IsDayMode)
            {
                string carrying = Character.CarryingId;
                if (carrying.Length > 0 && carrying != app.Id) return Reject("reject_hands_full", out reason);
                if (carrying == app.Id) Character.CarryingId = "";
            }
            else
            {
                var req = FindRequest(app.Id);
                if (req != null && req.RoomId != socket.RoomId)
                    return Reject(out reason, "reject_wrong_room", ("owner", req.Owner), ("room", FindRoom(req.RoomId).Name));
            }

            Vacate(app);
            socket.OccupantId = app.Id;
            app.SocketId = socket.Id;
            reason = "";
            return true;
        }

        private bool ApplyUnplug(string applianceId, out string reason)
        {
            var app = FindAppliance(applianceId);
            // Day mode: an appliance is always in a socket or in the character's hands.
            if (IsDayMode || app == null || app.Fixed || app.SocketId.Length == 0) return Reject("reject_cant_unplug", out reason);
            Vacate(app);
            reason = "";
            return true;
        }

        private bool ApplySwitch(string applianceId, bool on, out string reason)
        {
            // Day mode: appliances switch themselves on in the room that wants them.
            if (IsDayMode) return Reject("reject_unknown", out reason);
            bool allowedNow = Phase == LevelPhase.Running || (Phase == LevelPhase.Planning && Rules.planningIsFree);
            if (!allowedNow) return Reject("reject_unpause_switch", out reason);
            var app = FindAppliance(applianceId);
            if (app == null) return Reject("reject_unknown", out reason);
            if (app.Fixed) return Reject("reject_not_yours", out reason);
            if (app.SocketId.Length == 0) return Reject("reject_plug_first", out reason);
            var req = FindRequest(app.Id);
            if (on && req != null && req.Done) return Reject("reject_already_done", out reason);
            app.SwitchedOn = on;
            reason = "";
            return true;
        }

        private bool ApplyBreaker(string circuitId, out string reason)
        {
            if (Phase != LevelPhase.Running) return Reject("reject_unpause_breaker", out reason);
            if (IsDayMode) return ResetHouse(out reason);
            var c = FindCircuit(circuitId);
            if (c == null) return Reject("reject_unknown", out reason);

            c.IsOn = !c.IsOn;
            if (Rules.breakerToggleResetsCounter)
            {
                c.OverTicks = 0;
                c.CoolTicks = 0;
            }
            if (c.IsOn) c.ResetWhileHot = c.OverTicks > 0;
            Emit(new SimEvent { Type = SimEventType.BreakerSwitched, Circuit = c.Id });
            if (!c.IsOn && !Rules.revealRequiresSight) RevealSwitchedOnSockets(c.Id);
            reason = "";
            return true;
        }

        private bool ApplyPlaceCord(string sourceId, string targetRoomId, out string reason)
        {
            if (CordsLeft <= 0) return Reject("reject_no_cords", out reason);
            var source = FindSocket(sourceId);
            if (source == null || source.OccupantId.Length > 0 || source.Reserved || source.IsCord || source.CordSocketId.Length > 0)
                return Reject("reject_cord_source", out reason);
            var target = FindRoom(targetRoomId);
            if (target == null || !AreNeighbours(source.RoomId, target.Id, Rules.cordThroughStairs))
                return Reject("reject_cord_too_far", out reason);

            var sourceRoom = FindRoom(source.RoomId);
            var cord = new SocketState
            {
                Id = CordSocketPrefix + source.Id,
                RoomId = target.Id,
                CircuitId = source.CircuitId,
                AssumedCircuitId = source.AssumedCircuitId,
                Revealed = source.Revealed,
                IsCord = true,
                CordSourceId = source.Id,
                CordSocketId = "",
                OccupantId = "",
                // Put the cord's end on the side of the room that faces the source.
                Pos = sourceRoom.Floor != target.Floor ? 0.5f : (sourceRoom.X < target.X ? 0.1f : 0.9f),
            };
            _sockets.Add(cord);
            source.CordSocketId = cord.Id;
            CordsLeft--;
            reason = "";
            return true;
        }

        private bool ApplyRemoveCord(string cordSocketId, out string reason)
        {
            var cord = FindSocket(cordSocketId);
            if (cord == null || !cord.IsCord) return Reject("reject_not_a_cord", out reason);
            if (cord.OccupantId.Length > 0) Vacate(FindAppliance(cord.OccupantId));
            FindSocket(cord.CordSourceId).CordSocketId = "";
            _sockets.Remove(cord);
            CordsLeft++;
            reason = "";
            return true;
        }

        private bool ApplyDoor(string doorId, out string reason)
        {
            var door = FindDoor(doorId);
            if (door == null || door.IsStairs) return Reject("reject_unknown", out reason);
            door.IsOpen = !door.IsOpen;
            Emit(new SimEvent { Type = SimEventType.DoorToggled, Door = door.Id });
            reason = "";
            return true;
        }

        /// <summary>Takes an appliance out of its socket. Unplugging or moving always leaves it switched off (R10, R11).</summary>
        private void Vacate(ApplianceState app)
        {
            if (app.SocketId.Length > 0) FindSocket(app.SocketId).OccupantId = "";
            app.SocketId = "";
            app.SwitchedOn = false;
            app.Unplugged = false;
            app.FloorRoomId = "";
        }

        /// <summary>
        /// Remembers appliances that a command just cut off, so a fragile one loses its progress even if it is
        /// plugged back and switched on within the same tick (v1.1 §2.9).
        /// </summary>
        private void NoteLostPower()
        {
            foreach (var app in _appliances)
                if (_prevPowered.Contains(app.Id) && !IsPowered(app)) _lostPowerByCommand.Add(app.Id);
        }

        private bool Reject(string key, out string reason)
        {
            reason = _strings.Get(key);
            return false;
        }

        private bool Reject(out string reason, string key, params (string name, object value)[] args)
        {
            reason = _strings.Format(key, args);
            return false;
        }

        // ---- power and reveals ---------------------------------------------------------

        /// <summary>Day mode: the house is dark. One reset at the panel turns every room back on.</summary>
        private bool ResetHouse(out string reason)
        {
            bool any = false;
            foreach (var c in _circuitsById)
            {
                if (c.IsOn) continue;
                c.IsOn = true;
                c.OverTicks = 0;
                c.CoolTicks = 0;
                any = true;
                Emit(new SimEvent { Type = SimEventType.BreakerSwitched, Circuit = c.Id });
            }
            if (!any) return Reject("reject_unknown", out reason);
            reason = "";
            return true;
        }

        private void LoseHeart()
        {
            if (Hearts <= 0) return;
            Hearts--;
            Emit(new SimEvent { Type = SimEventType.HeartLost });
        }

        /// <summary>
        /// Day mode, after the clock moves: requests not started in time are missed, then lose (no hearts),
        /// then win (the day is over), then new requests.
        /// </summary>
        private void DayEndOfTick()
        {
            foreach (var req in _requests)
            {
                if (!req.IsActive || req.ProgressTicks > 0 || Now <= req.DueTicks) continue;
                req.Missed = true;
                RequestsMissed++;
                Emit(new SimEvent { Type = SimEventType.RequestMissed, Appliance = req.ApplianceId, Room = req.RoomId });
                if (req.CostsHeart) LoseHeart();
            }
            if (Hearts <= 0)
            {
                Lose(LoseReason.OutOfHearts, "");
                return;
            }
            if (Now >= DayLengthTicks)
            {
                Phase = LevelPhase.Won;
                Emit(new SimEvent { Type = SimEventType.LevelWon });
                return;
            }
            int before = _requests.Count;
            _generator.Tick(this, Now, _requests);
            for (int i = before; i < _requests.Count; i++)
                Emit(new SimEvent { Type = SimEventType.RequestSpawned, Appliance = _requests[i].ApplianceId, Room = _requests[i].RoomId });
        }

        /// <summary>Day mode: every appliance plugged into a socket draws its load from that room; one carried, unplugged or on the floor draws nothing.</summary>
        private void DayAutoSwitch()
        {
            foreach (var app in _appliances)
                app.SwitchedOn = app.SocketId.Length > 0 && !app.Unplugged;
        }

        /// <summary>Day mode: a request only progresses while its appliance is powered in the requesting room.</summary>
        private bool ServesRequest(ApplianceState app, RequestState req)
        {
            return !IsDayMode || FindSocket(app.SocketId).RoomId == req.RoomId;
        }

        private bool IsPowered(ApplianceState app)
        {
            if (app.SocketId.Length == 0 || !app.SwitchedOn) return false;
            return FindCircuit(FindSocket(app.SocketId).CircuitId).IsOn;
        }

        /// <summary>Recomputes Powered on every appliance and Load on every circuit (R2).</summary>
        private void RecomputePower()
        {
            if (IsDayMode) DayAutoSwitch();
            foreach (var c in _circuits) c.Load = 0;
            foreach (var app in _appliances)
            {
                app.Powered = IsPowered(app);
                if (app.Powered) FindCircuit(FindSocket(app.SocketId).CircuitId).Load += app.Load;
            }
            int total = 0;
            foreach (var c in _circuits)
                if (c.IsOn) total += c.Load;
            MainLoad = total;
        }

        /// <summary>v1.0 R16: when a breaker goes OFF, sockets on it with a switched-on appliance are revealed.</summary>
        private void RevealSwitchedOnSockets(string circuitId)
        {
            foreach (var s in _sockets)
            {
                if (s.CircuitId != circuitId || s.Revealed || s.OccupantId.Length == 0) continue;
                if (FindAppliance(s.OccupantId).SwitchedOn) Reveal(s);
            }
        }

        /// <summary>v1.1 R16′: a visible socket whose appliance is switched on but whose breaker is OFF is revealed.</summary>
        private void RevealSeenDeadSockets()
        {
            for (int i = 0; i < _sockets.Count; i++)
            {
                var s = _sockets[i];
                if (s.Revealed || s.OccupantId.Length == 0 || !IsRoomVisible(s.RoomId)) continue;
                if (FindAppliance(s.OccupantId).SwitchedOn && !FindCircuit(s.CircuitId).IsOn) Reveal(s);
            }
        }

        private void Reveal(SocketState s)
        {
            RevealOne(s);
            // A cord and the socket it plugs into share a circuit, so seeing one tells you the other.
            if (s.IsCord) RevealOne(FindSocket(s.CordSourceId));
            else if (s.CordSocketId.Length > 0) RevealOne(FindSocket(s.CordSocketId));
        }

        private void RevealOne(SocketState s)
        {
            if (s.Revealed) return;
            s.Revealed = true;
            Emit(new SimEvent
            {
                Type = SimEventType.SocketRevealed,
                Socket = s.Id,
                Circuit = s.CircuitId,
                AssumptionWasWrong = s.AssumedCircuitId.Length > 0 && s.AssumedCircuitId != s.CircuitId,
            });
        }

        // ---- outcome -------------------------------------------------------------------

        private void Lose(LoseReason reason, string applianceId)
        {
            Phase = LevelPhase.Lost;
            LoseReason = reason;
            Emit(new SimEvent { Type = SimEventType.LevelLost, Reason = reason, Appliance = applianceId });
        }

        private void Emit(SimEvent e)
        {
            e.Tick = Now;
            if (_inStep) _events.Add(e);
            else _pending.Add(e);
        }

        // ---- construction --------------------------------------------------------------

        private void BuildRooms()
        {
            bool autoLayout = true;
            foreach (var r in Level.rooms)
                if (r.width > 0f) autoLayout = false;

            float x = 0f;
            foreach (var r in Level.rooms)
            {
                float width = r.width > 0f ? r.width : Rules.autoRoomWidth;
                _rooms.Add(new RoomState
                {
                    Id = r.id,
                    Name = r.name,
                    Floor = autoLayout ? 0 : r.floor,
                    X = autoLayout ? x : r.x,
                    Width = width,
                    Theme = string.IsNullOrEmpty(r.theme) ? r.id : r.theme,
                    LastSeenTick = -1,
                });
                x += width;
            }
            if (_rooms.Count == 0) throw new ArgumentException("A level needs at least one room.");
        }

        private void BuildDoors()
        {
            if (Level.doors.Count == 0)
            {
                for (int i = 1; i < _rooms.Count; i++)
                {
                    _doors.Add(new DoorState
                    {
                        Id = "D" + i,
                        RoomA = _rooms[i - 1].Id,
                        RoomB = _rooms[i].Id,
                        IsStairs = false,
                        IsOpen = true,
                    });
                }
                return;
            }

            foreach (var d in Level.doors)
            {
                RequireRoom(d.a, $"Door {d.id}");
                RequireRoom(d.b, $"Door {d.id}");
                var door = new DoorState
                {
                    Id = d.id,
                    RoomA = d.a,
                    RoomB = d.b,
                    IsStairs = d.kind == "stairs",
                    IsOpen = d.open,
                };
                if (door.IsStairs) PlaceFlight(door, d);
                _doors.Add(door);
            }
        }

        /// <summary>
        /// Day mode: where a flight of stairs between two floors runs. The level's own <see cref="DoorData.stairsBottomX"/>
        /// and <see cref="DoorData.stairsRun"/> when it gives a run; otherwise <see cref="RulesConfig.stairsRunMetres"/>
        /// long (shortened to fit), climbing toward +x from half a metre inside the x range both rooms share.
        /// </summary>
        private void PlaceFlight(DoorState door, DoorData d)
        {
            var a = FindRoom(d.a);
            var b = FindRoom(d.b);
            if (a.Floor == b.Floor) return;
            var lower = a.Floor < b.Floor ? a : b;
            var upper = lower == a ? b : a;
            float lo = Math.Max(lower.X, upper.X), hi = Math.Min(lower.X + lower.Width, upper.X + upper.Width);
            float bottom, run;
            if (d.stairsRun != 0f)
            {
                bottom = d.stairsBottomX;
                run = d.stairsRun;
            }
            else
            {
                if (hi - lo < 1.5f) return;
                run = Math.Min(Rules.stairsRunMetres, hi - lo - 1f);
                bottom = lo + 0.5f;
            }
            door.LowerRoomId = lower.Id;
            door.UpperRoomId = upper.Id;
            door.BottomXCm = Cm(bottom);
            door.TopXCm = Cm(bottom + run);
        }

        private void BuildCircuits()
        {
            if (IsDayMode)
            {
                // Each room is its own circuit, with the same id as the room.
                var caps = HouseRoller.RollRoomCapacities(Level.rooms, Level.day.houseCapacity, _rng);
                foreach (var r in Level.rooms)
                    _circuits.Add(new CircuitState { Id = r.id, ShownLabel = r.name, Capacity = caps[r.id], IsOn = true });
                _circuitsById.AddRange(_circuits);
                _circuitsById.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
                return;
            }
            foreach (var c in Level.circuits)
            {
                _circuits.Add(new CircuitState
                {
                    Id = c.id,
                    ShownLabel = c.label,
                    Capacity = c.capacity,
                    IsOn = true,
                });
            }
            _circuitsById.AddRange(_circuits);
            _circuitsById.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
        }

        private void BuildSockets()
        {
            // Spread sockets that have no position evenly across their room.
            var countInRoom = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var s in Level.sockets)
                countInRoom[s.room] = countInRoom.TryGetValue(s.room, out var n) ? n + 1 : 1;
            var indexInRoom = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var s in Level.sockets)
            {
                RequireRoom(s.room, $"Socket {s.id}");
                string circuit = IsDayMode ? s.room : s.circuit;
                if (FindCircuit(circuit) == null) throw new ArgumentException($"Socket {s.id} is on unknown circuit '{s.circuit}'.");
                indexInRoom[s.room] = indexInRoom.TryGetValue(s.room, out var i) ? i + 1 : 1;

                _sockets.Add(new SocketState
                {
                    Id = s.id,
                    RoomId = s.room,
                    CircuitId = circuit,
                    AssumedCircuitId = IsDayMode ? "" : s.assumed ?? "",
                    Revealed = IsDayMode || s.revealed,
                    CordSourceId = "",
                    CordSocketId = "",
                    OccupantId = "",
                    Pos = s.pos > 0f ? s.pos : (float)indexInRoom[s.room] / (countInRoom[s.room] + 1),
                });
            }
        }

        private void BuildAppliances()
        {
            if (IsDayMode)
            {
                foreach (var a in Level.appliances)
                {
                    var app = AddAppliance(a.id, a.type, "", fixedInPlace: false);
                    app.Load = HouseRoller.RollLoad(a, Catalog.Find(a.type), _rng);
                    PlaceFixed(app, a.socket, reserve: false);
                }
                _appliancesById.AddRange(_appliances);
                _appliancesById.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
                return;
            }
            foreach (var r in Level.requests)
            {
                RequireRoom(r.room, $"{r.owner}'s request");
                AddAppliance(r.appliance, r.appliance, r.owner, fixedInPlace: false);
                _requests.Add(new RequestState
                {
                    Owner = r.owner,
                    ApplianceId = r.appliance,
                    RoomId = r.room,
                    DurationTicks = Rules.SecondsToTicks(r.durationSec),
                    DueTicks = Rules.SecondsToTicks(r.dueSec),
                });
            }

            for (int i = 1; i <= Level.lampCount; i++)
                AddAppliance("lamp" + i, "lamp", "", fixedInPlace: false);

            if (!string.IsNullOrEmpty(Level.fridgeSocket))
            {
                var fridge = AddAppliance(FridgeId, "fridge", "", fixedInPlace: true);
                fridge.IsFridge = true;
                fridge.SwitchedOn = true;
                PlaceFixed(fridge, Level.fridgeSocket, reserve: false);
            }

            foreach (var r in Level.routines)
            {
                var app = AddAppliance(r.appliance, r.appliance, r.owner, fixedInPlace: true);
                app.IsRoutine = true;
                PlaceFixed(app, r.socket, reserve: true);
                int start = Rules.SecondsToTicks(r.startSec);
                _routines.Add(new RoutineState
                {
                    Owner = r.owner,
                    ApplianceId = r.appliance,
                    SocketId = r.socket,
                    StartTick = start,
                    EndTick = start + Rules.SecondsToTicks(r.durationSec),
                });
            }

            _appliancesById.AddRange(_appliances);
            _appliancesById.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
        }

        private ApplianceState AddAppliance(string id, string type, string owner, bool fixedInPlace)
        {
            var def = Catalog.Find(type) ?? throw new ArgumentException($"Appliance type '{type}' is not in the catalog.");
            if (FindAppliance(id) != null) throw new ArgumentException($"Appliance '{id}' appears twice in this level.");
            var app = new ApplianceState
            {
                Id = id,
                Type = type,
                Load = def.load,
                Fragile = def.fragile,
                Fixed = fixedInPlace,
                Owner = owner ?? "",
                SocketId = "",
            };
            _appliances.Add(app);
            return app;
        }

        private void PlaceFixed(ApplianceState app, string socketId, bool reserve)
        {
            var socket = FindSocket(socketId) ?? throw new ArgumentException($"'{app.Id}' uses unknown socket '{socketId}'.");
            if (socket.OccupantId.Length > 0) throw new ArgumentException($"Socket {socketId} is used by both '{socket.OccupantId}' and '{app.Id}'.");
            socket.OccupantId = app.Id;
            socket.Reserved = reserve;
            app.SocketId = socketId;
        }

        private void BuildCharacter()
        {
            string start = string.IsNullOrEmpty(Level.startRoom) ? _rooms[0].Id : Level.startRoom;
            RequireRoom(start, "startRoom");
            PanelRoomId = string.IsNullOrEmpty(Level.panelRoom) ? start : Level.panelRoom;
            RequireRoom(PanelRoomId, "panelRoom");

            Character.RoomId = start;
            Character.FromRoomId = start;
            Character.ToRoomId = start;
            var startRoom = FindRoom(start);
            Character.XCm = Cm(startRoom.CenterX);
            Character.PrevXCm = Character.XCm;
        }

        // ---- character -----------------------------------------------------------------

        /// <summary>
        /// One tick of the character (v1.1 §2.2). Walking or interacting spends the tick. Actions that take no
        /// time (interactTicks 0, nothing to walk) chain within the tick, so several can happen at once.
        /// </summary>
        private void CharacterTick()
        {
            if (IsDayMode)
            {
                for (int i = 0; i < _characters.Count; i++)
                {
                    _active = _characters[i];
                    KeyboardTick();
                }
                _active = _characters[0];
                return;
            }
            var ch = Character;
            var queue = ch.QueueList;
            // Each pass spends the tick (return), removes an action, or arrives at no cost (then the next
            // pass removes an action), so two passes per action is enough.
            int passes = 2 * queue.Count + 2;
            for (int pass = 0; pass < passes; pass++)
            {
                if (ch.IsWalking)
                {
                    WalkOneTick();
                    return;
                }
                if (queue.Count == 0) return;

                var head = queue[0];
                if (head.TargetRoomId == null) head.TargetRoomId = TargetRoomOf(head.Command);

                if (ch.RoomId != head.TargetRoomId)
                {
                    if (!PlanPath(ch.RoomId, head.TargetRoomId))
                    {
                        queue.RemoveAt(0);
                        Emit(new SimEvent { Type = SimEventType.CommandRejected, Message = _strings.Get("reject_no_path"), Command = head.Command });
                        continue;
                    }
                    StartNextSegment();
                    if (ch.IsWalking)
                    {
                        WalkOneTick();
                        return;
                    }
                    continue; // the whole way cost nothing
                }

                if (!head.Started)
                {
                    head.Started = true;
                    // Walking somewhere is done on arrival; everything else takes hands-on time.
                    ch.InteractTicksLeft = head.Command.Type == CommandType.MoveTo ? 0 : Rules.interactTicks;
                }
                if (ch.InteractTicksLeft > 0) ch.InteractTicksLeft--;
                if (ch.InteractTicksLeft > 0) return;

                queue.RemoveAt(0);
                if (Apply(head.Command, out var reason))
                    Emit(new SimEvent { Type = SimEventType.ActionDone, Command = head.Command, Room = ch.RoomId });
                else
                    Emit(new SimEvent { Type = SimEventType.CommandRejected, Message = reason, Command = head.Command });
                if (Rules.interactTicks > 0 && head.Command.Type != CommandType.MoveTo) return;
            }
        }

        // ---- day mode keyboard ---------------------------------------------------------

        /// <summary>Day mode: where the panel hangs, in metres.</summary>
        public float PanelX
        {
            get
            {
                var room = FindRoom(PanelRoomId);
                return room.X + Clamp01(Level.panelPos) * room.Width;
            }
        }

        /// <summary>Horizontal position of a socket, in metres.</summary>
        public float SocketX(SocketState s)
        {
            var room = FindRoom(s.RoomId);
            return room.X + s.Pos * room.Width;
        }

        private static int Cm(float metres) => (int)Math.Round(metres * 100f, MidpointRounding.AwayFromZero);
        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        /// <summary>Day mode: how far (centimetres, across the floor) the character reaches for sockets, appliances and the panel.</summary>
        public int ReachCm => Cm(Rules.reachMetres);

        /// <summary>
        /// Day mode: depth (centimetres) the character reaches sockets, the appliances in them and the panel from: the
        /// back of the walkable floor.
        /// </summary>
        public int BackZCm => Cm(Rules.walkDepthMetres);

        /// <summary>
        /// True when (x, z) is within <paramref name="rangeCm"/> of (tx, tz) across the floor (all centimetres), depth
        /// counting <see cref="RulesConfig.reachDepthWeight"/> of distance along the room.
        /// </summary>
        public bool Within(int xCm, int zCm, int txCm, int tzCm, int rangeCm) => Dist2(xCm, zCm, txCm, tzCm) <= (long)rangeCm * rangeCm;

        private long Dist2(int xCm, int zCm, int txCm, int tzCm)
        {
            long dx = xCm - txCm;
            long dz = (long)Math.Round((zCm - tzCm) * (double)Rules.reachDepthWeight);
            return dx * dx + dz * dz;
        }

        /// <summary>Day mode: the character can reach the panel now (in its room, at the back of the floor by it).</summary>
        public bool CanReachPanel => Character.RoomId == PanelRoomId && Within(Character.XCm, Character.ZCm, Cm(PanelX), BackZCm, ReachCm);

        /// <summary>Day mode: true when the character flies with the jetpack (rules verticalMove "jetpack") instead of jumping.</summary>
        public bool UsesJetpack => Rules.verticalMove == "jetpack";
        /// <summary>Day mode: true when floors are joined by walkable stairs (rules verticalMove "stairs").</summary>
        public bool UsesStairs => Rules.verticalMove == "stairs";
        /// <summary>Day mode: true when Space jumps between floors (rules verticalMove "jump").</summary>
        public bool UsesJump => Rules.verticalMove == "jump";

        /// <summary>
        /// Day mode: height (metres) over the lower floor of a point on a flight of stairs at x metres, from 0 at the
        /// bottom step to one floor at the top, clamped to the flight.
        /// </summary>
        public float StairsHeight(DoorState door, float x)
        {
            float bottom = door.BottomXCm / 100f, top = door.TopXCm / 100f;
            if (top == bottom) return 0f;
            float t = (x - bottom) / (top - bottom);
            return Math.Max(0f, Math.Min(1f, t)) * Rules.floorHeightMetres;
        }

        /// <summary>
        /// Day mode, verticalMove "jump": the room a jump would land in (<paramref name="direction"/> 1 = up, Space;
        /// -1 = down, Shift+Space): the room on the next floor up or down over (or under) the character's x, or null.
        /// Not while already in the air.
        /// </summary>
        public RoomState JumpTarget(int direction) => Character.IsAirborne ? null : RoomAcross(direction);

        /// <summary>Day mode: the room on the next floor up (1) or down (-1) over or under the character's x, or null.</summary>
        public RoomState RoomAcross(int direction)
        {
            var here = FindRoom(Character.RoomId);
            if (here == null || direction == 0) return null;
            int floor = here.Floor + Math.Sign(direction);
            int x = Character.XCm;
            foreach (var room in _rooms)
                if (room.Floor == floor && x > Cm(room.X) && x < Cm(room.X + room.Width)) return room;
            return null;
        }

        /// <summary>
        /// Day mode: upward push of the jetpack now, cm per tick per tick: <see cref="RulesConfig.jetpackThrust"/> less
        /// <see cref="RulesConfig.jetpackWeightThrust"/> per weight point of what is carried.
        /// </summary>
        public int JetpackThrustCm()
        {
            float thrust = Rules.jetpackThrust;
            var carried = Character.CarryingId.Length > 0 ? FindAppliance(Character.CarryingId) : null;
            var def = carried != null ? Catalog.Find(carried.Type) : null;
            if (def != null) thrust -= def.weight * Rules.jetpackWeightThrust;
            return AccelCm(Math.Max(0f, thrust));
        }

        private int AccelCm(float metresPerSecond2) => (int)Math.Round(metresPerSecond2 * 100f / (Rules.ticksPerSecond * Rules.ticksPerSecond), MidpointRounding.AwayFromZero);
        private int SpeedCm(float metresPerSecond) => (int)Math.Round(metresPerSecond * 100f / Rules.ticksPerSecond, MidpointRounding.AwayFromZero);

        private bool ApplyWalk(int direction, out string reason)
        {
            Character.WalkDir = Math.Sign(direction);
            if (Character.WalkDir != 0) Character.Facing = Character.WalkDir;
            reason = "";
            return true;
        }

        private bool ApplyWalkDepth(int direction, out string reason)
        {
            Character.DepthDir = Math.Sign(direction);
            reason = "";
            return true;
        }

        /// <summary>Space / Shift+Space (verticalMove "jump"): start a jump to the room straight above or below (<see cref="JumpTarget"/>).</summary>
        private bool ApplyJump(int direction, out string reason)
        {
            if (!UsesJump) return Reject("reject_unknown", out reason);
            var to = JumpTarget(direction);
            if (to == null) return Reject("reject_no_floor", out reason);
            var ch = Character;
            ch.JumpFromRoomId = ch.RoomId;
            ch.JumpToRoomId = to.Id;
            ch.JumpTicks = Math.Max(1, Rules.jumpTicks);
            ch.JumpTicksLeft = ch.JumpTicks;
            reason = "";
            return true;
        }

        /// <summary>Space held (1) or released (0), verticalMove "jetpack": the jetpack pushes up while held.</summary>
        private bool ApplyThrust(int on, out string reason)
        {
            if (!UsesJetpack) return Reject("reject_unknown", out reason);
            Character.Thrusting = on != 0;
            if (Character.Thrusting) Character.DropThrough = false;
            reason = "";
            return true;
        }

        /// <summary>
        /// S+Space or Shift+Space: drop through the floor under the character (the next one it falls onto) into the room
        /// below. Refused when there is no room below.
        /// </summary>
        private bool ApplyDrop(out string reason)
        {
            if (!UsesJetpack) return Reject("reject_unknown", out reason);
            if (RoomAcross(-1) == null) return Reject("reject_no_floor_below", out reason);
            Character.DropThrough = true;
            Character.Thrusting = false;
            reason = "";
            return true;
        }

        /// <summary>
        /// E: reset the panel during a blackout when standing at it; otherwise put down what is carried (unplugged,
        /// beside the nearest free socket or on the floor, by <see cref="RulesConfig.placeMode"/>; plugged in at once
        /// when <see cref="RulesConfig.placeUnplugged"/> is off); otherwise pick up the nearest appliance in reach.
        /// </summary>
        private bool ApplyInteract(out string reason)
        {
            var ch = Character;
            if (!ch.CanUseHands) return Reject("reject_unknown", out reason);

            bool dark = false;
            foreach (var c in _circuits)
                if (!c.IsOn) dark = true;
            if (dark && CanReachPanel)
                return ResetHouse(out reason);

            if (ch.CarryingId.Length > 0)
            {
                if (!Rules.placeUnplugged)
                {
                    var free = NearestFreeSocket(ch.RoomId, ch.XCm, ch.ZCm, ReachCm, 0);
                    if (free == null) return Reject("reject_no_free_socket", out reason);
                    return ApplyPlug(ch.CarryingId, free.Id, out reason);
                }
                return PutDown(ch.XCm, ch.ZCm, ReachCm, 0, SimEventType.PutDown, out reason);
            }

            var pick = NearestAppliance(ch.RoomId, ch.XCm, ch.ZCm, ReachCm);
            if (pick == null) return Reject("reject_unknown", out reason);
            string from = pick.SocketId;
            Vacate(pick);
            ch.CarryingId = pick.Id;
            Emit(new SimEvent { Type = SimEventType.PickedUp, Appliance = pick.Id, Socket = from });
            reason = "";
            return true;
        }

        /// <summary>
        /// Hold E: throw what is carried ahead (the way the character faces). "socket" mode lands it beside the
        /// nearest free socket ahead within <see cref="RulesConfig.throwMetres"/>; "floor" mode that far ahead, stopped
        /// by the room's walls, at the character's depth. It lands unplugged.
        /// </summary>
        private bool ApplyThrow(out string reason)
        {
            var ch = Character;
            if (!ch.CanUseHands || ch.CarryingId.Length == 0) return Reject("reject_nothing_carried", out reason);
            int range = Cm(Rules.throwMetres);
            if (FloorMode)
            {
                var room = FindRoom(ch.RoomId);
                int x = Math.Max(Cm(room.X) + 30, Math.Min(Cm(room.X + room.Width) - 30, ch.XCm + ch.Facing * range));
                return PutDown(x, ch.ZCm, 0, 0, SimEventType.Thrown, out reason);
            }
            // A throw flies over the floor: only how far along the room counts, not the depth.
            return PutDown(ch.XCm, null, range, ch.Facing, SimEventType.Thrown, out reason);
        }

        /// <summary>
        /// F: the nearest unplugged appliance in reach (beside a socket or on the floor) is plugged in; with none, the
        /// nearest plugged one is unplugged. In "floor" mode an appliance on the floor goes into the nearest free socket in reach of it, and
        /// one that is unplugged drops to the floor by its socket.
        /// </summary>
        private bool ApplyTogglePlug(out string reason)
        {
            var ch = Character;
            if (!ch.CanUseHands) return Reject("reject_unknown", out reason);
            var app = NearestAppliance(ch.RoomId, ch.XCm, ch.ZCm, ReachCm, unpluggedOnly: true) ?? NearestAppliance(ch.RoomId, ch.XCm, ch.ZCm, ReachCm);
            if (app == null) return Reject("reject_nothing_to_plug", out reason);
            if (app.Fixed) return Reject("reject_not_yours", out reason);

            if (app.FloorRoomId.Length > 0)
            {
                var socket = NearestFreeSocket(app.FloorRoomId, app.FloorXCm, app.FloorZCm, ReachCm, 0);
                if (socket == null) return Reject("reject_no_free_socket", out reason);
                app.FloorRoomId = "";
                socket.OccupantId = app.Id;
                app.SocketId = socket.Id;
                app.Unplugged = false;
                app.PluggedTick = Now;
                Emit(new SimEvent { Type = SimEventType.PluggedIn, Appliance = app.Id, Socket = socket.Id });
                reason = "";
                return true;
            }

            var at = FindSocket(app.SocketId);
            if (app.Unplugged)
            {
                app.Unplugged = false;
                app.PluggedTick = Now;
                Emit(new SimEvent { Type = SimEventType.PluggedIn, Appliance = app.Id, Socket = at.Id });
            }
            else Unplug(app);
            reason = "";
            return true;
        }

        /// <summary>
        /// Day mode: pulls <paramref name="app"/> out of its socket. "floor" mode drops it on the floor in front of the
        /// socket, "socket" mode leaves it beside the socket unplugged.
        /// </summary>
        private void Unplug(ApplianceState app)
        {
            var at = FindSocket(app.SocketId);
            if (FloorMode)
            {
                int x = Cm(SocketX(at));
                Vacate(app);
                app.FloorRoomId = at.RoomId;
                app.FloorXCm = x;
                app.FloorZCm = BackZCm;
            }
            else app.Unplugged = true;
            Emit(new SimEvent { Type = SimEventType.Unplugged, Appliance = app.Id, Socket = at.Id });
        }

        /// <summary>
        /// Day mode, on a trip: the plugs that pushed the room over pop out, newest first, until what is left fits,
        /// so resetting the breaker brings the power back instead of tripping again at once. The level's own
        /// appliances (fridge) never pop.
        /// </summary>
        private void PopNewestPlugs(CircuitState c)
        {
            var plugged = new List<ApplianceState>();
            foreach (var app in _appliances)
                if (app.Powered && !app.Fixed && FindSocket(app.SocketId).CircuitId == c.Id) plugged.Add(app);
            plugged.Sort((a, b) => a.PluggedTick != b.PluggedTick ? b.PluggedTick.CompareTo(a.PluggedTick) : string.CompareOrdinal(b.Id, a.Id));
            int load = c.Load;
            for (int i = 0; i < plugged.Count && load > c.Capacity; i++)
            {
                load -= plugged[i].Load;
                Unplug(plugged[i]);
            }
        }

        /// <summary>Day mode: appliances are put down on the floor (rules placeMode "floor"), not beside a socket.</summary>
        public bool FloorMode => string.Equals(Rules.placeMode, "floor", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Puts what the character carries down, unplugged: "floor" mode at (<paramref name="xCm"/>,
        /// <paramref name="zCm"/>) in the character's room; "socket" mode beside the nearest free socket within
        /// <paramref name="rangeCm"/> of that point (null depth = along the room only; only ahead when
        /// <paramref name="ahead"/> is 1 or -1).
        /// </summary>
        private bool PutDown(int xCm, int? zCm, int rangeCm, int ahead, SimEventType kind, out string reason)
        {
            var ch = Character;
            var app = FindAppliance(ch.CarryingId);
            if (FloorMode)
            {
                app.FloorRoomId = ch.RoomId;
                app.FloorXCm = xCm;
                app.FloorZCm = zCm ?? ch.ZCm;
                app.Unplugged = false;
                ch.CarryingId = "";
                Emit(new SimEvent { Type = kind, Appliance = app.Id, Room = ch.RoomId });
                reason = "";
                return true;
            }
            var socket = NearestFreeSocket(ch.RoomId, xCm, zCm, rangeCm, ahead);
            if (socket == null) return Reject("reject_no_free_socket", out reason);
            socket.OccupantId = app.Id;
            app.SocketId = socket.Id;
            app.Unplugged = true;
            ch.CarryingId = "";
            Emit(new SimEvent { Type = kind, Appliance = app.Id, Socket = socket.Id, Room = ch.RoomId });
            reason = "";
            return true;
        }

        /// <summary>
        /// The free socket of <paramref name="roomId"/> nearest (<paramref name="xCm"/>, <paramref name="zCm"/>), no
        /// further than <paramref name="rangeCm"/> across the floor (a socket is reached from <see cref="BackZCm"/>;
        /// null depth = measure along the room only); with <paramref name="ahead"/> 1 or -1 only sockets on that side
        /// count. Null if none.
        /// </summary>
        public SocketState NearestFreeSocket(string roomId, int xCm, int? zCm, int rangeCm, int ahead)
        {
            SocketState best = null;
            int bestDist = int.MaxValue;
            foreach (var s in _sockets)
            {
                if (s.RoomId != roomId || s.OccupantId.Length > 0 || s.Reserved || s.CordSocketId.Length > 0) continue;
                int dx = Cm(SocketX(s)) - xCm;
                if (ahead != 0 && dx != 0 && Math.Sign(dx) != ahead) continue;
                long d2 = Dist2(xCm, zCm ?? BackZCm, xCm + dx, BackZCm);
                if (d2 > (long)rangeCm * rangeCm) continue;
                int d = (int)Math.Sqrt(d2);
                if (d < bestDist)
                {
                    best = s;
                    bestDist = d;
                }
            }
            return best;
        }

        /// <summary>
        /// The appliance in a socket or on the floor of <paramref name="roomId"/> nearest (<paramref name="xCm"/>,
        /// <paramref name="zCm"/>) within reach across the floor, or null (one in a socket is reached from
        /// <see cref="BackZCm"/>). <paramref name="unpluggedOnly"/> = only ones unplugged or on the floor.
        /// </summary>
        public ApplianceState NearestAppliance(string roomId, int xCm, int zCm, int rangeCm, bool unpluggedOnly = false)
        {
            ApplianceState pick = null;
            int pickDist = int.MaxValue;
            foreach (var app in _appliancesById)
            {
                int x, z;
                if (unpluggedOnly && !app.Unplugged && app.FloorRoomId.Length == 0) continue;
                if (app.SocketId.Length > 0)
                {
                    var s = FindSocket(app.SocketId);
                    if (s.RoomId != roomId) continue;
                    x = Cm(SocketX(s));
                    z = BackZCm;
                }
                else if (app.FloorRoomId.Length > 0)
                {
                    if (app.FloorRoomId != roomId) continue;
                    x = app.FloorXCm;
                    z = app.FloorZCm;
                }
                else continue;
                long d2 = Dist2(xCm, zCm, x, z);
                if (d2 > (long)rangeCm * rangeCm) continue;
                int d = (int)Math.Sqrt(d2);
                if (d < pickDist)
                {
                    pick = app;
                    pickDist = d;
                }
            }
            return pick;
        }

        /// <summary>Depth in metres of an appliance in a socket (the back of the walkable floor) or on the floor; NaN while it is carried.</summary>
        public float ApplianceZ(ApplianceState app)
        {
            if (app.SocketId.Length > 0) return BackZCm / 100f;
            if (app.FloorRoomId.Length > 0) return app.FloorZ;
            return float.NaN;
        }

        /// <summary>x in metres of an appliance in a socket or on the floor; NaN while it is carried.</summary>
        public float ApplianceX(ApplianceState app)
        {
            if (app.SocketId.Length > 0) return SocketX(FindSocket(app.SocketId));
            if (app.FloorRoomId.Length > 0) return app.FloorX;
            return float.NaN;
        }

        /// <summary>
        /// Day mode: one tick of the keyboard character. Mid-jump it only flies on (<see cref="RulesConfig.jumpTicks"/>
        /// ticks, landing in the room above or below at the same x and depth). With the jetpack, first up and down
        /// (<see cref="FlyTick"/>). Then A/D walk (or fly) along the room, through a door only within <see cref="RulesConfig.doorDepthMetres"/> of the open front:
        /// walking into a door's wall further in slides the character out to the doorway instead of stopping it. W/S
        /// walk in and out between the open front and <see cref="RulesConfig.walkDepthMetres"/>.
        /// </summary>
        private void KeyboardTick()
        {
            var ch = Character;
            ch.PrevXCm = ch.XCm;
            ch.PrevZCm = ch.ZCm;
            if (ch.IsJumping)
            {
                ch.JumpTicksLeft--;
                if (ch.JumpTicksLeft == 0) EnterRoom(ch.JumpToRoomId);
                return;
            }
            if (ch.OnStairs)
            {
                StairsTick();
                return;
            }
            if (UsesJetpack) FlyTick();

            int depthStep = Math.Max(1, Cm(Rules.depthWalkSpeed / Rules.ticksPerSecond));
            if (ch.DepthDir != 0)
            {
                int z = ch.ZCm + ch.DepthDir * depthStep;
                ch.ZCm = Math.Max(0, Math.Min(Cm(Rules.walkDepthMetres), z));
            }
            if (ch.WalkDir == 0) return;

            const int wallGapCm = 30;
            int doorCm = Cm(Rules.doorDepthMetres);
            int x = ch.XCm + ch.WalkDir * Math.Max(1, Cm(Rules.walkSpeed / Rules.ticksPerSecond));
            if (UsesStairs && TryStepOntoStairs(x)) return;
            var room = FindRoom(ch.RoomId);
            for (int guard = 0; guard < _rooms.Count; guard++)
            {
                int left = Cm(room.X);
                int right = Cm(room.X + room.Width);
                if (x >= left && x <= right) break;
                // Only the doorway, at the open front, leads into the next room; deeper in it is a wall.
                var door = NeighbourThroughDoor(room, x > right ? 1 : -1);
                var next = ch.ZCm <= doorCm ? door : null;
                if (next == null)
                {
                    // Stop at the wall gap, but never push back from a spot already past it (that jittered every tick).
                    x = x > right ? Math.Max(ch.XCm, right - wallGapCm) : Math.Min(ch.XCm, left + wallGapCm);
                    // Pushing on a wall with a door in it: step out toward the doorway (unless W/S is steering).
                    if (door != null && ch.DepthDir == 0) ch.ZCm = Math.Max(doorCm, ch.ZCm - depthStep);
                    break;
                }
                room = next;
                EnterRoom(room.Id);
            }
            ch.XCm = x;
        }

        /// <summary>
        /// Day mode, stairs: walking along from <see cref="CharacterState.XCm"/> to <paramref name="nextX"/> in the front
        /// strip of the room (<see cref="RulesConfig.stairsDepthMetres"/>) across the bottom step going up, or across the
        /// top step going down, puts the character on that flight. True if it did (the step is then taken on the stairs).
        /// </summary>
        private bool TryStepOntoStairs(int nextX)
        {
            var ch = Character;
            if (ch.ZCm > Cm(Rules.stairsDepthMetres) || ch.IsAirborne) return false;
            foreach (var door in _doors)
            {
                if (!door.IsFlight || !door.IsOpen) continue;
                int up = Math.Sign(door.TopXCm - door.BottomXCm);
                bool onto;
                if (ch.RoomId == door.LowerRoomId)
                    onto = ch.WalkDir == up && (ch.XCm - door.BottomXCm) * up <= 0 && (nextX - door.BottomXCm) * up > 0;
                else if (ch.RoomId == door.UpperRoomId)
                    onto = ch.WalkDir == -up && (ch.XCm - door.TopXCm) * up >= 0 && (nextX - door.TopXCm) * up < 0;
                else continue;
                if (!onto) continue;
                ch.StairsDoorId = door.Id;
                ch.VyCm = 0;
                PlaceOnStairs(door, nextX);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Day mode, stairs: one tick on a flight. A/D walk up or down it at <see cref="RulesConfig.stairsSpeed"/>; past
        /// either end the character steps off onto that floor. W/S do nothing here.
        /// </summary>
        private void StairsTick()
        {
            var ch = Character;
            var door = FindDoor(ch.StairsDoorId);
            if (ch.WalkDir == 0) return;
            int x = ch.XCm + ch.WalkDir * Math.Max(1, Cm(Rules.stairsSpeed / Rules.ticksPerSecond));
            PlaceOnStairs(door, x);
        }

        /// <summary>
        /// Puts the character at x (cm) on a flight: its room is the lower one on the bottom half and the upper one on
        /// the top half, and <see cref="CharacterState.AirCm"/> is its height over that room's floor. At or past either
        /// end it steps off, onto the floor there.
        /// </summary>
        private void PlaceOnStairs(DoorState door, int x)
        {
            var ch = Character;
            int up = Math.Sign(door.TopXCm - door.BottomXCm);
            int length = Math.Abs(door.TopXCm - door.BottomXCm);
            int along = (x - door.BottomXCm) * up;
            int floorCm = Cm(Rules.floorHeightMetres);
            string room;
            if (along <= 0 || along >= length)
            {
                bool top = along >= length;
                ch.XCm = top ? door.TopXCm : door.BottomXCm;
                room = top ? door.UpperRoomId : door.LowerRoomId;
                ch.AirCm = 0;
                ch.StairsDoorId = "";
            }
            else
            {
                ch.XCm = x;
                int height = (int)((long)floorCm * along / length);
                room = along * 2 < length ? door.LowerRoomId : door.UpperRoomId;
                ch.AirCm = room == door.LowerRoomId ? height : height - floorCm;
            }
            if (room != ch.RoomId) EnterRoom(room);
        }

        /// <summary>
        /// Day mode: one tick of the jetpack. Gravity pulls down, a held Space pushes up (<see cref="JetpackThrustCm"/>);
        /// speed is capped both ways. Rising past the ceiling enters the room above when there is one there, else the
        /// head stops <see cref="RulesConfig.headroomMetres"/> under it. Falling lands on the first floor below, or
        /// passes through it into the room under it once after a drop (S+Space / Shift+Space).
        /// </summary>
        private void FlyTick()
        {
            var ch = Character;
            if (!ch.IsAirborne && !ch.Thrusting && !ch.DropThrough) return;
            int vy = ch.VyCm - AccelCm(Rules.gravity);
            if (ch.Thrusting) vy += JetpackThrustCm();
            ch.VyCm = Math.Max(-SpeedCm(Rules.maxFallSpeed), Math.Min(SpeedCm(Rules.jetpackMaxRise), vy));
            ch.AirCm += ch.VyCm;

            int floorCm = Cm(Rules.floorHeightMetres);
            if (ch.VyCm > 0)
            {
                var above = RoomAcross(1);
                if (above != null && ch.AirCm >= floorCm)
                {
                    EnterRoom(above.Id);
                    ch.AirCm -= floorCm;
                }
                else if (above == null && ch.AirCm > floorCm - Cm(Rules.headroomMetres))
                {
                    ch.AirCm = Math.Max(0, floorCm - Cm(Rules.headroomMetres));
                    ch.VyCm = 0;
                }
            }
            else if (ch.AirCm <= 0)
            {
                var below = ch.DropThrough ? RoomAcross(-1) : null;
                if (below != null)
                {
                    EnterRoom(below.Id);
                    ch.AirCm += floorCm;
                    ch.DropThrough = false;
                }
                else
                {
                    ch.AirCm = 0;
                    ch.VyCm = 0;
                    ch.DropThrough = false;
                }
            }
        }

        /// <summary>The room on the same floor right next to this one on that side, joined by an open door. Null = a wall.</summary>
        private RoomState NeighbourThroughDoor(RoomState room, int side)
        {
            int edge = Cm(side > 0 ? room.X + room.Width : room.X);
            foreach (var door in _doors)
            {
                if (door.IsStairs || !door.IsOpen || (door.RoomA != room.Id && door.RoomB != room.Id)) continue;
                var other = FindRoom(door.OtherRoom(room.Id));
                if (other.Floor != room.Floor) continue;
                int otherEdge = Cm(side > 0 ? other.X : other.X + other.Width);
                if (Math.Abs(otherEdge - edge) <= 1) return other;
            }
            return null;
        }

        /// <summary>The room the character has to be in to do this command.</summary>
        private string TargetRoomOf(Command cmd)
        {
            string here = Character.RoomId;
            switch (cmd.Type)
            {
                case CommandType.Plug:
                    return FindSocket(cmd.B)?.RoomId ?? here;
                case CommandType.Unplug:
                case CommandType.SetSwitch:
                {
                    var app = FindAppliance(cmd.A);
                    return app != null && app.SocketId.Length > 0 ? FindSocket(app.SocketId).RoomId : here;
                }
                case CommandType.ToggleBreaker:
                    return PanelRoomId;
                case CommandType.PlaceCord:
                case CommandType.RemoveCord:
                    return FindSocket(cmd.A)?.RoomId ?? here;
                case CommandType.ToggleDoor:
                {
                    var door = FindDoor(cmd.A);
                    if (door == null || here == door.RoomA || here == door.RoomB) return here;
                    return PathCost(here, door.RoomA) <= PathCost(here, door.RoomB) ? door.RoomA : door.RoomB;
                }
                case CommandType.MoveTo:
                    return FindRoom(cmd.A) != null ? cmd.A : here;
                default:
                    return here;
            }
        }

        private void WalkOneTick()
        {
            var ch = Character;
            ch.SegmentElapsed++;
            if (ch.RoomId != ch.ToRoomId && ch.SegmentElapsed * 2 >= ch.SegmentTicks) EnterRoom(ch.ToRoomId);
            if (ch.SegmentElapsed < ch.SegmentTicks) return;
            ch.IsWalking = false;
            ch.SegmentDoorId = "";
            if (_path.Count > 0) StartNextSegment();
        }

        /// <summary>
        /// Starts walking to the next room on the path. Opens a closed door on the way (it stays open, R21).
        /// Segments that cost nothing are crossed at once.
        /// </summary>
        private void StartNextSegment()
        {
            var ch = Character;
            while (_path.Count > 0)
            {
                string next = _path[0];
                _path.RemoveAt(0);
                var door = BestDoor(ch.RoomId, next, out int ticks);
                if (!door.IsOpen && !door.IsStairs)
                {
                    door.IsOpen = true;
                    Emit(new SimEvent { Type = SimEventType.DoorToggled, Door = door.Id });
                }

                ch.FromRoomId = ch.RoomId;
                ch.ToRoomId = next;
                ch.SegmentDoorId = door.Id;
                ch.SegmentTicks = ticks;
                ch.SegmentElapsed = 0;
                if (ticks > 0)
                {
                    ch.IsWalking = true;
                    return;
                }
                EnterRoom(next);
                ch.SegmentDoorId = "";
            }
            ch.IsWalking = false;
        }

        private void EnterRoom(string roomId)
        {
            Character.RoomId = roomId;
            Emit(new SimEvent { Type = SimEventType.CharacterEnteredRoom, Room = roomId });
        }

        /// <summary>Fills <see cref="_path"/> with the quickest way from one room to another. False if there is none.</summary>
        private bool PlanPath(string from, string to)
        {
            _path.Clear();
            var previous = ShortestPaths(from, out var cost);
            if (!cost.ContainsKey(to)) return false;
            for (string room = to; room != from; room = previous[room]) _path.Insert(0, room);
            return true;
        }

        /// <summary>Ticks to walk from one room to another, or int.MaxValue if unreachable.</summary>
        private int PathCost(string from, string to)
        {
            ShortestPaths(from, out var cost);
            return cost.TryGetValue(to, out int c) ? c : int.MaxValue;
        }

        /// <summary>Dijkstra over rooms. Ties go to the room that comes first in the level, so results never vary.</summary>
        private Dictionary<string, string> ShortestPaths(string from, out Dictionary<string, int> cost)
        {
            cost = new Dictionary<string, int>(StringComparer.Ordinal) { [from] = 0 };
            var previous = new Dictionary<string, string>(StringComparer.Ordinal);
            var done = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                string current = null;
                foreach (var room in _rooms)
                    if (!done.Contains(room.Id) && cost.ContainsKey(room.Id) && (current == null || cost[room.Id] < cost[current]))
                        current = room.Id;
                if (current == null) return previous;
                done.Add(current);
                foreach (var door in _doors)
                {
                    if (door.RoomA != current && door.RoomB != current) continue;
                    string other = door.OtherRoom(current);
                    int c = cost[current] + SegmentCost(door, current, other);
                    if (!cost.TryGetValue(other, out int known) || c < known)
                    {
                        cost[other] = c;
                        previous[other] = current;
                    }
                }
            }
        }

        /// <summary>The cheapest door between two rooms next to each other.</summary>
        private DoorState BestDoor(string from, string to, out int ticks)
        {
            DoorState best = null;
            ticks = int.MaxValue;
            foreach (var door in _doors)
            {
                if (!((door.RoomA == from && door.RoomB == to) || (door.RoomA == to && door.RoomB == from))) continue;
                int c = SegmentCost(door, from, to);
                if (c < ticks)
                {
                    best = door;
                    ticks = c;
                }
            }
            return best;
        }

        /// <summary>Walking time between two room centres through one door (v1.1 §2.2).</summary>
        private int SegmentCost(DoorState door, string from, string to)
        {
            float distance = Math.Abs(FindRoom(from).CenterX - FindRoom(to).CenterX);
            int ticks = (int)Math.Round(distance / Rules.walkSpeed * Rules.ticksPerSecond, MidpointRounding.AwayFromZero);
            if (door.IsStairs) ticks += Rules.stairsTicks;
            else if (!door.IsOpen) ticks += Rules.doorOpenTicks;
            return ticks;
        }

        // ---- sight ---------------------------------------------------------------------

        /// <summary>
        /// Works out which rooms the player can see (v1.1 §2.3) and remembers what was seen in them.
        /// Planning shows the whole house; after that the character's room plus rooms through open doors,
        /// up to sightDepth doors away.
        /// </summary>
        private void UpdateSight()
        {
            _visibleBefore.Clear();
            foreach (var room in _rooms)
                if (room.Visible) _visibleBefore.Add(room.Id);

            if (Phase == LevelPhase.Planning)
            {
                foreach (var room in _rooms) room.Visible = true;
            }
            else
            {
                foreach (var room in _rooms) room.Visible = false;
                var frontier = new List<string> { Character.RoomId };
                FindRoom(Character.RoomId).Visible = true;
                for (int depth = 0; depth < Rules.sightDepth && frontier.Count > 0; depth++)
                {
                    var next = new List<string>();
                    foreach (var roomId in frontier)
                    {
                        foreach (var door in _doors)
                        {
                            if (door.RoomA != roomId && door.RoomB != roomId) continue;
                            bool seeThrough = door.IsStairs ? Rules.sightThroughStairs : door.IsOpen;
                            if (!seeThrough) continue;
                            var other = FindRoom(door.OtherRoom(roomId));
                            if (other.Visible) continue;
                            other.Visible = true;
                            next.Add(other.Id);
                        }
                    }
                    frontier = next;
                }
            }

            bool changed = false;
            foreach (var room in _rooms)
            {
                if (room.Visible) room.LastSeenTick = Now;
                if (room.Visible != _visibleBefore.Contains(room.Id)) changed = true;
            }
            if (changed && _inStep) Emit(new SimEvent { Type = SimEventType.SightChanged });

            foreach (var app in _appliances)
            {
                bool seen = app.SocketId.Length > 0 ? IsRoomVisible(FindSocket(app.SocketId).RoomId)
                    : app.FloorRoomId.Length == 0 || IsRoomVisible(app.FloorRoomId);
                if (!seen) continue;
                app.SeenSocketId = app.SocketId;
                app.SeenUnplugged = app.Unplugged;
                app.SeenFloorRoomId = app.FloorRoomId;
                app.SeenFloorX = app.FloorX;
                app.SeenFloorZ = app.FloorZ;
                app.SeenSwitchedOn = app.SwitchedOn;
                app.SeenPowered = app.Powered;
                app.SeenTick = Now;
            }
        }

        // ---- helpers -------------------------------------------------------------------

        /// <summary>Rooms joined by a door (or by stairs when <paramref name="viaStairs"/>).</summary>
        public bool AreNeighbours(string roomA, string roomB, bool viaStairs)
        {
            foreach (var d in _doors)
            {
                if (d.IsStairs && !viaStairs) continue;
                if ((d.RoomA == roomA && d.RoomB == roomB) || (d.RoomA == roomB && d.RoomB == roomA)) return true;
            }
            return false;
        }

        private string NameOf(ApplianceState app) => Catalog.Find(app.Type)?.name ?? app.Type;

        private void RequireRoom(string id, string who)
        {
            if (FindRoom(id) == null) throw new ArgumentException($"{who} refers to unknown room '{id}'.");
        }

        private static T Find<T>(List<T> list, string id, Func<T, string> key) where T : class
        {
            if (id == null) return null;
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(key(list[i]), id, StringComparison.Ordinal)) return list[i];
            return null;
        }
    }
}
