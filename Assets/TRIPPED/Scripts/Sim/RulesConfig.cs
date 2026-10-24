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
        /// <summary>
        /// "keep" = after a trip the counter stays full and cools like any other time, so a breaker reset while
        /// still overloaded trips again almost at once (a hot breaker). "reset" = the counter goes to 0 (v1.0 rule).
        /// </summary>
        public string counterAfterTrip = "keep";

        /// <summary>Metres per second.</summary>
        public float walkSpeed = 3f;
        /// <summary>Ticks an action takes once the character is in the right room; it happens on the last one. 0 = at once.</summary>
        public int interactTicks = 3;
        /// <summary>Extra ticks to walk through a closed door.</summary>
        public int doorOpenTicks = 5;
        /// <summary>Extra ticks to use stairs.</summary>
        public int stairsTicks = 20;
        public int actionQueueSize = 3;
        /// <summary>Day mode, keyboard: how close (metres, across the floor) the character must be to a socket, appliance or panel to use it.</summary>
        public float reachMetres = 1.5f;
        /// <summary>Day mode: metres per second walking into the room (W) or toward the open front (S); as quick as walking along.</summary>
        public float depthWalkSpeed = 3f;
        /// <summary>
        /// Day mode: how deep the walkable floor of a room is, metres from the open front. Sockets, the appliances in
        /// them and the panel are reached from this depth (the furniture fills the room behind it).
        /// </summary>
        public float walkDepthMetres = 2f;
        /// <summary>Day mode: a door to the next room is only passable this close to the open front, metres (the doorway).</summary>
        public float doorDepthMetres = 1f;
        /// <summary>
        /// Day mode: how the character changes floor. "stairs" (default): walk onto a flight of stairs with A/D from the
        /// front of the room and up or down it. "jump": Space jumps to the room straight above, Shift+Space to the one
        /// below, in <see cref="jumpTicks"/>. "jetpack": Space held flies up, release falls, S/Shift+Space drops through
        /// the floor (the jetpack values below).
        /// </summary>
        public string verticalMove = "stairs";
        /// <summary>Day mode, stairs: length of a flight along the floor, metres, unless the level gives its own.</summary>
        public float stairsRunMetres = 3f;
        /// <summary>
        /// Day mode, stairs: the flights stand at the open front of the house, this deep (metres). Walking along within
        /// this depth steps onto them; deeper in, the character walks past behind them.
        /// </summary>
        public float stairsDepthMetres = 1f;
        /// <summary>Day mode, stairs: metres per second along the floor while on the stairs.</summary>
        public float stairsSpeed = 2.2f;
        /// <summary>Day mode, verticalMove "jump": ticks a jump to the floor above or below takes; it lands on the last.</summary>
        public int jumpTicks = 6;
        /// <summary>Day mode: height of one floor, metres (floor to floor; the view's roomHeight matches it).</summary>
        public float floorHeightMetres = 3f;
        /// <summary>Day mode: upward push of the jetpack while Space is held, metres per second squared.</summary>
        public float jetpackThrust = 35f;
        /// <summary>Day mode: thrust lost per weight point of the carried appliance (catalog "weight"), m/s².</summary>
        public float jetpackWeightThrust = 4f;
        /// <summary>Day mode: pull down, metres per second squared.</summary>
        public float gravity = 20f;
        /// <summary>Day mode: fastest climb with the jetpack, metres per second.</summary>
        public float jetpackMaxRise = 6f;
        /// <summary>Day mode: fastest fall, metres per second.</summary>
        public float maxFallSpeed = 9f;
        /// <summary>Day mode: under a ceiling with no room above, the feet stop this far below it (the character's height), metres.</summary>
        public float headroomMetres = 1.7f;
        /// <summary>
        /// Day mode: how much depth (in and out of the room) counts toward reach, against 1 for distance along the room.
        /// Below 1 sockets and appliances at the back are in reach from nearer the front.
        /// </summary>
        public float reachDepthWeight = 0.5f;
        /// <summary>
        /// Day mode, keyboard: true = E puts what is carried down unplugged and F plugs it in. false = E plugs it in at
        /// once (the first day-mode rule).
        /// </summary>
        public bool placeUnplugged = true;
        /// <summary>Day mode: "socket" = E puts down beside the nearest free socket in reach; "floor" = where the character stands.</summary>
        public string placeMode = "socket";
        /// <summary>Day mode: how far ahead (metres) a held-E throw can land.</summary>
        public float throwMetres = 4f;
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
