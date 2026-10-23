using UnityEngine;

namespace Trouble.Game
{
    public static class Theme
    {
        public static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        public static readonly Color Bg = H("#1E1E24"), Room = H("#2B2B33"), Border = H("#44444F"),
            Text = H("#F5F5F5"), TextDim = H("#A0A0AA"), Unknown = H("#9E9E9E"),
            LoadOk = H("#4CAF50"), LoadWarn = H("#FFB300"), LoadBad = H("#E53935"),
            Glow = H("#FFE066"), Good = H("#27AE60"),
            Panel = H("#25252C"), Chip = H("#3C3C48"), ChipDim = H("#26262E");

        public static Color Circuit(string id)
        {
            switch (id)
            {
                case "A": return H("#F2994A");
                case "B": return H("#2D9CDB");
                case "C": return H("#27AE60");
                case "D": return H("#9B51E0");
                default: return Unknown;
            }
        }

        public static Color LoadColor(int load, int cap)
        {
            float r = cap > 0 ? (float)load / cap : 0f;
            if (r > 1f) return LoadBad;
            return r > 0.7f ? LoadWarn : LoadOk;
        }
    }
}