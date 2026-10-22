using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>HouseSim.Recorded: a hand-played run, played back, gives the same game (GDD v1.1 §4.4 Record).</summary>
    public class RecordTests
    {
        [Test]
        public void RecordedHandPlay_ReplaysToTheSameGame()
        {
            // Arrange: play level 2 by hand on the shipped rules, with plain clicks, Shift+clicks,
            // a cancel, a pause, and a mistake that trips a breaker.
            var level = TestData.Level(2);
            var rules = TestData.V11Rules();
            var played = new HouseSim(level, rules, TestData.Catalog(), TestData.Strings());
            played.Submit(Command.Plug("dryer", "BA1"), out _);
            played.Submit(Command.Plug("kettle", "K1"), out _);
            played.Submit(Command.Plug("microwave", "K2"), out _);
            played.Submit(Command.Plug("phone", "BR1"), out _);
            foreach (var id in new[] { "dryer", "kettle", "microwave", "phone" }) played.Submit(Command.SetSwitch(id, true), out _);
            played.Start();
            var playedEvents = new List<SimEvent>();
            for (int t = 0; t < 400 && played.Phase != LevelPhase.Won && played.Phase != LevelPhase.Lost; t++)
            {
                if (t == 12)
                {
                    played.Submit(Command.Unplug("microwave"), out _);
                    played.Submit(Command.Plug("kettle", "K2", append: true), out _);
                    played.Submit(Command.MoveTo("bedroom", append: true), out _);
                    played.Submit(Command.CancelAction(2), out _);
                    played.SetPaused(true);
                    played.Submit(Command.Plug("microwave", "K1", append: true), out _);
                    played.SetPaused(false);
                }
                if (t == 30) played.Submit(Command.SetSwitch("kettle", true), out _);
                if (t == 31) played.Submit(Command.SetSwitch("microwave", true, append: true), out _);
                if (t == 32) played.Submit(Command.ToggleBreaker("A", append: true), out _);
                playedEvents.AddRange(played.Step());
            }

            // Act
            var replayed = Replay.Run(new HouseSim(level, rules, TestData.Catalog(), TestData.Strings()), new List<CommandData>(played.Recorded));

            // Assert
            Assert.AreEqual(played.Phase, replayed.Phase);
            Assert.AreEqual(played.Now, replayed.EndTick);
            Assert.AreEqual(played.TripCount, replayed.TripCount);
            Assert.AreEqual(playedEvents.Count, replayed.Events.Count);
            for (int i = 0; i < playedEvents.Count; i++)
                Assert.AreEqual((playedEvents[i].Type, playedEvents[i].Tick), (replayed.Events[i].Type, replayed.Events[i].Tick), $"event {i}");
        }

        [Test]
        public void ScriptWithReplaceMarks_RoundTripsThroughText()
        {
            // Arrange
            var commands = Replay.Parse("-1 plug kettle K1; 12 !on kettle; 13 off kettle; 14 cancel 1");

            // Act
            var again = Replay.Parse(Replay.ToScript(commands));

            // Assert
            Assert.AreEqual(4, again.Count);
            Assert.IsTrue(again[1].replace);
            Assert.IsFalse(again[2].replace);
            Assert.AreEqual("cancel", again[3].cmd);
            Assert.AreEqual("1", again[3].a);
        }
    }
}
