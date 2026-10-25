using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Which character the local player plays: the art prefix of its rig (Art/Characters/Rig/{id}_*.png and
    /// {id}_rig.json) and sprites (Art/Characters/{id}_idle.png). Picked in the lobby, kept between sessions.
    /// </summary>
    public static class PlayerCharacter
    {
        /// <summary>The girl (the original player art).</summary>
        public const string Girl = "player";
        /// <summary>The blue-haired boy.</summary>
        public const string Boy = "boy";

        private const string PrefKey = "tripped.character";
        private static string _selected;

        /// <summary>The chosen id (<see cref="Girl"/> until the player picks another).</summary>
        public static string Selected
        {
            get
            {
                if (_selected == null)
                {
                    try { _selected = PlayerPrefs.GetString(PrefKey, Girl); }
                    catch { _selected = Girl; }
                    if (_selected != Girl && _selected != Boy) _selected = Girl;
                }
                return _selected;
            }
            set
            {
                _selected = value == Boy ? Boy : Girl;
                try { PlayerPrefs.SetString(PrefKey, _selected); }
                catch { /* no prefs (tests): keep it for this run */ }
            }
        }
    }
}
