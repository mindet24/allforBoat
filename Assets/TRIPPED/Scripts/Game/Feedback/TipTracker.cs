using System.Collections.Generic;
using Trouble.Sim;

namespace Trouble.Game
{
    /// <summary>
    /// First-time tips (decision Q33): which strings.json tip to show, if any, when a level starts or a sim event
    /// happens. Each tip is shown at most once per session, so restarting a level does not repeat it.
    /// </summary>
    /// <remarks>
    /// Tips: tip_level1_start, tip_level2_start, tip_level3_start (level start), tip_level1_first_trip (first trip
    /// in level 1) and tip_first_reveal (first socket revealed in any level). Pure C#, so it is unit-tested.
    /// </remarks>
    public sealed class TipTracker
    {
        public const string Level1Start = "tip_level1_start";
        public const string Level1FirstTrip = "tip_level1_first_trip";
        public const string Level2Start = "tip_level2_start";
        public const string Level3Start = "tip_level3_start";
        public const string FirstReveal = "tip_first_reveal";

        private readonly HashSet<string> _shown = new HashSet<string>();

        /// <summary>false = no tip is ever returned (F1 panel toggle).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Tip keys already given out this session.</summary>
        public IReadOnlyCollection<string> Shown => _shown;

        /// <summary>The tip for the start of level <paramref name="level"/>, or null.</summary>
        public string OnLevelStart(int level)
        {
            switch (level)
            {
                case 1: return Take(Level1Start);
                case 2: return Take(Level2Start);
                case 3: return Take(Level3Start);
                default: return null;
            }
        }

        /// <summary>The tip this event earns in level <paramref name="level"/>, or null.</summary>
        public string OnEvent(int level, SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.CircuitTripped:
                case SimEventType.MainTripped:
                    return level == 1 ? Take(Level1FirstTrip) : null;
                case SimEventType.SocketRevealed:
                    return Take(FirstReveal);
                default:
                    return null;
            }
        }

        /// <summary>Forgets every tip shown, so each can be shown again.</summary>
        public void Reset() => _shown.Clear();

        private string Take(string key)
        {
            if (!Enabled || _shown.Contains(key)) return null;
            _shown.Add(key);
            return key;
        }
    }
}
