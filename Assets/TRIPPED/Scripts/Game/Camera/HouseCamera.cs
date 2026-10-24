using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Trouble.Game
{
    /// <summary>
    /// Camera looking straight at the house front (GDD v1.1 §2.6), orthographic by default (art direction 23 Oct)
    /// or perspective (<see cref="TrippedViewConfig.orthographic"/>). Wheel zooms toward the cursor
    /// (smoothed), middle-drag and WASD/arrows pan, F toggles following the character, Home shows the whole house.
    /// Position is clamped to the house. All numbers come from <see cref="TrippedViewConfig"/>.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class HouseCamera : MonoBehaviour
    {
        /// <summary>z of the plane the camera frames: the open front of the house.</summary>
        private const float FocusZ = 0f;

        private Camera _camera;
        private TrippedViewConfig _config;
        private Func<Vector2> _followPoint;
        private Rect _bounds;
        private Rect _limits;
        // "View height" is the world height seen on the focus plane. Both projections are driven by it.
        private float _minDistance = 5f;
        private float _maxDistance = 30f;
        private float _distance;
        private float _targetDistance;
        private float _distanceVelocity;
        private const float OrthoDepth = 50f;
        private Vector2 _position;
        private Vector2 _targetPosition;
        private Vector2 _positionVelocity;
        private bool _dragging;
        private Vector2 _lastMouse;
        /// <summary>The player has not zoomed or panned since the last Home, so a window resize re-frames the house.</summary>
        private Rect _area;
        private bool _atHome;
        private float _framedAspect;

        /// <summary>True while the camera follows the character (F).</summary>
        public bool Following { get; private set; }

        /// <summary>true = A/D/W/S and the arrow keys pan the camera. Off in day mode, where they move the character.</summary>
        public bool KeyPan { get; set; } = true;

        /// <summary>View height (world metres on screen, top to bottom) the camera is easing toward.</summary>
        public float TargetViewHeight => _targetDistance;

        /// <summary>Current camera x, y on the house front.</summary>
        public Vector2 Position => _position;

        /// <summary>Current view height in world metres.</summary>
        public float ViewHeight => _distance;

        /// <summary>The Unity camera, for ray casts.</summary>
        public Camera Cam => _camera;

        /// <summary>
        /// Screen position of a house-front point as a viewport coordinate (0..1, origin bottom-left; outside 0..1 =
        /// off screen). Uses the position the camera is easing toward, so alert arrows do not lag.
        /// </summary>
        public Vector2 WorldToViewport(Vector2 point)
        {
            if (_targetDistance <= 0.0001f) return new Vector2(0.5f, 0.5f);
            return CameraMath.PlaneToViewportForHeight(_targetPosition, _targetDistance, Aspect(), point);
        }

        /// <summary>Zooms to a view height (clamped to the zoom limits). <paramref name="snap"/> = no easing.</summary>
        public void ZoomToHeight(float viewHeight, bool snap)
        {
            _atHome = false;
            _targetDistance = Mathf.Clamp(viewHeight, _minDistance, _maxDistance);
            if (!snap) return;
            _distance = _targetDistance;
            _distanceVelocity = 0f;
            Apply();
        }

        /// <summary>Moves the view so <paramref name="point"/> is centred (no zoom change). Stops following.</summary>
        public void PanTo(Vector2 point)
        {
            Following = false;
            _atHome = false;
            _targetPosition = CameraMath.CenterOnForHeight(point, _targetDistance, Aspect(), _area);
        }

        /// <summary>The part of the screen the house may use (viewport units). Day mode uses a wider one.</summary>
        public void UseArea(Rect area)
        {
            _area = area;
        }

        /// <summary>Space around the whole house at the farthest zoom (0.1 = 10%). Negative = the config's homeMargin.</summary>
        public float HomeMargin { get; set; } = -1f;

        /// <summary>Takes the camera settings from the config.</summary>
        public void Setup(TrippedViewConfig config)
        {
            _config = config;
            _area = config.houseArea;
            _camera = GetComponent<Camera>();
            _camera.orthographic = config.orthographic;
            _camera.orthographicSize = 5f;
            _camera.fieldOfView = config.fieldOfView;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = config.sceneBackground;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 500f;
            transform.rotation = Quaternion.identity;
        }

        /// <summary>Frames a new house and jumps to the whole-house view.</summary>
        /// <param name="bounds">House front rect in world metres.</param>
        /// <param name="followPoint">Where the character is, for F.</param>
        public void Frame(Rect bounds, Func<Vector2> followPoint)
        {
            _bounds = bounds;
            _followPoint = followPoint;
            Following = false;
            Home(true);
        }

        /// <summary>Shows the whole house. <paramref name="snap"/> = no easing.</summary>
        public void Home(bool snap)
        {
            float aspect = Aspect();
            _maxDistance = CameraMath.FitViewHeight(_bounds.size, aspect, _area, HomeMargin >= 0f ? HomeMargin : _config.homeMargin);
            _minDistance = Mathf.Min(_maxDistance,
                CameraMath.ViewHeightForWidth(_config.nearRoomWidth, _config.nearRoomScreenFraction, aspect));
            _targetDistance = _maxDistance;
            _targetPosition = CameraMath.CenterOnForHeight(_bounds.center, _maxDistance, aspect, _area);
            _limits = CameraMath.Encapsulate(_bounds, _targetPosition);
            _atHome = true;
            _framedAspect = aspect;
            if (snap)
            {
                _distance = _targetDistance;
                _position = _targetPosition;
                _distanceVelocity = 0f;
                _positionVelocity = Vector2.zero;
                Apply();
            }
        }

        private void Update()
        {
            if (_config == null) return;
            float dt = Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            float aspect = Aspect();

            if (kb != null)
            {
                if (kb.fKey.wasPressedThisFrame)
                {
                    Following = !Following;
                    _atHome = false;
                }
                if (kb.homeKey.wasPressedThisFrame)
                {
                    Following = false;
                    Home(false);
                }

                var pan = Vector2.zero;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) pan.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) pan.x += 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) pan.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) pan.y -= 1f;
                if (!KeyPan) pan = Vector2.zero;
                if (pan != Vector2.zero)
                {
                    Following = false;
                    _atHome = false;
                    var visible = CameraMath.VisibleSizeForHeight(_targetDistance, aspect);
                    _targetPosition += pan * (_config.keyPanSpeed * visible.x * dt);
                }
            }

            if (mouse != null)
            {
                Vector2 mousePos = mouse.position.ReadValue();
                float scroll = mouse.scroll.ReadValue().y;
                // Windows reports 120 per wheel notch; other platforms report about 1.
                float notches = Mathf.Abs(scroll) > 10f ? scroll / 120f : scroll;
                if (Mathf.Abs(notches) > 0.001f && !UiHit.IsOverUi(mousePos)) ZoomAt(mousePos, notches, aspect);

                if (mouse.middleButton.wasPressedThisFrame && !UiHit.IsOverUi(mousePos))
                {
                    _dragging = true;
                    _lastMouse = mousePos;
                }
                if (!mouse.middleButton.isPressed) _dragging = false;
                if (_dragging)
                {
                    var delta = mousePos - _lastMouse;
                    _lastMouse = mousePos;
                    if (delta != Vector2.zero)
                    {
                        Following = false;
                        _atHome = false;
                        var visible = CameraMath.VisibleSizeForHeight(_distance, aspect);
                        float metresPerPixel = visible.y / Mathf.Max(1, Screen.height);
                        _targetPosition -= delta * metresPerPixel;
                        _position -= delta * metresPerPixel; // drag sticks to the cursor, no easing lag
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (_config == null) return;
            float aspect = Aspect();
            // The window (or Game view) changed shape: re-frame if the player has not moved the camera,
            // otherwise just recompute the zoom limits.
            if (Mathf.Abs(aspect - _framedAspect) > 0.001f)
            {
                if (_atHome) Home(true);
                else
                {
                    var keepDistance = _targetDistance;
                    var keepPosition = _targetPosition;
                    Home(false);
                    _atHome = false;
                    _targetDistance = keepDistance;
                    _targetPosition = keepPosition;
                }
            }
            if (Following && _followPoint != null)
                _targetPosition = CameraMath.CenterOnForHeight(_followPoint() + new Vector2(0f, 1f), _targetDistance, aspect, _area);

            _targetDistance = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);
            _targetPosition = CameraMath.Clamp(_targetPosition, _limits);
            float smooth = Mathf.Max(0.01f, _config.zoomSmoothTime);
            float dt = Time.unscaledDeltaTime;
            _distance = Mathf.SmoothDamp(_distance, _targetDistance, ref _distanceVelocity, smooth, Mathf.Infinity, dt);
            _position = Vector2.SmoothDamp(_position, _targetPosition, ref _positionVelocity, smooth, Mathf.Infinity, dt);
            Apply();
        }

        private void ZoomAt(Vector2 screenPos, float notches, float aspect)
        {
            _atHome = false;
            float newDistance = Mathf.Clamp(_targetDistance * Mathf.Pow(1f - _config.zoomStep, notches), _minDistance, _maxDistance);
            var viewport = new Vector2(screenPos.x / Mathf.Max(1, Screen.width), screenPos.y / Mathf.Max(1, Screen.height));
            var point = CameraMath.ViewportToPlaneForHeight(_targetPosition, _targetDistance, aspect, viewport);
            _targetPosition = CameraMath.ZoomToward(_targetPosition, _targetDistance, newDistance, point);
            _targetDistance = newDistance;
        }

        private void Apply()
        {
            if (_config.orthographic)
            {
                _camera.orthographic = true;
                _camera.orthographicSize = _distance * 0.5f;
                transform.position = new Vector3(_position.x, _position.y, FocusZ - OrthoDepth);
            }
            else
            {
                _camera.orthographic = false;
                transform.position = new Vector3(_position.x, _position.y, FocusZ - CameraMath.DistanceForViewHeight(_distance, _config.fieldOfView));
            }
            transform.rotation = Quaternion.identity;
        }

        private float Aspect()
        {
            return _camera != null && _camera.aspect > 0.01f ? _camera.aspect : 16f / 9f;
        }
    }
}
