using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Day mode: the family (Mom, Dad, Daughter, Grandma) standing in the house, drawn from family_{name}.png. Each
    /// person waits in the room of their request (<see cref="FamilyRules"/>) and hops while they wait; when they move
    /// room they fade out and back in at the new spot. In a blackout everyone stops hopping, shakes and shows a
    /// "!?" bubble, so the dark reads as trouble for the whole family. Drawn behind the player and the appliances.
    /// People without art are not drawn.
    /// </summary>
    public sealed class FamilyView : MonoBehaviour
    {
        private sealed class Member
        {
            public string Name;
            public List<string> Rooms;
            public SpriteRenderer Sprite;
            public SpriteRenderer Shadow;
            public SpriteRenderer Badge;
            public Color BadgeColor;
            public Transform Bubble;
            public SpriteRenderer BubbleDisc;
            public SpriteRenderer BubbleRim;
            public TMPro.TextMeshPro BubbleText;
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

        /// <summary>
        /// Makes a sprite for each person of the day who has art, with a ring on the floor in
        /// <paramref name="personColor"/>(name), the colour of their request cards.
        /// </summary>
        public void Build(HouseSim sim, TrippedViewConfig config, ArtLibrary art, System.Func<string, Color> personColor)
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
                var color = personColor(person.name);
                var badge = SpriteStyle.FootBadge(go.transform, height * config.footBadgeWidth, color, config);
                var member = new Member { Name = person.name, Rooms = person.rooms, Sprite = sr, Shadow = shadow, Badge = badge, BadgeColor = color };
                MakeBubble(member, go.transform, height);
                _members.Add(member);
                if (_members.Count == _targets.Length) break;
            }
        }

        /// <summary>The blackout bubble over the head: a white disc, dark rim and the emote, hidden until a blackout.</summary>
        private void MakeBubble(Member m, Transform root, float height)
        {
            var bubble = new GameObject("DarkBubble").transform;
            bubble.SetParent(root, false);
            bubble.localPosition = new Vector3(0f, height + _config.familyDarkBubbleSize * 0.6f, -0.02f);
            m.Bubble = bubble;
            (m.BubbleDisc, m.BubbleRim) = SpriteStyle.Halo(bubble, Vector3.zero, _config.familyDarkBubbleSize, Color.white,
                _config.spriteOutlineColor, 0);
            m.BubbleText = Prims.Text(bubble, "Emote", new Vector3(0f, 0f, -0.01f),
                Vector2.one * _config.familyDarkBubbleSize * 0.8f, _config.familyDarkEmote, _config.familyDarkEmoteColor, 3f);
            bubble.gameObject.SetActive(false);
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
            bool dark = DayRules.IsBlackout(sim);
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
                    if (m.Badge != null) m.Badge.enabled = false;
                    if (m.Bubble.gameObject.activeSelf) m.Bubble.gameObject.SetActive(false);
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
                // Blackout: no hopping, a nervous shake instead.
                float hop = waiting && !dark ? Mathf.Abs(Mathf.Sin((time + i * 0.37f) * _config.familyHopSpeed)) * _config.familyHopHeight : 0f;
                float shake = dark ? Mathf.Sin((time + i * 0.21f) * _config.familyDarkShakeHz * Mathf.PI * 2f) * _config.familyDarkShake : 0f;
                float fz = _config.dayFrontZ + _config.familyDepth;
                m.Sprite.transform.parent.localPosition = new Vector3(m.Spot.x + shake, m.Spot.y + hop, fz);
                int order = HouseLayout.DepthSortingOrder(fz);
                m.Sprite.sortingOrder = order;
                bool bubble = dark && m.Alpha > 0.5f;
                if (m.Bubble.gameObject.activeSelf != bubble) m.Bubble.gameObject.SetActive(bubble);
                if (bubble)
                {
                    m.BubbleDisc.enabled = m.BubbleRim.enabled = true;
                    m.BubbleDisc.sortingOrder = order + 1;
                    m.BubbleRim.sortingOrder = order + 2;
                    m.BubbleText.sortingOrder = order + 3;
                    float pop = 1f + 0.08f * Mathf.Sin((time + i * 0.5f) * 6f);
                    m.Bubble.localScale = new Vector3(pop, pop, 1f);
                }
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
                if (m.Badge != null)
                {
                    // The ring stays on the floor too.
                    m.Badge.transform.localPosition = new Vector3(0f, 0.015f - hop, 0f);
                    var b = m.BadgeColor;
                    m.Badge.color = new Color(b.r, b.g, b.b, _config.footBadgeAlpha * m.Alpha);
                    m.Badge.enabled = m.Sprite.enabled;
                }
            }
        }
    }
}
