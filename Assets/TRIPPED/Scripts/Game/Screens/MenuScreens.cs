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

    /// <summary>What the menu buttons do. Any action may be null (the button then does nothing).</summary>
    public sealed class MenuActions
    {
        public Action Play;
        public Action HowTo;
        public Action Quit;
        public Action Classic;
        public Action BackToTitle;
        public Action EndLevels;
        public Action<int> PickLevel;
        public Action Resume;
        public Action Restart;
        public Action MainMenu;
    }

    /// <summary>
    /// The uGUI of every menu (G6 look: warm gradient, cream rounded cards, charcoal bold text, animated buttons with
    /// keyboard focus): Title, How to Play, Pause, classic Level Select and classic End. Built in code on a Screen
    /// Space Overlay canvas scaled from 1920x1080. <see cref="ScreenFlow"/> decides which one is visible.
    /// </summary>
    public sealed class MenuScreens
    {
        private const float CardWidth = 300f, CardHeight = 330f, CardGap = 30f;

        private readonly TrippedViewConfig _config;
        private readonly TrippedUiConfig _ui;
        private readonly HudText _text;
        private RectTransform _selectCards;
        private MenuNav _selectNav;
        private ChunkyButton _selectBack;
        private MenuActions _actions;
        private TextMeshProUGUI _endTime;
        private TextMeshProUGUI _best;
        private Sprite _backdrop;

        /// <summary>The Title screen object.</summary>
        public GameObject Title { get; private set; }
        /// <summary>The How to Play screen object.</summary>
        public GameObject HowTo { get; private set; }
        /// <summary>The Level Select screen object.</summary>
        public GameObject Select { get; private set; }
        /// <summary>The End screen object (after the last classic level).</summary>
        public GameObject End { get; private set; }
        /// <summary>The Pause overlay (the level stays loaded behind it).</summary>
        public GameObject Pause { get; private set; }

        public MenuScreens(TrippedViewConfig config, TrippedUiConfig ui, HudText text)
        {
            _config = config;
            _ui = ui;
            _text = text;
        }

        /// <summary>Creates all screens (hidden) under <paramref name="canvas"/>.</summary>
        public void Build(RectTransform canvas, MenuActions actions)
        {
            _actions = actions;
            UiArt.Configure(_ui);
            _backdrop = UiArt.Gradient(_ui.backdropTop, _ui.backdropBottom);
            BuildTitle(canvas);
            BuildHowTo(canvas);
            BuildSelect(canvas);
            BuildEnd(canvas);
            BuildPause(canvas);
        }

        /// <summary>Shows the best day score on the Title (0 = hidden).</summary>
        public void SetBestScore(int score)
        {
            _best.text = score > 0 ? _text.Format("g6_best", ("score", score)) : "";
        }

        /// <summary>Fills the Level Select with one card per level. Locked cards are dimmed and do nothing.</summary>
        public void ShowCards(IReadOnlyList<LevelCard> cards)
        {
            for (int i = _selectCards.childCount - 1; i >= 0; i--)
            {
                var child = _selectCards.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            _selectNav.Clear();
            int n = cards.Count;
            float total = n * CardWidth + Mathf.Max(0, n - 1) * CardGap;
            float x0 = (_config.referenceResolution.x - total) / 2f;
            for (int i = 0; i < n; i++) BuildCard(cards[i], x0 + i * (CardWidth + CardGap) + CardWidth * 0.5f, 330f + CardHeight * 0.5f);
            _selectNav.Add(_selectBack);
            _selectNav.Focus(0);
        }

        /// <summary>Sets the total time shown on the End screen.</summary>
        public void SetEndTime(string time)
        {
            _endTime.text = _text.Format("end_time", ("time", time));
        }

        private float W => _config.referenceResolution.x;

        private RectTransform Screen(RectTransform canvas, string name, bool gradient)
        {
            var img = UiKit.Backdrop(canvas, name, gradient ? Color.white : _ui.dim, gradient ? _backdrop : null);
            return img.rectTransform;
        }

        private ChunkyButton MenuButton(Transform parent, string name, float centreY, string label, bool primary, Action onClick)
        {
            var ui = _ui;
            return UiKit.Button(parent, name, new Vector2(0.5f, 1f), new Vector2(0f, -centreY), ui.menuButtonSize, label, 44,
                primary ? ui.buttonPrimary : ui.buttonSecondary, primary ? Color.white : ui.charcoal, ui, onClick);
        }

        private void BuildTitle(RectTransform canvas)
        {
            var ui = _ui;
            var root = Screen(canvas, "Title", true);
            Decorate(root);

            // TRIPPED! with a chunky offset shadow.
            UiKit.Text(root, "NameShadow", 10, 130 + 12, W, 220, _text.Get("g6_title"), 210, ui.charcoal).characterSpacing = 4f;
            var name = UiKit.Text(root, "Name", 0, 130, W, 220, _text.Get("g6_title"), 210, ui.cream);
            name.characterSpacing = 4f;

            var tag = UiKit.Card(root, "Tagline", new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -400f), new Vector2(520f, 76f), ui.charcoal, ui);
            tag.transform.parent.localRotation = Quaternion.Euler(0f, 0f, -3f);
            UiKit.Fill(tag.transform, "Text", _text.Get("g6_tagline"), 44, ui.accent);

            var nav = root.gameObject.AddComponent<MenuNav>();
            float y = 540f;
            float step = ui.menuButtonSize.y + ui.menuButtonGap;
            nav.Add(MenuButton(root, "Play", y, _text.Get("g6_play"), true, () => _actions.Play?.Invoke()));
            nav.Add(MenuButton(root, "HowTo", y + step, _text.Get("g6_howto"), false, () => _actions.HowTo?.Invoke()));
            if (Application.platform != RuntimePlatform.WebGLPlayer)
                nav.Add(MenuButton(root, "Quit", y + 2f * step, _text.Get("g6_quit"), false, () => _actions.Quit?.Invoke()));
            _best = UiKit.Text(root, "Best", 0, y + 3f * step - 30f, W, 50, "", 32, ui.charcoal);

            if (_actions.Classic != null)
                nav.Add(UiKit.Button(root, "Classic", new Vector2(1f, 0f), new Vector2(-190f, 70f), new Vector2(300f, 70f), _text.Get("g6_classic"), 28,
                    ui.charcoal, ui.cream, ui, () => _actions.Classic()));
            UiKit.Text(root, "Keys", 0, 1080 - 70, W, 40, _text.Get("g6_menu_keys"), 24, ui.charcoal, TextAlignmentOptions.Center, false);
            Title = root.gameObject;
            Title.SetActive(false);
        }

        /// <summary>Appliance sprites tilted around the edges of the Title for a playful look.</summary>
        private void Decorate(RectTransform root)
        {
            var spots = new (string type, float x, float y, float size, float angle)[]
            {
                ("kettle", 230, 210, 190, -14), ("toaster", 1660, 220, 200, 12), ("tv", 250, 760, 210, 9),
                ("microwave", 1640, 760, 220, -10), ("blender", 470, 470, 150, 6), ("heater", 1460, 480, 160, -6),
            };
            foreach (var s in spots)
            {
                var sprite = Resources.Load<Sprite>(_config.applianceArtFolder + "/" + s.type);
                if (sprite == null) continue;
                var shadow = UiKit.Icon(root, "Deco shadow " + s.type, s.x - s.size * 0.5f + 8f, s.y - s.size * 0.5f + 10f, s.size, s.size, sprite, new Color(0f, 0f, 0f, 0.18f));
                shadow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, s.angle);
                var img = UiKit.Icon(root, "Deco " + s.type, s.x - s.size * 0.5f, s.y - s.size * 0.5f, s.size, s.size, sprite, Color.white);
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, s.angle);
            }
        }

        private void BuildHowTo(RectTransform canvas)
        {
            var ui = _ui;
            var root = Screen(canvas, "HowTo", true);
            UiKit.Text(root, "HeaderShadow", 6, 60 + 7, W, 120, _text.Get("g6_howto_title"), 96, ui.charcoal);
            UiKit.Text(root, "Header", 0, 60, W, 120, _text.Get("g6_howto_title"), 96, ui.cream);

            var size = ui.howToCardSize;
            float total = 4f * size.x + 3f * ui.howToCardGap;
            float x0 = (W - total) * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float x = x0 + i * (size.x + ui.howToCardGap);
                var card = UiKit.CardAt(root, "Card " + (i + 1), x, 220, size.x, size.y, ui.cream, ui);
                var t = card.transform;
                var badge = UIFactory.Panel(t, "Number", 18, 18, 52, 52, ui.accent, false, false);
                badge.sprite = PlaceholderArt.Circle;
                UiKit.Fill(badge.transform, "N", (i + 1).ToString(), 30, ui.charcoal);
                HowToIcon(t, i, size.x);
                UiKit.Text(t, "Title", 16, 220, size.x - 32f, 50, _text.Get("g6_card" + (i + 1) + "_title"), 38, ui.charcoal);
                var body = UiKit.Text(t, "Body", 26, 278, size.x - 52f, size.y - 296f, _text.Get("g6_card" + (i + 1) + "_body"), 25, ui.charcoalSoft,
                    TextAlignmentOptions.Top, false);
                body.textWrappingMode = TextWrappingModes.Normal;
                body.enableAutoSizing = true;
                body.fontSizeMin = 16;
                body.fontSizeMax = 25;
            }

            var keys = UiKit.Card(root, "Keys", new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -770f), new Vector2(1240f, 64f), ui.charcoal, ui);
            keys.sprite = UiArt.Pill;
            UiKit.Fill(keys.transform, "Text", _text.Get("g6_keys"), 28, ui.cream, TextAlignmentOptions.Center, false);

            var nav = root.gameObject.AddComponent<MenuNav>();
            nav.Add(MenuButton(root, "Back", 900f, _text.Get("g6_back"), true, () => _actions.BackToTitle?.Invoke()));
            HowTo = root.gameObject;
            HowTo.SetActive(false);
        }

        private void HowToIcon(Transform card, int index, float w)
        {
            var ui = _ui;
            float cx = w * 0.5f;
            switch (index)
            {
                case 0:
                {
                    var kettle = Resources.Load<Sprite>(_config.applianceArtFolder + "/kettle");
                    if (kettle != null) UiKit.Icon(card, "Kettle", cx - 80f, 40, 160, 160, kettle, Color.white);
                    KeyCap(card, "E", cx + 70f, 140f);
                    break;
                }
                case 1:
                {
                    // A little meter: 5 of 7 lit, the last one red.
                    var back = UIFactory.Panel(card, "Meter", cx - 150f, 95, 300, 60, ui.meterBack, false, false);
                    back.sprite = UiArt.Pill;
                    back.type = Image.Type.Sliced;
                    for (int s = 0; s < 7; s++)
                    {
                        Color c = s < 3 ? _config.loadGreen : s < 5 ? _config.loadAmber : s < 6 ? _config.loadRed : ui.segmentEmpty;
                        var seg = UIFactory.Panel(back.transform, "Seg" + s, 22 + s * 30, 14, 24, 32, c, true, false);
                        seg.pixelsPerUnitMultiplier = 3f;
                    }
                    UiKit.Text(back.transform, "Count", 228, 0, 70, 60, "6/7", 26, ui.cream);
                    break;
                }
                case 2:
                {
                    var panel = Resources.Load<Sprite>(_config.panelArtPath);
                    if (panel != null) UiKit.Icon(card, "Panel", cx - 70f, 40, 140, 160, panel, Color.white);
                    KeyCap(card, "E", cx + 70f, 140f);
                    break;
                }
                default:
                    for (int h = 0; h < 3; h++)
                        UiKit.Icon(card, "Heart" + h, cx - 150f + h * 100f, 80, 96, 96, PlaceholderArt.Heart, h < 2 ? _config.heartColor : _config.heartEmpty);
                    break;
            }
        }

        private void KeyCap(Transform parent, string key, float x, float y)
        {
            var cap = UiKit.CardAt(parent, "Key " + key, x - 32f, y - 32f, 64, 64, _ui.charcoal, _ui);
            UiKit.Fill(cap.transform, "Text", key, 36, _ui.cream);
        }

        private void BuildSelect(RectTransform canvas)
        {
            var ui = _ui;
            var root = Screen(canvas, "LevelSelect", true);
            UiKit.Text(root, "HeaderShadow", 6, 127, W, 110, _text.Get("g6_choose_level"), 80, ui.charcoal);
            UiKit.Text(root, "Header", 0, 120, W, 110, _text.Get("g6_choose_level"), 80, ui.cream);
            _selectCards = UIFactory.Rect(root, "Cards", 0, 0, W, _config.referenceResolution.y);
            _selectNav = root.gameObject.AddComponent<MenuNav>();
            _selectBack = MenuButton(root, "Back", 820f, _text.Get("g6_back"), false, () => _actions.BackToTitle?.Invoke());
            Select = root.gameObject;
            Select.SetActive(false);
        }

        private void BuildEnd(RectTransform canvas)
        {
            var ui = _ui;
            var root = Screen(canvas, "End", true);
            UiKit.Text(root, "Title", 0, 250, W, 140, _text.Get("end_title"), 96, ui.cream);
            _endTime = UiKit.Text(root, "Time", 0, 430, W, 70, "", 48, ui.charcoal);
            UiKit.Text(root, "Credits", 0, 540, W, 50, _text.Get("end_credits"), 32, ui.charcoal, TextAlignmentOptions.Center, false);
            var nav = root.gameObject.AddComponent<MenuNav>();
            nav.Add(MenuButton(root, "Levels", 740f, _text.Get("end_levels"), true, () => _actions.EndLevels?.Invoke()));
            End = root.gameObject;
            End.SetActive(false);
        }

        private void BuildPause(RectTransform canvas)
        {
            var ui = _ui;
            var root = Screen(canvas, "Pause", false);
            var size = ui.pausePanelSize;
            var panel = UiKit.Card(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, ui.cream, ui);
            var t = panel.transform;
            UiKit.Text(t, "Title", 0, 34, size.x, 100, _text.Get("g6_paused"), 84, ui.charcoal);
            var nav = root.gameObject.AddComponent<MenuNav>();
            nav.AllowSpace = false; // Space is the game's own pause/resume key
            float step = ui.menuButtonSize.y + ui.menuButtonGap;
            float y0 = 160f + ui.menuButtonSize.y * 0.5f;
            nav.Add(UiKit.Button(t, "Resume", new Vector2(0.5f, 1f), new Vector2(0f, -y0), ui.menuButtonSize, _text.Get("g6_resume"), 44, ui.buttonPrimary, Color.white, ui, () => _actions.Resume?.Invoke()));
            nav.Add(UiKit.Button(t, "Restart", new Vector2(0.5f, 1f), new Vector2(0f, -(y0 + step)), ui.menuButtonSize, _text.Get("g6_restart"), 44, ui.buttonSecondary, ui.charcoal, ui, () => _actions.Restart?.Invoke()));
            nav.Add(UiKit.Button(t, "MainMenu", new Vector2(0.5f, 1f), new Vector2(0f, -(y0 + 2f * step)), ui.menuButtonSize, _text.Get("g6_main_menu"), 44, ui.buttonSecondary, ui.charcoal, ui, () => _actions.MainMenu?.Invoke()));
            UiKit.Text(t, "Hint", 0, size.y - 52f, size.x, 36, _text.Get("g6_pause_hint"), 22, ui.charcoalSoft, TextAlignmentOptions.Center, false);
            Pause = root.gameObject;
            Pause.SetActive(false);
        }

        private void BuildCard(LevelCard card, float cx, float cy)
        {
            var ui = _ui;
            float a = card.Locked ? 0.45f : 1f;
            var bg = new Color(ui.cream.r, ui.cream.g, ui.cream.b, a);
            var main = new Color(ui.charcoal.r, ui.charcoal.g, ui.charcoal.b, a);
            var second = new Color(ui.charcoalSoft.r, ui.charcoalSoft.g, ui.charcoalSoft.b, a);
            int number = card.Number;
            var button = UiKit.Button(_selectCards, "Level" + number, new Vector2(0f, 1f), new Vector2(cx, -cy), new Vector2(CardWidth, CardHeight), "", 20, bg, main, ui,
                () => _actions.PickLevel?.Invoke(number));
            button.GetComponent<Button>().interactable = !card.Locked;
            var t = button.transform;
            UiKit.Text(t, "Number", 0, 24, CardWidth, 100, number.ToString(), 84, main);
            var title = UiKit.Text(t, "Name", 15, 140, CardWidth - 30, 50, card.Locked ? _text.Get("menu_locked") : card.Title, 32, main);
            title.overflowMode = TextOverflowModes.Ellipsis;
            var teaches = UiKit.Text(t, "Teaches", 20, 195, CardWidth - 40, 70, card.Locked ? "" : card.Teaches, 21, second, TextAlignmentOptions.Top, false);
            teaches.textWrappingMode = TextWrappingModes.Normal;
            string best = card.Locked ? "" : (string.IsNullOrEmpty(card.Best) ? _text.Get("menu_not_won") : _text.Format("menu_best", ("time", card.Best)));
            UiKit.Text(t, "Best", 0, 280, CardWidth, 36, best, 22, second, TextAlignmentOptions.Center, false);
            if (!card.Locked) _selectNav.Add(button);
        }
    }
}
