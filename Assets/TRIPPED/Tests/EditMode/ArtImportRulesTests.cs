using NUnit.Framework;
using Trouble.EditorTools;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>Art drop-in conventions (GDD v1.1 §3.6–3.7), decided from path and size.</summary>
    public class ArtImportRulesTests
    {
        private static readonly string[] Catalog = { "kettle", "toaster", "panel" };

        [Test]
        public void ApplianceAtSpecSize_ImportsWithBottomPivotAndNoProblems()
        {
            // Act
            var plan = ArtImportRules.Plan("Assets/TRIPPED/Art/Appliances/kettle.png", 512, 512, Catalog);

            // Assert
            Assert.AreEqual(ArtKind.Appliance, plan.Kind);
            Assert.AreEqual(new Vector2(0.5f, 0f), plan.Pivot);
            Assert.AreEqual(512f, plan.PixelsPerUnit);
            Assert.IsEmpty(plan.Problems);
        }

        [Test]
        public void ApplianceWithUnknownNameOrWrongSize_IsReported()
        {
            // Act
            var plan = ArtImportRules.Plan("Assets/TRIPPED/Art/Appliances/Kettle_final.png", 600, 512, Catalog);

            // Assert
            Assert.AreEqual(2, plan.Problems.Count);
        }

        [Test]
        public void CharacterStrip_CountsFramesAndKeepsFullWidth()
        {
            // Act
            var plan = ArtImportRules.Plan("Assets/TRIPPED/Art/Characters/player_walk.png", 4096, 512, Catalog);

            // Assert
            Assert.AreEqual(ArtKind.Character, plan.Kind);
            Assert.AreEqual(8, plan.Frames);
            Assert.AreEqual(4096, plan.MaxTextureSize, "a downscaled strip would blur every frame");
            Assert.IsEmpty(plan.Problems);
        }

        [Test]
        public void CharacterStripThatIsNotSquareFrames_IsReported()
        {
            // Act
            var plan = ArtImportRules.Plan("Assets/TRIPPED/Art/Characters/player_idle.png", 2000, 512, Catalog);

            // Assert
            Assert.That(plan.Problems, Has.Some.Contains("multiple of height"));
        }

        [Test]
        public void FaceUnderResources_IsRecognisedWithCentrePivot()
        {
            // Act
            var plan = ArtImportRules.Plan("Assets/TRIPPED/Resources/Art/Faces/face_mum.png", 256, 256, Catalog);

            // Assert
            Assert.AreEqual(ArtKind.Face, plan.Kind);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), plan.Pivot);
            Assert.IsEmpty(plan.Problems);
        }

        [Test]
        public void FileOutsideTheArtFolders_IsLeftAlone()
        {
            // Act
            var plan = ArtImportRules.Plan("Assets/_Game/Art/Icons/Icon_Rice.png", 512, 512, Catalog);

            // Assert
            Assert.AreEqual(ArtKind.None, plan.Kind);
        }
    }
}
