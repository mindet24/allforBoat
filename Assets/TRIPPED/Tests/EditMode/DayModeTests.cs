using System;
using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>Day mode (design 2026-10-24): rolls, generated requests, carrying, blackout, hearts.</summary>
    public class DayModeTests
    {
        private const int Seed = 12345;

        // ---- fixtures -----------------------------------------------------------------

        /// <summary>
        /// Two rooms joined by a door: "a" (where Mum asks) and "b" (panel, appliances start here).
        /// Fixed capacities, one request at tick 0 and no more for the rest of the day.
        /// </summary>
        private static LevelData TwoRooms(int capA, string[] wants, bool urgentOnly = true, float bundleChance = 0f, int hearts = 3, float dueSec = 20f, float daySec = 120f)
        {
            var level = new LevelData
            {
                id = 900,
                dayLengthSec = daySec,
                startRoom = "b",
                panelRoom = "b",
            };
            level.rooms.Add(new RoomData { id = "a", name = "Room A", capacity = capA });
            level.rooms.Add(new RoomData { id = "b", name = "Room B", capacity = 20 });
            level.doors.Add(new DoorData { id = "D", a = "a", b = "b" });
            foreach (var id in new[] { "A1", "A2" }) level.sockets.Add(new SocketData { id = id, room = "a" });
            foreach (var id in new[] { "B1", "B2", "B3" }) level.sockets.Add(new SocketData { id = id, room = "b" });
            level.appliances.Add(new ApplianceData { id = "kettle", type = "kettle", socket = "B1", load = 6, useSec = 8f });
            level.appliances.Add(new ApplianceData { id = "toaster", type = "toaster", socket = "B2", load = 3, useSec = 12f });

            var day = level.day;
            day.enabled = true;
            day.seed = Seed;
            day.hearts = hearts;
            day.firstRequestSec = 0f;
            day.intervalStartSec = 10_000f;
            day.intervalEndSec = 10_000f;
            day.intervalJitter = 0f;
            day.bundleChance = bundleChance;
            day.urgencies.Add(new UrgencyData { id = "urgent", dueSec = dueSec, points = 3, costsHeart = true, weight = 1f });
            if (!urgentOnly) day.urgencies.Add(new UrgencyData { id = "normal", dueSec = dueSec, points = 2, costsHeart = false, weight = 1f });
            day.people.Add(new PersonData { name = "Mum", rooms = new List<string> { "a" }, wants = new List<string>(wants) });
            return level;
        }

        private static RulesConfig Rules(bool instant = true)
        {
            var rules = TestData.V11Rules();
            rules.overTripTicks = 30;
            rules.counterAfterTrip = "reset";
            rules.sightDepth = 99;
            return instant ? TestData.Instant(rules) : rules;
        }

        private static HouseSim Sim(LevelData level, RulesConfig rules = null, Catalog catalog = null) =>
            new HouseSim(level, rules ?? Rules(), catalog ?? TestData.Catalog(), TestData.Strings());

        private static LevelData Demo() =>
            JsonUtility.FromJson<LevelData>(Resources.Load<TextAsset>("Levels/level_demo").text);

        /// <summary>The demo level is checked against the catalog it ships with.</summary>
        private static Catalog ShippedCatalog() =>
            new Catalog(JsonUtility.FromJson<CatalogData>(Resources.Load<TextAsset>("Config/catalog").text));

        private static List<SimEvent> Run(HouseSim sim, int ticks)
        {
            var events = new List<SimEvent>();
            for (int i = 0; i < ticks && sim.Phase == LevelPhase.Running; i++) events.AddRange(sim.Step());
            return events;
        }

        private static void Submit(HouseSim sim, Command command)
        {
            Assert.IsTrue(sim.Submit(command, out var reason), reason);
        }

        // ---- rolls --------------------------------------------------------------------

        [Test]
        public void RollRoomCapacities_RandomRooms_SumToHouseCapacityWithinMinAndMax()
        {
            // Arrange
            var rooms = Demo().rooms;

            for (int seed = 1; seed <= 200; seed++)
            {
                // Act
                var caps = HouseRoller.RollRoomCapacities(rooms, 35, new SimRandom(seed));

                // Assert
                int sum = 0;
                foreach (var r in rooms)
                {
                    Assert.GreaterOrEqual(caps[r.id], r.capMin, $"seed {seed} room {r.id}");
                    Assert.LessOrEqual(caps[r.id], r.capMax, $"seed {seed} room {r.id}");
                    sum += caps[r.id];
                }
                Assert.AreEqual(35, sum, $"seed {seed}");
            }
        }

        [Test]
        public void RollRoomCapacities_SameSeed_GivesSameCapacities()
        {
            // Arrange
            var rooms = Demo().rooms;

            // Act
            var first = HouseRoller.RollRoomCapacities(rooms, 35, new SimRandom(77));
            var second = HouseRoller.RollRoomCapacities(rooms, 35, new SimRandom(77));

            // Assert
            CollectionAssert.AreEquivalent(first, second);
        }

        [Test]
        public void RollRoomCapacities_FixedRoom_KeepsItsCapacity()
        {
            // Arrange
            var rooms = new List<RoomData>
            {
                new RoomData { id = "fixed", capacity = 9 },
                new RoomData { id = "r1", capRandom = true, capMin = 2, capMax = 10 },
                new RoomData { id = "r2", capRandom = true, capMin = 2, capMax = 10 },
            };

            // Act
            var caps = HouseRoller.RollRoomCapacities(rooms, 20, new SimRandom(5));

            // Assert
            Assert.AreEqual(9, caps["fixed"]);
            Assert.AreEqual(11, caps["r1"] + caps["r2"]);
        }

        [Test]
        public void RollRoomCapacities_TotalBelowMinimums_Throws()
        {
            // Arrange
            var rooms = new List<RoomData>
            {
                new RoomData { id = "r1", capRandom = true, capMin = 6 },
                new RoomData { id = "r2", capRandom = true, capMin = 6 },
            };

            // Act / Assert
            Assert.Throws<ArgumentException>(() => HouseRoller.RollRoomCapacities(rooms, 10, new SimRandom(1)));
        }

        [Test]
        public void RollLoad_FixedOrRandom_UsesFixedValueOrStaysInRange()
        {
            // Arrange
            var def = new ApplianceDef { id = "kettle", load = 6 };
            var fixedLoad = new ApplianceData { id = "k", load = 4 };
            var catalogLoad = new ApplianceData { id = "k" };
            var randomLoad = new ApplianceData { id = "k", loadRandom = true, loadMin = 2, loadMax = 5 };
            var rng = new SimRandom(9);

            // Act / Assert
            Assert.AreEqual(4, HouseRoller.RollLoad(fixedLoad, def, rng));
            Assert.AreEqual(6, HouseRoller.RollLoad(catalogLoad, def, rng));
            for (int i = 0; i < 100; i++)
            {
                int load = HouseRoller.RollLoad(randomLoad, def, rng);
                Assert.That(load, Is.InRange(2, 5));
            }
        }

        // ---- requests, carrying, power ------------------------------------------------

        [Test]
        public void ApplianceCarriedToRequestRoom_RunsByItselfAndScores()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }));
            sim.Start();
            Run(sim, 1);
            Assert.AreEqual("kettle", sim.Requests[0].ApplianceId);

            // Act
            Submit(sim, Command.Plug("kettle", "A1"));
            var events = Run(sim, 90);

            // Assert
            Assert.IsNotEmpty(TestData.OfType(events, SimEventType.PickedUp));
            Assert.IsTrue(sim.Requests[0].Done);
            Assert.AreEqual(3, sim.Score);
            Assert.AreEqual("A1", sim.FindAppliance("kettle").SocketId);
            Assert.IsFalse(sim.FindAppliance("kettle").SwitchedOn, "Done appliances switch off.");
        }

        [Test]
        public void Carrying_WithWalkTime_HoldsApplianceUntilItIsPlugged()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }), Rules(instant: false));
            sim.Start();
            Run(sim, 1);
            Submit(sim, Command.Plug("kettle", "A1"));

            // Act: run until the pickup, then look at the hands.
            int guard = 0;
            while (sim.Character.CarryingId.Length == 0 && guard++ < 200) sim.Step();
            string socketWhileCarried = sim.FindAppliance("kettle").SocketId;
            while (sim.FindAppliance("kettle").SocketId.Length == 0 && guard++ < 400) sim.Step();

            // Assert
            Assert.AreEqual("", socketWhileCarried);
            Assert.AreEqual("A1", sim.FindAppliance("kettle").SocketId);
            Assert.AreEqual("", sim.Character.CarryingId);
        }

        [Test]
        public void Unplug_InDayMode_IsRejected()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }));

            // Act
            bool accepted = sim.Submit(Command.Unplug("kettle"), out _);

            // Assert
            Assert.IsFalse(accepted);
            Assert.AreEqual("B1", sim.FindAppliance("kettle").SocketId);
        }

        [Test]
        public void ApplianceInItsRoomWithoutRequest_DrawsNoPower()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }));

            // Act
            sim.Start();
            Run(sim, 5);

            // Assert
            Assert.AreEqual(0, sim.FindCircuit("b").Load);
            Assert.IsFalse(sim.FindAppliance("toaster").Powered);
        }

        // ---- blackout and hearts ------------------------------------------------------

        [Test]
        public void RoomOverCapacityForTripDelay_BlacksOutWholeHouseAndCostsHeart()
        {
            // Arrange: kettle 6 + toaster 3 both wanted in room A, which holds 6.
            var sim = Sim(TwoRooms(6, new[] { "kettle", "toaster" }, urgentOnly: false, bundleChance: 1f));
            sim.Start();
            Run(sim, 1);
            Assert.AreEqual(2, sim.Requests.Count);

            // Act
            Submit(sim, Command.Plug("kettle", "A1", append: true));
            Submit(sim, Command.Plug("toaster", "A2", append: true));
            var events = Run(sim, 29);
            bool darkBeforeDelay = !sim.FindCircuit("a").IsOn;
            events.AddRange(Run(sim, 5));

            // Assert
            Assert.IsFalse(darkBeforeDelay, "No trip before overTripTicks.");
            Assert.AreEqual(1, TestData.OfType(events, SimEventType.CircuitTripped).Count);
            foreach (var c in sim.Circuits) Assert.IsFalse(c.IsOn, $"Circuit {c.Id} should be off.");
            Assert.AreEqual(2, sim.Hearts);
        }

        [Test]
        public void BreakerAfterBlackout_TurnsEveryRoomBackOn()
        {
            // Arrange
            var sim = Sim(TwoRooms(6, new[] { "kettle", "toaster" }, urgentOnly: false, bundleChance: 1f));
            sim.Start();
            Run(sim, 1);
            Submit(sim, Command.Plug("kettle", "A1", append: true));
            Submit(sim, Command.Plug("toaster", "A2", append: true));
            Run(sim, 40);
            Submit(sim, Command.Plug("toaster", "B2", append: true));

            // Act
            Submit(sim, Command.ToggleBreaker("", append: true));
            Run(sim, 5);

            // Assert
            foreach (var c in sim.Circuits) Assert.IsTrue(c.IsOn, $"Circuit {c.Id} should be on.");
        }

        [Test]
        public void UrgentRequestNotStartedInTime_IsMissedAndCostsHeart()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }, dueSec: 2f));
            sim.Start();

            // Act
            var events = Run(sim, 30);

            // Assert
            Assert.IsTrue(sim.Requests[0].Missed);
            Assert.AreEqual(1, TestData.OfType(events, SimEventType.RequestMissed).Count);
            Assert.AreEqual(2, sim.Hearts);
        }

        [Test]
        public void StartedRequest_IsNotMissedAfterItsDueTime()
        {
            // Arrange: due 2 s, takes 8 s to finish.
            var sim = Sim(TwoRooms(10, new[] { "kettle" }, dueSec: 2f));
            sim.Start();
            Run(sim, 1);
            Submit(sim, Command.Plug("kettle", "A1"));

            // Act
            Run(sim, 100);

            // Assert
            Assert.IsTrue(sim.Requests[0].Done);
            Assert.IsFalse(sim.Requests[0].Missed);
            Assert.AreEqual(3, sim.Hearts);
        }

        [Test]
        public void LastHeartLost_LosesWithOutOfHearts()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }, hearts: 1, dueSec: 2f));
            sim.Start();

            // Act
            Run(sim, 30);

            // Assert
            Assert.AreEqual(LevelPhase.Lost, sim.Phase);
            Assert.AreEqual(LoseReason.OutOfHearts, sim.LoseReason);
        }

        [Test]
        public void DayEndsWithHeartsLeft_Wins()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }, daySec: 5f));
            sim.Start();
            Run(sim, 1);
            Submit(sim, Command.Plug("kettle", "A1"));

            // Act
            Run(sim, 100);

            // Assert
            Assert.AreEqual(LevelPhase.Won, sim.Phase);
        }

        [Test]
        public void UniformUseSec_OverridesEveryAppliancesOwnTime()
        {
            // Arrange
            var level = TwoRooms(10, new[] { "kettle" });
            level.day.uniformUseSec = 3f;
            var sim = Sim(level);

            // Act
            sim.Start();
            Run(sim, 1);

            // Assert
            Assert.AreEqual(30, sim.Requests[0].DurationTicks);
        }

        // ---- generator ----------------------------------------------------------------

        [Test]
        public void DemoDay_GeneratedRequests_AreAlwaysDoable()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                // Arrange: nobody acts, and hearts never run out, so the whole day is generated.
                var level = Demo();
                level.day.seed = seed;
                level.day.hearts = 1000;
                var sim = Sim(level, Rules(instant: false), ShippedCatalog());
                sim.Start();

                while (sim.Phase == LevelPhase.Running)
                {
                    // Act
                    var events = new List<SimEvent>(sim.Step());

                    // Assert
                    int active = 0;
                    var wanted = new HashSet<string>();
                    var urgentLoad = new Dictionary<string, int>();
                    foreach (var r in sim.Requests)
                    {
                        if (!r.IsActive) continue;
                        active++;
                        Assert.IsTrue(wanted.Add(r.ApplianceId), $"seed {seed}: {r.ApplianceId} wanted twice");
                        int load = sim.FindAppliance(r.ApplianceId).Load;
                        Assert.LessOrEqual(load, sim.FindCircuit(r.RoomId).Capacity, $"seed {seed}: {r.ApplianceId} too big for {r.RoomId}");
                        if (r.CostsHeart) urgentLoad[r.RoomId] = (urgentLoad.TryGetValue(r.RoomId, out var u) ? u : 0) + load;
                    }
                    Assert.LessOrEqual(active, level.day.maxActive, $"seed {seed}");
                    foreach (var kv in urgentLoad)
                        Assert.LessOrEqual(kv.Value, sim.FindCircuit(kv.Key).Capacity, $"seed {seed}: urgent load in {kv.Key}");
                    foreach (var e in TestData.OfType(events, SimEventType.RequestSpawned))
                    {
                        string at = sim.FindSocket(sim.FindAppliance(e.Appliance).SocketId).RoomId;
                        Assert.AreNotEqual(e.Room, at, $"seed {seed}: {e.Appliance} was already in {e.Room}");
                    }
                }
                Assert.AreEqual(LevelPhase.Won, sim.Phase, $"seed {seed}");
                Assert.Greater(sim.Requests.Count, 10, $"seed {seed}: a 3-minute day should have plenty of requests");
            }
        }

        [Test]
        public void DemoDay_SameSeed_GivesSameRequests()
        {
            // Arrange
            var level = Demo();
            level.day.seed = 4242;
            level.day.hearts = 1000;
            var a = Sim(level, catalog: ShippedCatalog());
            var b = Sim(level, catalog: ShippedCatalog());
            a.Start();
            b.Start();

            // Act
            Run(a, 1800);
            Run(b, 1800);

            // Assert
            Assert.AreEqual(a.Requests.Count, b.Requests.Count);
            for (int i = 0; i < a.Requests.Count; i++)
            {
                Assert.AreEqual(a.Requests[i].ApplianceId, b.Requests[i].ApplianceId);
                Assert.AreEqual(a.Requests[i].RoomId, b.Requests[i].RoomId);
                Assert.AreEqual(a.Requests[i].SpawnTick, b.Requests[i].SpawnTick);
                Assert.AreEqual(a.Requests[i].Urgency, b.Requests[i].Urgency);
            }
            foreach (var c in a.Circuits) Assert.AreEqual(c.Capacity, b.FindCircuit(c.Id).Capacity);
        }
    }
}
