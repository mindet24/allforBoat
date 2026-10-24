using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// Full-screen juice (G5): a white flash frame and a red edge vignette, on an overlay canvas of its own that never
    /// takes clicks. Its sorting order (<see cref="JuiceConfig.overlaySortingOrder"/>) keeps it under the menus.
    /// </summary>
    public sealed class ScreenFx : MonoBehaviour
    {
        private const int VignetteTextureSize = 128;

        private JuiceConfig _juice;
        private Image _flash;
        private Image _vignette;
        private float _flashAt = -100f;
        private Color _flashColor;

        /// <summary>Vignette strength wanted this frame, 0..1 of <see cref="JuiceConfig.vignetteAlphaMax"/>-style alpha. Set every frame.</summary>
        public float VignetteAlpha { get; set; }

        /// <summary>Current flash alpha (for checks).</summary>
        public float FlashAlpha => _flash != null ? _flash.color.a : 0f;

        /// <summary>Builds the canvas. Call once.</summary>
        public void Build(JuiceConfig juice)
        {
            _juice = juice;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = juice.overlaySortingOrder;
            _vignette = MakeImage("Vignette", MakeVignetteSprite());
            _flash = MakeImage("Flash", null);
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _vignette.color = new Color(1f, 0f, 0f, 0f);
        }

        /// <summary>A flash of <paramref name="color"/> that fades over <see cref="JuiceConfig.flashSeconds"/>.</summary>
        public void Flash(Color color)
        {
            _flashColor = color;
            _flashAt = Time.unscaledTime;
        }

        private Image MakeImage(string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        private static Sprite MakeVignetteSprite()
        {
            int n = VignetteTextureSize;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "JuiceVignette" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Clamp01((Mathf.Sqrt(u * u + v * v) - 0.55f) / 0.6f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(d * d * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
            if (_juice == null) return;
            float t = (Time.unscaledTime - _flashAt) / Mathf.Max(0.01f, _juice.flashSeconds);
            var f = _flashColor;
            f.a = t >= 1f || t < 0f ? 0f : _flashColor.a * (1f - t);
            _flash.color = f;
            _flash.enabled = f.a > 0.001f;

            var v = _juice.vignetteColor;
            v.a = Mathf.Clamp01(VignetteAlpha);
            _vignette.color = v;
            _vignette.enabled = v.a > 0.001f;
        }
    }
}
