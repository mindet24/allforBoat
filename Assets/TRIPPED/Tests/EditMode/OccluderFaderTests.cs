using NUnit.Framework;
using Trouble.Game;

namespace Trouble.Tests
{
    /// <summary>Which house meshes the see-through fader leaves solid.</summary>
    public class OccluderFaderTests
    {
        private static readonly string[] Parts = { "Floor", "Foundation" };

        [TestCase("Living_Floor")]
        [TestCase("Room_Floor")]
        [TestCase("Foundation_Segment")]
        [TestCase("kitchen_floor")]
        public void NeverFades_FloorsAndFoundation_StaySolid(string meshName)
        {
            // Act / Assert
            Assert.IsTrue(OccluderFader.NeverFades(meshName, Parts));
        }

        [TestCase("Kitchen_Chairs")]
        [TestCase("Room_SideWall_Solid")]
        [TestCase("Room_Ceiling")]
        [TestCase("Living_Rug")]
        public void NeverFades_WallsCeilingsAndFurniture_CanFade(string meshName)
        {
            // Act / Assert
            Assert.IsFalse(OccluderFader.NeverFades(meshName, Parts));
        }

        [Test]
        public void NeverFades_NoParts_EverythingCanFade()
        {
            // Act / Assert
            Assert.IsFalse(OccluderFader.NeverFades("Living_Floor", null));
        }
    }
}
