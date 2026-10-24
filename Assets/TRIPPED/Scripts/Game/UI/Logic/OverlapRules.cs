using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Keeps the day-mode key prompts apart: when the stairs prompt ("W ↑" / "S ↓") would touch the "E" prompt, it moves
    /// sideways to the side of the character away from E, leaving <c>gap</c> between them. World metres, y up.
    /// </summary>
    public static class PromptLayout
    {
        /// <summary>
        /// Centre for a stairs prompt of <paramref name="stairsSize"/> wanted at <paramref name="stairsCentre"/>, kept
        /// clear of the E prompt rect <paramref name="e"/> (null = no E prompt shown).
        /// </summary>
        public static Vector2 StairsCentre(Vector2 stairsCentre, Vector2 stairsSize, Rect? e, float characterX, float gap)
        {
            if (!e.HasValue) return stairsCentre;
            var er = e.Value;
            var want = new Rect(stairsCentre - stairsSize * 0.5f, stairsSize);
            var grown = new Rect(er.xMin - gap, er.yMin - gap, er.width + 2f * gap, er.height + 2f * gap);
            if (!grown.Overlaps(want)) return stairsCentre;
            // E sits right of (or on) the character: put the stairs prompt to the left, and the other way round.
            bool left = er.center.x >= characterX;
            float x = left
                ? Mathf.Min(stairsCentre.x, er.xMin - gap - stairsSize.x * 0.5f)
                : Mathf.Max(stairsCentre.x, er.xMax + gap + stairsSize.x * 0.5f);
            return new Vector2(x, stairsCentre.y);
        }
    }

    /// <summary>Which world-pinned room meters to draw while the camera follows the character.</summary>
    public static class MeterVisibility
    {
        /// <summary>
        /// Overview (not following): every meter. Following: only the character's room and rooms fully on screen
        /// (<paramref name="roomViewport"/> inside 0..1), and never a meter whose canvas rect touches a HUD panel.
        /// </summary>
        public static bool Show(bool following, bool isCurrentRoom, Rect roomViewport, Rect meterRect, IReadOnlyList<Rect> hudRects)
        {
            if (!following) return true;
            const float tolerance = 0.01f;
            bool fullyOnScreen = roomViewport.xMin >= -tolerance && roomViewport.yMin >= -tolerance
                                 && roomViewport.xMax <= 1f + tolerance && roomViewport.yMax <= 1f + tolerance;
            if (!isCurrentRoom && !fullyOnScreen) return false;
            if (hudRects != null)
                foreach (var r in hudRects)
                    if (r.Overlaps(meterRect)) return false;
            return true;
        }
    }
}
