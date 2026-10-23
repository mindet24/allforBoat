using NUnit.Framework;
using Trouble.Game;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>HUD text and number rules (GDD v1.0 §4.2, §6.2–6.3).</summary>
    public class HudFormatTests
    {
        [TestCase(0, "0:00")]
        [TestCase(182, "0:18")]
        [TestCase(650, "1:05")]
        [TestCase(-5, "0:00")]
        public void Clock_RoundsDownToWholeSeconds(int ticks, string expected)
        {
            // Arrange + Act
            var text = HudFormat.Clock(ticks, 10);

            // Assert
            Assert.AreEqual(expected, text);
        }

        [Test]
        public void ClockPair_ShowsNowSlashDayLength()
        {
            // Arrange + Act
            var text = HudFormat.ClockPair(120, 350, 10);

            // Assert
            Assert.AreEqual("0:12 / 0:35", text);
        }

        [TestCase(300, 0, "0:30")]
        [TestCase(300, 291, "0:01")]
        [TestCase(300, 300, "0:00")]
        [TestCase(300, 400, "0:00")]
        public void TimeLeft_RoundsUpAndNeverGoesNegative(int due, int now, string expected)
        {
            // Arrange + Act
            var text = HudFormat.TimeLeft(due, now, 10);

            // Assert
            Assert.AreEqual(expected, text);
        }

        // Boundary values are the point here: 70% and 100% of capacity are the band edges.
        [TestCase(0, 8, LoadBand.Green)]
        [TestCase(7, 10, LoadBand.Green)]
        [TestCase(6, 8, LoadBand.Amber)]
        [TestCase(8, 8, LoadBand.Amber)]
        [TestCase(10, 8, LoadBand.Red)]
        [TestCase(1, 0, LoadBand.Red)]
        [TestCase(0, 0, LoadBand.Green)]
        public void Band_UsesSeventyAndHundredPercentEdges(int load, int capacity, LoadBand expected)
        {
            // Arrange + Act
            var band = HudFormat.Band(load, capacity);

            // Assert
            Assert.AreEqual(expected, band);
        }

        [TestCase(0, 8, 0f)]
        [TestCase(4, 8, 0.5f)]
        [TestCase(10, 8, 1f)]
        public void Fill_IsLoadOverCapacityClampedToOne(int load, int capacity, float expected)
        {
            // Arrange + Act
            float fill = HudFormat.Fill(load, capacity);

            // Assert
            Assert.AreEqual(expected, fill, 1e-5f);
        }

        [Test]
        public void LoadText_IsLoadSlashCapacity()
        {
            // Arrange + Act
            var text = HudFormat.LoadText(6, 8);

            // Assert
            Assert.AreEqual("6 / 8", text);
        }

        [TestCase(0, 10, 1f)]
        [TestCase(4, 10, 0.6f)]
        [TestCase(12, 10, 0f)]
        public void SecondsToTrip_CountsDownToTheTripTick(int overTicks, int tripTicks, float expected)
        {
            // Arrange + Act
            float seconds = HudFormat.SecondsToTrip(overTicks, tripTicks, 10);

            // Assert
            Assert.AreEqual(expected, seconds, 1e-5f);
        }

        [Test]
        public void ActionLabel_NamesTheApplianceOrCircuit()
        {
            // Arrange
            string Name(string id) => id == "kettle" ? "Kettle" : id;
            string Room(string id) => id == "living" ? "Living room" : id;

            // Act
            var plug = HudFormat.ActionLabel(Command.Plug("kettle", "K1"), Name, Room);
            var on = HudFormat.ActionLabel(Command.SetSwitch("kettle", true), Name, Room);
            var breaker = HudFormat.ActionLabel(Command.ToggleBreaker("A"), Name, Room);
            var move = HudFormat.ActionLabel(Command.MoveTo("living"), Name, Room);

            // Assert
            Assert.AreEqual("Plug Kettle", plug);
            Assert.AreEqual("On Kettle", on);
            Assert.AreEqual("Breaker A", breaker);
            Assert.AreEqual("Go to Living room", move);
        }
    }
}
