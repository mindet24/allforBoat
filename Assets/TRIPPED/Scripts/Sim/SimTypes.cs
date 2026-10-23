using System.Collections.Generic;

namespace Trouble.Sim
{
    public enum LevelPhase { Planning, Running, Paused, Won, Lost }

    public enum LoseReason { None, Deadline, FridgeSpoiled, DayOver }

    // Read-only views of sim state. Setters are internal: only HouseSim changes them.
    // Times are ticks (1 tick = 1 / RulesConfig.ticksPerSecond seconds). Lengths are metres.

    /// <summary>A room of the house and whether the player can see it.</summary>
    public sealed class RoomState
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public int Floor { get; internal set; }
        public float X { get; internal set; }
        public float Width { get; internal set; }
        public string Theme { get; internal set; }
        public bool Visible { get; internal set; }
        /// <summary>Last tick the room was visible. -1 = never.</summary>
        public int LastSeenTick { get; internal set; }

        public float CenterX => X + Width * 0.5f;
    }

    /// <summary>A door or stairs between two rooms.</summary>
    public sealed class DoorState
    {
        public string Id { get; internal set; }
        public string RoomA { get; internal set; }
        public string RoomB { get; internal set; }
        public bool IsStairs { get; internal set; }
        public bool IsOpen { get; internal set; }

        public string OtherRoom(string roomId) => roomId == RoomA ? RoomB : RoomA;
    }

    /// <summary>A circuit and its breaker.</summary>
    public sealed class CircuitState
    {
        public string Id { get; internal set; }
        public string ShownLabel { get; internal set; }
        public int Capacity { get; internal set; }
        /// <summary>The breaker is ON.</summary>
        public bool IsOn { get; internal set; }
        public int Load { get; internal set; }
        /// <summary>Overload counter. The breaker trips when it reaches RulesConfig.overTripTicks.</summary>
        public int OverTicks { get; internal set; }
        /// <summary>Load is above capacity this tick.</summary>
        public bool Overloading { get; internal set; }
        /// <summary>OFF with a counter that has not cooled to 0 yet: resetting now under the same load trips again at once (GDD v1.1 R5b). Show "HOT".</summary>
        public bool IsHot => !IsOn && OverTicks > 0;

        internal int CoolTicks;
        /// <summary>Set when the player switched this breaker ON while its counter was still above 0.</summary>
        internal bool ResetWhileHot;
    }

    public sealed class SocketState
    {
        public string Id { get; internal set; }
        public string RoomId { get; internal set; }
        /// <summary>The real circuit. The view must show it only when <see cref="Revealed"/>.</summary>
        public string CircuitId { get; internal set; }
        /// <summary>The circuit the labels suggest. Empty = none.</summary>
        public string AssumedCircuitId { get; internal set; }
        public bool Revealed { get; internal set; }
        /// <summary>Held by a routine; the player cannot use it.</summary>
        public bool Reserved { get; internal set; }
        /// <summary>This socket is the far end of an extension cord.</summary>
        public bool IsCord { get; internal set; }
        /// <summary>For a cord socket: the real socket the cord is plugged into. Otherwise empty.</summary>
        public string CordSourceId { get; internal set; }
        /// <summary>For a real socket with a cord in it: the cord socket. Otherwise empty.</summary>
        public string CordSocketId { get; internal set; }
        /// <summary>Appliance plugged in here. Empty = free.</summary>
        public string OccupantId { get; internal set; }
        /// <summary>Horizontal position inside the room, 0..1.</summary>
        public float Pos { get; internal set; }
    }

    public sealed class ApplianceState
    {
        /// <summary>Instance id: the type id for request and routine appliances, "lamp1".. for lamps, "fridge" for the fridge.</summary>
        public string Id { get; internal set; }
        /// <summary>Catalog id.</summary>
        public string Type { get; internal set; }
        public int Load { get; internal set; }
        public bool Fragile { get; internal set; }
        /// <summary>The player cannot move or switch it (fridge, routines).</summary>
        public bool Fixed { get; internal set; }
        public bool IsFridge { get; internal set; }
        public bool IsRoutine { get; internal set; }
        /// <summary>Request or routine owner. Empty for lamps and the fridge.</summary>
        public string Owner { get; internal set; }
        /// <summary>Empty = not plugged in (in the tray).</summary>
        public string SocketId { get; internal set; }
        public bool SwitchedOn { get; internal set; }
        public bool Powered { get; internal set; }

        // What the player last saw (V4). Views show these instead of the live values while the
        // appliance's room is not visible.

        /// <summary>Socket the appliance was in when last seen. Empty = in the tray.</summary>
        public string SeenSocketId { get; internal set; }
        public bool SeenSwitchedOn { get; internal set; }
        public bool SeenPowered { get; internal set; }
        /// <summary>Tick it was last seen. -1 = never.</summary>
        public int SeenTick { get; internal set; } = -1;
    }

    public sealed class RequestState
    {
        public string Owner { get; internal set; }
        public string ApplianceId { get; internal set; }
        public string RoomId { get; internal set; }
        public int DurationTicks { get; internal set; }
        public int DueTicks { get; internal set; }
        public int ProgressTicks { get; internal set; }
        public bool Done { get; internal set; }
    }

    public sealed class RoutineState
    {
        public string Owner { get; internal set; }
        public string ApplianceId { get; internal set; }
        public string SocketId { get; internal set; }
        public int StartTick { get; internal set; }
        public int EndTick { get; internal set; }
    }

    /// <summary>One action waiting in, or being done by, the character.</summary>
    public sealed class CharacterAction
    {
        public Command Command { get; internal set; }
        /// <summary>Room the character must reach before doing it. Null until the action reaches the front of the queue.</summary>
        public string TargetRoomId { get; internal set; }

        internal bool Started;
    }

    /// <summary>
    /// The player character. While walking it moves from <see cref="FromRoomId"/> to <see cref="ToRoomId"/>
    /// over <see cref="SegmentTicks"/> ticks; the view places the sprite at <see cref="SegmentProgress"/>.
    /// </summary>
    public sealed class CharacterState
    {
        /// <summary>The room the character counts as being in (changes at the middle of a door).</summary>
        public string RoomId { get; internal set; }
        public bool IsWalking { get; internal set; }
        public string FromRoomId { get; internal set; }
        public string ToRoomId { get; internal set; }
        public int SegmentTicks { get; internal set; }
        public int SegmentElapsed { get; internal set; }
        /// <summary>Door or stairs being used by the current segment. Empty when not walking.</summary>
        public string SegmentDoorId { get; internal set; } = "";
        /// <summary>Ticks left of the current interaction. 0 = not interacting.</summary>
        public int InteractTicksLeft { get; internal set; }
        /// <summary>First entry is the action in progress.</summary>
        public IReadOnlyList<CharacterAction> Queue => QueueList;

        internal readonly List<CharacterAction> QueueList = new List<CharacterAction>();

        public float SegmentProgress => SegmentTicks <= 0 ? 1f : (float)SegmentElapsed / SegmentTicks;
    }
}
