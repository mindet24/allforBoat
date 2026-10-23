using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The 3D greybox house for one sim: rooms, walls with door openings, sockets, appliances and the character,
    /// built at runtime from the sim's rooms (GDD v1.1 §3.5). Reads sim state every frame in LateUpdate and never
    /// stores game state of its own. Destroy the object to remove everything.
    /// </summary>
    public sealed class HouseView : MonoBehaviour
    {
        private LevelController _controller;
        private HouseSim _sim;
        private TrippedViewConfig _config;
        private readonly List<RoomView> _rooms = new List<RoomView>();
        private readonly Dictionary<string, SocketView> _sockets = new Dictionary<string, SocketView>();
        private readonly List<SocketView> _socketList = new List<SocketView>();
        private readonly List<ApplianceView> _appliances = new List<ApplianceView>();
        private readonly List<DoorState> _doorStates = new List<DoorState>();
        private readonly List<Transform> _doorPivots = new List<Transform>();
        private readonly List<float> _doorClosedness = new List<float>();
        private readonly List<WireView> _wires = new List<WireView>();
        private readonly Dictionary<string, CableView> _cables = new Dictionary<string, CableView>();
        private readonly List<string> _staleCables = new List<string>();
        private Transform _wireRoot;
        private Transform _cableRoot;
        private CharacterView _character;
        private Transform _socketRoot;
        private string _highlightedSocket;

        /// <summary>World rect of the whole house front (x right, y up), for the camera.</summary>
        public Rect Bounds { get; private set; }
        private bool _day;
        private bool _kit;
        private SpriteRenderer _blackout;
        private DayPromptView _prompts;

        /// <summary>True when the rooms were built from the 3D house kit (day mode, useHouseKit), false = greybox.</summary>
        public bool UsesHouseKit => _kit;

        /// <summary>Day mode key prompts (null in other levels).</summary>
        public DayPromptView Prompts => _prompts;

        /// <summary>Builds the house for the controller's current sim.</summary>
        public void Build(LevelController controller, TrippedViewConfig config, ArtLibrary art)
        {
            _controller = controller;
            _sim = controller.Sim;
            _config = config;
            Bounds = HouseLayout.HouseBounds(_sim.Rooms, config.roomHeight);
            _day = _sim.IsDayMode;
            _kit = _day && HouseKitView.Available(config);
            if (_kit)
            {
                var kitGo = new GameObject("HouseKit");
                kitGo.transform.SetParent(transform, false);
                kitGo.AddComponent<HouseKitView>().Build(_sim, config);
                var b = Bounds;
                Bounds = Rect.MinMaxRect(b.xMin, b.yMin - HouseKitView.FoundationDepth, b.xMax, b.yMax + HouseKitView.RoofHeight);
            }

            foreach (var room in _sim.Rooms)
            {
                var go = new GameObject("Room " + room.Id);
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<RoomView>();
                bool panelRoom = room.Id == _sim.PanelRoomId;
                view.Build(room, panelRoom, config, art, _sim.Rooms, _day, !_kit, _day && panelRoom ? _sim.PanelX : float.NaN);
                _rooms.Add(view);
            }

            if (!_kit) BuildWalls();
            BuildStairs();

            _wireRoot = new GameObject("Wires").transform;
            _wireRoot.SetParent(transform, false);
            _cableRoot = new GameObject("Cables").transform;
            _cableRoot.SetParent(transform, false);
            int realSockets = 0;
            foreach (var s in _sim.Sockets)
                if (!s.IsCord) realSockets++;
            int lane = 0;
            foreach (var s in _sim.Sockets)
            {
                // No circuit puzzle in a day: no wires at all.
                if (s.IsCord || _day) continue;
                var go = new GameObject("Wire " + s.Id);
                go.transform.SetParent(_wireRoot, false);
                var wire = go.AddComponent<WireView>();
                wire.Build(s, _sim, config, lane++, realSockets);
                _wires.Add(wire);
            }

            _socketRoot = new GameObject("Sockets").transform;
            _socketRoot.SetParent(transform, false);
            SyncSocketSet();

            var appRoot = new GameObject("Appliances").transform;
            appRoot.SetParent(transform, false);
            foreach (var app in _sim.Appliances)
            {
                var go = new GameObject("Appliance " + app.Id);
                go.transform.SetParent(appRoot, false);
                var view = go.AddComponent<ApplianceView>();
                view.Build(app, _sim.Catalog.Find(app.Type), config, art, _day);
                _appliances.Add(view);
            }

            var chGo = new GameObject("Character");
            chGo.transform.SetParent(transform, false);
            _character = chGo.AddComponent<CharacterView>();
            _character.Build(config, art);

            if (_day)
            {
                var promptGo = new GameObject("Prompts");
                promptGo.transform.SetParent(transform, false);
                _prompts = promptGo.AddComponent<DayPromptView>();
                _prompts.Build(this, config);
            }

            LateUpdate();
        }

        /// <summary>Highlights one socket (the drop target under the pointer while an appliance is selected). Null = none.</summary>
        public void HighlightSocket(string socketId)
        {
            if (socketId == _highlightedSocket) return;
            if (_highlightedSocket != null && _sockets.TryGetValue(_highlightedSocket, out var old) && old != null) old.SetHighlight(false);
            _highlightedSocket = socketId;
            if (socketId != null && _sockets.TryGetValue(socketId, out var now) && now != null) now.SetHighlight(true);
        }

        /// <summary>The appliance view with this id, or null.</summary>
        public ApplianceView FindApplianceView(string applianceId)
        {
            for (int i = 0; i < _appliances.Count; i++)
                if (_appliances[i].Appliance.Id == applianceId) return _appliances[i];
            return null;
        }

        /// <summary>The socket view with this id, or null.</summary>
        public SocketView FindSocketView(string socketId)
        {
            return socketId != null && _sockets.TryGetValue(socketId, out var v) ? v : null;
        }

        private void LateUpdate()
        {
            if (_sim == null || _controller == null || _controller.Sim != _sim) return;
            float time = Time.unscaledTime;

            for (int i = 0; i < _rooms.Count; i++) _rooms[i].Sync(_sim);
            SyncBlackout();
            // Door leaves slide between closed (across the opening) and open (folded back to the hinge edge, so an
            // open door never hides what is behind it), smoothly.
            for (int i = 0; i < _doorStates.Count; i++)
            {
                float target = _doorStates[i].IsOpen ? 0.08f : 1f;
                _doorClosedness[i] = Mathf.MoveTowards(_doorClosedness[i], target, 6f * Time.unscaledDeltaTime);
                _doorPivots[i].localScale = new Vector3(1f, 1f, _doorClosedness[i]);
            }
            for (int i = 0; i < _wires.Count; i++) _wires[i].Sync();

            SyncSocketSet();
            for (int i = 0; i < _socketList.Count; i++)
            {
                var s = _socketList[i];
                s.Sync(_sim, _sim.IsRoomVisible(s.Socket.RoomId));
            }
            SyncCables();

            string selected = _controller.Selection;
            for (int i = 0; i < _appliances.Count; i++)
                _appliances[i].Sync(_sim, selected != null && selected == _appliances[i].Appliance.Id, time);

            _character.Sync(_sim, _controller.TickFraction, time);
            if (_prompts != null) _prompts.Sync(_sim);
        }

        /// <summary>Day mode: dims the whole house while any breaker is OFF (a blackout).</summary>
        private void SyncBlackout()
        {
            if (!_day) return;
            if (_blackout == null)
            {
                var go = new GameObject("BlackoutOverlay");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(Bounds.center.x, Bounds.center.y, -0.1f);
                _blackout = go.AddComponent<SpriteRenderer>();
                _blackout.sprite = PlaceholderArt.SoftRect;
                _blackout.drawMode = SpriteDrawMode.Sliced;
                _blackout.size = new Vector2(Bounds.width + 1.2f, Bounds.height + 1.2f);
                _blackout.color = _config.blackoutOverlay;
                _blackout.sortingOrder = 11;
                _blackout.enabled = false;
            }
            bool on = DayRules.IsBlackout(_sim);
            if (_blackout.enabled != on) _blackout.enabled = on;
        }

        /// <summary>One cable per cord socket: created when a cord appears, removed when it goes.</summary>
        private void SyncCables()
        {
            int frame = Time.frameCount;
            foreach (var s in _sim.Sockets)
            {
                if (!s.IsCord) continue;
                if (!_cables.TryGetValue(s.Id, out var cable) || cable == null || cable.Cord != s)
                {
                    if (cable != null) Destroy(cable.gameObject);
                    var go = new GameObject("Cable " + s.Id);
                    go.transform.SetParent(_cableRoot, false);
                    cable = go.AddComponent<CableView>();
                    cable.Build(s, _config);
                    _cables[s.Id] = cable;
                }
                cable.SeenFrame = frame;
                cable.Sync(_sim);
            }
            _staleCables.Clear();
            foreach (var pair in _cables)
                if (pair.Value == null || pair.Value.SeenFrame != frame) _staleCables.Add(pair.Key);
            foreach (var id in _staleCables)
            {
                if (_cables[id] != null) Destroy(_cables[id].gameObject);
                _cables.Remove(id);
            }
        }

        /// <summary>Creates views for new sockets (extension cords) and removes views of sockets that are gone.</summary>
        private void SyncSocketSet()
        {
            int frame = Time.frameCount;
            var sockets = _sim.Sockets;
            for (int i = 0; i < sockets.Count; i++)
            {
                var s = sockets[i];
                if (!_sockets.TryGetValue(s.Id, out var view) || view == null || view.Socket != s)
                {
                    if (view != null)
                    {
                        _socketList.Remove(view);
                        Destroy(view.gameObject);
                    }
                    var go = new GameObject("Socket " + s.Id);
                    go.transform.SetParent(_socketRoot, false);
                    view = go.AddComponent<SocketView>();
                    view.Build(s, _sim, _config, _kit);
                    _sockets[s.Id] = view;
                    _socketList.Add(view);
                }
                view.SeenFrame = frame;
            }
            if (_socketList.Count == sockets.Count) return;
            for (int i = _socketList.Count - 1; i >= 0; i--)
            {
                var view = _socketList[i];
                if (view.SeenFrame == frame) continue;
                _sockets.Remove(view.SocketId);
                _socketList.RemoveAt(i);
                Destroy(view.gameObject);
            }
        }

        /// <summary>
        /// Stairs between rooms on different floors: a slanted plank with rungs, drawn on the front edge of the
        /// house inside the x range the two rooms share, so it reads from the camera without hiding the rooms.
        /// </summary>
        private void BuildStairs()
        {
            var root = new GameObject("Stairs").transform;
            root.SetParent(transform, false);
            foreach (var door in _sim.Doors)
            {
                if (!door.IsStairs) continue;
                var a = _sim.FindRoom(door.RoomA);
                var b = _sim.FindRoom(door.RoomB);
                if (a == null || b == null || a.Floor == b.Floor) continue;
                var lower = a.Floor < b.Floor ? a : b;
                var upper = lower == a ? b : a;
                float yLow = HouseLayout.FloorY(lower.Floor, _config.roomHeight);
                float yHigh = HouseLayout.FloorY(upper.Floor, _config.roomHeight);
                Vector2 p0, p1;
                float z, depth;
                if (_day)
                {
                    // Day mode: the flight is centred on sim.StairsX (where W/S work and the climb runs), drawn just
                    // behind the character's walking line so a climbing character is in front of it.
                    float sx = _sim.StairsX(door);
                    p0 = HouseLayout.ClimbPoint(sx, yLow, yHigh, _config.stairsLanding, _config.stairsHalfRun, _config.stairsLanding);
                    p1 = HouseLayout.ClimbPoint(sx, yLow, yHigh, 1f - _config.stairsLanding, _config.stairsHalfRun, _config.stairsLanding);
                    p0.y += 0.05f;
                    p1.y += 0.05f;
                    depth = Mathf.Max(0.05f, _config.characterInset * 0.6f);
                    z = _config.roomDepth - _config.characterInset * 0.5f + 0.02f;
                }
                else
                {
                    var span = HouseLayout.OverlapSpan(lower, upper);
                    if (span.y - span.x < 1f) continue;
                    p0 = new Vector2(span.x + 0.8f, yLow + 0.1f);
                    p1 = new Vector2(span.y - 0.8f, yHigh + 0.1f);
                    z = -0.3f;
                    depth = 0.5f;
                }
                var delta = p1 - p0;
                float length = delta.magnitude;
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                var plank = Prims.Box(root, "Stairs " + door.Id, new Vector3((p0.x + p1.x) * 0.5f, (p0.y + p1.y) * 0.5f, z),
                    new Vector3(length, 0.09f, depth), _config.doorColor);
                plank.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                int rungs = Mathf.Max(3, Mathf.RoundToInt(length / 0.45f));
                for (int i = 1; i < rungs; i++)
                {
                    var at = Vector2.Lerp(p0, p1, (float)i / rungs);
                    Prims.Box(root, "Rung", new Vector3(at.x, at.y + 0.12f, z), new Vector3(0.22f, 0.07f, depth), _config.slabColor);
                }
            }
        }

        private void BuildWalls()
        {
            var root = new GameObject("Walls").transform;
            root.SetParent(transform, false);
            float h = _config.roomHeight, d = _config.roomDepth, t = _config.wallThickness;
            float doorH = Mathf.Min(_config.doorHeight, h - 0.1f);
            float doorW = Mathf.Min(_config.doorWidth, d - 0.1f);

            foreach (var wall in HouseLayout.Walls(_sim.Rooms, _sim.Doors))
            {
                float y0 = HouseLayout.FloorY(wall.Floor, h);
                string name = "Wall x" + wall.X.ToString("0.##");
                if (string.IsNullOrEmpty(wall.DoorId))
                {
                    Prims.Box(root, name, new Vector3(wall.X, y0 + h * 0.5f, d * 0.5f), new Vector3(t, h, d), _config.wallColor);
                    continue;
                }

                // Door opening at the open front of the house so it reads from the camera.
                Prims.Box(root, name + " lintel", new Vector3(wall.X, y0 + doorH + (h - doorH) * 0.5f, d * 0.5f),
                    new Vector3(t, h - doorH, d), _config.wallColor);
                Prims.Box(root, name + " back", new Vector3(wall.X, y0 + doorH * 0.5f, (doorW + d) * 0.5f),
                    new Vector3(t, doorH, d - doorW), _config.wallColor);

                // The leaf hangs on a hinge at the far edge of the opening and folds back to it when open.
                var pivot = new GameObject("Hinge " + wall.DoorId).transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(wall.X, y0, doorW);
                Prims.Box(pivot, "Door " + wall.DoorId, new Vector3(0f, doorH * 0.5f, -doorW * 0.5f),
                    new Vector3(t * 0.6f, doorH, doorW), _config.doorColor);
                var door = _sim.FindDoor(wall.DoorId);
                if (door != null)
                {
                    _doorStates.Add(door);
                    _doorPivots.Add(pivot);
                    _doorClosedness.Add(door.IsOpen ? 0.08f : 1f);
                    pivot.localScale = new Vector3(1f, 1f, door.IsOpen ? 0.08f : 1f);
                    // Clickable area stays put whether the door is open or shut.
                    if (_day) continue;
                    var click = new GameObject("DoorClick " + wall.DoorId);
                    click.transform.SetParent(root, false);
                    click.transform.localPosition = new Vector3(wall.X, y0 + doorH * 0.5f, doorW * 0.5f);
                    ClickTarget.Add(click, ClickKind.Door, wall.DoorId, "", 1, Vector3.zero, new Vector3(0.9f, doorH, doorW));
                }
            }
        }
    }
}
