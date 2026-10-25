using System;
using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// The fixed parts of the day HUD: hearts and a pause button top-right, the coin score bottom-left with the
    /// current room's power readout above it, the hourglass day timer bottom-right, the carry slot and key-hint bar
    /// bottom-centre, the start card before the day runs, the OVERVIEW tag while Tab is held, and the big BLACKOUT
    /// banner with an arrow to the breaker panel.
    /// </summary>
    public sealed class DayStatus
    {
        private const int MaxHearts = 3;

        private DayHudContext _cx;
        private Image[] _hearts;
        private TextMeshProUGUI _score;
        private RectTransform _scoreRoot;
        private TextMeshProUGUI _time;
        private Image _timeFill;
        private Image _timeCard;
        private RectTransform _hintRoot;
        private RectTransform _heartsRoot;
        private Image _carryIcon;
        private TextMeshProUGUI _carryName;
        private RectTransform _carryRoot;
        private RectTransform _readoutBody;
        private MeterWidget _readout;
        private GameObject _startCard;
        private GameObject _overview;
        private GameObject _banner;
        private Image _bannerImage;
        private RectTransform _bannerArrow;
        private int _shownHearts = -1;
        private int _shownScore = -1;
        private int _shownSecond = -1;
        private string _shownCarry = "~";
        private float _heartLostAt = -10f;
        private float _scoreAt = -10f;
        private float _carryAt = -10f;

        /// <summary>True while the BLACKOUT banner shows (for checks).</summary>
        public bool BannerShown => _banner != null && _banner.activeSelf;

        /// <summary>The current-room readout meter (for checks).</summary>
        public MeterWidget Readout => _readout;

        public void Build(DayHudContext cx, HouseSim sim, Action onPause)
        {
            _cx = cx;
            var ui = cx.Ui;
            var text = cx.Text;
            var root = cx.Canvas;
            float inset = ui.edgeInset;
            var panel = ui.panel;

            // Hearts, top-right.
            var hearts = UiKit.Card(root, "Hearts", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-inset, -inset), ui.heartsSize, panel, ui);
            _heartsRoot = (RectTransform)hearts.transform.parent;
            _hearts = new Image[MaxHearts];
            float hs = ui.heartSize;
            float hx0 = (ui.heartsSize.x - MaxHearts * hs - (MaxHearts - 1) * 10f) * 0.5f;
            for (int i = 0; i < MaxHearts; i++)
            {
                var h = UIFactory.Anchored(hearts.transform, "Heart " + i, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(hx0 + hs * 0.5f + i * (hs + 10f), 0f), new Vector2(hs, hs));
                var img = h.gameObject.AddComponent<Image>();
                img.sprite = UiIcons.Heart;
                img.color = Color.white;
                img.preserveAspect = true;
                img.raycastTarget = false;
                _hearts[i] = img;
            }

            // Pause button left of the hearts.
            var pause = UiKit.Button(root, "Pause", new Vector2(1f, 1f),
                new Vector2(-inset - ui.heartsSize.x - 16f - ui.pauseButtonSize.x * 0.5f, -inset - ui.heartsSize.y * 0.5f),
                ui.pauseButtonSize, "II", 40, panel, ui.cream, ui, onPause);
            pause.Label.characterSpacing = -20f;

            // Score coin, bottom-left.
            var score = UiKit.Card(root, "Score", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(inset, inset), ui.scoreSize, panel, ui);
            _scoreRoot = (RectTransform)score.transform.parent;
            _scoreRoot.pivot = new Vector2(0.5f, 0.5f);
            _scoreRoot.anchoredPosition = new Vector2(inset + ui.scoreSize.x * 0.5f, inset + ui.scoreSize.y * 0.5f);
            float coin = ui.scoreSize.y + 12f;
            var coinImg = UiKit.Icon(score.transform, "Coin", -14f, -6f, coin, coin, UiIcons.Coin, Color.white);
            coinImg.preserveAspect = true;
            _score = UiKit.Text(score.transform, "Value", coin - 6f, 0, ui.scoreSize.x - coin, ui.scoreSize.y, "0", 60, ui.cream);

            // (The current room's power now lives in the power panel, top-right.)

            // Day timer, bottom-right.
            _timeCard = UiKit.Card(root, "Timer", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-inset, inset), ui.timerSize, panel, ui);
            UiKit.Icon(_timeCard.transform, "Hourglass", 16, 14, 56, ui.timerSize.y - 28f, UiIcons.Hourglass, Color.white);
            _time = UiKit.Text(_timeCard.transform, "Time", 76, 4, ui.timerSize.x - 92f, ui.timerSize.y - 30f, "", 56, ui.cream);
            var barBack = UIFactory.Panel(_timeCard.transform, "Bar", 84, ui.timerSize.y - 28f, ui.timerSize.x - 104f, 14, ui.charcoalSoft, false, false);
            barBack.sprite = UiArt.Pill;
            barBack.type = Image.Type.Sliced;
            _timeFill = UIFactory.Fill(UIFactory.Stretch(UIFactory.Rect(barBack.transform, "Fill", 0, 0, 0, 0)), ui.accent, false, false);
            _timeFill.sprite = UiArt.Pill;
            _timeFill.type = Image.Type.Sliced;

            // Key hints, bottom-centre; the carry slot sits above the score coin, bottom-left, clear of the house.
            var hint = UiKit.Card(root, "KeyHints", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, inset), ui.keyHintSize, panel, ui, false);
            hint.sprite = UiArt.Pill;
            _hintRoot = (RectTransform)hint.transform.parent;
            var hintText = UiKit.Fill(hint.transform, "Text", text.Get(HudFormat.MoveKeys(sim, "g6_keys_hud")), 24, ui.cream, TextAlignmentOptions.Center, false);
            hintText.enableAutoSizing = true;
            hintText.fontSizeMin = 14;
            hintText.fontSizeMax = 24;

            var carry = UiKit.Card(root, "Carry", new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(inset + ui.carrySize.x * 0.5f, inset + ui.scoreSize.y + 16f + ui.carrySize.y * 0.5f), ui.carrySize, ui.cream, ui);
            _carryRoot = (RectTransform)carry.transform.parent;
            UiKit.Text(carry.transform, "Caption", 0, 4, ui.carrySize.x, 20, text.Get("g6_holding"), 15, ui.charcoalSoft);
            _carryIcon = UiKit.Icon(carry.transform, "Icon", (ui.carrySize.x - 72f) * 0.5f, 24, 72, 72, null, Color.white);
            _carryName = UiKit.Text(carry.transform, "Name", 4, ui.carrySize.y - 28f, ui.carrySize.x - 8f, 24, "", 16, ui.charcoal);
            _carryName.enableAutoSizing = true;
            _carryName.fontSizeMin = 10;
            _carryName.fontSizeMax = 16;

            // Overview tag, top-right beside the pause button (clear of the tickets and the house).
            var ov = UiKit.Card(root, "OverviewTag", new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-inset - ui.heartsSize.x - ui.pauseButtonSize.x - 32f, -inset - ui.heartsSize.y * 0.5f), new Vector2(240f, 60f), ui.accent, ui);
            UiKit.Fill(ov.transform, "Text", text.Get("g6_overview"), 28, ui.charcoal);
            _overview = ov.transform.parent.gameObject;
            _overview.SetActive(false);

            // Start card.
            var start = UiKit.Card(root, "StartCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), ui.startCardSize, ui.cream, ui);
            float sw = ui.startCardSize.x;
            UiKit.Text(start.transform, "Title", 0, 26, sw, 80, text.Get("g6_start_title"), 64, ui.charcoal);
            UiKit.Text(start.transform, "Body", 0, 110, sw, 50, text.Get("g6_start_body"), 30, ui.charcoalSoft, TextAlignmentOptions.Center, false);
            var key = UiKit.Card(start.transform, "Key", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(sw - 120f, 70f), ui.accent, ui);
            UiKit.Fill(key.transform, "Text", text.Get("g6_start_key"), 32, ui.charcoal, TextAlignmentOptions.Center, false);
            _startCard = start.transform.parent.gameObject;

            // Blackout banner.
            _bannerImage = UiKit.Card(root, "Blackout", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -ui.blackoutY), ui.blackoutSize, ui.bad, ui);
            float bw = ui.blackoutSize.x, bh = ui.blackoutSize.y;
            UiKit.Icon(_bannerImage.transform, "Warning", 26, (bh - 104f) * 0.5f, 104, 104, UiIcons.Warning, Color.white);
            UiKit.Text(_bannerImage.transform, "Title", 146, 12, bw - 330f, 86, text.Get("g6_blackout"), 80, Color.white, TextAlignmentOptions.MidlineLeft);
            UiKit.Text(_bannerImage.transform, "Sub", 150, 94, bw - 330f, 44, text.Get("g6_blackout_sub"), 34, Color.white, TextAlignmentOptions.MidlineLeft, false);
            var arrowBack = UIFactory.Anchored(_bannerImage.transform, "ArrowBack", new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-bh * 0.55f - 10f, 0f), new Vector2(bh - 24f, bh - 24f));
            var abImg = arrowBack.gameObject.AddComponent<Image>();
            abImg.sprite = PlaceholderArt.Circle;
            abImg.color = Color.white;
            abImg.raycastTarget = false;
            _bannerArrow = UIFactory.Anchored(arrowBack, "Arrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(bh - 50f, bh - 50f));
            var arrowImg = _bannerArrow.gameObject.AddComponent<Image>();
            arrowImg.sprite = UiArt.Arrow;
            arrowImg.color = ui.bad;
            arrowImg.raycastTarget = false;
            _banner = _bannerImage.transform.parent.gameObject;
            _banner.SetActive(false);
        }

        /// <summary>
        /// Adds the canvas-local rects (origin at the canvas centre, y up) of the fixed HUD panels that world-pinned
        /// meters must not sit under: room readout, score, carry slot, key hints, timer and hearts.
        /// </summary>
        public void CollectHudRects(List<Rect> into)
        {
            AddRect(_readoutBody, into);
            AddRect(_scoreRoot, into);
            AddRect(_carryRoot, into);
            AddRect(_hintRoot, into);
            AddRect(_timeCard != null ? _timeCard.rectTransform : null, into);
            AddRect(_heartsRoot, into);
        }

        private readonly Vector3[] _corners = new Vector3[4];

        private void AddRect(RectTransform rt, List<Rect> into)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return;
            rt.GetWorldCorners(_corners);
            var canvas = _cx.Canvas;
            var a = canvas.InverseTransformPoint(_corners[0]);
            var b = canvas.InverseTransformPoint(_corners[2]);
            into.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)));
        }

        public void Sync(HouseSim sim, float now)
        {
            var ui = _cx.Ui;
            var cfg = _cx.Config;
            bool over = sim.Phase == LevelPhase.Won || sim.Phase == LevelPhase.Lost;

            int hearts = DayRules.FilledHearts(sim.Hearts, MaxHearts);
            if (hearts != _shownHearts)
            {
                if (_shownHearts > hearts) _heartLostAt = now;
                _shownHearts = hearts;
                for (int i = 0; i < _hearts.Length; i++) _hearts[i].color = i < hearts ? Color.white : new Color(0.35f, 0.4f, 0.52f, 0.7f);
            }
            float since = now - _heartLostAt;
            for (int i = 0; i < _hearts.Length; i++)
            {
                float s = 1f;
                if (i == hearts && since < ui.heartLostBounceSeconds) s = 1f + 0.4f * Mathf.Sin(since / ui.heartLostBounceSeconds * Mathf.PI) * (1f - since / ui.heartLostBounceSeconds);
                _hearts[i].rectTransform.localScale = new Vector3(s, s, 1f);
            }

            if (sim.Score != _shownScore)
            {
                if (_shownScore >= 0 && sim.Score > _shownScore) _scoreAt = now;
                _shownScore = sim.Score;
                _score.text = sim.Score.ToString();
            }
            float sp = Mathf.Clamp01((now - _scoreAt) / Mathf.Max(0.01f, ui.scorePopSeconds));
            float scoreScale = sp < 1f ? 1f + 0.18f * Mathf.Sin(sp * Mathf.PI) : 1f;
            _scoreRoot.localScale = new Vector3(scoreScale, scoreScale, 1f);

            int tps = Mathf.Max(1, sim.Rules.ticksPerSecond);
            int left = DayRules.TicksLeft(sim.DayLengthTicks, sim.Now);
            int second = (left + tps - 1) / tps;
            if (second != _shownSecond)
            {
                _shownSecond = second;
                _time.text = second / 60 + ":" + (second % 60).ToString("00");
            }
            UIFactory.SetBar(_timeFill, sim.DayLengthTicks > 0 ? (float)left / sim.DayLengthTicks : 0f);
            bool warn = second <= ui.timerWarnSeconds && !over && sim.Phase != LevelPhase.Planning;
            var panel = ui.panel;
            _timeCard.color = warn ? Color.Lerp(panel, ui.bad, 0.5f + 0.5f * Mathf.Sin(now * 5f)) : panel;
            _timeFill.color = warn ? ui.bad : ui.accent;

            // Carry slot.
            string carrying = sim.Character.CarryingId ?? "";
            if (carrying != _shownCarry)
            {
                if (_shownCarry != "~") _carryAt = now;
                _shownCarry = carrying;
                var icon = carrying.Length > 0 ? _cx.IconOf(sim, carrying) : null;
                _carryIcon.sprite = icon;
                _carryIcon.enabled = icon != null;
                _carryName.text = carrying.Length > 0 ? DayHudContext.NameOf(sim, carrying) : _cx.Text.Get("g6_hands_free");
                _carryName.color = carrying.Length > 0 ? ui.charcoal : ui.charcoalSoft;
            }
            float cp = Mathf.Clamp01((now - _carryAt) / 0.3f);
            float carryScale = cp < 1f ? 1f + 0.2f * Mathf.Sin(cp * Mathf.PI) : 1f;
            _carryRoot.localScale = new Vector3(carryScale, carryScale, 1f);



            bool planning = sim.Phase == LevelPhase.Planning;
            if (_startCard.activeSelf != planning) _startCard.SetActive(planning);
            if (planning)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) _cx.Controller.StartOrTogglePause();
            }

            var cam = _cx.Controller.HouseCam;
            bool overview = cam != null && cam.Overview && !over;
            if (_overview.activeSelf != overview) _overview.SetActive(overview);

            bool blackout = DayRules.IsBlackout(sim);
            if (_banner.activeSelf != blackout) _banner.SetActive(blackout);
            if (blackout)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(now * ui.blackoutPulseSpeed);
                _bannerImage.color = Color.Lerp(ui.bad, Color.Lerp(ui.bad, Color.black, 0.3f), p);
                var panelRoom = sim.FindRoom(sim.PanelRoomId);
                float py = (panelRoom != null ? HouseLayout.FloorY(panelRoom.Floor, cfg.roomHeight) : 0f) + cfg.panelMountHeight + cfg.panelWorldHeight * 0.5f;
                var target = _cx.ToCanvas(new Vector2(sim.PanelX, py), out _, cfg.roomDepth);
                var size = _cx.CanvasSize;
                var from = new Vector2(size.x * 0.5f + ui.blackoutSize.x * 0.5f - ui.blackoutSize.y * 0.55f - 10f, -ui.blackoutY - ui.blackoutSize.y * 0.5f);
                var d = target - from;
                float angle = d.sqrMagnitude < 1f ? -90f : Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                _bannerArrow.localRotation = Quaternion.Euler(0f, 0f, angle);
                _bannerArrow.anchoredPosition = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * (6f * p);
            }
        }
    }
}
