using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>What one button of the Level Select shows.</summary>
    public struct LevelCard
    {
        public int Number;
        public string Title;
        public string Teaches;
        public bool Locked;
        /// <summary>Best time as m:ss, or empty if the level was never won.</summary>
        public string Best;
    }

    /// <summary>
    /// The uGUI of the Title, Level Select and End screens (GDD v1.0 §6.1), built in code on a Screen Space
    /// Overlay canvas scaled from 1920x1080. <see cref="ScreenFlow"/> decides which one is visible.
    /// </summary>
    public sealed class MenuScreens
    {
        private const float CardWidth = 300f, CardHeight = 330f, CardGap = 30f;

        private readonly TrippedViewConfig _config;
        private readonly HudText _text;
        private RectTransform _selectCards;
        private Action<int> _onPick;
        private TextMeshProUGUI _endTime;

        /// <summary>The Title screen object.</summary>
        public GameObject Title { get; private set; }
        /// <summary>The Level Select screen object.</summary>
        public GameObject Select { get; private set; }
        /// <summary>The End screen object.</summary>
        public GameObject End { get; private set; }
        /// <summary>The "Levels (Esc)" button shown under the result panel.</summary>
        public GameObject ResultLevels { get; private set; }

        public MenuScreens(TrippedViewConfig config, HudText text)
        {
            _config = config;
            _text = text;
        }

        /// <summary>Creates all screens (hidden) under <paramref name="canvas"/>.</summary>
        public void Build(RectTransform canvas, Action onPlay, Action onQuit, Action onBack, Action onEndLevels,
            Action onResultLevels, Action<int> onPick, Action onTitleLevels = null)
        {
            _onPick = onPick;
            BuildTitle(canvas, onPlay, onQuit, onTitleLevels);
            BuildSelect(canvas, onBack);
            BuildEnd(canvas, onEndLevels);

            var holder = UIFactory.Rect(canvas, "ResultLevels", 810, 800, 300, 70);
            UIFactory.Button(holder, "Levels", 0, 0, 300, 70, _text.Get("result_levels"), 28, _config.hudOutline, _config.textPrimary, () => onResultLevels?.Invoke());
            ResultLevels = holder.gameObject;
            ResultLevels.SetActive(false);
        }

        /// <summary>Fills the Level Select with one button per card. Locked cards are dimmed and do nothing.</summary>
        public void ShowCards(IReadOnlyList<LevelCard> cards)
        {
            for (int i = _selectCards.childCount - 1; i >= 0; i--)
            {
                var child = _selectCards.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            int n = cards.Count;
            float total = n * CardWidth + Mathf.Max(0, n - 1) * CardGap;
            float x0 = (_config.referenceResolution.x - total) / 2f;
            for (int i = 0; i < n; i++) BuildCard(cards[i], x0 + i * (CardWidth + CardGap), 330f);
        }

        /// <summary>Sets the total time shown on the End screen.</summary>
        public void SetEndTime(string time)
        {
            _endTime.text = _text.Format("end_time", ("time", time));
        }

        private void BuildTitle(RectTransform canvas, Action onPlay, Action onQuit, Action onLevels = null)
        {
            var root = Background(canvas, "Title");
            float w = _config.referenceResolution.x;
            var title = UIFactory.Label(root, "Name", 0, 230, w, 180, _text.Get("menu_title"), 120, _config.textPrimary, TextAlignmentOptions.Center, true);
            title.overflowMode = TextOverflowModes.Overflow;
            UIFactory.Label(root, "Intro", 0, 430, w, 60, _text.Sim("intro_line"), 36, _config.textSecondary, TextAlignmentOptions.Center);
            UIFactory.Button(root, "Play", (w - 320f) / 2f, 580, 320, 96, _text.Get("menu_play"), 44, _config.resultWon, _config.textPrimary, () => onPlay?.Invoke());
            if (onLevels != null)
                UIFactory.Button(root, "Levels", (w - 320f) / 2f, 700, 320, 70, _text.Get("menu_levels"), 32, _config.hudOutline, _config.textPrimary, () => onLevels());
            if (Application.platform != RuntimePlatform.WebGLPlayer)
                UIFactory.Button(root, "Quit", (w - 320f) / 2f, 790, 320, 70, _text.Get("menu_quit"), 36, _config.hudOutline, _config.textPrimary, () => onQuit?.Invoke());
            Title = root.gameObject;
            Title.SetActive(false);
        }

        private void BuildSelect(RectTransform canvas, Action onBack)
        {
            var root = Background(canvas, "LevelSelect");
            float w = _config.referenceResolution.x;
            UIFactory.Label(root, "Header", 0, 120, w, 100, _text.Get("menu_choose_level"), 64, _config.textPrimary, TextAlignmentOptions.Center, true);
            _selectCards = UIFactory.Rect(root, "Cards", 0, 0, w, _config.referenceResolution.y);
            UIFactory.Button(root, "Back", 40, 40, 240, 64, _text.Get("menu_back"), 26, _config.hudOutline, _config.textPrimary, () => onBack?.Invoke());
            Select = root.gameObject;
            Select.SetActive(false);
        }

        private void BuildEnd(RectTransform canvas, Action onLevels)
        {
            var root = Background(canvas, "End");
            float w = _config.referenceResolution.x;
            UIFactory.Label(root, "Title", 0, 250, w, 140, _text.Get("end_title"), 96, _config.resultWon, TextAlignmentOptions.Center, true).overflowMode = TextOverflowModes.Overflow;
            _endTime = UIFactory.Label(root, "Time", 0, 430, w, 70, "", 48, _config.textPrimary, TextAlignmentOptions.Center);
            UIFactory.Label(root, "Credits", 0, 540, w, 50, _text.Get("end_credits"), 32, _config.textSecondary, TextAlignmentOptions.Center);
            UIFactory.Button(root, "Levels", (w - 320f) / 2f, 700, 320, 90, _text.Get("end_levels"), 40, _config.hudOutline, _config.textPrimary, () => onLevels?.Invoke());
            End = root.gameObject;
            End.SetActive(false);
        }

        private void BuildCard(LevelCard card, float x, float y)
        {
            var bg = card.Locked ? new Color(_config.hudPanel.r, _config.hudPanel.g, _config.hudPanel.b, 0.35f) : _config.hudPanel;
            var main = card.Locked ? new Color(_config.textPrimary.r, _config.textPrimary.g, _config.textPrimary.b, 0.35f) : _config.textPrimary;
            var second = card.Locked ? new Color(_config.textSecondary.r, _config.textSecondary.g, _config.textSecondary.b, 0.35f) : _config.textSecondary;
            int number = card.Number;
            var button = UIFactory.Button(_selectCards, "Level" + number, x, y, CardWidth, CardHeight, "", 20, bg, main, () => _onPick?.Invoke(number));
            button.interactable = !card.Locked;
            var t = button.transform;
            UIFactory.Label(t, "Number", 0, 24, CardWidth, 100, number.ToString(), 80, main, TextAlignmentOptions.Center, true);
            var title = UIFactory.Label(t, "Name", 15, 140, CardWidth - 30, 50, card.Locked ? _text.Get("menu_locked") : card.Title, 34, main, TextAlignmentOptions.Center, true);
            title.overflowMode = TextOverflowModes.Ellipsis;
            var teaches = UIFactory.Label(t, "Teaches", 20, 195, CardWidth - 40, 70, card.Locked ? "" : card.Teaches, 22, second, TextAlignmentOptions.Top);
            teaches.textWrappingMode = TextWrappingModes.Normal;
            string best = card.Locked ? "" : (string.IsNullOrEmpty(card.Best) ? _text.Get("menu_not_won") : _text.Format("menu_best", ("time", card.Best)));
            UIFactory.Label(t, "Best", 0, 280, CardWidth, 36, best, 24, second, TextAlignmentOptions.Center);
        }

        private RectTransform Background(RectTransform canvas, string name)
        {
            var rt = UIFactory.Stretch(UIFactory.Rect(canvas, name, 0, 0, 0, 0));
            UIFactory.Fill(rt, _config.hudBackground, false, true);
            return rt;
        }
    }
}
