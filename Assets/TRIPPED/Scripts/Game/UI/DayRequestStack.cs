using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// The request cards of a day, stacked down the right side: person, appliance and target room, urgency colour,
    /// a countdown to the tick it must start by, then a progress bar once it runs. A finished or missed request
    /// slides out and fades; the cards below close the gap.
    /// </summary>
    public sealed class DayRequestStack
    {
        private const float CardW = 360f, CardH = 112f, Gap = 12f, Top = 108f, Right = 12f;

        private sealed class Card
        {
            public RequestState Request;
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Stripe;
            public Image Outline;
            public Image Bar;
            public TextMeshProUGUI Status;
            public TextMeshProUGUI Tag;
            public float BornAt;
            public float ExitAt = -1f;
            public float Y;
            public string ShownStatus = "";
            public bool ShownFinished;
        }

        private readonly Dictionary<int, Card> _cards = new Dictionary<int, Card>();
        private readonly List<Card> _ordered = new List<Card>();
        private readonly List<int> _dead = new List<int>();
        private RectTransform _root;
        private TrippedViewConfig _config;
        private HudText _text;

        /// <summary>Cards on screen, including ones still sliding out (for checks).</summary>
        public int CardCount => _cards.Count;

        public void Build(RectTransform canvas, TrippedViewConfig config, HudText text)
        {
            _config = config;
            _text = text;
            _root = UIFactory.Stretch(UIFactory.Rect(canvas, "DayRequests", 0, 0, 0, 0));
        }

        public void Sync(HouseSim sim, float now, float dt)
        {
            int tps = sim.Rules.ticksPerSecond;
            // When the day is over every card leaves, including requests that were still waiting.
            bool dayOver = sim.Phase == LevelPhase.Won || sim.Phase == LevelPhase.Lost;
            foreach (var r in sim.Requests)
            {
                if (r.Serial <= 0) continue;
                if (!_cards.TryGetValue(r.Serial, out var card))
                {
                    if (!r.IsActive || dayOver) continue; // finished before it ever had a card
                    card = MakeCard(sim, r, now);
                    _cards[r.Serial] = card;
                }
                Update(card, sim, r, now, tps, dayOver);
            }

            // Keep cards in the order the requests appeared and let them glide into place.
            _ordered.Clear();
            _dead.Clear();
            foreach (var pair in _cards)
            {
                var c = pair.Value;
                if (c.ExitAt >= 0f && now - c.ExitAt > _config.cardExitSeconds) _dead.Add(pair.Key);
                else _ordered.Add(c);
            }
            foreach (int serial in _dead)
            {
                Object.Destroy(_cards[serial].Root.gameObject);
                _cards.Remove(serial);
            }
            _ordered.Sort((a, b) => a.Request.Serial.CompareTo(b.Request.Serial));
            int slot = 0;
            foreach (var c in _ordered)
            {
                float targetY = Top + slot * (CardH + Gap);
                if (c.ExitAt < 0f) slot++; // an exiting card no longer takes a slot
                c.Y = Mathf.Lerp(c.Y, targetY, 1f - Mathf.Exp(-12f * dt));

                float enter = Mathf.Clamp01((now - c.BornAt) / 0.25f);
                float exit = c.ExitAt < 0f ? 0f : Mathf.Clamp01((now - c.ExitAt) / _config.cardExitSeconds);
                float x = (1f - enter) * 380f + exit * 160f;
                c.Root.anchoredPosition = new Vector2(-Right + x, -c.Y);
                c.Group.alpha = enter * (1f - exit);
            }
        }

        private void Update(Card card, HouseSim sim, RequestState r, float now, int tps, bool dayOver)
        {
            bool finished = !r.IsActive || dayOver;
            if (finished && card.ExitAt < 0f) card.ExitAt = now;
            var level = DayRules.ParseUrgency(r.Urgency);
            var color = _config.UrgencyColor(level);
            string status;
            float fill;
            Color fillColor = color;
            if (r.Done)
            {
                status = _text.Get("day_card_done");
                fill = 1f;
                fillColor = _config.requestDone;
            }
            else if (r.Missed)
            {
                status = _text.Get("day_card_missed");
                fill = 0f;
            }
            else if (r.ProgressTicks > 0)
            {
                status = _text.Get("day_card_running");
                fill = HudFormat.Progress(r.ProgressTicks, r.DurationTicks);
                fillColor = _config.glowOn;
            }
            else
            {
                status = _text.Format("day_card_wait", ("time", HudFormat.TimeLeft(r.DueTicks, sim.Now, tps)));
                fill = DayRules.WaitFraction(r.SpawnTick, r.DueTicks, sim.Now);
            }
            if (status != card.ShownStatus)
            {
                card.ShownStatus = status;
                card.Status.text = status;
            }
            UIFactory.SetBar(card.Bar, fill);
            card.Bar.color = fillColor;
            if (finished != card.ShownFinished)
            {
                card.ShownFinished = finished;
                var outline = r.Done ? _config.requestDone : (r.Missed ? _config.loadRed : _config.hudOutline);
                card.Outline.color = outline;
            }
            // An urgent request that is nearly out of time pulses its outline.
            if (!finished && level == UrgencyLevel.Urgent && r.ProgressTicks == 0 && r.DueTicks - sim.Now < 5 * tps)
                card.Outline.color = Mathf.Repeat(now * 2f, 1f) < 0.5f ? _config.loadRed : _config.hudOutline;
        }

        private Card MakeCard(HouseSim sim, RequestState r, float now)
        {
            var level = DayRules.ParseUrgency(r.Urgency);
            var color = _config.UrgencyColor(level);
            var card = new Card { Request = r, BornAt = now, Y = Top + _cards.Count * (CardH + Gap) };
            card.Root = UIFactory.Anchored(_root, "Request " + r.Serial, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(CardW, CardH));
            card.Group = card.Root.gameObject.AddComponent<CanvasGroup>();
            card.Group.blocksRaycasts = false;
            UIFactory.Fill(card.Root, _config.hudPanel, true, false);
            card.Outline = UIFactory.Panel(card.Root, "Outline", 0, 0, CardW, CardH, _config.hudOutline, true, false);
            card.Outline.sprite = PlaceholderArt.UiRoundedOutline;
            card.Stripe = UIFactory.Panel(card.Root, "Stripe", 10, 12, 10, CardH - 24, color, false, false);

            UIFactory.Label(card.Root, "Owner", 34, 6, 190, 40, r.Owner, 28, _config.textPrimary, TextAlignmentOptions.MidlineLeft, true);
            string tagKey = level == UrgencyLevel.Urgent ? "day_urgent" : level == UrgencyLevel.Relaxed ? "day_relaxed" : "day_normal";
            card.Tag = UIFactory.Label(card.Root, "Tag", 200, 8, 150, 36, _text.Get(tagKey), 20, color, TextAlignmentOptions.MidlineRight, true);
            string appName = NameOf(sim, r.ApplianceId);
            string roomName = sim.FindRoom(r.RoomId)?.Name ?? r.RoomId;
            var what = UIFactory.Label(card.Root, "What", 34, 44, 316, 30, appName + "  →  " + roomName, 22, _config.textPrimary);
            what.enableAutoSizing = true;
            what.fontSizeMin = 14;
            what.fontSizeMax = 22;
            card.Status = UIFactory.Label(card.Root, "Status", 34, 70, 316, 24, "", 18, _config.textSecondary);
            card.Bar = UIFactory.Bar(card.Root, "Progress", 34, 94, 316, 10, _config.hudBackground, color);
            return card;
        }

        private static string NameOf(HouseSim sim, string applianceId)
        {
            var app = sim.FindAppliance(applianceId);
            return app == null ? applianceId : sim.Catalog.Find(app.Type)?.name ?? app.Type;
        }
    }
}
