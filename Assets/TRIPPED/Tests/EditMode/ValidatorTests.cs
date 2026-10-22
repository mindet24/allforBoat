using NUnit.Framework;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>GDD v1.0 §9.3: every shipped level passes; broken data is reported.</summary>
    public class ValidatorTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void ShippedLevel_HasNoErrors(int n)
        {
            // Arrange
            var level = TestData.Level(n);

            // Act
            var errors = LevelValidator.Validate(level, TestData.Catalog(), TestData.V11Rules());

            // Assert
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void SocketOnUnknownCircuit_IsReported()
        {
            // Arrange
            var level = TestData.Level(1);
            level.sockets[0].circuit = "Z";

            // Act
            var errors = LevelValidator.Validate(level, TestData.Catalog(), TestData.V11Rules());

            // Assert
            Assert.That(errors, Has.Some.Contains("unknown circuit 'Z'"));
        }

        [Test]
        public void RequestDueAfterDayEnds_IsReported()
        {
            // Arrange
            var level = TestData.Level(1);
            level.requests[0].dueSec = level.dayLengthSec + 1f;

            // Act
            var errors = LevelValidator.Validate(level, TestData.Catalog(), TestData.V11Rules());

            // Assert
            Assert.That(errors, Has.Some.Contains("due after the day ends"));
        }

        [Test]
        public void UnknownRuleOverride_IsReported()
        {
            // Arrange
            var level = TestData.Level(1);
            level.rulesOverride.Add(new RuleOverrideData { key = "noSuchRule", value = "1" });

            // Act
            var errors = LevelValidator.Validate(level, TestData.Catalog(), TestData.V11Rules());

            // Assert
            Assert.That(errors, Has.Some.Contains("noSuchRule"));
        }

        [Test]
        public void ReferenceOutOfTickOrder_IsReported()
        {
            // Arrange
            var level = TestData.Level(1);
            level.reference.Add(new CommandData { tick = 5, cmd = "on", a = "kettle" });

            // Act
            var errors = LevelValidator.Validate(level, TestData.Catalog(), TestData.V11Rules());

            // Assert
            Assert.That(errors, Has.Some.Contains("tick order"));
        }
    }
}
