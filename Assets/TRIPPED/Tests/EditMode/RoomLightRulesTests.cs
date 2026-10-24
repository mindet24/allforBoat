using NUnit.Framework;
using Trouble.Game;

namespace Trouble.Tests
{
    /// <summary>Room ceiling lights: on with power, fade up, flicker out.</summary>
    public class RoomLightRulesTests
    {
        private const float FadeIn = 0.4f;
        private const float Flicker = 0.3f;

        [Test]
        public void IsLit_RoomWithoutOwnCircuit_AlwaysLit()
        {
            // Arrange: puzzle levels have no circuit per room.

            // Act
            bool lit = RoomLightRules.IsLit(null);

            // Assert
            Assert.IsTrue(lit);
        }

        [Test]
        public void Level_PowerBack_FadesUpToFull()
        {
            // Arrange / Act
            float start = RoomLightRules.Level(true, 0f, FadeIn, Flicker);
            float half = RoomLightRules.Level(true, FadeIn * 0.5f, FadeIn, Flicker);
            float done = RoomLightRules.Level(true, FadeIn * 2f, FadeIn, Flicker);

            // Assert
            Assert.AreEqual(0f, start, 1e-4f);
            Assert.AreEqual(0.5f, half, 1e-4f);
            Assert.AreEqual(1f, done, 1e-4f);
        }

        [Test]
        public void Level_PowerLost_BlinksThenStaysDark()
        {
            // Arrange / Act
            float first = RoomLightRules.Level(false, Flicker * 0.1f, FadeIn, Flicker);
            float blink = RoomLightRules.Level(false, Flicker * 0.3f, FadeIn, Flicker);
            float after = RoomLightRules.Level(false, Flicker * 1.5f, FadeIn, Flicker);

            // Assert
            Assert.AreEqual(0f, first, 1e-4f);
            Assert.Greater(blink, 0f);
            Assert.AreEqual(0f, after, 1e-4f);
        }

        [Test]
        public void Level_NoFadeOrFlicker_SwitchesAtOnce()
        {
            // Arrange / Act
            float on = RoomLightRules.Level(true, 0f, 0f, 0f);
            float off = RoomLightRules.Level(false, 0f, 0f, 0f);

            // Assert
            Assert.AreEqual(1f, on, 1e-4f);
            Assert.AreEqual(0f, off, 1e-4f);
        }
    }
}
