using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Marks a collider as clickable and says what it is. Colliders are for clicks only (no physics).
    /// When a ray hits several, the highest <see cref="Priority"/> wins, then the nearest.
    /// </summary>
    public sealed class ClickTarget : MonoBehaviour
    {
        /// <summary>What a click here means.</summary>
        public ClickKind Kind;
        /// <summary>Appliance id, socket id or room id.</summary>
        public string Id = "";
        /// <summary>Room this target is in.</summary>
        public string RoomId = "";
        /// <summary>Appliances 3, sockets 2, rooms 0.</summary>
        public int Priority;

        /// <summary>Adds a box collider and a target to <paramref name="go"/>.</summary>
        public static ClickTarget Add(GameObject go, ClickKind kind, string id, string roomId, int priority, Vector3 center, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            var t = go.AddComponent<ClickTarget>();
            t.Kind = kind;
            t.Id = id ?? "";
            t.RoomId = roomId ?? "";
            t.Priority = priority;
            return t;
        }
    }
}
