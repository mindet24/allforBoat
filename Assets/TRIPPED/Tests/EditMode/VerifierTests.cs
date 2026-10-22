using NUnit.Framework;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>Run Sim rows: which runs exist and how expectations are judged.</summary>
    public class VerifierTests
    {
        [Test]
        public void RunAll_ReportsReferenceNaiveBothPumpsThenScenarios()
        {
            // Arrange
            var level = TestData.Level(1);
            level.scenarios.Add(new ScenarioData { name = "cord", expect = Verifier.Won,
                commands = Replay.Parse("-1 cord L1 kitchen; -1 plug kettle CORD-L1; -1 plug toaster K1; -1 on kettle; -1 on toaster") });

            // Act
            var rows = Verifier.RunAll(level, TestData.Instant(TestData.V11Rules()), TestData.Catalog(), TestData.Strings());

            // Assert
            Assert.AreEqual(5, rows.Count);
            Assert.AreEqual("reference", rows[0].Name);
            Assert.AreEqual(LevelPhase.Won, rows[0].Phase);
            Assert.IsTrue(rows[0].MetExpectation);
            Assert.AreEqual("Everyone's happy! Finished at 0:18.", rows[0].Cause);
            Assert.AreEqual(LevelPhase.Lost, rows[1].Phase, "naive play loses level 1");
            Assert.IsTrue(rows[1].MetExpectation);
            Assert.AreEqual("A 10>8 @0:00.9", rows[1].Trips);
            Assert.AreEqual("cord", rows[4].Name);
            Assert.AreEqual(100, rows[4].EndTick);
            Assert.IsTrue(rows[4].MetExpectation);
        }

        [Test]
        public void ScenarioWinningFasterThanReference_FailsNotFasterExpectation()
        {
            // Arrange: the cord solution (0:10) beats the reference (0:18).
            var level = TestData.Level(1);
            level.scenarios.Add(new ScenarioData { name = "too fast", expect = Verifier.NotFasterThanReference,
                commands = Replay.Parse("-1 cord L1 kitchen; -1 plug kettle CORD-L1; -1 plug toaster K1; -1 on kettle; -1 on toaster") });

            // Act
            var rows = Verifier.RunAll(level, TestData.Instant(TestData.V11Rules()), TestData.Catalog(), TestData.Strings());

            // Assert
            Assert.IsFalse(rows[4].MetExpectation);
        }

        [Test]
        public void LevelThatCannotBeBuilt_ReportsTheProblemInsteadOfThrowing()
        {
            // Arrange
            var level = TestData.Level(1);
            level.requests[0].appliance = "teleporter";

            // Act
            var rows = Verifier.RunAll(level, TestData.V11Rules(), TestData.Catalog(), TestData.Strings());

            // Assert
            Assert.That(rows[0].Problem, Does.Contain("teleporter"));
            Assert.IsFalse(rows[0].MetExpectation);
        }
    }
}
