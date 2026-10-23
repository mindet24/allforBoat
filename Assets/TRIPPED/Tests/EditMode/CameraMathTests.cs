using NUnit.Framework;
using Trouble.Game;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>House camera framing and zoom-to-cursor math (GDD v1.1 §2.6).</summary>
    public class CameraMathTests
    {
        private const float Fov = 20f;
        private const float Aspect = 16f / 9f;
        private static readonly Rect HouseViewport = new Rect(400f / 1920f, 200f / 1080f, 1120f / 1920f, 784f / 1080f);

        [Test]
        public void FitDistance_WideHouse_FillsViewportWidthWithMargin()
        {
            // Arrange
            var size = new Vector2(24f, 3f);

            // Act
            float d = CameraMath.FitDistance(size, Fov, Aspect, HouseViewport, 0.1f);
            var visible = CameraMath.VisibleSize(d, Fov, Aspect);

            // Assert: width is the limit, and it fits exactly with 10% margin; the height fits too.
            Assert.AreEqual(24f * 1.1f, visible.x * HouseViewport.width, 1e-3f);
            Assert.LessOrEqual(3f * 1.1f, visible.y * HouseViewport.height + 1e-3f);
        }

        [Test]
        public void FitDistance_TallHouse_FillsViewportHeightWithMargin()
        {
            // Arrange
            var size = new Vector2(4f, 9f);

            // Act
            float d = CameraMath.FitDistance(size, Fov, Aspect, HouseViewport, 0.1f);
            var visible = CameraMath.VisibleSize(d, Fov, Aspect);

            // Assert
            Assert.AreEqual(9f * 1.1f, visible.y * HouseViewport.height, 1e-3f);
            Assert.LessOrEqual(4f * 1.1f, visible.x * HouseViewport.width + 1e-3f);
        }

        [Test]
        public void CenterOn_PutsTargetAtTheViewportCentre()
        {
            // Arrange
            var target = new Vector2(6f, 1.5f);
            float d = 30f;

            // Act
            var cam = CameraMath.CenterOn(target, d, Fov, Aspect, HouseViewport);
            var seen = CameraMath.ViewportToPlane(cam, d, Fov, Aspect, HouseViewport.center);

            // Assert
            Assert.AreEqual(target.x, seen.x, 1e-4f);
            Assert.AreEqual(target.y, seen.y, 1e-4f);
        }

        [Test]
        public void ZoomToward_KeepsThePointUnderTheSameScreenSpot()
        {
            // Arrange
            var cam = new Vector2(6f, 1.5f);
            float oldD = 30f, newD = 30f * 0.85f;
            var cursor = new Vector2(0.8f, 0.3f);
            var point = CameraMath.ViewportToPlane(cam, oldD, Fov, Aspect, cursor);

            // Act
            var newCam = CameraMath.ZoomToward(cam, oldD, newD, point);
            var after = CameraMath.ViewportToPlane(newCam, newD, Fov, Aspect, cursor);

            // Assert
            Assert.AreEqual(point.x, after.x, 1e-4f);
            Assert.AreEqual(point.y, after.y, 1e-4f);
        }

        [Test]
        public void DistanceForWidth_RoomFillsTheRequestedScreenFraction()
        {
            // Arrange + Act
            float d = CameraMath.DistanceForWidth(4f, 0.6f, Fov, Aspect);

            // Assert
            Assert.AreEqual(4f, CameraMath.VisibleSize(d, Fov, Aspect).x * 0.6f, 1e-3f);
        }

        [Test]
        public void Clamp_KeepsCameraInsideLimits()
        {
            // Arrange
            var limits = new Rect(0f, 0f, 12f, 3f);

            // Act
            var clamped = CameraMath.Clamp(new Vector2(-5f, 10f), limits);

            // Assert
            Assert.AreEqual(new Vector2(0f, 3f), clamped);
        }
    }
}
