using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        public Action Settings;
        public Action Credits;
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
    /// The uGUI of every menu in the art-reference style (navy panels, yellow accents, white rounded display font,
    /// code-drawn icons): Title, How to Play, Settings, Credits, Pause, and the classic Level Select and End.
    /// Built in code on a Screen Space Overlay canvas scaled from 1920x1080. <see cref="ScreenFlow"/> decides which
    /// one is visible; the Title can sit over the game's own house (see <see cref="TitleBackdrop"/>).
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
        private Sprite _sky;
        private Image _titleSky;
        private SettingsPanel _settings;

        /// <summary>The Title screen object.</summary>
        public GameObject Title { get; private set; }
        /// <summary>The How to Play screen object.</summary>
        public GameObject HowTo { get; private set; }
        /// <summary>The Settings screen object.</summary>
        public GameObject Settings { get; private set; }
        /// <summary>The Credits screen object.</summary>
        public GameObject Credits { get; private set; }
        /// <summary>The Level Select screen object (classic levels).</summary>
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
            UiIcons.Configure(_ui);
            _sky = _ui.SkyGradient();
            BuildTitle(canvas);
            BuildHowTo(canvas);
            BuildSettings(canvas);
            BuildCredits(canvas);
            BuildSelect(canvas);
            BuildEnd(canvas);
            BuildPause(canvas);
        }

        /// <summary>Shows the best day score on the Title (0 = hidden).</summary>
        public void SetBestScore(int score)
        {
            _best.text = score > 0 ? _text.Format("g6_best", ("score", score)) : "";
        }

        /// <summary>true = the Title draws its own sky (no 3D house behind it).</summary>
        public void SetTitleSky(bool on)
        {
            if (_titleSky != null) _titleSky.enabled = on;
        }

        /// <summary>Re-reads the saved settings into the Settings screen.</summary>
        public void RefreshSettings() => _settings?.Refresh();

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
        private float H => _config.referenceResolution.y;

        private RectTransform Screen(RectTransform canvas, string name, bool sky, float dim = 0f)
        {
            var img = UiKit.Backdrop(canvas, name, sky ? Color.white : new Color(_ui.dim.r, _ui.dim.g, _ui.dim.b, dim), sky ? _sky : null);
            return img.rectTransform;
        }

        private ChunkyButton Yellow(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string label, float font, Action onClick, Sprite icon = null)
        {
            return UiKit.Button(parent, name, anchor, pos, size, label, font, _ui.buttonPrimary, _ui.buttonPrimaryText, _ui, onClick, icon, _ui.buttonPrimaryText);
        }

        private ChunkyButton Navy(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string label, float font, Action onClick, Sprite icon = null)
        {
            return UiKit.Button(parent, name, anchor, pos, size, label, font, _ui.buttonSecondary, _ui.buttonSecondaryText, _ui, onClick, icon, Color.white);
        }

        /// <summary>A big display title with a navy outline and drop shadow (white on the sky, like the logo).</summary>
        private TextMeshProUGUI Headline(Transform parent, string name, float y, string text, float size)
        {
            var t = UiKit.Text(parent, name, 0, y, W, size * 1.2f, text, size, Color.white).Outlined(_ui.navy, _ui.logoOutline * 0.8f);
            return t;
        }

        // ---- Title -----------------------------------------------------------------------------------------

        private void BuildTitle(RectTransform canvas)
        {
            var ui = _ui;
            var root = UIFactory.Stretch(UIFactory.Rect(canvas, "Title", 0, 0, 0, 0));
            _titleSky = root.gameObject.AddComponent<Image>();
            _titleSky.sprite = _sky;
            _titleSky.raycastTarget = true;

            // Logo: white brushed TRIPPED! with navy outline, tilted, a lightning bolt and sparks, a plug-and-cord swoosh.
            var logo = UIFactory.Rect(root, "Logo", ui.logoPosition.x, ui.logoPosition.y, 900f, 360f);
            logo.localRotation = Quaternion.Euler(0f, 0f, ui.logoTilt);
            UiKit.Text(logo, "Shadow", 10, 14, 860, 240, _text.Get("g6_title"), ui.logoSize, ui.navy, TextAlignmentOptions.Center);
            var name = UiKit.Text(logo, "Name", 0, 0, 860, 240, _text.Get("g6_title"), ui.logoSize, Color.white, TextAlignmentOptions.Center)
                .Outlined(ui.navy, ui.logoOutline);
            name.characterSpacing = -2f;
            UiKit.Icon(logo, "Bolt", 770, 30, 120, 150, UiIcons.Lightning, Color.white).rectTransform.localRotation = Quaternion.Euler(0, 0, -12f);
            for (int i = 0; i < 3; i++)
            {
                var spark = UIFactory.Panel(logo, "Spark" + i, 790 + i * 36, -18 + Mathf.Abs(i - 1) * 16, 12, 44, ui.yellow, true, false);
                spark.rectTransform.localRotation = Quaternion.Euler(0, 0, 30f - i * 30f);
            }
            for (int i = 0; i < 2; i++)
            {
                var spark = UIFactory.Panel(logo, "SparkL" + i, 30 + i * 40, 10 + i * 6, 12, 40, ui.yellow, true, false);
                spark.rectTransform.localRotation = Quaternion.Euler(0, 0, 40f - i * 30f);
            }
            // Plug and cord swoosh under the word.
            var cord = UiKit.Icon(logo, "Cord", 70, 210, 640, 70, UiIcons.Cord, Color.white);
            cord.preserveAspect = false;
            UiKit.Icon(logo, "Plug", 690, 205, 72, 72, UiIcons.Plug, Color.white).rectTransform.localRotation = Quaternion.Euler(0, 0, -90f);
            // Tagline on a yellow brush stripe.
            var stripe = UiKit.Icon(logo, "Stripe", 150, 262, 640, 92, UiIcons.Brush, ui.yellow);
            stripe.preserveAspect = false;
            UiKit.Text(stripe.transform, "Tagline", 0, 6, 640, 80, _text.Get("g7_tagline"), 40, ui.navy, TextAlignmentOptions.Center);

            // Menu column on the left, slightly tilted buttons like the reference.
            var nav = root.gameObject.AddComponent<MenuNav>();
            float x = ui.titleMenuLeft;
            float y = ui.titleMenuTop;
            var play = Yellow(root, "Play", new Vector2(0f, 1f), new Vector2(x + ui.playButtonSize.x * 0.5f, -(y + ui.playButtonSize.y * 0.5f)), ui.playButtonSize,
                _text.Get("g6_play"), 66, () => _actions.Play?.Invoke(), UiIcons.Play);
            play.Root.localRotation = Quaternion.Euler(0, 0, ui.titleButtonTilt);
            nav.Add(play);
            y += ui.playButtonSize.y + ui.titleButtonGap + 4f;
            var items = new (string key, Sprite icon, Action act)[]
            {
                ("g6_howto", UiIcons.Lightning, () => _actions.HowTo?.Invoke()),
                ("g7_settings", UiIcons.Gear, () => _actions.Settings?.Invoke()),
                ("g7_credits", UiIcons.Book, () => _actions.Credits?.Invoke()),
                ("g6_quit", UiIcons.Power, () => _actions.Quit?.Invoke()),
            };
            foreach (var item in items)
            {
                if (item.key == "g6_quit" && Application.platform == RuntimePlatform.WebGLPlayer) continue;
                var b = Navy(root, item.key, new Vector2(0f, 1f), new Vector2(x + 20f + ui.titleButtonSize.x * 0.5f, -(y + ui.titleButtonSize.y * 0.5f)), ui.titleButtonSize,
                    _text.Get(item.key), 44, item.act, item.icon);
                b.Root.localRotation = Quaternion.Euler(0, 0, ui.titleButtonTilt * 0.6f);
                nav.Add(b);
                y += ui.titleButtonSize.y + ui.titleButtonGap;
            }
            // Best score: a small yellow tag beside the Play button.
            _best = UiKit.Text(root, "Best", x + ui.playButtonSize.x + 30f, ui.titleMenuTop + 30f, 300, 56, "", 34, ui.yellow, TextAlignmentOptions.MidlineLeft).Outlined(ui.navy, 0.25f);

            // Handwritten notes.
            Note(root, "NoteTop", new Vector2(1f, 1f), new Vector2(-300f, -105f), _text.Get("g7_note_top"), -8f, UiIcons.House);
            Note(root, "NoteBottom", new Vector2(0f, 0f), new Vector2(820f, 120f), _text.Get("g7_note_bottom"), -8f, UiIcons.Lightning);

            UiKit.Text(root, "Keys", 0, H - 46, W, 36, _text.Get("g6_menu_keys"), 22, Color.white, TextAlignmentOptions.Center, false).AsHand();
            Title = root.gameObject;
            Title.SetActive(false);
        }

        private void Note(RectTransform root, string name, Vector2 anchor, Vector2 pos, string text, float tilt, Sprite icon)
        {
            var rt = UIFactory.Anchored(root, name, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(380f, 170f));
            rt.localRotation = Quaternion.Euler(0, 0, tilt);
            var t = UIFactory.LabelOn(UIFactory.Stretch(UIFactory.Rect(rt, "Text", 0, 0, 0, 0)), text, _ui.noteSize, Color.white, TextAlignmentOptions.Center).AsHand();
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.Outlined(_ui.navy, 0.18f);
            if (icon != null)
            {
                var img = UiKit.Icon(rt, "Icon", 330, 30, 80, 80, icon, Color.white);
                img.rectTransform.localRotation = Quaternion.Euler(0, 0, 8f);
            }
        }

        // ---- How to Play -----------------------------------------------------------------------------------

        private void BuildHowTo(RectTransform canvas)
        {
            var ui = _ui;
            var root = Screen(canvas, "HowTo", false, 0.55f);
            Headline(root, "Header", 50, _text.Get("g6_howto_title"), 96);

            var size = ui.howToCardSize;
            float total = 4f * size.x + 3f * ui.howToCardGap;
            float x0 = (W - total) * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float x = x0 + i * (size.x + ui.howToCardGap);
                var card = UiKit.CardAt(root, "Card " + (i + 1), x, 220, size.x, size.y, ui.cream, ui);
                var t = card.transform;
                var badge = UIFactory.Panel(t, "Number", 18, 18, 56, 56, ui.navy, false, false);
                badge.sprite = PlaceholderArt.Circle;
                UiKit.Fill(badge.transform, "N", (i + 1).ToString(), 32, ui.yellow);
                HowToIcon(t, i, size.x);
                UiKit.Text(t, "Title", 16, 220, size.x - 32f, 50, _text.Get("g6_card" + (i + 1) + "_title"), 40, ui.navy);
                var body = UiKit.Text(t, "Body", 26, 280, size.x - 52f, size.y - 296f, _text.Get("g6_card" + (i + 1) + "_body"), 25, ui.charcoalSoft,
                    TextAlignmentOptions.Top, false);
                body.textWrappingMode = TextWrappingModes.Normal;
                body.enableAutoSizing = true;
                body.fontSizeMin = 16;
                body.fontSizeMax = 25;
            }

            var keys = UiKit.Card(root, "Keys", new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -770f), new Vector2(1240f, 64f), ui.navy, ui);
            keys.sprite = UiArt.Pill;
            UiKit.Fill(keys.transform, "Text", _text.Get("g6_keys"), 28, Color.white, TextAlignmentOptions.Center, false);

            var nav = root.gameObject.AddComponent<MenuNav>();
            nav.Add(Yellow(root, "Back", new Vector2(0.5f, 1f), new Vector2(0f, -900f), ui.menuButtonSize, _text.Get("g6_back"), 44, () => _actions.BackToTitle?.Invoke()));
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
                    var back = UIFactory.Panel(card, "Meter", cx - 160f, 95, 320, 64, ui.navy, false, false);
                    back.sprite = UiArt.Pill;
                    back.type = Image.Type.Sliced;
                    UiKit.Icon(back.transform, "Bolt", 10, 8, 48, 48, UiIcons.Lightning, Color.white);
                    for (int s = 0; s < 7; s++)
                    {
                        Color c = s < 3 ? ui.green : s < 5 ? ui.yellow : s < 6 ? ui.red : ui.segmentEmpty;
                        var seg = UIFactory.Panel(back.transform, "Seg" + s, 62 + s * 26, 16, 20, 32, c, true, false);
                        seg.pixelsPerUnitMultiplier = 3f;
                    }
                    UiKit.Text(back.transform, "Count", 246, 0, 70, 64, "6/7", 28, Color.white);
                    break;
                }
                case 2:
                {
                    var panel = Resources.Load<Sprite>(_config.panelArtPath);
                    if (panel != null) UiKit.Icon(card, "Panel", cx - 70f, 40, 140, 160, panel, Color.white);
                    UiKit.Icon(card, "Warn", cx - 140f, 40, 70, 70, UiIcons.Warning, Color.white).color = Color.white;
                    KeyCap(card, "E", cx + 70f, 140f);
                    break;
                }
                default:
                    for (int h = 0; h < 3; h++)
                        UiKit.Icon(card, "Heart" + h, cx - 150f + h * 100f, 80, 96, 96, UiIcons.Heart, h < 2 ? Color.white : new Color(0.4f, 0.4f, 0.45f, 0.6f));
                    break;
            }
        }

        private void KeyCap(Transform parent, string key, float x, float y)
        {
            var cap = UiKit.CardAt(parent, "Key " + key, x - 32f, y - 32f, 64, 64, _ui.navy, _ui);
            UiKit.Fill(cap.transform, "Text", key, 38, _ui.yellow);
        }

        // ---- Settings --------------------------------------------------------------------------------------

        private void BuildSettings(RectTransform canvas)
        {
            var root = Screen(canvas, "Settings", false, 0.6f);
            _settings = root.gameObject.AddComponent<SettingsPanel>();
            _settings.Build(root, _ui, _text, _config, () => _actions.BackToTitle?.Invoke(), () => _actions.Classic?.Invoke());
            Settings = root.gameObject;
            Settings.SetActive(false);
        }

        // ---- Credits ---------------------------------------------------------------------------------------

        private void BuildCredits(RectTransform canvas)
        {
            var ui = _ui;
            var root = Screen(canvas, "Credits", false, 0.6f);
            var size = ui.creditsPanelSize;
            var panel = UiKit.Card(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), size, ui.panel, ui);
            var t = panel.transform;
            UiKit.Icon(t, "Book", size.x * 0.5f - 245f, 36, 80, 80, UiIcons.Book, ui.yellow);
            UiKit.Text(t, "Title", 0, 26, size.x, 100, _text.Get("g7_credits"), 84, Color.white);

            float y = 150f;
            UiKit.Text(t, "Team", 60, y, size.x - 120f, 50, _text.Get("g7_team"), 40, ui.yellow, TextAlignmentOptions.MidlineLeft);
            y += 56f;
            foreach (var (role, who) in new[] { ("g7_role_code", "g7_name_code"), ("g7_role_art", "g7_name_art"), ("g7_role_test", "g7_name_test") })
            {
                UiKit.Text(t, role, 80, y, 460, 44, _text.Get(role), 30, ui.creamDark, TextAlignmentOptions.MidlineLeft, false);
                UiKit.Text(t, who, 560, y, size.x - 620f, 44, _text.Get(who), 34, Color.white, TextAlignmentOptions.MidlineLeft);
                y += 48f;
            }
            y += 24f;
            UiKit.Text(t, "ThirdHeader", 60, y, size.x - 120f, 50, _text.Get("g7_third_party"), 40, ui.yellow, TextAlignmentOptions.MidlineLeft);
            y += 56f;
            var file = Resources.Load<TextAsset>(ui.thirdPartyCreditsPath);
            var lines = CreditsRules.Parse(file != null ? file.text : "");
            var list = UiKit.Text(t, "ThirdParty", 80, y, size.x - 160f, size.y - y - 190f, string.Join("\n", lines), 26, ui.creamDark, TextAlignmentOptions.TopLeft, false);
            list.textWrappingMode = TextWrappingModes.Normal;
            list.enableAutoSizing = true;
            list.fontSizeMin = 14;
            list.fontSizeMax = 26;

            var made = UiKit.Card(t, "MadeWith", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170f, 50f), new Vector2(360f, 70f), ui.navyLight, ui, false);
            made.sprite = UiArt.Pill;
            UiKit.Fill(made.transform, "Text", _text.Get("g7_made_with"), 32, Color.white);
            var nav = root.gameObject.AddComponent<MenuNav>();
            nav.Add(Yellow(t, "Back", new Vector2(0.5f, 0f), new Vector2(230f, 85f), new Vector2(320f, 84f), _text.Get("g6_back"), 38, () => _actions.BackToTitle?.Invoke()));
            Credits = root.gameObject;
            Credits.SetActive(false);
        }

        // ---- classic Level Select / End --------------------------------------------------------------------

        private void BuildSelect(RectTransform canvas)
        {
            var root = Screen(canvas, "LevelSelect", true);
            Headline(root, "Header", 110, _text.Get("g6_choose_level"), 84);
            _selectCards = UIFactory.Rect(root, "Cards", 0, 0, W, _config.referenceResolution.y);
            _selectNav = root.gameObject.AddComponent<MenuNav>();
            _selectBack = Navy(root, "Back", new Vector2(0.5f, 1f), new Vector2(0f, -820f), _ui.menuButtonSize, _text.Get("g6_back"), 40, () => _actions.BackToTitle?.Invoke());
            Select = root.gameObject;
            Select.SetActive(false);
        }

        private void BuildEnd(RectTransform canvas)
        {
            var root = Screen(canvas, "End", true);
            Headline(root, "Title", 250, _text.Get("end_title"), 96);
            _endTime = UiKit.Text(root, "Time", 0, 430, W, 70, "", 48, _ui.navy);
            UiKit.Text(root, "Credits", 0, 540, W, 50, _text.Get("end_credits"), 32, _ui.navy, TextAlignmentOptions.Center, false);
            var nav = root.gameObject.AddComponent<MenuNav>();
            nav.Add(Yellow(root, "Levels", new Vector2(0.5f, 1f), new Vector2(0f, -740f), _ui.menuButtonSize, _text.Get("end_levels"), 44, () => _actions.EndLevels?.Invoke()));
            End = root.gameObject;
            End.SetActive(false);
        }

        private void BuildCard(LevelCard card, float cx, float cy)
        {
            var ui = _ui;
            float a = card.Locked ? 0.5f : 1f;
            var bg = new Color(ui.panel.r, ui.panel.g, ui.panel.b, a);
            var main = new Color(1f, 1f, 1f, a);
            var second = new Color(ui.creamDark.r, ui.creamDark.g, ui.creamDark.b, a);
            int number = card.Number;
            var button = UiKit.Button(_selectCards, "Level" + number, new Vector2(0f, 1f), new Vector2(cx, -cy), new Vector2(CardWidth, CardHeight), "", 20, bg, main, ui,
                () => _actions.PickLevel?.Invoke(number));
            button.GetComponent<Button>().interactable = !card.Locked;
            var t = button.transform;
            UiKit.Text(t, "Number", 0, 24, CardWidth, 100, number.ToString(), 88, card.Locked ? second : ui.yellow);
            var title = UiKit.Text(t, "Name", 15, 140, CardWidth - 30, 50, card.Locked ? _text.Get("menu_locked") : card.Title, 32, main);
            title.overflowMode = TextOverflowModes.Ellipsis;
            var teaches = UiKit.Text(t, "Teaches", 20, 195, CardWidth - 40, 70, card.Locked ? "" : card.Teaches, 21, second, TextAlignmentOptions.Top, false);
            teaches.textWrappingMode = TextWrappingModes.Normal;
            string best = card.Locked ? "" : (string.IsNullOrEmpty(card.Best) ? _text.Get("menu_not_won") : _text.Format("menu_best", ("time", card.Best)));
            UiKit.Text(t, "Best", 0, 280, CardWidth, 36, best, 22, second, TextAlignmentOptions.Center, false);
            if (!card.Locked) _selectNav.Add(button);
        }

        // ---- Pause -----------------------------------------------------------------------------------------

        private void BuildPause(RectTransform canvas)
        {
            var ui = _ui;
            var root = Screen(canvas, "Pause", false, ui.dim.a);
            var size = ui.pausePanelSize;
            var panel = UiKit.Card(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, ui.panel, ui);
            var t = panel.transform;
            UiKit.Text(t, "Title", 0, 30, size.x, 100, _text.Get("g6_paused"), 84, Color.white).Outlined(ui.navy, 0.2f);
            var nav = root.gameObject.AddComponent<MenuNav>();
            nav.AllowSpace = false; // Space is the game's own pause/resume key
            float step = ui.menuButtonSize.y + ui.menuButtonGap;
            float y0 = 160f + ui.menuButtonSize.y * 0.5f;
            nav.Add(Yellow(t, "Resume", new Vector2(0.5f, 1f), new Vector2(0f, -y0), ui.menuButtonSize, _text.Get("g6_resume"), 46, () => _actions.Resume?.Invoke(), UiIcons.Play));
            nav.Add(UiKit.Button(t, "Restart", new Vector2(0.5f, 1f), new Vector2(0f, -(y0 + step)), ui.menuButtonSize, _text.Get("g6_restart"), 44, ui.navyLight, Color.white, ui, () => _actions.Restart?.Invoke(), UiIcons.Hourglass));
            nav.Add(UiKit.Button(t, "MainMenu", new Vector2(0.5f, 1f), new Vector2(0f, -(y0 + 2f * step)), ui.menuButtonSize, _text.Get("g6_main_menu"), 44, ui.navyLight, Color.white, ui, () => _actions.MainMenu?.Invoke(), UiIcons.House));
            UiKit.Text(t, "Hint", 0, size.y - 52f, size.x, 36, _text.Get("g6_pause_hint"), 24, ui.creamDark, TextAlignmentOptions.Center, false);
            Pause = root.gameObject;
            Pause.SetActive(false);
        }
    }

    /// <summary>
    /// The Settings screen: five volume sliders, fullscreen, resolution, camera shake, a Classic levels shortcut and
    /// Back. Keyboard: Up/Down (W/S) pick a row, Left/Right (A/D) change it, Enter toggles or presses, Esc goes back
    /// (through the screen flow). Every change is saved at once in <see cref="UserSettings.Current"/>.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private sealed class Row
        {
            public Image Ring;
            public Action<int> Change;
            public Action Submit;
            public Action Refresh;
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly List<Vector2Int> _sizes = new List<Vector2Int>();
        private int _focus;
        private int _shownFrame;
        private TrippedUiConfig _ui;
        private HudText _text;

        /// <summary>Focused row index (for checks).</summary>
        public int Focus => _focus;

        public void Build(RectTransform root, TrippedUiConfig ui, HudText text, TrippedViewConfig config, Action back, Action classic)
        {
            _ui = ui;
            _text = text;
            var size = ui.settingsPanelSize;
            var panel = UiKit.Card(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, ui.panel, ui);
            var t = panel.transform;
            UiKit.Icon(t, "Gear", size.x * 0.5f - 250f, 32, 76, 76, UiIcons.Gear, ui.yellow);
            UiKit.Text(t, "Title", 0, 24, size.x, 100, text.Get("g7_settings"), 84, Color.white);

            float y = 140f;
            foreach (var (channel, key) in new[]
                     {
                         (VolumeChannel.Master, "g7_master"), (VolumeChannel.Music, "g7_music"), (VolumeChannel.Sfx, "g7_sfx"),
                         (VolumeChannel.Ui, "g7_ui"), (VolumeChannel.Voice, "g7_voice"),
                     })
            {
                VolumeRow(t, y, channel, text.Get(key));
                y += ui.settingsRowHeight;
            }
            ToggleRow(t, y, text.Get("g7_fullscreen"), () => UserSettings.Current.Fullscreen, v =>
            {
                UserSettings.Current.Fullscreen = v;
                UserSettings.Current.ApplyAll();
            });
            y += ui.settingsRowHeight;
            ResolutionRow(t, y);
            y += ui.settingsRowHeight;
            ToggleRow(t, y, text.Get("g7_shake"), () => UserSettings.Current.CameraShake, v => UserSettings.Current.CameraShake = v);
            y += ui.settingsRowHeight + 14f;

            ButtonRow(t, y, text.Get("g6_classic"), ui.navyLight, Color.white, -200f, classic);
            ButtonRow(t, y, text.Get("g6_back"), ui.buttonPrimary, ui.buttonPrimaryText, 200f, back);
            UiKit.Text(t, "Keys", 0, size.y - 46f, size.x, 34, text.Get("g7_settings_keys"), 22, ui.creamDark, TextAlignmentOptions.Center, false);
            Refresh();
        }

        /// <summary>Shows the saved values.</summary>
        public void Refresh()
        {
            foreach (var r in _rows) r.Refresh?.Invoke();
            SetFocus(_focus);
        }

        private RectTransform RowRoot(Transform panel, float y, string label)
        {
            var rt = UIFactory.Rect(panel, "Row " + label, 60, y, _ui.settingsPanelSize.x - 120f, _ui.settingsRowHeight - 10f);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UiArt.Pill;
            bg.type = Image.Type.Sliced;
            bg.color = _ui.navyLight;
            UiKit.Text(rt, "Label", 30, 0, 360, _ui.settingsRowHeight - 10f, label, 32, Color.white, TextAlignmentOptions.MidlineLeft);
            return rt;
        }

        private Image Ring(RectTransform row)
        {
            var ringRt = UIFactory.Stretch(UIFactory.Rect(row, "Focus", 0, 0, 0, 0));
            ringRt.offsetMin = new Vector2(-6f, -6f);
            ringRt.offsetMax = new Vector2(6f, 6f);
            var ring = ringRt.gameObject.AddComponent<Image>();
            ring.sprite = UiArt.RoundedOutline;
            ring.type = Image.Type.Sliced;
            ring.color = _ui.yellow;
            ring.raycastTarget = false;
            ring.enabled = false;
            return ring;
        }

        private void VolumeRow(Transform panel, float y, VolumeChannel channel, string label)
        {
            var row = RowRoot(panel, y, label);
            float w = 320f, h = 18f;
            var track = UIFactory.Panel(row, "Track", 400, (_ui.settingsRowHeight - 10f - h) * 0.5f, w, h, _ui.navy, false, true);
            track.sprite = UiArt.Pill;
            track.type = Image.Type.Sliced;
            var fill = UIFactory.Fill(UIFactory.Stretch(UIFactory.Rect(track.transform, "Fill", 0, 0, 0, 0)), _ui.yellow, false, false);
            fill.sprite = UiArt.Pill;
            fill.type = Image.Type.Sliced;
            var handleArea = UIFactory.Stretch(UIFactory.Rect(track.transform, "HandleArea", 0, 0, 0, 0));
            var handle = UIFactory.Anchored(handleArea, "Handle", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            var himg = handle.gameObject.AddComponent<Image>();
            himg.sprite = PlaceholderArt.Circle;
            himg.color = Color.white;
            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            slider.targetGraphic = himg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.wholeNumbers = true;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            var value = UiKit.Text(row, "Value", 730, 0, 120, _ui.settingsRowHeight - 10f, "", 32, _ui.yellow, TextAlignmentOptions.MidlineRight);
            bool refreshing = false;
            slider.onValueChanged.AddListener(v =>
            {
                value.text = Mathf.RoundToInt(v) + "%";
                if (!refreshing) UserSettings.Current.SetVolume(channel, Mathf.RoundToInt(v));
            });
            var r = new Row
            {
                Ring = Ring(row),
                Change = d => slider.value = SettingsRules.StepVolume(UserSettings.Current.Volume(channel), d, _ui.volumeStep),
                Refresh = () =>
                {
                    refreshing = true;
                    slider.value = UserSettings.Current.Volume(channel);
                    value.text = UserSettings.Current.Volume(channel) + "%";
                    refreshing = false;
                },
            };
            Hover(row, r);
            _rows.Add(r);
        }

        private void ToggleRow(Transform panel, float y, string label, Func<bool> get, Action<bool> set)
        {
            var row = RowRoot(panel, y, label);
            var pill = UIFactory.Panel(row, "Switch", 670, 8, 180, _ui.settingsRowHeight - 26f, _ui.green, false, true);
            pill.sprite = UiArt.Pill;
            pill.type = Image.Type.Sliced;
            var txt = UiKit.Fill(pill.transform, "Text", "", 30, Color.white);
            var button = pill.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var r = new Row { Ring = Ring(row) };
            r.Refresh = () =>
            {
                bool on = get();
                pill.color = on ? _ui.green : _ui.segmentEmpty;
                txt.text = _text.Get(on ? "g7_on" : "g7_off");
            };
            r.Submit = () => { set(!get()); r.Refresh(); };
            r.Change = d => { set(d > 0); r.Refresh(); };
            button.onClick.AddListener(() => r.Submit());
            Hover(row, r);
            _rows.Add(r);
        }

        private void ResolutionRow(Transform panel, float y)
        {
            var row = RowRoot(panel, y, _text.Get("g7_resolution"));
            _sizes.Clear();
            var all = new List<Vector2Int>();
            foreach (var res in UnityEngine.Screen.resolutions) all.Add(new Vector2Int(res.width, res.height));
            all.Add(new Vector2Int(1280, 720));
            all.Add(new Vector2Int(1920, 1080));
            _sizes.AddRange(SettingsRules.DistinctSizes(all));
            var value = UiKit.Text(row, "Value", 520, 0, 290, _ui.settingsRowHeight - 10f, "", 32, _ui.yellow, TextAlignmentOptions.Center);
            var left = UiKit.Icon(row, "Left", 480, 14, 32, 32, UiIcons.Play, Color.white);
            // Turn around the centre (icons are placed by their top-left corner).
            left.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            left.rectTransform.anchoredPosition += new Vector2(16f, -16f);
            left.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);
            UiKit.Icon(row, "Right", 818, 14, 32, 32, UiIcons.Play, Color.white);
            int index = 0;
            var r = new Row { Ring = Ring(row) };
            r.Refresh = () =>
            {
                var saved = UserSettings.Current.Resolution;
                var want = saved.x > 0 ? saved : new Vector2Int(UnityEngine.Screen.width, UnityEngine.Screen.height);
                index = Mathf.Max(0, SettingsRules.ClosestSize(_sizes, want));
                value.text = _sizes.Count > 0 ? _sizes[index].x + " x " + _sizes[index].y : "-";
            };
            r.Change = d =>
            {
                index = SettingsRules.StepIndex(index, d, _sizes.Count);
                if (index < 0) return;
                UserSettings.Current.Resolution = _sizes[index];
                UserSettings.Current.ApplyAll();
                value.text = _sizes[index].x + " x " + _sizes[index].y;
            };
            Hover(row, r);
            _rows.Add(r);
        }

        private void ButtonRow(Transform panel, float y, string label, Color bg, Color fg, float x, Action act)
        {
            var b = UiKit.Button(panel, "Button " + label, new Vector2(0.5f, 1f), new Vector2(x, -(y + 40f)), new Vector2(340f, 80f), label, 36, bg, fg, _ui, act);
            var r = new Row { Ring = null, Submit = b.Invoke, Change = null };
            r.Refresh = () => b.SetFocused(_rows.IndexOf(r) == _focus);
            b.Hovered += _ => SetFocus(_rows.IndexOf(r));
            _rows.Add(r);
        }

        private void Hover(RectTransform row, Row r)
        {
            var trigger = row.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => SetFocus(_rows.IndexOf(r)));
            trigger.triggers.Add(entry);
        }

        private void SetFocus(int index)
        {
            _focus = Mathf.Clamp(index, 0, Mathf.Max(0, _rows.Count - 1));
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Ring != null) _rows[i].Ring.enabled = i == _focus;
                else _rows[i].Refresh?.Invoke();
            }
        }

        private void OnEnable()
        {
            _shownFrame = Time.frameCount;
            if (_rows.Count > 0) Refresh();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _rows.Count == 0 || Time.frameCount == _shownFrame) return;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) SetFocus(MenuNavRules.Step(_focus, -1, _rows.Count));
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame) SetFocus(MenuNavRules.Step(_focus, 1, _rows.Count));
            var row = _rows[_focus];
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) row.Change?.Invoke(-1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) row.Change?.Invoke(1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) row.Submit?.Invoke();
        }
    }
}
