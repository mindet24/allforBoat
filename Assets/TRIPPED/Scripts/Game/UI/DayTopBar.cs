using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// The Day-mode top bar: level title, hearts, score, time left, Start/Pause and Restart, the hint under the
    /// bar, and the big BLACKOUT banner while any breaker is OFF.
    /// </summary>
    public sealed class DayTopBar
    {
        private const int MaxHearts = 3;

        private TextMeshProUGUI _score;
        private TextMeshProUGUI _timeLeft;
        private TextMeshProUGUI _hint;
        private TextMeshProUGUI _playLabel;
        private Button _play;
        private Image[] _hearts;
        private GameObject _hintBox;
        private GameObject _banner;
        private Image _bannerImage;
        private HudText _text;
        private TrippedViewConfig _config;
        private int _shownHearts = -1;
        private int _shownScore = -1;
        private int _shownSecond = -1;
        private LevelPhase _shownPhase = (LevelPhase)(-1);
        private float _pulseUntil = -1f;

        /// <summary>True while the BLACKOUT banner is on screen (for checks).</summary>
        public bool BannerShown => _banner != null && _banner.activeSelf;

        /// <summary>Text of the banner (for checks).</summary>
        public string BannerText { get; private set; } = "";

        public void Build(RectTransform canvas, LevelController controller, TrippedViewConfig config, HudText text)
        {
            _text = text;
            _config = config;
            var sim = controller.Sim;
            var bar = UIFactory.Anchored(canvas, "DayTopBar", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 96f));
            bar.anchorMax = new Vector2(1f, 1f);
            bar.sizeDelta = new Vector2(0f, 96f);
            var bg = config.hudBackground;
            bg.a = 0.94f;
            UIFactory.Fill(bar, bg, false);

            UIFactory.Label(bar, "Title", 24, 0, 420, 96, sim.Level.title, 36, config.textPrimary, TextAlignmentOptions.MidlineLeft, true);

            _hearts = new Image[MaxHearts];
            for (int i = 0; i < MaxHearts; i++)
            {
                var heart = UIFactory.Panel(bar, "Heart " + i, 470 + i * 62, 20, 54, 54, config.heartColor, false, false);
                heart.sprite = PlaceholderArt.Heart;
                _hearts[i] = heart;
            }
            _score = UIFactory.Label(bar, "Score", 680, 0, 300, 96, "", 36, config.glowOn, TextAlignmentOptions.MidlineLeft, true);

            var timeRt = UIFactory.Anchored(bar, "TimeLeft", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(80f, 0f), new Vector2(360, 96));
            _timeLeft = UIFactory.LabelOn(timeRt, "", 44, config.textPrimary, TextAlignmentOptions.Center, true);

            var buttons = UIFactory.Anchored(bar, "Buttons", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(460, 60));
            _play = UIFactory.Button(buttons, "StartPause", 0, 0, 240, 60, text.Get("hud_start"), 24, config.hudOutline, config.textPrimary, controller.StartOrTogglePause);
            _playLabel = _play.GetComponentInChildren<TextMeshProUGUI>();
            UIFactory.Button(buttons, "Restart", 256, 0, 204, 60, text.Get("hud_restart"), 24, config.hudOutline, config.textPrimary, controller.Restart);

            var hintRt = UIFactory.Anchored(canvas, "DayHint", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -108f), new Vector2(470, 44));
            var hintBg = UIFactory.Fill(hintRt, new Color(0f, 0f, 0f, 0.55f), true, false);
            _hintBox = hintRt.gameObject;
            _hint = UIFactory.LabelOn(UIFactory.Stretch(UIFactory.Rect(hintBg.transform, "Text", 0, 0, 470, 44)), "", 26, config.loadAmber, TextAlignmentOptions.Center, true);
            _hint.enableAutoSizing = true;
            _hint.fontSizeMin = 14;
            _hint.fontSizeMax = 26;

            // Beside the top floor of the house, in the empty corner between the house and the request cards.
            var bannerRt = UIFactory.Anchored(canvas, "Blackout", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1075f, -108f), new Vector2(450, 170));
            _bannerImage = UIFactory.Fill(bannerRt, new Color(0.7f, 0.08f, 0.08f, 0.92f), true, false);
            string panelName = sim.FindRoom(sim.PanelRoomId)?.Name ?? "Basement";
            BannerText = text.Format("day_blackout_banner", ("panel", panelName));
            var bannerText = UIFactory.LabelOn(UIFactory.Stretch(UIFactory.Rect(bannerRt, "Text", 12, 8, 426, 154)), BannerText, 40, Color.white, TextAlignmentOptions.Center, true);
            bannerText.textWrappingMode = TextWrappingModes.Normal;
            bannerText.overflowMode = TextOverflowModes.Overflow;
            bannerText.enableAutoSizing = true;
            bannerText.fontSizeMin = 20;
            bannerText.fontSizeMax = 44;
            _banner = bannerRt.gameObject;
            _banner.SetActive(false);
        }

        public void Sync(HouseSim sim, float now)
        {
            int hearts = DayRules.FilledHearts(sim.Hearts, MaxHearts);
            if (hearts != _shownHearts)
            {
                if (_shownHearts > hearts) _pulseUntil = now + 0.6f;
                _shownHearts = hearts;
                for (int i = 0; i < _hearts.Length; i++) _hearts[i].color = i < hearts ? _config.heartColor : _config.heartEmpty;
            }
            // A lost heart bounces for a moment.
            float pulse = now < _pulseUntil ? 1f + 0.25f * Mathf.Sin((_pulseUntil - now) * 25f) : 1f;
            for (int i = 0; i < _hearts.Length; i++) _hearts[i].rectTransform.localScale = Vector3.one * (i == hearts ? pulse : 1f);

            if (sim.Score != _shownScore)
            {
                _shownScore = sim.Score;
                _score.text = _text.Format("day_score", ("score", sim.Score));
            }
            int tps = sim.Rules.ticksPerSecond;
            int left = DayRules.TicksLeft(sim.DayLengthTicks, sim.Now);
            int second = (left + tps - 1) / tps;
            if (second != _shownSecond)
            {
                _shownSecond = second;
                _timeLeft.text = _text.Format("day_time_left", ("time", $"{second / 60}:{second % 60:00}"));
            }

            if (sim.Phase != _shownPhase)
            {
                _shownPhase = sim.Phase;
                bool hint = sim.Phase == LevelPhase.Planning || sim.Phase == LevelPhase.Paused;
                _hintBox.SetActive(hint);
                _hint.text = sim.Phase == LevelPhase.Planning ? _text.Get("day_hint_start") : _text.Sim("pause_hint");
                switch (sim.Phase)
                {
                    case LevelPhase.Planning: _playLabel.text = _text.Get("hud_start"); break;
                    case LevelPhase.Running: _playLabel.text = _text.Get("hud_pause"); break;
                    case LevelPhase.Paused: _playLabel.text = _text.Get("hud_resume"); break;
                }
                _play.gameObject.SetActive(sim.Phase != LevelPhase.Won && sim.Phase != LevelPhase.Lost);
            }

            bool blackout = DayRules.IsBlackout(sim);
            if (_banner.activeSelf != blackout) _banner.SetActive(blackout);
            if (blackout)
            {
                var c = _bannerImage.color;
                c.a = 0.75f + 0.2f * Mathf.Sin(now * 6f);
                _bannerImage.color = c;
            }
        }
    }
}
