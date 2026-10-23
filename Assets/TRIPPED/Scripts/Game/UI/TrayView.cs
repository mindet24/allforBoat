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
        private int _shownCords = -1;
        private string _shownSelection = "\0";

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
            _cordChip = UIFactory.Panel(_tray, "Cords", 0, Top, ChipW, ChipH, config.hudPanel).rectTransform;
            _cords = UIFactory.LabelOn(UIFactory.Stretch(UIFactory.Rect(_cordChip, "Text", 0, 0, ChipW, ChipH)), "", 24, config.textSecondary,
                TextAlignmentOptions.Center, true);
        }

        /// <summary>Rebuilds the chips when the set of unplugged appliances changes; updates selection and counts.</summary>
        public void Sync(HouseSim sim)
        {
            _wanted.Clear();
            foreach (var app in sim.Appliances)
                if (!app.Fixed && string.IsNullOrEmpty(app.SocketId)) _wanted.Add(app.Id);

            bool same = _wanted.Count == _chips.Count;
            for (int i = 0; same && i < _wanted.Count; i++) same = _wanted[i] == _chips[i].ApplianceId;
            if (!same) Rebuild(sim);

            string selection = _controller.Selection;
            foreach (var chip in _chips) chip.Ring.enabled = chip.ApplianceId == selection;

            if (sim.CordsLeft != _shownCords)
            {
                _shownCords = sim.CordsLeft;
                _cords.text = _text.Format("hud_cords", ("n", sim.CordsLeft));
            }
            if (selection != _shownSelection)
            {
                _shownSelection = selection;
                _hint.text = selection == null ? _text.Get("hud_tray_help") : _text.Format("hud_selected", ("appliance", NameOf(sim, selection)));
                _hint.color = selection == null ? _config.textSecondary : _config.selection;
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
