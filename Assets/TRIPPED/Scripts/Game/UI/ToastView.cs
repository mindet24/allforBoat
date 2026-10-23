using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// Short messages at the top centre (GDD v1.0 §6.2): each stays for toastSeconds, up to maxToasts stacked,
    /// newest at the bottom. Used for refusals and cause sentences. Never blocks clicks.
    /// </summary>
    public sealed class ToastView
    {
        private const float Width = 1000f, Height = 56f, Gap = 8f;
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
            _root = UIFactory.Anchored(canvas, "Toasts", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_top), new Vector2(Width, 3 * (Height + Gap)));
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
            var img = UIFactory.Panel(_root, "Toast", 0, 0, Width, Height, bg, true, false);
            var label = UIFactory.LabelOn(UIFactory.Stretch(UIFactory.Rect(img.transform, "Text", 0, 0, Width, Height)), message, 26,
                _config.textPrimary, TextAlignmentOptions.Center, true);
            label.enableAutoSizing = true;
            label.fontSizeMin = 16;
            label.fontSizeMax = 26;
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
            for (int i = 0; i < _toasts.Count; i++)
                _toasts[i].Root.anchoredPosition = new Vector2(0f, -i * (Height + Gap));
        }
    }
}
