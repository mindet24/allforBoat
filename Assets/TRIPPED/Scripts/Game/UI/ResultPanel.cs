using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// The end-of-level panel. Day mode (<see cref="ShowDay"/>): Day complete! / Out of hearts!, 0-3 stars that pop in
    /// one by one, the score on a coin, requests done and missed, the best score, and Retry / Main Menu. Classic levels
    /// (<see cref="Show"/>): ALL DONE! or TRIPPED!, the cause sentence, time and trips, and Retry / Next / Levels.
    /// A full-screen dim layer behind it blocks clicks on the house. Enter / Space press the focused button.
    /// </summary>
    public sealed class ResultPanel
    {
        private GameObject _root;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _cause;
        private TextMeshProUGUI _detail;
        private TextMeshProUGUI _stats;
        private TextMeshProUGUI _score;
        private TextMeshProUGUI _best;
        private GameObject _scoreRow;
        private Image[] _stars;
        private ChunkyButton _retry;
        private ChunkyButton _next;
        private ChunkyButton _menu;
        private MenuNav _nav;
        private TrippedViewConfig _config;
        private TrippedUiConfig _ui;
        private HudText _text;
        private int _starCount;
        private float _shownAt;
        private bool _day;

        /// <summary>True while the panel is on screen.</summary>
        public bool IsShown => _root != null && _root.activeSelf;

        /// <summary>Title text shown, for checks.</summary>
        public string Title => _title.text;

        /// <summary>Cause sentence shown, for checks.</summary>
        public string Cause => _cause.text;

        /// <summary>Stars shown in day mode, for checks.</summary>
        public int Stars => _starCount;

        /// <summary>Builds the (hidden) panel.</summary>
        /// <param name="onMenu">Main Menu (day) or Levels (classic).</param>
        public void Build(RectTransform canvas, LevelController controller, TrippedViewConfig config, HudText text, Action onMenu = null)
        {
            _config = config;
            _text = text;
            _ui = TrippedUiConfig.LoadOrDefault();
            var ui = _ui;
            var dim = UiKit.Backdrop(canvas, "Result", ui.dim).rectTransform;
            _root = dim.gameObject;

            var size = ui.resultPanelSize;
            var body = UiKit.Card(dim, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, ui.cream, ui);
            var t = body.transform;
            float w = size.x;
            _title = UiKit.Text(t, "Title", 0, 26, w, 90, "", 72, ui.charcoal);

            _stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float s = i == 1 ? 140f : 112f;
                float x = w * 0.5f + (i - 1) * 150f - s * 0.5f;
                float y = (i == 1 ? 116f : 140f);
                var img = UiKit.Icon(t, "Star " + i, x, y, s, s, UiArt.Star, ui.creamDark);
                _stars[i] = img;
            }

            var row = UIFactory.Rect(t, "ScoreRow", 0, 270, w, 100);
            _scoreRow = row.gameObject;
            UiKit.Icon(row, "Coin", w * 0.5f - 160f, 4, 92, 92, UiArt.Coin, Color.white);
            _score = UiKit.Text(row, "Score", w * 0.5f - 60f, 0, 200, 100, "", 76, ui.charcoal);

            _cause = UiKit.Text(t, "Cause", 40, 140, w - 80, 110, "", 30, ui.charcoal, TextAlignmentOptions.Center, false);
            _cause.textWrappingMode = TextWrappingModes.Normal;
            _detail = UiKit.Text(t, "Detail", 40, 250, w - 80, 60, "", 22, ui.charcoalSoft, TextAlignmentOptions.Center, false);
            _detail.textWrappingMode = TextWrappingModes.Normal;
            _stats = UiKit.Text(t, "Stats", 0, 380, w, 50, "", 32, ui.charcoalSoft);
            _best = UiKit.Text(t, "Best", 0, 430, w, 40, "", 26, ui.accent);

            var bs = new Vector2(260f, 84f);
            _retry = UiKit.Button(t, "Retry", new Vector2(0.5f, 0f), new Vector2(-150f, 80f), bs, text.Get("g6_retry"), 36, ui.buttonPrimary, Color.white, ui, controller.Restart);
            _next = UiKit.Button(t, "Next", new Vector2(0.5f, 0f), new Vector2(0f, 80f), bs, text.Get("result_next"), 36, ui.accent, ui.charcoal, ui, controller.LoadNextLevel);
            _menu = UiKit.Button(t, "Menu", new Vector2(0.5f, 0f), new Vector2(150f, 80f), bs, text.Get("g6_main_menu"), 32, ui.buttonSecondary, ui.charcoal, ui, () => onMenu?.Invoke());
            _nav = _root.AddComponent<MenuNav>();
            _root.SetActive(false);
        }

        /// <summary>Shows a classic-level result.</summary>
        /// <param name="won">ALL DONE! when true, TRIPPED! when false.</param>
        /// <param name="cause">Cause sentence from the CauseFormatter.</param>
        /// <param name="detail">Extra line (for a loss: the last trip sentence). Empty = none.</param>
        /// <param name="hasNext">Show the Next button (won and a next level exists).</param>
        public void Show(bool won, string cause, string detail, string time, int trips, bool hasNext, string stats = null, string title = null)
        {
            _day = false;
            _title.text = title ?? (won ? _text.Get("result_won") : _text.Get("result_lost"));
            _title.color = won ? _ui.good : _ui.bad;
            _cause.text = cause ?? "";
            _detail.text = detail ?? "";
            _stats.text = stats ?? _text.Format("result_stats", ("time", time), ("trips", trips));
            _best.text = "";
            foreach (var s in _stars) s.gameObject.SetActive(false);
            _scoreRow.SetActive(false);
            _cause.gameObject.SetActive(true);
            _detail.gameObject.SetActive(true);
            _menu.Label.text = _text.Get("g6_levels");
            _next.Root.gameObject.SetActive(hasNext);
            Arrange(hasNext);
            Open();
        }

        /// <summary>Shows the end of a day.</summary>
        public void ShowDay(bool won, int score, int done, int missed, int stars, int best, bool newBest)
        {
            _day = true;
            _starCount = stars;
            _title.text = _text.Get(won ? "g6_win" : "g6_lose");
            _title.color = won ? _ui.good : _ui.bad;
            _cause.gameObject.SetActive(false);
            _detail.gameObject.SetActive(false);
            _scoreRow.SetActive(true);
            _score.text = score.ToString();
            foreach (var s in _stars)
            {
                s.gameObject.SetActive(true);
                s.color = _ui.creamDark;
                s.rectTransform.localScale = Vector3.one;
            }
            _stats.text = _text.Get("g6_done") + "  " + done + "     " + _text.Get("g6_missed") + "  " + missed;
            _best.text = newBest ? _text.Get("g6_new_best") : (best > 0 ? _text.Format("g6_best", ("score", best)) : "");
            _menu.Label.text = _text.Get("g6_main_menu");
            _next.Root.gameObject.SetActive(false);
            Arrange(false);
            Open();
        }

        private void Arrange(bool hasNext)
        {
            _nav.Clear();
            if (hasNext)
            {
                _retry.Root.anchoredPosition = new Vector2(-280f, 80f);
                _next.Root.anchoredPosition = new Vector2(0f, 80f);
                _menu.Root.anchoredPosition = new Vector2(280f, 80f);
                _nav.Add(_next);
                _nav.Add(_retry);
                _nav.Add(_menu);
                return;
            }
            _retry.Root.anchoredPosition = new Vector2(-150f, 80f);
            _menu.Root.anchoredPosition = new Vector2(150f, 80f);
            _nav.Add(_retry);
            _nav.Add(_menu);
        }

        private void Open()
        {
            _shownAt = Time.unscaledTime;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _nav.Focus(0);
        }

        /// <summary>Animates the stars popping in. Call every frame (the HUD does).</summary>
        public void Sync(float now)
        {
            if (!IsShown || !_day) return;
            for (int i = 0; i < _stars.Length; i++)
            {
                float t = now - _shownAt - (i + 1) * _ui.starPopDelay;
                bool lit = i < _starCount && t >= 0f;
                _stars[i].color = lit ? _ui.accent : _ui.creamDark;
                float s = 1f;
                if (lit && t < 0.3f) s = 1f + 0.5f * Mathf.Sin(t / 0.3f * Mathf.PI);
                _stars[i].rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
