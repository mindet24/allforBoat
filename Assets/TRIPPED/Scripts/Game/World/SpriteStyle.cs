using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Makes the flat pixel-art sprites sit in the 3D house: a thin outline (TRIPPED/SpriteOutline shader) and a soft
    /// oval shadow on the floor under the feet, so nothing looks pasted on or floating.
    /// </summary>
    public static class SpriteStyle
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int SilhouetteId = Shader.PropertyToID("_Silhouette");
        private const string ShadowName = "BlobShadow";
        private const string OutlineName = "Outline";
        private const string BadgeName = "FootBadge";
        private static Material _outline;

        /// <summary>
        /// True for the shadows, foot badges and rig outlines added here. They are decoration, not something to look at:
        /// the see-through fader skips them, or a floor shadow in another room would fade the walls in between.
        /// </summary>
        public static bool IsDecoration(SpriteRenderer sr) => sr.name == ShadowName || sr.name == OutlineName || sr.name == BadgeName;
        private static Sprite _blob;
        private static Sprite _ring;

        /// <summary>
        /// Draws <paramref name="sr"/> with the outline material. The width is given in metres and turned into texels
        /// from the sprite's resolution and the renderer's scale, so hi-res rig parts and small pixel sprites get the same edge.
        /// Call after the renderer has its sprite and final scale.
        /// </summary>
        public static void Outline(SpriteRenderer sr, TrippedViewConfig config) => Outline(sr, config, false);

        /// <summary>
        /// Adds a flat outline-coloured copy of <paramref name="part"/> behind it at <paramref name="sortingOrder"/>,
        /// for multi-part rigs: every part's copy goes behind every part, so the edge only shows around the whole body.
        /// Returns the copy (null when outlines are off) so the caller can fade it with the part.
        /// </summary>
        public static SpriteRenderer Silhouette(SpriteRenderer part, int sortingOrder, TrippedViewConfig config)
        {
            if (part == null || config == null || !config.spriteOutline) return null;
            var go = new GameObject(OutlineName);
            go.transform.SetParent(part.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = part.sprite;
            sr.flipX = part.flipX;
            sr.sortingOrder = sortingOrder;
            Outline(sr, config, true);
            return sr;
        }

        private static void Outline(SpriteRenderer sr, TrippedViewConfig config, bool silhouette)
        {
            if (sr == null || config == null || !config.spriteOutline) return;
            if (_outline == null)
            {
                var shader = Resources.Load<Shader>("Shaders/SpriteOutline");
                if (shader == null) return;
                _outline = new Material(shader) { name = "SpriteOutline" };
            }
            sr.sharedMaterial = _outline;
            float metresPerTexel = sr.sprite != null ? sr.transform.lossyScale.y / sr.sprite.pixelsPerUnit : 0f;
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            block.SetColor(OutlineColorId, config.spriteOutlineColor);
            block.SetFloat(OutlineWidthId, metresPerTexel > 0f ? config.spriteOutlineMetres / metresPerTexel : 1f);
            block.SetFloat(SilhouetteId, silhouette ? 1f : 0f);
            sr.SetPropertyBlock(block);
        }

        /// <summary>
        /// Adds a soft dark oval lying on the floor at the parent's origin (the feet), <paramref name="width"/> metres wide,
        /// drawn before every other sprite.
        /// </summary>
        public static SpriteRenderer BlobShadow(Transform parent, float width, TrippedViewConfig config)
        {
            if (config == null || !config.blobShadows) return null;
            var go = new GameObject(ShadowName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(width, width * config.blobShadowDepth, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Blob();
            sr.color = new Color(0f, 0f, 0f, config.blobShadowAlpha);
            sr.sortingOrder = short.MinValue + 1;
            return sr;
        }

        /// <summary>
        /// Adds a ring in <paramref name="color"/> lying on the floor at the parent's origin (the feet),
        /// <paramref name="width"/> metres wide, drawn just above the blob shadow: who someone is, readable by colour
        /// alone (the same colour as their request cards).
        /// </summary>
        public static SpriteRenderer FootBadge(Transform parent, float width, Color color, TrippedViewConfig config)
        {
            if (config == null || !config.footBadges) return null;
            var go = new GameObject(BadgeName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(width, width * config.blobShadowDepth, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Ring(config.footBadgeThickness);
            sr.color = new Color(color.r, color.g, color.b, config.footBadgeAlpha);
            sr.sortingOrder = short.MinValue + 2;
            return sr;
        }

        /// <summary>A 1-unit ring, <paramref name="thickness"/> of the radius wide, made once.</summary>
        private static Sprite Ring(float thickness)
        {
            if (_ring != null) return _ring;
            const int size = 128;
            float inner = 1f - Mathf.Clamp(thickness, 0.05f, 1f);
            float edge = 2f / size;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FootBadge" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) / edge) * Mathf.Clamp01((d - inner) / edge);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            tex.SetPixels32(px);
            tex.Apply();
            _ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            return _ring;
        }

        /// <summary>A 1-unit radial gradient, made once.</summary>
        private static Sprite Blob()
        {
            if (_blob != null) return _blob;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BlobShadow" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.SmoothStep(0f, 1f, t)));
                }
            tex.SetPixels32(px);
            tex.Apply();
            _blob = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            return _blob;
        }
    }
}
