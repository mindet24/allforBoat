using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// Builds uGUI pieces in code (GDD v1.0 §8.5). Positions are in canvas reference pixels (1920×1080) measured
    /// from the top-left of the parent, like the layout tables of GDD v1.0 §6.2.
    /// </summary>
    public static class UIFactory
    {
        /// <summary>A RectTransform anchored to the top-left of its parent at (x, y) with size (w, h).</summary>
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            return Anchored(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
        }

        /// <summary>A RectTransform with one anchor point (min = max), pivot, offset and size.</summary>
        public static RectTransform Anchored(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Makes a RectTransform fill its parent.</summary>
        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>A coloured (optionally rounded) image on an existing RectTransform.</summary>
        public static Image Fill(RectTransform rt, Color color, bool rounded = true, bool raycast = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            if (rounded)
            {
                img.sprite = PlaceholderArt.UiRounded;
                img.type = Image.Type.Sliced;
            }
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>A coloured panel at (x, y, w, h) from the parent's top-left.</summary>
        public static Image Panel(Transform parent, string name, float x, float y, float w, float h, Color color, bool rounded = true, bool raycast = true)
        {
            return Fill(Rect(parent, name, x, y, w, h), color, rounded, raycast);
        }

        /// <summary>A TextMeshPro label. Labels never block clicks.</summary>
        public static TextMeshProUGUI Label(Transform parent, string name, float x, float y, float w, float h, string text, float size,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, bool bold = false)
        {
            return LabelOn(Rect(parent, name, x, y, w, h), text, size, color, alignment, bold);
        }

        /// <summary>A TextMeshPro label on an existing RectTransform.</summary>
        public static TextMeshProUGUI LabelOn(RectTransform rt, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, bool bold = false)
        {
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        /// <summary>
        /// A rounded button with a centred label. It never keeps keyboard focus, so Space and Enter stay game keys.
        /// </summary>
        public static Button Button(Transform parent, string name, float x, float y, float w, float h, string label, float size,
            Color background, Color textColor, UnityAction onClick)
        {
            var img = Panel(parent, name, x, y, w, h, background);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                onClick?.Invoke();
            });
            var text = LabelOn(Stretch(Rect(img.transform, "Label", 0, 0, w, h)), label, size, textColor, TextAlignmentOptions.Center, true);
            text.overflowMode = TextOverflowModes.Overflow;
            return button;
        }

        /// <summary>A horizontal bar: a background with a fill anchored left. Set the fill with <see cref="SetBar"/>.</summary>
        public static Image Bar(Transform parent, string name, float x, float y, float w, float h, Color background, Color fillColor)
        {
            var bg = Panel(parent, name, x, y, w, h, background, true, false);
            var fill = Fill(Stretch(Rect(bg.transform, "Fill", 0, 0, w, h)), fillColor, true, false);
            return fill;
        }

        /// <summary>Sets how much of a bar made by <see cref="Bar"/> is filled, 0..1.</summary>
        public static void SetBar(Image fill, float amount)
        {
            var rt = fill.rectTransform;
            float a = Mathf.Clamp01(amount);
            if (Mathf.Approximately(rt.anchorMax.x, a)) return;
            rt.anchorMax = new Vector2(a, 1f);
            fill.enabled = a > 0.001f;
        }
    }
}
