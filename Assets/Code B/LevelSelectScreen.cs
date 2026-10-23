using TMPro;
using UnityEngine;

namespace Trouble.Game
{
    public static class LevelSelectScreen
    {
        public static void Build(GameRoot root, RectTransform sr)
        {
            UIFactory.Box(sr, "BG", 0, 0, 1920, 1080, Theme.Bg);
            UIFactory.Label(sr, 0, 120, 1920, 80, "Choose a level", 56, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);

            int unlocked = Progress.Unlocked;
            for (int n = 1; n <= GameRoot.LevelCount; n++)
            {
                int level = n;
                var d = LevelLoader.LoadRaw(n);
                float x = 150f + (n - 1) * 330f;
                var btn = UIFactory.MakeButton(sr, x, 300, 300, 360, "", () => root.ShowLevel(level), 20);
                btn.interactable = n <= unlocked;
                var t = btn.transform;
                UIFactory.Label(t, 0, 20, 300, 110, n.ToString(), 96, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Label(t, 10, 150, 280, 90, d != null ? d.title : "Level " + n, 30, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Label(t, 16, 245, 268, 100, d != null ? d.teaches : "", 22, Theme.TextDim, TextAlignmentOptions.Top);
            }

            UIFactory.MakeButton(sr, 810, 800, 300, 70, "Back (Esc)", root.ShowTitle, 28);
            root.EscAction = root.ShowTitle;
        }
    }
}