using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Code-generated UI sprites for the G6 HUD and menus: chunky rounded panels, a coin, a star, an hourglass, a
    /// vertical backdrop gradient and an arrow. White (tinted by the Image) unless stated. Made once and shared.
    /// </summary>
    public static class UiArt
    {
        private static Sprite _rounded;
        private static Sprite _roundedOutline;
        private static Sprite _coin;
        private static Sprite _star;
        private static Sprite _hourglass;
        private static Sprite _arrow;
        private static Sprite _pill;
        private static int _radius = 30;

        /// <summary>Sets the corner radius used by sprites made after this call (from <see cref="TrippedUiConfig.cornerRadius"/>).</summary>
        public static void Configure(TrippedUiConfig ui)
        {
            if (ui == null || ui.cornerRadius == _radius) return;
            _radius = Mathf.Clamp(ui.cornerRadius, 4, 60);
            _rounded = null;
            _roundedOutline = null;
        }

        /// <summary>Rounded rect, 9-sliced, big corners.</summary>
        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = MakeRounded(128, _radius, false, 0));

        /// <summary>Rounded rect outline (6 px), 9-sliced, for focus rings and ticket borders.</summary>
        public static Sprite RoundedOutline => _roundedOutline != null ? _roundedOutline : (_roundedOutline = MakeRounded(128, _radius, true, 7));

        /// <summary>Fully rounded ends (pill), 9-sliced.</summary>
        public static Sprite Pill => _pill != null ? _pill : (_pill = MakeRounded(64, 31, false, 0));

        /// <summary>Gold coin with a rim and a lightning mark, already coloured.</summary>
        public static Sprite Coin => _coin != null ? _coin : (_coin = MakeCoin());

        /// <summary>Five-point star, white.</summary>
        public static Sprite Star => _star != null ? _star : (_star = MakeStar());

        /// <summary>Hourglass outline, white.</summary>
        public static Sprite Hourglass => _hourglass != null ? _hourglass : (_hourglass = MakeHourglass());

        /// <summary>Chunky arrow pointing right with a notch, white.</summary>
        public static Sprite Arrow => _arrow != null ? _arrow : (_arrow = MakeArrow());

        private static Texture2D NewTexture(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
        }

        private static Sprite MakeRounded(int size, int radius, bool outline, int thickness)
        {
            var tex = NewTexture(size, size, outline ? "UiRoundedOutline" : "UiRounded");
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedDistance(x + 0.5f, y + 0.5f, size, radius);
                    float a = Mathf.Clamp01(0.5f - d);
                    if (outline) a = Mathf.Min(a, Mathf.Clamp01(d + thickness + 0.5f));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float border = radius + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        /// <summary>Signed distance to a rounded square of side <paramref name="size"/> (negative inside).</summary>
        private static float RoundedDistance(float x, float y, int size, int radius)
        {
            float half = size * 0.5f;
            float qx = Mathf.Abs(x - half) - (half - radius);
            float qy = Mathf.Abs(y - half) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static Sprite MakeCoin()
        {
            const int size = 128;
            var tex = NewTexture(size, size, "UiCoin");
            var px = new Color32[size * size];
            var rim = new Color(0.85f, 0.56f, 0.1f);
            var face = new Color(1f, 0.79f, 0.24f);
            var shine = new Color(1f, 0.92f, 0.55f);
            var mark = new Color(0.8f, 0.5f, 0.08f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((0.98f - r) * size * 0.5f);
                    Color c = r > 0.8f ? rim : face;
                    if (r < 0.8f && u * 0.7f + v * 0.7f > 0.35f && r > 0.55f) c = shine;
                    // A lightning bolt: two slanted bars.
                    bool bolt = (Mathf.Abs(u + v * 0.45f - 0.08f) < 0.11f && v > -0.05f && v < 0.55f)
                                || (Mathf.Abs(u + v * 0.45f + 0.08f) < 0.11f && v < 0.05f && v > -0.55f)
                                || (Mathf.Abs(v) < 0.07f && u > -0.22f && u < 0.22f);
                    if (r < 0.8f && bolt) c = mark;
                    c.a = a;
                    px[y * size + x] = c;
                }
            tex.SetPixels32(ToColor32(px));
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Color32[] ToColor32(Color32[] px) => px;

        private static Sprite MakeStar()
        {
            const int size = 128;
            var tex = NewTexture(size, size, "UiStar");
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f + 0.06f;
                    float ang = Mathf.Atan2(v, u) - Mathf.PI * 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    // Star radius alternates between outer and inner every 36 degrees.
                    float k = Mathf.Repeat(ang, Mathf.PI * 2f / 5f) / (Mathf.PI * 2f / 5f);
                    float t = Mathf.Abs(k - 0.5f) * 2f; // 1 at a tip, 0 between
                    float edge = Mathf.Lerp(0.42f, 0.95f, t * t * 0.4f + t * 0.6f);
                    float a = Mathf.Clamp01((edge - r) * size * 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeHourglass()
        {
            const int size = 96;
            var tex = NewTexture(size, size, "UiHourglass");
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    bool cap = Mathf.Abs(Mathf.Abs(v) - 0.86f) < 0.09f && Mathf.Abs(u) < 0.7f;
                    float halfW = 0.08f + Mathf.Abs(v) * 0.62f;
                    bool glass = Mathf.Abs(v) < 0.8f && Mathf.Abs(Mathf.Abs(u) - halfW) < 0.09f;
                    bool sand = (v < -0.25f && v > -0.78f && Mathf.Abs(u) < halfW - 0.08f) || (v > 0.2f && v < 0.5f && Mathf.Abs(u) < halfW - 0.1f);
                    byte a = (byte)(cap || glass || sand ? 255 : 0);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeArrow()
        {
            const int size = 96;
            var tex = NewTexture(size, size, "UiArrow");
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size - 0.5f;
                    // Head: triangle from u 0.45 to the tip at 0.95; shaft: u 0.08..0.5, |v| < 0.14.
                    float head = (0.95f - u) * 0.9f - Mathf.Abs(v);
                    bool inHead = u >= 0.42f && head > 0f;
                    bool inShaft = u >= 0.08f && u < 0.5f && Mathf.Abs(v) < 0.15f;
                    float a = inHead ? Mathf.Clamp01(head * size) : (inShaft ? 1f : 0f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>A 1×64 vertical gradient texture (top colour at the top), for full-screen menu backdrops.</summary>
        /// <summary>
        /// A vertical three-stop gradient, eased between stops: <paramref name="bottom"/> rising to
        /// <paramref name="mid"/> at height <paramref name="midAt"/> (0..1), then to <paramref name="top"/>.
        /// For an anime sky: pale horizon, clear blue, deep blue overhead.
        /// </summary>
        public static Sprite Gradient(Color top, Color mid, Color bottom, float midAt)
        {
            const int h = 128;
            var tex = NewTexture(1, h, "UiGradient3");
            var px = new Color[h];
            float m = Mathf.Clamp(midAt, 0.01f, 0.99f);
            for (int y = 0; y < h; y++)
            {
                float t = y / (h - 1f);
                px[y] = t < m
                    ? Color.Lerp(bottom, mid, Mathf.SmoothStep(0f, 1f, t / m))
                    : Color.Lerp(mid, top, Mathf.SmoothStep(0f, 1f, (t - m) / (1f - m)));
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Sprite Gradient(Color top, Color bottom)
        {
            var tex = NewTexture(1, 64, "UiGradient");
            var px = new Color[64];
            for (int y = 0; y < 64; y++) px[y] = Color.Lerp(bottom, top, y / 63f);
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
