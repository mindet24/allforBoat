using System;
using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>
    /// Day mode, rules verticalMove "stairs": flights at the open front of the house that A/D walk up and down, and that
    /// the character walks past behind.
    /// </summary>
    public class StairsTests
    {
        /// <summary>Two rooms stacked on two floors, both x 0..10, joined by stairs placed automatically (bottom step 0.5, top 3.5).</summary>
        private static LevelData TwoFloors()
        {
            var level = new LevelData { id = 902, dayLengthSec = 600f, startRoom = "down", panelRoom = "down" };
            level.rooms.Add(new RoomData { id = "down", name = "Down", floor = 0, x = 0, width = 10, capacity = 20 });
            level.rooms.Add(new RoomData { id = "up", name = "Up", floor = 1, x = 0, width = 10, capacity = 20 });
            level.doors.Add(new DoorData { id = "S", a = "down", b = "up", kind = "stairs" });
            level.sockets.Add(new SocketData { id = "D1", room = "down" });
            level.sockets.Add(new SocketData { id = "U1", room = "up" });
            level.appliances.Add(new ApplianceData { id = "kettle", type = "kettle", socket = "D1", load = 6, useSec = 8f });
            var day = level.day;
            day.enabled = true;
            day.seed = 5;
            day.hearts = 3;
            day.firstRequestSec = 10_000f;
            day.intervalStartSec = 10_000f;
            day.intervalEndSec = 10_000f;
            day.urgencies.Add(new UrgencyData { id = "normal", dueSec = 300f, points = 2, weight = 1f });
            day.people.Add(new PersonData { name = "Mum", rooms = new List<string> { "up" }, wants = new List<string> { "kettle" } });
            return level;
        }

        private static HouseSim Started()
        {
            var sim = new HouseSim(TwoFloors(), TestData.V11Rules(), TestData.Catalog(), TestData.Strings());
            sim.Start();
            return sim;
        }

        private static void Submit(HouseSim sim, Command command)
        {
            Assert.IsTrue(sim.Submit(command, out var reason), reason);
        }

        /// <summary>Steps until <paramref name="done"/> is true (at most 500 ticks).</summary>
        private static void StepUntil(HouseSim sim, Func<bool> done)
        {
            for (int i = 0; i < 500 && !done(); i++) sim.Step();
            var ch = sim.Character;
            Assert.IsTrue(done(), $"condition not reached in 500 ticks (room {ch.RoomId}, x {ch.XCm}, z {ch.ZCm}, air {ch.AirCm}, walk {ch.WalkDir}, stairs '{ch.StairsDoorId}', phase {sim.Phase})");
        }

        /// <summary>Walks to the left wall of the lower room, at the front.</summary>
        private static void ToLeftWall(HouseSim sim)
        {
            Submit(sim, Command.Walk(-1));
            int last = int.MinValue;
            StepUntil(sim, () =>
            {
                bool still = sim.Character.XCm == last;
                last = sim.Character.XCm;
                return still;
            });
            Submit(sim, Command.Walk(0));
        }

        private static DoorState Flight(HouseSim sim) => sim.FindDoor("S");

        [Test]
        public void StairsBetweenStackedRooms_ArePlacedInsideTheSharedRange()
        {
            // Act
            var sim = Started();
            var flight = Flight(sim);

            // Assert
            Assert.IsTrue(flight.IsFlight);
            Assert.AreEqual("down", flight.LowerRoomId);
            Assert.AreEqual("up", flight.UpperRoomId);
            Assert.AreEqual(50, flight.BottomXCm);
            Assert.AreEqual(50 + 300, flight.TopXCm);
        }

        [Test]
        public void AtTheFront_WalkingRightAcrossBottomStep_ClimbsToTheUpperFloor()
        {
            // Arrange
            var sim = Started();
            ToLeftWall(sim);

            // Act
            Submit(sim, Command.Walk(1));
            StepUntil(sim, () => sim.Character.OnStairs);
            StepUntil(sim, () => !sim.Character.OnStairs);

            // Assert
            Assert.AreEqual("up", sim.Character.RoomId);
            Assert.AreEqual(Flight(sim).TopXCm, sim.Character.XCm);
            Assert.AreEqual(0, sim.Character.AirCm);
        }

        [Test]
        public void OnTheStairs_HeightFollowsTheStepsUnderTheCharacter()
        {
            // Arrange
            var sim = Started();
            ToLeftWall(sim);
            Submit(sim, Command.Walk(1));
            StepUntil(sim, () => sim.Character.OnStairs);

            // Act
            sim.Step();
            sim.Step();

            // Assert: the height over the lower floor matches the flight's height at this x.
            var ch = sim.Character;
            var flight = Flight(sim);
            int overLower = ch.RoomId == "down" ? ch.AirCm : ch.AirCm + 300;
            Assert.AreEqual(sim.StairsHeight(flight, ch.X) * 100f, overLower, 1.5f);
            Assert.Greater(overLower, 0);
        }

        [Test]
        public void DeeperInTheRoom_WalkingAcrossTheBottomStep_WalksPastBehindTheStairs()
        {
            // Arrange: walk in past the stairs' depth.
            var sim = Started();
            ToLeftWall(sim);
            Submit(sim, Command.WalkDepth(1));
            StepUntil(sim, () => sim.Character.Z > sim.Rules.stairsDepthMetres);
            Submit(sim, Command.WalkDepth(0));

            // Act
            Submit(sim, Command.Walk(1));
            StepUntil(sim, () => sim.Character.XCm > Flight(sim).TopXCm);

            // Assert
            Assert.IsFalse(sim.Character.OnStairs);
            Assert.AreEqual("down", sim.Character.RoomId);
            Assert.AreEqual(0, sim.Character.AirCm);
        }

        [Test]
        public void UpstairsAtTheFront_WalkingLeftAcrossTopStep_GoesDown()
        {
            // Arrange: climb, then step a little further right on the upper floor.
            var sim = Started();
            ToLeftWall(sim);
            Submit(sim, Command.Walk(1));
            StepUntil(sim, () => sim.Character.RoomId == "up" && !sim.Character.OnStairs);
            sim.Step();

            // Act
            Submit(sim, Command.Walk(-1));
            StepUntil(sim, () => sim.Character.OnStairs);
            StepUntil(sim, () => !sim.Character.OnStairs);

            // Assert
            Assert.AreEqual("down", sim.Character.RoomId);
            Assert.AreEqual(Flight(sim).BottomXCm, sim.Character.XCm);
        }

        [Test]
        public void OnTheStairs_HandsAreBusy()
        {
            // Arrange
            var sim = Started();
            ToLeftWall(sim);
            Submit(sim, Command.Walk(1));
            StepUntil(sim, () => sim.Character.OnStairs);

            // Act
            bool interact = sim.Submit(Command.Interact(), out _);
            bool plug = sim.Submit(Command.TogglePlug(), out _);

            // Assert
            Assert.IsFalse(interact);
            Assert.IsFalse(plug);
        }

        [Test]
        public void WalkingIntoAWall_StaysStillInsteadOfJittering()
        {
            // Arrange: walk into the left wall (no door there).
            var sim = Started();
            Submit(sim, Command.Walk(-1));
            for (int i = 0; i < 40; i++) sim.Step();

            // Act: keep pushing.
            int before = sim.Character.XCm;
            sim.Step();
            int after = sim.Character.XCm;

            // Assert
            Assert.AreEqual(before, after);
        }

        [Test]
        public void StairsMode_RefusesJumping()
        {
            // Arrange
            var sim = Started();

            // Act
            bool accepted = sim.Submit(Command.Jump(1), out _);

            // Assert
            Assert.IsTrue(sim.UsesStairs);
            Assert.IsFalse(accepted);
        }
    }
}
