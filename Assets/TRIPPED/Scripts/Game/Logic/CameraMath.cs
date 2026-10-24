using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Pure math for the house camera (GDD v1.1 §2.6): straight down +z at the house front, or tilted to look down.
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

        // ---- tilted perspective: the camera orbits the focus point, looks down by tilt degrees and turns by yaw ----
        // At tilt 0 and yaw 0 these match the view-height functions above for points on the focus plane.

        /// <summary>
        /// World position of a perspective camera that shows <paramref name="viewHeight"/> at the focus point
        /// (<paramref name="focus"/>, on the plane z = <paramref name="focusZ"/>) while looking down by
        /// <paramref name="tiltDeg"/> degrees and turned by <paramref name="yawDeg"/> (positive = the camera swings
        /// left and looks right). The camera's rotation is Euler(tiltDeg, yawDeg, 0).
        /// </summary>
        public static Vector3 TiltedCameraPosition(Vector2 focus, float focusZ, float viewHeight, float verticalFovDeg, float tiltDeg, float yawDeg = 0f)
        {
            float d = DistanceForViewHeight(viewHeight, verticalFovDeg);
            return new Vector3(focus.x, focus.y, focusZ) - Quaternion.Euler(tiltDeg, yawDeg, 0f) * Vector3.forward * d;
        }

        /// <summary>Viewport position (0..1, may be outside) of a world point seen by the tilted camera.</summary>
        public static Vector2 WorldToViewportTilted(Vector2 focus, float focusZ, float viewHeight, float verticalFovDeg,
            float tiltDeg, float aspect, Vector3 point, float yawDeg = 0f)
        {
            var cam = TiltedCameraPosition(focus, focusZ, viewHeight, verticalFovDeg, tiltDeg, yawDeg);
            var rotation = Quaternion.Euler(tiltDeg, yawDeg, 0f);
            var v = point - cam;
            float z = Mathf.Max(0.001f, Vector3.Dot(v, rotation * Vector3.forward));
            float tan = Mathf.Tan(verticalFovDeg * 0.5f * Mathf.Deg2Rad);
            return new Vector2(0.5f + 0.5f * Vector3.Dot(v, rotation * Vector3.right) / (z * tan * aspect),
                0.5f + 0.5f * Vector3.Dot(v, rotation * Vector3.up) / (z * tan));
        }

        /// <summary>The point on the plane z = <paramref name="focusZ"/> under a viewport position, for the tilted camera.</summary>
        public static Vector2 ViewportToPlaneTilted(Vector2 focus, float focusZ, float viewHeight, float verticalFovDeg,
            float tiltDeg, float aspect, Vector2 viewportPoint, float yawDeg = 0f)
        {
            var cam = TiltedCameraPosition(focus, focusZ, viewHeight, verticalFovDeg, tiltDeg, yawDeg);
            var rotation = Quaternion.Euler(tiltDeg, yawDeg, 0f);
            float tan = Mathf.Tan(verticalFovDeg * 0.5f * Mathf.Deg2Rad);
            var dir = rotation * Vector3.forward + rotation * Vector3.right * (2f * (viewportPoint.x - 0.5f) * tan * aspect)
                      + rotation * Vector3.up * (2f * (viewportPoint.y - 0.5f) * tan);
            float s = (focusZ - cam.z) / Mathf.Max(0.0001f, dir.z);
            var p = cam + dir * s;
            return new Vector2(p.x, p.y);
        }
    }
}
