using NUnit.Framework;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>
    /// The player character, its action queue, and Sight (GDD v1.1 §2.2–2.4), on the v1.1 default rules.
    /// Rooms are laid out automatically 6 m wide, so neighbouring centres are 6 m apart = 20 ticks at 3 m/s.
    /// </summary>
    public class CharacterTests
    {
        private const float LongSec = 50f;
        private const float DueSec = 60f;
        private const int OneRoomWalk = 20;

        private static void Steps(HouseSim sim, int count)
        {
            for (int i = 0; i < count; i++) sim.Step();
        }

        private static LevelBuilder ThreeRooms(bool doorBcOpen = true) =>
            new LevelBuilder().Room("a").Room("b").Room("c")
                .Door("D1", "a", "b").Door("D2", "b", "c", open: doorBcOpen)
                .Circuit("A", 10).Socket("A1", "a", "A")
                .Request("Mum", "kettle", "a", LongSec, DueSec);

        [Test]
        public void MoveToNextRoom_CrossesAtHalfwayAndArrivesAfterDistanceOverSpeed()
        {
            // Arrange
            var sim = ThreeRooms().Sim(TestData.V11Rules());
            sim.Start();
            sim.Submit(Command.MoveTo("b"), out _);

            // Act + Assert
            Steps(sim, OneRoomWalk / 2 - 1);
            Assert.AreEqual("a", sim.Character.RoomId);
            Steps(sim, 1);
            Assert.AreEqual("b", sim.Character.RoomId, "counts as in the next room from the middle of the door");
            Assert.IsTrue(sim.Character.IsWalking);
            Steps(sim, OneRoomWalk / 2);
            Assert.IsFalse(sim.Character.IsWalking);
        }

        [Test]
        public void SwitchInAnotherRoom_HappensAfterTheWalkAndThreeInteractTicks()
        {
            // Arrange: the kettle is in room b; the character starts in a.
            var sim = new LevelBuilder().Room("a").Room("b").Circuit("A", 10).Socket("B1", "b", "A")
                .Request("Mum", "kettle", "b", LongSec, DueSec).Sim(TestData.V11Rules());
            sim.Submit(Command.Plug("kettle", "B1"), out _);
            sim.Start();

            // Act
            sim.Submit(Command.SetSwitch("kettle", true), out _);
            Steps(sim, OneRoomWalk + 2);

            // Assert: walk ticks 0–19, interact 20–22, switched on during tick 22.
            Assert.IsFalse(sim.FindAppliance("kettle").SwitchedOn);
            Steps(sim, 1);
            Assert.IsTrue(sim.FindAppliance("kettle").SwitchedOn);
            Assert.AreEqual(1, sim.FindRequest("kettle").ProgressTicks, "powered from the tick it was switched on");
        }

        [Test]
        public void WalkingThroughClosedDoor_CostsOpeningTimeAndLeavesItOpen()
        {
            // Arrange
            var sim = new LevelBuilder().Room("a").Room("b").Door("D1", "a", "b", open: false)
                .Circuit("A", 10).Socket("A1", "a", "A").Request("Mum", "kettle", "a", LongSec, DueSec)
                .Sim(TestData.V11Rules());
            sim.Start();

            // Act
            sim.Submit(Command.MoveTo("b"), out _);
            Steps(sim, 1);

            // Assert
            Assert.AreEqual(OneRoomWalk + sim.Rules.doorOpenTicks, sim.Character.SegmentTicks);
            Assert.IsTrue(sim.FindDoor("D1").IsOpen);
        }

        [Test]
        public void Sight_IsCharacterRoomPlusRoomsThroughOpenDoors()
        {
            // Arrange: a–b open, b–c closed; character in a.
            var sim = ThreeRooms(doorBcOpen: false).Sim(TestData.V11Rules());

            // Act
            sim.Start();

            // Assert
            Assert.IsTrue(sim.IsRoomVisible("a"));
            Assert.IsTrue(sim.IsRoomVisible("b"));
            Assert.IsFalse(sim.IsRoomVisible("c"));
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        public void SightDepth_LimitsHowManyOpenDoorsAwayYouSee(int depth, bool seesC)
        {
            // Arrange: every door open; c is two doors from a.
            var rules = TestData.V11Rules();
            rules.sightDepth = depth;
            var sim = ThreeRooms().Sim(rules);

            // Act
            sim.Start();

            // Assert
            Assert.AreEqual(seesC, sim.IsRoomVisible("c"));
        }

        [Test]
        public void RoutineInUnseenRoom_KeepsItsLastSeenStateOff()
        {
            // Arrange: heater in c (behind a closed door) switches on at 1 s.
            var sim = ThreeRooms(doorBcOpen: false).Circuit("B", 10).Socket("C1", "c", "B")
                .Routine("Grandpa", "heater", "C1", 1f, 5f).Sim(TestData.V11Rules());
            sim.Start();

            // Act
            Steps(sim, 15);

            // Assert
            var heater = sim.FindAppliance("heater");
            Assert.IsTrue(heater.SwitchedOn);
            Assert.IsFalse(heater.SeenSwitchedOn);
            Assert.AreEqual(0, heater.SeenTick, "last seen during planning");
        }

        [Test]
        public void BreakerOff_RevealsSeenSocketNowAndUnseenSocketOnlyWhenTheCharacterLooks()
        {
            // Arrange: lamps on circuit L in b (visible) and c (behind a closed door). Panel in a.
            var sim = ThreeRooms(doorBcOpen: false).Circuit("L", 10)
                .Socket("B1", "b", "L", revealed: false).Socket("C1", "c", "L", revealed: false)
                .Lamps(2).Sim(TestData.V11Rules());
            sim.Submit(Command.Plug("lamp1", "B1"), out _);
            sim.Submit(Command.Plug("lamp2", "C1"), out _);
            sim.Submit(Command.SetSwitch("lamp1", true), out _);
            sim.Submit(Command.SetSwitch("lamp2", true), out _);
            sim.Start();

            // Act: flip L off, then walk to c.
            sim.Submit(Command.ToggleBreaker("L"), out _);
            Steps(sim, 10);
            bool seenSocketRevealed = sim.FindSocket("B1").Revealed;
            bool hiddenSocketRevealedEarly = sim.FindSocket("C1").Revealed;
            sim.Submit(Command.MoveTo("c"), out _);
            Steps(sim, 2 * OneRoomWalk + sim.Rules.doorOpenTicks);

            // Assert
            Assert.IsTrue(seenSocketRevealed);
            Assert.IsFalse(hiddenSocketRevealedEarly);
            Assert.IsTrue(sim.FindSocket("C1").Revealed);
        }

        [Test]
        public void BreakerAtFarPanel_FlipsOnlyAfterTheWalk()
        {
            // Arrange: panel in b; character in a.
            var sim = ThreeRooms().Panel("b").Sim(TestData.V11Rules());
            sim.Start();

            // Act
            sim.Submit(Command.ToggleBreaker("A"), out _);
            Steps(sim, OneRoomWalk + 2);
            bool stillOn = sim.FindCircuit("A").IsOn;
            Steps(sim, 1);

            // Assert
            Assert.IsTrue(stillOn);
            Assert.IsFalse(sim.FindCircuit("A").IsOn);
        }

        [Test]
        public void NewOrder_ReplacesQueueUnlessAppendedAndQueueHasALimit()
        {
            // Arrange
            var sim = ThreeRooms().Sim(TestData.V11Rules());
            sim.Start();

            // Act + Assert
            Assert.IsTrue(sim.Submit(Command.MoveTo("b"), out _));
            Assert.IsTrue(sim.Submit(Command.MoveTo("c", append: true), out _));
            Assert.IsTrue(sim.Submit(Command.MoveTo("a", append: true), out _));
            Assert.AreEqual(3, sim.Character.Queue.Count);
            Assert.IsFalse(sim.Submit(Command.MoveTo("b", append: true), out var reason));
            Assert.AreEqual("Too many things to do.", reason);
            Assert.IsTrue(sim.Submit(Command.MoveTo("c"), out _));
            Assert.AreEqual(1, sim.Character.Queue.Count);
            Assert.AreEqual(CommandType.MoveTo, sim.Character.Queue[0].Command.Type);
            Assert.AreEqual("c", sim.Character.Queue[0].Command.A);
        }

        [Test]
        public void CancelAction_RemovesThatActionOnly()
        {
            // Arrange
            var sim = ThreeRooms().Sim(TestData.V11Rules());
            sim.Start();
            sim.Submit(Command.MoveTo("b"), out _);
            sim.Submit(Command.MoveTo("c", append: true), out _);

            // Act
            bool ok = sim.Submit(Command.CancelAction(1), out _);

            // Assert
            Assert.IsTrue(ok);
            Assert.AreEqual(1, sim.Character.Queue.Count);
            Assert.AreEqual("b", sim.Character.Queue[0].Command.A);
        }

        [Test]
        public void OrdersGivenWhilePaused_WaitUntilTimeRuns()
        {
            // Arrange
            var sim = ThreeRooms().Sim(TestData.V11Rules());
            sim.Submit(Command.Plug("kettle", "A1"), out _);
            sim.Start();
            sim.SetPaused(true);

            // Act
            bool accepted = sim.Submit(Command.SetSwitch("kettle", true), out _);
            Steps(sim, 5);
            bool onWhilePaused = sim.FindAppliance("kettle").SwitchedOn;
            sim.SetPaused(false);
            Steps(sim, sim.Rules.interactTicks);

            // Assert
            Assert.IsTrue(accepted);
            Assert.IsFalse(onWhilePaused);
            Assert.AreEqual(sim.Rules.interactTicks, sim.Now);
            Assert.IsTrue(sim.FindAppliance("kettle").SwitchedOn);
        }

        [Test]
        public void MoveToDuringFreePlanning_IsRejected()
        {
            // Arrange
            var sim = ThreeRooms().Sim(TestData.V11Rules());

            // Act
            bool ok = sim.Submit(Command.MoveTo("b"), out var reason);

            // Assert
            Assert.IsFalse(ok);
            Assert.AreEqual("Unpause to walk.", reason);
        }

        [Test]
        public void UnreachableRoom_IsRejectedWhenTheCharacterSetsOff()
        {
            // Arrange: c has no door.
            var sim = new LevelBuilder().Room("a").Room("b").Room("c").Door("D1", "a", "b")
                .Circuit("A", 10).Socket("A1", "a", "A").Request("Mum", "kettle", "a", LongSec, DueSec)
                .Sim(TestData.V11Rules());
            sim.Start();
            sim.Submit(Command.MoveTo("c"), out _);

            // Act
            var events = sim.Step();

            // Assert
            var rejected = TestData.OfType(new System.Collections.Generic.List<SimEvent>(events), SimEventType.CommandRejected);
            Assert.AreEqual(1, rejected.Count);
            Assert.AreEqual("Can't get there.", rejected[0].Message);
            Assert.AreEqual(0, sim.Character.Queue.Count);
        }
    }
}
