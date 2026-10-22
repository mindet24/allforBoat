using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Trouble.Sim
{
    /// <summary>
    /// Every tunable rule number and switch. Mirrors Resources/Config/rules.json.
    /// Field names are the keys used by rules.json and by a level's rulesOverride.
    /// Defaults are GDD v1.1 values.
    /// </summary>
    [Serializable]
    public sealed class RulesConfig
    {
        public int ticksPerSecond = 10;

        /// <summary>Overload counter value at which a breaker trips (R3).</summary>
        public int overTripTicks = 10;
        /// <summary>The counter drops by 1 every this many ticks without overload. 0 or less = drops to 0 at once (v1.0 rule).</summary>
        public int overCoolEvery = 3;
        /// <summary>true = flipping a breaker sets its counter to 0 (v1.0 rule).</summary>
        public bool breakerToggleResetsCounter;

        /// <summary>Metres per second.</summary>
        public float walkSpeed = 3f;
        /// <summary>Ticks an action takes once the character is in the right room; it happens on the last one. 0 = at once.</summary>
        public int interactTicks = 3;
        /// <summary>Extra ticks to walk through a closed door.</summary>
        public int doorOpenTicks = 5;
        /// <summary>Extra ticks to use stairs.</summary>
        public int stairsTicks = 20;
        public int actionQueueSize = 3;
        /// <summary>true = during planning every action except breakers happens at once, with no walking.</summary>
        public bool planningIsFree = true;

        /// <summary>How many open doors deep the player can see from the character's room.</summary>
        public int sightDepth = 1;
        public bool sightThroughStairs;
        /// <summary>true = a socket is revealed only while its room is visible (R16′). false = v1.0 R16.</summary>
        public bool revealRequiresSight = true;
        public bool cordThroughStairs;

        /// <summary>"Always" or "AtPanelOnly". Read by the view.</summary>
        public string breakerHudMode = "Always";

        /// <summary>Width in metres for rooms whose level data gives none.</summary>
        public float autoRoomWidth = 6f;

        public int SecondsToTicks(float seconds)
        {
            return (int)Math.Round(seconds * ticksPerSecond, MidpointRounding.AwayFromZero);
        }

        public RulesConfig Clone()
        {
            return (RulesConfig)MemberwiseClone();
        }

        /// <summary>
        /// Returns a copy with the given overrides applied. Unknown keys and unreadable values
        /// are reported in <paramref name="errors"/> and skipped.
        /// </summary>
        public RulesConfig WithOverrides(IReadOnlyList<RuleOverrideData> overrides, List<string> errors)
        {
            var copy = Clone();
            if (overrides == null) return copy;
            foreach (var o in overrides)
            {
                var field = typeof(RulesConfig).GetField(o.key, BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                {
                    errors?.Add($"Unknown rule '{o.key}'.");
                    continue;
                }
                try
                {
                    var value = Convert.ChangeType(o.value, field.FieldType, CultureInfo.InvariantCulture);
                    field.SetValue(copy, value);
                }
                catch (Exception)
                {
                    errors?.Add($"Rule '{o.key}' cannot be '{o.value}' (expects {field.FieldType.Name}).");
                }
            }
            return copy;
        }
    }
}
