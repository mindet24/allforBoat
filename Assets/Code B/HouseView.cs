using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    public sealed class HouseView
    {
        public const float X = 400f, Y = 176f, W = 1120f, H = 704f;

        readonly LevelScreen screen;
        readonly RectTransform root, lineLayer;
        readonly List<RoomView> rooms = new List<RoomView>();
        readonly Dictionary<string, RoomView> roomById = new Dictionary<string, RoomView>();
        readonly Dictionary<string, int> roomIndex = new Dictionary<string, int>();
        readonly Dictionary<string, SocketView> sockets = new Dictionary<string, SocketView>();
        readonly HashSet<string> seen = new HashSet<string>();
        readonly List<string> removeBuf = new List<string>();
        readonly List<Image> lines = new List<Image>();

        public HouseView(LevelScreen screen, Transform parent, LevelData data)
        {
            this.screen = screen;
            root = UIFactory.MakeRect(parent, "House", X, Y, W, H);
            int n = 0;
            foreach (var r in data.rooms) n++;
            float cell = W / n;
            int i = 0;
            foreach (var r in data.rooms)
            {
                var rv = RoomView.Create(screen, root, r.id, r.name, i * cell + 8f, 8f, cell - 16f, H - 16f);
                rooms.Add(rv); roomById[r.id] = rv; roomIndex[r.id] = i;
                i++;
            }
            lineLayer = UIFactory.MakeRect(root, "CordLines", 0, 0, W, H);
        }

        public void Sync(HouseSim sim)
        {
            seen.Clear();
            bool changed = false;
            var socks = sim.Sockets;

            for (int i = 0; i < socks.Count; i++)
            {
                var s = socks[i];
                seen.Add(s.Id);
                if (sockets.ContainsKey(s.Id)) continue;
                if (!roomById.TryGetValue(s.RoomId, out var room)) continue;
                sockets[s.Id] = SocketView.Create(screen, room, s.Id);
                changed = true;
            }

            removeBuf.Clear();
            foreach (var kv in sockets) if (!seen.Contains(kv.Key)) removeBuf.Add(kv.Key);
            for (int i = 0; i < removeBuf.Count; i++)
            {
                Object.Destroy(sockets[removeBuf[i]].gameObject);
                sockets.Remove(removeBuf[i]);
                changed = true;
            }
            if (changed) Relayout(sim);

            for (int i = 0; i < socks.Count; i++)
                if (sockets.TryGetValue(socks[i].Id, out var sv))
                    sv.Refresh(socks[i], CircuitOn(sim, socks[i].CircuitId));

            UpdateLines(sim);
        }

        static bool CircuitOn(HouseSim sim, string id)
        {
            var cs = sim.Circuits;
            for (int i = 0; i < cs.Count; i++) if (cs[i].Id == id) return cs[i].IsOn;
            return true;
        }

        void Relayout(HouseSim sim)
        {
            var socks = sim.Sockets;
            foreach (var room in rooms)
            {
                int k = 0;
                for (int i = 0; i < socks.Count; i++) if (socks[i].RoomId == room.RoomId) k++;
                if (k == 0) continue;
                float slot = Mathf.Min(150f, room.W / k);
                float start = (room.W - slot * k) * 0.5f;
                int idx = 0;
                for (int i = 0; i < socks.Count; i++)
                {
                    if (socks[i].RoomId != room.RoomId) continue;
                    if (!sockets.TryGetValue(socks[i].Id, out var sv)) continue;
                    sv.Slot = slot;
                    sv.X = start + idx * slot + (slot - 96f) * 0.5f;
                    sv.Y = room.H - 140f;
                    UIFactory.SetPos(sv.Rt, sv.X, sv.Y);
                    idx++;
                }
            }
        }

        // เครื่องที่เสียบอยู่ยืนอยู่เหนือปลั๊ก (พิกัดสัมบูรณ์บน ChipLayer)
        public void PlaceChip(ApplianceView v, string socketId)
        {
            if (!sockets.TryGetValue(socketId, out var sv)) return;
            float s = Mathf.Min(1f, (sv.Slot - 8f) / 140f);
            float cx = X + sv.Room.X + sv.X + 48f;
            float top = Y + sv.Room.Y + sv.Y;
            v.SetPlaced(cx - 70f * s, top - 110f * s - 6f, s);
        }

        public bool IsDark(string socketId)
        {
            return sockets.TryGetValue(socketId, out var sv) && sv.Revealed && !sv.CircuitOn;
        }

        // ไฮไลต์ห้องข้างเคียงขณะรอเลือกห้องปลายทางสายพ่วง
        public void ShowCordTargets(string srcSocket)
        {
            int src = -1;
            if (!string.IsNullOrEmpty(srcSocket) && sockets.TryGetValue(srcSocket, out var sv)) src = roomIndex[sv.RoomId];
            for (int i = 0; i < rooms.Count; i++) rooms[i].SetHighlight(src >= 0 && Mathf.Abs(i - src) == 1);
        }

        void UpdateLines(HouseSim sim)
        {
            int li = 0;
            var socks = sim.Sockets;
            for (int i = 0; i < socks.Count; i++)
            {
                var s = socks[i];
                if (!s.IsCord || !s.Id.StartsWith("CORD-")) continue;
                if (!sockets.TryGetValue(s.Id, out var cv)) continue;
                if (!sockets.TryGetValue(s.Id.Substring(5), out var sv)) continue;
                var img = GetLine(li++);
                img.gameObject.SetActive(true);
                DrawLine(img.rectTransform, Center(sv), Center(cv));
            }
            for (; li < lines.Count; li++) lines[li].gameObject.SetActive(false);
        }

        static Vector2 Center(SocketView sv) { return new Vector2(sv.Room.X + sv.X + 48f, sv.Room.Y + sv.Y + 48f); }

        Image GetLine(int i)
        {
            while (lines.Count <= i)
                lines.Add(UIFactory.Box(lineLayer, "Cord", 0, 0, 10, 6, new Color(1, 1, 1, 0.7f)));
            return lines[i];
        }

        static void DrawLine(RectTransform rt, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(d.magnitude, 6f);
            rt.anchoredPosition = new Vector2(a.x, -a.y);
            rt.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
    }
}