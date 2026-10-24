using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Places the day-mode request markers of one room in a fixed row near the top of the room, side by side from
    /// the room's right edge leftwards, never overlapping each other or a blocked rect (key prompts, the character's
    /// head and what it carries). Works in any 2D space with y up (the HUD uses canvas pixels).
    /// </summary>
    public static class MarkerLayout
    {
        /// <summary>
        /// Rects for <paramref name="count"/> markers of <paramref name="size"/>, centred on <paramref name="rowY"/>,
        /// between <paramref name="xMin"/> and <paramref name="xMax"/>. A marker that would overlap a blocked rect moves
        /// left past it. When a row is full the next markers start a new row one marker height lower.
        /// </summary>
        public static List<Rect> Row(float xMin, float xMax, float rowY, Vector2 size, float gap, int count, IReadOnlyList<Rect> blocked)
        {
            var placed = new List<Rect>(count);
            float y = rowY;
            float right = xMax;
            for (int i = 0; i < count; i++)
            {
                Rect r = default;
                bool found = false;
                for (int guard = 0; guard < 64 && !found; guard++)
                {
                    r = new Rect(right - size.x, y - size.y * 0.5f, size.x, size.y);
                    bool freshRow = right >= xMax - 0.001f;
                    if (r.xMin < xMin && !freshRow)
                    {
                        // Row full: start the next one lower, at the right edge again.
                        y -= size.y + gap;
                        right = xMax;
                        continue;
                    }
                    var hit = FirstOverlap(r, blocked, placed, gap);
                    if (hit == null) found = true;
                    else right = hit.Value.xMin - gap;
                }
                placed.Add(r);
                right = r.xMin - gap;
            }
            return placed;
        }

        private static Rect? FirstOverlap(Rect r, IReadOnlyList<Rect> blocked, List<Rect> placed, float gap)
        {
            var grown = new Rect(r.x - gap * 0.5f, r.y - gap * 0.5f, r.width + gap, r.height + gap);
            if (blocked != null)
                foreach (var b in blocked)
                    if (grown.Overlaps(b)) return b;
            foreach (var p in placed)
                if (grown.Overlaps(p)) return p;
            return null;
        }
    }
}
