using TMPro;
using UnityEngine;

namespace Trouble.Game
{
    public static class TitleScreen
    {
        public static void Build(GameRoot root, RectTransform sr)
        {
            UIFactory.Box(sr, "BG", 0, 0, 1920, 1080, Theme.Bg);
            UIFactory.Label(sr, 0, 220, 1920, 160, "TRIPPED!", 120, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Label(sr, 0, 400, 1920, 50, "Get everyone's requests done without wrecking the house.", 32, Theme.TextDim);
            UIFactory.MakeButton(sr, 810, 560, 300, 80, "Play", root.ShowLevelSelect, 36);
#if !UNITY_WEBGL
            UIFactory.MakeButton(sr, 810, 670, 300, 80, "Quit", Application.Quit, 36);
#endif
        }
    }
}