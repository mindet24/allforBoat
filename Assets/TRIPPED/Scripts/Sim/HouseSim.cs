using System;
using System.Collections.Generic;

namespace Trouble.Sim
{
    /// <summary>
    /// The whole game state of one level and every rule that changes it.
    /// Pure C#: no UnityEngine, no randomness, no real time. One call to <see cref="Step"/> = one tick.
    /// </summary>
    /// <remarks>
    /// STUB (23 Oct): the constructor builds the real starting state from level data, so views can draw
    /// any level. <see cref="Submit"/> rejects everything and <see cref="Step"/> only advances time.
    /// The rules arrive in later commits without changing this public API.
    /// </remarks>
    public sealed class HouseSim
    {
        public const string FridgeId = "fridge";
        public const string CordSocketPrefix = "CORD-";

        private readonly List<RoomState> _rooms = new List<RoomState>();
        private readonly List<DoorState> _doors = new List<DoorState>();
        private readonly List<CircuitState> _circuits = new List<CircuitState>();
        private readonly List<SocketState> _sockets = new List<SocketState>();
        private readonly List<ApplianceState> _appliances = new List<ApplianceState>();
        private readonly List<RequestState> _requests = new List<RequestState>();
        private readonly List<RoutineState> _routines = new List<RoutineState>();
        private readonly List<SimEvent> _events = new List<SimEvent>();

        /// <param name="level">Level data as loaded from JSON.</param>
        /// <param name="rules">Global rules. The level's rulesOverride is applied on top.</param>
        /// <param name="catalog">Appliance types.</param>
        /// <exception cref="ArgumentException">The level refers to something that does not exist.</exception>
        public HouseSim(LevelData level, RulesConfig rules, Catalog catalog)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

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
        public int MainLoad { get; private set; }
        /// <summary>0 = no main breaker.</summary>
        public int MainCapacity { get; }
        public int MainOverTicks { get; private set; }
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

        /// <summary>Planning → Running. Called once, when the player first presses Space.</summary>
        public void Start()
        {
            if (Phase == LevelPhase.Planning) Phase = LevelPhase.Running;
        }

        /// <summary>Running ↔ Paused. Ignored in Planning, Won and Lost.</summary>
        public void SetPaused(bool paused)
        {
            if (paused && Phase == LevelPhase.Running) Phase = LevelPhase.Paused;
            else if (!paused && Phase == LevelPhase.Paused) Phase = LevelPhase.Running;
        }

        /// <summary>
        /// Hands a player command to the sim. The sim decides whether it happens now (planning)
        /// or goes into the character's queue (running or paused).
        /// </summary>
        /// <param name="reason">Rejection text for the toast when this returns false.</param>
        /// <returns>false if the command was rejected; state did not change.</returns>
        public bool Submit(Command command, out string reason)
        {
            reason = "Not implemented yet.";
            return false;
        }

        /// <summary>
        /// Advances one tick if the phase is Running. Returns the events of this tick; the list is
        /// reused by the next call, so copy it if you need to keep it.
        /// </summary>
        public IReadOnlyList<SimEvent> Step()
        {
            _events.Clear();
            if (Phase != LevelPhase.Running) return _events;
            Now++;
            return _events;
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

        // ---- sight ---------------------------------------------------------------------

        /// <summary>Planning shows the whole house (V3). Sight rules (V1) arrive with the character rules.</summary>
        private void UpdateSight()
        {
            foreach (var room in _rooms)
            {
                room.Visible = true;
                room.LastSeenTick = Now;
            }
        }

        // ---- helpers -------------------------------------------------------------------

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
