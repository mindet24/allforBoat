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
