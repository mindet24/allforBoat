using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Trouble.Game
{
    public sealed class TrayView
    {
        public const float Y = 880f;
        CordChip cord;

        public TrayView(LevelScreen screen, Transform parent)
        {
            UIFactory.Box(parent, "Tray", 0, Y, 1920, 200, Theme.Panel);
            UIFactory.Label(parent, 24, Y + 6, 700, 24, "TRAY  -  drag appliances onto a socket", 18, Theme.TextDim, TextAlignmentOptions.TopLeft);
        }

        public void CreateCordChip(LevelScreen screen, RectTransform chipLayer)
        {
            cord = CordChip.Create(screen, chipLayer, 1756f, Y + 45f);
        }

        public void PlaceChip(ApplianceView v, int slot) { v.SetPlaced(24f + slot * 156f, Y + 45f, 1f); }
        public void Refresh(int cordsLeft) { cord.Refresh(cordsLeft); }
    }

    public sealed class CordChip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        LevelScreen screen;
        RectTransform rt, layer;
        CanvasGroup cg;
        TextMeshProUGUI count;
        Vector2 home;
        int left;
        bool dragging;

        public static CordChip Create(LevelScreen screen, RectTransform layer, float x, float y)
        {
            var bg = UIFactory.Box(layer, "CordChip", x, y, 140, 110, Theme.Chip, 14, true);
            var c = bg.gameObject.AddComponent<CordChip>();
            c.screen = screen; c.layer = layer; c.rt = bg.rectTransform;
            c.home = new Vector2(x, -y);
            c.cg = bg.gameObject.AddComponent<CanvasGroup>();
            UIFactory.Label(bg.transform, 0, 14, 140, 40, "CORD", 30, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            c.count = UIFactory.Label(bg.transform, 0, 58, 140, 26, "x0", 22, Theme.TextDim);
            UIFactory.Label(bg.transform, 0, 82, 140, 22, "drop on a socket", 15, Theme.TextDim);
            return c;
        }

        public void Refresh(int cordsLeft)
        {
            left = cordsLeft;
            count.text = "x" + cordsLeft;
            if (!dragging) cg.alpha = cordsLeft > 0 ? 1f : 0.4f;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (left <= 0 || e.button != PointerEventData.InputButton.Left) return;
            dragging = true;
            cg.blocksRaycasts = false;
            rt.SetParent(screen.DragLayer, false);
            Move(e);
        }

        public void OnDrag(PointerEventData e) { if (dragging) Move(e); }

        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging) return;
            screen.EndDragCord(e);
            dragging = false;
            cg.blocksRaycasts = true;
            rt.SetParent(layer, false);
            rt.anchoredPosition = home;
        }

        void Move(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(screen.DragLayer, e.position, null, out var p);
            rt.anchoredPosition = p + new Vector2(-70f, 55f);
        }
    }
}