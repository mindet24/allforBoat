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
            MainCapacity = level.mainCapacity;
            SpoilLimitTicks = Rules.SecondsToTicks(level.fridgeSpoilSec);
            CordsLeft = level.cordCount;
            Phase = LevelPhase.Planning;

            BuildRooms();
            BuildDoors();
            BuildCircuits();
            BuildSockets();
            BuildAppliances();
            BuildCharacter();
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
        public CharacterState Character { get; } = new CharacterState();

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
        public RequestState FindRequest(string applianceId) => Find(_requests, applianceId, r => r.ApplianceId);

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
            reason = "";
            if (Phase == LevelPhase.Won || Phase == LevelPhase.Lost)
                return Reject("reject_level_over", out reason);
            if (command.Type == CommandType.CancelAction)
                return CancelAction(command.Index, out reason);

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
            try
            {
                StepInternal();
            }
            finally
            {
                _inStep = false;
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
                if (trip) TripCircuit(c);
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
                if (trip) TripMain(total);
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
                if (req == null || req.Done) continue;
                req.ProgressTicks++;
                if (req.ProgressTicks >= req.DurationTicks)
                {
                    req.Done = true;
                    app.SwitchedOn = false;
                    Emit(new SimEvent { Type = SimEventType.RequestCompleted, Appliance = app.Id });
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
            });
            TripCount++;
            TurnOff(c);
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
            MainOverTicks = 0;
            _mainCool = 0;
            MainOverloading = false;
            foreach (var c in _circuitsById)
                if (c.IsOn) TurnOff(c);
        }

        /// <summary>A breaker goes OFF by tripping. Its counter restarts.</summary>
        private void TurnOff(CircuitState c)
        {
            c.IsOn = false;
            c.OverTicks = 0;
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
            var req = FindRequest(app.Id);
            if (req != null && req.RoomId != socket.RoomId)
                return Reject(out reason, "reject_wrong_room", ("owner", req.Owner), ("room", FindRoom(req.RoomId).Name));

            Vacate(app);
            socket.OccupantId = app.Id;
            app.SocketId = socket.Id;
            reason = "";
            return true;
        }

        private bool ApplyUnplug(string applianceId, out string reason)
        {
            var app = FindAppliance(applianceId);
            if (app == null || app.Fixed || app.SocketId.Length == 0) return Reject("reject_cant_unplug", out reason);
            Vacate(app);
            reason = "";
            return true;
        }

        private bool ApplySwitch(string applianceId, bool on, out string reason)
        {
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
            var c = FindCircuit(circuitId);
            if (c == null) return Reject("reject_unknown", out reason);

            c.IsOn = !c.IsOn;
            if (Rules.breakerToggleResetsCounter)
            {
                c.OverTicks = 0;
                c.CoolTicks = 0;
            }
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

        private bool IsPowered(ApplianceState app)
        {
            if (app.SocketId.Length == 0 || !app.SwitchedOn) return false;
            return FindCircuit(FindSocket(app.SocketId).CircuitId).IsOn;
        }

        /// <summary>Recomputes Powered on every appliance and Load on every circuit (R2).</summary>
        private void RecomputePower()
        {
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
                _doors.Add(new DoorState
                {
                    Id = d.id,
                    RoomA = d.a,
                    RoomB = d.b,
                    IsStairs = d.kind == "stairs",
                    IsOpen = d.open,
                });
            }
        }

        private void BuildCircuits()
        {
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
                if (FindCircuit(s.circuit) == null) throw new ArgumentException($"Socket {s.id} is on unknown circuit '{s.circuit}'.");
                indexInRoom[s.room] = indexInRoom.TryGetValue(s.room, out var i) ? i + 1 : 1;

                _sockets.Add(new SocketState
                {
                    Id = s.id,
                    RoomId = s.room,
                    CircuitId = s.circuit,
                    AssumedCircuitId = s.assumed ?? "",
                    Revealed = s.revealed,
                    CordSourceId = "",
                    CordSocketId = "",
                    OccupantId = "",
                    Pos = s.pos > 0f ? s.pos : (float)indexInRoom[s.room] / (countInRoom[s.room] + 1),
                });
            }
        }

        private void BuildAppliances()
        {
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
        }

        // ---- character -----------------------------------------------------------------

        /// <summary>
        /// One tick of the character (v1.1 §2.2). Walking or interacting spends the tick. Actions that take no
        /// time (interactTicks 0, nothing to walk) chain within the tick, so several can happen at once.
        /// </summary>
        private void CharacterTick()
        {
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
                bool seen = app.SocketId.Length == 0 || IsRoomVisible(FindSocket(app.SocketId).RoomId);
                if (!seen) continue;
                app.SeenSocketId = app.SocketId;
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
