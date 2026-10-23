using UnityEngine;

namespace Trouble.Game
{
    /// <summary>Where <see cref="Progress"/> keeps its numbers. PlayerPrefs in the game, a dictionary in tests.</summary>
    public interface IProgressStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        void Save();
    }

    /// <summary><see cref="IProgressStore"/> on <see cref="PlayerPrefs"/>.</summary>
    public sealed class PlayerPrefsProgressStore : IProgressStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>
    /// Which levels the player may open, and the best winning time of each (GDD v1.0 §6.1): level 1 is always
    /// open; winning level n opens level n+1. Stored under "trouble.unlocked" (int, starts at 1) and
    /// "trouble.best.{n}" (ticks, -1 = never won).
    /// </summary>
    public sealed class Progress
    {
        /// <summary>PlayerPrefs key of the highest unlocked level.</summary>
        public const string UnlockedKey = "trouble.unlocked";

        /// <summary>PlayerPrefs key format of a level's best winning time in ticks.</summary>
        public const string BestKeyFormat = "trouble.best.{0}";

        private readonly IProgressStore _store;

        public Progress(IProgressStore store)
        {
            _store = store;
        }

        /// <summary>Highest level the player may open (at least 1).</summary>
        public int Unlocked => Mathf.Max(1, _store.GetInt(UnlockedKey, 1));

        /// <summary>True if level n may be opened.</summary>
        public bool IsUnlocked(int level) => level >= 1 && level <= Unlocked;

        /// <summary>Best winning time of a level in ticks, or -1 if it was never won.</summary>
        public int BestTicks(int level) => _store.GetInt(string.Format(BestKeyFormat, level), -1);

        /// <summary>Records a win: opens the next level and keeps the time if it is the best so far.</summary>
        public void RecordWin(int level, int ticks)
        {
            if (level < 1) return;
            if (level + 1 > Unlocked) _store.SetInt(UnlockedKey, level + 1);
            int best = BestTicks(level);
            if (best < 0 || ticks < best) _store.SetInt(string.Format(BestKeyFormat, level), ticks);
            _store.Save();
        }

        /// <summary>Sum of the best times of levels 1..<paramref name="levelCount"/>; levels never won count 0.</summary>
        public int TotalBestTicks(int levelCount)
        {
            int total = 0;
            for (int n = 1; n <= levelCount; n++) total += Mathf.Max(0, BestTicks(n));
            return total;
        }

        /// <summary>Sets the highest unlocked level (debug and tests). Values below 1 become 1.</summary>
        public void SetUnlocked(int level)
        {
            _store.SetInt(UnlockedKey, Mathf.Max(1, level));
            _store.Save();
        }
    }
}
