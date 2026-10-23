using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Game;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>State-based room alerts and the off-screen arrow math (GDD v1.1 §2.5).</summary>
    public class AlertRulesTests
    {
        private static HouseSim Level()
        {
            // Kettle due at 12 s, the heater routine starts at 5 s, the fridge spoils after 2 s without power.
            return new LevelBuilder()
                .Room("kitchen").Room("living")
                .Circuit("A", 8).Circuit("B", 8)
                .Socket("K1", "kitchen", "A").Socket("K2", "kitchen", "A").Socket("L1", "living", "B")
                .Request("Mum", "kettle", "kitchen", 8, 12)
                .Routine("Grandpa", "heater", "L1", 5, 5)
                .Fridge("K2", 2)
                .Day(30)
                .Panel("kitchen")
                .Sim(TestData.V11Rules());
        }

        private static List<Alert> Collect(HouseSim sim)
        {
            var list = new List<Alert>();
            AlertRules.Collect(sim, 5f, 3f, list);
            return list;
        }

        private static void Run(HouseSim sim, int ticks)
        {
            for (int i = 0; i < ticks; i++) sim.Step();
        }

        [Test]
        public void Collect_Planning_HasNoAlerts()
        {
            // Arrange
            var sim = Level();

            // Act
            var alerts = Collect(sim);

            // Assert
            Assert.AreEqual(0, alerts.Count);
        }

        [Test]
        public void Collect_RequestWithinFiveSecondsOfItsDeadline_AlertsOverTheRequestRoom()
        {
            // Arrange: due at tick 120; warn from tick 71.
            var sim = Level();
            sim.Start();
            Run(sim, 60);
            var early = Collect(sim);
            Run(sim, 20);

            // Act
            var alerts = Collect(sim);

            // Assert
            Assert.IsFalse(early.Exists(a => a.Kind == AlertKind.DeadlineSoon));
            var deadline = alerts.Find(a => a.Kind == AlertKind.DeadlineSoon);
            Assert.AreEqual("kitchen", deadline.RoomId);
            Assert.AreEqual("Mum", deadline.Owner);
            Assert.AreEqual(40, deadline.TicksLeft);
        }

        [Test]
        public void Collect_RoutineStartingWithinThreeSeconds_AlertsOverTheRoutineRoom()
        {
            // Arrange: starts at tick 50; warn from tick 20.
            var sim = Level();
            sim.Start();
            Run(sim, 30);

            // Act
            var alerts = Collect(sim);

            // Assert
            var routine = alerts.Find(a => a.Kind == AlertKind.RoutineSoon);
            Assert.AreEqual("living", routine.RoomId);
            Assert.AreEqual("Grandpa", routine.Owner);
            Assert.AreEqual(20, routine.TicksLeft);
        }

        [Test]
        public void Collect_RoutineAlreadyRunning_HasNoRoutineAlert()
        {
            // Arrange
            var sim = Level();
            sim.Start();
            Run(sim, 55);

            // Act
            var alerts = Collect(sim);

            // Assert
            Assert.IsFalse(alerts.Exists(a => a.Kind == AlertKind.RoutineSoon));
        }

        [Test]
        public void Collect_FridgeWithoutPower_CountsDownToSpoiling()
        {
            // Arrange: flip the kitchen breaker off (the character is already in the panel room).
            var sim = Level();
            sim.Start();
            Assert.IsTrue(sim.Submit(Command.ToggleBreaker("A"), out _));
            Run(sim, 8);
            Assert.IsFalse(sim.FindCircuit("A").IsOn);

            // Act
            var alerts = Collect(sim);

            // Assert
            var fridge = alerts.Find(a => a.Kind == AlertKind.FridgeOff);
            Assert.AreEqual("kitchen", fridge.RoomId);
            Assert.AreEqual(sim.SpoilLimitTicks - sim.SpoilTicks, fridge.TicksLeft);
        }

        [Test]
        public void Collect_OverloadedCircuit_AddsNoAlertAboutIt()
        {
            // Arrange: kettle (6) and heater are not both on the kitchen circuit, but load alone must never make an alert.
            var sim = Level();
            Assert.IsTrue(sim.Submit(Command.Plug("kettle", "K1"), out _));
            Assert.IsTrue(sim.Submit(Command.SetSwitch("kettle", true), out _));
            sim.Start();
            Run(sim, 5);

            // Act
            var alerts = Collect(sim);

            // Assert: every alert is one of the four kinds of GDD v1.1 §2.5.
            foreach (var a in alerts)
                Assert.IsTrue(a.Kind == AlertKind.DeadlineSoon || a.Kind == AlertKind.RoutineSoon || a.Kind == AlertKind.FridgeOff);
        }

        [Test]
        public void EdgePoint_InsideTheArea_NeedsNoArrow()
        {
            // Arrange + Act
            bool arrow = AlertRules.EdgePoint(new Vector2(0.5f, 0.5f), new Rect(0.2f, 0.2f, 0.6f, 0.6f), out _, out _);

            // Assert
            Assert.IsFalse(arrow);
        }

        [Test]
        public void EdgePoint_ToTheRight_LandsOnTheRightEdgePointingRight()
        {
            // Arrange + Act
            bool arrow = AlertRules.EdgePoint(new Vector2(1.5f, 0.5f), new Rect(0.2f, 0.2f, 0.6f, 0.6f), out var edge, out float angle);

            // Assert
            Assert.IsTrue(arrow);
            Assert.AreEqual(0.8f, edge.x, 1e-4f);
            Assert.AreEqual(0.5f, edge.y, 1e-4f);
            Assert.AreEqual(0f, angle, 1e-3f);
        }

        [Test]
        public void EdgePoint_SteeplyUpAndLeft_ExitsThroughTheTopEdge()
        {
            // Arrange + Act: from the centre (0.5, 0.5) toward (-0.5, 2.5) the line is steeper than 45 degrees.
            bool arrow = AlertRules.EdgePoint(new Vector2(-0.5f, 2.5f), new Rect(0.2f, 0.2f, 0.6f, 0.6f), out var edge, out float angle);

            // Assert
            Assert.IsTrue(arrow);
            Assert.AreEqual(0.8f, edge.y, 1e-4f);
            Assert.AreEqual(0.5f - 0.15f, edge.x, 1e-4f);
            Assert.Greater(angle, 90f);
        }

        [TestCase(0, 350, 0f)]
        [TestCase(175, 350, 0.5f)]
        [TestCase(500, 350, 1f)]
        [TestCase(10, 0, 0f)]
        public void TimelineFraction_IsTickOverDayClamped(int tick, int day, float expected)
        {
            // Arrange + Act
            float f = AlertRules.TimelineFraction(tick, day);

            // Assert
            Assert.AreEqual(expected, f, 1e-5f);
        }

        [TestCase("Mum", "M")]
        [TestCase("grandpa", "G")]
        [TestCase("", "?")]
        public void Initial_IsTheUpperCaseFirstLetter(string name, string expected)
        {
            // Arrange + Act
            var initial = AlertRules.Initial(name);

            // Assert
            Assert.AreEqual(expected, initial);
        }
    }
}
