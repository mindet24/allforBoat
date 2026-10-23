using TMPro;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// A socket plate on a room's back wall. Shows its circuit letter and colour only once the socket is revealed;
    /// before that it is grey with "?" (or "A?" in the assumed circuit's colour when the labels suggest one).
    /// </summary>
    public sealed class SocketView : MonoBehaviour
    {
        private SocketState _socket;
        private TrippedViewConfig _config;
        private SpriteRenderer _face;
        private TextMeshPro _label;
        private string _shownText;
        private Color _shownColor;
        private bool _highlighted;

        /// <summary>Sim id of the socket.</summary>
        public string SocketId => _socket.Id;
        /// <summary>The sim socket, for click handling.</summary>
        public SocketState Socket => _socket;
        /// <summary>Frame the view was last matched to a sim socket (to drop views of removed cord sockets).</summary>
        public int SeenFrame { get; set; }

        /// <summary>Builds the plate under this object and places it on the wall.</summary>
        public void Build(SocketState socket, HouseSim sim, TrippedViewConfig config)
        {
            _socket = socket;
            _config = config;
            float s = config.socketSize;
            // A block sticking out of the back wall; this object sits at the wall surface.
            float d = config.socketDepth;
            Prims.Box(transform, "Block", new Vector3(0f, 0f, -d * 0.5f), new Vector3(s, s, d), config.socketBody);
            // The face is an unlit sprite so the circuit colour shows exactly as defined (it is data, not decoration).
            var faceGo = new GameObject("Face");
            faceGo.transform.SetParent(transform, false);
            faceGo.transform.localPosition = new Vector3(0f, 0f, -d - 0.005f);
            _face = faceGo.AddComponent<SpriteRenderer>();
            _face.sprite = PlaceholderArt.WorldRounded;
            _face.drawMode = SpriteDrawMode.Sliced;
            _face.size = new Vector2(s * 0.92f, s * 0.92f);
            _face.sortingOrder = 1;
            _label = Prims.Text(transform, "Circuit", new Vector3(0f, 0f, -d - 0.01f), new Vector2(s * 0.9f, s * 0.8f), "?", config.worldText, 3f);
            _label.sortingOrder = 2;
            if (socket.IsCord)
                Prims.Text(transform, "Cord", new Vector3(0f, -s * 0.85f, -d - 0.01f), new Vector2(s * 2f, s * 0.4f), "CORD", config.roomNameText, 1.5f);
            ClickTarget.Add(gameObject, ClickKind.Socket, socket.Id, socket.RoomId, 2, new Vector3(0f, 0f, -d * 0.5f), new Vector3(s * 1.6f, s * 1.6f, d + 0.1f));
            Place(sim);
        }

        /// <summary>Highlights the plate while an appliance is selected and the pointer is over it.</summary>
        public void SetHighlight(bool on)
        {
            if (_highlighted == on) return;
            _highlighted = on;
            transform.localScale = on ? Vector3.one * 1.35f : Vector3.one;
        }

        /// <summary>Updates the plate from the sim. Cheap when nothing changed.</summary>
        public void Sync(HouseSim sim, bool roomVisible)
        {
            string text;
            Color color;
            if (_socket.Revealed)
            {
                text = _socket.CircuitId;
                color = _config.CircuitColor(_socket.CircuitId);
            }
            else if (!string.IsNullOrEmpty(_socket.AssumedCircuitId))
            {
                text = _socket.AssumedCircuitId + "?";
                color = Color.Lerp(_config.unknownSocket, _config.CircuitColor(_socket.AssumedCircuitId), 0.5f);
            }
            else
            {
                text = "?";
                color = _config.unknownSocket;
            }
            if (!roomVisible) color *= _config.hiddenRoomTint;
            color.a = 1f;

            if (text != _shownText)
            {
                _shownText = text;
                _label.text = text;
            }
            if (color != _shownColor)
            {
                _shownColor = color;
                _face.color = color;
            }
            Place(sim);
        }

        private void Place(HouseSim sim)
        {
            var room = sim.FindRoom(_socket.RoomId);
            if (room == null) return;
            float x = HouseLayout.SocketX(room.X, room.Width, _socket.Pos);
            float y = HouseLayout.FloorY(room.Floor, _config.roomHeight) + _config.socketHeight;
            transform.localPosition = new Vector3(x, y, _config.roomDepth);
        }
    }
}
