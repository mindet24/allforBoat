using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Trouble.Game
{
    public sealed class BreakerView : MonoBehaviour, IPointerClickHandler
    {
        const float BarW = 332f;
        string id;
        LevelScreen screen;
        Image bg, fill;
        TextMeshProUGUI idLbl, labelLbl, valueLbl, lever, trip;
        Color barColor;
        int over;

        public static BreakerView Create(LevelScreen screen, Transform parent, float x, float y, string id)
        {
            var bg = UIFactory.Box(parent, "Breaker_" + id, x, y, 360, 150, Theme.Room, 14, true);
            var v = bg.gameObject.AddComponent<BreakerView>();
            v.screen = screen; v.id = id; v.bg = bg;
            v.idLbl = UIFactory.Label(bg.transform, 14, 8, 64, 64, id, 48, Theme.Circuit(id), TextAlignmentOptions.Center, FontStyles.Bold);
            v.labelLbl = UIFactory.Label(bg.transform, 80, 14, 118, 30, "", 20, Theme.TextDim, TextAlignmentOptions.Left);
            v.valueLbl = UIFactory.Label(bg.transform, 196, 8, 150, 48, "0 / 0", 36, Theme.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            UIFactory.Box(bg.transform, "BarBg", 14, 78, BarW, 28, Theme.Border, 8);
            v.fill = UIFactory.Box(bg.transform, "BarFill", 14, 78, 0, 28, Theme.LoadOk, 8);
            var pill = UIFactory.Box(bg.transform, "Lever", 14, 114, 100, 28, Theme.ChipDim, 10);
            v.lever = UIFactory.Label(pill.transform, 0, 0, 100, 28, "ON", 20, Theme.Good, TextAlignmentOptions.Center, FontStyles.Bold);
            v.trip = UIFactory.Label(bg.transform, 124, 114, 222, 28, "", 20, Theme.LoadBad, TextAlignmentOptions.Right, FontStyles.Bold);
            return v;
        }

        public void Refresh(CircuitState c)
        {
            over = c.OverTicks;
            idLbl.text = c.Id;
            labelLbl.text = c.ShownLabel;
            valueLbl.text = c.Load + " / " + c.Capacity;
            float r = c.Capacity > 0 ? (float)c.Load / c.Capacity : 0f;
            fill.rectTransform.sizeDelta = new Vector2(BarW * Mathf.Clamp01(r), 28f);
            barColor = c.IsOn ? Theme.LoadColor(c.Load, c.Capacity) : Theme.Unknown;
            bg.color = c.IsOn ? Theme.Room : Theme.ChipDim;
            lever.text = c.IsOn ? "ON" : "OFF";
            lever.color = c.IsOn ? Theme.Good : Theme.LoadBad;
            trip.text = (c.IsOn && c.OverTicks > 0) ? "TRIP in " + ((10 - c.OverTicks) / 10f).ToString("0.0") + "s" : "";
        }

        void Update()
        {
            var c = barColor;
            if (over > 0) c = ((int)(Time.unscaledTime * 8f) % 2 == 0) ? Theme.LoadBad : Theme.LoadBad * 0.5f; // กะพริบ 4 Hz
            fill.color = c;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) screen.Submit(Cmds.Breaker(id));
        }
    }
}