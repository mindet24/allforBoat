using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// Alerts over rooms (GDD v1.1 §2.5): owner icon when a request is close to its deadline, "!" with the owner's
    /// line after a lost save, a routine about to start, the fridge-off countdown. An alert whose room is outside
    /// the house area of the screen becomes an arrow on the edge; clicking any alert pans the camera to its room
    /// (the character does not move). Overload is never shown here: it would give the wiring away.
    /// </summary>
    public sealed class AlertView
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

        private struct Interrupt
        {
            public string Owner;
            public string RoomId;
            public float Expires;
        }

        private readonly List<Item> _items = new List<Item>();
        private readonly List<Alert> _alerts = new List<Alert>();
        private readonly List<Interrupt> _interrupts = new List<Interrupt>();
        private readonly Dictionary<string, int> _perRoom = new Dictionary<string, int>();
        private RectTransform _root;
        private LevelController _controller;
        private TrippedViewConfig _config;
        private HudText _text;

        /// <summary>Number of alerts drawn last frame (for checks).</summary>
        public int ShownCount { get; private set; }

        /// <summary>How many of the drawn alerts are edge arrows (their room is off screen).</summary>
        public int ArrowCount { get; private set; }

        public void Build(RectTransform canvas, LevelController controller, TrippedViewConfig config, HudText text)
        {
            _controller = controller;
            _config = config;
            _text = text;
            _root = UIFactory.Stretch(UIFactory.Rect(canvas, "Alerts", 0, 0, 0, 0));
        }

        /// <summary>A lost save: shows "!" and the owner's line over the room for a few seconds.</summary>
        public void AddInterrupt(string owner, string roomId, float now)
        {
            _interrupts.Add(new Interrupt { Owner = owner, RoomId = roomId, Expires = now + _config.interruptAlertSeconds });
        }

        public void Sync(HouseSim sim, HouseCamera cam, float now)
        {
            AlertRules.Collect(sim, _config.deadlineWarnSeconds, _config.routineWarnSeconds, _alerts);
            for (int i = _interrupts.Count - 1; i >= 0; i--)
                if (_interrupts[i].Expires <= now) _interrupts.RemoveAt(i);
            foreach (var it in _interrupts)
                _alerts.Add(new Alert { Kind = AlertKind.Interrupted, RoomId = it.RoomId, Owner = it.Owner, TicksLeft = 0 });

            _perRoom.Clear();
            ShownCount = 0;
            ArrowCount = 0;
            if (cam == null || _root == null) { Hide(0); return; }

            var rect = _root.rect;
            var area = _config.houseArea;
            float m = _config.alertEdgeMargin;
            var inner = new Rect(area.x + m, area.y + m, area.width - 2f * m, area.height - 2f * m);
            int tps = sim.Rules.ticksPerSecond;

            for (int i = 0; i < _alerts.Count; i++)
            {
                var a = _alerts[i];
                var room = sim.FindRoom(a.RoomId);
                if (room == null) continue;
                var item = GetItem(ShownCount);
                ShownCount++;
                item.RoomId = a.RoomId;
                item.Root.gameObject.SetActive(true);

                // Stack alerts of one room side by side so they do not cover each other.
                _perRoom.TryGetValue(a.RoomId, out int stack);
                _perRoom[a.RoomId] = stack + 1;
                var worldPoint = new Vector2(room.CenterX - 0.3f + stack * 0.6f,
                    HouseLayout.FloorY(room.Floor, _config.roomHeight) + _config.roomHeight * 0.78f);
                var vp = cam.WorldToViewport(worldPoint);
                bool off = AlertRules.EdgePoint(vp, inner, out var edge, out float angle);
                var at = off ? edge : vp;
                item.Root.anchoredPosition = new Vector2(at.x * rect.width, -(1f - at.y) * rect.height);
                item.Arrow.gameObject.SetActive(off);
                if (off)
                {
                    ArrowCount++;
                    item.Arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
                    item.Arrow.anchoredPosition = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 44f;
                }
                Fill(item, a, sim, tps);
            }
            Hide(ShownCount);
        }

        private void Fill(Item item, Alert a, HouseSim sim, int tps)
        {
            string initial;
            string caption;
            Color color = _config.alertColor;
            switch (a.Kind)
            {
                case AlertKind.DeadlineSoon:
                    initial = AlertRules.Initial(a.Owner);
                    caption = a.Owner + " " + HudFormat.TimeLeft(sim.Now + a.TicksLeft, sim.Now, tps);
                    break;
                case AlertKind.RoutineSoon:
                    initial = AlertRules.Initial(a.Owner);
                    caption = a.Owner + " " + HudFormat.TimeLeft(sim.Now + a.TicksLeft, sim.Now, tps);
                    color = _config.loadAmber;
                    break;
                case AlertKind.FridgeOff:
                    initial = "❄";
                    caption = "FRIDGE " + HudFormat.TimeLeft(sim.Now + a.TicksLeft, sim.Now, tps);
                    break;
                default:
                    initial = "!";
                    caption = _text.Format("alert_save", ("owner", a.Owner));
                    break;
            }
            item.Circle.color = color;
            if (item.Initial.text != initial) item.Initial.text = initial;
            if (item.Caption.text != caption) item.Caption.text = caption;
            item.ArrowImage.color = color;
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
            item.Root = UIFactory.Anchored(_root, "Alert " + index, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f));
            item.Circle = UIFactory.Fill(item.Root, _config.alertColor, false, true);
            item.Circle.sprite = PlaceholderArt.Circle;
            var button = item.Root.gameObject.AddComponent<Button>();
            button.targetGraphic = item.Circle;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => _controller.PanToRoom(item.RoomId));
            item.Initial = UIFactory.LabelOn(UIFactory.Stretch(UIFactory.Rect(item.Root, "Initial", 0, 0, 52, 52)), "", 28, _config.textPrimary,
                TextAlignmentOptions.Center, true);
            var cap = UIFactory.Anchored(item.Root, "Caption", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(300f, 30f));
            item.Caption = UIFactory.LabelOn(cap, "", 20, _config.textPrimary, TextAlignmentOptions.Top, true);
            item.Caption.overflowMode = TextOverflowModes.Overflow;
            item.Arrow = UIFactory.Anchored(item.Root, "Arrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            item.ArrowImage = UIFactory.Fill(item.Arrow, _config.alertColor, false, false);
            item.ArrowImage.sprite = PlaceholderArt.Arrow;
            return item;
        }
    }
}
