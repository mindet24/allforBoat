using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// The art sheet's power panel, top-right under the hearts: a "⚡ Room power" caption, then one row per room with
    /// its colour, name, used/cap and a bar. There is no house total: breakers trip per room, so a total would only
    /// compete with the number that matters. While following, only the current room (highlighted) and any overloading room are listed;
    /// the overview (Tab) lists every room. A red "⚠ Overload!" badge shows while any room is over capacity.
    /// </summary>
    public sealed class PowerPanel
    {
        private sealed class Row
        {
            public string Id;
            public RectTransform Root;
            public Image Highlight;
            public Image Fill;
            public TextMeshProUGUI Count;
            public Color Color;
            public string Shown = "";
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly List<RoomPower> _power = new List<RoomPower>();
        private DayHudContext _cx;
        private RectTransform _root;
        private GameObject _badge;
        private int _shownRows = -1;
        private readonly Vector3[] _corners = new Vector3[4];

        /// <summary>Rows listed last frame (for checks).</summary>
        public int RowsShown { get; private set; }

        public void Build(DayHudContext cx, HouseSim sim)
        {
            _cx = cx;
            var ui = cx.Ui;
            float w = ui.powerPanelWidth;
            var body = UiKit.Card(cx.Canvas, "PowerPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-ui.edgeInset, -ui.powerPanelTop),
                new Vector2(w, ui.powerHeaderHeight), ui.panel, ui);
            _root = (RectTransform)body.transform.parent;
            var t = body.transform;

            float bolt = ui.powerHeaderHeight - 16f;
            UiKit.Icon(t, "Bolt", 12, 8, bolt, bolt, UiIcons.Lightning, Color.white);
            UiKit.Text(t, "Caption", 20 + bolt, 4, w - 36f - bolt, ui.powerHeaderHeight - 8f, cx.Text.Get("g7_room_power"), 28, ui.creamDark, TextAlignmentOptions.MidlineLeft);

            int i = 0;
            foreach (var room in sim.Rooms)
            {
                if (sim.FindCircuit(room.Id) == null) continue;
                _rows.Add(MakeRow(t, room, ui.roomColors != null && ui.roomColors.Length > 0 ? ui.roomColors[i % ui.roomColors.Length] : ui.sky));
                i++;
            }

            // "⚠ Overload!" badge hanging off the panel's left edge, tilted like the sheet.
            var badge = UiKit.Card(_root, "Overload", new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(26f, -30f), new Vector2(230f, 58f), ui.red, ui);
            var bt = badge.transform;
            UiKit.Icon(bt, "Warning", 10, 7, 44, 44, UiIcons.Warning, Color.white);
            UiKit.Text(bt, "Text", 58, 0, 166, 58, cx.Text.Get("g7_overload"), 32, Color.white, TextAlignmentOptions.MidlineLeft);
            _badge = badge.transform.parent.gameObject;
            _badge.transform.localRotation = Quaternion.Euler(0f, 0f, -6f);
            _badge.SetActive(false);
        }

        private Row MakeRow(Transform panel, RoomState room, Color color)
        {
            var ui = _cx.Ui;
            float w = ui.powerPanelWidth, h = ui.powerRowHeight;
            var row = new Row { Id = room.Id, Color = color };
            row.Root = UIFactory.Rect(panel, "Room " + room.Id, 0, 0, w, h);
            row.Highlight = UIFactory.Fill(UIFactory.Stretch(UIFactory.Rect(row.Root, "Highlight", 0, 0, 0, 0)), ui.rowHighlight, false, false);
            var dot = UIFactory.Panel(row.Root, "Dot", 16, (h - 26f) * 0.5f - 4f, 26, 26, color, false, false);
            dot.sprite = UiArt.Rounded;
            dot.type = Image.Type.Sliced;
            dot.pixelsPerUnitMultiplier = 4f;
            UiKit.Icon(dot.transform, "Plug", 3, 3, 20, 20, UiIcons.Plug, Color.white);
            var name = UiKit.Text(row.Root, "Name", 52, 2, w - 150f, h - 16f, room.Name, 24, Color.white, TextAlignmentOptions.MidlineLeft);
            name.overflowMode = TextOverflowModes.Ellipsis;
            row.Count = UiKit.Text(row.Root, "Count", w - 104f, 2, 88, h - 16f, "", 24, Color.white, TextAlignmentOptions.MidlineRight);
            var barBack = UIFactory.Panel(row.Root, "Bar", 52, h - 14f, w - 68f, 8, ui.navyLight, false, false);
            barBack.sprite = UiArt.Pill;
            barBack.type = Image.Type.Sliced;
            row.Fill = UIFactory.Fill(UIFactory.Stretch(UIFactory.Rect(barBack.transform, "Fill", 0, 0, 0, 0)), color, false, false);
            row.Fill.sprite = UiArt.Pill;
            row.Fill.type = Image.Type.Sliced;
            return row;
        }

        /// <summary>Adds the panel's canvas-local rect (origin at the canvas centre) for meters to keep clear of.</summary>
        public void CollectRect(List<Rect> into)
        {
            if (_root == null || !_root.gameObject.activeInHierarchy) return;
            _root.GetWorldCorners(_corners);
            var a = _cx.Canvas.InverseTransformPoint(_corners[0]);
            var b = _cx.Canvas.InverseTransformPoint(_corners[2]);
            into.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)));
        }

        public void Sync(HouseSim sim, float now)
        {
            var ui = _cx.Ui;
            var cfg = _cx.Config;
            _power.Clear();
            foreach (var row in _rows)
            {
                var c = sim.FindCircuit(row.Id);
                _power.Add(new RoomPower { Id = row.Id, Load = c.Load, Capacity = c.Capacity, On = c.IsOn, Overloading = c.Overloading });
            }
            var cam = _cx.Controller.HouseCam;
            bool following = cam != null && cam.DayFollowing && !cam.Overview;
            var ch = sim.Character;
            string current = ch.RoomId;
            var show = PowerPanelRules.RowsToShow(_power, following, current);

            float y = ui.powerHeaderHeight + 4f;
            int shown = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var p = _power[i];
                bool visible = show.Contains(row.Id);
                if (row.Root.gameObject.activeSelf != visible) row.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                shown++;
                row.Root.anchoredPosition = new Vector2(0f, -y);
                y += ui.powerRowHeight;
                row.Highlight.enabled = row.Id == current;
                string count = p.On ? p.Load + "/" + p.Capacity : _cx.Text.Get("g6_off");
                if (count != row.Shown)
                {
                    row.Shown = count;
                    row.Count.text = count;
                }
                var rb = HudFormat.Band(p.Load, p.Capacity, cfg.loadAmberAbove, cfg.loadRedAbove);
                row.Count.color = !p.On || rb == LoadBand.Red ? ui.red : Color.white;
                UIFactory.SetBar(row.Fill, p.On ? HudFormat.Fill(p.Load, p.Capacity) : 0f);
                row.Fill.color = rb == LoadBand.Green ? row.Color : rb == LoadBand.Amber ? ui.accent : ui.red;
            }
            if (shown != _shownRows)
            {
                _shownRows = shown;
                _root.sizeDelta = new Vector2(ui.powerPanelWidth, ui.powerHeaderHeight + (shown > 0 ? shown * ui.powerRowHeight + 10f : 0f));
            }
            RowsShown = shown;

            bool overload = PowerPanelRules.AnyOverload(_power);
            if (_badge.activeSelf != overload) _badge.SetActive(overload);
            if (overload) _badge.transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(now * ui.overloadPulseSpeed));
        }
    }
}
