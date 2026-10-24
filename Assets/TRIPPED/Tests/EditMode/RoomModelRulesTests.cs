using NUnit.Framework;
using Trouble.Game;

namespace Trouble.Tests
{
    /// <summary>Furnished room models: real-width models at scale 1, old module-width ones stretched, hide lists.</summary>
    public class RoomModelRulesTests
    {
        [TestCase(9f, 9f, 1f)]
        [TestCase(8.95f, 9f, 1f)]
        [TestCase(6f, 8f, 8f / 6f)]
        [TestCase(6f, 5f, 5f / 6f)]
        [TestCase(0f, 8f, 1f)]
        public void ScaleX_IsOneAtRealWidthElseStretchesToFit(float model, float room, float expected)
        {
            // Act
            float scale = RoomModelRules.ScaleX(model, room, 0.1f);

            // Assert
            Assert.AreEqual(expected, scale, 1e-4f);
        }

        [Test]
        public void Hides_AlwaysListApplies_RescaledListOnlyWhenStretched()
        {
            // Arrange
            var always = new[] { "Fridge" };
            var whenRescaled = new[] { "Kitchen_Counter" };

            // Act + Assert
            Assert.IsTrue(RoomModelRules.Hides("Fridge", always, whenRescaled, false));
            Assert.IsFalse(RoomModelRules.Hides("Kitchen_Counter", always, whenRescaled, false), "a real-width kitchen keeps its counter");
            Assert.IsTrue(RoomModelRules.Hides("Kitchen_Counter", always, whenRescaled, true));
            Assert.IsFalse(RoomModelRules.Hides("Kitchen_Window", always, whenRescaled, true));
            Assert.IsFalse(RoomModelRules.Hides("Fridge", null, null, true));
        }
    }
}
