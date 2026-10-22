using NUnit.Framework;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>
    /// GDD v1.0 §9.1, one test per row, on tiny levels built in code; plus the v1.1 counter and fragile rules.
    /// Unless a test says otherwise it runs on the v1.0 rules, which the replay table was verified with.
    /// </summary>
    public class RuleTests
    {
        // A request that keeps the level running for the whole test.
        private const float LongSec = 50f;
        private const float DueSec = 60f;

        private static LevelBuilder OneRoom(int capacityA = 8) =>
            new LevelBuilder().Room("a").Circuit("A", capacityA).Socket("A1", "a", "A").Socket("A2", "a", "A");

        [Test]
        public void LoadBelowCapacity_NeverTrips()
        {
            // Arrange
            var sim = OneRoom(8).Request("Mum", "kettle", "a", LongSec, DueSec).Sim();

            // Act
            var events = TestData.Play(sim, "-1 plug kettle A1; 0 on kettle", 100);

            // Assert
            Assert.IsEmpty(TestData.OfType(events, SimEventType.CircuitTripped));
        }

        [Test]
        public void LoadEqualToCapacity_NeverTrips()
        {
            // Arrange
            var sim = OneRoom(10).Request("Mum", "kettle", "a", LongSec, DueSec).Request("Dad", "toaster", "a", LongSec, DueSec).Sim();

            // Act
            var events = TestData.Play(sim, "-1 plug kettle A1; -1 plug toaster A2; 0 on kettle; 0 on toaster", 100);

            // Assert
            Assert.AreEqual(10, sim.FindCircuit("A").Load);
            Assert.IsEmpty(TestData.OfType(events, SimEventType.CircuitTripped));
        }

        [Test]
        public void OverloadShorterThanTripTime_ClearsWithoutTrip()
        {
            // Arrange: 6 + 4 = 10 > 8 for ticks 0–8, then the toaster goes off.
            var sim = OneRoom(8).Request("Mum", "kettle", "a", LongSec, DueSec).Request("Dad", "toaster", "a", LongSec, DueSec).Sim();

            // Act
            var events = TestData.Play(sim, "-1 plug kettle A1; -1 plug toaster A2; 0 on kettle; 0 on toaster; 9 off toaster", 30);

            // Assert
            Assert.IsEmpty(TestData.OfType(events, SimEventType.CircuitTripped));
            Assert.AreEqual(1, TestData.OfType(events, SimEventType.OverloadStarted).Count);
            Assert.AreEqual(1, TestData.OfType(events, SimEventType.OverloadCleared).Count);
        }

        [Test]
        public void SustainedOverload_TripsOnTenthTick()
        {
            // Arrange
            var sim = OneRoom(8).Request("Mum", "kettle", "a", LongSec, DueSec).Request("Dad", "toaster", "a", LongSec, DueSec).Sim();

            // Act
            var events = TestData.Play(sim, "-1 plug kettle A1; -1 plug toaster A2; 0 on kettle; 0 on toaster", 10);

            // Assert
            var trips = TestData.OfType(events, SimEventType.CircuitTripped);
            Assert.AreEqual(1, trips.Count);
            Assert.AreEqual(9, trips[0].Tick);
            Assert.IsFalse(sim.FindCircuit("A").IsOn);
        }

        [Test]
        public void ResetWhileStillOverloaded_TripsAgainAfterTenTicks()
        {
            // Arrange
            var sim = OneRoom(8).Request("Mum", "kettle", "a", LongSec, DueSec).Request("Dad", "toaster", "a", LongSec, DueSec).Sim();

            // Act: trips at 9, reset at 10, still 10 > 8.
            var events = TestData.Play(sim, "-1 plug kettle A1; -1 plug toaster A2; 0 on kettle; 0 on toaster; 10 breaker A", 25);

            // Assert
            var trips = TestData.OfType(events, SimEventType.CircuitTripped);
            Assert.AreEqual(2, trips.Count);
            Assert.AreEqual(9, trips[0].Tick);
            Assert.AreEqual(19, trips[1].Tick);
        }

        [Test]
        public void MainOverCapacity_TripsEveryCircuit()
        {
            // Arrange: A = 6 + 3 = 9, B = 5 + 4 = 9, both under 10; total 18 > 15.
            var sim = new LevelBuilder().Room("a")
                .Circuit("A", 10).Circuit("B", 10).Main(15)
                .Socket("A1", "a", "A").Socket("A2", "a", "A").Socket("B1", "a", "B").Socket("B2", "a", "B")
                .Request("Mum", "kettle", "a", LongSec, DueSec).Request("Kid", "console", "a", LongSec, DueSec)
                .Request("Dad", "microwave", "a", LongSec, DueSec).Request("Sis", "toaster", "a", LongSec, DueSec)
                .Sim();

            // Act
            var events = TestData.Play(sim,
                "-1 plug kettle A1; -1 plug console A2; -1 plug microwave B1; -1 plug toaster B2; 0 on kettle; 0 on console; 0 on microwave; 0 on toaster", 10);

            // Assert
            var mains = TestData.OfType(events, SimEventType.MainTripped);
            Assert.AreEqual(1, mains.Count);
            Assert.AreEqual(9, mains[0].Tick);
            Assert.AreEqual(18, mains[0].Load);
            Assert.IsFalse(sim.FindCircuit("A").IsOn);
            Assert.IsFalse(sim.FindCircuit("B").IsOn);
            Assert.IsEmpty(TestData.OfType(events, SimEventType.CircuitTripped));
        }

        [Test]
        public void TwoCircuitsOverloadTogether_BothTripInIdOrder()
        {
            // Arrange
            var sim = new LevelBuilder().Room("a")
                .Circuit("B", 8).Circuit("A", 8)
                .Socket("A1", "a", "A").Socket("A2", "a", "A").Socket("B1", "a", "B").Socket("B2", "a", "B")
                .Request("Mum", "kettle", "a", LongSec, DueSec).Request("Dad", "toaster", "a", LongSec, DueSec)
                .Request("Sis", "microwave", "a", LongSec, DueSec).Request("Kid", "dryer", "a", LongSec, DueSec)
                .Sim();

            // Act
            var events = TestData.Play(sim,
                "-1 plug kettle A1; -1 plug toaster A2; -1 plug microwave B1; -1 plug dryer B2; 0 on kettle; 0 on toaster; 0 on microwave; 0 on dryer", 10);

            // Assert
            var trips = TestData.OfType(events, SimEventType.CircuitTripped);
            Assert.AreEqual(2, trips.Count);
            Assert.AreEqual("A", trips[0].Circuit);
            Assert.AreEqual("B", trips[1].Circuit);
            Assert.AreEqual(9, trips[0].Tick);
            Assert.AreEqual(9, trips[1].Tick);
        }

        [Test]
        public void CircuitAndMainOverloadTogether_CircuitTripsFirstAndMainUsesWhatIsLeft()
        {
            // Arrange: A = 10 > 8; B = 6; total 16 > 15 until A trips, then 6.
            var sim = new LevelBuilder().Room("a")
                .Circuit("A", 8).Circuit("B", 10).Main(15)
                .Socket("A1", "a", "A").Socket("A2", "a", "A").Socket("B1", "a", "B").Socket("B2", "a", "B")
                .Request("Mum", "kettle", "a", LongSec, DueSec).Request("Dad", "toaster", "a", LongSec, DueSec)
                .Request("Sis", "microwave", "a", LongSec, DueSec).Request("Kid", "phone", "a", LongSec, DueSec)
                .Sim();

            // Act
            var events = TestData.Play(sim,
                "-1 plug kettle A1; -1 plug toaster A2; -1 plug microwave B1; -1 plug phone B2; 0 on kettle; 0 on toaster; 0 on microwave; 0 on phone", 30);

            // Assert
            var trips = TestData.OfType(events, SimEventType.CircuitTripped);
            Assert.AreEqual(1, trips.Count);
            Assert.AreEqual("A", trips[0].Circuit);
            Assert.AreEqual(9, trips[0].Tick);
            Assert.IsEmpty(TestData.OfType(events, SimEventType.MainTripped));
            Assert.IsTrue(sim.FindCircuit("B").IsOn);
        }

        [Test]
        public void FragileLosesPower_ProgressResetsToZero()
        {
            // Arrange
            var sim = OneRoom().Request("Kid", "console", "a", LongSec, DueSec).Sim();

            // Act
            var events = TestData.Play(sim, "-1 plug console A1; 0 on console; 50 off console", 51);

            // Assert
            Assert.AreEqual(0, sim.FindRequest("console").ProgressTicks);
            var lost = TestData.OfType(events, SimEventType.ApplianceInterrupted);
            Assert.AreEqual(1, lost.Count);
            Assert.IsTrue(lost[0].ProgressLost);
        }

        [Test]
        public void NormalApplianceLosesPower_KeepsProgress()
        {
            // Arrange
            var sim = OneRoom().Request("Mum", "kettle", "a", LongSec, DueSec).Sim();

            // Act: powered ticks 0–39, off 40–49, powered 50–59.
            TestData.Play(sim, "-1 plug kettle A1; 0 on kettle; 40 breaker A; 50 breaker A", 60);

            // Assert
            Assert.AreEqual(50, sim.FindRequest("kettle").ProgressTicks);
        }

        [Test]
        public void FinishingExactlyOnDeadline_Wins()
        {
            // Arrange
            var sim = OneRoom().Request("Mum", "kettle", "a", 8f, 8f).Day(10f).Sim();

            // Act
            var events = TestData.Play(sim, "-1 plug kettle A1; 0 on kettle", 200);

            // Assert
            Assert.AreEqual(LevelPhase.Won, sim.Phase);
            Assert.AreEqual(80, sim.Now);
            Assert.AreEqual(1, TestData.OfType(events, SimEventType.LevelWon).Count);
        }

        [Test]
        public void WinAndLoseInSameTick_Loses()
        {
            // Arrange: the kettle finishes at tick 19; the fridge (off from tick 9) passes 10 off-ticks on the same tick.
            var sim = new LevelBuilder().Room("a")
                .Circuit("A", 10).Circuit("B", 10)
                .Socket("A1", "a", "A").Socket("B1", "a", "B")
                .Fridge("B1", 1f)
                .Request("Mum", "kettle", "a", 2f, 10f).Day(10f)
                .Sim();

            // Act
            var events = TestData.Play(sim, "-1 plug kettle A1; 0 on kettle; 9 breaker B", 100);

            // Assert
            Assert.AreEqual(LevelPhase.Lost, sim.Phase);
            Assert.AreEqual(LoseReason.FridgeSpoiled, sim.LoseReason);
            Assert.AreEqual(20, sim.Now);
            Assert.IsEmpty(TestData.OfType(events, SimEventType.LevelWon));
        }

        [Test]
        public void FridgeOffTime_AddsUpAcrossOutages()
        {
            // Arrange: limit 10 s; off 6 s, on 2 s, off again.
            var sim = new LevelBuilder().Room("a")
                .Circuit("A", 10).Circuit("B", 10)
                .Socket("A1", "a", "A").Socket("B1", "a", "B")
                .Fridge("B1", 10f)
                .Request("Mum", "kettle", "a", 30f, 30f).Day(30f)
                .Sim();

            // Act
            TestData.Play(sim, "0 breaker B; 60 breaker B; 80 breaker B", 300);

            // Assert
            Assert.AreEqual(LoseReason.FridgeSpoiled, sim.LoseReason);
            Assert.AreEqual(101, sim.SpoilTicks);
            Assert.AreEqual(121, sim.Now);
        }

        [Test]
        public void BreakerFlippedOff_RevealsOnlySocketsWithSwitchedOnAppliances()
        {
            // Arrange: A1 empty, A2 has a lamp that is on.
            var sim = new LevelBuilder().Room("a").Circuit("A", 10)
                .Socket("A1", "a", "A", revealed: false).Socket("A2", "a", "A", revealed: false)
                .Lamps(1).Request("Mum", "kettle", "a", LongSec, DueSec)
                .Sim();

            // Act
            var events = TestData.Play(sim, "-1 plug lamp1 A2; 0 on lamp1; 1 breaker A", 3);

            // Assert
            var reveals = TestData.OfType(events, SimEventType.SocketRevealed);
            Assert.AreEqual(1, reveals.Count);
            Assert.AreEqual("A2", reveals[0].Socket);
            Assert.IsFalse(sim.FindSocket("A1").Revealed);
        }

        [Test]
        public void BreakerFlippedOffWithSightRule_RevealsTheSeenDeadSocket()
        {
            // Arrange: same as above with the v1.1 reveal rule; the lamp is in the character's room, so it is seen.
            var sim = new LevelBuilder().Room("a").Circuit("A", 10)
                .Socket("A1", "a", "A", revealed: false).Socket("A2", "a", "A", revealed: false)
                .Lamps(1).Request("Mum", "kettle", "a", LongSec, DueSec)
                .Sim(TestData.Instant(TestData.V11Rules()));

            // Act
            var events = TestData.Play(sim, "-1 plug lamp1 A2; 0 on lamp1; 1 breaker A", 3);

            // Assert
            var reveals = TestData.OfType(events, SimEventType.SocketRevealed);
            Assert.AreEqual(1, reveals.Count);
            Assert.AreEqual("A2", reveals[0].Socket);
            Assert.AreEqual(1, reveals[0].Tick);
        }

        [Test]
        public void RevealingMislabelledSocket_FlagsWrongAssumption()
        {
            // Arrange: K1 looks like it is on A but is really on B.
            var sim = new LevelBuilder().Room("kitchen").Circuit("A", 12).Circuit("B", 8)
                .Socket("K1", "kitchen", "B", assumed: "A", revealed: false)
                .Request("Mum", "kettle", "kitchen", LongSec, DueSec)
                .Sim();

            // Act
            var events = TestData.Play(sim, "-1 plug kettle K1; 0 on kettle; 1 breaker B", 3);

            // Assert
            var reveals = TestData.OfType(events, SimEventType.SocketRevealed);
            Assert.AreEqual(1, reveals.Count);
            Assert.IsTrue(reveals[0].AssumptionWasWrong);
        }

        [Test]
        public void ApplianceOnCord_LoadsTheSourceCircuit()
        {
            // Arrange
            var sim = new LevelBuilder().Room("a").Room("b").Circuit("A", 10).Circuit("B", 10)
                .Socket("B1", "a", "B").Socket("A1", "b", "A")
                .Cords(1).Request("Mum", "kettle", "b", LongSec, DueSec)
                .Sim();

            // Act
            TestData.Play(sim, "-1 cord B1 b; -1 plug kettle CORD-B1; 0 on kettle", 1);

            // Assert
            Assert.AreEqual(6, sim.FindCircuit("B").Load);
            Assert.AreEqual(0, sim.FindCircuit("A").Load);
        }

        [Test]
        public void RemovingCord_SendsItsApplianceBackToTheTray()
        {
            // Arrange
            var sim = new LevelBuilder().Room("a").Room("b").Circuit("A", 10).Circuit("B", 10)
                .Socket("B1", "a", "B").Socket("A1", "b", "A")
                .Cords(1).Request("Mum", "kettle", "b", LongSec, DueSec)
                .Sim();

            // Act
            TestData.Play(sim, "-1 cord B1 b; -1 plug kettle CORD-B1; 0 on kettle; 5 uncord CORD-B1", 6);

            // Assert
            var kettle = sim.FindAppliance("kettle");
            Assert.AreEqual("", kettle.SocketId);
            Assert.IsFalse(kettle.Powered);
            Assert.IsNull(sim.FindSocket("CORD-B1"));
            Assert.AreEqual(1, sim.CordsLeft);
        }

        [Test]
        public void CordToRoomThatIsNotNextDoor_IsRejected()
        {
            // Arrange
            var sim = new LevelBuilder().Room("a").Room("b").Room("c").Circuit("A", 10)
                .Socket("A1", "a", "A").Cords(1).Request("Mum", "kettle", "c", LongSec, DueSec)
                .Sim();

            // Act
            bool ok = sim.Submit(Command.PlaceCord("A1", "c"), out var reason);

            // Assert
            Assert.IsFalse(ok);
            Assert.AreEqual("Too far. Cords only reach the next room.", reason);
            Assert.AreEqual(1, sim.CordsLeft);
        }

        [Test]
        public void RoutineStartingWhileBreakerOff_DrawsPowerOnlyAfterReset()
        {
            // Arrange: heater runs 2.0–5.0 s on B; B is off from 0 until tick 30.
            var sim = new LevelBuilder().Room("a").Circuit("A", 10).Circuit("B", 10)
                .Socket("A1", "a", "A").Socket("B1", "a", "B")
                .Routine("Grandpa", "heater", "B1", 2f, 3f)
                .Request("Mum", "kettle", "a", LongSec, DueSec)
                .Sim();
            var loadB = new int[60];

            // Act
            sim.Start();
            for (int t = 0; t < 60; t++)
            {
                if (t == 0 || t == 30) sim.Submit(Command.ToggleBreaker("B"), out _);
                sim.Step();
                loadB[t] = sim.FindCircuit("B").Load;
            }

            // Assert
            Assert.AreEqual(0, loadB[25], "routine is on but its breaker is off");
            Assert.AreEqual(6, loadB[30], "draws power as soon as the breaker is reset");
            Assert.AreEqual(6, loadB[49]);
            Assert.AreEqual(0, loadB[50], "switches itself off at the end of its time");
        }

        [Test]
        public void MovingSwitchedOnAppliance_LeavesItSwitchedOff()
        {
            // Arrange
            var sim = OneRoom().Request("Mum", "kettle", "a", LongSec, DueSec).Sim();
            sim.Submit(Command.Plug("kettle", "A1"), out _);
            sim.Submit(Command.SetSwitch("kettle", true), out _);

            // Act
            bool ok = sim.Submit(Command.Plug("kettle", "A2"), out _);

            // Assert
            Assert.IsTrue(ok);
            Assert.AreEqual("A2", sim.FindAppliance("kettle").SocketId);
            Assert.IsFalse(sim.FindAppliance("kettle").SwitchedOn);
            Assert.AreEqual("", sim.FindSocket("A1").OccupantId);
        }

        [Test]
        public void IllegalCommands_AreRejectedWithTheirMessageAndChangeNothing()
        {
            // Arrange: free planning, where commands are checked at once.
            // (v1.0 also refused switching while paused; v1.1 queues it instead — see CharacterTests.)
            var sim = new LevelBuilder().Room("kitchen").Room("living").Circuit("A", 10).Circuit("B", 10)
                .Socket("K1", "kitchen", "A").Socket("L1", "living", "B").Socket("L2", "living", "B")
                .Routine("Grandpa", "heater", "L1", 20f, 20f)
                .Lamps(1).Request("Mum", "kettle", "kitchen", LongSec, DueSec)
                .Sim();
            sim.Submit(Command.Plug("lamp1", "K1"), out _);

            // Act + Assert
            AssertRejected(sim, Command.Plug("kettle", "L1"), "Reserved for Grandpa's Space heater.");
            AssertRejected(sim, Command.Plug("kettle", "L2"), "Mum needs it in the kitchen.");
            AssertRejected(sim, Command.Plug("kettle", "K1"), "Socket in use.");
            AssertRejected(sim, Command.SetSwitch("kettle", true), "Plug it in first.");
            AssertRejected(sim, Command.ToggleBreaker("A"), "Unpause to flip breakers.");
            AssertRejected(sim, Command.Unplug("heater"), "Can't unplug that.");
            Assert.AreEqual("", sim.FindAppliance("kettle").SocketId);
            Assert.AreEqual("lamp1", sim.FindSocket("K1").OccupantId);
            Assert.IsFalse(sim.FindAppliance("lamp1").SwitchedOn);
            Assert.IsTrue(sim.FindCircuit("A").IsOn);
        }

        [Test]
        public void SameLevelAndCommands_GiveIdenticalEventLogs()
        {
            // Arrange
            var level = TestData.Level(5);

            // Act
            var first = Replay.Run(new HouseSim(level, TestData.V10Rules(), TestData.Catalog()), level.reference);
            var second = Replay.Run(new HouseSim(level, TestData.V10Rules(), TestData.Catalog()), level.reference);

            // Assert
            Assert.AreEqual(first.Events.Count, second.Events.Count);
            for (int i = 0; i < first.Events.Count; i++)
            {
                var a = first.Events[i];
                var b = second.Events[i];
                Assert.AreEqual((a.Type, a.Tick, a.Circuit, a.Socket, a.Appliance, a.Load), (b.Type, b.Tick, b.Circuit, b.Socket, b.Appliance, b.Load), $"event {i}");
            }
        }

        // ---- v1.1 rules ----------------------------------------------------------------

        [Test]
        public void FragileSwitchedOffAndOnInOneTick_StillLosesProgress()
        {
            // Arrange
            var sim = OneRoom().Request("Kid", "console", "a", LongSec, DueSec).Sim(TestData.Instant(TestData.V11Rules()));

            // Act: off and back on within tick 20.
            var events = TestData.Play(sim, "-1 plug console A1; 0 on console; 20 off console; 20 on console", 21);

            // Assert
            Assert.AreEqual(1, sim.FindRequest("console").ProgressTicks, "reset to 0 at tick 20, then powered for tick 20");
            Assert.IsTrue(TestData.OfType(events, SimEventType.ApplianceInterrupted)[0].ProgressLost);
        }

        [Test]
        public void PumpingTheBreaker_StillTripsUnderV11Counter()
        {
            // Arrange: 10 > 8. The player flips A off on the 10th overloaded tick and straight back on.
            var sim = OneRoom(8).Request("Mum", "kettle", "a", LongSec, DueSec).Request("Dad", "toaster", "a", LongSec, DueSec)
                .Sim(TestData.Instant(TestData.V11Rules()));

            // Act
            var events = TestData.Play(sim, "-1 plug kettle A1; -1 plug toaster A2; 0 on kettle; 0 on toaster; 9 breaker A; 10 breaker A", 11);

            // Assert
            var trips = TestData.OfType(events, SimEventType.CircuitTripped);
            Assert.AreEqual(1, trips.Count);
            Assert.AreEqual(10, trips[0].Tick);
        }

        [Test]
        public void PumpingTheBreaker_AvoidsTripUnderV10Counter()
        {
            // Arrange: the exploit the v1.1 counter closes (GDD v1.1 §2.8).
            var sim = OneRoom(8).Request("Mum", "kettle", "a", LongSec, DueSec).Request("Dad", "toaster", "a", LongSec, DueSec).Sim();

            // Act
            var events = TestData.Play(sim, "-1 plug kettle A1; -1 plug toaster A2; 0 on kettle; 0 on toaster; 9 breaker A; 10 breaker A", 11);

            // Assert
            Assert.IsEmpty(TestData.OfType(events, SimEventType.CircuitTripped));
        }

        [Test]
        public void CalmTicks_CoolTheCounterOneStepPerThree()
        {
            // Arrange: overloaded for ticks 0–4 (counter 5), calm from tick 5.
            var sim = OneRoom(8).Request("Mum", "kettle", "a", LongSec, DueSec).Request("Dad", "toaster", "a", LongSec, DueSec)
                .Sim(TestData.Instant(TestData.V11Rules()));

            // Act: six calm ticks (5–10).
            TestData.Play(sim, "-1 plug kettle A1; -1 plug toaster A2; 0 on kettle; 0 on toaster; 5 off toaster", 11);

            // Assert
            Assert.AreEqual(3, sim.FindCircuit("A").OverTicks);
        }

        [Test]
        public void HotBreakerResetWhileStillOverloaded_TripsAgainNextTick()
        {
            // Arrange: v1.1 counter, counterAfterTrip = keep. Trips at tick 9.
            var sim = OneRoom(8).Request("Mum", "kettle", "a", LongSec, DueSec).Request("Dad", "toaster", "a", LongSec, DueSec)
                .Sim(TestData.Instant(TestData.V11Rules()));

            // Act: reset at tick 10 with 10 > 8 still plugged in.
            var events = TestData.Play(sim, "-1 plug kettle A1; -1 plug toaster A2; -1 on kettle; -1 on toaster; 10 breaker A", 12);

            // Assert
            var trips = TestData.OfType(events, SimEventType.CircuitTripped);
            Assert.AreEqual(2, trips.Count);
            Assert.AreEqual(9, trips[0].Tick);
            Assert.AreEqual(10, trips[1].Tick);
        }

        [Test]
        public void HotBreakerLeftOff_CoolsWithoutTrippingAgain()
        {
            // Arrange
            var sim = OneRoom(8).Request("Mum", "kettle", "a", LongSec, DueSec).Request("Dad", "toaster", "a", LongSec, DueSec)
                .Sim(TestData.Instant(TestData.V11Rules()));

            // Act: trips at tick 9, then left off for 30 ticks (cools one step per 3 ticks).
            var events = TestData.Play(sim, "-1 plug kettle A1; -1 plug toaster A2; -1 on kettle; -1 on toaster", 40);

            // Assert
            Assert.AreEqual(1, TestData.OfType(events, SimEventType.CircuitTripped).Count, "an OFF breaker never trips");
            Assert.AreEqual(0, sim.FindCircuit("A").OverTicks);
        }

        private static void AssertRejected(HouseSim sim, Command command, string message)
        {
            bool ok = sim.Submit(command, out var reason);
            Assert.IsFalse(ok, command.ToString());
            Assert.AreEqual(message, reason, command.ToString());
        }
    }
}
