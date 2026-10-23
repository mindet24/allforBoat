using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Trouble.Game
{
    public sealed class RoomView : MonoBehaviour, IPointerClickHandler
    {
        public string RoomId;
        public RectTransform Rt;
        public float X, Y, W, H;
        LevelScreen screen;
        Image border;

        public static RoomView Create(LevelScreen screen, Transform parent, string id, string name,
            float x, float y, float w, float h)
        {
            var bg = UIFactory.Box(parent, "Room_" + id, x, y, w, h, Theme.Room, 16, true);
            var v = bg.gameObject.AddComponent<RoomView>();
            v.screen = screen; v.RoomId = id; v.Rt = bg.rectTransform;
            v.X = x; v.Y = y; v.W = w; v.H = h;
            v.border = UIFactory.Ring(bg.transform, "Border", 0, 0, w, h, Theme.Border, 16, 3);
            UIFactory.Label(bg.transform, 16, 10, w - 32, 36, name, 24, Theme.TextDim, TextAlignmentOptions.TopLeft);
            return v;
        }

        public void SetHighlight(bool on) { border.color = on ? Theme.Glow : Theme.Border; }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) screen.RoomClicked(RoomId);
        }
    }
}