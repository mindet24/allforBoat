using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Code-generated placeholder sprites (GDD v1.1 §3.6–3.7): rounded cards, circles and a capsule character.
    /// Made once and shared; they are white so renderers tint them.
    /// </summary>
    public static class PlaceholderArt
    {
        private static Sprite _uiRounded;
        private static Sprite _uiRoundedOutline;
        private static Sprite _worldRounded;
        private static Sprite _circle;
        private static Sprite _character;
        private static Sprite _soft;
        private static Sprite _dashed;
        private static Sprite _padlock;
        private static Sprite _ring;
        private static Sprite _arrow;

        /// <summary>Rounded rect for uGUI, 9-sliced (use Image.Type.Sliced).</summary>
        public static Sprite UiRounded => _uiRounded != null ? _uiRounded : (_uiRounded = MakeRounded(64, 18, 100f, false));

        /// <summary>Rounded rect outline for uGUI (selection rings), 9-sliced.</summary>
        public static Sprite UiRoundedOutline => _uiRoundedOutline != null ? _uiRoundedOutline : (_uiRoundedOutline = MakeRounded(64, 18, 100f, true));

        /// <summary>Rounded rect for SpriteRenderer in Sliced mode; corners about 9 cm in the world.</summary>
        public static Sprite WorldRounded => _worldRounded != null ? _worldRounded : (_worldRounded = MakeRounded(64, 24, 280f, false));

        /// <summary>Filled circle, 1 unit across at 100 pixels per unit.</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeCircle(128));

        /// <summary>Cream capsule with two eyes, pivot at the feet, 1 unit tall.</summary>
        public static Sprite Character => _character != null ? _character : (_character = MakeCharacter());

        /// <summary>
        /// White rect whose edges fade out, 9-sliced so the fade keeps its width at any size (Sight overlay).
        /// 1 unit = 100 pixels; the fade is 32 pixels = 0.32 m.
        /// </summary>
        public static Sprite SoftRect => _soft != null ? _soft : (_soft = MakeSoft());

        /// <summary>Dashed square outline, 1 unit across (assumed-circuit marker on sockets).</summary>
        public static Sprite DashedOutline => _dashed != null ? _dashed : (_dashed = MakeDashed());

        /// <summary>Padlock icon, 1 unit tall (reserved sockets).</summary>
        public static Sprite Padlock => _padlock != null ? _padlock : (_padlock = MakePadlock());

        /// <summary>Thick ring for radial countdowns (Image.Type.Filled, Radial360).</summary>
        public static Sprite Ring => _ring != null ? _ring : (_ring = MakeRing());

        /// <summary>Arrow pointing right, for off-screen alerts.</summary>
        public static Sprite Arrow => _arrow != null ? _arrow : (_arrow = MakeArrow());

        private static Sprite _heart;

        /// <summary>A heart (white, tinted by the user), 1 unit across at 100 pixels per unit.</summary>
        public static Sprite Heart => _heart != null ? _heart : (_heart = MakeHeart());

        private static Sprite MakeHeart()
        {
            const int size = 96;
            var tex = NewTexture(size, size, "PlaceholderHeart");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Implicit heart curve (x^2 + y^2 - 1)^3 - x^2 y^3 <= 0, scaled into the texture.
                    float u = ((x + 0.5f) / size - 0.5f) * 2.7f;
                    float v = ((y + 0.5f) / size - 0.45f) * 2.7f;
                    float f = Mathf.Pow(u * u + v * v - 1f, 3f) - u * u * v * v * v;
                    float a = Mathf.Clamp01(0.5f - f * 14f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeSoft()
        {
            const int size = 96, fade = 32;
            var tex = NewTexture(size, size, "PlaceholderSoft");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x + 0.5f, size - x - 0.5f), Mathf.Min(y + 0.5f, size - y - 0.5f));
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / fade));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(fade, fade, fade, fade));
        }

        private static Sprite MakeDashed()
        {
            const int size = 64, ring = 6, dash = 8;
            var tex = NewTexture(size, size, "PlaceholderDashed");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool onRing = x < ring || y < ring || x >= size - ring || y >= size - ring;
                    // Position along the edge decides dash or gap.
                    int along = (x < ring || x >= size - ring) ? y : x;
                    bool on = onRing && (along / dash) % 2 == 0;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(on ? 255 : 0));
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite MakePadlock()
        {
            const int size = 64;
            var tex = NewTexture(size, size, "PlaceholderPadlock");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    bool body = px >= 14 && px <= 50 && py >= 6 && py <= 34;
                    float r = Mathf.Sqrt((px - 32) * (px - 32) + (py - 36) * (py - 36));
                    bool shackle = py >= 34 && r >= 11 && r <= 17;
                    bool hole = Mathf.Sqrt((px - 32) * (px - 32) + (py - 22) * (py - 22)) < 4f;
                    bool on = (body && !hole) || shackle;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(on ? 255 : 0));
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite MakeRing()
        {
            const int size = 128;
            var tex = NewTexture(size, size, "PlaceholderRing");
            var pixels = new Color32[size * size];
            float outer = size * 0.5f - 1f, inner = outer - 14f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - size * 0.5f) * (x + 0.5f - size * 0.5f) + (y + 0.5f - size * 0.5f) * (y + 0.5f - size * 0.5f));
                    float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeArrow()
        {
            const int size = 64;
            var tex = NewTexture(size, size, "PlaceholderArrow");
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Triangle pointing right: x from 8 to 56, half-height shrinking toward the tip.
                    float px = x + 0.5f, py = y + 0.5f - size * 0.5f;
                    bool on = px >= 8 && px <= 56 && Mathf.Abs(py) <= (56 - px) * 0.55f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(on ? 255 : 0));
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeRounded(int size, int radius, float pixelsPerUnit, bool outlineOnly)
        {
            var tex = NewTexture(size, size, "PlaceholderRounded");
            var pixels = new Color32[size * size];
            float ring = 4f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedRectDistance(x + 0.5f, y + 0.5f, size, size, radius);
                    float a = Mathf.Clamp01(0.5f - d);
                    if (outlineOnly) a = Mathf.Min(a, Mathf.Clamp01(d + ring + 0.5f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            int border = radius + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite MakeCircle(int size)
        {
            var tex = NewTexture(size, size, "PlaceholderCircle");
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) - (r - 1f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeCharacter()
        {
            const int w = 128, h = 256;
            var tex = NewTexture(w, h, "PlaceholderCharacter");
            var pixels = new Color32[w * h];
            var body = new Color32(239, 227, 207, 255);
            var line = new Color32(43, 43, 51, 255);
            float radius = w * 0.5f - 4f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Capsule: distance to the vertical segment between the two cap centres.
                    float px = x + 0.5f, py = y + 0.5f;
                    float cy = Mathf.Clamp(py, radius + 4f, h - radius - 4f);
                    float d = Mathf.Sqrt((px - w * 0.5f) * (px - w * 0.5f) + (py - cy) * (py - cy)) - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    Color32 c = d > -6f ? line : body;
                    // Two eyes near the top.
                    float eyeY = h - radius - 10f;
                    float e1 = Mathf.Sqrt((px - w * 0.36f) * (px - w * 0.36f) + (py - eyeY) * (py - eyeY));
                    float e2 = Mathf.Sqrt((px - w * 0.64f) * (px - w * 0.64f) + (py - eyeY) * (py - eyeY));
                    if (e1 < 8f || e2 < 8f) c = line;
                    c.a = (byte)(a * 255f);
                    pixels[y * w + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);
        }

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

        /// <summary>Signed distance from a point to a rounded rect filling the texture (negative inside).</summary>
        private static float RoundedRectDistance(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r - 1f);
            float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r - 1f);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }
}
