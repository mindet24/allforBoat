using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Trouble.EditorTools
{
    public enum ArtKind { None, Appliance, Character, Face, Prop }

    /// <summary>How one art file should be imported, and anything wrong with how it was made.</summary>
    public sealed class ArtImportPlan
    {
        public ArtKind Kind;
        /// <summary>Sprite pivot in 0..1 of the image.</summary>
        public Vector2 Pivot;
        public float PixelsPerUnit;
        public int MaxTextureSize;
        /// <summary>Character strips only: frames across the strip.</summary>
        public int Frames;
        public readonly List<string> Problems = new List<string>();
    }

    /// <summary>
    /// The art drop-in conventions of GDD v1.1 §3.6–3.7, decided from the file path and size alone.
    /// Any folder named Art/Appliances, Art/Characters, Art/Faces or Art/Props under Assets/TRIPPED counts.
    /// </summary>
    public static class ArtImportRules
    {
        public const int ApplianceSize = 512;
        public const int FaceSize = 256;
        public static readonly string[] CharacterStates = { "idle", "walk", "interact", "panic" };

        private static readonly Regex Folder = new Regex(@"^Assets/TRIPPED/(?:.*/)?Art/(Appliances|Characters|Faces|Props)/[^/]+\.png$", RegexOptions.IgnoreCase);

        /// <param name="assetPath">Project path with forward slashes.</param>
        /// <param name="width">Source image width in pixels.</param>
        /// <param name="height">Source image height in pixels.</param>
        /// <param name="knownApplianceIds">Catalog ids, to catch misnamed files. Null = skip that check.</param>
        public static ArtImportPlan Plan(string assetPath, int width, int height, ICollection<string> knownApplianceIds)
        {
            var plan = new ArtImportPlan();
            var m = Folder.Match(assetPath);
            if (!m.Success) return plan;

            string name = Path.GetFileNameWithoutExtension(assetPath);
            switch (m.Groups[1].Value.ToLowerInvariant())
            {
                case "appliances":
                    plan.Kind = ArtKind.Appliance;
                    plan.Pivot = new Vector2(0.5f, 0f);
                    plan.PixelsPerUnit = Mathf.Max(1, height);
                    if (width != ApplianceSize || height != ApplianceSize)
                        plan.Problems.Add($"{name}.png is {width}x{height}; appliances should be {ApplianceSize}x{ApplianceSize}.");
                    if (knownApplianceIds != null && !knownApplianceIds.Contains(name))
                        plan.Problems.Add($"{name}.png does not match any appliance id in catalog.json, so the game will not use it.");
                    break;

                case "characters":
                    plan.Kind = ArtKind.Character;
                    plan.Pivot = new Vector2(0.5f, 0f);
                    plan.PixelsPerUnit = Mathf.Max(1, height);
                    plan.Frames = height > 0 ? width / height : 0;
                    if (height <= 0 || width % height != 0)
                        plan.Problems.Add($"{name}.png is {width}x{height}; a strip must be square frames side by side (width a multiple of height).");
                    if (!IsCharacterName(name))
                        plan.Problems.Add($"{name}.png should be named player_{string.Join("/", CharacterStates)}.png or family_<owner>.png.");
                    break;

                case "props":
                    // Props (the breaker panel): stand on their bottom edge like appliances, 1 unit tall, any name.
                    plan.Kind = ArtKind.Prop;
                    plan.Pivot = new Vector2(0.5f, 0f);
                    plan.PixelsPerUnit = Mathf.Max(1, height);
                    break;

                case "faces":
                    plan.Kind = ArtKind.Face;
                    plan.Pivot = new Vector2(0.5f, 0.5f);
                    plan.PixelsPerUnit = Mathf.Max(1, height);
                    if (width != FaceSize || height != FaceSize)
                        plan.Problems.Add($"{name}.png is {width}x{height}; faces should be {FaceSize}x{FaceSize}.");
                    if (!name.StartsWith("face_"))
                        plan.Problems.Add($"{name}.png should be named face_<owner>.png, for example face_mum.png.");
                    break;
            }

            plan.MaxTextureSize = 32;
            while (plan.MaxTextureSize < Mathf.Max(width, height) && plan.MaxTextureSize < 8192) plan.MaxTextureSize *= 2;
            return plan;
        }

        private static bool IsCharacterName(string name)
        {
            if (name.StartsWith("family_") && name.Length > "family_".Length) return true;
            foreach (var state in CharacterStates)
                if (name == "player_" + state) return true;
            return false;
        }
    }
}
