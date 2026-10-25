using NUnit.Framework;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>Day requests that clash with their room on purpose (roadmap M2), and only when they can be solved.</summary>
    public class ConflictTests
    {
        private const int Travel = 100;

        [Test]
        public void IsSolvableConflict_FitsWithTheRest_NotAClash()
        {
            // Arrange / Act: 4 + 2 waiting + 3 = 9 of 11.
            bool clash = ConflictRules.IsSolvableConflict(11, 4, 2, 3, 0, Travel, 400);

            // Assert
            Assert.IsFalse(clash);
        }

        [Test]
        public void IsSolvableConflict_OverWithTimeToWait_Clash()
        {
            // Arrange / Act: 7 + 3 + 6 = 16 of 11; 120 ticks of use left + 100 to carry fits in 400.
            bool clash = ConflictRules.IsSolvableConflict(11, 7, 3, 6, 120, Travel, 400);

            // Assert
            Assert.IsTrue(clash);
        }

        [Test]
        public void IsSolvableConflict_TooBigForTheRoomAlone_NotAClash()
        {
            // Arrange / Act: a 12 can never run in an 11 room.
            bool clash = ConflictRules.IsSolvableConflict(11, 5, 0, 12, 0, Travel, 400);

            // Assert
            Assert.IsFalse(clash);
        }

        [Test]
        public void IsSolvableConflict_NoTimeToWait_NotAClash()
        {
            // Arrange / Act: 350 ticks of use left + 100 to carry is past the 400 deadline.
            bool clash = ConflictRules.IsSolvableConflict(11, 7, 3, 6, 350, Travel, 400);

            // Assert
            Assert.IsFalse(clash);
        }

        [Test]
        public void DemoDay_WithConflictChance_MakesClashesThatFitTheirRoom()
        {
            // Arrange
            int clashes = 0;

            // Act: twenty idle days of the shipped demo.
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = IdleDay(seed, 0.6f);
                foreach (var r in sim.Requests)
                {
                    // Assert: every request still fits its room on its own.
                    Assert.LessOrEqual(sim.FindAppliance(r.ApplianceId).Load, sim.FindCircuit(r.RoomId).Capacity, $"seed {seed} {r.ApplianceId}");
                    if (r.Conflict) clashes++;
                }
            }

            // Assert
            Assert.Greater(clashes, 0);
        }

        [Test]
        public void DemoDay_NoConflictChance_NeverMarksAClash()
        {
            // Arrange / Act
            var sim = IdleDay(3, 0f);

            // Assert
            foreach (var r in sim.Requests) Assert.IsFalse(r.Conflict);
        }

        private static HouseSim IdleDay(int seed, float conflictChance)
        {
            var level = JsonUtility.FromJson<LevelData>(Resources.Load<TextAsset>("Levels/level_demo").text);
            level.day.seed = seed;
            level.day.conflictChance = conflictChance;
            var catalog = new Catalog(JsonUtility.FromJson<CatalogData>(Resources.Load<TextAsset>("Config/catalog").text));
            var rules = TestData.V11Rules();
            var sim = new HouseSim(level, rules, catalog, TestData.Strings());
            sim.Start();
            for (int guard = 0; guard < 5000 && sim.Phase != LevelPhase.Won && sim.Phase != LevelPhase.Lost; guard++) sim.Step();
            return sim;
        }
    }
}
