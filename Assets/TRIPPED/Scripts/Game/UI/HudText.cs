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
            { "menu_levels", "Levels" },
            { "day_hearts", "Hearts" },
            { "day_score", "Score {score}" },
            { "day_time_left", "{time} left" },
            { "day_blackout_banner", "BLACKOUT – reset the breaker in the {panel}" },
            { "day_hint", "A/D walk (onto the stairs from the front) · W/S in / out · E pick up / put down / reset breaker · F plug · right-drag turn camera" },
            { "day_hint_jump", "A/D walk · W/S in / out · Space jump up · Shift+Space jump down · E pick up / put down / reset breaker · F plug · right-drag turn camera" },
            { "day_hint_jetpack", "A/D walk · W/S in / out · hold Space jetpack up · S+Space drop down · E pick up / put down / reset breaker · F plug · right-drag turn camera" },
            { "day_hint_start", "Press SPACE to start the day" },
            { "day_urgent", "URGENT" },
            { "day_normal", "NORMAL" },
            { "day_relaxed", "RELAXED" },
            { "day_card_wait", "start within {time}" },
            { "day_card_running", "running" },
            { "day_card_done", "DONE" },
            { "day_card_missed", "MISSED" },
            { "day_carrying", "carrying {appliance}" },
            { "result_day_won", "DAY COMPLETE!" },
            { "result_day_lost", "OUT OF HEARTS!" },
            { "result_day_stats", "Score {score}   ·   Done {done}   ·   Missed {missed}" },
            { "hud_queue_help", "Queued actions (right-click one to cancel it)" },
            { "alert_save", "{owner}: MY SAVE!" },
            { "hud_cord_selected", "Cord: click the socket it plugs into." },
            { "hud_cord_room", "Cord from {socket}: click the room it runs to (Esc to cancel)." },
            { "hud_selected", "{appliance} selected: click a socket to plug it in (Esc to cancel)." },
            { "result_won", "ALL DONE!" },
            { "result_lost", "TRIPPED!" },
            { "result_stats", "Time {time}   ·   Trips {trips}" },
            { "result_retry", "Retry (R)" },
            { "result_next", "Next" },
            // ---- G6 HUD and menus ----
            { "g6_title", "TRIPPED!" },
            { "g6_tagline", "We R in Trouble" },
            { "g6_play", "Play" },
            { "g6_howto", "How to Play" },
            { "g6_quit", "Quit" },
            { "g6_classic", "Classic levels" },
            { "g6_back", "Back (Esc)" },
            { "g6_best", "Best score  {score}" },
            { "g6_howto_title", "How to Play" },
            { "g6_card1_title", "Carry it over" },
            { "g6_card1_body", "Walk to an appliance and press <b>E</b> to pick it up. Take it to the room on the ticket and press <b>E</b> by a free socket." },
            { "g6_card2_title", "Mind the meter" },
            { "g6_card2_body", "Everything plugged into a room draws power. Keep each room's meter out of the red: 3 seconds over and it trips!" },
            { "g6_card3_title", "Blackout!" },
            { "g6_card3_body", "A trip blacks out the <b>whole house</b>. Run down to the breaker panel in the basement and press <b>E</b>." },
            { "g6_card4_title", "Three hearts" },
            { "g6_card4_body", "Missing an <color=#E53935><b>urgent</b></color> ticket costs a heart. Lose all three and the day is over." },
            { "g6_keys", "<b>WASD</b> walk (W/S: in and out)   <b>A/D</b> at the front walks up and down the stairs   <b>E</b> pick up / put down (hold: throw)   <b>F</b> plug   <b>Tab</b> overview   <b>Right-drag</b> turn camera   <b>Middle-click</b> reset camera   <b>Space</b> hop   <b>P</b> / <b>Esc</b> pause" },
            { "g6_keys_jump", "<b>WASD</b> walk (W/S: in and out)   <b>Space</b> jump up   <b>Shift+Space</b> jump down   <b>E</b> pick up / put down (hold: throw)   <b>F</b> plug   <b>Tab</b> overview   <b>Right-drag</b> turn camera   <b>Middle-click</b> reset camera   <b>P</b> / <b>Esc</b> pause" },
            { "g6_keys_jetpack", "<b>WASD</b> walk (W/S: in and out)   <b>Space</b> (hold) jetpack up   <b>S+Space</b> / <b>Shift+Space</b> drop through the floor   <b>E</b> pick up / put down (hold: throw)   <b>F</b> plug   <b>Tab</b> overview   <b>Right-drag</b> turn camera   <b>Middle-click</b> reset camera   <b>P</b> / <b>Esc</b> pause" },
            { "g6_keys_hud", "<color=#FFB300><b>WASD</b></color> walk  ·  <color=#FFB300><b>A/D</b></color> at the front: stairs  ·  <color=#FFB300><b>E</b></color> carry (hold: throw)  ·  <color=#FFB300><b>F</b></color> plug  ·  <color=#FFB300><b>Tab</b></color> overview" },
            { "g6_keys_hud_jump", "<color=#FFB300><b>WASD</b></color> walk  ·  <color=#FFB300><b>Space</b></color> jump (<color=#FFB300><b>Shift</b></color> down)  ·  <color=#FFB300><b>E</b></color> carry (hold: throw)  ·  <color=#FFB300><b>F</b></color> plug  ·  <color=#FFB300><b>Tab</b></color> overview" },
            { "g6_keys_hud_jetpack", "<color=#FFB300><b>WASD</b></color> walk  ·  <color=#FFB300><b>Space</b></color> fly  ·  <color=#FFB300><b>S+Space</b></color> drop  ·  <color=#FFB300><b>E</b></color> carry (hold: throw)  ·  <color=#FFB300><b>F</b></color> plug  ·  <color=#FFB300><b>Tab</b></color> overview" },
            { "g6_overview", "OVERVIEW" },
            { "g6_start_title", "One Busy Day" },
            { "g6_start_body", "Everyone wants power, right now." },
            { "g6_start_key", "Press <b>SPACE</b> or <b>ENTER</b> to start" },
            { "g6_hands_free", "Hands free" },
            { "g6_holding", "HOLDING" },
            { "g6_you_are_in", "YOU ARE IN" },
            { "g6_off", "OFF" },
            { "g6_overload", "Overload! {s}" },
            { "g6_blackout", "BLACKOUT!" },
            { "g6_blackout_sub", "Get to the breaker" },
            { "g6_breaker", "BREAKER" },
            { "g6_paused", "Paused" },
            { "g6_resume", "Resume" },
            { "g6_restart", "Restart" },
            { "g6_main_menu", "Main Menu" },
            { "g6_retry", "Retry" },
            { "g6_levels", "Levels" },
            { "g6_win", "Day complete!" },
            { "g6_lose", "Out of hearts!" },
            { "g6_done", "Done" },
            { "g6_missed", "Missed" },
            { "g6_new_best", "NEW BEST!" },
            { "g6_choose_level", "Classic levels" },
            { "g6_menu_keys", "Enter  select   ·   arrows  move" },
            { "g6_pause_hint", "Esc / Space  resume" },
            // ---- G6 restyle (art reference) ----
            { "g7_tagline", "Power • People • Priorities" },
            { "g7_settings", "Settings" },
            { "g7_credits", "Credits" },
            { "g7_note_top", "A little chaos...\nmakes a home\nbrighter." },
            { "g7_note_bottom", "Same house.\nDifferent days.\nMore power." },
            { "g7_master", "Master volume" },
            { "g7_music", "Music" },
            { "g7_sfx", "Sound effects" },
            { "g7_ui", "Interface" },
            { "g7_voice", "Voices" },
            { "g7_fullscreen", "Fullscreen" },
            { "g7_resolution", "Resolution" },
            { "g7_shake", "Camera shake" },
            { "g7_on", "ON" },
            { "g7_off", "OFF" },
            { "g7_settings_keys", "Up/Down choose  ·  Left/Right change  ·  Enter toggle  ·  Esc back" },
            { "g7_team", "ทีมส้มปี๊ดจี๊ดจ๊าด" },
            { "g7_role_code", "Team member" },
            { "g7_name_code", "โบ๊ท" },
            { "g7_role_art", "Team member" },
            { "g7_name_art", "ภู" },
            { "g7_role_test", "Team member" },
            { "g7_name_test", "ไข่มุก" },
            { "g7_made_with", "Made with Unity" },
            { "g7_third_party", "Third-party assets" },
            { "g7_need", "Need a {appliance}!" },
            { "g7_in_room", "in the {room}" },
            { "g7_room_power", "Room power" },
            { "g7_overload", "Overload!" },
            { "g7_score", "Score" },
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
