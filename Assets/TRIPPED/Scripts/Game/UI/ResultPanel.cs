using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// The end-of-level panel (GDD v1.0 §6.1): TRIPPED! or ALL DONE!, the cause sentence, time and trips, and
    /// Retry (R) / Next buttons. A full-screen dim layer behind it blocks clicks on the house.
    /// </summary>
    public sealed class ResultPanel
    {
        private GameObject _root;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _cause;
        private TextMeshProUGUI _detail;
        private TextMeshProUGUI _stats;
        private Button _next;
        private RectTransform _retry;
        private TrippedViewConfig _config;
        private HudText _text;

        /// <summary>True while the panel is on screen.</summary>
        public bool IsShown => _root != null && _root.activeSelf;

        /// <summary>Title text shown, for checks.</summary>
        public string Title => _title.text;

        /// <summary>Cause sentence shown, for checks.</summary>
        public string Cause => _cause.text;

        /// <summary>Builds the (hidden) panel.</summary>
        public void Build(RectTransform canvas, LevelController controller, TrippedViewConfig config, HudText text)
        {
            _config = config;
            _text = text;
            var dim = UIFactory.Stretch(UIFactory.Rect(canvas, "Result", 0, 0, 0, 0));
            UIFactory.Fill(dim, new Color(0f, 0f, 0f, 0.6f), false, true);
            _root = dim.gameObject;

            var panelRt = UIFactory.Anchored(dim, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 480f));
            UIFactory.Fill(panelRt, config.hudPanel);
            UIFactory.Panel(panelRt, "Outline", 0, 0, 800, 480, config.hudOutline, true, false).sprite = PlaceholderArt.UiRoundedOutline;
            _title = UIFactory.Label(panelRt, "Title", 0, 30, 800, 90, "", 72, config.textPrimary, TextAlignmentOptions.Center, true);
            _cause = UIFactory.Label(panelRt, "Cause", 40, 130, 720, 90, "", 30, config.textPrimary, TextAlignmentOptions.Center);
            _cause.textWrappingMode = TextWrappingModes.Normal;
            _detail = UIFactory.Label(panelRt, "Detail", 40, 222, 720, 60, "", 22, config.textSecondary, TextAlignmentOptions.Center);
            _detail.textWrappingMode = TextWrappingModes.Normal;
            _stats = UIFactory.Label(panelRt, "Stats", 0, 290, 800, 44, "", 28, config.textSecondary, TextAlignmentOptions.Center, true);
            _retry = (RectTransform)UIFactory.Button(panelRt, "Retry", 150, 370, 230, 72, text.Get("result_retry"), 28, config.hudOutline, config.textPrimary, controller.Restart).transform;
            _next = UIFactory.Button(panelRt, "Next", 420, 370, 230, 72, text.Get("result_next"), 28, config.resultWon, config.textPrimary, controller.LoadNextLevel);
            _root.SetActive(false);
        }

        /// <summary>Shows the result.</summary>
        /// <param name="won">ALL DONE! when true, TRIPPED! when false.</param>
        /// <param name="cause">Cause sentence from the CauseFormatter.</param>
        /// <param name="detail">Extra line (for a loss: the last trip sentence). Empty = none.</param>
        /// <param name="hasNext">Show the Next button (won and a next level exists).</param>
        public void Show(bool won, string cause, string detail, string time, int trips, bool hasNext, string stats = null, string title = null)
        {
            _title.text = title ?? (won ? _text.Get("result_won") : _text.Get("result_lost"));
            _title.color = won ? _config.resultWon : _config.resultLost;
            _cause.text = cause ?? "";
            _detail.text = detail ?? "";
            _stats.text = stats ?? _text.Format("result_stats", ("time", time), ("trips", trips));
            _next.gameObject.SetActive(hasNext);
            _retry.anchoredPosition = new Vector2(hasNext ? 150f : 285f, -370f);
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
    }
}
