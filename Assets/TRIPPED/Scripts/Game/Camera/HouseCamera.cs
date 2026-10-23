using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Trouble.Game
{
    /// <summary>
    /// Perspective camera looking straight at the house front (GDD v1.1 §2.6): wheel zooms toward the cursor
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
        private float _minDistance = 5f;
        private float _maxDistance = 30f;
        private float _distance;
        private float _targetDistance;
        private float _distanceVelocity;
        private Vector2 _position;
        private Vector2 _targetPosition;
        private Vector2 _positionVelocity;
        private bool _dragging;
        private Vector2 _lastMouse;
        /// <summary>The player has not zoomed or panned since the last Home, so a window resize re-frames the house.</summary>
        private bool _atHome;
        private float _framedAspect;

        /// <summary>True while the camera follows the character (F).</summary>
        public bool Following { get; private set; }

        /// <summary>Distance the camera is easing toward.</summary>
        public float TargetDistance => _targetDistance;

        /// <summary>Takes the camera settings from the config.</summary>
        public void Setup(TrippedViewConfig config)
        {
            _config = config;
            _camera = GetComponent<Camera>();
            _camera.orthographic = false;
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
            _maxDistance = CameraMath.FitDistance(_bounds.size, _config.fieldOfView, aspect, _config.houseViewport, _config.homeMargin);
            _minDistance = Mathf.Min(_maxDistance,
                CameraMath.DistanceForWidth(_config.nearRoomWidth, _config.nearRoomScreenFraction, _config.fieldOfView, aspect));
            _targetDistance = _maxDistance;
            _targetPosition = CameraMath.CenterOn(_bounds.center, _maxDistance, _config.fieldOfView, aspect, _config.houseViewport);
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
                if (pan != Vector2.zero)
                {
                    Following = false;
                    _atHome = false;
                    var visible = CameraMath.VisibleSize(_targetDistance, _config.fieldOfView, aspect);
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
                        var visible = CameraMath.VisibleSize(_distance, _config.fieldOfView, aspect);
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
                _targetPosition = CameraMath.CenterOn(_followPoint() + new Vector2(0f, 1f), _targetDistance, _config.fieldOfView, aspect, _config.houseViewport);

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
            var point = CameraMath.ViewportToPlane(_targetPosition, _targetDistance, _config.fieldOfView, aspect, viewport);
            _targetPosition = CameraMath.ZoomToward(_targetPosition, _targetDistance, newDistance, point);
            _targetDistance = newDistance;
        }

        private void Apply()
        {
            transform.position = new Vector3(_position.x, _position.y, FocusZ - _distance);
            transform.rotation = Quaternion.identity;
        }

        private float Aspect()
        {
            return _camera != null && _camera.aspect > 0.01f ? _camera.aspect : 16f / 9f;
        }
    }
}
