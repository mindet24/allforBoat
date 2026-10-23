using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Builds greybox pieces at runtime: boxes and quads with the render pipeline's default lit material, one
    /// shared material per colour, plus world-space text. No colliders unless asked for.
    /// </summary>
    public static class Prims
    {
        private static Mesh _cube;
        private static Mesh _quad;
        private static Material _baseMaterial;
        private static readonly Dictionary<Color32, Material> Materials = new Dictionary<Color32, Material>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        /// <summary>A box centred on <paramref name="center"/> (local to the parent) with this size and colour.</summary>
        public static MeshRenderer Box(Transform parent, string name, Vector3 center, Vector3 size, Color color)
        {
            EnsureMeshes();
            return Make(parent, name, _cube, center, size, Lit(color));
        }

        /// <summary>A quad facing the camera (-z), centred on <paramref name="center"/>, with a texture.</summary>
        public static MeshRenderer TexturedQuad(Transform parent, string name, Vector3 center, Vector2 size, Texture2D texture)
        {
            EnsureMeshes();
            var material = new Material(BaseMaterial()) { name = "RoomBackground" };
            if (material.HasProperty(BaseMapId)) material.SetTexture(BaseMapId, texture);
            if (material.HasProperty(MainTexId)) material.SetTexture(MainTexId, texture);
            SetColor(material, Color.white);
            return Make(parent, name, _quad, center, new Vector3(size.x, size.y, 1f), material);
        }

        /// <summary>World-space TextMeshPro text that auto-sizes into a box of <paramref name="size"/> metres.</summary>
        public static TextMeshPro Text(Transform parent, string name, Vector3 localPosition, Vector2 size, string text,
            Color color, float maxFontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center, bool bold = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = size;
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.3f;
            tmp.fontSizeMax = maxFontSize;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        /// <summary>The shared lit material for a colour.</summary>
        public static Material Lit(Color color)
        {
            Color32 key = color;
            if (Materials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(BaseMaterial()) { name = "Greybox " + ColorUtility.ToHtmlStringRGB(color) };
            SetColor(m, color);
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, 0.05f);
            Materials[key] = m;
            return m;
        }

        /// <summary>Overrides a renderer's colour without a new material (hidden-room tint).</summary>
        public static void Tint(Renderer renderer, Color color, MaterialPropertyBlock block)
        {
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        private static void SetColor(Material m, Color color)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, color);
        }

        private static MeshRenderer Make(Transform parent, string name, Mesh mesh, Vector3 center, Vector3 size, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            return r;
        }

        private static Material BaseMaterial()
        {
            if (_baseMaterial != null) return _baseMaterial;
            EnsureMeshes();
            return _baseMaterial;
        }

        /// <summary>Takes the built-in cube and quad meshes and the pipeline's default material from temporary primitives.</summary>
        private static void EnsureMeshes()
        {
            if (_cube != null && _quad != null && _baseMaterial != null) return;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _cube = cube.GetComponent<MeshFilter>().sharedMesh;
            _quad = quad.GetComponent<MeshFilter>().sharedMesh;
            _baseMaterial = cube.GetComponent<MeshRenderer>().sharedMaterial;
            Object.DestroyImmediate(cube);
            Object.DestroyImmediate(quad);
        }
    }
}
