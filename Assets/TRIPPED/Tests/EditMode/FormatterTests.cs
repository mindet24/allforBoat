using NUnit.Framework;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>GDD v1.0 §9.3: cause sentences built from real replays.</summary>
    public class FormatterTests
    {
        private static (ReplayResult result, CauseFormatter formatter) Play(int level, string script)
        {
            var data = TestData.Level(level);
            var commands = script == "naive" ? Replay.Naive(data) : Replay.Parse(script);
            var result = Replay.Run(new HouseSim(data, TestData.V10Rules(), TestData.Catalog(), TestData.Strings()), commands);
            return (result, new CauseFormatter(TestData.Strings()));
        }

        private static SimEvent First(ReplayResult result, SimEventType type) => result.Events.Find(e => e.Type == type);

        [Test]
        public void CircuitTrip_SpellsOutTheSum()
        {
            // Arrange: R3, level 1 naive.
            var (result, formatter) = Play(1, "naive");

            // Act
            var text = formatter.Describe(First(result, SimEventType.CircuitTripped), result.Sim);

            // Assert
            Assert.AreEqual("Breaker A (KITCHEN) tripped: Kettle 6 + Toaster 4 = 10 > 8", text);
        }

        [Test]
        public void MainTrip_GivesTheHouseTotal()
        {
            // Arrange: R20.
            var (result, formatter) = Play(5,
                "-1 plug kettle K1; -1 plug console BR1; -1 plug phone BR3; -1 plug washer LA1; -1 plug microwave K2; -1 plug dryer BA1; " +
                "0 on kettle; 0 on console; 0 on phone; 0 on dryer; 0 on washer; 80 on microwave");

            // Act
            var text = formatter.Describe(First(result, SimEventType.MainTripped), result.Sim);

            // Assert
            Assert.AreEqual("MAIN breaker tripped: whole house 23 > 20", text);
        }

        [Test]
        public void MissedDeadline_NamesOwnerApplianceAndTime()
        {
            // Arrange: R3.
            var (result, formatter) = Play(1, "naive");

            // Act
            var text = formatter.Describe(First(result, SimEventType.LevelLost), result.Sim);

            // Assert
            Assert.AreEqual("Mum's Kettle wasn't done by 0:30.", text);
        }

        [Test]
        public void SpoiledFridge_GivesTheLimit()
        {
            // Arrange: R17, level 4 naive.
            var (result, formatter) = Play(4, "naive");

            // Act
            var text = formatter.Describe(First(result, SimEventType.LevelLost), result.Sim);

            // Assert
            Assert.AreEqual("The fridge was off for more than 10 s. The food spoiled.", text);
        }

        [Test]
        public void Win_GivesTheFinishTime()
        {
            // Arrange: R1.
            var data = TestData.Level(1);
            var result = Replay.Run(new HouseSim(data, TestData.V10Rules(), TestData.Catalog(), TestData.Strings()), data.reference);

            // Act
            var text = new CauseFormatter(TestData.Strings()).Describe(First(result, SimEventType.LevelWon), result.Sim);

            // Assert
            Assert.AreEqual("Everyone's happy! Finished at 0:18.", text);
        }
    }
}
