using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// Top bar (GDD v1.0 §6.2): level title left, clock "m:ss / m:ss" centre, Start/Pause and Restart buttons right,
    /// and the planning/paused hint under the bar.
    /// </summary>
    public sealed class TopBar
    {
        private TextMeshProUGUI _clock;
        private TextMeshProUGUI _hint;
        private TextMeshProUGUI _playLabel;
        private Button _play;
        private HudText _text;
        private LevelPhase _shownPhase = (LevelPhase)(-1);
        private int _shownNow = -1;

        /// <summary>Builds the bar along the top of the canvas.</summary>
        public void Build(RectTransform canvas, LevelController controller, TrippedViewConfig config, HudText text)
        {
            _text = text;
            var sim = controller.Sim;
            var bar = UIFactory.Anchored(canvas, "TopBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 96f));
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.sizeDelta = new Vector2(0f, 96f);
            var bg = config.hudBackground;
            bg.a = 0.94f;
            UIFactory.Fill(bar, bg, false);

            string title = text.Format("hud_level_title", ("n", sim.Level.id), ("title", sim.Level.title));
            UIFactory.Label(bar, "Title", 24, 0, 700, 96, title, 36, config.textPrimary, TextAlignmentOptions.MidlineLeft, true);

            var clockRt = UIFactory.Anchored(bar, "Clock", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, 96));
            _clock = UIFactory.LabelOn(clockRt, "", 44, config.textPrimary, TextAlignmentOptions.Center, true);

            var buttons = UIFactory.Anchored(bar, "Buttons", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(460, 60));
            _play = UIFactory.Button(buttons, "StartPause", 0, 0, 240, 60, text.Get("hud_start"), 24, config.hudOutline, config.textPrimary,
                controller.StartOrTogglePause);
            _playLabel = _play.GetComponentInChildren<TextMeshProUGUI>();
            UIFactory.Button(buttons, "Restart", 256, 0, 204, 60, text.Get("hud_restart"), 24, config.hudOutline, config.textPrimary,
                controller.Restart);

            var hintRt = UIFactory.Anchored(canvas, "Hint", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(1000, 44));
            var hintBg = UIFactory.Fill(hintRt, new Color(0f, 0f, 0f, 0.55f), true, false);
            _hint = UIFactory.LabelOn(UIFactory.Stretch(UIFactory.Rect(hintBg.transform, "Text", 0, 0, 1000, 44)),
                text.Sim("pause_hint"), 26, config.loadAmber, TextAlignmentOptions.Center, true);
        }

        /// <summary>Updates the clock, hint and button label from the sim.</summary>
        public void Sync(HouseSim sim)
        {
            if (sim.Now != _shownNow)
            {
                _shownNow = sim.Now;
                _clock.text = HudFormat.ClockPair(sim.Now, sim.DayLengthTicks, sim.Rules.ticksPerSecond);
            }
            if (sim.Phase == _shownPhase) return;
            _shownPhase = sim.Phase;
            bool showHint = sim.Phase == LevelPhase.Planning || sim.Phase == LevelPhase.Paused;
            _hint.transform.parent.gameObject.SetActive(showHint);
            switch (sim.Phase)
            {
                case LevelPhase.Planning: _playLabel.text = _text.Get("hud_start"); break;
                case LevelPhase.Running: _playLabel.text = _text.Get("hud_pause"); break;
                case LevelPhase.Paused: _playLabel.text = _text.Get("hud_resume"); break;
            }
            // Once the level is over only Restart makes sense; the result panel has Retry/Next.
            _play.gameObject.SetActive(sim.Phase != LevelPhase.Won && sim.Phase != LevelPhase.Lost);
        }
    }
}
