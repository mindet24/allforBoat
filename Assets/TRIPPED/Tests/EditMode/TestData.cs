using System.Collections.Generic;
using Trouble.Sim;
using UnityEditor;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>Shared fixtures: catalog, rule sets, level files and a builder for tiny levels.</summary>
    internal static class TestData
    {
        /// <summary>Appliance loads from GDD v1.0 §4.1, built in code so rule tests do not depend on catalog.json.</summary>
        public static Catalog Catalog()
        {
            var data = new CatalogData();
            void Add(string id, string name, int load, bool fragile = false) =>
                data.appliances.Add(new ApplianceDef { id = id, name = name, load = load, fragile = fragile, shortLabel = id });
            Add("kettle", "Kettle", 6);
            Add("washer", "Washing machine", 6);
            Add("microwave", "Microwave", 5);
            Add("dryer", "Hair dryer", 5);
            Add("iron", "Iron", 5);
            Add("toaster", "Toaster", 4);
            Add("console", "Game console", 3, fragile: true);
            Add("fridge", "Fridge", 2);
            Add("phone", "Phone charger", 1);
            Add("lamp", "Lamp", 1);
            Add("heater", "Space heater", 6);
            Add("tv", "TV", 2);
            Add("ac", "Air conditioner", 8);
            return new Catalog(data);
        }

        /// <summary>
        /// The v1.0 rules the replay table was verified with: counter resets at once, flipping resets it,
        /// R16 reveals, and no walking (every action happens the tick it is given).
        /// </summary>
        public static RulesConfig V10Rules()
        {
            return Instant(new RulesConfig
            {
                overCoolEvery = 0,
                breakerToggleResetsCounter = true,
                counterAfterTrip = "reset",
                revealRequiresSight = false,
            });
        }

        /// <summary>The v1.1 defaults, walking included.</summary>
        public static RulesConfig V11Rules() => new RulesConfig();

        /// <summary>Makes walking and acting take no time and lifts the queue limit, leaving every other rule as given.</summary>
        public static RulesConfig Instant(RulesConfig rules)
        {
            rules.walkSpeed = 1_000_000f;
            rules.interactTicks = 0;
            rules.doorOpenTicks = 0;
            rules.stairsTicks = 0;
            rules.actionQueueSize = 1000;
            return rules;
        }

        public static Strings Strings() =>
            new Strings(JsonUtility.FromJson<StringsData>(Resources.Load<TextAsset>("Config/strings").text));

        /// <summary>Levels 1–4 from Resources; level 5 (cut from the game) from the test fixture.</summary>
        public static LevelData Level(int n)
        {
            var asset = n == 5
                ? AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TRIPPED/Tests/EditMode/Fixtures/level5.json")
                : Resources.Load<TextAsset>($"Levels/level{n}");
            return JsonUtility.FromJson<LevelData>(asset.text);
        }

        /// <summary>
        /// Plays a short script (see <see cref="Replay.Parse"/>) for at most <paramref name="ticks"/> ticks and
        /// returns every event. tick -1 commands are submitted during planning.
        /// </summary>
        public static List<SimEvent> Play(HouseSim sim, string script, int ticks)
        {
            var commands = Replay.Parse(script);
            int next = 0;
            while (next < commands.Count && commands[next].tick < 0)
                sim.Submit(Replay.ToCommand(commands[next++]), out _);
            sim.Start();

            var events = new List<SimEvent>();
            for (int i = 0; i < ticks && sim.Phase == LevelPhase.Running; i++)
            {
                while (next < commands.Count && commands[next].tick <= sim.Now)
                    sim.Submit(Replay.ToCommand(commands[next++]), out _);
                events.AddRange(sim.Step());
            }
            return events;
        }

        public static List<SimEvent> OfType(List<SimEvent> events, SimEventType type) => events.FindAll(e => e.Type == type);
    }

    /// <summary>Builds a small level in code. Defaults: 60 s day, no fridge, no cords, no lamps.</summary>
    internal sealed class LevelBuilder
    {
        private readonly LevelData _level = new LevelData { dayLengthSec = 60 };

        public LevelBuilder Room(string id) { _level.rooms.Add(new RoomData { id = id, name = id }); return this; }
        public LevelBuilder Circuit(string id, int capacity) { _level.circuits.Add(new CircuitData { id = id, label = id, capacity = capacity }); return this; }
        public LevelBuilder Socket(string id, string room, string circuit, string assumed = "", bool revealed = true)
        {
            _level.sockets.Add(new SocketData { id = id, room = room, circuit = circuit, assumed = assumed, revealed = revealed });
            return this;
        }
        public LevelBuilder Request(string owner, string appliance, string room, float durationSec, float dueSec)
        {
            _level.requests.Add(new RequestData { owner = owner, appliance = appliance, room = room, durationSec = durationSec, dueSec = dueSec });
            return this;
        }
        public LevelBuilder Routine(string owner, string appliance, string socket, float startSec, float durationSec)
        {
            _level.routines.Add(new RoutineData { owner = owner, appliance = appliance, socket = socket, startSec = startSec, durationSec = durationSec });
            return this;
        }
        public LevelBuilder Fridge(string socket, float spoilSec) { _level.fridgeSocket = socket; _level.fridgeSpoilSec = spoilSec; return this; }
        public LevelBuilder Main(int capacity) { _level.mainCapacity = capacity; return this; }
        public LevelBuilder Cords(int count) { _level.cordCount = count; return this; }
        public LevelBuilder Lamps(int count) { _level.lampCount = count; return this; }
        public LevelBuilder Day(float seconds) { _level.dayLengthSec = seconds; return this; }
        /// <summary>Adding any door turns off the automatic doors between neighbouring rooms.</summary>
        public LevelBuilder Door(string id, string a, string b, bool open = true, string kind = "door")
        {
            _level.doors.Add(new DoorData { id = id, a = a, b = b, open = open, kind = kind });
            return this;
        }
        public LevelBuilder Panel(string room) { _level.panelRoom = room; return this; }

        public LevelData Build() => _level;

        public HouseSim Sim(RulesConfig rules = null) => new HouseSim(_level, rules ?? TestData.V10Rules(), TestData.Catalog(), TestData.Strings());
    }
}
