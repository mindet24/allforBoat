using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The wire inside the wall from one socket, up to the ceiling and across to the Panel (decision Q28).
    /// It is grey until the socket is Revealed; then it lights in the circuit colour with a sweep from the socket
    /// to the panel (wireSweepSeconds). An unrevealed wire is never coloured, not even by an assumed circuit.
    /// </summary>
    public sealed class WireView : MonoBehaviour
    {
        private SocketState _socket;
        private TrippedViewConfig _config;
        private LineRenderer _base;
        private LineRenderer _lit;
        private readonly List<Vector2> _route = new List<Vector2>();
        private readonly List<Vector2> _partial = new List<Vector2>();
        private float _sweepStart = -1f;
        private bool _wasRevealed;
        private float _shownFraction = -1f;

        /// <summary>The socket this wire belongs to.</summary>
        public SocketState Socket => _socket;

        /// <summary>Builds the two lines (grey base, coloured overlay). <paramref name="lane"/> 0.. separates parallel runs.</summary>
        public void Build(SocketState socket, HouseSim sim, TrippedViewConfig config, int lane, int laneCount)
        {
            _socket = socket;
            _config = config;
            _base = Prims.Line(transform, "Grey", config.wireWidth, 4);
            _lit = Prims.Line(transform, "Lit", config.wireWidth * 1.15f, 5);
            _lit.enabled = false;

            var room = sim.FindRoom(socket.RoomId);
            var panelRoom = sim.FindRoom(sim.PanelRoomId);
            if (room == null || panelRoom == null) return;
            float x = HouseLayout.SocketX(room.X, room.Width, socket.Pos);
            float floorY = HouseLayout.FloorY(room.Floor, config.roomHeight);
            var from = new Vector2(x, floorY + config.socketHeight + config.socketSize * 0.5f);
            // The panel box sits 0.7 m in from its room's left wall, 1.5 m up (RoomView); wires enter its top,
            // spread a little so they do not overlap.
            float spread = (lane - (laneCount - 1) * 0.5f) * 0.05f;
            var panelTop = new Vector2(panelRoom.X + 0.7f + spread,
                HouseLayout.FloorY(panelRoom.Floor, config.roomHeight) + 1.5f + 0.375f);
            float ceiling = floorY + config.roomHeight - 0.2f;
            WireRouting.Route(from, panelTop, ceiling, 0.06f + 0.06f * lane, _route);
            Prims.SetLine(_base, _route, config.roomDepth - 0.015f, config.wireUnknown);
            _wasRevealed = socket.Revealed;
            if (_wasRevealed) ShowLit(1f);
        }

        /// <summary>Starts the sweep when the socket becomes revealed; animates it.</summary>
        public void Sync()
        {
            if (_socket == null) return;
            if (_socket.Revealed && !_wasRevealed) _sweepStart = Time.unscaledTime;
            _wasRevealed = _socket.Revealed;
            if (!_socket.Revealed) return;

            float fraction = 1f;
            if (_sweepStart >= 0f)
            {
                fraction = Mathf.Clamp01((Time.unscaledTime - _sweepStart) / Mathf.Max(0.01f, _config.wireSweepSeconds));
                if (fraction >= 1f) _sweepStart = -1f;
            }
            if (!Mathf.Approximately(fraction, _shownFraction)) ShowLit(fraction);
        }

        private void ShowLit(float fraction)
        {
            _shownFraction = fraction;
            WireRouting.Truncate(_route, fraction, _partial);
            if (_partial.Count < 2)
            {
                _lit.enabled = false;
                return;
            }
            _lit.enabled = true;
            Prims.SetLine(_lit, _partial, _config.roomDepth - 0.02f, _config.CircuitColor(_socket.CircuitId));
        }
    }
}
