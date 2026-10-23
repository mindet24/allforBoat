using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The extension cord: a sagging cable from the source socket, through the door, to the cord's far-end socket.
    /// One per cord socket; HouseView creates and removes them as cords are placed and taken away.
    /// </summary>
    public sealed class CableView : MonoBehaviour
    {
        private SocketState _cord;
        private LineRenderer _line;
        private TrippedViewConfig _config;
        private readonly List<Vector2> _anchors = new List<Vector2>();
        private readonly List<Vector2> _path = new List<Vector2>();
        private Vector2 _lastFrom = new Vector2(float.NaN, 0f);
        private Vector2 _lastTo;

        /// <summary>The cord socket this cable ends at.</summary>
        public SocketState Cord => _cord;
        /// <summary>Frame the view was last matched to a sim socket.</summary>
        public int SeenFrame { get; set; }

        public void Build(SocketState cord, TrippedViewConfig config)
        {
            _cord = cord;
            _config = config;
            _line = Prims.Line(transform, "Cable", config.cordWidth, 6);
        }

        /// <summary>Re-routes the cable if either end moved.</summary>
        public void Sync(HouseSim sim)
        {
            var source = sim.FindSocket(_cord.CordSourceId);
            var a = sim.FindRoom(source?.RoomId);
            var b = sim.FindRoom(_cord.RoomId);
            if (source == null || a == null || b == null) return;

            var from = new Vector2(HouseLayout.SocketX(a.X, a.Width, source.Pos), HouseLayout.FloorY(a.Floor, _config.roomHeight) + _config.socketHeight - 0.15f);
            var to = new Vector2(HouseLayout.SocketX(b.X, b.Width, _cord.Pos), HouseLayout.FloorY(b.Floor, _config.roomHeight) + _config.socketHeight - 0.15f);
            if (from == _lastFrom && to == _lastTo) return;
            _lastFrom = from;
            _lastTo = to;

            // Through the doorway: the shared wall, at cable height.
            float wallX = a.X < b.X ? a.X + a.Width : b.X + b.Width;
            _anchors.Clear();
            _anchors.Add(from);
            _anchors.Add(new Vector2(wallX, Mathf.Min(from.y, to.y) - 0.1f));
            _anchors.Add(to);
            WireRouting.SagPath(_anchors, _config.cordSag, 8, _path);
            Prims.SetLine(_line, _path, _config.roomDepth - 0.18f, _config.cordColor);
        }
    }
}
