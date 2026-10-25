using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// Short messages (GDD v1.0 §6.2): each stays for toastSeconds, up to maxToasts stacked. Puzzle levels show them
    /// at the top centre, newest at the bottom; Day mode tucks them into the bottom-right corner above the timer,
    /// small and left-aligned, newest at the bottom, so they never cover the house. Never blocks clicks.
    /// </summary>
    public sealed class ToastView
    {
        private const float Gap = 8f;
        private float _width = 1000f, _height = 56f, _fontMax = 26f;
        private bool _corner;
        private float _top = 236f;

        private sealed class Toast
        {
            public RectTransform Root;
            public Image Background;
            public TextMeshProUGUI Label;
            public float Expires;
        }

        private readonly List<Toast> _toasts = new List<Toast>();
        private RectTransform _root;
        private TrippedViewConfig _config;

        /// <summary>Texts currently on screen, oldest first.</summary>
        public IEnumerable<string> Visible
        {
            get
            {
                foreach (var t in _toasts) yield return t.Label.text;
            }
        }

        /// <summary>Creates the (empty) toast area.</summary>
        public void Build(RectTransform canvas, TrippedViewConfig config, float top = 236f)
        {
            _top = top;
            _config = config;
            _root = UIFactory.Anchored(canvas, "Toasts", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_top), new Vector2(_width, 3 * (_height + Gap)));
        }

        /// <summary>
        /// Creates the toast area in the bottom-right corner (Day mode): <paramref name="width"/> wide, its lowest
        /// toast <paramref name="bottom"/> reference pixels above the screen's bottom edge, <paramref name="right"/>
        /// in from the right edge.
        /// </summary>
        public void BuildCorner(RectTransform canvas, TrippedViewConfig config, float width, float right, float bottom)
        {
            _config = config;
            _corner = true;
            _width = width;
            _height = 62f;
            _fontMax = 22f;
            _root = UIFactory.Anchored(canvas, "Toasts", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-right, bottom), new Vector2(_width, _height));
        }

        /// <summary>Shows a message; the oldest is dropped when there are already maxToasts.</summary>
        public void Show(string message, float now)
        {
            if (string.IsNullOrEmpty(message)) return;
            while (_toasts.Count >= Mathf.Max(1, _config.maxToasts))
            {
                Object.Destroy(_toasts[0].Root.gameObject);
                _toasts.RemoveAt(0);
            }
            var bg = _config.hudBackground;
            bg.a = 0.92f;
            var img = UIFactory.Panel(_root, "Toast", 0, 0, _width, _height, bg, true, false);
            var text = UIFactory.Rect(img.transform, "Text", 0, 0, _width, _height);
            var label = UIFactory.LabelOn(UIFactory.Stretch(text), message, _fontMax,
                _config.textPrimary, _corner ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center, true);
            if (_corner)
            {
                // Two lines rather than shrinking a long cause sentence to nothing.
                label.margin = new Vector4(14f, 4f, 10f, 4f);
                label.textWrappingMode = TextWrappingModes.Normal;
            }
            label.enableAutoSizing = true;
            label.fontSizeMin = _corner ? 17 : 16;
            label.fontSizeMax = _fontMax;
            _toasts.Add(new Toast { Root = img.rectTransform, Background = img, Label = label, Expires = now + _config.toastSeconds });
            Layout();
        }

        /// <summary>Removes expired toasts and fades the last half second.</summary>
        public void Sync(float now)
        {
            bool changed = false;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                float left = t.Expires - now;
                if (left <= 0f)
                {
                    Object.Destroy(t.Root.gameObject);
                    _toasts.RemoveAt(i);
                    changed = true;
                    continue;
                }
                float a = Mathf.Clamp01(left / 0.5f);
                var c = t.Label.color;
                if (!Mathf.Approximately(c.a, a))
                {
                    c.a = a;
                    t.Label.color = c;
                    var b = t.Background.color;
                    b.a = 0.92f * a;
                    t.Background.color = b;
                }
            }
            if (changed) Layout();
        }

        private void Layout()
        {
            // Corner: the newest sits on the bottom row and older ones move up.
            for (int i = 0; i < _toasts.Count; i++)
                _toasts[i].Root.anchoredPosition = _corner
                    ? new Vector2(0f, (_toasts.Count - 1 - i) * (_height + Gap))
                    : new Vector2(0f, -i * (_height + Gap));
        }
    }
}
