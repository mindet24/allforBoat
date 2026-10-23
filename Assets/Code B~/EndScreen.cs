using TMPro;
using UnityEngine;

namespace Trouble.Game
{
    public static class EndScreen
    {
        public static void Build(GameRoot root, RectTransform sr)
        {
            UIFactory.Box(sr, "BG", 0, 0, 1920, 1080, Theme.Bg);
            int s = Mathf.RoundToInt(Progress.TotalTime());
            UIFactory.Label(sr, 0, 280, 1920, 120, "The house survived.", 84, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Label(sr, 0, 430, 1920, 60, "Total time  " + (s / 60) + ":" + (s % 60).ToString("00"), 40, Theme.Glow);
            UIFactory.Label(sr, 0, 540, 1920, 40, "TRIPPED!  -  made by <team name>", 26, Theme.TextDim);
            UIFactory.MakeButton(sr, 810, 680, 300, 80, "Levels", root.ShowLevelSelect, 34);
            root.EscAction = root.ShowLevelSelect;
        }
    }
}