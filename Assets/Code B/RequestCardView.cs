using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    public sealed class RequestCardView : MonoBehaviour
    {
        Image bg, bar;
        TextMeshProUGUI owner, what, left;
        Image urgent;
        bool blink;

        public static RequestCardView Create(Transform parent, float x, float y, string ownerName)
        {
            var bg = UIFactory.Box(parent, "Request_" + ownerName, x, y, 360, 120, Theme.Room, 14);
            var v = bg.gameObject.AddComponent<RequestCardView>();
            v.bg = bg;
            v.owner = UIFactory.Label(bg.transform, 14, 8, 190, 34, ownerName, 26, Theme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            v.left = UIFactory.Label(bg.transform, 196, 10, 150, 30, "", 22, Theme.TextDim, TextAlignmentOptions.Right);
            v.what = UIFactory.Label(bg.transform, 14, 44, 332, 28, "", 22, Theme.Text, TextAlignmentOptions.Left);
            UIFactory.Box(bg.transform, "BarBg", 14, 86, 332, 14, Theme.Border, 6);
            v.bar = UIFactory.Box(bg.transform, "Bar", 14, 86, 0, 14, Theme.Good, 6);
            v.urgent = UIFactory.Ring(bg.transform, "Urgent", 0, 0, 360, 120, Theme.LoadBad, 14, 4);
            v.urgent.gameObject.SetActive(false);
            return v;
        }

        public void Refresh(RequestState r, int now, string roomName)
        {
            what.text = ApplianceInfo.Get(r.ApplianceId).Name + " in " + roomName;
            float p = r.DurationTicks > 0 ? Mathf.Clamp01((float)r.ProgressTicks / r.DurationTicks) : 0f;
            bar.rectTransform.sizeDelta = new Vector2(332f * p, 14f);

            int leftTicks = Mathf.Max(0, r.DueTicks - now);
            if (r.Done)
            {
                left.text = "DONE";
                left.color = Theme.Good;
                bg.color = new Color(0.17f, 0.25f, 0.19f, 1f);
                blink = false;
            }
            else
            {
                int s = (leftTicks + 9) / 10;
                left.text = (s / 60) + ":" + (s % 60).ToString("00") + " left";
                left.color = Theme.TextDim;
                bg.color = Theme.Room;
                blink = leftTicks > 0 && leftTicks < 50;       // < 5 วินาที
            }
            if (!blink) urgent.gameObject.SetActive(false);
        }

        void Update()
        {
            if (blink) urgent.gameObject.SetActive(((int)(Time.unscaledTime * 4f) % 2) == 0);
        }
    }
}