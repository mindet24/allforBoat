using NUnit.Framework;
using Trouble.Game;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>The rule that no circuit colour shows on an unrevealed socket or wire.</summary>
    public class SocketLookTests
    {
        [Test]
        public void Unrevealed_ShowsQuestionMarkAndNoFill()
        {
            // Arrange + Act
            var look = SocketLook.For(false, "B", "");

            // Assert
            Assert.AreEqual("?", look.Text);
            Assert.AreEqual("", look.FillCircuit);
            Assert.AreEqual("", look.DashedCircuit);
        }

        [Test]
        public void UnrevealedWithAssumption_OnlyDashesTheAssumedCircuit_NeverTheRealOne()
        {
            // Arrange + Act: level 4 style, really B but the labels say A.
            var look = SocketLook.For(false, "B", "A");

            // Assert
            Assert.AreEqual("?", look.Text);
            Assert.AreEqual("", look.FillCircuit);
            Assert.AreEqual("A", look.DashedCircuit);
        }

        [Test]
        public void Revealed_ShowsTheRealCircuitAndDropsTheDashes()
        {
            // Arrange + Act
            var look = SocketLook.For(true, "B", "A");

            // Assert
            Assert.AreEqual("B", look.Text);
            Assert.AreEqual("B", look.FillCircuit);
            Assert.AreEqual("", look.DashedCircuit);
        }

        [TestCase(2)]
        [TestCase(4)]
        public void ShippedLevelAtStart_NoUnrevealedSocketHasAFillColour(int level)
        {
            // Arrange
            var sim = new HouseSim(TestData.Level(level), TestData.V11Rules(), TestData.Catalog(), TestData.Strings());

            // Act + Assert
            foreach (var socket in sim.Sockets)
            {
                var look = SocketLook.For(socket.Revealed, socket.CircuitId, socket.AssumedCircuitId);
                if (!socket.Revealed) Assert.AreEqual("", look.FillCircuit, socket.Id);
                else Assert.AreEqual(socket.CircuitId, look.FillCircuit, socket.Id);
            }
        }
    }
}
