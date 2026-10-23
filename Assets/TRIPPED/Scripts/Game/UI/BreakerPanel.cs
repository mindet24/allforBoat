using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// Breaker cards on the right (GDD v1.0 §6.3): one per circuit with its id in the circuit colour, the shown
    /// label, a load bar "load / cap" (green / amber / red), ON/OFF and an overload countdown that flashes.
    /// A MAIN card sits on top when the level has a main breaker. Clicking a circuit card submits ToggleBreaker;
    /// the character then walks to the Panel to flip it.
    /// </summary>
    public sealed class BreakerPanel
    {
        private const float CardW = 360f, CardH = 150f, Gap = 12f;
        private const float BarW = 220f, BarH = 32f;

        private sealed class Card
        {
            public CircuitState Circuit; // null = MAIN
            public Image Background;
            public Image BarFill;
            public TextMeshProUGUI LoadText;
            public TextMeshProUGUI State;
            public Image StateBg;
            public TextMeshProUGUI Warning;
            public float ShownFill;
            /// <summary>Last values written to the texts, so text is only rebuilt when they change.</summary>
            public int ShownKey = int.MinValue;
        }

        private readonly List<Card> _cards = new List<Card>();
        private TrippedViewConfig _config;
        private HudText _text;

        /// <summary>Builds the panel and its cards.</summary>
        public void Build(RectTransform canvas, LevelController controller, TrippedViewConfig config, HudText text)
        {
            _config = config;
            _text = text;
            var sim = controller.Sim;
            var panel = UIFactory.Anchored(canvas, "BreakerPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -96f), new Vector2(380f, 784f));
            float y = 10f;
            if (sim.MainCapacity > 0)
            {
                _cards.Add(MakeCard(panel, null, y, null));
                y += CardH + Gap;
            }
            foreach (var c in sim.Circuits)
            {
                string id = c.Id;
                _cards.Add(MakeCard(panel, c, y, () => controller.SubmitFromUi(Command.ToggleBreaker(id, controller.ShiftHeld))));
                y += CardH + Gap;
            }
        }

        private Card MakeCard(RectTransform panel, CircuitState circuit, float y, UnityEngine.Events.UnityAction onClick)
        {
            var card = new Card { Circuit = circuit };
            bool isMain = circuit == null;
            card.Background = UIFactory.Panel(panel, isMain ? "Card MAIN" : "Card " + circuit.Id, 10f, y, CardW, CardH, _config.hudPanel);
            if (onClick != null)
            {
                var button = card.Background.gameObject.AddComponent<Button>();
                button.targetGraphic = card.Background;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() =>
                {
                    if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                    onClick();
                });
            }
            var t = card.Background.transform;
            UIFactory.Panel(t, "Outline", 0, 0, CardW, CardH, _config.hudOutline, true, false).sprite = PlaceholderArt.UiRoundedOutline;

            string id = isMain ? _text.Get("hud_main") : circuit.Id;
            Color idColor = isMain ? _config.textPrimary : _config.CircuitColor(circuit.Id);
            UIFactory.Label(t, "Id", 16, 6, isMain ? 160 : 80, 64, id, 48, idColor, TextAlignmentOptions.MidlineLeft, true);
            string label = isMain ? _text.Get("hud_main_label") : circuit.ShownLabel;
            UIFactory.Label(t, "ShownLabel", isMain ? 140 : 96, 14, 160, 48, label, 24, _config.textSecondary);

            card.StateBg = UIFactory.Panel(t, "State", 266, 16, 80, 44, _config.breakerOn, true, false);
            card.State = UIFactory.LabelOn(UIFactory.Stretch(UIFactory.Rect(card.StateBg.transform, "Text", 0, 0, 80, 44)),
                _text.Get("hud_on"), 22, _config.textPrimary, TextAlignmentOptions.Center, true);
            if (isMain) card.StateBg.gameObject.SetActive(false);

            card.Warning = UIFactory.Label(t, "Warning", 16, 66, 330, 26, "", 20, _config.loadRed, TextAlignmentOptions.MidlineLeft, true);
            card.BarFill = UIFactory.Bar(t, "LoadBar", 16, 100, BarW, BarH, _config.hudBackground, _config.loadGreen);
            card.LoadText = UIFactory.Label(t, "Load", 16 + BarW + 10, 92, CardW - BarW - 36, 48, "", 34, _config.textPrimary, TextAlignmentOptions.MidlineRight, true);
            return card;
        }

        /// <summary>Updates every card from the sim.</summary>
        public void Sync(HouseSim sim, float time, float deltaTime)
        {
            int tps = sim.Rules.ticksPerSecond;
            bool loadHidden = sim.Rules.breakerHudMode == "AtPanelOnly" && sim.Character.RoomId != sim.PanelRoomId;
            foreach (var card in _cards)
            {
                bool isMain = card.Circuit == null;
                int load = isMain ? sim.MainLoad : card.Circuit.Load;
                int cap = isMain ? sim.MainCapacity : card.Circuit.Capacity;
                bool on = isMain || card.Circuit.IsOn;
                bool over = isMain ? sim.MainOverloading : card.Circuit.Overloading;
                int overTicks = isMain ? sim.MainOverTicks : card.Circuit.OverTicks;

                // Load bar eases to the new value in about 0.15 s (GDD v1.0 §7.1).
                float target = loadHidden ? 0f : HudFormat.Fill(load, cap);
                card.ShownFill = Mathf.MoveTowards(card.ShownFill, target, deltaTime / 0.15f);
                UIFactory.SetBar(card.BarFill, card.ShownFill);
                var band = HudFormat.Band(load, cap, _config.loadAmberAbove, _config.loadRedAbove);
                card.BarFill.color = _config.LoadColor(band);
                // Texts only change when the numbers do (no string building every frame).
                int key = ((load * 397 + cap) * 31 + overTicks) * 8 + (on ? 4 : 0) + (over ? 2 : 0) + (loadHidden ? 1 : 0);
                if (key != card.ShownKey)
                {
                    card.ShownKey = key;
                    card.LoadText.text = loadHidden ? _text.Format("hud_load_unknown", ("cap", cap)) : HudFormat.LoadText(load, cap);
                    if (!isMain)
                    {
                        card.State.text = on ? _text.Get("hud_on") : _text.Get("hud_off");
                        card.StateBg.color = on ? _config.breakerOn : _config.breakerOff;
                    }
                    if (over)
                        card.Warning.text = _text.Format("hud_overload", ("s", HudFormat.SecondsToTrip(overTicks, sim.Rules.overTripTicks, tps).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
                    else if (!on && overTicks > 0)
                        card.Warning.text = _text.Get("hud_hot");
                    else
                        card.Warning.text = "";
                }

                bool flash = over && Mathf.Repeat(time * 4f, 1f) < 0.5f;
                card.Background.color = flash ? Color.Lerp(_config.hudPanel, _config.loadRed, 0.45f) : _config.hudPanel;
            }
        }
    }
}
