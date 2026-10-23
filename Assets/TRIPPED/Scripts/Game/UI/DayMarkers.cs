using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// A marker over the room of every active day request: the person's initial in the urgency colour, with the
    /// appliance wanted and the time left to start it. A marker whose room is off screen becomes an arrow on the edge
    /// of the house area; clicking a marker pans the camera to that room.
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
        private readonly Dictionary<string, int> _count = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>();
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

            _count.Clear();
            _index.Clear();
            foreach (var r in sim.Requests)
            {
                if (r.Serial <= 0 || !r.IsActive) continue;
                _count.TryGetValue(r.RoomId, out int n);
                _count[r.RoomId] = n + 1;
            }

            var rect = _root.rect;
            var area = _config.dayHouseArea;
            var inner = new Rect(area.x + 0.02f, area.y + 0.04f, area.width - 0.04f, area.height - 0.08f);
            foreach (var r in sim.Requests)
            {
                if (r.Serial <= 0 || !r.IsActive) continue;
                var room = sim.FindRoom(r.RoomId);
                if (room == null) continue;
                _index.TryGetValue(r.RoomId, out int slot);
                _index[r.RoomId] = slot + 1;
                int count = _count[r.RoomId];

                var item = GetItem(ShownCount++);
                item.RoomId = r.RoomId;
                item.Root.gameObject.SetActive(true);

                // Several requests in one room sit side by side.
                float worldX = room.CenterX + (slot - 0.5f * (count - 1)) * 0.95f;
                float worldY = HouseLayout.FloorY(room.Floor, _config.roomHeight) + 1.55f;
                var vp = cam.WorldToViewport(new Vector2(worldX, worldY));
                bool off = AlertRules.EdgePoint(vp, inner, out var edge, out float angle);
                var at = off ? edge : vp;
                item.Root.anchoredPosition = new Vector2(at.x * rect.width, -(1f - at.y) * rect.height);
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
                // Captions of markers in the same room are listed one under the other so they never overlap.
                item.Caption.rectTransform.anchoredPosition = new Vector2(0f, -2f - slot * 24f);
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
            item.Root = UIFactory.Anchored(_root, "Marker " + index, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            item.Circle = UIFactory.Fill(item.Root, _config.alertColor, false, true);
            item.Circle.sprite = PlaceholderArt.Circle;
            var button = item.Root.gameObject.AddComponent<Button>();
            button.targetGraphic = item.Circle;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => _controller.PanToRoom(item.RoomId));
            item.Initial = UIFactory.LabelOn(UIFactory.Stretch(UIFactory.Rect(item.Root, "Initial", 0, 0, 46, 46)), "", 26, Color.white, TextAlignmentOptions.Center, true);
            var cap = UIFactory.Anchored(item.Root, "Caption", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(240f, 28f));
            item.Caption = UIFactory.LabelOn(cap, "", 18, _config.textPrimary, TextAlignmentOptions.Top, true);
            item.Caption.overflowMode = TextOverflowModes.Overflow;
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
