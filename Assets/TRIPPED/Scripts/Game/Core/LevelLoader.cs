using System;
using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Reads the shared config (rules, catalog, strings) and level files from Resources with JsonUtility
    /// (GDD v1.1 §4.2) and builds a <see cref="HouseSim"/>. Levels are checked with <see cref="LevelValidator"/>.
    /// </summary>
    public sealed class LevelLoader
    {
        public const string RulesPath = "Config/rules";
        public const string CatalogPath = "Config/catalog";
        public const string StringsPath = "Config/strings";
        public const string LevelPathFormat = "Levels/level{0}";
        /// <summary>Level number of the Day-mode demo "One Busy Day" (file level_demo.json).</summary>
        public const int DemoLevel = 100;
        public const string DemoPath = "Levels/level_demo";

        /// <summary>Resources path of level n (the demo has its own file name).</summary>
        public static string PathFor(int n) => n == DemoLevel ? DemoPath : string.Format(LevelPathFormat, n);

        /// <summary>Global rules (before a level's overrides).</summary>
        public RulesConfig Rules { get; }
        public Catalog Catalog { get; }
        public Strings Strings { get; }

        /// <summary>Loads rules, catalog and strings. Missing files fall back to defaults and are logged.</summary>
        public LevelLoader()
        {
            Rules = Load<RulesConfig>(RulesPath) ?? new RulesConfig();
            Catalog = new Catalog(Load<CatalogData>(CatalogPath) ?? new CatalogData());
            Strings = new Strings(Load<StringsData>(StringsPath) ?? new StringsData());
        }

        /// <summary>
        /// Debug (F1 panel): rulesOverride lists that replace the ones in a level's file on the next
        /// <see cref="Build"/>, keyed by level number. Empty in normal play.
        /// </summary>
        public Dictionary<int, List<RuleOverrideData>> OverrideReplacements { get; } = new Dictionary<int, List<RuleOverrideData>>();

        /// <summary>Reads level{n}.json without validating or building it (for titles in menus). Null if missing.</summary>
        public static LevelData ReadLevel(int n) => n > 0 ? Load<LevelData>(PathFor(n)) : null;

        /// <summary>True if Resources holds level{n}.json.</summary>
        public static bool Exists(int n) => n > 0 && Resources.Load<TextAsset>(PathFor(n)) != null;

        /// <summary>
        /// Builds a fresh sim for level n. Returns null and fills <paramref name="errors"/> if the file is missing,
        /// unreadable or fails validation.
        /// </summary>
        public HouseSim Build(int n, List<string> errors)
        {
            var level = Load<LevelData>(PathFor(n));
            if (level == null)
            {
                errors.Add($"Level {n} was not found in Resources/Levels.");
                return null;
            }
            // Day mode with seed 0: roll a fresh non-zero seed for this run (the level is a fresh copy from JSON).
            if (level.day != null && level.day.enabled && level.day.seed == 0)
                level.day.seed = (Environment.TickCount & 0x7FFFFFFF) | 1;
            if (OverrideReplacements.TryGetValue(n, out var overrides))
                level.rulesOverride = new List<RuleOverrideData>(overrides);
            errors.AddRange(LevelValidator.Validate(level, Catalog, Rules));
            if (errors.Count > 0) return null;
            try
            {
                return new HouseSim(level, Rules, Catalog, Strings);
            }
            catch (ArgumentException e)
            {
                errors.Add(e.Message);
                return null;
            }
        }

        private static T Load<T>(string path) where T : class
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogError($"[TRIPPED] Missing Resources/{path}.json");
                return null;
            }
            try
            {
                return JsonUtility.FromJson<T>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TRIPPED] Cannot read Resources/{path}.json: {e.Message}");
                return null;
            }
        }
    }
}
