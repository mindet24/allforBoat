using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The house shell built from the artist's 3D kit (Art/Models/HouseKit.fbx, Room_Kitchen.fbx) instead of
    /// greybox boxes: per room a floor and back wall scaled to the room's width, ceilings where nothing is above,
    /// side walls (with a door opening where a door is), roofs over the top, a foundation under the ground floor.
    /// Purely decorative: every gameplay position (sockets, panel, stairs, character) still comes from the sim.
    /// </summary>
    /// <remarks>
    /// A kit module is 6 m wide, 4.2 m deep, 3 m tall, with its origin at floor level. It imports turned 180° about
    /// y (x and depth negated: the room spans x −6..0, depth 0..−4.2), so every piece is yawed by
    /// <see cref="TrippedViewConfig.kitYaw"/> to land on x 0..6, depth 0..4.2 with the open front at z = 0.
    /// </remarks>
    public sealed class HouseKitView : MonoBehaviour
    {
        private TrippedViewConfig _config;
        private Transform _kit;

        /// <summary>True when the config asks for the kit and the kit is assigned.</summary>
        public static bool Available(TrippedViewConfig config) => config != null && config.useHouseKit && config.houseKit != null;

        /// <summary>Extra space the roof (above) and the foundation (below) add to the house rect.</summary>
        public const float RoofHeight = 1.8f, FoundationDepth = 1.0f;

        /// <summary>Builds the whole shell for these rooms and doors.</summary>
        public void Build(HouseSim sim, TrippedViewConfig config)
        {
            _config = config;
            _kit = config.houseKit.transform;
            float h = config.roomHeight;
            float module = Mathf.Max(0.1f, config.kitModuleWidth);
            int topFloor = int.MinValue;
            foreach (var r in sim.Rooms) topFloor = Mathf.Max(topFloor, r.Floor);

            foreach (var room in sim.Rooms)
            {
                float y = HouseLayout.FloorY(room.Floor, h);
                var holder = Holder("Kit " + room.Id, new Vector3(room.X, y, 0f), room.Width / module);
                bool kitchen = room.Theme == "kitchen" && config.kitchenRoom != null;
                if (kitchen) AddKitchen(holder);
                else
                {
                    Piece(holder, "Room_Floor");
                    Piece(holder, "Room_BackWall");
                }

                // Ceiling and roof only where no room above provides the floor (they would poke into it).
                var covers = new List<Vector2>();
                foreach (var other in sim.Rooms)
                    if (other.Floor == room.Floor + 1) covers.Add(new Vector2(other.X, other.X + other.Width));
                foreach (var span in HouseLayout.UncoveredSpans(room.X, room.X + room.Width, covers))
                {
                    float w = span.y - span.x;
                    var top = Holder("Top " + room.Id, new Vector3(span.x, y, 0f), w / module);
                    Piece(top, "Room_Ceiling");
                    Piece(top, "Roof_Segment");
                    // Gables only at the open ends of the roof, never into a room beside it on the floor above.
                    if (!Touches(sim.Rooms, room.Floor + 1, span.x)) Piece(Holder("GableL", new Vector3(span.x, y, 0f), 1f), "Roof_GableLeft");
                    if (!Touches(sim.Rooms, room.Floor + 1, span.y)) Piece(Holder("GableR", new Vector3(span.y - module, y, 0f), 1f), "Roof_GableRight");
                    if (room.Floor == topFloor && room.Width >= module) Piece(Holder("Chimney", new Vector3(span.x, y, 0f), 1f), "Chimney");
                }

                if (room.Floor == 0)
                    Piece(Holder("Foundation " + room.Id, new Vector3(room.X, y, 0f), room.Width / module), "Foundation_Segment");
            }

            foreach (var wall in HouseLayout.Walls(sim.Rooms, sim.Doors))
            {
                var holder = Holder("Wall x" + wall.X.ToString("0.##"), new Vector3(wall.X, HouseLayout.FloorY(wall.Floor, h), 0f), 1f);
                Piece(holder, string.IsNullOrEmpty(wall.DoorId) ? "Room_SideWall_Solid" : "Room_SideWall_Door");
            }
        }

        /// <summary>A copy of the kit's Socket_Plate (or null without a kit), yawed to face the camera.</summary>
        public static GameObject SocketPlate(TrippedViewConfig config, Transform parent)
        {
            if (!Available(config)) return null;
            var source = config.houseKit.transform.Find("Socket_Plate");
            if (source == null) return null;
            var go = Instantiate(source.gameObject, parent, false);
            go.name = "Socket_Plate";
            go.transform.localPosition = new Vector3(0f, 0f, -0.03f); // just in front of the kit back wall (z 3.98)
            go.transform.localRotation = Quaternion.Euler(0f, config.kitYaw, 0f);
            go.transform.localScale = Vector3.one * config.socketPlateScale;
            return go;
        }

        private static bool Touches(IReadOnlyList<RoomState> rooms, int floor, float x)
        {
            foreach (var r in rooms)
                if (r.Floor == floor && x > r.X - 0.01f && x < r.X + r.Width + 0.01f) return true;
            return false;
        }

        private void AddKitchen(Transform holder)
        {
            var skip = new HashSet<string>(_config.kitchenSkipParts ?? new string[0]);
            foreach (Transform part in _config.kitchenRoom.transform)
            {
                if (skip.Contains(part.name)) continue;
                Copy(holder, part);
            }
        }

        private Transform Holder(string name, Vector3 position, float scaleX)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(scaleX, 1f, 1f);
            return go.transform;
        }

        private void Piece(Transform holder, string name)
        {
            var source = _kit.Find(name);
            if (source == null)
            {
                Debug.LogWarning("[TRIPPED] House kit has no piece " + name + ".");
                return;
            }
            Copy(holder, source);
        }

        private void Copy(Transform holder, Transform source)
        {
            // A yawed pivot under the (x-scaled) holder: the piece keeps its own offsets inside the module.
            var pivot = new GameObject(source.name).transform;
            pivot.SetParent(holder, false);
            pivot.localRotation = Quaternion.Euler(0f, _config.kitYaw, 0f);
            var copy = Instantiate(source.gameObject, pivot, false);
            copy.name = source.name;
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
        }
    }
}
