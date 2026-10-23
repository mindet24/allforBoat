using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Game;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>Wall wire routes, the reveal sweep and the sagging cord (decision Q28, GDD v1.0 §6.3).</summary>
    public class WireRoutingTests
    {
        private static List<Vector2> TestRoute()
        {
            var route = new List<Vector2>();
            WireRouting.Route(new Vector2(1f, 0.6f), new Vector2(7f, 1.9f), 2.8f, 0.1f, route);
            return route;
        }

        [Test]
        public void Route_GoesUpAcrossAndDownToThePanel()
        {
            // Arrange + Act
            var route = TestRoute();

            // Assert
            Assert.AreEqual(4, route.Count);
            Assert.AreEqual(new Vector2(1f, 0.6f), route[0]);
            Assert.AreEqual(2.7f, route[1].y, 1e-5f);
            Assert.AreEqual(1f, route[1].x, 1e-5f);
            Assert.AreEqual(7f, route[2].x, 1e-5f);
            Assert.AreEqual(2.7f, route[2].y, 1e-5f);
            Assert.AreEqual(new Vector2(7f, 1.9f), route[3]);
        }

        [Test]
        public void Length_AddsEverySegment()
        {
            // Arrange
            var route = TestRoute();

            // Act
            float length = WireRouting.Length(route);

            // Assert: 2.1 up, 6 across, 0.8 down.
            Assert.AreEqual(8.9f, length, 1e-4f);
        }

        [TestCase(0f, 1)]
        [TestCase(1f, 4)]
        public void Truncate_ZeroKeepsOnlyTheStartAndOneKeepsEverything(float fraction, int expectedPoints)
        {
            // Arrange
            var route = TestRoute();
            var part = new List<Vector2>();

            // Act
            WireRouting.Truncate(route, fraction, part);

            // Assert
            Assert.AreEqual(expectedPoints, part.Count);
            Assert.AreEqual(route[0], part[0]);
        }

        [Test]
        public void Truncate_Half_StopsInsideTheHorizontalRun()
        {
            // Arrange: half of 8.9 m = 4.45 m = 2.1 up + 2.35 across.
            var route = TestRoute();
            var part = new List<Vector2>();

            // Act
            WireRouting.Truncate(route, 0.5f, part);

            // Assert
            Assert.AreEqual(3, part.Count);
            Assert.AreEqual(1f + 2.35f, part[2].x, 1e-4f);
            Assert.AreEqual(2.7f, part[2].y, 1e-4f);
        }

        [Test]
        public void SagPath_KeepsTheAnchorsAndHangsLowestInTheMiddleOfEachSpan()
        {
            // Arrange
            var anchors = new List<Vector2> { new Vector2(0f, 1f), new Vector2(4f, 1f) };
            var path = new List<Vector2>();

            // Act
            WireRouting.SagPath(anchors, 0.2f, 8, path);

            // Assert
            Assert.AreEqual(9, path.Count);
            Assert.AreEqual(anchors[0], path[0]);
            Assert.AreEqual(1f, path[8].y, 1e-5f);
            Assert.AreEqual(0.8f, path[4].y, 1e-5f);
        }
    }
}
