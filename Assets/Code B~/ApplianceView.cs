using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Trouble.Game
{
    public sealed class ApplianceView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public string Id;
        public string SocketId = "";
        public bool IsFixed, Dragging;
        public RectTransform Rt;

        LevelScreen screen;
        CanvasGroup cg;
        Image bg, glow, barBg, barFill;
        TextMeshProUGUI loadLbl, stateLbl, tagLbl;
        Vector2 home;
        float homeScale = 1f;
        bool switchedOn, powered;

        public static ApplianceView Create(LevelScreen screen, string id, string type)
        {
            var info = ApplianceInfo.Get(type);
            var bg = UIFactory.Box(screen.ChipLayer, "Chip_" + id, 0, 0, 140, 110, Theme.Chip, 14, true);
            var v = bg.gameObject.AddComponent<ApplianceView>();
            v.screen = screen; v.Id = id; v.Rt = bg.rectTransform; v.bg = bg;
            v.cg = bg.gameObject.AddComponent<CanvasGroup>();
            v.glow = UIFactory.Ring(bg.transform, "Glow", -2, -2, 144, 114, Theme.Glow, 16, 4);
            UIFactory.Label(bg.transform, 0, 10, 140, 44, info.Short, 32, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Label(bg.transform, 4, 54, 132, 24, info.Name, 18, Theme.TextDim);
            var badge = UIFactory.Box(bg.transform, "Load", 106, 6, 30, 24, Theme.Border, 8);
            v.loadLbl = UIFactory.Label(badge.transform, 0, 0, 30, 24, "0", 18, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            v.stateLbl = UIFactory.Label(bg.transform, 8, 78, 64, 22, "OFF", 18, Theme.TextDim, TextAlignmentOptions.Left, FontStyles.Bold);
            v.tagLbl = UIFactory.Label(bg.transform, 72, 78, 62, 22, "", 16, Theme.TextDim, TextAlignmentOptions.Right);
            v.barBg = UIFactory.Box(bg.transform, "BarBg", 10, 102, 120, 6, Theme.Border);
            v.barFill = UIFactory.Box(bg.transform, "BarFill", 10, 102, 0, 6, Theme.Good);
            return v;
        }

        public void SetPlaced(float x, float y, float scale)
        {
            home = new Vector2(x, -y);
            homeScale = scale;
            if (Dragging) return;
            Rt.anchoredPosition = home;
            Rt.localScale = Vector3.one * scale;
        }

        // progress01 < 0 = ไม่มีคำขอ (เช่น โคมไฟ ตู้เย็น)
        public void Refresh(ApplianceState a, float progress01, bool done, bool dim)
        {
            IsFixed = a.Fixed;
            SocketId = a.SocketId ?? "";
            switchedOn = a.SwitchedOn;
            powered = a.Powered;

            loadLbl.text = a.Load.ToString();
            stateLbl.text = done ? "DONE" : (a.SwitchedOn ? "ON" : "OFF");
            stateLbl.color = done ? Theme.Good : (a.SwitchedOn ? Theme.Glow : Theme.TextDim);
            tagLbl.text = a.Fixed ? "FIXED" : (a.Fragile ? "SAVE" : "");
            bg.color = dim ? Theme.ChipDim : Theme.Chip;

            bool showBar = progress01 >= 0f;
            barBg.gameObject.SetActive(showBar);
            barFill.gameObject.SetActive(showBar);
            if (showBar) barFill.rectTransform.sizeDelta = new Vector2(120f * Mathf.Clamp01(progress01), 6f);
        }

        void Update()
        {
            float a = 0f;
            if (powered) a = 1f;
            else if (switchedOn) a = ((int)(Time.unscaledTime * 4f) % 2 == 0) ? 1f : 0.15f; // กะพริบ 2 Hz
            var c = Theme.Glow; c.a = a;
            glow.color = c;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (IsFixed) return;
            if (e.button == PointerEventData.InputButton.Left) screen.Submit(Cmds.Switch(Id, !switchedOn));
            else if (e.button == PointerEventData.InputButton.Right && SocketId.Length > 0) screen.Submit(Cmds.Unplug(Id));
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (IsFixed || e.button != PointerEventData.InputButton.Left) return;
            Dragging = true;
            cg.blocksRaycasts = false;
            Rt.SetParent(screen.DragLayer, false);
            Rt.localScale = Vector3.one;
            Move(e);
        }

        public void OnDrag(PointerEventData e) { if (Dragging) Move(e); }

        public void OnEndDrag(PointerEventData e)
        {
            if (!Dragging) return;
            screen.EndDragAppliance(this, e);          // ต้องทำก่อนคืน raycast ไม่งั้นชนตัวเอง
            Dragging = false;
            cg.blocksRaycasts = true;
            Rt.SetParent(screen.ChipLayer, false);
            Rt.anchoredPosition = home;
            Rt.localScale = Vector3.one * homeScale;
        }

        void Move(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(screen.DragLayer, e.position, null, out var p);
            Rt.anchoredPosition = p + new Vector2(-70f, 55f);
        }
    }
}