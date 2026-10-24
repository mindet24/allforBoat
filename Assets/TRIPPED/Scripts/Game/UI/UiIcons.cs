using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The art sheet's icon set drawn in code (crisp, no pixels cut from the reference): heart, coin, star, lightning,
    /// plug, hourglass, plus the menu icons (play, gear, book, power), a clock, a warning triangle, a paint-brush stripe
    /// and a curly cord. Coloured icons get a navy outline like the sheet; menu icons are plain white. Made once.
    /// </summary>
    public static class UiIcons
    {
        private const int Size = 128;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static Color _outline = TrippedViewConfig.Hex("#1F2A44");

        /// <summary>Uses the config's outline colour for icons made after this call.</summary>
        public static void Configure(TrippedUiConfig ui)
        {
            if (ui != null) _outline = ui.iconOutline;
        }

        public static Sprite Heart => Get("heart", () => Make(HeartMask, Hex("#F0507A"), Color.white, true));
        public static Sprite Coin => Get("coin", () => Make(CoinMask, Hex("#F6C933"), Hex("#E09A1C"), true));
        public static Sprite Star => Get("star", () => Make(StarMask, Hex("#F6C933"), Hex("#FFE58A"), true));
        public static Sprite StarEmpty => Get("starEmpty", () => Make(StarMask, Hex("#D9CDB6"), Hex("#E6DCC8"), true));
        public static Sprite Lightning => Get("bolt", () => Make(BoltMask, Hex("#F6C933"), Hex("#FFE58A"), true));
        public static Sprite Plug => Get("plug", () => Make(PlugMask, Hex("#3FA2F6"), Hex("#9FD3FF"), true));
        public static Sprite Hourglass => Get("hourglass", () => Make(HourglassMask, Hex("#8E5BD9"), Hex("#F6C933"), true));
        public static Sprite Clock => Get("clock", () => Make(ClockMask, Color.white, Hex("#E8423F"), true));
        public static Sprite Warning => Get("warning", () => Make(WarningMask, Color.white, Hex("#E8423F"), false));
        public static Sprite Play => Get("play", () => Make(PlayMask, Hex("#1F2A44"), Hex("#1F2A44"), false));
        public static Sprite Gear => Get("gear", () => Make(GearMask, Color.white, Color.white, false));
        public static Sprite Book => Get("book", () => Make(BookMask, Color.white, Hex("#1F2A44"), false));
        public static Sprite Power => Get("power", () => Make(PowerMask, Color.white, Color.white, false));
        public static Sprite House => Get("house", () => Make(HouseMask, Color.white, Color.white, false));

        /// <summary>A rough horizontal paint-brush stripe (white, tint it), 512×128, 9-slice-free: stretch it.</summary>
        public static Sprite Brush => Get("brush", MakeBrush);

        /// <summary>A wavy cord line (white), 512×96.</summary>
        public static Sprite Cord => Get("cord", MakeCord);

        private static Sprite Get(string key, Func<Sprite> make)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            s = make();
            Cache[key] = s;
            return s;
        }

        private static Color Hex(string h) => TrippedViewConfig.Hex(h);

        // ---- masks: u, v in -1..1 (v up); 0 = empty, 1 = fill, 2 = detail ---------------------------------

        private static int HeartMask(float u, float v)
        {
            float x = u * 1.25f, y = (v + 0.12f) * 1.25f;
            float f = Mathf.Pow(x * x + y * y - 1f, 3f) - x * x * y * y * y;
            if (f > 0f) return 0;
            float hx = u + 0.38f, hy = v - 0.3f;
            return hx * hx * 3f + hy * hy * 6f < 0.07f ? 2 : 1;
        }

        private static int CoinMask(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            if (r > 0.86f) return 0;
            if (r > 0.66f) return 1;
            // A bolt on the coin face in the darker colour.
            return InPoly(u, v, BoltPoly, 0.5f) ? 2 : 1;
        }

        private static readonly Vector2[] StarPoly = StarPoints(0.88f, 0.4f);

        private static int StarMask(float u, float v)
        {
            if (!InPoly(u, v + 0.05f, StarPoly, 1f)) return 0;
            float hx = u + 0.2f, hy = v - 0.15f;
            return hx * hx + hy * hy * 3f < 0.04f ? 2 : 1;
        }

        private static readonly Vector2[] BoltPoly =
        {
            new Vector2(0.2f, 0.95f), new Vector2(-0.5f, -0.05f), new Vector2(-0.02f, -0.05f), new Vector2(-0.25f, -0.95f),
            new Vector2(0.55f, 0.15f), new Vector2(0.06f, 0.15f), new Vector2(0.35f, 0.95f),
        };

        private static int BoltMask(float u, float v)
        {
            if (!InPoly(u, v, BoltPoly, 0.92f)) return 0;
            return u < -0.05f && v > -0.05f && u > -0.35f && v < 0.3f && u - v * 0.7f < -0.2f ? 2 : 1;
        }

        private static int PlugMask(float u, float v)
        {
            bool body = Mathf.Abs(u) < 0.42f && v > -0.25f && v < 0.38f && RoundBox(u, v - 0.06f, 0.42f, 0.32f, 0.14f);
            bool prongs = (Mathf.Abs(u - 0.2f) < 0.08f || Mathf.Abs(u + 0.2f) < 0.08f) && v >= 0.38f && v < 0.85f;
            bool neck = Mathf.Abs(u) < 0.22f && v <= -0.25f && v > -0.45f;
            bool cord = Mathf.Abs(u) < 0.08f && v <= -0.45f && v > -0.92f;
            if (!(body || prongs || neck || cord)) return 0;
            return body && u < -0.2f && v > 0.0f ? 2 : 1;
        }

        private static int HourglassMask(float u, float v)
        {
            bool cap = Mathf.Abs(Mathf.Abs(v) - 0.82f) < 0.11f && Mathf.Abs(u) < 0.66f;
            float halfW = 0.1f + Mathf.Abs(v) * 0.62f;
            bool glass = Mathf.Abs(v) < 0.72f && Mathf.Abs(u) < halfW;
            if (cap) return 1;
            if (!glass) return 0;
            bool sand = (v < -0.25f && Mathf.Abs(u) < halfW - 0.12f) || (v > 0.25f && v < 0.5f && Mathf.Abs(u) < halfW - 0.14f);
            return sand ? 2 : 1;
        }

        private static int ClockMask(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            if (r > 0.86f) return 0;
            bool hands = (Mathf.Abs(u) < 0.07f && v > -0.05f && v < 0.55f) || (Mathf.Abs(v) < 0.07f && u > -0.05f && u < 0.42f);
            return hands || r > 0.74f ? 2 : 1;
        }

        private static readonly Vector2[] Triangle = { new Vector2(0f, 0.9f), new Vector2(-0.95f, -0.78f), new Vector2(0.95f, -0.78f) };

        private static int WarningMask(float u, float v)
        {
            if (!InPoly(u, v, Triangle, 1f)) return 0;
            bool bar = Mathf.Abs(u) < 0.09f && v > -0.25f && v < 0.45f;
            bool dot = u * u + (v + 0.5f) * (v + 0.5f) < 0.012f;
            return bar || dot ? 2 : 1;
        }

        private static readonly Vector2[] PlayPoly = { new Vector2(-0.6f, 0.8f), new Vector2(-0.6f, -0.8f), new Vector2(0.8f, 0f) };

        private static int PlayMask(float u, float v) => InPoly(u, v, PlayPoly, 1f) ? 1 : 0;

        private static int GearMask(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Atan2(v, u);
            float tooth = Mathf.Cos(a * 8f) > 0.25f ? 0.88f : 0.68f;
            return r < tooth && r > 0.28f ? 1 : 0;
        }

        private static int BookMask(float u, float v)
        {
            float top = 0.55f - Mathf.Abs(u) * 0.12f + (Mathf.Abs(u) < 0.06f ? -0.08f : 0f);
            float bottom = -0.6f + Mathf.Abs(u) * 0.08f;
            if (Mathf.Abs(u) > 0.9f || v > top || v < bottom) return 0;
            if (Mathf.Abs(u) < 0.04f) return 0; // spine gap
            // Page lines in the dark colour.
            float lv = (v - bottom) / (top - bottom);
            bool line = Mathf.Abs(u) > 0.18f && Mathf.Abs(u) < 0.75f && lv > 0.25f && lv < 0.85f && Mathf.Repeat(lv * 5f, 1f) < 0.16f;
            return line ? 2 : 1;
        }

        private static int PowerMask(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Atan2(u, v) * Mathf.Rad2Deg; // 0 = up
            bool ring = r > 0.54f && r < 0.78f && Mathf.Abs(a) > 34f;
            bool bar = Mathf.Abs(u) < 0.12f && v > 0.0f && v < 0.92f;
            return ring || bar ? 1 : 0;
        }

        private static readonly Vector2[] Roof = { new Vector2(0f, 0.9f), new Vector2(-0.9f, 0.05f), new Vector2(0.9f, 0.05f) };

        private static int HouseMask(float u, float v)
        {
            bool roof = InPoly(u, v, Roof, 1f) && !InPoly(u, v + 0.16f, Roof, 0.72f);
            bool walls = Mathf.Abs(u) < 0.66f && v < 0.12f && v > -0.85f && !(Mathf.Abs(u) < 0.5f && v < 0.02f && v > -0.72f);
            bool door = Mathf.Abs(u) < 0.14f && v < -0.35f && v > -0.85f;
            return roof || walls || door ? 1 : 0;
        }

        // ---- drawing --------------------------------------------------------------------------------------

        private static Sprite Make(Func<float, float, int> mask, Color fill, Color detail, bool outline)
        {
            int n = Size;
            var region = new int[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    // Shrink a little so the outline fits.
                    region[y * n + x] = mask(u / 0.88f, v / 0.88f);
                }
            var px = new Color32[n * n];
            int r = outline ? 6 : 0;
            bool[] edge = outline ? Dilate(region, n, r) : null;
            for (int i = 0; i < n * n; i++)
            {
                int k = region[i];
                Color c = k == 0 ? (outline && edge[i] ? _outline : new Color(0, 0, 0, 0)) : (k == 2 ? detail : fill);
                px[i] = c;
            }
            return ToSprite(px, n, n, "UiIcon");
        }

        /// <summary>Pixels within r of a filled pixel (separable square dilation).</summary>
        private static bool[] Dilate(int[] region, int n, int r)
        {
            var row = new bool[n * n];
            for (int y = 0; y < n; y++)
            {
                // Two passes: distance from the nearest filled pixel to the left, then to the right.
                int dist = 10000;
                for (int x = 0; x < n; x++) { dist = region[y * n + x] != 0 ? 0 : dist + 1; row[y * n + x] = dist <= r; }
                dist = 10000;
                for (int x = n - 1; x >= 0; x--) { dist = region[y * n + x] != 0 ? 0 : dist + 1; if (dist <= r) row[y * n + x] = true; }
            }
            var outp = new bool[n * n];
            for (int x = 0; x < n; x++)
            {
                int dist = 10000;
                for (int y = 0; y < n; y++) { dist = row[y * n + x] ? 0 : dist + 1; outp[y * n + x] = dist <= r; }
                dist = 10000;
                for (int y = n - 1; y >= 0; y--) { dist = row[y * n + x] ? 0 : dist + 1; if (dist <= r) outp[y * n + x] = true; }
            }
            return outp;
        }

        private static Sprite MakeBrush()
        {
            const int w = 512, h = 128;
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float t = (float)x / (w - 1);
                // Tapered, wobbly ends; ragged top and bottom edges like a dry brush.
                float taper = Mathf.Clamp01(Mathf.Min(t, 1f - t) * 9f);
                float top = 0.5f + 0.38f * taper + 0.05f * Mathf.Sin(t * 37f) + 0.03f * Mathf.Sin(t * 91f + 1f);
                float bottom = 0.5f - 0.36f * taper + 0.04f * Mathf.Sin(t * 29f + 2f) + 0.03f * Mathf.Sin(t * 113f);
                for (int y = 0; y < h; y++)
                {
                    float s = (y + 0.5f) / h;
                    float a = Mathf.Clamp01((top - s) * h * 0.5f) * Mathf.Clamp01((s - bottom) * h * 0.5f);
                    // Bristle streaks.
                    float streak = 0.88f + 0.12f * Mathf.PerlinNoise(t * 6f, s * 40f);
                    px[y * w + x] = new Color(1f, 1f, 1f, a * streak);
                }
            }
            return ToSprite(px, w, h, "UiBrush");
        }

        private static Sprite MakeCord()
        {
            const int w = 512, h = 96;
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float t = (float)x / (w - 1);
                float cy = 0.5f + 0.28f * Mathf.Sin(t * Mathf.PI * 2.2f) * (1f - t * 0.5f);
                for (int y = 0; y < h; y++)
                {
                    float d = Mathf.Abs((y + 0.5f) / h - cy) * h;
                    float a = Mathf.Clamp01(4.5f - d);
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            return ToSprite(px, w, h, "UiCord");
        }

        private static Sprite ToSprite(Color32[] px, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Vector2[] StarPoints(float outer, float inner)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return pts;
        }

        private static bool RoundBox(float u, float v, float hw, float hh, float rad)
        {
            float qx = Mathf.Abs(u) - (hw - rad), qy = Mathf.Abs(v) - (hh - rad);
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - rad <= 0f;
        }

        /// <summary>Point-in-polygon (even-odd) for the polygon scaled by <paramref name="scale"/>.</summary>
        public static bool InPoly(float u, float v, Vector2[] poly, float scale)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[i] * scale;
                var b = poly[j] * scale;
                if ((a.y > v) != (b.y > v) && u < (b.x - a.x) * (v - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
    }
}
