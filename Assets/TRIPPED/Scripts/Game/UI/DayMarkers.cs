using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// A marker in the room of every active day request: the person's initial in the urgency colour, with the
    /// appliance wanted and the time left to start it. Markers sit in one fixed row per room at the label's height,
    /// side by side from the right wall (<see cref="MarkerLayout"/>), clear of key prompts, the character's head and
    /// room labels. A marker whose room is off screen becomes an arrow on the edge of the house area; clicking a
    /// marker pans the camera to that room.
    /// </summary>
    public sealed class DayMarkers
    {
        private sealed class Item
        {
            public RectTransform Root;
            public Image Circle;
            public TextMeshProUGUI Initial;
            public TextMeshProUGUI Caption;
            public RectTransform Arrow;
            public Image ArrowImage;
            public string RoomId = "";
        }

        private readonly List<Item> _items = new List<Item>();
        private readonly List<RequestState> _roomRequests = new List<RequestState>();
        private readonly List<Rect> _keepClearWorld = new List<Rect>();
        private readonly List<Rect> _keepClear = new List<Rect>();
        private RectTransform _root;
        private LevelController _controller;
        private TrippedViewConfig _config;

        /// <summary>Markers drawn last frame (for checks).</summary>
        public int ShownCount { get; private set; }

        public void Build(RectTransform canvas, LevelController controller, TrippedViewConfig config)
        {
            _controller = controller;
            _config = config;
            _root = UIFactory.Stretch(UIFactory.Rect(canvas, "DayMarkers", 0, 0, 0, 0));
        }

        public void Sync(HouseSim sim, HouseCamera cam)
        {
            ShownCount = 0;
            // Nothing to point at once the day is over.
            if (cam == null || _root == null || sim.Phase == LevelPhase.Won || sim.Phase == LevelPhase.Lost) { Hide(0); return; }
            int tps = sim.Rules.ticksPerSecond;
            var rect = _root.rect;
            float w = rect.width, h = rect.height;

            // What the markers must never cover, in canvas pixels (y up): key prompts, the character's head and
            // what it carries, and the room labels.
            _keepClearWorld.Clear();
            _controller.House?.CollectKeepClear(_keepClearWorld);
            _keepClear.Clear();
            foreach (var wr in _keepClearWorld)
            {
                var a = cam.WorldToViewport(new Vector2(wr.xMin, wr.yMin));
                var b = cam.WorldToViewport(new Vector2(wr.xMax, wr.yMax));
                _keepClear.Add(Rect.MinMaxRect(a.x * w, a.y * h, b.x * w, b.y * h));
            }

            var area = _config.dayHouseArea;
            var inner = new Rect(area.x + 0.02f, area.y + 0.04f, area.width - 0.04f, area.height - 0.08f);
            foreach (var room in sim.Rooms)
            {
                _roomRequests.Clear();
                foreach (var r in sim.Requests)
                    if (r.Serial > 0 && r.IsActive && r.RoomId == room.Id) _roomRequests.Add(r);
                if (_roomRequests.Count == 0) continue;

                // One fixed row per room at the label's height, filled from the right wall leftwards.
                float rowWorldY = HouseLayout.FloorY(room.Floor, _config.roomHeight) + _config.roomHeight - _config.markerRowBelowCeiling;
                var left = cam.WorldToViewport(new Vector2(room.X + 0.2f, rowWorldY));
                var right = cam.WorldToViewport(new Vector2(room.X + room.Width - 0.25f, rowWorldY));
                var slots = MarkerLayout.Row(left.x * w, right.x * w, right.y * h, _config.markerSize, _config.markerGap,
                    _roomRequests.Count, _keepClear);

                for (int i = 0; i < _roomRequests.Count; i++)
                {
                    var r = _roomRequests[i];
                    var item = GetItem(ShownCount++);
                    item.RoomId = r.RoomId;
                    item.Root.gameObject.SetActive(true);

                    var centre = slots[i].center;
                    var vp = new Vector2(centre.x / Mathf.Max(1f, w), centre.y / Mathf.Max(1f, h));
                    bool off = AlertRules.EdgePoint(vp, inner, out var edge, out float angle);
                    var at = off ? edge : vp;
                    item.Root.anchoredPosition = new Vector2(at.x * w, -(1f - at.y) * h);
                    item.Arrow.gameObject.SetActive(off);
                    if (off)
                    {
                        item.Arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
                        item.Arrow.anchoredPosition = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 40f;
                    }

                    var color = _config.UrgencyColor(DayRules.ParseUrgency(r.Urgency));
                    item.Circle.color = color;
                    item.ArrowImage.color = color;
                    string initial = AlertRules.Initial(r.Owner);
                    if (item.Initial.text != initial) item.Initial.text = initial;
                    string caption = r.ProgressTicks > 0
                        ? NameOf(sim, r.ApplianceId)
                        : NameOf(sim, r.ApplianceId) + " " + HudFormat.TimeLeft(r.DueTicks, sim.Now, tps);
                    if (item.Caption.text != caption) item.Caption.text = caption;
                }
            }
            Hide(ShownCount);
        }

        private Item GetItem(int index)
        {
            while (_items.Count <= index) _items.Add(MakeItem(_items.Count));
            return _items[index];
        }

        private void Hide(int from)
        {
            for (int i = from; i < _items.Count; i++)
                if (_items[i].Root.gameObject.activeSelf) _items[i].Root.gameObject.SetActive(false);
        }

        private Item MakeItem(int index)
        {
            var item = new Item();
            // A pill: the person's initial in an urgency-coloured circle on the left, the caption beside it.
            var size = _config.markerSize;
            item.Root = UIFactory.Anchored(_root, "Marker " + index, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var back = UIFactory.Fill(item.Root, new Color(0.12f, 0.12f, 0.14f, 0.82f), false, true);
            back.sprite = PlaceholderArt.UiRounded;
            back.type = Image.Type.Sliced;
            var circleRect = UIFactory.Anchored(item.Root, "Circle", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(size.y, size.y));
            item.Circle = UIFactory.Fill(circleRect, _config.alertColor, false, true);
            item.Circle.sprite = PlaceholderArt.Circle;
            var button = item.Root.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => _controller.PanToRoom(item.RoomId));
            item.Initial = UIFactory.LabelOn(UIFactory.Stretch(UIFactory.Rect(circleRect, "Initial", 0, 0, size.y, size.y)), "", size.y * 0.6f, Color.white, TextAlignmentOptions.Center, true);
            var cap = UIFactory.Anchored(item.Root, "Caption", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(size.y + 4f, 0f), new Vector2(size.x - size.y - 8f, size.y));
            item.Caption = UIFactory.LabelOn(cap, "", 17, _config.textPrimary, TextAlignmentOptions.MidlineLeft, true);
            item.Caption.enableAutoSizing = true;
            item.Caption.fontSizeMin = 11;
            item.Caption.fontSizeMax = 17;
            item.Arrow = UIFactory.Anchored(item.Root, "Arrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            item.ArrowImage = UIFactory.Fill(item.Arrow, _config.alertColor, false, false);
            item.ArrowImage.sprite = PlaceholderArt.Arrow;
            return item;
        }

        private static string NameOf(HouseSim sim, string applianceId)
        {
            var app = sim.FindAppliance(applianceId);
            return app == null ? applianceId : sim.Catalog.Find(app.Type)?.name ?? app.Type;
        }
    }
}
