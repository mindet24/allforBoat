using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// The tray along the bottom (GDD v1.0 §6.2): a chip for every appliance that is not plugged in (lamps
    /// included), the extension-cord count, and a one-line hint. Clicking a chip selects that appliance; the
    /// player then clicks a socket in the house to plug it in.
    /// </summary>
    public sealed class TrayView
    {
        private const float ChipW = 140f, ChipH = 110f, Gap = 16f, Left = 24f, Top = 45f;

        private sealed class Chip
        {
            public string ApplianceId;
            public GameObject Root;
            public Image Ring;
        }

        private readonly List<Chip> _chips = new List<Chip>();
        private readonly List<string> _wanted = new List<string>();
        private RectTransform _tray;
        private LevelController _controller;
        private TrippedViewConfig _config;
        private HudText _text;
        private TextMeshProUGUI _cords;
        private TextMeshProUGUI _hint;
        private RectTransform _cordChip;
        private Image _cordRing;
        private string _shownHint = "~";
        private int _shownCords = -1;

        /// <summary>The tray strip, so other HUD parts can sit inside it.</summary>
        public RectTransform Root => _tray;

        /// <summary>Builds the tray strip.</summary>
        public void Build(RectTransform canvas, LevelController controller, TrippedViewConfig config, HudText text)
        {
            _controller = controller;
            _config = config;
            _text = text;
            _tray = UIFactory.Anchored(canvas, "Tray", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 200f));
            _tray.anchorMin = new Vector2(0f, 0f);
            _tray.anchorMax = new Vector2(1f, 0f);
            _tray.sizeDelta = new Vector2(0f, 200f);
            var bg = config.hudBackground;
            bg.a = 0.94f;
            UIFactory.Fill(_tray, bg, false);

            _hint = UIFactory.Label(_tray, "Hint", Left, 6, 1800, 34, "", 20, config.textSecondary);
            var cordButton = UIFactory.Button(_tray, "Cords", 0, Top, ChipW, ChipH, "", 24, config.hudPanel, config.textSecondary,
                () => _controller.HandleClick(new ClickInput { Kind = ClickKind.TrayCord }));
            _cordChip = (RectTransform)cordButton.transform;
            _cords = cordButton.GetComponentInChildren<TextMeshProUGUI>();
            _cordRing = UIFactory.Panel(_cordChip, "Selected", -6, -6, ChipW + 12, ChipH + 12, config.selection, true, false);
            _cordRing.sprite = PlaceholderArt.UiRoundedOutline;
            _cordRing.enabled = false;
        }

        /// <summary>Rebuilds the chips when the set of unplugged appliances changes; updates selection and counts.</summary>
        public void Sync(HouseSim sim)
        {
            _wanted.Clear();
            bool day = sim.IsDayMode;
            // Day mode: every appliance stays in a socket (or in the hands), so the tray has no chips and no cord.
            if (_cordChip.gameObject.activeSelf == day) _cordChip.gameObject.SetActive(!day);
            foreach (var app in sim.Appliances)
                if (!day && !app.Fixed && string.IsNullOrEmpty(app.SocketId)) _wanted.Add(app.Id);

            bool same = _wanted.Count == _chips.Count;
            for (int i = 0; same && i < _wanted.Count; i++) same = _wanted[i] == _chips[i].ApplianceId;
            if (!same) Rebuild(sim);

            string selection = _controller.Selection;
            foreach (var chip in _chips) chip.Ring.enabled = chip.ApplianceId == selection;
            var holding = _controller.Holding;
            _cordRing.enabled = holding.Kind == HeldKind.Cord || holding.Kind == HeldKind.CordFrom;

            if (sim.CordsLeft != _shownCords)
            {
                _shownCords = sim.CordsLeft;
                _cords.text = _text.Format("hud_cords", ("n", sim.CordsLeft));
            }
            string hintKey = holding.Kind == HeldKind.Cord ? "hud_cord_selected"
                : holding.Kind == HeldKind.CordFrom ? "hud_cord_room|" + holding.Id
                : selection == null ? (day ? "day_hint" : "hud_tray_help") : "hud_selected|" + selection;
            if (hintKey != _shownHint)
            {
                _shownHint = hintKey;
                if (holding.Kind == HeldKind.Cord) _hint.text = _text.Get("hud_cord_selected");
                else if (holding.Kind == HeldKind.CordFrom) _hint.text = _text.Format("hud_cord_room", ("socket", holding.Id));
                else if (selection == null) _hint.text = _text.Get(day ? "day_hint" : "hud_tray_help");
                else _hint.text = _text.Format("hud_selected", ("appliance", NameOf(sim, selection)));
                _hint.color = holding.Kind == HeldKind.None ? _config.textSecondary : _config.selection;
            }
        }

        private void Rebuild(HouseSim sim)
        {
            foreach (var chip in _chips) Object.Destroy(chip.Root);
            _chips.Clear();
            float x = Left;
            foreach (var id in _wanted)
            {
                var app = sim.FindAppliance(id);
                var def = sim.Catalog.Find(app.Type);
                string appId = id;
                var button = UIFactory.Button(_tray, "Chip " + id, x, Top, ChipW, ChipH, "", 1, _config.applianceCard, _config.worldText,
                    () => _controller.HandleClick(new ClickInput { Kind = ClickKind.TrayAppliance, Id = appId }));
                var t = button.transform;
                UIFactory.Label(t, "Short", 0, 14, ChipW, 44, def != null ? def.shortLabel : app.Type, 32, _config.worldText, TextAlignmentOptions.Center, true);
                UIFactory.Label(t, "Name", 6, 60, ChipW - 12, 26, def != null ? def.name : app.Type, 16, _config.worldText, TextAlignmentOptions.Center);
                UIFactory.Label(t, "Load", 0, 2, ChipW - 10, 22, app.Load.ToString(), 18, _config.worldText, TextAlignmentOptions.TopRight, true);
                string owner = string.IsNullOrEmpty(app.Owner) ? "" : app.Owner;
                UIFactory.Label(t, "Owner", 6, 84, ChipW - 12, 22, owner, 15, _config.worldText, TextAlignmentOptions.Center);
                var ring = UIFactory.Panel(t, "Selected", -6, -6, ChipW + 12, ChipH + 12, _config.selection, true, false);
                ring.sprite = PlaceholderArt.UiRoundedOutline;
                ring.enabled = false;
                _chips.Add(new Chip { ApplianceId = id, Root = button.gameObject, Ring = ring });
                x += ChipW + Gap;
            }
            _cordChip.anchoredPosition = new Vector2(x + Gap, -Top);
        }

        private static string NameOf(HouseSim sim, string applianceId)
        {
            var app = sim.FindAppliance(applianceId);
            return app == null ? applianceId : sim.Catalog.Find(app.Type)?.name ?? app.Type;
        }
    }
}
