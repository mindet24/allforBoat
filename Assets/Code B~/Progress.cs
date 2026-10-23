using UnityEngine;

namespace Trouble.Game
{
    public static class Progress
    {
        const string KeyUnlocked = "trouble.unlocked";

        public static int Unlocked => Mathf.Clamp(PlayerPrefs.GetInt(KeyUnlocked, 1), 1, GameRoot.LevelCount);

        public static void Unlock(int level)
        {
            level = Mathf.Clamp(level, 1, GameRoot.LevelCount);
            if (level <= Unlocked) return;
            PlayerPrefs.SetInt(KeyUnlocked, level);
            PlayerPrefs.Save();
        }

        public static void SaveTime(int level, float sec)
        {
            PlayerPrefs.SetFloat("trouble.time." + level, sec);
            PlayerPrefs.Save();
        }

        public static float TotalTime()
        {
            float t = 0f;
            for (int i = 1; i <= GameRoot.LevelCount; i++) t += PlayerPrefs.GetFloat("trouble.time." + i, 0f);
            return t;
        }

        public static void ResetAll() { PlayerPrefs.DeleteAll(); }
    }
}