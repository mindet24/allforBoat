using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Day mode: the family (Mom, Dad, Daughter, Grandma) standing in the house, drawn from family_{name}.png. Each
    /// person waits in the room of their request (<see cref="FamilyRules"/>) and hops while they wait; when they move
    /// room they fade out and back in at the new spot. Drawn behind the player and the appliances. People without
    /// art are not drawn.
    /// </summary>
    public sealed class FamilyView : MonoBehaviour
    {
        private sealed class Member
        {
            public string Name;
            public List<string> Rooms;
            public SpriteRenderer Sprite;
            public SpriteRenderer Shadow;
            public string Room;
            public Vector2 Spot;
            public float Alpha;
        }

        private readonly List<Member> _members = new List<Member>();
        private readonly Dictionary<string, int> _perRoom = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _placed = new Dictionary<string, int>();
        private readonly string[] _targets = new string[8];
        private readonly List<Vector2> _blocked = new List<Vector2>();
        private TrippedViewConfig _config;

        /// <summary>Makes a sprite for each person of the day who has art.</summary>
        public void Build(HouseSim sim, TrippedViewConfig config, ArtLibrary art)
        {
            _config = config;
            var people = sim.Level.day?.people;
            if (people == null) return;
            // The family is drawn at the same pixel scale as the player, so the sheet's heights carry over.
            var player = art.CharacterFrames("idle");
            float playerPx = player != null && player.Length > 0 ? player[0].rect.height : 0f;
            foreach (var person in people)
            {
                var frames = art.FamilyFrames(person.name);
                if (frames == null || frames.Length == 0) continue;
                float px = frames[0].rect.height;
                float height = config.playerWorldHeight * (playerPx > 0f ? px / playerPx : 1f);
                var go = new GameObject("Family " + person.name);
                go.transform.SetParent(transform, false);
                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(go.transform, false);
                spriteGo.transform.localScale = Vector3.one * height;
                var sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                // Behind the player (-1) and every appliance and socket.
                sr.sortingOrder = -2;
                SpriteStyle.Outline(sr, config);
                var shadow = SpriteStyle.BlobShadow(go.transform, height * config.blobShadowWidth, config);
                _members.Add(new Member { Name = person.name, Rooms = person.rooms, Sprite = sr, Shadow = shadow });
                if (_members.Count == _targets.Length) break;
            }
        }

        /// <summary>Places and animates everyone for this frame, keeping them out from behind <paramref name="appliances"/>.</summary>
        public void Sync(HouseSim sim, float time, IReadOnlyList<ApplianceView> appliances)
        {
            if (_members.Count == 0) return;
            var requests = sim.Requests;
            _perRoom.Clear();
            for (int i = 0; i < _members.Count; i++)
            {
                string room = FamilyRules.RoomOf(_members[i].Name, _members[i].Rooms, requests);
                _targets[i] = room;
                if (room == null) continue;
                _perRoom.TryGetValue(room, out int n);
                _perRoom[room] = n + 1;
            }

            _placed.Clear();
            float dt = Time.unscaledDeltaTime;
            float fade = dt / Mathf.Max(0.01f, _config.familyFadeTime);
            for (int i = 0; i < _members.Count; i++)
            {
                var m = _members[i];
                string target = _targets[i];
                var roomState = target != null ? sim.FindRoom(target) : null;
                if (roomState == null)
                {
                    m.Sprite.enabled = false;
                    if (m.Shadow != null) m.Shadow.enabled = false;
                    continue;
                }
                _placed.TryGetValue(target, out int slot);
                _placed[target] = slot + 1;
                _blocked.Clear();
                for (int k = 0; k < appliances.Count; k++)
                    if (appliances[k].ShownRoomId == target) _blocked.Add(appliances[k].SpanX);
                float x = FamilyRules.SlotX(roomState.X, roomState.Width, slot, _perRoom[target]);
                x = FamilyRules.AvoidX(x, _config.familyHalfWidth, roomState.X, roomState.X + roomState.Width, _blocked);
                var spot = new Vector2(x, HouseLayout.FloorY(roomState.Floor, _config.roomHeight));

                // A new room: fade out where they are, then appear at the new spot. A first placement is instant.
                if (m.Room == null)
                {
                    m.Room = target;
                    m.Spot = spot;
                    m.Alpha = 1f;
                }
                else if (m.Room != target)
                {
                    m.Alpha = Mathf.Max(0f, m.Alpha - fade);
                    if (m.Alpha <= 0f)
                    {
                        m.Room = target;
                        m.Spot = spot;
                    }
                }
                else
                {
                    // Same room, new slot (someone came or left): step aside.
                    m.Spot = Vector2.MoveTowards(m.Spot, spot, _config.characterFollowSpeed * dt);
                    m.Alpha = Mathf.Min(1f, m.Alpha + fade);
                }

                bool waiting = FamilyRules.IsWaiting(m.Name, requests);
                float hop = waiting ? Mathf.Abs(Mathf.Sin((time + i * 0.37f) * _config.familyHopSpeed)) * _config.familyHopHeight : 0f;
                float fz = _config.dayFrontZ + _config.familyDepth;
                m.Sprite.transform.parent.localPosition = new Vector3(m.Spot.x, m.Spot.y + hop, fz);
                m.Sprite.sortingOrder = HouseLayout.DepthSortingOrder(fz);
                var c = m.Sprite.color;
                if (!Mathf.Approximately(c.a, m.Alpha)) m.Sprite.color = new Color(c.r, c.g, c.b, m.Alpha);
                m.Sprite.enabled = m.Alpha > 0.001f;
                if (m.Shadow != null)
                {
                    // The shadow stays on the floor while the person hops, and fades with them.
                    m.Shadow.transform.localPosition = new Vector3(0f, 0.01f - hop, 0f);
                    m.Shadow.color = new Color(0f, 0f, 0f, _config.blobShadowAlpha * m.Alpha);
                    m.Shadow.enabled = m.Sprite.enabled;
                }
            }
        }
    }
}
