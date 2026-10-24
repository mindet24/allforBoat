using System;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Trouble.Game
{
    /// <summary>
    /// Camera looking at the house front (GDD v1.1 §2.6): perspective, looking down by
    /// <see cref="TrippedViewConfig.cameraTilt"/> degrees so floors and room depth show, or orthographic and straight on
    /// (<see cref="TrippedViewConfig.orthographic"/>). Wheel zooms toward the cursor
    /// (smoothed), middle-drag and WASD/arrows pan, F toggles following the character, Home shows the whole house.
    /// Position is clamped to the house. All numbers come from <see cref="TrippedViewConfig"/>.
    /// </summary>
    /// <remarks>
    /// The camera is moved through a parent pivot ("CameraRig"); the camera's own local position and rotation are
    /// never written after <see cref="Setup"/>, so a shake component may offset <see cref="ShakeTarget"/> freely.
    /// Day mode (<see cref="FollowDay"/>): the view follows the character closely and holding the overview key
    /// (Tab / Q, <see cref="TrippedViewConfig.overviewKey"/>) shows the whole house. The player sets the angle by
    /// right-dragging (turn left / right, tilt up / down, within the config's limits); middle-click or Home puts it
    /// back. The follow zoom is fixed (no wheel).
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-50)]
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
        private Transform _rig;
        private HouseSim _daySim;
        private float _followVelX;
        private float _followVelY;
        // The camera's angle: looking down by _tilt and turned by _yaw, easing toward the player's choice.
        private float _tilt;
        private float _yaw;
        private float _targetTilt;
        private float _targetYaw;
        private float _tiltVelocity;
        private float _yawVelocity;
        private bool _orbiting;

        /// <summary>Name of the pivot object the camera hangs under.</summary>
        public const string RigName = "CameraRig";

        /// <summary>The pivot this component moves. The camera sits at its origin.</summary>
        public Transform Rig => _rig;

        /// <summary>The transform a camera shake may offset (local position / rotation); this component never writes it.</summary>
        public Transform ShakeTarget => transform;

        /// <summary>True while day mode follows the character.</summary>
        public bool DayFollowing => _daySim != null;

        /// <summary>True while the overview key is held in day mode (the whole house is shown).</summary>
        public bool Overview { get; private set; }

        /// <summary>Test and capture hook: true forces the overview as if the key were held.</summary>
        public bool ForceOverview { get; set; }

        /// <summary>Part of the screen the overview fits the house into (viewport units); null = the config's overviewArea. The Title uses the right side.</summary>
        public Rect? AreaOverride { get; set; }

        /// <summary>
        /// Day mode: follow the character's room of <paramref name="sim"/> (the follow point comes from <see cref="Frame"/>).
        /// Wheel zoom, drag and key panning are off while following.
        /// </summary>
        public void FollowDay(HouseSim sim)
        {
            _daySim = sim;
            _followVelX = 0f;
            _followVelY = 0f;
        }

        /// <summary>Stops following <paramref name="sim"/> (ignored if another sim is followed now).</summary>
        public void StopDay(HouseSim sim)
        {
            if (_daySim != sim) return;
            _daySim = null;
            Overview = false;
            _orbiting = false;
            // Other levels always look from the config's angle.
            _tilt = _targetTilt = _config.cameraTilt;
            _yaw = _targetYaw = 0f;
        }

        /// <summary>
        /// Viewport position (0..1, origin bottom-left) of a house-front point where the camera is NOW (not where it is
        /// easing to), for HUD pieces pinned to the world.
        /// </summary>
        /// <param name="z">Depth of the point (0 = the house front; the back wall is at roomDepth).</param>
        public Vector2 WorldToViewportNow(Vector2 point, float z = FocusZ)
        {
            if (_distance <= 0.0001f) return new Vector2(0.5f, 0.5f);
            return ToViewport(_position, _distance, new Vector3(point.x, point.y, z));
        }

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

        /// <summary>Degrees the camera looks down now.</summary>
        public float Tilt => _tilt;

        /// <summary>Degrees the camera is turned now (positive = looking right).</summary>
        public float Yaw => _yaw;

        /// <summary>
        /// Day mode: sets the angle the camera eases to, kept within the config's limits (the same thing a right-drag
        /// does). <paramref name="snap"/> = no easing.
        /// </summary>
        public void SetDayView(float tilt, float yaw, bool snap)
        {
            _targetTilt = Mathf.Clamp(tilt, _config.cameraTiltRange.x, _config.cameraTiltRange.y);
            _targetYaw = Mathf.Clamp(yaw, -_config.cameraYawLimit, _config.cameraYawLimit);
            if (!snap) return;
            _tilt = _targetTilt;
            _yaw = _targetYaw;
            _tiltVelocity = _yawVelocity = 0f;
            Apply();
        }

        /// <summary>Day mode: back to the config's angle.</summary>
        public void ResetDayView(bool snap) => SetDayView(_config.cameraTilt, 0f, snap);

        /// <summary>The Unity camera, for ray casts.</summary>
        public Camera Cam => _camera;

        /// <summary>
        /// Screen position of a house-front point as a viewport coordinate (0..1, origin bottom-left; outside 0..1 =
        /// off screen). Uses the position the camera is easing toward, so alert arrows do not lag.
        /// </summary>
        public Vector2 WorldToViewport(Vector2 point)
        {
            if (_targetDistance <= 0.0001f) return new Vector2(0.5f, 0.5f);
            return ToViewport(_targetPosition, _targetDistance, new Vector3(point.x, point.y, FocusZ));
        }

        /// <summary>Viewport position of a world point for a camera at <paramref name="position"/> showing <paramref name="viewHeight"/>.</summary>
        private Vector2 ToViewport(Vector2 position, float viewHeight, Vector3 point)
        {
            if (_config == null || _config.orthographic)
                return CameraMath.PlaneToViewportForHeight(position, viewHeight, Aspect(), point);
            return CameraMath.WorldToViewportTilted(position, FocusZ, viewHeight, _config.fieldOfView, _tilt, Aspect(), point, _yaw);
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
            if (_rig == null)
            {
                var parent = transform.parent;
                if (parent != null && parent.name == RigName) _rig = parent;
                else
                {
                    _rig = new GameObject(RigName).transform;
                    _rig.SetParent(parent, false);
                    _rig.SetPositionAndRotation(transform.position, Quaternion.identity);
                    transform.SetParent(_rig, true);
                }
            }
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _tilt = _targetTilt = config.cameraTilt;
            _yaw = _targetYaw = 0f;
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
            if (_daySim != null)
            {
                bool held = ForceOverview;
                if (kb != null)
                {
                    if (_config.overviewKey != Key.None) held |= kb[_config.overviewKey].isPressed;
                    if (_config.overviewAltKey != Key.None) held |= kb[_config.overviewAltKey].isPressed;
                }
                Overview = held;
                DayAngleInput(kb, mouse);
                return;
            }

            if (kb != null)
            {
                if (KeyDown(kb, _config.followKey))
                {
                    Following = !Following;
                    _atHome = false;
                }
                if (KeyDown(kb, _config.homeKey))
                {
                    Following = false;
                    Home(false);
                }

                var pan = Vector2.zero;
                if (KeyHeld(kb, _config.walkLeftKey) || KeyHeld(kb, _config.walkLeftAltKey)) pan.x -= 1f;
                if (KeyHeld(kb, _config.walkRightKey) || KeyHeld(kb, _config.walkRightAltKey)) pan.x += 1f;
                if (KeyHeld(kb, _config.walkInKey) || KeyHeld(kb, _config.walkInAltKey)) pan.y += 1f;
                if (KeyHeld(kb, _config.walkOutKey) || KeyHeld(kb, _config.walkOutAltKey)) pan.y -= 1f;
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
            if (_daySim != null)
            {
                DayLateUpdate();
                return;
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

        /// <summary>
        /// Day mode: right-drag turns (sideways) and tilts (up and down) the camera, middle-click or Home resets it.
        /// Drags that start over the HUD are left to it.
        /// </summary>
        private void DayAngleInput(Keyboard kb, Mouse mouse)
        {
            if (kb != null && KeyDown(kb, _config.homeKey)) ResetDayView(false);
            if (mouse == null) return;
            Vector2 mousePos = mouse.position.ReadValue();
            bool overUi = UiHit.IsOverUi(mousePos);
            if (mouse.middleButton.wasPressedThisFrame && !overUi) ResetDayView(false);

            if (mouse.rightButton.wasPressedThisFrame && !overUi)
            {
                _orbiting = true;
                _lastMouse = mousePos;
            }
            if (!mouse.rightButton.isPressed) _orbiting = false;
            if (!_orbiting) return;
            var delta = mousePos - _lastMouse;
            _lastMouse = mousePos;
            if (delta == Vector2.zero) return;
            // Drag right = the camera swings left and looks right; drag up = look further down.
            SetDayView(_targetTilt + delta.y * _config.orbitDegreesPerPixel, _targetYaw + delta.x * _config.orbitDegreesPerPixel, false);
        }

        /// <summary>Day mode: ease toward the follow view of the character, or the overview while it is held.</summary>
        private void DayLateUpdate()
        {
            float aspect = Aspect();
            float dt = Time.unscaledDeltaTime;
            float rh = _config.roomHeight;
            var pad = _bounds;
            pad.xMin -= _config.followBoundsPad;
            pad.xMax += _config.followBoundsPad;
            pad.yMin -= _config.followBoundsPad;
            pad.yMax += _config.followBoundsPad;
            var overviewArea = AreaOverride ?? _config.overviewArea;
            float overviewHeight = CameraMath.FitViewHeight(_bounds.size, aspect, overviewArea, _config.overviewMargin);

            if (Overview || _followPoint == null)
            {
                _targetDistance = overviewHeight;
                _targetPosition = CameraMath.CenterOnForHeight(_bounds.center, overviewHeight, aspect, overviewArea);
            }
            else
            {
                var ch = _daySim.Character;
                var room = _daySim.FindRoom(ch.RoomId);
                var feet = _followPoint();
                float h = FollowFraming.ViewHeight(rh, _config.followFloors, overviewHeight);
                float fx = room != null ? FollowFraming.FocusX(feet.x, room.X, room.X + room.Width, _config.followRoomLock) : feet.x;
                // The character's middle, not the room's: up close the room's middle would push its feet under the HUD.
                var focus = new Vector2(fx, feet.y + _config.playerWorldHeight * 0.5f);
                var centre = FollowFraming.Center(focus, h, aspect, _config.followScreenAnchor);
                _targetDistance = h;
                _targetPosition = FollowFraming.ClampToBounds(centre, h, aspect, pad);
            }

            float zoomSmooth = Mathf.Max(0.01f, _config.overviewSmoothTime);
            _distance = Mathf.SmoothDamp(_distance, _targetDistance, ref _distanceVelocity, zoomSmooth, Mathf.Infinity, dt);
            _position.x = Mathf.SmoothDamp(_position.x, _targetPosition.x, ref _followVelX, Mathf.Max(0.01f, _config.followSmoothTime), Mathf.Infinity, dt);
            _position.y = Mathf.SmoothDamp(_position.y, _targetPosition.y, ref _followVelY, Mathf.Max(0.01f, _config.followFloorSmoothTime), Mathf.Infinity, dt);
            // The overview always shows the house straight from the config's angle; the follow view uses the player's.
            float angleSmooth = Mathf.Max(0.01f, _config.cameraAngleSmoothTime);
            _tilt = Mathf.SmoothDamp(_tilt, Overview ? _config.cameraTilt : _targetTilt, ref _tiltVelocity, angleSmooth, Mathf.Infinity, dt);
            _yaw = Mathf.SmoothDamp(_yaw, Overview ? 0f : _targetYaw, ref _yawVelocity, angleSmooth, Mathf.Infinity, dt);
            Apply();
        }

        private void ZoomAt(Vector2 screenPos, float notches, float aspect)
        {
            _atHome = false;
            float newDistance = Mathf.Clamp(_targetDistance * Mathf.Pow(1f - _config.zoomStep, notches), _minDistance, _maxDistance);
            var viewport = new Vector2(screenPos.x / Mathf.Max(1, Screen.width), screenPos.y / Mathf.Max(1, Screen.height));
            var point = _config.orthographic
                ? CameraMath.ViewportToPlaneForHeight(_targetPosition, _targetDistance, aspect, viewport)
                : CameraMath.ViewportToPlaneTilted(_targetPosition, FocusZ, _targetDistance, _config.fieldOfView, _tilt, aspect, viewport, _yaw);
            _targetPosition = CameraMath.ZoomToward(_targetPosition, _targetDistance, newDistance, point);
            _targetDistance = newDistance;
        }

        private void Apply()
        {
            if (_config.orthographic)
            {
                _camera.orthographic = true;
                _camera.orthographicSize = _distance * 0.5f;
                Pivot.position = new Vector3(_position.x, _position.y, FocusZ - OrthoDepth);
                Pivot.rotation = Quaternion.identity;
            }
            else
            {
                _camera.orthographic = false;
                Pivot.position = CameraMath.TiltedCameraPosition(_position, FocusZ, _distance, _config.fieldOfView, _tilt, _yaw);
                Pivot.rotation = Quaternion.Euler(_tilt, _yaw, 0f);
            }
        }

        private static bool KeyHeld(Keyboard kb, Key key) => key != Key.None && kb[key].isPressed;
        private static bool KeyDown(Keyboard kb, Key key) => key != Key.None && kb[key].wasPressedThisFrame;

        private Transform Pivot => _rig != null ? _rig : transform;

        private float Aspect()
        {
            return _camera != null && _camera.aspect > 0.01f ? _camera.aspect : 16f / 9f;
        }
    }
}
