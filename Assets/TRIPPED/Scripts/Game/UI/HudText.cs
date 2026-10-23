using System.Globalization;
using Trouble.Sim;

namespace Trouble.Game
{
    /// <summary>
    /// HUD text looked up in strings.json by key, with an English fallback while the key is not in the file yet.
    /// Keys used by the HUD start with "hud_" or "result_" (listed in <see cref="Keys"/>) so they can be added to
    /// strings.json without touching code.
    /// </summary>
    public sealed class HudText
    {
        /// <summary>Every key the HUD asks for, with its fallback text.</summary>
        public static readonly string[,] Keys =
        {
            { "hud_level_title", "L{n}  {title}" },
            { "hud_start", "Start (Space)" },
            { "hud_pause", "Pause (Space)" },
            { "hud_resume", "Resume (Space)" },
            { "hud_restart", "Restart (R)" },
            { "hud_on", "ON" },
            { "hud_off", "OFF" },
            { "hud_main", "MAIN" },
            { "hud_main_label", "WHOLE HOUSE" },
            { "hud_overload", "OVERLOAD! trips in {s} s" },
            { "hud_hot", "HOT: still cooling" },
            { "hud_load_unknown", "? / {cap}" },
            { "hud_request_line", "{appliance} in {room}" },
            { "hud_done", "DONE" },
            { "hud_unseen", "not seen" },
            { "hud_cords", "CORD ×{n}" },
            { "hud_tray_help", "Click an appliance, then a socket.  Click a plugged appliance: on/off.  Right-click: unplug.  Shift: queue." },
            { "hud_selected", "{appliance} selected: click a socket to plug it in (Esc to cancel)." },
            { "result_won", "ALL DONE!" },
            { "result_lost", "TRIPPED!" },
            { "result_stats", "Time {time}   ·   Trips {trips}" },
            { "result_retry", "Retry (R)" },
            { "result_next", "Next" },
        };

        private readonly Strings _strings;

        public HudText(Strings strings)
        {
            _strings = strings;
        }

        /// <summary>The text for a key from strings.json, or its fallback.</summary>
        public string Get(string key)
        {
            if (_strings != null)
            {
                string text = _strings.Get(key);
                if (text != key) return text;
            }
            return Fallback(key);
        }

        /// <summary>The text for a key with each {name} replaced.</summary>
        public string Format(string key, params (string name, object value)[] args)
        {
            string text = Get(key);
            foreach (var (name, value) in args)
                text = text.Replace("{" + name + "}", System.Convert.ToString(value, CultureInfo.InvariantCulture));
            return text;
        }

        /// <summary>Strings from strings.json that the sim also uses (e.g. "pause_hint").</summary>
        public string Sim(string key) => _strings != null ? _strings.Get(key) : key;

        private static string Fallback(string key)
        {
            for (int i = 0; i < Keys.GetLength(0); i++)
                if (Keys[i, 0] == key) return Keys[i, 1];
            return key;
        }
    }
}
