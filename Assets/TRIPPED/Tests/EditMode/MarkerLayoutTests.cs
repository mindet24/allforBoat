using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Game;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>Day request markers: one row per room, side by side, never over prompts or each other.</summary>
    public class MarkerLayoutTests
    {
        private static readonly Vector2 Size = new Vector2(150f, 36f);
        private const float Gap = 6f;

        private static void AssertNoOverlap(List<Rect> rects, IReadOnlyList<Rect> blocked)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                for (int j = i + 1; j < rects.Count; j++) Assert.IsFalse(rects[i].Overlaps(rects[j]), $"markers {i} and {j} overlap");
                if (blocked == null) continue;
                foreach (var b in blocked) Assert.IsFalse(rects[i].Overlaps(b), $"marker {i} covers {b}");
            }
        }

        [Test]
        public void OneMarker_SitsAtTheRightEdgeOnTheRow()
        {
            // Act
            var rects = MarkerLayout.Row(0f, 600f, 300f, Size, Gap, 1, null);

            // Assert
            Assert.AreEqual(1, rects.Count);
            Assert.AreEqual(600f, rects[0].xMax, 1e-3f);
            Assert.AreEqual(300f, rects[0].center.y, 1e-3f);
        }

        [Test]
        public void ThreeMarkers_StandSideBySideOnOneRowWithoutOverlap()
        {
            // Act
            var rects = MarkerLayout.Row(0f, 600f, 300f, Size, Gap, 3, null);

            // Assert
            AssertNoOverlap(rects, null);
            foreach (var r in rects) Assert.AreEqual(300f, r.center.y, 1e-3f, "all on the same row");
            Assert.Less(rects[1].xMax, rects[0].xMin);
            Assert.Less(rects[2].xMax, rects[1].xMin);
        }

        [Test]
        public void PromptOnTheRow_MarkerMovesLeftOfIt()
        {
            // Arrange: an "E" prompt right where the first marker would go.
            var prompt = new Rect(520f, 285f, 40f, 30f);

            // Act
            var rects = MarkerLayout.Row(0f, 600f, 300f, Size, Gap, 2, new[] { prompt });

            // Assert
            AssertNoOverlap(rects, new[] { prompt });
            Assert.Less(rects[0].xMax, prompt.xMin);
            Assert.AreEqual(300f, rects[0].center.y, 1e-3f, "still on the row");
        }

        [Test]
        public void RowTooNarrow_ExtraMarkersStartANewRowBelow()
        {
            // Arrange: room for one marker only.
            // Act
            var rects = MarkerLayout.Row(400f, 600f, 300f, Size, Gap, 2, null);

            // Assert
            AssertNoOverlap(rects, null);
            Assert.AreEqual(300f, rects[0].center.y, 1e-3f);
            Assert.Less(rects[1].center.y, 300f - Size.y + 1e-3f, "second marker on a lower row");
            Assert.AreEqual(600f, rects[1].xMax, 1e-3f, "a new row starts at the right edge again");
        }

        [Test]
        public void PromptsAndLabelInTheWay_NoMarkerCoversAny()
        {
            // Arrange
            var blocked = new[] { new Rect(0f, 280f, 220f, 40f), new Rect(470f, 290f, 30f, 30f), new Rect(300f, 200f, 60f, 140f) };

            // Act
            var rects = MarkerLayout.Row(0f, 700f, 300f, Size, Gap, 4, blocked);

            // Assert
            Assert.AreEqual(4, rects.Count);
            AssertNoOverlap(rects, blocked);
        }
    }
}
