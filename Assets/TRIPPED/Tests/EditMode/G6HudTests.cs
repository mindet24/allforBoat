using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Game;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>G6 order tickets: phase, stacking, slide, shake, exit and timer colour.</summary>
    public class TicketRulesTests
    {
        private static readonly Vector2 Origin = new Vector2(20f, 18f);
        private static readonly Vector2 Size = new Vector2(196f, 186f);
        private const float Gap = 12f;

        [Test]
        public void Phase_FlagsMapToWaitingRunningDoneMissed()
        {
            // Arrange / Act / Assert
            Assert.AreEqual(TicketPhase.Waiting, TicketRules.Phase(false, false, 0));
            Assert.AreEqual(TicketPhase.Running, TicketRules.Phase(false, false, 3));
            Assert.AreEqual(TicketPhase.Done, TicketRules.Phase(true, false, 3));
            Assert.AreEqual(TicketPhase.Missed, TicketRules.Phase(false, true, 0));
        }

        [Test]
        public void SlotPosition_ThirdTicket_StacksLeftToRightWithGaps()
        {
            // Act
            var p = TicketRules.SlotPosition(2, Origin, Size, Gap, 6);

            // Assert
            Assert.AreEqual(Origin.x + 2 * (Size.x + Gap), p.x, 1e-3f);
            Assert.AreEqual(Origin.y, p.y, 1e-3f);
        }

        [Test]
        public void SlotPosition_PastRowLength_WrapsToSecondRow()
        {
            // Act
            var p = TicketRules.SlotPosition(6, Origin, Size, Gap, 6);

            // Assert
            Assert.AreEqual(Origin.x, p.x, 1e-3f);
            Assert.AreEqual(Origin.y + Size.y + Gap, p.y, 1e-3f);
        }

        [Test]
        public void SlotPosition_AdjacentTickets_NeverOverlap()
        {
            // Arrange
            var rects = new List<Rect>();
            for (int i = 0; i < 9; i++) rects.Add(new Rect(TicketRules.SlotPosition(i, Origin, Size, Gap, 6), Size));

            // Act / Assert
            for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.IsFalse(rects[i].Overlaps(rects[j]), $"tickets {i} and {j} overlap");
        }

        [Test]
        public void SlideOffset_BornThenArrived_GoesFromOneToZero()
        {
            // Act / Assert
            Assert.AreEqual(1f, TicketRules.SlideOffset(0f, 0.35f), 1e-4f);
            Assert.AreEqual(0f, TicketRules.SlideOffset(0.35f, 0.35f), 1e-4f);
            Assert.Less(TicketRules.SlideOffset(0.1f, 0.35f), 1f);
        }

        [Test]
        public void ShakeOffset_PlentyOfTimeLeft_IsZero()
        {
            // Act
            float s = TicketRules.ShakeOffset(TicketPhase.Waiting, 8f, 5f, 0.3f, 5f, 38f);

            // Assert
            Assert.AreEqual(0f, s);
        }

        [Test]
        public void ShakeOffset_UnderThreshold_ShakesWithinAmplitude()
        {
            // Arrange
            float max = 0f;

            // Act
            for (int i = 0; i < 50; i++) max = Mathf.Max(max, Mathf.Abs(TicketRules.ShakeOffset(TicketPhase.Waiting, 2f, 5f, i * 0.013f, 5f, 38f)));

            // Assert
            Assert.Greater(max, 0.5f);
            Assert.LessOrEqual(max, 5f + 1e-4f);
        }

        [Test]
        public void ShakeOffset_RunningTicket_NeverShakes()
        {
            // Act
            float s = TicketRules.ShakeOffset(TicketPhase.Running, 1f, 5f, 0.3f, 5f, 38f);

            // Assert
            Assert.AreEqual(0f, s);
        }

        [Test]
        public void ExitScale_DoneTicket_PopsAboveOneEarly()
        {
            // Act
            float s = TicketRules.ExitScale(TicketPhase.Done, 0.21f, 0.6f, 1.25f);

            // Assert
            Assert.AreEqual(1.25f, s, 1e-3f);
        }

        [Test]
        public void ExitAlpha_EndOfExit_IsTransparent()
        {
            // Act / Assert
            Assert.AreEqual(1f, TicketRules.ExitAlpha(0f, 0.6f), 1e-4f);
            Assert.AreEqual(0f, TicketRules.ExitAlpha(0.6f, 0.6f), 1e-4f);
        }

        [TestCase(0.9f, 0)]
        [TestCase(0.4f, 1)]
        [TestCase(0.1f, 2)]
        public void TimerBand_Fraction_GreenAmberRed(float fraction, int band)
        {
            // Act / Assert
            Assert.AreEqual(band, TicketRules.TimerBand(fraction, 0.5f, 0.25f));
        }
    }

    /// <summary>G6 room power meters.</summary>
    public class MeterRulesTests
    {
        [Test]
        public void Compute_FiveOfSeven_LightsFiveOfSevenSegments()
        {
            // Act
            var m = MeterRules.Compute(5, 7, 0.7f, 1f, 12);

            // Assert
            Assert.AreEqual(7, m.Segments);
            Assert.AreEqual(5, m.Filled);
            Assert.AreEqual(LoadBand.Amber, m.Band);
            Assert.IsFalse(m.Over);
        }

        [Test]
        public void Compute_OverCapacity_AllLitRedAndOver()
        {
            // Act
            var m = MeterRules.Compute(9, 7, 0.7f, 1f, 12);

            // Assert
            Assert.AreEqual(7, m.Filled);
            Assert.AreEqual(LoadBand.Red, m.Band);
            Assert.IsTrue(m.Over);
        }

        [Test]
        public void Compute_LowLoad_IsGreen()
        {
            // Act
            var m = MeterRules.Compute(2, 10, 0.7f, 1f, 12);

            // Assert
            Assert.AreEqual(LoadBand.Green, m.Band);
            Assert.AreEqual(2, m.Filled);
        }

        [Test]
        public void Compute_CapacityAboveMax_ScalesToMaxSegments()
        {
            // Act
            var m = MeterRules.Compute(10, 20, 0.7f, 1f, 10);

            // Assert
            Assert.AreEqual(10, m.Segments);
            Assert.AreEqual(5, m.Filled);
        }

        [Test]
        public void Compute_ZeroCapacity_StillOneSegment()
        {
            // Act
            var m = MeterRules.Compute(0, 0, 0.7f, 1f, 12);

            // Assert
            Assert.AreEqual(1, m.Segments);
            Assert.AreEqual(0, m.Filled);
        }

        [Test]
        public void Text_FiveOfSeven_ReadsLikeTheMockup()
        {
            // Act / Assert
            Assert.AreEqual("■■■■■□□ 5/7", MeterRules.Text(5, 7, 12));
        }

        [Test]
        public void OverloadSeconds_NineOfThirtyTicksAtTen_IsTwoPointOne()
        {
            // Act / Assert: 30 ticks to trip, 9 used, 10 ticks per second -> 2.1 s left.
            Assert.AreEqual("2.1s", MeterRules.OverloadSeconds(9, 30, 10));
        }
    }

    /// <summary>G6 end-of-day stars.</summary>
    public class StarRulesTests
    {
        private static readonly int[] Thresholds = { 10, 20, 30 };

        [TestCase(0, 0)]
        [TestCase(10, 1)]
        [TestCase(19, 1)]
        [TestCase(20, 2)]
        [TestCase(45, 3)]
        public void Stars_WonWithScore_OnePerThreshold(int score, int stars)
        {
            // Act / Assert
            Assert.AreEqual(stars, StarRules.Stars(score, Thresholds, true));
        }

        [Test]
        public void Stars_LostDay_IsZero()
        {
            // Act / Assert
            Assert.AreEqual(0, StarRules.Stars(50, Thresholds, false));
        }

        [Test]
        public void Stars_NoThresholds_IsZero()
        {
            // Act / Assert
            Assert.AreEqual(0, StarRules.Stars(50, null, true));
        }
    }

    /// <summary>G6 keyboard menu focus.</summary>
    public class MenuNavRulesTests
    {
        [Test]
        public void Step_PastLast_WrapsToFirst()
        {
            // Act / Assert
            Assert.AreEqual(0, MenuNavRules.Step(2, 1, 3));
        }

        [Test]
        public void Step_BeforeFirst_WrapsToLast()
        {
            // Act / Assert
            Assert.AreEqual(2, MenuNavRules.Step(0, -1, 3));
        }

        [Test]
        public void Step_EmptyMenu_IsMinusOne()
        {
            // Act / Assert
            Assert.AreEqual(-1, MenuNavRules.Step(0, 1, 0));
        }
    }

    /// <summary>G6 off-screen edge arrows.</summary>
    public class EdgeArrowLayoutTests
    {
        private static readonly Rect Safe = new Rect(100f, 100f, 1720f, 800f);

        [Test]
        public void Place_PointInside_StaysOnScreenInPlace()
        {
            // Arrange
            var into = new List<EdgeSlot>();

            // Act
            EdgeArrowLayout.Place(new[] { new Vector2(900f, 500f) }, Safe, 100f, into);

            // Assert
            Assert.IsTrue(into[0].OnScreen);
            Assert.AreEqual(new Vector2(900f, 500f), into[0].Position);
        }

        [Test]
        public void Place_PointFarBelow_SitsOnBottomEdgePointingDown()
        {
            // Arrange
            var into = new List<EdgeSlot>();

            // Act
            EdgeArrowLayout.Place(new[] { new Vector2(960f, -2000f) }, Safe, 100f, into);

            // Assert
            Assert.IsFalse(into[0].OnScreen);
            Assert.AreEqual(Safe.yMin, into[0].Position.y, 1e-2f);
            Assert.AreEqual(-90f, into[0].Angle, 1f);
        }

        [Test]
        public void Place_TwoPointsSameSpotOffRight_ArePushedApart()
        {
            // Arrange
            var into = new List<EdgeSlot>();
            var pts = new[] { new Vector2(5000f, 500f), new Vector2(5000f, 500f) };

            // Act
            EdgeArrowLayout.Place(pts, Safe, 100f, into);

            // Assert
            Assert.AreEqual(Safe.xMax, into[0].Position.x, 1e-2f);
            Assert.AreEqual(Safe.xMax, into[1].Position.x, 1e-2f);
            Assert.GreaterOrEqual(Mathf.Abs(into[0].Position.y - into[1].Position.y), 100f - 1e-3f);
        }

        [Test]
        public void Place_CrowdedEdge_StaysInsideTheSide()
        {
            // Arrange
            var into = new List<EdgeSlot>();
            var pts = new List<Vector2>();
            for (int i = 0; i < 5; i++) pts.Add(new Vector2(5000f, 890f));

            // Act
            EdgeArrowLayout.Place(pts, Safe, 100f, into);

            // Assert
            foreach (var s in into)
            {
                Assert.LessOrEqual(s.Position.y, Safe.yMax + 1e-2f);
                Assert.GreaterOrEqual(s.Position.y, Safe.yMin - 1e-2f);
            }
        }
    }

    /// <summary>G6 follow camera framing.</summary>
    public class FollowFramingTests
    {
        private const float Aspect = 16f / 9f;

        [Test]
        public void ViewHeight_WideRoom_ShowsRoomPlusPeekOnEachSide()
        {
            // Act
            float h = FollowFraming.ViewHeight(9f, 3f, Aspect, 1f, 1.5f, 100f);

            // Assert
            Assert.AreEqual(11f / Aspect, h, 1e-3f);
        }

        [Test]
        public void ViewHeight_NarrowRoom_ShowsAtLeastMinFloors()
        {
            // Act
            float h = FollowFraming.ViewHeight(4f, 3f, Aspect, 1f, 1.9f, 100f);

            // Assert
            Assert.AreEqual(3f * 1.9f, h, 1e-3f);
        }

        [Test]
        public void ViewHeight_NeverMoreThanTheOverview()
        {
            // Act
            float h = FollowFraming.ViewHeight(40f, 3f, Aspect, 1f, 1.9f, 12f);

            // Assert
            Assert.AreEqual(12f, h, 1e-3f);
        }

        [Test]
        public void FocusX_HalfLock_IsBetweenCharacterAndRoomCentre()
        {
            // Act
            float x = FollowFraming.FocusX(2f, 0f, 10f, 0.5f);

            // Assert
            Assert.AreEqual(3.5f, x, 1e-4f);
        }

        [Test]
        public void Center_AnchorBelowMiddle_PutsFocusThere()
        {
            // Arrange
            var focus = new Vector2(5f, 4.5f);
            float h = 6f;

            // Act
            var c = FollowFraming.Center(focus, h, Aspect, new Vector2(0.5f, 0.4f));
            var vp = CameraMath.PlaneToViewportForHeight(c, h, Aspect, focus);

            // Assert
            Assert.AreEqual(0.5f, vp.x, 1e-4f);
            Assert.AreEqual(0.4f, vp.y, 1e-4f);
        }

        [Test]
        public void ClampToBounds_NearLeftWall_ViewStopsAtTheWall()
        {
            // Arrange
            var bounds = new Rect(0f, 0f, 17f, 12f);
            float h = 6f;

            // Act
            var c = FollowFraming.ClampToBounds(new Vector2(0f, 6f), h, Aspect, bounds);

            // Assert
            Assert.AreEqual(h * Aspect * 0.5f, c.x, 1e-4f);
        }

        [Test]
        public void ClampToBounds_ViewWiderThanHouse_CentresOnIt()
        {
            // Arrange
            var bounds = new Rect(0f, 0f, 8f, 12f);

            // Act
            var c = FollowFraming.ClampToBounds(new Vector2(-20f, 6f), 6f, Aspect, bounds);

            // Assert
            Assert.AreEqual(4f, c.x, 1e-4f);
        }
    }

    /// <summary>G6 best day score in Progress.</summary>
    public class BestScoreTests
    {
        [Test]
        public void RecordScore_HigherThenLower_KeepsTheBest()
        {
            // Arrange
            var p = new Progress(new MemoryProgressStore());

            // Act
            bool first = p.RecordScore(100, 12);
            bool lower = p.RecordScore(100, 8);

            // Assert
            Assert.IsTrue(first);
            Assert.IsFalse(lower);
            Assert.AreEqual(12, p.BestScore(100));
        }

        [Test]
        public void BestScore_NeverPlayed_IsZero()
        {
            // Act / Assert
            Assert.AreEqual(0, new Progress(new MemoryProgressStore()).BestScore(100));
        }
    }

    /// <summary>G6 key prompts: the stairs prompt never touches the E prompt.</summary>
    public class PromptLayoutTests
    {
        private static readonly Vector2 StairsSize = new Vector2(0.8f, 0.42f);
        private const float Gap = 0.12f;

        [Test]
        public void StairsCentre_NoEPrompt_StaysWhereWanted()
        {
            // Act
            var c = PromptLayout.StairsCentre(new Vector2(5f, 1.5f), StairsSize, null, 5f, Gap);

            // Assert
            Assert.AreEqual(new Vector2(5f, 1.5f), c);
        }

        [Test]
        public void StairsCentre_FarFromE_StaysWhereWanted()
        {
            // Arrange
            var e = new Rect(9f, 1.3f, 0.5f, 0.42f);

            // Act
            var c = PromptLayout.StairsCentre(new Vector2(5f, 1.5f), StairsSize, e, 5f, Gap);

            // Assert
            Assert.AreEqual(new Vector2(5f, 1.5f), c);
        }

        [Test]
        public void StairsCentre_EOverlapsRightOfCharacter_MovesLeftWithGap()
        {
            // Arrange: E right of the character, on top of where W wants to be.
            var e = new Rect(5.0f, 1.3f, 0.5f, 0.42f);

            // Act
            var c = PromptLayout.StairsCentre(new Vector2(5.2f, 1.5f), StairsSize, e, 4.8f, Gap);
            var stairs = new Rect(c - StairsSize * 0.5f, StairsSize);

            // Assert
            Assert.LessOrEqual(stairs.xMax, e.xMin - Gap + 1e-4f);
            Assert.AreEqual(1.5f, c.y, 1e-4f);
        }

        [Test]
        public void StairsCentre_EOverlapsLeftOfCharacter_MovesRightWithGap()
        {
            // Arrange
            var e = new Rect(4.6f, 1.3f, 0.5f, 0.42f);

            // Act
            var c = PromptLayout.StairsCentre(new Vector2(4.9f, 1.5f), StairsSize, e, 5.5f, Gap);
            var stairs = new Rect(c - StairsSize * 0.5f, StairsSize);

            // Assert
            Assert.GreaterOrEqual(stairs.xMin, e.xMax + Gap - 1e-4f);
        }
    }

    /// <summary>G6 room meters in follow mode.</summary>
    public class MeterVisibilityTests
    {
        private static readonly Rect OnScreenRoom = new Rect(0.2f, 0.3f, 0.5f, 0.4f);
        private static readonly Rect HalfOffRoom = new Rect(0.2f, -0.3f, 0.5f, 0.4f);
        private static readonly Rect Meter = new Rect(100f, 100f, 300f, 40f);
        private static readonly List<Rect> Hud = new List<Rect> { new Rect(-60f, -500f, 120f, 120f) };

        [Test]
        public void Show_Overview_AlwaysShows()
        {
            // Act / Assert
            Assert.IsTrue(MeterVisibility.Show(false, false, HalfOffRoom, new Rect(-50f, -450f, 100f, 40f), Hud));
        }

        [Test]
        public void Show_FollowingRoomPartlyOffScreen_Hides()
        {
            // Act / Assert
            Assert.IsFalse(MeterVisibility.Show(true, false, HalfOffRoom, Meter, Hud));
        }

        [Test]
        public void Show_FollowingCurrentRoomPartlyOffScreen_Shows()
        {
            // Act / Assert
            Assert.IsTrue(MeterVisibility.Show(true, true, HalfOffRoom, Meter, Hud));
        }

        [Test]
        public void Show_FollowingRoomOnScreenClearOfHud_Shows()
        {
            // Act / Assert
            Assert.IsTrue(MeterVisibility.Show(true, false, OnScreenRoom, Meter, Hud));
        }

        [Test]
        public void Show_FollowingMeterUnderHudPanel_Hides()
        {
            // Arrange: the meter sits under the carry slot.
            var underCarry = new Rect(-100f, -460f, 300f, 40f);

            // Act / Assert
            Assert.IsFalse(MeterVisibility.Show(true, true, OnScreenRoom, underCarry, Hud));
        }
    }
}
