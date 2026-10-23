using Trouble.Sim;

namespace Trouble.Game
{
    /// <summary>What the pointer clicked.</summary>
    public enum ClickKind
    {
        /// <summary>Empty space, or something that does nothing.</summary>
        Nothing,
        /// <summary>An appliance chip in the tray (not plugged in).</summary>
        TrayAppliance,
        /// <summary>An appliance standing in the house (plugged in).</summary>
        WorldAppliance,
        Socket,
        /// <summary>A room's floor or back wall.</summary>
        Room,
    }

    /// <summary>One click, described with what the player can see (never hidden sim state).</summary>
    public struct ClickInput
    {
        public ClickKind Kind;
        /// <summary>true = right mouse button.</summary>
        public bool Right;
        /// <summary>Shift held: the action joins the end of the character's queue.</summary>
        public bool Shift;
        /// <summary>Appliance id (TrayAppliance, WorldAppliance) or socket id (Socket).</summary>
        public string Id;
        /// <summary>The room clicked, or the room of the socket or appliance clicked.</summary>
        public string RoomId;
        /// <summary>WorldAppliance: the switch as the player last saw it.</summary>
        public bool SwitchedOn;
        /// <summary>Socket: the appliance the player sees in it. Empty = free.</summary>
        public string OccupantId;
        /// <summary>Socket: the occupant cannot be moved (fridge, routine).</summary>
        public bool OccupantFixed;
    }

    /// <summary>The result of a click: maybe one command, and the selection afterwards.</summary>
    public struct ClickOutcome
    {
        public bool HasCommand;
        public Command Command;
        /// <summary>Selected appliance after the click. Null = nothing selected.</summary>
        public string Selection;
    }

    /// <summary>
    /// The click-to-plug input rules of G1 (GDD v1.1 N13, no drag and drop):
    /// <list type="bullet">
    /// <item>Click an appliance in the tray to select it (click again to drop the selection).</item>
    /// <item>With an appliance selected, click a socket to plug it there (moves it if it was plugged elsewhere).</item>
    /// <item>With nothing selected, click an occupied socket to select its appliance (to move it).</item>
    /// <item>Click a plugged appliance with nothing selected (or itself selected) to switch it on/off.
    /// With another appliance selected, the click selects this one instead.</item>
    /// <item>Right-click a plugged appliance (or its socket) to unplug it.</item>
    /// <item>Click a room (or a free socket with nothing selected) to walk there.</item>
    /// </list>
    /// </summary>
    public static class ClickResolver
    {
        /// <param name="selection">Currently selected appliance id, or null.</param>
        public static ClickOutcome Resolve(string selection, ClickInput click)
        {
            if (string.IsNullOrEmpty(selection)) selection = null;
            return click.Right ? ResolveRight(selection, click) : ResolveLeft(selection, click);
        }

        private static ClickOutcome ResolveLeft(string selection, ClickInput click)
        {
            switch (click.Kind)
            {
                case ClickKind.TrayAppliance:
                    return Select(selection == click.Id ? null : click.Id);

                case ClickKind.WorldAppliance:
                    if (selection == null || selection == click.Id)
                        return Do(Command.SetSwitch(click.Id, !click.SwitchedOn, click.Shift), null);
                    return Select(click.Id);

                case ClickKind.Socket:
                {
                    bool occupied = !string.IsNullOrEmpty(click.OccupantId);
                    if (selection != null)
                    {
                        // Clicking the socket the selected appliance is already in just drops the selection.
                        if (occupied && click.OccupantId == selection) return Select(null);
                        return Do(Command.Plug(selection, click.Id, click.Shift), null);
                    }
                    if (occupied && !click.OccupantFixed) return Select(click.OccupantId);
                    return WalkOrNothing(click, null);
                }

                case ClickKind.Room:
                    return WalkOrNothing(click, selection);

                default:
                    return Select(null);
            }
        }

        private static ClickOutcome ResolveRight(string selection, ClickInput click)
        {
            if (click.Kind == ClickKind.WorldAppliance)
                return Do(Command.Unplug(click.Id, click.Shift), null);
            if (click.Kind == ClickKind.Socket && !string.IsNullOrEmpty(click.OccupantId))
                return Do(Command.Unplug(click.OccupantId, click.Shift), null);
            return Select(null);
        }

        private static ClickOutcome WalkOrNothing(ClickInput click, string selection)
        {
            if (string.IsNullOrEmpty(click.RoomId)) return Select(selection);
            return Do(Command.MoveTo(click.RoomId, click.Shift), selection);
        }

        private static ClickOutcome Select(string selection) => new ClickOutcome { Selection = selection };

        private static ClickOutcome Do(Command command, string selection) =>
            new ClickOutcome { HasCommand = true, Command = command, Selection = selection };
    }
}
