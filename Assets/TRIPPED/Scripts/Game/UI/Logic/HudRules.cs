using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>Where an order ticket is in its life.</summary>
    public enum TicketPhase { Waiting, Running, Done, Missed }

    /// <summary>
    /// Pure rules of the order tickets (top-left, Overcooked style): stacking, slide-in, shake, pop and timer colour.
    /// Canvas pixels, x right, y down from the top-left of the screen.
    /// </summary>
    public static class TicketRules
    {
        /// <summary>The phase of a request from its flags.</summary>
        public static TicketPhase Phase(bool done, bool missed, int progressTicks)
        {
            if (done) return TicketPhase.Done;
            if (missed) return TicketPhase.Missed;
            return progressTicks > 0 ? TicketPhase.Running : TicketPhase.Waiting;
        }

        /// <summary>
        /// Top-left corner of ticket slot <paramref name="slot"/>: left to right from <paramref name="origin"/>, a new row
        /// below after <paramref name="perRow"/> tickets.
        /// </summary>
        public static Vector2 SlotPosition(int slot, Vector2 origin, Vector2 size, float gap, int perRow)
        {
            int row = perRow > 0 ? slot / perRow : 0;
            int col = perRow > 0 ? slot % perRow : slot;
            return new Vector2(origin.x + col * (size.x + gap), origin.y + row * (size.y + gap));
        }

        /// <summary>How far a new ticket still is from its slot, 1 = just born (one slide distance up), 0 = arrived. Ease-out.</summary>
        public static float SlideOffset(float age, float duration)
        {
            if (duration <= 0f) return 0f;
            float t = Mathf.Clamp01(age / duration);
            float inv = 1f - t;
            return inv * inv * inv;
        }

        /// <summary>
        /// Horizontal shake of a waiting ticket with little time left: 0 above the threshold, then a sine whose amplitude
        /// grows as the time runs out. Running and finished tickets never shake.
        /// </summary>
        public static float ShakeOffset(TicketPhase phase, float secondsLeft, float threshold, float time, float amplitude, float frequency)
        {
            if (phase != TicketPhase.Waiting || threshold <= 0f || secondsLeft > threshold || secondsLeft < 0f) return 0f;
            float strength = 0.4f + 0.6f * (1f - secondsLeft / threshold);
            return Mathf.Sin(time * frequency) * amplitude * strength;
        }

        /// <summary>Scale of a finished ticket as it leaves: Done pops up then shrinks; Missed shrinks a little.</summary>
        public static float ExitScale(TicketPhase phase, float sinceEnd, float duration, float popScale)
        {
            if (duration <= 0f) return 1f;
            float t = Mathf.Clamp01(sinceEnd / duration);
            if (phase == TicketPhase.Done)
                return t < 0.35f ? Mathf.Lerp(1f, popScale, t / 0.35f) : Mathf.Lerp(popScale, 0.4f, (t - 0.35f) / 0.65f);
            if (phase == TicketPhase.Missed) return Mathf.Lerp(1f, 0.85f, t);
            return 1f;
        }

        /// <summary>Opacity of a finished ticket as it leaves (1 → 0 over the last 60%).</summary>
        public static float ExitAlpha(float sinceEnd, float duration)
        {
            if (duration <= 0f) return 0f;
            float t = Mathf.Clamp01(sinceEnd / duration);
            return t < 0.4f ? 1f : 1f - (t - 0.4f) / 0.6f;
        }

        /// <summary>Band of a waiting timer bar: 0 green, 1 amber, 2 red.</summary>
        public static int TimerBand(float fraction, float greenAbove, float amberAbove)
        {
            if (fraction > greenAbove) return 0;
            if (fraction > amberAbove) return 1;
            return 2;
        }
    }

    /// <summary>What a segmented room power meter shows.</summary>
    public struct MeterState
    {
        /// <summary>Segments drawn (the capacity, capped).</summary>
        public int Segments;
        /// <summary>Segments lit.</summary>
        public int Filled;
        public LoadBand Band;
        /// <summary>Load above capacity.</summary>
        public bool Over;
    }

    /// <summary>Pure rules of the per-room power meters ("Kitchen ■■■■■□□ 5/7").</summary>
    public static class MeterRules
    {
        /// <summary>
        /// Segments and lit segments for a load and capacity. Up to <paramref name="maxSegments"/> each segment is one
        /// load point; a bigger capacity is scaled down to that many segments. Over capacity every segment is lit.
        /// </summary>
        public static MeterState Compute(int load, int capacity, float amberAbove, float redAbove, int maxSegments)
        {
            int cap = Mathf.Max(1, capacity);
            int segments = Mathf.Clamp(cap, 1, Mathf.Max(1, maxSegments));
            int clampedLoad = Mathf.Clamp(load, 0, cap);
            int filled = segments == cap ? clampedLoad : Mathf.CeilToInt((float)clampedLoad * segments / cap - 1e-4f);
            return new MeterState
            {
                Segments = segments,
                Filled = Mathf.Clamp(filled, 0, segments),
                Band = HudFormat.Band(load, cap, amberAbove, redAbove),
                Over = load > cap,
            };
        }

        /// <summary>The meter as text, for checks and logs: "■■■■■□□ 5/7".</summary>
        public static string Text(int load, int capacity, int maxSegments)
        {
            var m = Compute(load, capacity, 0.7f, 1f, maxSegments);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < m.Segments; i++) sb.Append(i < m.Filled ? '■' : '□');
            return sb + " " + load + "/" + capacity;
        }

        /// <summary>Seconds until an overloaded room trips, one decimal: "2.1s".</summary>
        public static string OverloadSeconds(int overTicks, int overTripTicks, int ticksPerSecond)
        {
            float s = HudFormat.SecondsToTrip(overTicks, overTripTicks, ticksPerSecond);
            return s.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
        }
    }

    /// <summary>Stars at the end of the day.</summary>
    public static class StarRules
    {
        /// <summary>0..3 stars: one per threshold reached (thresholds ascending). A lost day earns none.</summary>
        public static int Stars(int score, IReadOnlyList<int> thresholds, bool won)
        {
            if (!won || thresholds == null) return 0;
            int stars = 0;
            for (int i = 0; i < thresholds.Count && i < 3; i++)
                if (score >= thresholds[i]) stars++;
            return stars;
        }
    }

    /// <summary>Keyboard focus moving through a menu's buttons.</summary>
    public static class MenuNavRules
    {
        /// <summary>The next focused index after moving <paramref name="delta"/>, wrapping around. -1 for an empty menu.</summary>
        public static int Step(int index, int delta, int count)
        {
            if (count <= 0) return -1;
            if (index < 0) return delta < 0 ? count - 1 : 0;
            return ((index + delta) % count + count) % count;
        }
    }

    /// <summary>One placed off-screen arrow (or an on-screen marker).</summary>
    public struct EdgeSlot
    {
        /// <summary>The point is inside the safe area: draw it in place, no arrow.</summary>
        public bool OnScreen;
        /// <summary>Where to draw, canvas pixels (y up from the bottom).</summary>
        public Vector2 Position;
        /// <summary>Arrow direction in degrees (0 = right, 90 = up).</summary>
        public float Angle;
    }

    /// <summary>
    /// Places edge arrows for things outside the visible part of the screen: each one on the border of the safe area
    /// in the direction of its target, and arrows on the same edge pushed apart so they never overlap.
    /// </summary>
    public static class EdgeArrowLayout
    {
        private enum Side { Left, Right, Bottom, Top }

        /// <summary>
        /// Fills <paramref name="into"/> with one slot per point (same order). Points and the safe area are canvas
        /// pixels with y up. Arrows on one side keep <paramref name="spacing"/> pixels apart along that side.
        /// </summary>
        public static void Place(IReadOnlyList<Vector2> points, Rect safe, float spacing, List<EdgeSlot> into)
        {
            into.Clear();
            var sides = new List<Side>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (safe.Contains(p))
                {
                    into.Add(new EdgeSlot { OnScreen = true, Position = p });
                    sides.Add(Side.Left);
                    continue;
                }
                var c = safe.center;
                var d = p - c;
                float tx = Mathf.Abs(d.x) > 1e-5f ? safe.width * 0.5f / Mathf.Abs(d.x) : float.MaxValue;
                float ty = Mathf.Abs(d.y) > 1e-5f ? safe.height * 0.5f / Mathf.Abs(d.y) : float.MaxValue;
                var edge = c + d * Mathf.Min(tx, ty);
                Side side = tx <= ty ? (d.x < 0 ? Side.Left : Side.Right) : (d.y < 0 ? Side.Bottom : Side.Top);
                into.Add(new EdgeSlot { OnScreen = false, Position = edge, Angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg });
                sides.Add(side);
            }

            // Spread arrows on each side: sort along the side, push each one past the previous, then pull the group
            // back inside the side if it ran past the end.
            for (int s = 0; s < 4; s++)
            {
                var side = (Side)s;
                bool vertical = side == Side.Left || side == Side.Right;
                var idx = new List<int>();
                for (int i = 0; i < into.Count; i++)
                    if (!into[i].OnScreen && sides[i] == side) idx.Add(i);
                if (idx.Count < 2) continue;
                idx.Sort((a, b) => Along(into[a].Position, vertical).CompareTo(Along(into[b].Position, vertical)));
                float min = vertical ? safe.yMin : safe.xMin;
                float max = vertical ? safe.yMax : safe.xMax;
                var along = new float[idx.Count];
                for (int k = 0; k < idx.Count; k++)
                {
                    along[k] = Along(into[idx[k]].Position, vertical);
                    if (k > 0 && along[k] < along[k - 1] + spacing) along[k] = along[k - 1] + spacing;
                }
                float overflow = along[idx.Count - 1] - max;
                if (overflow > 0f)
                    for (int k = 0; k < idx.Count; k++) along[k] = Mathf.Max(min, along[k] - overflow);
                for (int k = 0; k < idx.Count; k++)
                {
                    var slot = into[idx[k]];
                    slot.Position = vertical ? new Vector2(slot.Position.x, along[k]) : new Vector2(along[k], slot.Position.y);
                    into[idx[k]] = slot;
                }
            }
        }

        private static float Along(Vector2 p, bool vertical) => vertical ? p.y : p.x;
    }
}
