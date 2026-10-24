using System.Collections.Generic;

namespace Trouble.Sim
{
    public enum LevelPhase { Planning, Running, Paused, Won, Lost }

    public enum LoseReason { None, Deadline, FridgeSpoiled, DayOver, OutOfHearts }

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
        /// <summary>Day mode, stairs between two floors: the room at the foot of the flight. Empty for doors.</summary>
        public string LowerRoomId { get; internal set; } = "";
        /// <summary>Day mode, stairs between two floors: the room at the top of the flight. Empty for doors.</summary>
        public string UpperRoomId { get; internal set; } = "";
        /// <summary>Day mode, stairs: x (cm) of the bottom step, on the lower floor.</summary>
        public int BottomXCm { get; internal set; }
        /// <summary>Day mode, stairs: x (cm) of the top step, on the upper floor.</summary>
        public int TopXCm { get; internal set; }
        /// <summary>True for stairs that can be walked in day mode (two rooms on different floors, a flight placed).</summary>
        public bool IsFlight => LowerRoomId.Length > 0 && TopXCm != BottomXCm;

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
        /// <summary>Day mode: standing at its socket but not plugged in, so it draws nothing until F.</summary>
        public bool Unplugged { get; internal set; }
        /// <summary>Day mode, placeMode "floor": room it lies in on the floor, away from any socket. Empty = not on the floor.</summary>
        public string FloorRoomId { get; internal set; } = "";
        /// <summary>Day mode: x (centimetres) where it lies on the floor of <see cref="FloorRoomId"/>.</summary>
        public int FloorXCm { get; internal set; }
        /// <summary>x in metres where it lies on the floor.</summary>
        public float FloorX => FloorXCm / 100f;
        /// <summary>Day mode: depth (centimetres from the open front) where it lies on the floor of <see cref="FloorRoomId"/>.</summary>
        public int FloorZCm { get; internal set; }
        /// <summary>Depth in metres where it lies on the floor.</summary>
        public float FloorZ => FloorZCm / 100f;

        // What the player last saw (V4). Views show these instead of the live values while the
        // appliance's room is not visible.

        /// <summary>Socket the appliance was in when last seen. Empty = in the tray.</summary>
        public string SeenSocketId { get; internal set; }
        public bool SeenSwitchedOn { get; internal set; }
        public bool SeenPowered { get; internal set; }
        public bool SeenUnplugged { get; internal set; }
        /// <summary>Floor room it was in when last seen (placeMode "floor"). Empty = not on the floor.</summary>
        public string SeenFloorRoomId { get; internal set; } = "";
        public float SeenFloorX { get; internal set; }
        public float SeenFloorZ { get; internal set; }
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

        // Day mode only. Scripted requests leave these at their defaults.

        /// <summary>Unique per request in a day, in the order they appeared. 0 for scripted requests.</summary>
        public int Serial { get; internal set; }
        /// <summary>Urgency id from the level (for example "urgent").</summary>
        public string Urgency { get; internal set; } = "";
        public int Points { get; internal set; }
        public bool CostsHeart { get; internal set; }
        public int SpawnTick { get; internal set; }
        /// <summary>The appliance was not running in the room by <see cref="DueTicks"/>.</summary>
        public bool Missed { get; internal set; }
        /// <summary>Not done and not missed.</summary>
        public bool IsActive => !Done && !Missed;
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
        /// <summary>Day mode: the appliance in the character's hands. Empty = hands free.</summary>
        public string CarryingId { get; internal set; } = "";

        // Day mode keyboard movement. Positions are whole centimetres so replays stay exact.

        /// <summary>Day mode: horizontal position in the house, in centimetres.</summary>
        public int XCm { get; internal set; }
        /// <summary>Day mode: horizontal position in metres, for the view.</summary>
        public float X => XCm / 100f;
        /// <summary>Day mode: held walking direction, -1, 0 or 1.</summary>
        public int WalkDir { get; internal set; }
        /// <summary>Day mode: depth into the room, centimetres from the open front (0) to rules.walkDepthMetres.</summary>
        public int ZCm { get; internal set; }
        /// <summary>Day mode: depth in metres, for the view.</summary>
        public float Z => ZCm / 100f;
        /// <summary>
        /// Day mode: x and depth (centimetres) at the start of the last tick, so the view can glide from there to
        /// <see cref="XCm"/> / <see cref="ZCm"/> between ticks instead of stepping.
        /// </summary>
        public int PrevXCm { get; internal set; }
        public int PrevZCm { get; internal set; }
        /// <summary>Day mode: held depth direction: 1 = W (into the room), -1 = S (toward the open front), 0 = none.</summary>
        public int DepthDir { get; internal set; }
        /// <summary>Day mode: last direction walked, -1 or 1.</summary>
        public int Facing { get; internal set; } = 1;
        /// <summary>Day mode: height of the feet above the floor of <see cref="RoomId"/>, centimetres (0 = standing on it).</summary>
        public int AirCm { get; internal set; }
        /// <summary>Day mode: height above the room's floor in metres, for the view.</summary>
        public float Air => AirCm / 100f;
        /// <summary>Day mode: vertical speed, centimetres per tick (up = positive).</summary>
        public int VyCm { get; internal set; }
        /// <summary>Day mode: Space is held, the jetpack pushes up.</summary>
        public bool Thrusting { get; internal set; }
        /// <summary>Day mode: dropping (S+Space / Shift+Space): the next floor it falls onto is passed through once.</summary>
        public bool DropThrough { get; internal set; }
        /// <summary>Day mode, verticalMove "jump": in the air, jumping to the floor above or below (Space / Shift+Space).</summary>
        public bool IsJumping => JumpTicksLeft > 0;
        public string JumpFromRoomId { get; internal set; } = "";
        public string JumpToRoomId { get; internal set; } = "";
        public int JumpTicks { get; internal set; }
        public int JumpTicksLeft { get; internal set; }
        /// <summary>Day mode: off the floor (jumping, flying, falling or dropping). E, F and throwing wait until it lands.</summary>
        public bool IsAirborne => !OnStairs && (IsJumping || AirCm != 0 || VyCm != 0 || DropThrough);
        /// <summary>Day mode, verticalMove "stairs": the stairs the character is walking on. Empty = not on stairs.</summary>
        public string StairsDoorId { get; internal set; } = "";
        /// <summary>True while walking up or down a flight of stairs (its height is <see cref="AirCm"/> over its room's floor).</summary>
        public bool OnStairs => StairsDoorId.Length > 0;
        /// <summary>Hands free to pick up, put down, throw or plug: not in the air and not on the stairs.</summary>
        public bool CanUseHands => !IsAirborne && !OnStairs;
        /// <summary>First entry is the action in progress.</summary>
        public IReadOnlyList<CharacterAction> Queue => QueueList;

        internal readonly List<CharacterAction> QueueList = new List<CharacterAction>();

        public float SegmentProgress => SegmentTicks <= 0 ? 1f : (float)SegmentElapsed / SegmentTicks;
    }
}
