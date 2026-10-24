using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
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
