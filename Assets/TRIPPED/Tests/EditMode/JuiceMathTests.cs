using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Game;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>Pure rules of the game-feel layer (G5).</summary>
    public class JuiceMathTests
    {
        private const int Trip = 30;

        [Test]
        public void HumPitch_NoOverload_IsMin()
        {
            // Arrange + Act
            float p = JuiceMath.HumPitch(0, Trip, 0.8f, 2f);

            // Assert
            Assert.AreEqual(0.8f, p, 1e-5f);
        }

        [Test]
        public void HumPitch_AtTrip_IsMax()
        {
            // Arrange + Act
            float p = JuiceMath.HumPitch(Trip, Trip, 0.8f, 2f);

            // Assert
            Assert.AreEqual(2f, p, 1e-5f);
        }

        [Test]
        public void HumPitch_RisesMonotonically_FasterNearTheEnd()
        {
            // Arrange
            float early = JuiceMath.HumPitch(5, Trip, 1f, 2f) - JuiceMath.HumPitch(0, Trip, 1f, 2f);
            float late = JuiceMath.HumPitch(30, Trip, 1f, 2f) - JuiceMath.HumPitch(25, Trip, 1f, 2f);

            // Act + Assert
            Assert.Greater(early, 0f);
            Assert.Greater(late, early);
        }

        [Test]
        public void HumPitch_PastTrip_IsClampedToMax()
        {
            Assert.AreEqual(2f, JuiceMath.HumPitch(99, Trip, 0.8f, 2f), 1e-5f);
        }

        [Test]
        public void OverloadFraction_ZeroTripTicks_IsOne()
        {
            Assert.AreEqual(1f, JuiceMath.OverloadFraction(0, 0));
        }

        [Test]
        public void ByOverload_Halfway_IsMidpoint()
        {
            Assert.AreEqual(0.5f, JuiceMath.ByOverload(15, Trip, 0f, 1f), 1e-5f);
        }

        [Test]
        public void NextCombo_WithinWindow_Increments()
        {
            // Arrange + Act
            int combo = JuiceMath.NextCombo(2, 10f, 12f, 4f);

            // Assert
            Assert.AreEqual(3, combo);
        }

        [Test]
        public void NextCombo_AfterWindow_ResetsToOne()
        {
            Assert.AreEqual(1, JuiceMath.NextCombo(5, 10f, 15f, 4f));
        }

        [Test]
        public void NextCombo_FirstCompletion_IsOne()
        {
            Assert.AreEqual(1, JuiceMath.NextCombo(0, -100f, 3f, 4f));
        }

        [Test]
        public void ComboPitch_First_IsOne()
        {
            Assert.AreEqual(1f, JuiceMath.ComboPitch(1, 0.12f, 1.6f), 1e-5f);
        }

        [Test]
        public void ComboPitch_Third_RisesTwoSteps()
        {
            Assert.AreEqual(1.24f, JuiceMath.ComboPitch(3, 0.12f, 1.6f), 1e-5f);
        }

        [Test]
        public void ComboPitch_Huge_IsCappedAtMax()
        {
            Assert.AreEqual(1.6f, JuiceMath.ComboPitch(50, 0.12f, 1.6f), 1e-5f);
        }

        [Test]
        public void UrgentTickDue_WholeSecondInsideWarn_True()
        {
            // Arrange: 10 tps, due at 100, now 70 = 3 s left.
            // Act + Assert
            Assert.IsTrue(JuiceMath.UrgentTickDue(70, 100, 10, 5f, 2f));
        }

        [Test]
        public void UrgentTickDue_HalfSecondBeforeFastPhase_False()
        {
            // 3.5 s left: inside warn, outside the fast phase, not on a whole second.
            Assert.IsFalse(JuiceMath.UrgentTickDue(65, 100, 10, 5f, 2f));
        }

        [Test]
        public void UrgentTickDue_HalfSecondInFastPhase_True()
        {
            // 1.5 s left.
            Assert.IsTrue(JuiceMath.UrgentTickDue(85, 100, 10, 5f, 2f));
        }

        [Test]
        public void UrgentTickDue_OutsideWarn_False()
        {
            Assert.IsFalse(JuiceMath.UrgentTickDue(40, 100, 10, 5f, 2f));
        }

        [Test]
        public void UrgentTickDue_PastDue_False()
        {
            Assert.IsFalse(JuiceMath.UrgentTickDue(100, 100, 10, 5f, 2f));
        }

        [Test]
        public void EaseOutBack_Ends_AreZeroAndOne()
        {
            Assert.AreEqual(0f, JuiceMath.EaseOutBack(0f, 1.8f), 1e-5f);
            Assert.AreEqual(1f, JuiceMath.EaseOutBack(1f, 1.8f), 1e-5f);
        }

        [Test]
        public void EaseOutBack_Middle_Overshoots()
        {
            // Arrange
            float max = 0f;
            for (int i = 0; i <= 100; i++) max = Mathf.Max(max, JuiceMath.EaseOutBack(i / 100f, 1.8f));

            // Assert
            Assert.Greater(max, 1f);
        }

        [Test]
        public void PopupScale_AfterLife_IsZero()
        {
            Assert.AreEqual(0f, JuiceMath.PopupScale(2f, 1.1f, 0.25f, 1.8f));
        }

        [Test]
        public void PopupScale_AfterPop_HoldsAtOne()
        {
            Assert.AreEqual(1f, JuiceMath.PopupScale(0.5f, 1.1f, 0.25f, 1.8f), 1e-5f);
        }

        [Test]
        public void PopupRise_EndOfLife_IsFullRise()
        {
            Assert.AreEqual(1.2f, JuiceMath.PopupRise(1.1f, 1.1f, 1.2f), 1e-5f);
        }

        [Test]
        public void Squash_AtStart_IsWideAndShort()
        {
            // Arrange + Act
            JuiceMath.Squash(0f, 0.28f, 0.28f, out float x, out float y);

            // Assert
            Assert.Greater(x, 1f);
            Assert.Less(y, 1f);
        }

        [Test]
        public void Squash_AfterDuration_IsIdentity()
        {
            // Arrange + Act
            JuiceMath.Squash(1f, 0.28f, 0.28f, out float x, out float y);

            // Assert
            Assert.AreEqual(1f, x);
            Assert.AreEqual(1f, y);
        }

        [Test]
        public void DecayShake_NeverBelowZero()
        {
            Assert.AreEqual(0f, JuiceMath.DecayShake(0.1f, 3.5f, 1f));
        }

        [Test]
        public void ShakeAmount_StrongerOfKickAndSteady_Capped()
        {
            Assert.AreEqual(0.3f, JuiceMath.ShakeAmount(0.3f, 0.1f, 0.8f), 1e-5f);
            Assert.AreEqual(0.2f, JuiceMath.ShakeAmount(0.05f, 0.2f, 0.8f), 1e-5f);
            Assert.AreEqual(0.8f, JuiceMath.ShakeAmount(5f, 0f, 0.8f), 1e-5f);
        }

        [Test]
        public void SlowMoScale_StartsLow_EndsAtOne()
        {
            Assert.AreEqual(0.25f, JuiceMath.SlowMoScale(0f, 0.3f, 0.25f), 1e-5f);
            Assert.AreEqual(1f, JuiceMath.SlowMoScale(0.3f, 0.3f, 0.25f), 1e-5f);
            float mid = JuiceMath.SlowMoScale(0.15f, 0.3f, 0.25f);
            Assert.Greater(mid, 0.25f);
            Assert.Less(mid, 1f);
        }

        [Test]
        public void FinalStretch_BeforeFinalSeconds_IsZero()
        {
            Assert.AreEqual(0f, JuiceMath.FinalStretch(600, 10, 30f));
        }

        [Test]
        public void FinalStretch_HalfwayThroughFinal_IsHalf()
        {
            Assert.AreEqual(0.5f, JuiceMath.FinalStretch(150, 10, 30f), 1e-5f);
        }

        [Test]
        public void MusicPitch_FullStretch_IsFinalPitch()
        {
            Assert.AreEqual(1.18f, JuiceMath.MusicPitch(1f, 1.18f), 1e-5f);
            Assert.AreEqual(1f, JuiceMath.MusicPitch(0f, 1.18f), 1e-5f);
        }

        [Test]
        public void RelightDarkness_BeforeItsTurn_IsDark()
        {
            Assert.AreEqual(1f, JuiceMath.RelightDarkness(0.1f, 3, 0.12f, 0.35f, 22f));
        }

        [Test]
        public void RelightDarkness_AfterFlicker_IsLit()
        {
            Assert.AreEqual(0f, JuiceMath.RelightDarkness(5f, 3, 0.12f, 0.35f, 22f));
        }

        [Test]
        public void RelightDarkness_SameInputs_SameResult()
        {
            Assert.AreEqual(JuiceMath.RelightDarkness(0.4f, 2, 0.12f, 0.35f, 22f), JuiceMath.RelightDarkness(0.4f, 2, 0.12f, 0.35f, 22f));
        }

        [Test]
        public void TakeWhole_KeepsTheFraction()
        {
            // Arrange
            float acc = 2.75f;

            // Act
            int n = JuiceMath.TakeWhole(ref acc);

            // Assert
            Assert.AreEqual(2, n);
            Assert.AreEqual(0.75f, acc, 1e-5f);
        }

        [Test]
        public void Pulse_StaysInZeroToOne()
        {
            for (int i = 0; i < 50; i++)
            {
                float p = JuiceMath.Pulse(i * 0.037f, 3f);
                Assert.GreaterOrEqual(p, 0f);
                Assert.LessOrEqual(p, 1f);
            }
        }

        [Test]
        public void HeatFraction_BelowStart_Zero()
        {
            // Arrange / Act: 6 of 10 with the glow starting at 70%.
            float heat = JuiceMath.HeatFraction(6, 10, 0.7f);

            // Assert
            Assert.AreEqual(0f, heat, 1e-4f);
        }

        [Test]
        public void HeatFraction_Full_One()
        {
            // Arrange / Act
            float full = JuiceMath.HeatFraction(10, 10, 0.7f);
            float over = JuiceMath.HeatFraction(13, 10, 0.7f);

            // Assert
            Assert.AreEqual(1f, full, 1e-4f);
            Assert.AreEqual(1f, over, 1e-4f);
        }

        [Test]
        public void HeatFraction_Between_Linear()
        {
            // Arrange / Act: 85% is halfway from 70% to 100%.
            float heat = JuiceMath.HeatFraction(17, 20, 0.7f);

            // Assert
            Assert.AreEqual(0.5f, heat, 1e-4f);
        }

        [Test]
        public void HeatFraction_NoCapacity_Zero()
        {
            // Arrange / Act
            float heat = JuiceMath.HeatFraction(3, 0, 0.7f);

            // Assert
            Assert.AreEqual(0f, heat, 1e-4f);
        }
    }
}
