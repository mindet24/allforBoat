using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>What the lobby's buttons do. Any action may be null.</summary>
    public sealed class LobbyActions
    {
        /// <summary>Ready: start the day (the host) or tell the host this player is ready.</summary>
        public Action Ready;
        public Action Back;
        /// <summary>Join the room with this code.</summary>
        public Action<string> Join;
        /// <summary>The host renamed the room.</summary>
        public Action<string> Rename;
        /// <summary>The local player picked a character (<see cref="PlayerCharacter"/> ids).</summary>
        public Action<string> PickCharacter;
    }

    /// <summary>
    /// The lobby after Play, in the title art style: the room's name and code (copy button) on a navy bar, "Select
    /// Your Character" with a Boy and a Girl card, a row to join a friend's room by code, and Ready. It only shows
    /// state; <see cref="ScreenFlow"/> and the room service decide what happens.
    /// </summary>
    public sealed class LobbyScreen
    {
        private const float PanelW = 980f, PanelH = 820f;
        private readonly TrippedViewConfig _config;
        private readonly TrippedUiConfig _ui;
        private LobbyActions _actions;
        private TMP_InputField _name;
        private TMP_InputField _joinCode;
        private TextMeshProUGUI _code;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _readyLabel;
        private Image _boyRing, _girlRing, _boyCheck, _girlCheck;
        private RectTransform _boyCard, _girlCard;

        /// <summary>The lobby's root object (hidden until shown).</summary>
        public GameObject Root { get; private set; }

        public LobbyScreen(TrippedViewConfig config, TrippedUiConfig ui)
        {
            _config = config;
            _ui = ui;
        }

        /// <summary>Builds the (hidden) lobby under <paramref name="canvas"/>.</summary>
        public void Build(RectTransform canvas, LobbyActions actions)
        {
            _actions = actions;
            var ui = _ui;
            var root = UIFactory.Stretch(UIFactory.Rect(canvas, "Lobby", 0, 0, 0, 0));
            Root = root.gameObject;
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(ui.dim.r, ui.dim.g, ui.dim.b, 0.35f);

            float W = _config.referenceResolution.x, H = _config.referenceResolution.y;
            var panel = UiKit.Card(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(PanelW, PanelH), ui.cream, ui).transform;

            // Navy bar: house badge, room name (editable), room code (copy).
            var bar = UiKit.CardAt(panel, "Bar", 16f, 16f, PanelW - 32f, 120f, ui.navy, ui, false).transform;
            var badge = UiKit.CardAt(bar, "Badge", 18f, 14f, 92f, 92f, ui.navyLight, ui, false).transform;
            UiKit.Icon(badge, "House", 14f, 14f, 64f, 64f, UiIcons.House, Color.white);
            var nameBox = UiKit.CardAt(bar, "NameBox", 126f, 14f, 480f, 92f, ui.navyLight, ui, false).transform;
            UiKit.Text(nameBox, "Caption", 20f, 8f, 300f, 28f, "Room Name", 22f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Left, false);
            _name = Input(nameBox, "Name", 20f, 38f, 420f, 46f, 34f, Color.white, "My Room", 18);
            _name.onEndEdit.AddListener(v => _actions.Rename?.Invoke(v));
            UiKit.Icon(nameBox, "Pencil", 440f, 46f, 30f, 30f, UiIcons.Brush, Color.white);
            var codeBox = UiKit.CardAt(bar, "CodeBox", 622f, 14f, PanelW - 32f - 640f, 92f, ui.navyLight, ui, false).transform;
            UiKit.Text(codeBox, "Caption", 20f, 8f, 200f, 28f, "Room ID", 22f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Left, false);
            _code = UiKit.Text(codeBox, "Code", 20f, 38f, 200f, 46f, "------", 36f, Color.white, TextAlignmentOptions.Left);
            UiKit.Button(codeBox, "Copy", new Vector2(1f, 0.5f), new Vector2(-62f, 0f), new Vector2(100f, 60f), "Copy", 24f, ui.navy, Color.white, ui,
                () => GUIUtility.systemCopyBuffer = _code.text);

            // "Select Your Character" ribbon and the two cards.
            var ribbon = UiKit.CardAt(panel, "Ribbon", 40f, 158f, 430f, 62f, ui.navy, ui).transform;
            UiKit.Fill(ribbon, "Text", "Select Your Character", 32f, Color.white);
            _boyCard = CharacterCard(panel, "Boy", PlayerCharacter.Boy, "♂", new Color(0.33f, 0.62f, 0.93f), 90f, out _boyRing, out _boyCheck);
            _girlCard = CharacterCard(panel, "Girl", PlayerCharacter.Girl, "♀", new Color(0.96f, 0.6f, 0.72f), PanelW - 90f - 380f, out _girlRing, out _girlCheck);

            // Join a friend: code field + Join.
            var joinRow = UiKit.CardAt(panel, "JoinRow", 90f, 640f, PanelW - 180f, 64f, ui.creamDark, ui, false).transform;
            _joinCode = Input(joinRow, "JoinCode", 20f, 8f, 480f, 48f, 30f, ui.navy, "Enter a room code to join", 12);
            _joinCode.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            _joinCode.onValueChanged.AddListener(v => { var up = v.ToUpperInvariant(); if (up != v) _joinCode.text = up; });
            UiKit.Button(joinRow, "Join", new Vector2(1f, 0.5f), new Vector2(-104f, 0f), new Vector2(180f, 52f), "Join", 30f, ui.navy, Color.white, ui,
                () => _actions.Join?.Invoke(_joinCode.text.Trim()));
            _status = UiKit.Text(panel, "Status", 90f, 708f, PanelW - 180f, 30f, "", 22f, ui.ink, TextAlignmentOptions.Center, false);

            var ready = UiKit.Button(root, "Ready", new Vector2(0.5f, 0.5f), new Vector2(0f, -PanelH * 0.5f - 30f), new Vector2(420f, 104f), "Ready", 52f,
                ui.buttonPrimary, ui.buttonPrimaryText, ui, () => _actions.Ready?.Invoke(), UiIcons.Play, ui.buttonPrimaryText);
            _readyLabel = ready.GetComponentInChildren<TextMeshProUGUI>();
            UiKit.Button(root, "Back", new Vector2(0f, 1f), new Vector2(150f, -70f), new Vector2(220f, 76f), "Back", 32f,
                ui.buttonSecondary, ui.buttonSecondaryText, ui, () => _actions.Back?.Invoke());
            ShowSelected(PlayerCharacter.Selected);
            Root.SetActive(false);
        }

        /// <summary>Shows the room (name, code) and a status line (players, errors; empty = none).</summary>
        public void SetRoom(string name, string code, string status)
        {
            if (_name != null && !_name.isFocused && _name.text != name) _name.SetTextWithoutNotify(name ?? "");
            if (_code != null) _code.text = string.IsNullOrEmpty(code) ? "------" : code;
            if (_status != null) _status.text = status ?? "";
        }

        /// <summary>The Ready button's label (Ready / Start / Waiting…).</summary>
        public void SetReadyLabel(string label)
        {
            if (_readyLabel != null) _readyLabel.text = label;
        }

        /// <summary>Marks <paramref name="id"/>'s card as the local player's pick.</summary>
        public void ShowSelected(string id)
        {
            bool boy = id == PlayerCharacter.Boy;
            _boyRing.enabled = _boyCheck.enabled = boy;
            _girlRing.enabled = _girlCheck.enabled = !boy;
            _boyCard.localScale = Vector3.one * (boy ? 1.04f : 1f);
            _girlCard.localScale = Vector3.one * (boy ? 1f : 1.04f);
        }

        private RectTransform CharacterCard(Transform panel, string label, string id, string glyph, Color color, float x, out Image ring, out Image check)
        {
            var ui = _ui;
            var body = UiKit.CardAt(panel, label + "Card", x, 240f, 380f, 380f, color, ui);
            var root = (RectTransform)body.transform.parent;
            ring = UIFactory.Panel(root, "Ring", -8f, -8f, 396f, 396f, ui.yellow, true, false);
            ring.sprite = UiArt.RoundedOutline;
            ring.type = Image.Type.Sliced;
            ring.transform.SetAsFirstSibling();
            var portrait = Portrait(id);
            if (portrait != null) UiKit.Icon(body.transform, "Portrait", 40f, 20f, 300f, 290f, portrait, Color.white);
            UiKit.Text(body.transform, "Glyph", 14f, 6f, 60f, 60f, glyph, 44f, Color.white);
            var pill = UiKit.CardAt(body.transform, "Name", 70f, 312f, 240f, 56f, ui.navy, ui, false).transform;
            UiKit.Fill(pill, "Text", label, 32f, Color.white);
            check = UIFactory.Panel(root, "Check", 330f, -18f, 64f, 64f, ui.yellow, true, false);
            UiKit.Icon(check.transform, "Mark", 10f, 10f, 44f, 44f, UiIcons.Star, ui.navy);
            var button = body.gameObject.AddComponent<Button>();
            body.raycastTarget = true;
            button.targetGraphic = body;
            button.onClick.AddListener(() =>
            {
                ShowSelected(id);
                _actions.PickCharacter?.Invoke(id);
            });
            return root;
        }

        /// <summary>The first frame of Art/Characters/{id}_idle.png, or null.</summary>
        private Sprite Portrait(string id)
        {
            var tex = Resources.Load<Texture2D>(_config.characterArtFolder + "/" + id + "_idle");
            if (tex == null) return null;
            int s = Mathf.Min(tex.width, tex.height);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0f), 100f);
        }

        /// <summary>A one-line TMP input field at a rect from the parent's top-left.</summary>
        private TMP_InputField Input(Transform parent, string name, float x, float y, float w, float h, float size, Color color, string hint, int limit)
        {
            var rt = UIFactory.Rect(parent, name, x, y, w, h);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.08f);
            var area = UIFactory.Stretch(UIFactory.Rect(rt, "Text Area", 0, 0, 0, 0));
            area.offsetMin = new Vector2(10f, 0f);
            area.offsetMax = new Vector2(-10f, 0f);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = UiKit.Fill(area, "Placeholder", hint, size * 0.8f, new Color(color.r, color.g, color.b, 0.45f), TextAlignmentOptions.Left, false);
            var text = UiKit.Fill(area, "Text", "", size, color, TextAlignmentOptions.Left);
            var field = rt.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = limit;
            field.targetGraphic = bg;
            field.fontAsset = text.font;
            field.pointSize = size;
            return field;
        }
    }
}
