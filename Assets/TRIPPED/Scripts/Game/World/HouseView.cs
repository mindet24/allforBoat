using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The 3D greybox house for one sim: rooms, walls with door openings, sockets, appliances and the character,
    /// built at runtime from the sim's rooms (GDD v1.1 §3.5). Reads sim state every frame in LateUpdate and never
    /// stores game state of its own. Destroy the object to remove everything.
    /// </summary>
    public sealed class HouseView : MonoBehaviour
    {
        private LevelController _controller;
        private HouseSim _sim;
        private TrippedViewConfig _config;
        private readonly List<RoomView> _rooms = new List<RoomView>();
        private readonly Dictionary<string, SocketView> _sockets = new Dictionary<string, SocketView>();
        private readonly List<SocketView> _socketList = new List<SocketView>();
        private readonly List<ApplianceView> _appliances = new List<ApplianceView>();
        private readonly List<DoorState> _doorStates = new List<DoorState>();
        private readonly List<GameObject> _doorLeaves = new List<GameObject>();
        private CharacterView _character;
        private Transform _socketRoot;
        private string _highlightedSocket;

        /// <summary>World rect of the whole house front (x right, y up), for the camera.</summary>
        public Rect Bounds { get; private set; }

        /// <summary>Builds the house for the controller's current sim.</summary>
        public void Build(LevelController controller, TrippedViewConfig config, ArtLibrary art)
        {
            _controller = controller;
            _sim = controller.Sim;
            _config = config;
            Bounds = HouseLayout.HouseBounds(_sim.Rooms, config.roomHeight);

            foreach (var room in _sim.Rooms)
            {
                var go = new GameObject("Room " + room.Id);
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<RoomView>();
                view.Build(room, room.Id == _sim.PanelRoomId, config, art);
                _rooms.Add(view);
            }

            BuildWalls();

            _socketRoot = new GameObject("Sockets").transform;
            _socketRoot.SetParent(transform, false);
            SyncSocketSet();

            var appRoot = new GameObject("Appliances").transform;
            appRoot.SetParent(transform, false);
            foreach (var app in _sim.Appliances)
            {
                var go = new GameObject("Appliance " + app.Id);
                go.transform.SetParent(appRoot, false);
                var view = go.AddComponent<ApplianceView>();
                view.Build(app, _sim.Catalog.Find(app.Type), config, art);
                _appliances.Add(view);
            }

            var chGo = new GameObject("Character");
            chGo.transform.SetParent(transform, false);
            _character = chGo.AddComponent<CharacterView>();
            _character.Build(config, art);

            LateUpdate();
        }

        /// <summary>Highlights one socket (the drop target under the pointer while an appliance is selected). Null = none.</summary>
        public void HighlightSocket(string socketId)
        {
            if (socketId == _highlightedSocket) return;
            if (_highlightedSocket != null && _sockets.TryGetValue(_highlightedSocket, out var old) && old != null) old.SetHighlight(false);
            _highlightedSocket = socketId;
            if (socketId != null && _sockets.TryGetValue(socketId, out var now) && now != null) now.SetHighlight(true);
        }

        /// <summary>The socket view with this id, or null.</summary>
        public SocketView FindSocketView(string socketId)
        {
            return socketId != null && _sockets.TryGetValue(socketId, out var v) ? v : null;
        }

        private void LateUpdate()
        {
            if (_sim == null || _controller == null || _controller.Sim != _sim) return;
            float time = Time.unscaledTime;

            for (int i = 0; i < _rooms.Count; i++) _rooms[i].Sync();
            for (int i = 0; i < _doorStates.Count; i++)
            {
                bool closed = !_doorStates[i].IsOpen;
                if (_doorLeaves[i].activeSelf != closed) _doorLeaves[i].SetActive(closed);
            }

            SyncSocketSet();
            for (int i = 0; i < _socketList.Count; i++)
            {
                var s = _socketList[i];
                s.Sync(_sim, _sim.IsRoomVisible(s.Socket.RoomId));
            }

            string selected = _controller.Selection;
            for (int i = 0; i < _appliances.Count; i++)
                _appliances[i].Sync(_sim, selected != null && selected == _appliances[i].Appliance.Id, time);

            _character.Sync(_sim, _controller.TickFraction, time);
        }

        /// <summary>Creates views for new sockets (extension cords) and removes views of sockets that are gone.</summary>
        private void SyncSocketSet()
        {
            int frame = Time.frameCount;
            var sockets = _sim.Sockets;
            for (int i = 0; i < sockets.Count; i++)
            {
                var s = sockets[i];
                if (!_sockets.TryGetValue(s.Id, out var view) || view == null || view.Socket != s)
                {
                    if (view != null)
                    {
                        _socketList.Remove(view);
                        Destroy(view.gameObject);
                    }
                    var go = new GameObject("Socket " + s.Id);
                    go.transform.SetParent(_socketRoot, false);
                    view = go.AddComponent<SocketView>();
                    view.Build(s, _sim, _config);
                    _sockets[s.Id] = view;
                    _socketList.Add(view);
                }
                view.SeenFrame = frame;
            }
            if (_socketList.Count == sockets.Count) return;
            for (int i = _socketList.Count - 1; i >= 0; i--)
            {
                var view = _socketList[i];
                if (view.SeenFrame == frame) continue;
                _sockets.Remove(view.SocketId);
                _socketList.RemoveAt(i);
                Destroy(view.gameObject);
            }
        }

        private void BuildWalls()
        {
            var root = new GameObject("Walls").transform;
            root.SetParent(transform, false);
            float h = _config.roomHeight, d = _config.roomDepth, t = _config.wallThickness;
            float doorH = Mathf.Min(_config.doorHeight, h - 0.1f);
            float doorW = Mathf.Min(_config.doorWidth, d - 0.1f);

            foreach (var wall in HouseLayout.Walls(_sim.Rooms, _sim.Doors))
            {
                float y0 = HouseLayout.FloorY(wall.Floor, h);
                string name = "Wall x" + wall.X.ToString("0.##");
                if (string.IsNullOrEmpty(wall.DoorId))
                {
                    Prims.Box(root, name, new Vector3(wall.X, y0 + h * 0.5f, d * 0.5f), new Vector3(t, h, d), _config.wallColor);
                    continue;
                }

                // Door opening at the open front of the house so it reads from the camera.
                Prims.Box(root, name + " lintel", new Vector3(wall.X, y0 + doorH + (h - doorH) * 0.5f, d * 0.5f),
                    new Vector3(t, h - doorH, d), _config.wallColor);
                Prims.Box(root, name + " back", new Vector3(wall.X, y0 + doorH * 0.5f, (doorW + d) * 0.5f),
                    new Vector3(t, doorH, d - doorW), _config.wallColor);
                var leaf = Prims.Box(root, "Door " + wall.DoorId, new Vector3(wall.X, y0 + doorH * 0.5f, doorW * 0.5f),
                    new Vector3(t * 0.6f, doorH, doorW), _config.doorColor).gameObject;
                var door = _sim.FindDoor(wall.DoorId);
                leaf.SetActive(door != null && !door.IsOpen);
                if (door != null)
                {
                    _doorStates.Add(door);
                    _doorLeaves.Add(leaf);
                }
            }
        }
    }
}
