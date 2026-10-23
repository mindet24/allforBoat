using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// Request cards on the left (GDD v1.0 §6.3): owner face and name, "Appliance in Room", progress bar, time
    /// left to the deadline, and DONE when finished. Progress is only updated while the request's room is visible;
    /// otherwise the card keeps the last value the player saw (no hidden-state leak).
    /// </summary>
    public sealed class RequestPanel
    {
        private const float CardW = 360f, CardH = 120f, Gap = 12f;

        private sealed class Card
        {
            public RequestState Request;
            public Image Background;
            public Image Outline;
            public Image Bar;
            public TextMeshProUGUI TimeLeft;
            public TextMeshProUGUI Unseen;
            public float SeenProgress;
            public bool SeenDone;
            public int ShownSecond = -1;
            public bool ShownDone;
            public bool ShownVisible = true;
        }

        private readonly List<Card> _cards = new List<Card>();
        private TrippedViewConfig _config;
        private HudText _text;

        /// <summary>Builds one card per request.</summary>
        public void Build(RectTransform canvas, LevelController controller, TrippedViewConfig config, HudText text, ArtLibrary art)
        {
            _config = config;
            _text = text;
            var sim = controller.Sim;
            var panel = UIFactory.Anchored(canvas, "RequestPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -96f), new Vector2(380f, 784f));
            float y = 10f;
            foreach (var r in sim.Requests)
            {
                var card = new Card { Request = r };
                card.Background = UIFactory.Panel(panel, "Request " + r.Owner, 10f, y, CardW, CardH, config.hudPanel);
                var t = card.Background.transform;
                card.Outline = UIFactory.Panel(t, "Outline", 0, 0, CardW, CardH, config.hudOutline, true, false);
                card.Outline.sprite = PlaceholderArt.UiRoundedOutline;

                var face = art.Face(r.Owner);
                var faceImg = UIFactory.Panel(t, "Face", 12, 12, 60, 60, face != null ? Color.white : FaceColor(r.Owner), false, false);
                if (face != null) faceImg.sprite = face;
                else
                {
                    faceImg.sprite = PlaceholderArt.Circle;
                    UIFactory.LabelOn(UIFactory.Stretch(UIFactory.Rect(faceImg.transform, "Initial", 0, 0, 60, 60)),
                        r.Owner.Length > 0 ? r.Owner.Substring(0, 1).ToUpperInvariant() : "?", 32, config.textPrimary, TextAlignmentOptions.Center, true);
                }

                UIFactory.Label(t, "Owner", 84, 6, 180, 36, r.Owner, 26, config.textPrimary, TextAlignmentOptions.MidlineLeft, true);
                string appName = ApplianceName(sim, r.ApplianceId);
                string roomName = sim.FindRoom(r.RoomId)?.Name ?? r.RoomId;
                UIFactory.Label(t, "What", 84, 40, 266, 30, text.Format("hud_request_line", ("appliance", appName), ("room", roomName)), 20, config.textSecondary);
                card.Bar = UIFactory.Bar(t, "Progress", 84, 82, 180, 18, config.hudBackground, config.glowOn);
                card.TimeLeft = UIFactory.Label(t, "TimeLeft", 270, 6, 80, 36, "", 24, config.textPrimary, TextAlignmentOptions.MidlineRight, true);
                card.Unseen = UIFactory.Label(t, "Unseen", 270, 72, 84, 36, text.Get("hud_unseen"), 16, config.textSecondary, TextAlignmentOptions.MidlineRight);
                card.Unseen.gameObject.SetActive(false);
                _cards.Add(card);
                y += CardH + Gap;
            }
        }

        /// <summary>Updates the cards. Progress only moves while the request's room is visible.</summary>
        public void Sync(HouseSim sim, float time)
        {
            int tps = sim.Rules.ticksPerSecond;
            foreach (var card in _cards)
            {
                var r = card.Request;
                bool visible = sim.IsRoomVisible(r.RoomId) || sim.Phase == LevelPhase.Won;
                if (visible)
                {
                    card.SeenProgress = HudFormat.Progress(r.ProgressTicks, r.DurationTicks);
                    card.SeenDone = r.Done;
                }
                UIFactory.SetBar(card.Bar, card.SeenProgress);
                card.Bar.color = card.SeenDone ? _config.requestDone : (visible ? _config.glowOn : _config.textSecondary);

                if (visible != card.ShownVisible)
                {
                    card.ShownVisible = visible;
                    card.Unseen.gameObject.SetActive(!visible && !card.SeenDone);
                }

                int left = r.DueTicks - sim.Now;
                int second = left <= 0 ? 0 : (left + tps - 1) / tps;
                if (card.SeenDone != card.ShownDone || second != card.ShownSecond)
                {
                    card.ShownDone = card.SeenDone;
                    card.ShownSecond = second;
                    card.TimeLeft.text = card.SeenDone ? _text.Get("hud_done") : HudFormat.TimeLeft(r.DueTicks, sim.Now, tps);
                    card.TimeLeft.color = card.SeenDone ? _config.requestDone : _config.textPrimary;
                    if (card.SeenDone) card.Unseen.gameObject.SetActive(false);
                }

                // Under 5 s to the deadline the outline blinks red (GDD v1.0 §7.1).
                bool urgent = !card.SeenDone && left < 5 * tps && sim.Phase == LevelPhase.Running && Mathf.Repeat(time * 2f, 1f) < 0.5f;
                card.Outline.color = card.SeenDone ? _config.requestDone : (urgent ? _config.loadRed : _config.hudOutline);
            }
        }

        private static string ApplianceName(HouseSim sim, string applianceId)
        {
            var app = sim.FindAppliance(applianceId);
            return app == null ? applianceId : sim.Catalog.Find(app.Type)?.name ?? app.Type;
        }

        /// <summary>A muted colour per owner name (never a circuit colour), the same every run.</summary>
        private static Color FaceColor(string owner)
        {
            int h = 0;
            foreach (char ch in owner) h = h * 31 + ch;
            float hue = Mathf.Repeat(h * 0.137f, 1f);
            return Color.HSVToRGB(hue, 0.25f, 0.55f);
        }
    }
}
