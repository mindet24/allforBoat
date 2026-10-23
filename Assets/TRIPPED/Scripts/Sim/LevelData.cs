using System;
using System.Collections.Generic;

namespace Trouble.Sim
{
    // Plain data that mirrors one Resources/Levels/level{n}.json file.
    // Field names match the JSON keys exactly so JsonUtility can fill them.
    // Times are in seconds here; HouseSim converts them to ticks.

    /// <summary>One level as stored on disk. See GDD v1.0 §5.0 and v1.1 §2.10.</summary>
    [Serializable]
    public sealed class LevelData
    {
        public int id;
        public string title = "";
        public string teaches = "";
        public string intro = "";
        public float dayLengthSec;
        /// <summary>0 = this level has no main breaker.</summary>
        public int mainCapacity;
        /// <summary>Empty = this level has no fridge.</summary>
        public string fridgeSocket = "";
        public float fridgeSpoilSec;
        public int lampCount;
        public int cordCount;
        /// <summary>Room the character starts in. Empty = the first room.</summary>
        public string startRoom = "";
        /// <summary>Room that holds the panel. Empty = the start room.</summary>
        public string panelRoom = "";
        public List<RoomData> rooms = new List<RoomData>();
        /// <summary>Empty = an open door between each pair of rooms that are next to each other in <see cref="rooms"/>.</summary>
        public List<DoorData> doors = new List<DoorData>();
        public List<CircuitData> circuits = new List<CircuitData>();
        public List<SocketData> sockets = new List<SocketData>();
        public List<RequestData> requests = new List<RequestData>();
        public List<RoutineData> routines = new List<RoutineData>();
        public List<RuleOverrideData> rulesOverride = new List<RuleOverrideData>();
        public List<CommandData> reference = new List<CommandData>();
        public List<ScenarioData> scenarios = new List<ScenarioData>();
        /// <summary>Day mode only: appliances in the house at the start, each in its socket.</summary>
        public List<ApplianceData> appliances = new List<ApplianceData>();
        /// <summary>Day mode: requests are generated while the day runs. Off = the scripted levels 1–4.</summary>
        public DayData day = new DayData();
    }

    /// <summary>A room. If every room has width 0, rooms are laid out left to right on floor 0.</summary>
    [Serializable]
    public sealed class RoomData
    {
        public string id = "";
        public string name = "";
        public int floor;
        /// <summary>Left edge in metres.</summary>
        public float x;
        /// <summary>Width in metres. 0 = use <see cref="RulesConfig.autoRoomWidth"/>.</summary>
        public float width;
        public string theme = "";
        /// <summary>Day mode: this room's circuit capacity when <see cref="capRandom"/> is off.</summary>
        public int capacity;
        /// <summary>Day mode: roll the capacity so all rooms add up to <see cref="DayData.houseCapacity"/>.</summary>
        public bool capRandom;
        /// <summary>Day mode, random: the lowest capacity this room may get.</summary>
        public int capMin;
        /// <summary>Day mode, random: the highest capacity this room may get. 0 = no limit.</summary>
        public int capMax;
    }

    [Serializable]
    public sealed class DoorData
    {
        public string id = "";
        public string a = "";
        public string b = "";
        /// <summary>"door" or "stairs".</summary>
        public string kind = "door";
        public bool open = true;
    }

    [Serializable]
    public sealed class CircuitData
    {
        public string id = "";
        /// <summary>The label the player sees. May be "?" or a lie.</summary>
        public string label = "";
        public int capacity;
    }

    [Serializable]
    public sealed class SocketData
    {
        public string id = "";
        public string room = "";
        /// <summary>The real circuit.</summary>
        public string circuit = "";
        /// <summary>The circuit the labels suggest. Empty = none.</summary>
        public string assumed = "";
        public bool revealed;
        /// <summary>Horizontal position in the room, 0..1. 0 or less = spread evenly.</summary>
        public float pos;
    }

    [Serializable]
    public sealed class RequestData
    {
        public string owner = "";
        public string appliance = "";
        public string room = "";
        public float durationSec;
        public float dueSec;
    }

    [Serializable]
    public sealed class RoutineData
    {
        public string owner = "";
        public string appliance = "";
        public string socket = "";
        public float startSec;
        public float durationSec;
    }

    /// <summary>One entry of rulesOverride. The value is text so one list can hold numbers, booleans and words.</summary>
    [Serializable]
    public sealed class RuleOverrideData
    {
        public string key = "";
        public string value = "";
    }

    /// <summary>
    /// One scripted player command. tick -1 = applied during planning.
    /// cmd: plug(a=appliance, b=socket), unplug(a), on(a), off(a), breaker(a=circuit),
    /// cord(a=source socket, b=room), uncord(a=cord socket), door(a=door), move(a=room), cancel(a=queue index).
    /// </summary>
    [Serializable]
    public sealed class CommandData
    {
        public int tick;
        public string cmd = "";
        public string a = "";
        public string b = "";
        /// <summary>true = clears the character's queue first (a plain click). false = joins the end (Shift+click), the default for scripts.</summary>
        public bool replace;
    }

    /// <summary>An extra run that the Level Editor's Run Sim checks, with the result it expects.</summary>
    [Serializable]
    public sealed class ScenarioData
    {
        public string name = "";
        /// <summary>"won", "lost", or "notFasterThanReference".</summary>
        public string expect = "";
        public List<CommandData> commands = new List<CommandData>();
    }

    /// <summary>Day mode: one appliance placed in the house at the start.</summary>
    [Serializable]
    public sealed class ApplianceData
    {
        public string id = "";
        /// <summary>Catalog id.</summary>
        public string type = "";
        /// <summary>Socket it starts in.</summary>
        public string socket = "";
        /// <summary>Load when <see cref="loadRandom"/> is off. 0 = the catalog load.</summary>
        public int load;
        /// <summary>Roll the load between <see cref="loadMin"/> and <see cref="loadMax"/> at the start of the day.</summary>
        public bool loadRandom;
        public int loadMin;
        public int loadMax;
        /// <summary>Seconds of power one request needs. Ignored when <see cref="DayData.uniformUseSec"/> is above 0.</summary>
        public float useSec = 10f;
    }

    /// <summary>
    /// Day mode settings (design: TRIPPED day mode, 2026-10-24). Each room is its own circuit; a room over its
    /// capacity for overTripTicks blacks out the whole house; requests appear over time and the player carries
    /// appliances to them.
    /// </summary>
    [Serializable]
    public sealed class DayData
    {
        public bool enabled;
        /// <summary>Seed for every roll of the day. 0 = the game picks one when the level starts.</summary>
        public int seed;
        /// <summary>Sum of every room capacity. Random rooms share what the fixed rooms leave.</summary>
        public int houseCapacity;
        public int hearts = 3;
        /// <summary>Above 0 = every appliance needs this many seconds of power, whatever its own useSec.</summary>
        public float uniformUseSec;
        public float firstRequestSec = 3f;
        /// <summary>Seconds between requests at the start of the day.</summary>
        public float intervalStartSec = 15f;
        /// <summary>Seconds between requests at the end of the day. The gap shrinks evenly in between.</summary>
        public float intervalEndSec = 6f;
        /// <summary>Each gap is multiplied by a random factor in 1 ± this.</summary>
        public float intervalJitter = 0.25f;
        public int maxActive = 5;
        /// <summary>Chance that a request comes with a second one from the same person, at another urgency.</summary>
        public float bundleChance = 0.35f;
        public List<UrgencyData> urgencies = new List<UrgencyData>();
        public List<PersonData> people = new List<PersonData>();
    }

    /// <summary>One urgency level of a request.</summary>
    [Serializable]
    public sealed class UrgencyData
    {
        public string id = "";
        /// <summary>Seconds the player has to get the appliance running in the person's room.</summary>
        public float dueSec;
        public int points;
        /// <summary>true = missing it costs a heart.</summary>
        public bool costsHeart;
        /// <summary>Relative chance of this urgency.</summary>
        public float weight = 1f;
    }

    /// <summary>Someone in the house who makes requests.</summary>
    [Serializable]
    public sealed class PersonData
    {
        public string name = "";
        /// <summary>Rooms this person asks from. They wait in the room until the request is done or missed.</summary>
        public List<string> rooms = new List<string>();
        /// <summary>Appliance ids this person asks for. Empty = any.</summary>
        public List<string> wants = new List<string>();
    }
}
