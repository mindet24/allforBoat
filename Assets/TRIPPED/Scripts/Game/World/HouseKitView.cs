using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The house shell built from the artist's 3D kit (Art/Models/HouseKit.fbx, Room_Kitchen.fbx) instead of
    /// greybox boxes: per room a floor and back wall scaled to the room's width, ceilings where nothing is above,
    /// side walls (with a door opening at the open front where a door is, so the character walks through it), roofs over the top, a foundation under the ground floor.
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
        /// <summary>The day's seed, which rolls the optional (*_Random*) parts of the room models.</summary>
        private int _daySeed;

        /// <summary>True when the config asks for the kit and the kit is assigned.</summary>
        public static bool Available(TrippedViewConfig config) => config != null && config.useHouseKit && config.houseKit != null;

        /// <summary>
        /// Front-view rect (x, y) of everything the kit built, roof peak and chimney included, so the camera can frame
        /// the whole house. Zero size until <see cref="Build"/> ran.
        /// </summary>
        public Rect Extent { get; private set; }

        /// <summary>Builds the whole shell for these rooms and doors.</summary>
        public void Build(HouseSim sim, TrippedViewConfig config)
        {
            _daySeed = sim.Level.day != null ? sim.Level.day.seed : 0;
            _config = config;
            _kit = config.houseKit.transform;
            float h = config.roomHeight;
            float module = Mathf.Max(0.1f, config.kitModuleWidth);
            int topFloor = int.MinValue;
            foreach (var r in sim.Rooms) topFloor = Mathf.Max(topFloor, r.Floor);

            foreach (var room in sim.Rooms)
            {
                float y = HouseLayout.FloorY(room.Floor, h);
                if (config.TryRoomModel(room.Id, out var entry)) AddRoomModel(room, entry, y);
                else
                {
                    var holder = Holder("Kit " + room.Id, new Vector3(room.X, y, 0f), room.Width / module);
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
                string name = "Wall x" + wall.X.ToString("0.##");
                float y = HouseLayout.FloorY(wall.Floor, h);
                if (string.IsNullOrEmpty(wall.DoorId))
                {
                    Piece(Holder(name, new Vector3(wall.X, y, 0f), 1f), "Room_SideWall_Solid");
                    continue;
                }
                // The kit's own door wall has its opening mid-depth, behind the line the character walks along (just
                // in front of z = 0). Build the wall from two squeezed solid pieces instead, leaving the opening at
                // the open front: a lintel over the full depth, and the wall behind the doorway.
                float depth = Mathf.Max(0.1f, config.kitModuleDepth);
                float doorH = Mathf.Clamp(config.doorHeight, 0.1f, h - 0.1f);
                float doorW = Mathf.Clamp(config.doorWidth, 0.1f, depth - 0.1f);
                var lintel = Holder(name + " lintel", new Vector3(wall.X, y + doorH, 0f), 1f);
                lintel.localScale = new Vector3(1f, (h - doorH) / h, 1f);
                Piece(lintel, "Room_SideWall_Solid");
                var back = Holder(name + " back", new Vector3(wall.X, y, doorW), 1f);
                back.localScale = new Vector3(1f, doorH / h, (depth - doorW) / depth);
                Piece(back, "Room_SideWall_Solid");
            }
            Extent = FrontRect(transform);
        }

        /// <summary>The x, y rect covered by every renderer under <paramref name="root"/>, in the house's local space.</summary>
        private Rect FrontRect(Transform root)
        {
            bool any = false;
            Bounds b = default;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!any) b = r.bounds;
                else b.Encapsulate(r.bounds);
                any = true;
            }
            if (!any) return new Rect(0, 0, 0, 0);
            var min = transform.InverseTransformPoint(b.min);
            var max = transform.InverseTransformPoint(b.max);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
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

        /// <summary>
        /// A furnished room (its own floor, back wall and furniture) in place of the kit's floor and back wall. Models
        /// built at the room's real width are used at scale 1; an older module-width model is stretched in x to fit,
        /// and then also loses its <see cref="RoomModelEntry.hideWhenRescaled"/> parts.
        /// </summary>
        private void AddRoomModel(RoomState room, RoomModelEntry entry, float y)
        {
            var holder = Holder("Room " + room.Id, new Vector3(room.X, y, 0f), 1f);
            var pivot = new GameObject(entry.model.name).transform;
            pivot.SetParent(holder, false);
            pivot.localRotation = Quaternion.Euler(0f, _config.kitYaw, 0f);
            var copy = Instantiate(entry.model, pivot, false);
            copy.name = entry.model.name;

            float modelWidth = FrontRect(copy.transform).width;
            float scaleX = RoomModelRules.ScaleX(modelWidth, room.Width, _config.roomModelWidthTolerance);
            bool rescaled = !Mathf.Approximately(scaleX, 1f);
            holder.localScale = new Vector3(scaleX, 1f, 1f);

            int seed = _daySeed;
            foreach (Transform part in copy.transform)
            {
                if (!RoomModelRules.ShowsRandomPart(part.name, room.Id, seed, _config.randomPropChance))
                {
                    part.gameObject.SetActive(false);
                    continue;
                }
                if (!RoomModelRules.Hides(part.name, entry.hideParts, entry.hideWhenRescaled, rescaled)) continue;
                part.gameObject.SetActive(false);
                Debug.Log($"[TRIPPED] Room {room.Id}: hid {part.name} of {entry.model.name}" + (rescaled ? $" (model {modelWidth:0.##} m stretched to {room.Width:0.##} m)." : "."));
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
