using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Game;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>Where the Day-mode family stands (requests themselves are covered in DayModeTests).</summary>
    public class FamilyRulesTests
    {
        [Test]
        public void RoomOf_NoRequests_IsFirstHomeRoom()
        {
            // Arrange
            var home = new List<string> { "kitchen", "living" };

            // Act
            string room = FamilyRules.RoomOf("Mom", home, new List<RequestState>());

            // Assert
            Assert.AreEqual("kitchen", room);
        }

        [Test]
        public void RoomOf_NoHomeAndNoRequests_IsNull()
        {
            // Act
            string room = FamilyRules.RoomOf("Mom", null, null);

            // Assert
            Assert.IsNull(room);
        }

        [Test]
        public void SlotX_ThreePeople_SpreadEvenlyAwayFromWalls()
        {
            // Arrange
            const float roomX = 4f, width = 8f;

            // Act
            float first = FamilyRules.SlotX(roomX, width, 0, 3);
            float middle = FamilyRules.SlotX(roomX, width, 1, 3);
            float last = FamilyRules.SlotX(roomX, width, 2, 3);

            // Assert
            Assert.AreEqual(6f, first, 1e-4f);
            Assert.AreEqual(8f, middle, 1e-4f);
            Assert.AreEqual(10f, last, 1e-4f);
        }

        [Test]
        public void AvoidX_StandingBehindFridge_StepsToNearerFreeSide()
        {
            // Arrange: a fridge from x 12.4 to 13.4 in a room from 9 to 17; the person stands at 13.
            var blocked = new List<UnityEngine.Vector2> { new UnityEngine.Vector2(12.4f, 13.4f) };

            // Act
            float x = FamilyRules.AvoidX(13f, 0.35f, 9f, 17f, blocked);

            // Assert: the right side (13.75) is nearer than the left (12.05).
            Assert.AreEqual(13.75f, x, 1e-4f);
        }

        [Test]
        public void AvoidX_AlreadyClear_StaysPut()
        {
            // Arrange
            var blocked = new List<UnityEngine.Vector2> { new UnityEngine.Vector2(10f, 11f) };

            // Act
            float x = FamilyRules.AvoidX(14f, 0.35f, 9f, 17f, blocked);

            // Assert
            Assert.AreEqual(14f, x, 1e-4f);
        }

        [Test]
        public void SlotX_OnePerson_StandsInTheMiddle()
        {
            // Act
            float x = FamilyRules.SlotX(0f, 6f, 0, 1);

            // Assert
            Assert.AreEqual(3f, x, 1e-4f);
        }
    }
}
