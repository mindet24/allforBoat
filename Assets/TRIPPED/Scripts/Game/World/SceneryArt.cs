using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Code-painted scenery sprites in the title art's style: soft cartoon shapes with a darker rim, a light top and a
    /// shaded bottom. Each sprite is made once (on first use) and shared; pixels per unit is 100, pivot bottom-centre
    /// unless noted. Main thread only.
    /// </summary>
    /// <code>
    /// var sr = new GameObject("Tree").AddComponent<SpriteRenderer>(); sr.sprite = SceneryArt.Tree(0);
    /// </code>
    public static class SceneryArt
    {
        private const float Ppu = 100f;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

        /// <summary>A sprite painted by <paramref name="paint"/> (x, y in pixels, origin bottom-left; returns premultiplied-free RGBA).</summary>
        private static Sprite Paint(string key, int w, int h, Vector2 pivot, Func<int, int, Color> paint)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Scenery " + key };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = paint(x, y);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, Ppu, 0, SpriteMeshType.FullRect);
            s.name = key;
            Cache[key] = s;
            return s;
        }

        /// <summary>Signed distance (pixels) to a union of circles (x, y, r); negative inside.</summary>
        private static float Blobs(float x, float y, Vector3[] circles)
        {
            float d = float.MaxValue;
            foreach (var c in circles) d = Mathf.Min(d, Mathf.Sqrt((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y)) - c.z);
            return d;
        }

        /// <summary>Fill + rim + top light + bottom shade for a shape at signed distance d, height fraction t (0 bottom).</summary>
        private static Color Shade(float d, float t, Color fill, Color light, Color dark, Color rim, float rimPx)
        {
            if (d > 1f) return Color.clear;
            var c = Color.Lerp(dark, fill, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.6f)));
            c = Color.Lerp(c, light, Mathf.Clamp01((t - 0.6f) * 2f) * 0.6f);
            if (d > -rimPx) c = Color.Lerp(c, rim, Mathf.Clamp01((d + rimPx) / rimPx) * 0.85f);
            c.a = Mathf.Clamp01(0.5f - d) * fill.a;
            return c;
        }

        private static Vector3[] RandomBlobs(int seed, int count, float w, float h, float rMin, float rMax, float bottom)
        {
            var rnd = new System.Random(seed);
            var list = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float r = Mathf.Lerp(rMin, rMax, (float)rnd.NextDouble());
                float x = Mathf.Lerp(r, w - r, t) + ((float)rnd.NextDouble() - 0.5f) * r * 0.4f;
                float y = Mathf.Lerp(bottom + r, h - r, (float)(0.25 + 0.75 * rnd.NextDouble() * Mathf.Sin(t * Mathf.PI)));
                list[i] = new Vector3(x, y, r);
            }
            return list;
        }

        /// <summary>
        /// A soft, fluffy cumulus (pivot centre): no outline, its edge feathers into the sky, the top is bright white
        /// and the flat-ish underside a cool lavender-blue. <paramref name="variant"/> picks the shape.
        /// </summary>
        public static Sprite Cloud(int variant)
        {
            const int w = 384, h = 200;
            const float feather = 16f;      // pixels the edge takes to fade out
            const float baseY = 30f;        // the underside: everything below it fades away
            var blobs = RandomBlobs(11 + variant * 7, 9, w, h - feather, 30f, 62f, baseY);
            var white = Color.white;
            var under = Hex("#C8D6F4");
            var deep = Hex("#B3BEE8");
            return Paint("cloud" + variant, w, h, new Vector2(0.5f, 0.5f), (x, y) =>
            {
                float d = Blobs(x, y, blobs);
                float edge = Mathf.Clamp01((feather - d) / (2f * feather));
                float bottom = Mathf.Clamp01((y - baseY + feather) / (2f * feather));
                float a = Mathf.SmoothStep(0f, 1f, edge) * Mathf.SmoothStep(0f, 1f, bottom);
                if (a <= 0f) return Color.clear;
                // Light from above: white on top and deep inside each puff, cool shade towards the underside.
                float t = Mathf.Clamp01((y - baseY) / (h * 0.55f));
                float inner = Mathf.Clamp01(-d / 40f);
                var c = Color.Lerp(deep, under, Mathf.SmoothStep(0f, 1f, t * 1.5f));
                c = Color.Lerp(c, white, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.2f + inner * 0.5f)));
                c.a = a;
                return c;
            });
        }

        /// <summary>A leafy tree: trunk and a round canopy with leaf clusters.</summary>
        public static Sprite Tree(int variant)
        {
            const int w = 300, h = 420;
            var canopy = RandomBlobs(31 + variant * 13, 7, w, h - 6, 52f, 88f, 150f);
            var leaf = variant % 2 == 0 ? Hex("#5DAA3E") : Hex("#4E9E48");
            var leafLight = Hex("#9CD36A");
            var leafDark = Hex("#2F6B2C");
            var bark = Hex("#7A4E2D");
            return Paint("tree" + variant, w, h, new Vector2(0.5f, 0f), (x, y) =>
            {
                float d = Blobs(x, y, canopy);
                if (d <= 1f) return Shade(d, (y - 150f) / (h - 150f), leaf, leafLight, leafDark, Hex("#24502A"), 4f);
                // Trunk: a tapered column with two branches into the canopy.
                float half = Mathf.Lerp(20f, 11f, y / 230f);
                float dx = Mathf.Abs(x - w * 0.5f);
                if (y < 230 && dx < half)
                {
                    var c = Color.Lerp(Hex("#5A3820"), bark, Mathf.Clamp01((w * 0.5f + half - x) / (2f * half) + 0.2f));
                    if (dx > half - 3f) c = Hex("#3E2614");
                    return c;
                }
                return Color.clear;
            });
        }

        /// <summary>A round flowering bush.</summary>
        public static Sprite Bush(int variant)
        {
            const int w = 220, h = 120;
            var blobs = RandomBlobs(51 + variant * 5, 5, w, h, 30f, 46f, 0f);
            var flowers = new System.Random(variant + 3);
            var dots = new List<Vector3>();
            for (int i = 0; i < 9; i++) dots.Add(new Vector3(20 + (float)flowers.NextDouble() * 180, 20 + (float)flowers.NextDouble() * 80, 4.5f));
            var petal = variant % 2 == 0 ? Hex("#F48FB1") : Hex("#FFD54F");
            return Paint("bush" + variant, w, h, new Vector2(0.5f, 0f), (x, y) =>
            {
                float d = Mathf.Max(Blobs(x, y, blobs), -y);
                var c = Shade(d, y / (float)h, Hex("#4F9B3A"), Hex("#8CCB5E"), Hex("#2D6328"), Hex("#22502A"), 3f);
                if (c.a > 0.5f)
                    foreach (var f in dots)
                        if ((x - f.x) * (x - f.x) + (y - f.y) * (y - f.y) < f.z * f.z) return petal;
                return c;
            });
        }

        /// <summary>One stretch of white picket fence (tiles side by side), 3 m wide at 100 ppu → 300 px.</summary>
        public static Sprite Fence()
        {
            const int w = 300, h = 110;
            var paint = Hex("#F7F4EC");
            var shade = Hex("#CFC8B8");
            var rim = Hex("#8E8676");
            return Paint("fence", w, h, new Vector2(0f, 0f), (x, y) =>
            {
                bool rail = (y > 28 && y < 40) || (y > 70 && y < 82);
                int px = x % 30;
                bool picket = px > 6 && px < 24 && y < 100 - Mathf.Abs(px - 15) * 1.2f;
                if (!rail && !picket) return Color.clear;
                bool edge = picket && (px < 9 || px > 21) || rail && (y < 31 || y > 79 || (y > 37 && y < 40) || (y > 70 && y < 73));
                var c = Color.Lerp(shade, paint, Mathf.Clamp01(y / 90f + 0.3f));
                return edge ? rim : c;
            });
        }

        /// <summary>A simple neighbour house front: walls, roof, door and lit windows.</summary>
        public static Sprite House(int variant)
        {
            const int w = 360, h = 330;
            var wall = new[] { Hex("#F2D9B8"), Hex("#D8E4F0"), Hex("#F3E3C3") }[variant % 3];
            var roof = new[] { Hex("#7A4A3A"), Hex("#4B5A78"), Hex("#8C5A3C") }[variant % 3];
            var window = Hex("#FFE7A3");
            return Paint("house" + variant, w, h, new Vector2(0.5f, 0f), (x, y) =>
            {
                // Roof: a triangle over the walls with eaves.
                if (y >= 200)
                {
                    float halfW = (w * 0.5f + 10f) * (1f - (y - 200f) / (h - 200f));
                    float dx = Mathf.Abs(x - w * 0.5f);
                    if (dx > halfW) return Color.clear;
                    var c = Color.Lerp(roof * 0.75f, roof, (y - 200f) / (h - 200f));
                    c.a = 1f;
                    if (halfW - dx < 5f || y < 206) c = roof * 0.5f;
                    c.a = 1f;
                    return c;
                }
                if (x < 20 || x > w - 20) return Color.clear;
                var wc = Color.Lerp(wall * 0.85f, wall, y / 200f);
                wc.a = 1f;
                if (x < 24 || x > w - 24) return wall * 0.6f + new Color(0, 0, 0, 0.4f);
                // Door and windows.
                if (x > 155 && x < 205 && y < 95) return x < 159 || x > 201 || y > 91 ? Hex("#4A2E1E") : Hex("#8B5A3C");
                foreach (int wx in new[] { 70, 250 })
                    foreach (int wy in new[] { 50, 130 })
                        if (x > wx && x < wx + 46 && y > wy && y < wy + 40)
                            return x < wx + 4 || x > wx + 42 || y < wy + 4 || y > wy + 36 || Mathf.Abs(x - wx - 23) < 2 ? Hex("#FFFFFF") : window;
                return wc;
            });
        }

        /// <summary>A wooden utility pole with a cross arm (pivot bottom-centre; top of the arm at 95% height).</summary>
        public static Sprite Pole()
        {
            const int w = 120, h = 700;
            return Paint("pole", w, h, new Vector2(0.5f, 0f), (x, y) =>
            {
                float dx = Mathf.Abs(x - w * 0.5f);
                bool arm = y > 630 && y < 650 && dx < 56;
                bool post = dx < 9 && y < 690;
                bool insulator = y >= 650 && y < 668 && (Mathf.Abs(x - 12) < 5 || Mathf.Abs(x - 108) < 5);
                if (insulator) return Hex("#DDE6EE");
                if (!arm && !post) return Color.clear;
                return (post && dx > 6) || (arm && (y < 633 || y > 647)) ? Hex("#4B3221") : Hex("#7B5638");
            });
        }

        /// <summary>A red mailbox on a post.</summary>
        public static Sprite Mailbox()
        {
            const int w = 80, h = 130;
            return Paint("mailbox", w, h, new Vector2(0.5f, 0f), (x, y) =>
            {
                if (Mathf.Abs(x - 40) < 5 && y < 90) return Hex("#6B4428");
                float dx = Mathf.Abs(x - 40);
                if (y >= 86 && y < 126 && dx < 30)
                {
                    bool rim = y < 89 || dx > 27 || (y > 112 && (x - 40) * (x - 40) / 900f + (y - 112) * (y - 112) / 196f > 0.82f);
                    if (y > 112 && (x - 40) * (x - 40) / 900f + (y - 112) * (y - 112) / 196f > 1f) return Color.clear;
                    return rim ? Hex("#7E1A1A") : Hex("#D93A33");
                }
                if (x > 64 && x < 70 && y > 100 && y < 128) return Hex("#F6C343");
                return Color.clear;
            });
        }

        /// <summary>A rolling hill band (tiles in x): a wavy top edge with a lighter crest, fading darker below.</summary>
        public static Sprite Hill(int variant, Color top, Color bottom)
        {
            const int w = 512, h = 160;
            float phase = variant * 1.7f;
            return Paint("hill" + variant, w, h, new Vector2(0f, 0f), (x, y) =>
            {
                float t = x / (float)w * Mathf.PI * 2f;
                float edge = h * 0.62f + Mathf.Sin(t + phase) * 22f + Mathf.Sin(t * 2f + phase * 2f) * 12f + Mathf.Sin(t * 5f) * 4f;
                if (y > edge + 1f) return Color.clear;
                var c = Color.Lerp(bottom, top, y / edge);
                if (y > edge - 6f) c = Color.Lerp(c, Color.white, 0.18f);
                c.a = Mathf.Clamp01(edge + 1f - y);
                return c;
            });
        }

        /// <summary>The sun: a warm disc in a soft glow (pivot centre).</summary>
        public static Sprite Sun()
        {
            const int s = 256;
            return Paint("sun", s, s, new Vector2(0.5f, 0.5f), (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.5f);
                if (d < 0.32f) return Color.Lerp(Hex("#FFF6C8"), Hex("#FFE27A"), d / 0.32f);
                var glow = Hex("#FFF3B0");
                glow.a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f) * 0.55f;
                return glow;
            });
        }

        /// <summary>A small bird: two curved wings (pivot centre). Flap by scaling y.</summary>
        public static Sprite Bird()
        {
            const int w = 64, h = 32;
            return Paint("bird", w, h, new Vector2(0.5f, 0.5f), (x, y) =>
            {
                float u = (x - 32f) / 30f;
                float wing = 14f + 12f * (1f - Mathf.Cos(Mathf.Abs(u) * Mathf.PI)) * 0.5f;
                return Mathf.Abs(u) <= 1f && Mathf.Abs(y - wing) < 2.2f - Mathf.Abs(u) ? Hex("#2E3A4E") : Color.clear;
            });
        }

        /// <summary>A soft round puff (smoke, dust), white, alpha falling to the edge (pivot centre).</summary>
        public static Sprite Puff()
        {
            const int s = 64;
            return Paint("puff", s, s, new Vector2(0.5f, 0.5f), (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.5f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * 0.9f);
            });
        }

        /// <summary>A grass strip (tiles in x) with tufts along its top edge.</summary>
        public static Sprite Grass()
        {
            const int w = 256, h = 64;
            return Paint("grass", w, h, new Vector2(0f, 0f), (x, y) =>
            {
                float tuft = 40f + Mathf.Abs(Mathf.Sin(x * 0.21f)) * 10f + Mathf.Abs(Mathf.Sin(x * 0.53f + 1f)) * 7f;
                if (y > tuft) return Color.clear;
                var c = Color.Lerp(Hex("#4C9A34"), Hex("#86C850"), y / tuft);
                if (y > tuft - 3f) c = Hex("#A6DB6E");
                return c;
            });
        }

        /// <summary>Soil (tiles both ways): brown with darker strata and scattered stones.</summary>
        public static Sprite Soil()
        {
            const int s = 256;
            var rnd = new System.Random(5);
            var stones = new List<Vector3>();
            for (int i = 0; i < 26; i++) stones.Add(new Vector3((float)rnd.NextDouble() * s, (float)rnd.NextDouble() * s, 4f + (float)rnd.NextDouble() * 9f));
            return Paint("soil", s, s, new Vector2(0f, 0f), (x, y) =>
            {
                foreach (var st in stones)
                {
                    float dx = Mathf.Repeat(x - st.x + s * 0.5f, s) - s * 0.5f, dy = Mathf.Repeat(y - st.y + s * 0.5f, s) - s * 0.5f;
                    float d = Mathf.Sqrt(dx * dx * 0.7f + dy * dy) - st.z;
                    if (d < 0f) return d > -2f ? Hex("#5E3A22") : Color.Lerp(Hex("#8E6A50"), Hex("#B08A6A"), (dy + st.z) / (2f * st.z));
                }
                float n = Mathf.Sin(y * 0.09f + Mathf.Sin(x * 0.03f) * 1.5f) * 0.5f + 0.5f;
                return Color.Lerp(Hex("#7A4A2A"), Hex("#8B5733"), n);
            });
        }
    }
}
