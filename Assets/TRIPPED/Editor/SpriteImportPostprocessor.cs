using System.Collections.Generic;
using System.IO;
using Trouble.Sim;
using UnityEditor;
using UnityEngine;

namespace Trouble.EditorTools
{
    /// <summary>
    /// Imports art dropped into Art/Appliances, Art/Characters or Art/Faces as ready-to-use sprites, so the
    /// artist only has to name the file right (GDD v1.1 §3.6–3.8). Character strips stay one sprite; the game
    /// slices frames at runtime (width ÷ height frames). Naming and size mistakes are logged as warnings.
    /// </summary>
    public sealed class SpriteImportPostprocessor : AssetPostprocessor
    {
        private const string CatalogPath = "Assets/TRIPPED/Resources/Config/catalog.json";

        private void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            var plan = ArtImportRules.Plan(assetPath.Replace('\\', '/'), width, height, CatalogIds());
            if (plan.Kind == ArtKind.None) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = plan.MaxTextureSize;
            importer.spritePixelsPerUnit = plan.PixelsPerUnit;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = plan.Pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            foreach (var problem in plan.Problems)
                Debug.LogWarning($"[TRIPPED art] {problem} ({assetPath})");
        }

        private static ICollection<string> CatalogIds()
        {
            if (!File.Exists(CatalogPath)) return null;
            var ids = new HashSet<string>();
            foreach (var def in JsonUtility.FromJson<CatalogData>(File.ReadAllText(CatalogPath)).appliances) ids.Add(def.id);
            // Panel and socket art live with the appliances (GDD v1.1 §3.8) but are not catalog entries.
            ids.Add("panel");
            ids.Add("socket");
            return ids;
        }
    }
}
