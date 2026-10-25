namespace Trouble.Sim
{
    public enum CommandType
    {
        Plug,
        Unplug,
        SetSwitch,
        ToggleBreaker,
        PlaceCord,
        RemoveCord,
        ToggleDoor,
        /// <summary>Walk to a room and do nothing there.</summary>
        MoveTo,
        /// <summary>Remove one waiting action from the character's queue.</summary>
        CancelAction,
        /// <summary>Day mode, keyboard: hold a walking direction (Index -1 left, 0 stop, 1 right).</summary>
        Walk,
        /// <summary>Day mode, keyboard: hold a depth direction (Index 1 = W, into the room, -1 = S, toward the open front, 0 = stop).</summary>
        WalkDepth,
        /// <summary>Day mode, keyboard (E): reset the panel, put down (or plug in) what is carried, or pick up what is nearest.</summary>
        Interact,
        /// <summary>Day mode, keyboard (F): plug in or unplug the appliance nearest the character.</summary>
        TogglePlug,
        /// <summary>Day mode, keyboard (hold E): throw what is carried ahead, up to <see cref="RulesConfig.throwMetres"/>.</summary>
        Throw,
        /// <summary>Day mode, keyboard, verticalMove "jump": jump to the room straight above (Index 1, Space) or below (-1, Shift+Space).</summary>
        Jump,
        /// <summary>Day mode, keyboard, verticalMove "jetpack": Space held (Index 1) or released (0); the jetpack pushes up while held.</summary>
        Thrust,
        /// <summary>Day mode, keyboard, verticalMove "jetpack" (S+Space / Shift+Space): drop through the floor into the room below.</summary>
        Drop,
    }

    /// <summary>
    /// Everything the player can ask for. The view builds these and passes them to
    /// <see cref="HouseSim.Submit"/>; it never changes sim state itself (D8).
    /// </summary>
    public readonly struct Command
    {
        public readonly CommandType Type;
        /// <summary>Appliance, circuit, source socket, cord socket, door or room, by type.</summary>
        public readonly string A;
        /// <summary>Target socket for Plug, target room for PlaceCord.</summary>
        public readonly string B;
        /// <summary>SetSwitch only.</summary>
        public readonly bool On;
        /// <summary>true = add to the end of the character's queue (Shift+click). false = clear the queue first.</summary>
        public readonly bool Append;
        /// <summary>CancelAction only: position in the queue.</summary>
        public readonly int Index;
        /// <summary>Day mode, co-op: which keyboard player this is for (0 = the first; see <see cref="HouseSim.AddPlayer"/>).</summary>
        public readonly int Player;

        public Command(CommandType type, string a = "", string b = "", bool on = false, bool append = false, int index = 0, int player = 0)
        {
            Type = type;
            A = a ?? "";
            B = b ?? "";
            On = on;
            Append = append;
            Index = index;
            Player = player;
        }

        /// <summary>The same command for keyboard player <paramref name="player"/>.</summary>
        public Command ForPlayer(int player) => new Command(Type, A, B, On, Append, Index, player);

        public static Command Plug(string appliance, string socket, bool append = false) => new Command(CommandType.Plug, appliance, socket, append: append);
        public static Command Unplug(string appliance, bool append = false) => new Command(CommandType.Unplug, appliance, append: append);
        public static Command SetSwitch(string appliance, bool on, bool append = false) => new Command(CommandType.SetSwitch, appliance, on: on, append: append);
        public static Command ToggleBreaker(string circuit, bool append = false) => new Command(CommandType.ToggleBreaker, circuit, append: append);
        public static Command PlaceCord(string sourceSocket, string targetRoom, bool append = false) => new Command(CommandType.PlaceCord, sourceSocket, targetRoom, append: append);
        public static Command RemoveCord(string cordSocket, bool append = false) => new Command(CommandType.RemoveCord, cordSocket, append: append);
        public static Command ToggleDoor(string door, bool append = false) => new Command(CommandType.ToggleDoor, door, append: append);
        public static Command MoveTo(string room, bool append = false) => new Command(CommandType.MoveTo, room, append: append);
        public static Command CancelAction(int index) => new Command(CommandType.CancelAction, index: index);
        public static Command Walk(int direction) => new Command(CommandType.Walk, index: direction);
        public static Command WalkDepth(int direction) => new Command(CommandType.WalkDepth, index: direction);
        public static Command Interact() => new Command(CommandType.Interact);
        public static Command TogglePlug() => new Command(CommandType.TogglePlug);
        public static Command Throw() => new Command(CommandType.Throw);
        public static Command Jump(int direction) => new Command(CommandType.Jump, index: direction);
        public static Command Thrust(bool on) => new Command(CommandType.Thrust, index: on ? 1 : 0);
        public static Command Drop() => new Command(CommandType.Drop);

        public override string ToString() => $"{Type}({A}{(B.Length > 0 ? ", " + B : "")}{(Type == CommandType.SetSwitch ? (On ? ", on" : ", off") : "")})";
    }
}
