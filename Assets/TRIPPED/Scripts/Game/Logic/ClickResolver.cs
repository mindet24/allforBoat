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
        /// <summary>The extension-cord chip in the tray.</summary>
        TrayCord,
        /// <summary>An appliance standing in the house (plugged in).</summary>
        WorldAppliance,
        Socket,
        /// <summary>A room's floor or back wall.</summary>
        Room,
        /// <summary>A door (open or closed).</summary>
        Door,
        /// <summary>The breaker panel in the world (day mode: reset the house).</summary>
        Panel,
    }

    /// <summary>What the player is holding between two clicks.</summary>
    public enum HeldKind
    {
        None,
        /// <summary>An appliance, waiting for a socket.</summary>
        Appliance,
        /// <summary>The extension cord, waiting for the socket it plugs into.</summary>
        Cord,
        /// <summary>The extension cord plugged into a socket, waiting for the room it runs to.</summary>
        CordFrom,
    }

    /// <summary>The current selection: an appliance id, or a cord (with its source socket once chosen).</summary>
    public readonly struct Held
    {
        public readonly HeldKind Kind;
        /// <summary>Appliance id (Appliance) or source socket id (CordFrom). Empty otherwise.</summary>
        public readonly string Id;

        public Held(HeldKind kind, string id = "")
        {
            Kind = kind;
            Id = id ?? "";
        }

        public static readonly Held None = new Held(HeldKind.None);
        public static Held Appliance(string id) => new Held(HeldKind.Appliance, id);
        public static readonly Held Cord = new Held(HeldKind.Cord);
        public static Held CordFrom(string socketId) => new Held(HeldKind.CordFrom, socketId);

        /// <summary>The selected appliance id, or null.</summary>
        public string ApplianceId => Kind == HeldKind.Appliance ? Id : null;

        public bool IsNone => Kind == HeldKind.None;

        public override string ToString() => Kind == HeldKind.None ? "None" : Kind + "(" + Id + ")";
    }

    /// <summary>One click, described with what the player can see (never hidden sim state).</summary>
    public struct ClickInput
    {
        public ClickKind Kind;
        /// <summary>true = right mouse button.</summary>
        public bool Right;
        /// <summary>Shift held: the action joins the end of the character's queue.</summary>
        public bool Shift;
        /// <summary>Appliance id, socket id or door id, by kind.</summary>
        public string Id;
        /// <summary>The room clicked, or the room of the socket or appliance clicked.</summary>
        public string RoomId;
        /// <summary>WorldAppliance: the switch as the player last saw it.</summary>
        public bool SwitchedOn;
        /// <summary>Socket: the appliance the player sees in it. Empty = free.</summary>
        public string OccupantId;
        /// <summary>Socket: the occupant cannot be moved (fridge, routine).</summary>
        public bool OccupantFixed;
        /// <summary>Socket: it is the far end of an extension cord.</summary>
        public bool SocketIsCord;
        /// <summary>The level is a Day: appliances are carried, never switched or unplugged by hand.</summary>
        public bool DayMode;
    }

    /// <summary>The result of a click: maybe one command, and the selection afterwards.</summary>
    public struct ClickOutcome
    {
        public bool HasCommand;
        public Command Command;
        public Held Selection;
    }

    /// <summary>
    /// The click-then-click input rules (GDD v1.1 N13, no drag and drop):
    /// <list type="bullet">
    /// <item>Tray appliance: select it (again = drop). Then click a socket to plug it there (moves it if plugged).</item>
    /// <item>Occupied socket with nothing selected: select its appliance (to move it).</item>
    /// <item>Plugged appliance with nothing (or itself) selected: switch on/off; with another selected: select it.</item>
    /// <item>CORD chip: then click the socket the cord plugs into, then a neighbouring room = PlaceCord.</item>
    /// <item>Door: open/close it. Room (or a free socket with nothing selected): walk there.</item>
    /// <item>Right-click: a cord socket removes the cord; a plugged appliance (or its socket) is unplugged;
    /// anywhere else drops the selection.</item>
    /// </list>
    /// </summary>
    public static class ClickResolver
    {
        public static ClickOutcome Resolve(Held selection, ClickInput click)
        {
            return click.Right ? ResolveRight(click) : ResolveLeft(selection, click);
        }

        private static ClickOutcome ResolveLeft(Held sel, ClickInput click)
        {
            // A cord waiting for its room: any click that names a room finishes it.
            if (sel.Kind == HeldKind.CordFrom)
            {
                if (click.Kind == ClickKind.TrayCord) return Select(Held.None);
                if (!string.IsNullOrEmpty(click.RoomId))
                    return Do(Command.PlaceCord(sel.Id, click.RoomId, click.Shift), Held.None);
                return Select(Held.None);
            }

            switch (click.Kind)
            {
                case ClickKind.TrayAppliance:
                    return Select(sel.ApplianceId == click.Id ? Held.None : Held.Appliance(click.Id));

                case ClickKind.TrayCord:
                    return Select(sel.Kind == HeldKind.Cord ? Held.None : Held.Cord);

                case ClickKind.Panel:
                    // Day mode: the panel resets every room's breaker (one command, the character walks there).
                    return click.DayMode ? Do(Command.ToggleBreaker("", click.Shift), Held.None) : Walk(click, sel);

                case ClickKind.WorldAppliance:
                    if (click.DayMode) return Select(sel.ApplianceId == click.Id ? Held.None : Held.Appliance(click.Id));
                    if (sel.Kind == HeldKind.Cord) return Select(sel);
                    if (sel.IsNone || sel.ApplianceId == click.Id)
                        return Do(Command.SetSwitch(click.Id, !click.SwitchedOn, click.Shift), Held.None);
                    return Select(Held.Appliance(click.Id));

                case ClickKind.Socket:
                {
                    bool occupied = !string.IsNullOrEmpty(click.OccupantId);
                    if (sel.Kind == HeldKind.Cord) return Select(Held.CordFrom(click.Id));
                    if (sel.Kind == HeldKind.Appliance)
                    {
                        if (occupied && click.OccupantId == sel.Id) return Select(Held.None);
                        return Do(Command.Plug(sel.Id, click.Id, click.Shift), Held.None);
                    }
                    if (occupied && !click.OccupantFixed) return Select(Held.Appliance(click.OccupantId));
                    return Walk(click, Held.None);
                }

                case ClickKind.Door:
                    return Do(Command.ToggleDoor(click.Id, click.Shift), sel);

                case ClickKind.Room:
                    return Walk(click, sel);

                default:
                    return Select(Held.None);
            }
        }

        private static ClickOutcome ResolveRight(ClickInput click)
        {
            if (click.DayMode) return Select(Held.None); // nothing is unplugged by hand in a day
            if (click.Kind == ClickKind.WorldAppliance)
                return Do(Command.Unplug(click.Id, click.Shift), Held.None);
            if (click.Kind == ClickKind.Socket && click.SocketIsCord)
                return Do(Command.RemoveCord(click.Id, click.Shift), Held.None);
            if (click.Kind == ClickKind.Socket && !string.IsNullOrEmpty(click.OccupantId))
                return Do(Command.Unplug(click.OccupantId, click.Shift), Held.None);
            return Select(Held.None);
        }

        private static ClickOutcome Walk(ClickInput click, Held keep)
        {
            if (string.IsNullOrEmpty(click.RoomId)) return Select(keep);
            return Do(Command.MoveTo(click.RoomId, click.Shift), keep);
        }

        private static ClickOutcome Select(Held selection) => new ClickOutcome { Selection = selection };

        private static ClickOutcome Do(Command command, Held selection) =>
            new ClickOutcome { HasCommand = true, Command = command, Selection = selection };
    }
}
