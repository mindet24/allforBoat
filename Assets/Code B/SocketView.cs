using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Trouble.Game
{
    public sealed class SocketView : MonoBehaviour, IPointerClickHandler
    {
        public string Id, RoomId;
        public RoomView Room;
        public RectTransform Rt;
        public float X, Y, Slot = 150f;
        public bool Revealed, CircuitOn = true, IsCord;

        LevelScreen screen;
        Image fill, ring, overlay;
        TextMeshProUGUI label, tag;
        GameObject dashed, padlock;
        Color lastDashed;
        bool init;
        float pulse = -1f;

        public static SocketView Create(LevelScreen screen, RoomView room, string id)
        {
            var fill = UIFactory.Box(room.Rt, "Socket_" + id, 0, 0, 96, 96, Color.clear, 16, true);
            var v = fill.gameObject.AddComponent<SocketView>();
            v.screen = screen; v.Id = id; v.Room = room; v.RoomId = room.RoomId;
            v.Rt = fill.rectTransform; v.fill = fill;
            v.ring = UIFactory.Ring(fill.transform, "Ring", 0, 0, 96, 96, Theme.Unknown, 16, 4);
            v.dashed = UIFactory.DashedBorder(fill.transform, 96, 96, Theme.Unknown);
            v.dashed.SetActive(false);
            v.label = UIFactory.Label(fill.transform, 0, 16, 96, 56, "?", 44, Theme.Unknown, TextAlignmentOptions.Center, FontStyles.Bold);
            v.tag = UIFactory.Label(fill.transform, 0, 74, 96, 18, id, 15, Theme.TextDim);
            v.overlay = UIFactory.Box(fill.transform, "Dark", 0, 0, 96, 96, new Color(0, 0, 0, 0.55f), 16);
            v.overlay.gameObject.SetActive(false);

            // กุญแจ (วาดจากสี่เหลี่ยม ไม่พึ่งฟอนต์)
            var lockRoot = UIFactory.MakeRect(fill.transform, "Padlock", 0, 0, 96, 96).gameObject;
            UIFactory.Ring(lockRoot.transform, "Shackle", 68, 50, 16, 20, Theme.Text, 8, 3);
            UIFactory.Box(lockRoot.transform, "Body", 64, 64, 24, 18, Theme.Text, 4);
            lockRoot.SetActive(false);
            v.padlock = lockRoot;
            return v;
        }

        public void Refresh(SocketState s, bool circuitOn)
        {
            IsCord = s.IsCord;
            CircuitOn = circuitOn;
            if (init && !Revealed && s.Revealed) pulse = 0f;
            Revealed = s.Revealed;
            init = true;

            bool assumed = !s.Revealed && !string.IsNullOrEmpty(s.AssumedCircuitId);
            Color col = s.Revealed ? Theme.Circuit(s.CircuitId)
                      : assumed ? Theme.Circuit(s.AssumedCircuitId) : Theme.Unknown;

            var f = col; f.a = s.Revealed ? 0.25f : 0f;
            fill.color = f;
            ring.gameObject.SetActive(!assumed);
            ring.color = col;

            dashed.SetActive(assumed);
            if (assumed && col != lastDashed) { UIFactory.Tint(dashed, col); lastDashed = col; }

            label.text = s.Revealed ? s.CircuitId : "?";
            label.color = col;
            tag.text = s.IsCord ? "CORD" : s.Id;
            padlock.SetActive(s.Reserved);
            overlay.gameObject.SetActive(s.Revealed && !circuitOn);
        }

        void Update()
        {
            if (pulse < 0f) return;
            pulse += Time.unscaledDeltaTime;
            float k = pulse / 0.25f;
            if (k >= 1f) { pulse = -1f; Rt.localScale = Vector3.one; return; }
            Rt.localScale = Vector3.one * (1f + 0.2f * (1f - Mathf.Abs(2f * k - 1f)));
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right && IsCord) screen.Submit(Cmds.RemoveCord(Id));
            else if (e.button == PointerEventData.InputButton.Left) screen.RoomClicked(RoomId); // ใช้เลือกห้องปลายทางสายพ่วง
        }
    }
}