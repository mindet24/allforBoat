using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// One room of the greybox house: floor, back wall (with an optional background picture), ceiling, name and a
    /// click collider (click = walk here). Rooms the player cannot see are darkened (minimal G1 Sight).
    /// </summary>
    public sealed class RoomView : MonoBehaviour
    {
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private RoomState _room;
        private TrippedViewConfig _config;
        private SpriteRenderer _overlay;
        private Material _backgroundMaterial;
        private bool _day;
        private TMPro.TextMeshPro _nameText;
        private TMPro.TextMeshPro _countdownText;
        private SpriteRenderer _barBack;
        private SpriteRenderer _barFill;
        private SpriteRenderer _panelGlow;
        private float _barLeft;
        private float _barWidth;
        private string _shownText = "";
        private string _shownCountdown = "";
        private int _shownFillKey = -1;

        /// <summary>The sim room this view shows.</summary>
        public RoomState Room => _room;

        /// <summary>Builds the room's pieces under this object, which sits at world origin.</summary>
        public void Build(RoomState room, bool isPanelRoom, TrippedViewConfig config, ArtLibrary art,
            IReadOnlyList<RoomState> allRooms = null, bool dayMode = false, bool greyboxShell = true, float panelX = float.NaN)
        {
            _room = room;
            _config = config;
            _day = dayMode;
            var rect = HouseLayout.RoomRect(room, config.roomHeight);
            float depth = config.roomDepth;

            // Day mode: labels and bars sit in front of the house so stairs, walls and kit props never cover them.
            float labelZ = dayMode ? config.dayOverlayZ : depth - 0.02f;
            if (greyboxShell) BuildGreybox(room, rect, config, art, allRooms);

            if (isPanelRoom)
            {
                float px = float.IsNaN(panelX) ? rect.xMin + 0.7f : panelX;
                float py = rect.yMin + 1.5f;
                var panelSprite = dayMode ? art.Prop(config.panelArtPath) : null;
                if (panelSprite != null)
                {
                    // The artist's panel (bottom-centre pivot, 1 unit tall) hung on the back wall at sim.PanelX.
                    float bottom = rect.yMin + config.panelMountHeight;
                    var go = new GameObject("Panel");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(px, bottom, depth - 0.06f);
                    float spriteH = panelSprite.rect.height / Mathf.Max(1f, panelSprite.pixelsPerUnit);
                    go.transform.localScale = Vector3.one * (config.panelWorldHeight / Mathf.Max(0.01f, spriteH));
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = panelSprite;
                    sr.sortingOrder = 21;
                    py = bottom + config.panelWorldHeight * 0.5f;
                }
                else
                {
                    var panelColor = config.panelColor;
                    Add(Prims.Box(transform, "Panel", new Vector3(px, py, depth - 0.08f),
                        new Vector3(0.55f, 0.75f, 0.16f), panelColor), panelColor);
                    Prims.Text(transform, "PanelLabel", new Vector3(px, py, depth - 0.17f),
                        new Vector2(0.5f, 0.2f), "PANEL", config.roomNameText, 1.2f).sortingOrder = 20;
                }
                // Glow behind the panel (blackout: reset me), and a click target on it.
                var glow = new GameObject("PanelGlow");
                glow.transform.SetParent(transform, false);
                glow.transform.localPosition = new Vector3(px, py, depth - 0.2f);
                _panelGlow = glow.AddComponent<SpriteRenderer>();
                _panelGlow.sprite = PlaceholderArt.WorldRounded;
                _panelGlow.drawMode = SpriteDrawMode.Sliced;
                _panelGlow.size = new Vector2(1.0f, 1.25f);
                _panelGlow.color = config.alertColor;
                _panelGlow.sortingOrder = 19;
                _panelGlow.enabled = false;
                var panelClick = new GameObject("PanelClick");
                panelClick.transform.SetParent(transform, false);
                panelClick.transform.localPosition = new Vector3(px, py, depth - 0.3f);
                ClickTarget.Add(panelClick, ClickKind.Panel, room.Id, room.Id, 3, Vector3.zero, new Vector3(0.9f, 1.1f, 0.5f));
            }

            BuildLabels(room, rect, config, dayMode, labelZ);
        }

        private void BuildGreybox(RoomState room, Rect rect, TrippedViewConfig config, ArtLibrary art, IReadOnlyList<RoomState> allRooms)
        {
            float depth = config.roomDepth;
            float floorT = config.floorThickness;
            float wallT = config.wallThickness;
            Add(Prims.Box(transform, "Floor", new Vector3(rect.center.x, rect.yMin - floorT * 0.5f, depth * 0.5f),
                new Vector3(rect.width, floorT, depth), config.floorColor), config.floorColor);
            Add(Prims.Box(transform, "BackWall", new Vector3(rect.center.x, rect.center.y, depth + wallT * 0.5f),
                new Vector3(rect.width, rect.height, wallT), config.backWallColor), config.backWallColor);
            // A ceiling only where no room above provides a floor (stacked floors would z-fight otherwise).
            var covers = new List<Vector2>();
            if (allRooms != null)
                foreach (var other in allRooms)
                    if (other.Floor == room.Floor + 1) covers.Add(new Vector2(other.X, other.X + other.Width));
            foreach (var span in HouseLayout.UncoveredSpans(rect.xMin, rect.xMax, covers))
                Add(Prims.Box(transform, "Ceiling", new Vector3((span.x + span.y) * 0.5f, rect.yMax + floorT * 0.5f, depth * 0.5f),
                    new Vector3(span.y - span.x, floorT, depth), config.slabColor), config.slabColor);

            var background = art.RoomBackground(room.Theme);
            if (background != null)
            {
                var quad = Prims.TexturedQuad(transform, "Background", new Vector3(rect.center.x, rect.center.y, depth - 0.01f),
                    new Vector2(rect.width, rect.height), background);
                _backgroundMaterial = quad.sharedMaterial;
                Add(quad, Color.white);
            }
        }

        private void BuildLabels(RoomState room, Rect rect, TrippedViewConfig config, bool dayMode, float labelZ)
        {
            float depth = config.roomDepth;
            // Inset from the left wall so the wall's side face (seen in perspective) never covers the name.
            _nameText = Prims.Text(transform, "Name", new Vector3(rect.xMin + 0.5f + rect.width * (dayMode ? 0.3f : 0.25f), rect.yMax - (dayMode ? 0.7f : 0.3f), labelZ),
                new Vector2(rect.width * (dayMode ? 0.6f : 0.5f), dayMode ? 0.42f : 0.35f), room.Name, config.roomNameText, dayMode ? 4f : 3f, TMPro.TextAlignmentOptions.Left);
            _nameText.sortingOrder = 20;
            if (dayMode)
            {
                // Load bar under the name, red over capacity, with the blackout countdown beside it.
                _barLeft = rect.xMin + 0.4f;
                _barWidth = rect.width * 0.5f;
                // Below the floor slab of the room above, which hangs 0.3 m into this room.
                float barY = rect.yMax - 1.12f;
                _barBack = MakeBar("LoadBarBack", new Vector3(_barLeft + _barWidth * 0.5f, barY, labelZ - 0.01f), 18);
                _barBack.color = new Color(0f, 0f, 0f, 0.55f);
                _barBack.size = new Vector2(_barWidth, 0.17f);
                _barFill = MakeBar("LoadBarFill", new Vector3(_barLeft, barY, labelZ - 0.02f), 19);
                _barFill.size = new Vector2(0.05f, 0.13f);
                _countdownText = Prims.Text(transform, "Countdown", new Vector3(_barLeft + _barWidth + 0.55f, barY, labelZ - 0.02f),
                    new Vector2(1.0f, 0.3f), "", config.alertColor, 3f, TMPro.TextAlignmentOptions.Left);
                _countdownText.sortingOrder = 20;
            }

            // Sight overlay (v1.1 §3.3): #1E1E24 at 85% over everything in the room, soft edges. Sockets and the
            // name are drawn above it (V4: the player always knows where the sockets are).
            var overlayGo = new GameObject("SightOverlay");
            overlayGo.transform.SetParent(transform, false);
            overlayGo.transform.localPosition = new Vector3(rect.center.x, rect.center.y, -0.05f);
            _overlay = overlayGo.AddComponent<SpriteRenderer>();
            _overlay.sprite = PlaceholderArt.SoftRect;
            _overlay.drawMode = SpriteDrawMode.Sliced;
            float grow = config.overlayFeather * 0.6f;
            _overlay.size = new Vector2(rect.width + grow, rect.height + grow);
            _overlay.color = config.hiddenRoomOverlay;
            _overlay.sortingOrder = 10;
            _overlay.enabled = false;

            // Click anywhere in the room volume to walk there. Appliances and sockets win over it (priority).
            ClickTarget.Add(gameObject, ClickKind.Room, room.Id, room.Id, 0,
                new Vector3(rect.center.x, rect.center.y, depth * 0.5f), new Vector3(rect.width, rect.height, depth));
        }

        /// <summary>
        /// Shows or hides the Sight overlay to match whether the player can see the room. In day mode it also keeps the
        /// room text with its load, the load bar and the blackout countdown up to date, and glows the panel in a blackout.
        /// </summary>
        public void Sync(HouseSim sim = null)
        {
            if (_room == null || _overlay == null) return;
            bool hidden = !_room.Visible;
            if (_overlay.enabled != hidden) _overlay.enabled = hidden;
            if (!_day || sim == null) return;

            var circuit = sim.FindCircuit(_room.Id);
            if (circuit != null)
            {
                string text = DayRules.RoomLoadText(_room.Name, circuit.Load, circuit.Capacity) + (circuit.IsOn ? "" : "  OFF");
                if (text != _shownText)
                {
                    _shownText = text;
                    _nameText.text = text;
                }
                float fill = HudFormat.Fill(circuit.Load, circuit.Capacity);
                var band = HudFormat.Band(circuit.Load, circuit.Capacity, _config.loadAmberAbove, _config.loadRedAbove);
                int key = (int)(fill * 100f) * 8 + (int)band;
                if (key != _shownFillKey)
                {
                    _shownFillKey = key;
                    float w = Mathf.Max(0.04f, _barWidth * fill);
                    _barFill.size = new Vector2(w, 0.13f);
                    var pos = _barFill.transform.localPosition;
                    _barFill.transform.localPosition = new Vector3(_barLeft + w * 0.5f, pos.y, pos.z);
                    _barFill.color = _config.LoadColor(band);
                }
                string countdown = "";
                if (circuit.Overloading)
                    countdown = HudFormat.SecondsToTrip(circuit.OverTicks, sim.Rules.overTripTicks, sim.Rules.ticksPerSecond).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s";
                if (countdown != _shownCountdown)
                {
                    _shownCountdown = countdown;
                    _countdownText.text = countdown;
                }
            }

            if (_panelGlow != null)
            {
                bool blackout = DayRules.IsBlackout(sim);
                if (_panelGlow.enabled != blackout) _panelGlow.enabled = blackout;
                if (blackout)
                {
                    var c = _config.alertColor;
                    c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 7f);
                    _panelGlow.color = c;
                }
            }
        }

        private SpriteRenderer MakeBar(string name, Vector3 localPos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.WorldRounded;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = order;
            return sr;
        }

        private void Add(Renderer r, Color unused)
        {
            _renderers.Add(r);
        }

        private void OnDestroy()
        {
            if (_backgroundMaterial != null) Destroy(_backgroundMaterial);
        }
    }
}
