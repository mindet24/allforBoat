using Trouble.Sim;

namespace Trouble.Game
{
    // จุดเดียวที่รู้จักตัวสร้าง Command ถ้า A ใช้ชื่ออื่นให้แก้ที่นี่
    public static class Cmds
    {
        public static Command Plug(string appliance, string socket) => new Command(CommandType.Plug, appliance, socket, false);
        public static Command Unplug(string appliance) => new Command(CommandType.Unplug, appliance, "", false);
        public static Command Switch(string appliance, bool on) => new Command(CommandType.SetSwitch, appliance, "", on);
        public static Command Breaker(string circuit) => new Command(CommandType.ToggleBreaker, circuit, "", false);
        public static Command Cord(string sourceSocket, string room) => new Command(CommandType.PlaceCord, sourceSocket, room, false);
        public static Command RemoveCord(string cordSocket) => new Command(CommandType.RemoveCord, cordSocket, "", false);
    }
}