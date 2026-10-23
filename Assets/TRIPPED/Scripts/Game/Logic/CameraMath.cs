using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Pure math for a perspective camera that looks straight down +z at the house front (GDD v1.1 §2.6).
    /// "Distance" is from the camera to the focus plane; positions are the camera's x, y.
    /// </summary>
    public static class CameraMath
    {
        /// <summary>World width and height seen on the focus plane at this distance.</summary>
        public static Vector2 VisibleSize(float distance, float verticalFovDeg, float aspect)
        {
            float h = 2f * distance * Mathf.Tan(verticalFovDeg * 0.5f * Mathf.Deg2Rad);
            return new Vector2(h * aspect, h);
        }

        /// <summary>
        /// Distance at which a box of <paramref name="size"/> plus <paramref name="margin"/> (0.1 = 10%) fits inside
        /// <paramref name="viewport"/> (the part of the screen the HUD leaves free, 0..1 units).
        /// </summary>
        public static float FitDistance(Vector2 size, float verticalFovDeg, float aspect, Rect viewport, float margin)
        {
            float tan = Mathf.Tan(verticalFovDeg * 0.5f * Mathf.Deg2Rad);
            float vw = Mathf.Max(0.05f, viewport.width);
            float vh = Mathf.Max(0.05f, viewport.height);
            float byWidth = size.x * (1f + margin) / (2f * tan * aspect * vw);
            float byHeight = size.y * (1f + margin) / (2f * tan * vh);
            return Mathf.Max(byWidth, byHeight, 0.01f);
        }

        /// <summary>Distance at which <paramref name="width"/> metres fill <paramref name="screenFraction"/> of the screen width.</summary>
        public static float DistanceForWidth(float width, float screenFraction, float verticalFovDeg, float aspect)
        {
            float tan = Mathf.Tan(verticalFovDeg * 0.5f * Mathf.Deg2Rad);
            return width / (2f * tan * aspect * Mathf.Max(0.01f, screenFraction));
        }

        /// <summary>Camera x, y that puts <paramref name="target"/> at the centre of <paramref name="viewport"/>.</summary>
        public static Vector2 CenterOn(Vector2 target, float distance, float verticalFovDeg, float aspect, Rect viewport)
        {
            var visible = VisibleSize(distance, verticalFovDeg, aspect);
            var offset = new Vector2((viewport.center.x - 0.5f) * visible.x, (viewport.center.y - 0.5f) * visible.y);
            return target - offset;
        }

        /// <summary>The point on the focus plane under a viewport position (0..1, origin bottom-left).</summary>
        public static Vector2 ViewportToPlane(Vector2 camera, float distance, float verticalFovDeg, float aspect, Vector2 viewportPoint)
        {
            var visible = VisibleSize(distance, verticalFovDeg, aspect);
            return camera + new Vector2((viewportPoint.x - 0.5f) * visible.x, (viewportPoint.y - 0.5f) * visible.y);
        }

        /// <summary>
        /// Camera x, y after changing distance so that <paramref name="point"/> (on the focus plane) stays under
        /// the same spot on screen — zoom toward the cursor.
        /// </summary>
        public static Vector2 ZoomToward(Vector2 camera, float oldDistance, float newDistance, Vector2 point)
        {
            if (oldDistance <= 0f) return camera;
            return point - (point - camera) * (newDistance / oldDistance);
        }

        /// <summary>Clamps a camera position into a rect.</summary>
        public static Vector2 Clamp(Vector2 camera, Rect limits)
        {
            return new Vector2(Mathf.Clamp(camera.x, limits.xMin, limits.xMax), Mathf.Clamp(camera.y, limits.yMin, limits.yMax));
        }

        /// <summary>Smallest rect holding both a rect and a point.</summary>
        public static Rect Encapsulate(Rect rect, Vector2 point)
        {
            return Rect.MinMaxRect(Mathf.Min(rect.xMin, point.x), Mathf.Min(rect.yMin, point.y),
                Mathf.Max(rect.xMax, point.x), Mathf.Max(rect.yMax, point.y));
        }

        // ---- view-height form: works for perspective and orthographic alike ----------------------------------
        // "View height" = world height seen on the focus plane. Orthographic: 2 × orthographicSize.
        // Perspective: 2 × distance × tan(fov/2). Zoom and pan math only needs this one number.

        /// <summary>World width and height seen for a view height.</summary>
        public static Vector2 VisibleSizeForHeight(float viewHeight, float aspect) => new Vector2(viewHeight * aspect, viewHeight);

        /// <summary>
        /// View height at which a box of <paramref name="size"/> plus <paramref name="margin"/> fits inside
        /// <paramref name="viewport"/> (0..1 units).
        /// </summary>
        public static float FitViewHeight(Vector2 size, float aspect, Rect viewport, float margin)
        {
            float vw = Mathf.Max(0.05f, viewport.width);
            float vh = Mathf.Max(0.05f, viewport.height);
            return Mathf.Max(size.x * (1f + margin) / (aspect * vw), size.y * (1f + margin) / vh, 0.01f);
        }

        /// <summary>View height at which <paramref name="width"/> metres fill <paramref name="screenFraction"/> of the screen width.</summary>
        public static float ViewHeightForWidth(float width, float screenFraction, float aspect)
        {
            return width / (Mathf.Max(0.01f, aspect) * Mathf.Max(0.01f, screenFraction));
        }

        /// <summary>Perspective camera distance that shows this view height on the focus plane.</summary>
        public static float DistanceForViewHeight(float viewHeight, float verticalFovDeg)
        {
            return viewHeight / (2f * Mathf.Tan(verticalFovDeg * 0.5f * Mathf.Deg2Rad));
        }

        /// <summary>Camera x, y that puts <paramref name="target"/> at the centre of <paramref name="viewport"/>.</summary>
        public static Vector2 CenterOnForHeight(Vector2 target, float viewHeight, float aspect, Rect viewport)
        {
            var visible = VisibleSizeForHeight(viewHeight, aspect);
            return target - new Vector2((viewport.center.x - 0.5f) * visible.x, (viewport.center.y - 0.5f) * visible.y);
        }

        /// <summary>The point on the focus plane under a viewport position (0..1, origin bottom-left).</summary>
        public static Vector2 ViewportToPlaneForHeight(Vector2 camera, float viewHeight, float aspect, Vector2 viewportPoint)
        {
            var visible = VisibleSizeForHeight(viewHeight, aspect);
            return camera + new Vector2((viewportPoint.x - 0.5f) * visible.x, (viewportPoint.y - 0.5f) * visible.y);
        }

        /// <summary>The viewport position (0..1, may be outside) of a point on the focus plane.</summary>
        public static Vector2 PlaneToViewportForHeight(Vector2 camera, float viewHeight, float aspect, Vector2 point)
        {
            var visible = VisibleSizeForHeight(viewHeight, aspect);
            return new Vector2((point.x - camera.x) / visible.x + 0.5f, (point.y - camera.y) / visible.y + 0.5f);
        }
    }
}
