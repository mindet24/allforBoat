using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Pure framing of the day-mode follow camera: how much of the house to show around the character's room, where
    /// to put the camera so the character sits at a chosen screen point, and how to keep the view inside the house.
    /// World metres on the house front, y up. "View height" is the world height on screen (see <see cref="CameraMath"/>).
    /// </summary>
    public static class FollowFraming
    {
        /// <summary>
        /// View height that shows <paramref name="floors"/> floors of <paramref name="roomHeight"/>, never more than
        /// <paramref name="maxViewHeight"/>.
        /// </summary>
        public static float ViewHeight(float roomHeight, float floors, float maxViewHeight)
        {
            float h = roomHeight * floors;
            return maxViewHeight > 0f ? Mathf.Min(h, maxViewHeight) : h;
        }

        /// <summary>
        /// The x the view centres on: the character's x pulled toward the centre of its room by
        /// <paramref name="roomLock"/> (0 = follow the character exactly, 1 = keep the room centred).
        /// </summary>
        public static float FocusX(float characterX, float roomXMin, float roomXMax, float roomLock)
        {
            float centre = (roomXMin + roomXMax) * 0.5f;
            return Mathf.Lerp(characterX, centre, Mathf.Clamp01(roomLock));
        }

        /// <summary>Camera centre that puts <paramref name="focus"/> at <paramref name="screenAnchor"/> (viewport 0..1).</summary>
        public static Vector2 Center(Vector2 focus, float viewHeight, float aspect, Vector2 screenAnchor)
        {
            var visible = new Vector2(viewHeight * aspect, viewHeight);
            return focus - new Vector2((screenAnchor.x - 0.5f) * visible.x, (screenAnchor.y - 0.5f) * visible.y);
        }

        /// <summary>
        /// Keeps the view inside <paramref name="bounds"/> on each axis; on an axis where the view is larger than the
        /// bounds, centres it on them.
        /// </summary>
        public static Vector2 ClampToBounds(Vector2 center, float viewHeight, float aspect, Rect bounds)
        {
            float halfW = viewHeight * aspect * 0.5f;
            float halfH = viewHeight * 0.5f;
            float x = bounds.width <= 2f * halfW ? bounds.center.x : Mathf.Clamp(center.x, bounds.xMin + halfW, bounds.xMax - halfW);
            float y = bounds.height <= 2f * halfH ? bounds.center.y : Mathf.Clamp(center.y, bounds.yMin + halfH, bounds.yMax - halfH);
            return new Vector2(x, y);
        }
    }
}
