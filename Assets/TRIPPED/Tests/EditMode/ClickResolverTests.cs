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

        private static ClickInput Socket(string id, string occupant = "", bool occupantFixed = false, string room = "kitchen", bool shift = false, bool right = false, bool cord = false) =>
            new ClickInput { Kind = ClickKind.Socket, Id = id, OccupantId = occupant, OccupantFixed = occupantFixed, RoomId = room, Shift = shift, Right = right, SocketIsCord = cord };

        [Test]
        public void TrayClick_NothingSelected_SelectsTheAppliance()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(Held.None, Tray("kettle"));

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.AreEqual("kettle", outcome.Selection.ApplianceId);
        }

        [Test]
        public void TrayClick_SameApplianceSelected_DropsSelection()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(Held.Appliance("kettle"), Tray("kettle"));

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.IsTrue(outcome.Selection.IsNone);
        }

        [Test]
        public void SocketClick_ApplianceSelected_PlugsItThereAndClearsSelection()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(Held.Appliance("kettle"), Socket("K1", shift: true));

            // Assert
            Assert.IsTrue(outcome.HasCommand);
            Assert.AreEqual(CommandType.Plug, outcome.Command.Type);
            Assert.AreEqual("kettle", outcome.Command.A);
            Assert.AreEqual("K1", outcome.Command.B);
            Assert.IsTrue(outcome.Command.Append);
            Assert.IsTrue(outcome.Selection.IsNone);
        }

        [Test]
        public void SocketClick_OwnSocketOfSelectedAppliance_JustDropsSelection()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(Held.Appliance("kettle"), Socket("K1", occupant: "kettle"));

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.IsTrue(outcome.Selection.IsNone);
        }

        [Test]
        public void SocketClick_NothingSelectedAndOccupied_SelectsTheOccupantToMoveIt()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(Held.None, Socket("K1", occupant: "toaster"));

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.AreEqual("toaster", outcome.Selection.ApplianceId);
        }

        [Test]
        public void SocketClick_NothingSelectedAndFixedOccupant_WalksToTheRoom()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(Held.None, Socket("K3", occupant: "fridge", occupantFixed: true));

            // Assert
            Assert.IsTrue(outcome.HasCommand);
            Assert.AreEqual(CommandType.MoveTo, outcome.Command.Type);
            Assert.AreEqual("kitchen", outcome.Command.A);
            Assert.IsTrue(outcome.Selection.IsNone);
        }

        [Test]
        public void ApplianceClick_NothingSelected_TogglesItsSwitch()
        {
            // Arrange + Act
            var turnOn = ClickResolver.Resolve(Held.None, Appliance("kettle", on: false));
            var turnOff = ClickResolver.Resolve(Held.None, Appliance("kettle", on: true));

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
            var outcome = ClickResolver.Resolve(Held.Appliance("kettle"), Appliance("kettle", on: false));

            // Assert
            Assert.IsTrue(outcome.HasCommand);
            Assert.AreEqual(CommandType.SetSwitch, outcome.Command.Type);
            Assert.IsTrue(outcome.Selection.IsNone);
        }

        [Test]
        public void ApplianceClick_OtherSelected_SelectsThisOneInstead()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(Held.Appliance("toaster"), Appliance("kettle", on: false));

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.AreEqual("kettle", outcome.Selection.ApplianceId);
        }

        [Test]
        public void RightClick_PluggedAppliance_Unplugs()
        {
            // Arrange + Act
            var onAppliance = ClickResolver.Resolve(Held.Appliance("toaster"), Appliance("kettle", on: true, right: true));
            var onSocket = ClickResolver.Resolve(Held.None, Socket("K1", occupant: "kettle", right: true));

            // Assert
            Assert.AreEqual(CommandType.Unplug, onAppliance.Command.Type);
            Assert.AreEqual("kettle", onAppliance.Command.A);
            Assert.IsTrue(onAppliance.Selection.IsNone);
            Assert.AreEqual(CommandType.Unplug, onSocket.Command.Type);
            Assert.AreEqual("kettle", onSocket.Command.A);
        }

        [Test]
        public void RoomClick_WalksThereAndKeepsSelection()
        {
            // Arrange
            var click = new ClickInput { Kind = ClickKind.Room, Id = "living", RoomId = "living" };

            // Act
            var outcome = ClickResolver.Resolve(Held.Appliance("kettle"), click);

            // Assert
            Assert.IsTrue(outcome.HasCommand);
            Assert.AreEqual(CommandType.MoveTo, outcome.Command.Type);
            Assert.AreEqual("living", outcome.Command.A);
            Assert.IsFalse(outcome.Command.Append);
            Assert.AreEqual("kettle", outcome.Selection.ApplianceId);
        }

        [Test]
        public void ClickOnNothing_ClearsSelection()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(Held.Appliance("kettle"), new ClickInput { Kind = ClickKind.Nothing });

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.IsTrue(outcome.Selection.IsNone);
        }

        [Test]
        public void CordChipThenSocketThenRoom_PlacesTheCord()
        {
            // Arrange
            var s1 = ClickResolver.Resolve(Held.None, new ClickInput { Kind = ClickKind.TrayCord });

            // Act
            var s2 = ClickResolver.Resolve(s1.Selection, Socket("BR1", room: "bedroom"));
            var s3 = ClickResolver.Resolve(s2.Selection, new ClickInput { Kind = ClickKind.Room, Id = "bathroom", RoomId = "bathroom" });

            // Assert
            Assert.AreEqual(HeldKind.Cord, s1.Selection.Kind);
            Assert.AreEqual(HeldKind.CordFrom, s2.Selection.Kind);
            Assert.AreEqual("BR1", s2.Selection.Id);
            Assert.IsFalse(s2.HasCommand);
            Assert.IsTrue(s3.HasCommand);
            Assert.AreEqual(CommandType.PlaceCord, s3.Command.Type);
            Assert.AreEqual("BR1", s3.Command.A);
            Assert.AreEqual("bathroom", s3.Command.B);
            Assert.IsTrue(s3.Selection.IsNone);
        }

        [Test]
        public void CordFrom_ClickOnApplianceInAnotherRoom_UsesThatRoom()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(Held.CordFrom("L1"), Appliance("kettle", on: false, room: "kitchen"));

            // Assert
            Assert.AreEqual(CommandType.PlaceCord, outcome.Command.Type);
            Assert.AreEqual("kitchen", outcome.Command.B);
        }

        [Test]
        public void RightClick_CordSocket_RemovesTheCord()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(Held.None, Socket("CORD-BR1", occupant: "dryer", right: true, cord: true));

            // Assert
            Assert.AreEqual(CommandType.RemoveCord, outcome.Command.Type);
            Assert.AreEqual("CORD-BR1", outcome.Command.A);
        }

        [Test]
        public void DoorClick_TogglesTheDoorAndKeepsSelection()
        {
            // Arrange + Act
            var outcome = ClickResolver.Resolve(Held.Appliance("lamp1"), new ClickInput { Kind = ClickKind.Door, Id = "D2", Shift = true });

            // Assert
            Assert.AreEqual(CommandType.ToggleDoor, outcome.Command.Type);
            Assert.AreEqual("D2", outcome.Command.A);
            Assert.IsTrue(outcome.Command.Append);
            Assert.AreEqual("lamp1", outcome.Selection.ApplianceId);
        }
    }
}
