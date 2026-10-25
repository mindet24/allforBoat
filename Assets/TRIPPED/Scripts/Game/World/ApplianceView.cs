using TMPro;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// An appliance standing on the floor next to its socket (dimmed while it is unplugged, day mode) or lying on the
    /// floor away from any socket (rules placeMode "floor"): the real sprite if Art/Appliances/{type}.png
    /// exists, otherwise a cream card with the short label (GDD v1.1 §3.6). Glows yellow when switched on and powered,
    /// blinks red when switched on without power. In a room the player cannot see, it shows what was last seen
    /// (Seen* fields) as a faint ghost with no glow, so hidden state never leaks.
    /// </summary>
    public sealed class ApplianceView : MonoBehaviour
    {
        private UnityEngine.Rendering.SortingGroup _depthGroup;
        private ApplianceState _app;
        private TrippedViewConfig _config;
        private SpriteRenderer _glow;
        private SpriteRenderer _outline;
        private SpriteRenderer _card;
        private TextMeshPro _label;
        private bool _hasArt;
        private float _width;
        private float _height;
        private ClickTarget _target;
        private TextMeshPro _seenText;
        private int _shownSeenSecond = -2;
        private bool _ghostOrder;
        private Vector3 _flyFrom;
        private float _flyStart = float.NegativeInfinity;

        /// <summary>The sim appliance this view shows.</summary>
        public ApplianceState Appliance => _app;

        /// <summary>Room the appliance is drawn in, or null while it is not in a socket.</summary>
        public string ShownRoomId => gameObject.activeSelf ? _target.RoomId : null;

        /// <summary>Left and right x of the drawn appliance in the house's local space.</summary>
        public Vector2 SpanX => new Vector2(transform.localPosition.x - _width * 0.5f, transform.localPosition.x + _width * 0.5f);

        /// <summary>
        /// Draws the appliance flying in an arc from <paramref name="from"/> (house-local) to where it lands, for a
        /// throw. The sim already has it at the landing spot; this only moves the drawing for a moment.
        /// </summary>
        public void FlyFrom(Vector3 from, float time)
        {
            _flyFrom = from;
            _flyStart = time;
        }

        /// <summary>Middle of the top edge of the drawn appliance, in the house's local space (for the "E" prompt).</summary>
        public Vector3 TopPoint => transform.localPosition + new Vector3(0f, _height, 0f);

        /// <summary>Builds the sprite layers and click collider.</summary>
        public void Build(ApplianceState app, ApplianceDef def, TrippedViewConfig config, ArtLibrary art, bool showLoad = false)
        {
            _app = app;
            _config = config;
            float height = def != null && def.worldHeight > 0f ? def.worldHeight : 0.6f;
            if (showLoad) height *= 1.4f; // day mode: bigger, the player reads loads from the house
            _height = height;
            var sprite = art.Appliance(app.Type);
            _hasArt = sprite != null;

            if (_hasArt)
            {
                // Imported with pivot at the bottom centre and 1 unit tall (SpriteImportPostprocessor).
                float aspect = sprite.rect.height > 0 ? sprite.rect.width / sprite.rect.height : 1f;
                _width = height * aspect;
                _glow = MakeSprite("Glow", sprite, 0, new Vector3(0f, -height * 0.04f, 0.02f), Vector3.one * height * 1.1f);
                _card = MakeSprite("Sprite", sprite, 2, Vector3.zero, Vector3.one * height);
                SpriteStyle.Outline(_card, config);
                SpriteStyle.BlobShadow(transform, _width * 0.9f, config);
                if (showLoad)
                {
                    // Day mode: the load the appliance draws, on a small badge at its top right.
                    var badge = MakeSliced("LoadBadge", PlaceholderArt.WorldRounded, 3, new Vector2(0.34f, 0.26f),
                        new Vector3(_width * 0.38f, height * 0.92f, -0.02f));
                    badge.color = config.applianceCard;
                    _label = Prims.Text(transform, "Load", new Vector3(_width * 0.38f, height * 0.92f, -0.03f), new Vector2(0.3f, 0.22f),
                        app.Load.ToString(), config.worldText, 2.4f);
                    _label.sortingOrder = 4;
                }
            }
            else
            {
                _width = Mathf.Clamp(height * 0.85f, 0.45f, 0.9f);
                var rounded = PlaceholderArt.WorldRounded;
                _glow = MakeSliced("Glow", rounded, 0, new Vector2(_width + 0.14f, height + 0.14f), new Vector3(0f, height * 0.5f, 0.02f));
                _outline = MakeSliced("Outline", rounded, 1, new Vector2(_width + 0.04f, height + 0.04f), new Vector3(0f, height * 0.5f, 0.01f));
                _outline.color = config.applianceOutline;
                _card = MakeSliced("Card", rounded, 2, new Vector2(_width, height), new Vector3(0f, height * 0.5f, 0f));
                string label = def != null && !string.IsNullOrEmpty(def.shortLabel) ? def.shortLabel : app.Type.ToUpperInvariant();
                if (showLoad) label += "\n" + app.Load; // day mode: the player must know what each appliance draws
                _label = Prims.Text(transform, "Label", new Vector3(0f, height * 0.5f, -0.01f), new Vector2(_width * 0.85f, height * 0.5f),
                    label, config.worldText, 3f);
                _label.sortingOrder = 3;
            }
            _glow.enabled = false;
            // "last seen m:ss" under a ghost (V4); drawn above the Sight overlay (order 10) so it can be read.
            _seenText = Prims.Text(transform, "Seen", new Vector3(0f, -0.12f, -0.02f), new Vector2(1.4f, 0.22f), "", _config.roomNameText, 1.2f);
            _seenText.sortingOrder = 24;
            _seenText.gameObject.SetActive(false);
            _target = ClickTarget.Add(gameObject, ClickKind.WorldAppliance, app.Id, "", 3,
                new Vector3(0f, height * 0.5f, 0f), new Vector3(_width + 0.1f, height + 0.1f, 0.3f));
        }

        /// <summary>
        /// Places and colours the appliance from what the player can see. Hidden when the player last saw it in
        /// the tray (or its socket is gone).
        /// </summary>
        public void Sync(HouseSim sim, bool selected, float time)
        {
            string socketId = _app.SeenSocketId ?? "";
            string floorRoomId = _app.SeenFloorRoomId ?? "";
            var socket = socketId.Length > 0 ? sim.FindSocket(socketId) : null;
            var room = socket != null ? sim.FindRoom(socket.RoomId) : floorRoomId.Length > 0 ? sim.FindRoom(floorRoomId) : null;
            if (room == null)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            _target.RoomId = room.Id;
            float x = socket != null
                ? HouseLayout.SocketX(room.X, room.Width, socket.Pos) + _config.socketSize * 0.5f + _width * 0.5f + 0.06f
                : _app.SeenFloorX;
            // Day mode: where the sim reaches it from, at the back of the walkable floor (in a socket) or where it lies,
            // so the character walks up to it. Taller appliances sit a hair further back, so a kettle beside the fridge
            // always draws in front of it (same sorting order otherwise ties and flips with the camera).
            float depth = socket != null ? sim.Rules.walkDepthMetres : _app.SeenFloorZ;
            float z = sim.IsDayMode ? _config.dayFrontZ + depth + _height * 0.02f : _config.roomDepth - _config.applianceInset;
            var landed = new Vector3(x, HouseLayout.FloorY(room.Floor, _config.roomHeight), z);
            if (sim.IsDayMode)
            {
                // Its layers (glow, card, label) sort as one, by depth with the characters.
                if (_depthGroup == null) _depthGroup = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
                _depthGroup.sortingOrder = HouseLayout.DepthSortingOrder(z);
            }
            float flight = (time - _flyStart) / Mathf.Max(0.01f, _config.throwFlightSeconds);
            transform.localPosition = flight < 1f
                ? Vector3.Lerp(_flyFrom, landed, flight) + Vector3.up * (4f * flight * (1f - flight) * _config.throwArcHeight)
                : landed;

            bool ghost = !room.Visible;
            SetGhostOrder(ghost);
            UpdateSeenText(sim, ghost);
            bool on = _app.SeenSwitchedOn;
            bool powered = _app.SeenPowered;
            float alpha = ghost ? _config.ghostAlpha : 1f;

            Color cardColor = _hasArt ? Color.white : (on ? _config.applianceCard : _config.applianceOffCard);
            if (_hasArt && on && !powered && !ghost) cardColor = new Color(0.6f, 0.6f, 0.6f);
            // Day mode: not plugged in (put down with E, or lying on the floor) draws nothing until F.
            if (sim.IsDayMode && (_app.SeenUnplugged || socket == null) && !ghost) cardColor = _config.unpluggedTint;
            cardColor.a = alpha;
            _card.color = cardColor;
            if (_outline != null)
            {
                var o = _config.applianceOutline;
                o.a = alpha;
                _outline.color = o;
            }
            if (_label != null)
            {
                var t = _config.worldText;
                t.a = alpha;
                _label.color = t;
            }

            // Glow: selection wins, then on+powered, then on without power (blinking 2 Hz).
            Color glow = default;
            bool showGlow = false;
            if (selected)
            {
                glow = _config.selection;
                glow.a = 0.6f + 0.4f * Mathf.Sin(time * 8f);
                showGlow = true;
            }
            else if (!ghost && on && powered)
            {
                glow = _config.glowOn;
                showGlow = true;
            }
            else if (!ghost && on && !powered && Mathf.Repeat(time * 2f, 1f) < 0.5f)
            {
                glow = _config.glowNoPower;
                showGlow = true;
            }
            _glow.enabled = showGlow;
            if (showGlow) _glow.color = glow;
        }

        /// <summary>Ghosts are lifted above the Sight overlay so they stay readable as faint shapes.</summary>
        private void SetGhostOrder(bool ghost)
        {
            if (ghost == _ghostOrder) return;
            _ghostOrder = ghost;
            int lift = ghost ? 14 : 0;
            _glow.sortingOrder = 0 + lift;
            _card.sortingOrder = 2 + lift;
            if (_outline != null) _outline.sortingOrder = 1 + lift;
            if (_label != null) _label.sortingOrder = 3 + lift;
        }

        private void UpdateSeenText(HouseSim sim, bool ghost)
        {
            bool show = ghost && _app.SeenTick >= 0;
            if (_seenText.gameObject.activeSelf != show) _seenText.gameObject.SetActive(show);
            if (!show) return;
            int tps = sim.Rules.ticksPerSecond;
            int second = _app.SeenTick / Mathf.Max(1, tps);
            if (second == _shownSeenSecond) return;
            _shownSeenSecond = second;
            _seenText.text = "seen " + HudFormat.Clock(_app.SeenTick, tps);
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, int order, Vector3 localPos, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        private SpriteRenderer MakeSliced(string name, Sprite sprite, int order, Vector2 size, Vector3 localPos)
        {
            var sr = MakeSprite(name, sprite, order, localPos, Vector3.one);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            return sr;
        }
    }
}
