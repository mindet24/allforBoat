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
            var demo = Demo();
            var rooms = demo.rooms;
            int house = demo.day.houseCapacity;

            for (int seed = 1; seed <= 200; seed++)
            {
                // Act
                var caps = HouseRoller.RollRoomCapacities(rooms, house, new SimRandom(seed));

                // Assert
                int sum = 0;
                foreach (var r in rooms)
                {
                    Assert.GreaterOrEqual(caps[r.id], r.capMin, $"seed {seed} room {r.id}");
                    Assert.LessOrEqual(caps[r.id], r.capMax, $"seed {seed} room {r.id}");
                    sum += caps[r.id];
                }
                Assert.AreEqual(house, sum, $"seed {seed}");
            }
        }

        [Test]
        public void RollRoomCapacities_SameSeed_GivesSameCapacities()
        {
            // Arrange
            var demo = Demo();

            // Act
            var first = HouseRoller.RollRoomCapacities(demo.rooms, demo.day.houseCapacity, new SimRandom(77));
            var second = HouseRoller.RollRoomCapacities(demo.rooms, demo.day.houseCapacity, new SimRandom(77));

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

        // ---- keyboard helpers -----------------------------------------------------------
        // TwoRooms layout (auto): room a 0–6 m, room b 6–12 m. Sockets A1 2 m, A2 4 m, B1 7.5 m (kettle),
        // B2 9 m (toaster), B3 10.5 m. The character starts at 9 m in b; the panel hangs at 6.6 m in b.

        private const float A1X = 2f, A2X = 4f, KettleX = 7.5f, ToasterX = 9f, PanelX = 6.6f;

        /// <summary>Holds a direction until the character is within 15 cm of <paramref name="metres"/>, then stops.</summary>
        private static void WalkTo(HouseSim sim, float metres)
        {
            int dir = Math.Sign(metres - sim.Character.X);
            if (dir == 0) return;
            Submit(sim, Command.Walk(dir));
            for (int guard = 0; guard < 1000 && Math.Abs(sim.Character.X - metres) > 0.15f; guard++)
            {
                if (Math.Sign(metres - sim.Character.X) != dir) break;
                sim.Step();
            }
            Submit(sim, Command.Walk(0));
        }

        private static void Carry(HouseSim sim, float fromX, float toX)
        {
            WalkTo(sim, fromX);
            Submit(sim, Command.Interact());
            WalkTo(sim, toX);
            Submit(sim, Command.Interact());
        }

        /// <summary>Kettle 6 and toaster 3 both wanted in room a, which holds 6; both carried there.</summary>
        private static HouseSim Overloaded(out List<SimEvent> events)
        {
            var sim = Sim(TwoRooms(6, new[] { "kettle", "toaster" }, urgentOnly: false, bundleChance: 1f), Rules(instant: false));
            sim.Start();
            Run(sim, 1);
            Assert.AreEqual(2, sim.Requests.Count);
            Carry(sim, KettleX, A1X);
            Carry(sim, ToasterX, A2X);
            events = new List<SimEvent>();
            return sim;
        }

        // ---- requests, carrying, power ------------------------------------------------

        [Test]
        public void ApplianceCarriedToRequestRoom_RunsByItselfAndScores()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }), Rules(instant: false));
            sim.Start();
            Run(sim, 1);
            Assert.AreEqual("kettle", sim.Requests[0].ApplianceId);

            // Act
            Carry(sim, KettleX, A1X);
            Run(sim, 90);

            // Assert
            Assert.IsTrue(sim.Requests[0].Done);
            Assert.AreEqual(3, sim.Score);
            Assert.AreEqual("A1", sim.FindAppliance("kettle").SocketId);
            Assert.AreEqual(6, sim.FindCircuit("a").Load, "A plugged appliance keeps drawing after its request is done.");
        }

        [Test]
        public void WantedApplianceInAnotherRoom_DoesNotProgressTheRequest()
        {
            // Arrange: Mum wants the kettle in room a; it sits powered in room b.
            var sim = Sim(TwoRooms(10, new[] { "kettle" }));
            sim.Start();

            // Act
            Run(sim, 50);

            // Assert
            Assert.AreEqual(0, sim.Requests[0].ProgressTicks);
            Assert.IsTrue(sim.FindAppliance("kettle").Powered);
        }

        [Test]
        public void PickingUpAnAppliance_RemovesItsLoadFromTheRoom()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }), Rules(instant: false));
            sim.Start();
            WalkTo(sim, KettleX);
            int before = sim.FindCircuit("b").Load;

            // Act
            Submit(sim, Command.Interact());

            // Assert
            Assert.AreEqual(9, before);
            Assert.AreEqual(3, sim.FindCircuit("b").Load);
        }

        [Test]
        public void InteractNearAppliance_PicksItUp_ThenInteractAtFreeSocket_PlugsIt()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }), Rules(instant: false));
            sim.Start();
            WalkTo(sim, KettleX);

            // Act
            Submit(sim, Command.Interact());
            string carried = sim.Character.CarryingId;
            string socketWhileCarried = sim.FindAppliance("kettle").SocketId;
            WalkTo(sim, A1X);
            Submit(sim, Command.Interact());

            // Assert
            Assert.AreEqual("kettle", carried);
            Assert.AreEqual("", socketWhileCarried);
            Assert.AreEqual("A1", sim.FindAppliance("kettle").SocketId);
            Assert.AreEqual("", sim.Character.CarryingId);
        }

        [Test]
        public void InteractCarryingWithNoFreeSocketInReach_IsRejected()
        {
            // Arrange: carry the kettle into room a at 5.6 m, more than 1.5 m from A2 (4 m), the nearest socket there.
            var sim = Sim(TwoRooms(10, new[] { "kettle" }), Rules(instant: false));
            sim.Start();
            WalkTo(sim, KettleX);
            Submit(sim, Command.Interact());
            WalkTo(sim, 5.6f);

            // Act
            bool accepted = sim.Submit(Command.Interact(), out var reason);

            // Assert
            Assert.IsFalse(accepted);
            Assert.AreEqual("kettle", sim.Character.CarryingId);
        }

        [Test]
        public void ClickCommands_InDayMode_AreRejected()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }));
            sim.Start();

            // Act
            bool unplug = sim.Submit(Command.Unplug("kettle"), out _);
            bool plug = sim.Submit(Command.Plug("kettle", "A1"), out _);

            // Assert
            Assert.IsFalse(unplug);
            Assert.IsFalse(plug);
            Assert.AreEqual("B1", sim.FindAppliance("kettle").SocketId);
        }

        [Test]
        public void PluggedApplianceWithoutRequest_DrawsItsLoad()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }));

            // Act
            sim.Start();
            Run(sim, 5);

            // Assert: kettle 6 + toaster 3, wanted or not.
            Assert.AreEqual(9, sim.FindCircuit("b").Load);
            Assert.IsTrue(sim.FindAppliance("toaster").Powered);
        }

        [Test]
        public void DayRoomStartingAboveItsLowestCapacity_IsReported()
        {
            // Arrange: room a (capacity 5) starts with the kettle (6).
            var level = TwoRooms(5, new[] { "toaster" });
            level.appliances[0].socket = "A1";

            // Act
            var errors = LevelValidator.Validate(level, TestData.Catalog(), Rules());

            // Assert
            Assert.That(errors, Has.Some.Contains("Room a starts with load 6"));
        }

        // ---- walking and stairs -------------------------------------------------------

        [Test]
        public void WalkThroughOpenDoor_EntersNextRoom()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }), Rules(instant: false));
            sim.Start();

            // Act
            WalkTo(sim, 3f);

            // Assert
            Assert.AreEqual("a", sim.Character.RoomId);
        }

        [Test]
        public void WalkIntoOuterWall_StopsInsideTheRoom()
        {
            // Arrange
            var sim = Sim(TwoRooms(10, new[] { "kettle" }), Rules(instant: false));
            sim.Start();
            Submit(sim, Command.Walk(1));

            // Act
            Run(sim, 100);

            // Assert
            Assert.AreEqual("b", sim.Character.RoomId);
            Assert.LessOrEqual(sim.Character.X, 12f);
            Assert.Greater(sim.Character.X, 11f);
        }

        [Test]
        public void ClimbAtStairs_ReachesTheFloorAboveAfterStairsTicks()
        {
            // Arrange: the demo starts in the living room, by the stairs up to the bedroom.
            var level = Demo();
            level.day.seed = 3;
            var rules = Rules(instant: false);
            var sim = Sim(level, rules, ShippedCatalog());
            sim.Start();

            // Act
            Submit(sim, Command.Climb(1));
            Run(sim, rules.stairsTicks - 1);
            string midway = sim.Character.RoomId;
            Run(sim, 1);

            // Assert
            Assert.AreEqual("living", midway);
            Assert.AreEqual("bedroom", sim.Character.RoomId);
        }

        [Test]
        public void ClimbAwayFromStairs_IsRejected()
        {
            // Arrange
            var level = Demo();
            level.day.seed = 3;
            var sim = Sim(level, Rules(instant: false), ShippedCatalog());
            sim.Start();
            WalkTo(sim, 8.5f);

            // Act
            bool accepted = sim.Submit(Command.Climb(1), out _);

            // Assert
            Assert.IsFalse(accepted);
            Assert.AreEqual("living", sim.Character.RoomId);
        }

        // ---- blackout and hearts ------------------------------------------------------

        [Test]
        public void RoomOverCapacityForTripDelay_BlacksOutWholeHouseAndCostsHeart()
        {
            // Arrange
            var sim = Overloaded(out var events);

            // Act
            events.AddRange(Run(sim, 29));
            bool darkBeforeDelay = !sim.FindCircuit("a").IsOn;
            events.AddRange(Run(sim, 5));

            // Assert
            Assert.IsFalse(darkBeforeDelay, "No trip before overTripTicks.");
            Assert.AreEqual(1, TestData.OfType(events, SimEventType.CircuitTripped).Count);
            foreach (var c in sim.Circuits) Assert.IsFalse(c.IsOn, $"Circuit {c.Id} should be off.");
            Assert.AreEqual(2, sim.Hearts);
        }

        [Test]
        public void InteractAtPanelDuringBlackout_TurnsEveryRoomBackOn()
        {
            // Arrange: blackout, then take the toaster away so the reset holds.
            var sim = Overloaded(out _);
            Run(sim, 40);
            WalkTo(sim, A2X);
            Submit(sim, Command.Interact());
            WalkTo(sim, PanelX);

            // Act
            Submit(sim, Command.Interact());
            Run(sim, 5);

            // Assert
            foreach (var c in sim.Circuits) Assert.IsTrue(c.IsOn, $"Circuit {c.Id} should be on.");
            Assert.AreEqual("toaster", sim.Character.CarryingId, "Resetting the panel does not drop what is carried.");
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
            // Arrange: due 5 s, takes 8 s to finish.
            var sim = Sim(TwoRooms(10, new[] { "kettle" }, dueSec: 5f), Rules(instant: false));
            sim.Start();
            Run(sim, 1);
            Carry(sim, KettleX, A1X);

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

        // ---- validation ---------------------------------------------------------------

        [Test]
        public void DemoLevel_PassesValidation()
        {
            // Arrange
            var level = Demo();

            // Act
            var errors = LevelValidator.Validate(level, ShippedCatalog(), Rules());

            // Assert
            CollectionAssert.IsEmpty(errors);
        }

        [Test]
        public void DayRoomWithEverySocketTaken_IsReported()
        {
            // Arrange: room A's two sockets both hold an appliance at the start.
            var level = TwoRooms(10, new[] { "kettle" });
            level.appliances[0].socket = "A1";
            level.appliances[1].socket = "A2";

            // Act
            var errors = LevelValidator.Validate(level, TestData.Catalog(), Rules());

            // Assert
            Assert.That(errors, Has.Some.Contains("Room a has no free socket"));
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
