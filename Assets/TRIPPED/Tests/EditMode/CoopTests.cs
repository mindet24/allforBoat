using NUnit.Framework;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>Day mode co-op: a second keyboard player in the same sim, and lockstep determinism.</summary>
    public class CoopTests
    {
        private static HouseSim Demo(int players)
        {
            var level = JsonUtility.FromJson<LevelData>(Resources.Load<TextAsset>("Levels/level_demo").text);
            level.day.seed = 3;
            var catalog = new Catalog(JsonUtility.FromJson<CatalogData>(Resources.Load<TextAsset>("Config/catalog").text));
            var rules = TestData.V11Rules();
            var sim = new HouseSim(level, rules, catalog, TestData.Strings());
            for (int p = 1; p < players; p++) sim.AddPlayer();
            sim.Start();
            return sim;
        }

        [Test]
        public void AddPlayer_StartsWhereTheFirstStarts()
        {
            // Act
            var sim = Demo(2);

            // Assert
            Assert.AreEqual(2, sim.Characters.Count);
            Assert.AreEqual(sim.Characters[0].RoomId, sim.Characters[1].RoomId);
            Assert.AreEqual(sim.Characters[0].XCm, sim.Characters[1].XCm);
        }

        [Test]
        public void SecondPlayerWalks_FirstStaysPut()
        {
            // Arrange
            var sim = Demo(2);
            int firstX = sim.Characters[0].XCm, secondX = sim.Characters[1].XCm;

            // Act
            Assert.IsTrue(sim.Submit(Command.Walk(1).ForPlayer(1), out var reason), reason);
            for (int i = 0; i < 10; i++) sim.Step();

            // Assert
            Assert.AreEqual(firstX, sim.Characters[0].XCm);
            Assert.Greater(sim.Characters[1].XCm, secondX);
        }

        [Test]
        public void ViewPlayer_ChangesWhatCharacterShows_NotTheGame()
        {
            // Arrange
            var sim = Demo(2);
            sim.Submit(Command.Walk(1).ForPlayer(1), out _);
            for (int i = 0; i < 5; i++) sim.Step();

            // Act
            sim.ViewPlayer = 1;
            int shown = sim.Character.XCm;
            sim.ViewPlayer = 0;

            // Assert
            Assert.AreEqual(sim.Characters[1].XCm, shown);
            Assert.AreEqual(sim.Characters[0].XCm, sim.Character.XCm);
        }

        [Test]
        public void TwoSims_FedTheSameCommandsAndTicks_StayIdentical()
        {
            // Arrange: the host's sim and the guest's (which follows the player 1 view), as in NetRoom's lockstep.
            var host = Demo(2);
            var guest = Demo(2);
            guest.ViewPlayer = 1;
            var script = new (int tick, Command cmd)[]
            {
                (0, Command.Walk(1)), (3, Command.Walk(-1).ForPlayer(1)), (12, Command.Walk(0)),
                (14, Command.Interact()), (20, Command.Walk(0).ForPlayer(1)), (22, Command.Interact().ForPlayer(1)),
            };

            // Act
            int next = 0;
            for (int t = 0; t < 120; t++)
            {
                while (next < script.Length && script[next].tick == t)
                {
                    bool a = host.Submit(script[next].cmd, out _);
                    bool b = guest.Submit(script[next].cmd, out _);
                    Assert.AreEqual(a, b, $"tick {t}");
                    next++;
                }
                host.Step();
                guest.Step();
            }

            // Assert
            for (int p = 0; p < 2; p++)
            {
                Assert.AreEqual(host.Characters[p].XCm, guest.Characters[p].XCm, "x of player " + p);
                Assert.AreEqual(host.Characters[p].RoomId, guest.Characters[p].RoomId, "room of player " + p);
                Assert.AreEqual(host.Characters[p].CarryingId, guest.Characters[p].CarryingId, "carrying of player " + p);
            }
            Assert.AreEqual(host.Score, guest.Score);
            Assert.AreEqual(host.Now, guest.Now);
        }
    }
}
