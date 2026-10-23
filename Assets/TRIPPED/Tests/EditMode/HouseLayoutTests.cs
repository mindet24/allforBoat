using NUnit.Framework;
using Trouble.Game;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>Rooms to world geometry: the 3D house must match the sim's metres (ADR-0001).</summary>
    public class HouseLayoutTests
    {
        private const float RoomHeight = 3f;

        /// <summary>Two auto-laid-out rooms (6 m each) joined by door D1, like level 1.</summary>
        private static HouseSim TwoRooms(RulesConfig rules = null)
        {
            return new LevelBuilder()
                .Room("kitchen").Room("living")
                .Circuit("A", 8)
                .Socket("K1", "kitchen", "A")
                .Request("Mum", "kettle", "kitchen", 8, 30)
                .Door("D1", "kitchen", "living")
                .Sim(rules ?? TestData.V11Rules());
        }

        [Test]
        public void RoomRect_UsesXWidthAndFloorTimesHeight()
        {
            // Arrange + Act
            var rect = HouseLayout.RoomRect(6f, 4f, 1, RoomHeight);

            // Assert
            Assert.AreEqual(new Rect(6f, 3f, 4f, 3f), rect);
        }

        [Test]
        public void HouseBounds_TwoAutoRooms_SpanTwelveMetresByOneStorey()
        {
            // Arrange
            var sim = TwoRooms();

            // Act
            var bounds = HouseLayout.HouseBounds(sim.Rooms, RoomHeight);

            // Assert
            Assert.AreEqual(new Rect(0f, 0f, 12f, 3f), bounds);
        }

        [Test]
        public void Walls_TwoRoomsWithDoor_ThreeWallsAndTheMiddleOneHasTheDoor()
        {
            // Arrange
            var sim = TwoRooms();

            // Act
            var walls = HouseLayout.Walls(sim.Rooms, sim.Doors);

            // Assert
            Assert.AreEqual(3, walls.Count);
            Assert.AreEqual(0f, walls[0].X, 1e-4f);
            Assert.IsTrue(walls[0].IsOuter);
            Assert.AreEqual("", walls[0].DoorId);
            Assert.AreEqual(6f, walls[1].X, 1e-4f);
            Assert.IsFalse(walls[1].IsOuter);
            Assert.AreEqual("D1", walls[1].DoorId);
            Assert.AreEqual(12f, walls[2].X, 1e-4f);
            Assert.IsTrue(walls[2].IsOuter);
        }

        [Test]
        public void Walls_ClosedDoor_StillMakesAnOpening()
        {
            // Arrange
            var sim = new LevelBuilder()
                .Room("a").Room("b").Room("c")
                .Circuit("A", 8).Socket("S1", "a", "A")
                .Door("D1", "a", "b", open: false)
                .Sim(TestData.V11Rules());

            // Act
            var walls = HouseLayout.Walls(sim.Rooms, sim.Doors);

            // Assert: a | b is the closed door; b | c has no door at all, so it is solid.
            Assert.AreEqual(4, walls.Count);
            Assert.AreEqual("D1", walls[1].DoorId);
            Assert.AreEqual("", walls[2].DoorId);
            Assert.IsFalse(walls[2].IsOuter);
        }

        [Test]
        public void Walls_StairsBetweenFloors_MakeNoWallOpening()
        {
            // Arrange: two rooms stacked, joined by stairs.
            var level = new LevelBuilder()
                .Room("down").Room("up")
                .Circuit("A", 8).Socket("S1", "down", "A")
                .Door("ST", "down", "up", kind: "stairs")
                .Build();
            level.rooms[0].width = 6f;
            level.rooms[1].width = 6f;
            level.rooms[1].floor = 1;
            var sim = new HouseSim(level, TestData.V11Rules(), TestData.Catalog(), TestData.Strings());

            // Act
            var walls = HouseLayout.Walls(sim.Rooms, sim.Doors);

            // Assert
            Assert.AreEqual(4, walls.Count);
            foreach (var w in walls)
            {
                Assert.AreEqual("", w.DoorId);
                Assert.IsTrue(w.IsOuter);
            }
            Assert.AreEqual(new Rect(0f, 0f, 6f, 6f), HouseLayout.HouseBounds(sim.Rooms, RoomHeight));
        }

        [Test]
        public void SocketX_IsRoomLeftEdgePlusPosTimesWidth()
        {
            // Arrange + Act
            float x = HouseLayout.SocketX(6f, 6f, 0.5f);

            // Assert
            Assert.AreEqual(9f, x, 1e-5f);
        }

        [Test]
        public void CharacterPoint_Standing_IsTheRoomCentreOnTheFloor()
        {
            // Arrange
            var sim = TwoRooms();

            // Act
            var p = HouseLayout.CharacterPoint(sim, 0f, RoomHeight);

            // Assert
            Assert.AreEqual(new Vector2(3f, 0f), p);
        }

        [Test]
        public void CharacterPoint_Walking_InterpolatesBetweenRoomCentres()
        {
            // Arrange: 6 m between centres at 3 m/s = 20 ticks; walk 10 of them.
            var sim = TwoRooms();
            sim.Start();
            Assert.IsTrue(sim.Submit(Command.MoveTo("living"), out _));
            for (int i = 0; i < 10; i++) sim.Step();
            Assert.IsTrue(sim.Character.IsWalking);
            Assert.AreEqual(20, sim.Character.SegmentTicks);

            // Act
            var atTick = HouseLayout.CharacterPoint(sim, 0f, RoomHeight);
            var between = HouseLayout.CharacterPoint(sim, 0.5f, RoomHeight);

            // Assert
            Assert.AreEqual(6f, atTick.x, 1e-4f);
            Assert.AreEqual(6.15f, between.x, 1e-4f);
            Assert.AreEqual(0f, atTick.y, 1e-4f);
        }

        [TestCase(0, 0, 0f, 1f)]
        [TestCase(5, 10, 0f, 0.5f)]
        [TestCase(9, 10, 2f, 1f)]
        public void WalkProgress_AddsTheTickFractionAndClamps(int elapsed, int segment, float fraction, float expected)
        {
            // Arrange + Act
            float p = HouseLayout.WalkProgress(elapsed, segment, fraction);

            // Assert
            Assert.AreEqual(expected, p, 1e-5f);
        }
    }
}
