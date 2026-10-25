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
        private readonly List<CharacterView> _partners = new List<CharacterView>();
        private readonly List<int> _partnerPlayers = new List<int>();
        private FamilyView _family;
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
            Transform kitRoot = null;
            _controller = controller;
            controller.CommandAccepted += OnCommandAccepted;
            _sim = controller.Sim;
            _config = config;
            Bounds = HouseLayout.HouseBounds(_sim.Rooms, config.roomHeight);
            _day = _sim.IsDayMode;
            _kit = _day && HouseKitView.Available(config);
            if (_kit)
            {
                var kitGo = new GameObject("HouseKit");
                kitGo.transform.SetParent(transform, false);
                kitRoot = kitGo.transform;
                var kitView = kitGo.AddComponent<HouseKitView>();
                kitView.Build(_sim, config);
                HouseKitView.Pastel(kitRoot, config);
                // Frame what was really built: roof peak, chimney and foundation included.
                var b = Bounds;
                var e = kitView.Extent;
                if (e.width > 0f)
                    Bounds = Rect.MinMaxRect(Mathf.Min(b.xMin, e.xMin), Mathf.Min(b.yMin, e.yMin), Mathf.Max(b.xMax, e.xMax), Mathf.Max(b.yMax, e.yMax));
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
            if (!_day) BuildStairs();
            // Day mode, rules verticalMove "stairs": solid steps at the open front, under the kit so they fade like
            // any wall when the character walks behind them.
            else if (_sim.UsesStairs) BuildDayStairs(kitRoot != null ? kitRoot : transform);

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
            _character.Build(config, art, controller.ArtFor(_sim.ViewPlayer));
            var targets = new List<Transform> { chGo.transform };
            // Co-op: one more view per other keyboard player.
            for (int p = 0; p < _sim.Characters.Count; p++)
            {
                if (p == _sim.ViewPlayer) continue;
                var pGo = new GameObject("Partner " + p);
                pGo.transform.SetParent(transform, false);
                var view = pGo.AddComponent<CharacterView>();
                view.Build(config, art, controller.ArtFor(p));
                _partners.Add(view);
                _partnerPlayers.Add(p);
                targets.Add(pGo.transform);
            }

            if (_day)
            {
                var familyGo = new GameObject("Family");
                familyGo.transform.SetParent(transform, false);
                _family = familyGo.AddComponent<FamilyView>();
                _family.Build(_sim, config, art, TrippedUiConfig.LoadOrDefault().PersonColor);

                var promptGo = new GameObject("Prompts");
                promptGo.transform.SetParent(transform, false);
                _prompts = promptGo.AddComponent<DayPromptView>();
                _prompts.Build(this, config);

                // Walls, floors and furniture in front of the character, an appliance or a family member go see-through.
                var cam = controller.HouseCam != null ? controller.HouseCam.Cam : Camera.main;
                if (kitRoot != null && cam != null) BuildScenery(kitRoot, cam);
                if (kitRoot != null && cam != null)
                {
                    targets.Add(appRoot);
                    targets.Add(familyGo.transform);
                    gameObject.AddComponent<OccluderFader>().Build(kitRoot, targets, cam, config);
                }
            }

            LateUpdate();
        }

        private void OnCommandAccepted(Command command)
        {
            if (_character != null && _sim != null) _character.OnCommandAccepted(command, _sim);
        }

        private void OnDestroy()
        {
            if (_controller != null) _controller.CommandAccepted -= OnCommandAccepted;
        }

        /// <summary>Highlights one socket (the drop target under the pointer while an appliance is selected). Null = none.</summary>
        public void HighlightSocket(string socketId)
        {
            if (socketId == _highlightedSocket) return;
            if (_highlightedSocket != null && _sockets.TryGetValue(_highlightedSocket, out var old) && old != null) old.SetHighlight(false);
            _highlightedSocket = socketId;
            if (socketId != null && _sockets.TryGetValue(socketId, out var now) && now != null) now.SetHighlight(true);
        }

        /// <summary>
        /// Adds the world x, y rects HUD markers must keep clear of: every key prompt on screen, the character's head
        /// and what it carries, and every room label.
        /// </summary>
        public void CollectKeepClear(List<Rect> into)
        {
            if (_prompts != null) _prompts.CollectShownRects(into);
            if (_character != null) into.Add(_character.HeadWorldRect);
            for (int i = 0; i < _rooms.Count; i++) into.Add(_rooms[i].LabelWorldRect);
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
            if (_partners.Count > 0)
            {
                // Each partner view reads the sim as if it were that player's (Character = that player).
                int view = _sim.ViewPlayer;
                for (int i = 0; i < _partners.Count; i++)
                {
                    _sim.ViewPlayer = _partnerPlayers[i];
                    _partners[i].Sync(_sim, _controller.TickFraction, time);
                }
                _sim.ViewPlayer = view;
            }
            if (_family != null) _family.Sync(_sim, time, _appliances);
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
            var sockets = _sim.Sockets;
            for (int i = 0; i < sockets.Count; i++) // indexed: no boxed enumerator every frame
            {
                var s = sockets[i];
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
        /// Stairs between rooms on different floors (not in day mode): a slanted plank with rungs, drawn on the front
        /// edge of the house inside the x range the two rooms share, so it reads from the camera without hiding the rooms.
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
                var span = HouseLayout.OverlapSpan(lower, upper);
                if (span.y - span.x < 1f) continue;
                var p0 = new Vector2(span.x + 0.8f, yLow + 0.1f);
                var p1 = new Vector2(span.y - 0.8f, yHigh + 0.1f);
                const float z = -0.3f;
                const float depth = 0.5f;
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

        /// <summary>
        /// Day mode: each flight of stairs as a row of solid wooden steps from its bottom step to its top step (the sim's
        /// <see cref="DoorState.BottomXCm"/> / <see cref="DoorState.TopXCm"/>), from the open front of the house to
        /// <see cref="RulesConfig.stairsDepthMetres"/> deep. Their tops are where <see cref="HouseSim.StairsHeight"/>
        /// puts the character's feet.
        /// </summary>
        private void BuildDayStairs(Transform parent)
        {
            var root = new GameObject("Day stairs").transform;
            root.SetParent(parent, false);
            float depth = Mathf.Max(0.2f, _sim.Rules.stairsDepthMetres);
            float rise = Mathf.Max(0.05f, _config.dayStairsRise);
            foreach (var door in _sim.Doors)
            {
                if (!door.IsFlight) continue;
                var lower = _sim.FindRoom(door.LowerRoomId);
                float yLow = HouseLayout.FloorY(lower.Floor, _config.roomHeight);
                float bottom = door.BottomXCm / 100f, top = door.TopXCm / 100f;
                if (parent != transform) CutFloorOpening(parent, door, yLow + _config.roomHeight, depth);
                if (_config.dayStairsModel != null)
                {
                    PlaceStairsModel(root, door.Id, bottom, top, yLow, depth);
                    continue;
                }
                int steps = Mathf.Max(3, Mathf.RoundToInt(_config.roomHeight / rise));
                float run = (top - bottom) / steps;
                for (int i = 0; i < steps; i++)
                {
                    float x0 = bottom + run * i;
                    float h = _config.roomHeight * (i + 1) / steps;
                    Prims.Box(root, "Stair step " + door.Id + " " + i,
                        new Vector3(x0 + run * 0.5f, yLow + h * 0.5f, depth * 0.5f),
                        new Vector3(Mathf.Abs(run), h, depth), _config.dayStairsColor);
                }
            }
        }

        /// <summary>
        /// The staircase model for one flight: turned so its steps climb from <paramref name="bottom"/> toward
        /// <paramref name="top"/>, and stretched so the steps part spans that run, one floor of height and
        /// <paramref name="depth"/> from the open front.
        /// </summary>
        private void PlaceStairsModel(Transform root, string id, float bottom, float top, float yLow, float depth)
        {
            float dir = Mathf.Sign(top - bottom);
            var holder = new GameObject("Stairs " + id).transform;
            holder.SetParent(root, false);
            var pivot = new GameObject("Turn").transform;
            pivot.SetParent(holder, false);
            // Turn the model's climb direction onto world +x (or -x for a flight climbing left).
            var climb = _config.dayStairsModelClimb;
            climb.y = 0f;
            pivot.localRotation = Quaternion.FromToRotation(climb.sqrMagnitude > 0f ? climb.normalized : Vector3.back, new Vector3(dir, 0f, 0f));
            var model = Instantiate(_config.dayStairsModel, pivot, false);
            model.name = _config.dayStairsModel.name;

            // Fit in the holder's space (no scale yet): bounds of the steps part, or of the whole model.
            Bounds fit = default;
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (!string.IsNullOrEmpty(_config.dayStairsStepsPart) && r.name != _config.dayStairsStepsPart) continue;
                var bb = LocalBounds(holder, r.bounds);
                if (!any) fit = bb;
                else fit.Encapsulate(bb);
                any = true;
            }
            if (!any)
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    if (!any) fit = LocalBounds(holder, r.bounds);
                    else fit.Encapsulate(LocalBounds(holder, r.bounds));
                    any = true;
                }
            if (!any) return;
            var want = new Vector3(Mathf.Abs(top - bottom), _config.roomHeight, depth);
            var k = new Vector3(want.x / Mathf.Max(0.01f, fit.size.x), want.y / Mathf.Max(0.01f, fit.size.y), want.z / Mathf.Max(0.01f, fit.size.z));
            var wantMin = new Vector3(Mathf.Min(bottom, top), yLow, 0f);
            holder.localScale = k;
            holder.localPosition = wantMin - Vector3.Scale(fit.min, k);
        }

        /// <summary>A world-space box in <paramref name="space"/>'s local axes (no rotation between them).</summary>
        private static Bounds LocalBounds(Transform space, Bounds world)
        {
            var b = new Bounds(space.InverseTransformPoint(world.min), Vector3.zero);
            b.Encapsulate(space.InverseTransformPoint(world.max));
            return b;
        }

        /// <summary>
        /// Opens the upper floor over a flight: every floor slab of the kit at <paramref name="floorY"/> that covers
        /// the opening is replaced by stretched copies of itself around it, so the climbing character's head goes up
        /// through a stairwell, not through the boards.
        /// </summary>
        private void CutFloorOpening(Transform kit, DoorState door, float floorY, float depth)
        {
            float bottom = door.BottomXCm / 100f, top = door.TopXCm / 100f;
            float from = Mathf.Lerp(bottom, top, _config.dayStairsOpeningFrom);
            float x0 = Mathf.Min(from, top) - 0.05f, x1 = Mathf.Max(from, top) + 0.05f;
            float z1 = depth + 0.1f;
            var origin = transform.position;
            foreach (var r in kit.GetComponentsInChildren<MeshRenderer>())
            {
                var b = r.bounds;
                float bx0 = b.min.x - origin.x, bx1 = b.max.x - origin.x, bz0 = b.min.z - origin.z, bz1 = b.max.z - origin.z;
                // Flat decor lying on that floor (rugs, mats) over the opening would cover it: hide it.
                bool flat = b.size.y < 0.06f && Mathf.Abs(b.min.y - origin.y - floorY) < 0.1f;
                if (flat && bx1 > x0 && bx0 < x1 && bz0 < z1)
                {
                    r.gameObject.SetActive(false);
                    continue;
                }
                if (r.name.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (Mathf.Abs(b.max.y - origin.y - floorY) > 0.2f || bx1 <= x0 || bx0 >= x1 || bz0 >= z1) continue;
                float hx0 = Mathf.Max(x0, bx0), hx1 = Mathf.Min(x1, bx1);
                // Left and right of the opening (full depth), then behind it.
                if (hx0 - bx0 > 0.01f) FloorPiece(kit, r, new Rect(bx0, bz0, hx0 - bx0, bz1 - bz0));
                if (bx1 - hx1 > 0.01f) FloorPiece(kit, r, new Rect(hx1, bz0, bx1 - hx1, bz1 - bz0));
                if (bz1 - z1 > 0.01f) FloorPiece(kit, r, new Rect(hx0, z1, hx1 - hx0, bz1 - z1));
                r.gameObject.SetActive(false);
            }
        }

        /// <summary>A copy of floor slab <paramref name="slab"/> squeezed onto <paramref name="xz"/> (house x, z), same height.</summary>
        private void FloorPiece(Transform kit, MeshRenderer slab, Rect xz)
        {
            var b = slab.bounds;
            var origin = transform.position;
            var k = new Vector3(xz.width / Mathf.Max(0.001f, b.size.x), 1f, xz.height / Mathf.Max(0.001f, b.size.z));
            var wantMin = new Vector3(origin.x + xz.xMin, b.min.y, origin.z + xz.yMin);
            var squeeze = new GameObject(slab.name + " piece").transform;
            squeeze.SetParent(kit, true);
            squeeze.position = wantMin - Vector3.Scale(b.min, k);
            squeeze.localScale = k;
            // The copy keeps the slab's own world pose inside the squeeze, which stretches it in house x and z.
            var copy = Instantiate(slab.gameObject, squeeze, false);
            copy.name = slab.name;
            copy.transform.localPosition = slab.transform.position;
            copy.transform.localRotation = slab.transform.rotation;
            copy.transform.localScale = slab.transform.lossyScale;
        }

        /// <summary>Day mode: the neighbourhood around the house (sky, hills, trees, fence, soil under the ground).</summary>
        private void BuildScenery(Transform kitRoot, Camera cam)
        {
            float basementMin = float.MaxValue, basementMax = float.MinValue;
            foreach (var room in _sim.Rooms)
            {
                if (room.Floor != 0) continue;
                basementMin = Mathf.Min(basementMin, room.X);
                basementMax = Mathf.Max(basementMax, room.X + room.Width);
            }
            if (basementMin > basementMax) basementMin = basementMax = Bounds.center.x;
            var go = new GameObject("Scenery");
            go.transform.SetParent(transform, false);
            go.AddComponent<SceneryView>().Build(Bounds, HouseLayout.FloorY(1, _config.roomHeight),
                new Vector2(basementMin, basementMax), cam, TrippedUiConfig.LoadOrDefault(), kitRoot);
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
