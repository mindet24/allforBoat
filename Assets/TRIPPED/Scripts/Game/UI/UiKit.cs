using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// Builders for the G6 look: chunky rounded cards with a drop shadow, bold TMP text and animated buttons.
    /// Sizes and colours come from <see cref="TrippedUiConfig"/>; positions are canvas reference pixels.
    /// </summary>
    public static class UiKit
    {
        /// <summary>
        /// A card: a root holding a soft shadow and a rounded body. Returns the body (put contents under it). The root is
        /// the body's parent, for moving and scaling the whole card.
        /// </summary>
        public static Image Card(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size,
            Color color, TrippedUiConfig ui, bool shadow = true, bool raycast = false)
        {
            var root = UIFactory.Anchored(parent, name, anchor, pivot, position, size);
            if (shadow)
            {
                var sh = UIFactory.Stretch(UIFactory.Rect(root, "Shadow", 0, 0, 0, 0));
                sh.offsetMin = ui.shadowOffset;
                sh.offsetMax = ui.shadowOffset;
                var shImg = sh.gameObject.AddComponent<Image>();
                shImg.sprite = UiArt.Rounded;
                shImg.type = Image.Type.Sliced;
                shImg.color = ui.shadow;
                shImg.raycastTarget = false;
            }
            var body = UIFactory.Stretch(UIFactory.Rect(root, "Body", 0, 0, 0, 0));
            var img = body.gameObject.AddComponent<Image>();
            img.sprite = UiArt.Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>A card placed from the parent's top-left at (x, y).</summary>
        public static Image CardAt(Transform parent, string name, float x, float y, float w, float h, Color color, TrippedUiConfig ui, bool shadow = true)
        {
            return Card(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h), color, ui, shadow);
        }

        /// <summary>Bold readable label filling a rect given from the parent's top-left.</summary>
        public static TextMeshProUGUI Text(Transform parent, string name, float x, float y, float w, float h, string text, float size,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true)
        {
            var t = UIFactory.Label(parent, name, x, y, w, h, text, size, color, align, bold);
            t.overflowMode = TextOverflowModes.Overflow;
            return bold ? t.AsDisplay() : t;
        }

        /// <summary>A label that fills its parent.</summary>
        public static TextMeshProUGUI Fill(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true)
        {
            var rt = UIFactory.Stretch(UIFactory.Rect(parent, name, 0, 0, 0, 0));
            var t = UIFactory.LabelOn(rt, text, size, color, align, bold);
            t.overflowMode = TextOverflowModes.Overflow;
            return bold ? t.AsDisplay() : t;
        }

        /// <summary>A plain image (no slicing) at a rect from the parent's top-left.</summary>
        public static Image Icon(Transform parent, string name, float x, float y, float w, float h, Sprite sprite, Color color)
        {
            var img = UIFactory.Panel(parent, name, x, y, w, h, color, false, false);
            img.sprite = sprite;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>
        /// An animated chunky button (hover grows, press squashes, keyboard focus shows a ring). Anchored at
        /// <paramref name="anchor"/> with its centre at <paramref name="position"/>.
        /// </summary>
        public static ChunkyButton Button(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, string label,
            float fontSize, Color background, Color textColor, TrippedUiConfig ui, Action onClick, Sprite icon = null, Color? iconColor = null)
        {
            var body = Card(parent, name, anchor, new Vector2(0.5f, 0.5f), position, size, background, ui, true, true);
            var root = (RectTransform)body.transform.parent;
            var ringRt = UIFactory.Stretch(UIFactory.Rect(root, "Focus", 0, 0, 0, 0));
            ringRt.offsetMin = new Vector2(-8f, -8f);
            ringRt.offsetMax = new Vector2(8f, 8f);
            var ring = ringRt.gameObject.AddComponent<Image>();
            ring.sprite = UiArt.RoundedOutline;
            ring.type = Image.Type.Sliced;
            ring.color = ui.buttonFocusRing;
            ring.raycastTarget = false;
            ring.enabled = false;

            var button = body.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            var label1 = Fill(body.transform, "Label", label, fontSize, textColor);
            if (icon != null)
            {
                // Icon on the left, the label beside it.
                float s = size.y * 0.56f;
                var iconRt = UIFactory.Anchored(body.transform, "Icon", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(size.y * 0.62f, 0f), new Vector2(s, s));
                var img = iconRt.gameObject.AddComponent<Image>();
                img.sprite = icon;
                img.color = iconColor ?? Color.white;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var lrt = label1.rectTransform;
                lrt.offsetMin = new Vector2(size.y * 1.15f, 0f);
                label1.alignment = TextAlignmentOptions.MidlineLeft;
            }
            var chunky = body.gameObject.AddComponent<ChunkyButton>();
            chunky.Init(root, ring, label1, ui, onClick);
            button.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                chunky.Invoke();
            });
            return chunky;
        }

        /// <summary>A full-screen image that blocks clicks (menu backdrop or dim layer).</summary>
        public static Image Backdrop(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rt = UIFactory.Stretch(UIFactory.Rect(parent, name, 0, 0, 0, 0));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = true;
            return img;
        }

        /// <summary>A canvas with the project's scaler settings.</summary>
        public static RectTransform Canvas(GameObject go, TrippedViewConfig config, int sortingOrder)
        {
            var canvas = go.GetComponent<Canvas>();
            if (canvas == null) canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = config.referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = config.matchWidthOrHeight;
            if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }
    }

    /// <summary>
    /// A button that grows on hover, squashes on press and shows a ring while it has keyboard focus. The click itself
    /// is the uGUI Button's; <see cref="MenuNav"/> calls <see cref="Invoke"/> for Enter.
    /// </summary>
    public sealed class ChunkyButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _root;
        private Image _ring;
        private TrippedUiConfig _ui;
        private Action _onClick;
        private bool _hover;
        private bool _pressed;
        private float _pressFlash;

        /// <summary>The text on the button.</summary>
        public TextMeshProUGUI Label { get; private set; }

        /// <summary>The whole button (shadow and body), for placing it.</summary>
        public RectTransform Root => _root;

        /// <summary>True while keyboard focus is on this button.</summary>
        public bool Focused { get; private set; }

        /// <summary>Raised when the pointer enters (the menu moves keyboard focus here).</summary>
        public event Action<ChunkyButton> Hovered;

        internal void Init(RectTransform root, Image ring, TextMeshProUGUI label, TrippedUiConfig ui, Action onClick)
        {
            _root = root;
            _ring = ring;
            Label = label;
            _ui = ui;
            _onClick = onClick;
        }

        /// <summary>Shows or hides the focus ring.</summary>
        public void SetFocused(bool focused)
        {
            Focused = focused;
            if (_ring != null) _ring.enabled = focused;
        }

        /// <summary>Runs the click action with a short press squash.</summary>
        public void Invoke()
        {
            _pressFlash = 0.12f;
            _onClick?.Invoke();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            Hovered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData e) => _pressed = true;

        public void OnPointerUp(PointerEventData e) => _pressed = false;

        private void OnDisable()
        {
            _hover = false;
            _pressed = false;
            if (_root != null) _root.localScale = Vector3.one;
        }

        private void Update()
        {
            if (_root == null || _ui == null) return;
            float dt = Time.unscaledDeltaTime;
            _pressFlash = Mathf.Max(0f, _pressFlash - dt);
            float target = _pressed || _pressFlash > 0f ? _ui.buttonPressScale : (_hover || Focused ? _ui.buttonHoverScale : 1f);
            float s = Mathf.Lerp(_root.localScale.x, target, 1f - Mathf.Exp(-_ui.buttonAnimSpeed * dt));
            _root.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>
    /// Keyboard navigation of one menu: arrows / W S A D move the focus, Enter (and Space unless turned off) presses
    /// the focused button. Hovering a button with the mouse moves the focus too. Acts only while its object is active,
    /// and never in the frame it was shown (so the key that opened a menu does not also press its first button).
    /// </summary>
    public sealed class MenuNav : MonoBehaviour
    {
        private readonly List<ChunkyButton> _buttons = new List<ChunkyButton>();
        private int _index = -1;
        private int _enabledFrame = -1;

        /// <summary>false = Space does not press buttons (the pause menu: Space is the game's resume key).</summary>
        public bool AllowSpace { get; set; } = true;

        /// <summary>Index of the focused button, -1 when there is none.</summary>
        public int Index => _index;

        /// <summary>The buttons in focus order.</summary>
        public IReadOnlyList<ChunkyButton> Buttons => _buttons;

        /// <summary>Adds a button at the end of the focus order.</summary>
        public void Add(ChunkyButton button)
        {
            if (button == null) return;
            _buttons.Add(button);
            button.Hovered += b => Focus(_buttons.IndexOf(b));
            if (_index < 0) Focus(0);
        }

        /// <summary>Removes every button.</summary>
        public void Clear()
        {
            _buttons.Clear();
            _index = -1;
        }

        /// <summary>Moves keyboard focus to button <paramref name="index"/>.</summary>
        public void Focus(int index)
        {
            _index = _buttons.Count == 0 ? -1 : Mathf.Clamp(index, 0, _buttons.Count - 1);
            for (int i = 0; i < _buttons.Count; i++) _buttons[i].SetFocused(i == _index);
        }

        private void OnEnable()
        {
            _enabledFrame = Time.frameCount;
            Focus(0);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _buttons.Count == 0 || Time.frameCount == _enabledFrame) return;
            int delta = 0;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) delta -= 1;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame) delta += 1;
            if (delta != 0) Focus(MenuNavRules.Step(_index, delta, _buttons.Count));
            bool submit = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || (AllowSpace && kb.spaceKey.wasPressedThisFrame);
            if (submit && _index >= 0 && _index < _buttons.Count && _buttons[_index].isActiveAndEnabled) _buttons[_index].Invoke();
        }
    }
}
