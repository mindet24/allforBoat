using TMPro;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// A socket block on a room's back wall. It shows its circuit letter and colour only once revealed; before that
    /// it is grey with "?", and when the labels name an assumed circuit (level 4) a dashed outline in that colour.
    /// Reserved sockets (a routine holds them) show a padlock. Revealing flips the face (scale 1 → 0 → 1).
    /// A dark patch covers the socket area while its breaker is OFF, but only if that already shows (revealed, or an
    /// appliance switched on there), so it never gives the wiring away. Rules for what may be shown: <see cref="SocketLook"/>.
    /// </summary>
    public sealed class SocketView : MonoBehaviour
    {
        private const int FaceOrder = 21;

        private SocketState _socket;
        private TrippedViewConfig _config;
        private SpriteRenderer _face;
        private SpriteRenderer _dashed;
        private SpriteRenderer _padlock;
        private SpriteRenderer _powerOut;
        private TextMeshPro _label;
        private Transform _faceRoot;
        private string _shownText;
        private bool _shownRevealed;
        private float _flipStart = -1f;
        private bool _highlighted;
        private float _size;
        private bool _day;

        /// <summary>Sim id of the socket.</summary>
        public string SocketId => _socket.Id;
        /// <summary>The sim socket, for click handling.</summary>
        public SocketState Socket => _socket;
        /// <summary>Frame the view was last matched to a sim socket (to drop views of removed cord sockets).</summary>
        public int SeenFrame { get; set; }
        /// <summary>World position of the socket's face centre on the wall (x, y).</summary>
        public Vector2 WallPoint { get; private set; }

        /// <summary>Builds the block under this object and places it on the wall.</summary>
        /// <param name="kitPlate">Day mode with the 3D house kit: draw the kit's Socket_Plate on the wall instead of the greybox block.</param>
        public void Build(SocketState socket, HouseSim sim, TrippedViewConfig config, bool kitPlate = false)
        {
            _socket = socket;
            _config = config;
            _size = config.socketSize;
            float s = _size;
            float d = config.socketDepth;
            _shownRevealed = socket.Revealed;
            _day = sim.IsDayMode;

            // Area darkened while the breaker is OFF (behind the socket, over the room contents).
            var patch = new GameObject("PowerOut");
            patch.transform.SetParent(transform, false);
            patch.transform.localPosition = new Vector3(0f, -config.socketHeight * 0.25f, -0.02f);
            _powerOut = patch.AddComponent<SpriteRenderer>();
            _powerOut.sprite = PlaceholderArt.SoftRect;
            _powerOut.drawMode = SpriteDrawMode.Sliced;
            _powerOut.size = new Vector2(1.5f, 1.4f);
            _powerOut.color = config.powerOutOverlay;
            _powerOut.sortingOrder = 8;
            _powerOut.enabled = false;

            var plate = kitPlate ? HouseKitView.SocketPlate(config, transform) : null;
            if (plate == null) Prims.Box(transform, "Block", new Vector3(0f, 0f, -d * 0.5f), new Vector3(s, s, d), config.socketBody);

            _faceRoot = new GameObject("FaceRoot").transform;
            _faceRoot.SetParent(transform, false);
            _faceRoot.localPosition = new Vector3(0f, 0f, -d - 0.005f);
            // The face is an unlit sprite so the circuit colour shows exactly as defined (it is data, not decoration).
            _face = MakeSprite("Face", PlaceholderArt.WorldRounded, FaceOrder, _faceRoot);
            _face.drawMode = SpriteDrawMode.Sliced;
            _face.size = new Vector2(s * 0.92f, s * 0.92f);
            if (plate != null) _face.enabled = false;
            _dashed = MakeSprite("Assumed", PlaceholderArt.DashedOutline, FaceOrder + 1, _faceRoot);
            _dashed.transform.localScale = Vector3.one * (s * 1.25f);
            _dashed.enabled = false;
            _padlock = MakeSprite("Padlock", PlaceholderArt.Padlock, FaceOrder + 2, _faceRoot);
            _padlock.transform.localScale = Vector3.one * (s * 0.55f);
            _padlock.transform.localPosition = new Vector3(s * 0.5f, s * 0.5f, -0.01f);
            _padlock.color = config.roomNameText;
            _padlock.enabled = socket.Reserved;
            _label = Prims.Text(_faceRoot, "Circuit", new Vector3(0f, 0f, -0.005f), new Vector2(s * 0.9f, s * 0.8f), "?", config.worldText, 3f);
            _label.sortingOrder = FaceOrder + 3;
            if (socket.IsCord)
            {
                var cordText = Prims.Text(transform, "Cord", new Vector3(0f, -s * 0.85f, -d - 0.01f), new Vector2(s * 2f, s * 0.4f), "CORD", config.roomNameText, 1.5f);
                cordText.sortingOrder = FaceOrder + 3;
            }
            ClickTarget.Add(gameObject, ClickKind.Socket, socket.Id, socket.RoomId, 2, new Vector3(0f, 0f, -d * 0.5f), new Vector3(s * 1.6f, s * 1.6f, d + 0.1f));
            Place(sim);
        }

        /// <summary>Highlights the socket while an appliance (or the cord) is held and the pointer is over it.</summary>
        public void SetHighlight(bool on)
        {
            if (_highlighted == on) return;
            _highlighted = on;
            transform.localScale = on ? Vector3.one * 1.35f : Vector3.one;
        }

        /// <summary>Updates the socket from the sim. Cheap when nothing changed.</summary>
        public void Sync(HouseSim sim, bool roomVisible)
        {
            if (_socket.Revealed && !_shownRevealed) _flipStart = Time.unscaledTime;
            _shownRevealed = _socket.Revealed;

var look = SocketLook.For(_socket.Revealed, _socket.CircuitId, _socket.AssumedCircuitId);
            string text = look.DashedCircuit.Length > 0 ? "?" : look.Text;
            Color color = look.FillCircuit.Length > 0 ? _config.CircuitColor(look.FillCircuit) : _config.unknownSocket;
            // Day mode: a room is its own circuit, so there is nothing to find out. Plain neutral sockets, no letters.
            if (_day)
            {
                text = "";
                color = _config.daySocket;
                look = new SocketLook("", "", "");
            }
            if (!string.Equals(text, _shownText))
            {
                _shownText = text;
                _label.text = text;
            }
            _face.color = color;
            bool dashed = look.DashedCircuit.Length > 0;
            if (_dashed.enabled != dashed) _dashed.enabled = dashed;
            if (dashed) _dashed.color = _config.CircuitColor(look.DashedCircuit);
            if (_padlock.enabled != _socket.Reserved) _padlock.enabled = _socket.Reserved;

            // Flip on reveal: scale x goes 1 → 0 → 1 over 0.25 s, with the colour already the new one on the way back.
            float sx = 1f;
            if (_flipStart >= 0f)
            {
                float t = (Time.unscaledTime - _flipStart) / 0.25f;
                if (t >= 1f) _flipStart = -1f;
                else sx = Mathf.Abs(1f - 2f * t);
            }
            _faceRoot.localScale = new Vector3(Mathf.Max(0.02f, sx), 1f, 1f);

            // Power out over the socket area: only where the player could already tell (no wiring leak).
            bool lit = true;
            var circuit = sim.FindCircuit(_socket.CircuitId);
            if (circuit != null) lit = circuit.IsOn;
            bool tellable = _socket.Revealed || SwitchedOnHere(sim);
            bool patch = !_day && roomVisible && !lit && tellable;
            if (_powerOut.enabled != patch) _powerOut.enabled = patch;
            Place(sim);
        }

        private bool SwitchedOnHere(HouseSim sim)
        {
            if (string.IsNullOrEmpty(_socket.OccupantId)) return false;
            var app = sim.FindAppliance(_socket.OccupantId);
            return app != null && app.SeenSwitchedOn;
        }

        private void Place(HouseSim sim)
        {
            var room = sim.FindRoom(_socket.RoomId);
            if (room == null) return;
            float x = HouseLayout.SocketX(room.X, room.Width, _socket.Pos);
            float y = HouseLayout.FloorY(room.Floor, _config.roomHeight) + _config.socketHeight;
            transform.localPosition = new Vector3(x, y, _config.roomDepth);
            WallPoint = new Vector2(x, y);
        }

        private static SpriteRenderer MakeSprite(string name, Sprite sprite, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
