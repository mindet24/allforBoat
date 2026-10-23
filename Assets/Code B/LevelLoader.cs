using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    public static class LevelLoader
    {
        public static LevelData LoadRaw(int n)
        {
            var ta = Resources.Load<TextAsset>("Levels/level" + n);
            if (ta == null) return null;
            try { return JsonUtility.FromJson<LevelData>(ta.text); }
            catch (System.Exception) { return null; }
        }

        public static LevelData Load(int n, out List<string> errors)
        {
            errors = new List<string>();
            var d = LoadRaw(n);
            if (d == null) { errors.Add("Cannot read Resources/Levels/level" + n + ".json"); return null; }

            // ถ้า A ยังไม่ส่ง LevelValidator ให้ comment 3 บรรทัดนี้ไว้ก่อน
            IEnumerable<string> v = LevelValidator.Validate(d);
            if (v != null) foreach (var e in v) errors.Add(e);
            return d;
        }
    }
}