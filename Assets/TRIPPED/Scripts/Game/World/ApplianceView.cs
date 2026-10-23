using TMPro;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// A plugged-in appliance standing on the floor next to its socket: the real sprite if Art/Appliances/{type}.png
    /// exists, otherwise a cream card with the short label (GDD v1.1 §3.6). Glows yellow when switched on and powered,
    /// blinks red when switched on without power. In a room the player cannot see, it shows what was last seen
    /// (Seen* fields) as a faint ghost with no glow, so hidden state never leaks.
    /// </summary>
    public sealed class ApplianceView : MonoBehaviour
    {
        private ApplianceState _app;
        private TrippedViewConfig _config;
        private SpriteRenderer _glow;
        private SpriteRenderer _outline;
        private SpriteRenderer _card;
        private TextMeshPro _label;
        private bool _hasArt;
        private float _width;
        private ClickTarget _target;

        /// <summary>The sim appliance this view shows.</summary>
        public ApplianceState Appliance => _app;

        /// <summary>Builds the sprite layers and click collider.</summary>
        public void Build(ApplianceState app, ApplianceDef def, TrippedViewConfig config, ArtLibrary art)
        {
            _app = app;
            _config = config;
            float height = def != null && def.worldHeight > 0f ? def.worldHeight : 0.6f;
            var sprite = art.Appliance(app.Type);
            _hasArt = sprite != null;

            if (_hasArt)
            {
                // Imported with pivot at the bottom centre and 1 unit tall (SpriteImportPostprocessor).
                float aspect = sprite.rect.height > 0 ? sprite.rect.width / sprite.rect.height : 1f;
                _width = height * aspect;
                _glow = MakeSprite("Glow", sprite, 0, new Vector3(0f, -height * 0.04f, 0.02f), Vector3.one * height * 1.1f);
                _card = MakeSprite("Sprite", sprite, 2, Vector3.zero, Vector3.one * height);
            }
            else
            {
                _width = Mathf.Clamp(height * 0.85f, 0.45f, 0.9f);
                var rounded = PlaceholderArt.WorldRounded;
                _glow = MakeSliced("Glow", rounded, 0, new Vector2(_width + 0.14f, height + 0.14f), new Vector3(0f, height * 0.5f, 0.02f));
                _outline = MakeSliced("Outline", rounded, 1, new Vector2(_width + 0.04f, height + 0.04f), new Vector3(0f, height * 0.5f, 0.01f));
                _outline.color = config.applianceOutline;
                _card = MakeSliced("Card", rounded, 2, new Vector2(_width, height), new Vector3(0f, height * 0.5f, 0f));
                string label = def != null && !string.IsNullOrEmpty(def.shortLabel) ? def.shortLabel : app.Type.ToUpperInvariant();
                _label = Prims.Text(transform, "Label", new Vector3(0f, height * 0.5f, -0.01f), new Vector2(_width * 0.85f, height * 0.5f),
                    label, config.worldText, 3f);
                _label.sortingOrder = 3;
            }
            _glow.enabled = false;
            _target = ClickTarget.Add(gameObject, ClickKind.WorldAppliance, app.Id, "", 3,
                new Vector3(0f, height * 0.5f, 0f), new Vector3(_width + 0.1f, height + 0.1f, 0.3f));
        }

        /// <summary>
        /// Places and colours the appliance from what the player can see. Hidden when the player last saw it in
        /// the tray (or its socket is gone).
        /// </summary>
        public void Sync(HouseSim sim, bool selected, float time)
        {
            string socketId = _app.SeenSocketId ?? "";
            var socket = socketId.Length > 0 ? sim.FindSocket(socketId) : null;
            var room = socket != null ? sim.FindRoom(socket.RoomId) : null;
            if (room == null)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            _target.RoomId = room.Id;
            float socketX = HouseLayout.SocketX(room.X, room.Width, socket.Pos);
            float x = socketX + _config.socketSize * 0.5f + _width * 0.5f + 0.06f;
            transform.localPosition = new Vector3(x, HouseLayout.FloorY(room.Floor, _config.roomHeight), _config.roomDepth - _config.applianceInset);

            bool ghost = !room.Visible;
            bool on = _app.SeenSwitchedOn;
            bool powered = _app.SeenPowered;
            float alpha = ghost ? _config.ghostAlpha : 1f;

            Color cardColor = _hasArt ? Color.white : (on ? _config.applianceCard : _config.applianceOffCard);
            if (_hasArt && on && !powered && !ghost) cardColor = new Color(0.6f, 0.6f, 0.6f);
            cardColor.a = alpha;
            _card.color = cardColor;
            if (_outline != null)
            {
                var o = _config.applianceOutline;
                o.a = alpha;
                _outline.color = o;
            }
            if (_label != null)
            {
                var t = _config.worldText;
                t.a = alpha;
                _label.color = t;
            }

            // Glow: selection wins, then on+powered, then on without power (blinking 2 Hz).
            Color glow = default;
            bool showGlow = false;
            if (selected)
            {
                glow = _config.selection;
                glow.a = 0.6f + 0.4f * Mathf.Sin(time * 8f);
                showGlow = true;
            }
            else if (!ghost && on && powered)
            {
                glow = _config.glowOn;
                showGlow = true;
            }
            else if (!ghost && on && !powered && Mathf.Repeat(time * 2f, 1f) < 0.5f)
            {
                glow = _config.glowNoPower;
                showGlow = true;
            }
            _glow.enabled = showGlow;
            if (showGlow) _glow.color = glow;
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, int order, Vector3 localPos, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        private SpriteRenderer MakeSliced(string name, Sprite sprite, int order, Vector2 size, Vector3 localPos)
        {
            var sr = MakeSprite(name, sprite, order, localPos, Vector3.one);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            return sr;
        }
    }
}
