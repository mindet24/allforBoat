using NUnit.Framework;
using Trouble.Game;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>Click-then-click input rules of G1 (GDD v1.1 N13).</summary>
    public class ClickResolverTests
    {
        private static ClickInput Tray(string id) => new ClickInput { Kind = ClickKind.TrayAppliance, Id = id };

        private static ClickInput Appliance(string id, bool on, string room = "kitchen", bool right = false, bool shift = false) =>
            new ClickInput { Kind = ClickKind.WorldAppliance, Id = id, SwitchedOn = on, RoomId = room, Right = right, Shift = shift };

        private static ClickInput Socket(string id, string occupant = "", bool occupantFixed = false, string room = "kitchen", bool shift = false, bool right = false) =>
            new ClickInput { Kind = ClickKind.Socket, Id = id, OccupantId = occupant, OccupantFixed = occupantFixed, RoomId = room, Shift = shift, Right = right };

        [Test]
        public void TrayClick_NothingSelected_SelectsTheAppliance()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(null, Tray("kettle"));

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.AreEqual("kettle", outcome.Selection);
        }

        [Test]
        public void TrayClick_SameApplianceSelected_DropsSelection()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve("kettle", Tray("kettle"));

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.IsNull(outcome.Selection);
        }

        [Test]
        public void SocketClick_ApplianceSelected_PlugsItThereAndClearsSelection()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve("kettle", Socket("K1", shift: true));

            // Assert
            Assert.IsTrue(outcome.HasCommand);
            Assert.AreEqual(CommandType.Plug, outcome.Command.Type);
            Assert.AreEqual("kettle", outcome.Command.A);
            Assert.AreEqual("K1", outcome.Command.B);
            Assert.IsTrue(outcome.Command.Append);
            Assert.IsNull(outcome.Selection);
        }

        [Test]
        public void SocketClick_OwnSocketOfSelectedAppliance_JustDropsSelection()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve("kettle", Socket("K1", occupant: "kettle"));

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.IsNull(outcome.Selection);
        }

        [Test]
        public void SocketClick_NothingSelectedAndOccupied_SelectsTheOccupantToMoveIt()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(null, Socket("K1", occupant: "toaster"));

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.AreEqual("toaster", outcome.Selection);
        }

        [Test]
        public void SocketClick_NothingSelectedAndFixedOccupant_WalksToTheRoom()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(null, Socket("K3", occupant: "fridge", occupantFixed: true));

            // Assert
            Assert.IsTrue(outcome.HasCommand);
            Assert.AreEqual(CommandType.MoveTo, outcome.Command.Type);
            Assert.AreEqual("kitchen", outcome.Command.A);
            Assert.IsNull(outcome.Selection);
        }

        [Test]
        public void ApplianceClick_NothingSelected_TogglesItsSwitch()
        {
            // Arrange + Act
            var turnOn = ClickResolver.Resolve(null, Appliance("kettle", on: false));
            var turnOff = ClickResolver.Resolve(null, Appliance("kettle", on: true));

            // Assert
            Assert.AreEqual(CommandType.SetSwitch, turnOn.Command.Type);
            Assert.IsTrue(turnOn.Command.On);
            Assert.AreEqual(CommandType.SetSwitch, turnOff.Command.Type);
            Assert.IsFalse(turnOff.Command.On);
        }

        [Test]
        public void ApplianceClick_ItselfSelected_TogglesAndClearsSelection()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve("kettle", Appliance("kettle", on: false));

            // Assert
            Assert.IsTrue(outcome.HasCommand);
            Assert.AreEqual(CommandType.SetSwitch, outcome.Command.Type);
            Assert.IsNull(outcome.Selection);
        }

        [Test]
        public void ApplianceClick_OtherSelected_SelectsThisOneInstead()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve("toaster", Appliance("kettle", on: false));

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.AreEqual("kettle", outcome.Selection);
        }

        [Test]
        public void RightClick_PluggedAppliance_Unplugs()
        {
            // Arrange + Act
            var onAppliance = ClickResolver.Resolve("toaster", Appliance("kettle", on: true, right: true));
            var onSocket = ClickResolver.Resolve(null, Socket("K1", occupant: "kettle", right: true));

            // Assert
            Assert.AreEqual(CommandType.Unplug, onAppliance.Command.Type);
            Assert.AreEqual("kettle", onAppliance.Command.A);
            Assert.IsNull(onAppliance.Selection);
            Assert.AreEqual(CommandType.Unplug, onSocket.Command.Type);
            Assert.AreEqual("kettle", onSocket.Command.A);
        }

        [Test]
        public void RoomClick_WalksThereAndKeepsSelection()
        {
            // Arrange
            var click = new ClickInput { Kind = ClickKind.Room, Id = "living", RoomId = "living" };

            // Act
            var outcome = ClickResolver.Resolve("kettle", click);

            // Assert
            Assert.IsTrue(outcome.HasCommand);
            Assert.AreEqual(CommandType.MoveTo, outcome.Command.Type);
            Assert.AreEqual("living", outcome.Command.A);
            Assert.IsFalse(outcome.Command.Append);
            Assert.AreEqual("kettle", outcome.Selection);
        }

        [Test]
        public void ClickOnNothing_ClearsSelection()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve("kettle", new ClickInput { Kind = ClickKind.Nothing });

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.IsNull(outcome.Selection);
        }
    }
}
