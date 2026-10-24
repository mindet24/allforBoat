using NUnit.Framework;
using Trouble.Game;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>Pose maths of the player's cut-out rig.</summary>
    public class RigAnimatorTests
    {
        private RigConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<RigConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void Idle_LegsStillAndArmsDown()
        {
            // Act
            var pose = RigAnimator.Evaluate(new RigState { Time = 1.3f }, _config);

            // Assert
            Assert.AreEqual(0f, pose.LegL, 1e-4f);
            Assert.AreEqual(0f, pose.LegR, 1e-4f);
            Assert.LessOrEqual(pose.ArmRaiseL, _config.idleArmSway + 1e-4f);
            Assert.AreEqual(0f, pose.BodyY, 1e-4f);
        }

        [Test]
        public void Walking_LegsSwingOppositeWays()
        {
            // Arrange: a quarter step cycle, where the swing is at its widest.
            float t = 0.5f / _config.walkStepRate;

            // Act
            var pose = RigAnimator.Evaluate(new RigState { Time = t, Walking = true }, _config);

            // Assert
            Assert.AreEqual(_config.walkLegSwing, pose.LegL, 1e-3f);
            Assert.AreEqual(-_config.walkLegSwing, pose.LegR, 1e-3f);
            Assert.Greater(pose.LegLiftL, 0f);
            Assert.AreEqual(0f, pose.LegLiftR, 1e-4f);
        }

        [Test]
        public void Flying_ArmsOutAndFeetTucked()
        {
            // Act
            var pose = RigAnimator.Evaluate(new RigState { Time = 0.2f, Walking = true, Flying = true }, _config);

            // Assert: the flying pose wins over the walk cycle.
            Assert.AreEqual(_config.flyArmRaise, pose.ArmRaiseL, 1e-4f);
            Assert.AreEqual(_config.flyArmRaise, pose.ArmRaiseR, 1e-4f);
            Assert.AreEqual(_config.flyLegTuck, pose.LegLiftL, 1e-4f);
            Assert.AreEqual(_config.flyLegTuck, pose.LegLiftR, 1e-4f);
        }

        [Test]
        public void Carrying_ArmsUpOverHead()
        {
            // Act
            var pose = RigAnimator.Evaluate(new RigState { Time = 0f, Carrying = true }, _config);

            // Assert
            Assert.AreEqual(_config.carryArmRaise, pose.ArmRaiseL, _config.carryArmSway + 1e-3f);
            Assert.AreEqual(_config.carryArmRaise, pose.ArmRaiseR, _config.carryArmSway + 1e-3f);
        }

        [Test]
        public void Lift_StartsArmsDownEndsArmsUpAndCrouchesMidway()
        {
            // Act
            var start = RigAnimator.Evaluate(new RigState { Action = RigAction.Lift, ActionT = 0f }, _config);
            var middle = RigAnimator.Evaluate(new RigState { Action = RigAction.Lift, ActionT = 0.5f }, _config);
            var end = RigAnimator.Evaluate(new RigState { Action = RigAction.Lift, ActionT = 1f }, _config);

            // Assert
            Assert.AreEqual(0f, start.ArmRaiseL, 1e-3f);
            Assert.AreEqual(_config.carryArmRaise, end.ArmRaiseL, 1e-3f);
            Assert.AreEqual(-_config.liftCrouch, middle.BodyY, 1e-3f);
        }

        [Test]
        public void Throw_WindsUpBehindHeadThenReleasesAheadAndLeansForward()
        {
            // Act
            var windUp = RigAnimator.Evaluate(new RigState { Action = RigAction.Throw, ActionT = _config.throwWindUp }, _config);
            float releaseT = _config.throwWindUp + (1f - _config.throwWindUp) * 0.4f;
            var release = RigAnimator.Evaluate(new RigState { Action = RigAction.Throw, ActionT = releaseT }, _config);

            // Assert
            Assert.AreEqual(_config.throwWindUpRaise, windUp.ArmRaiseL, 1e-2f);
            Assert.Less(windUp.Lean, 0f, "leans back while winding up");
            Assert.AreEqual(_config.throwReleaseRaise, release.ArmRaiseL, 1e-2f);
            Assert.Greater(release.Lean, 0f, "leans into the throw");
        }

        [Test]
        public void Plug_CrouchesAndReachesWithOneArm()
        {
            // Act
            var pose = RigAnimator.Evaluate(new RigState { Action = RigAction.Plug, ActionT = 0.5f }, _config);

            // Assert
            Assert.AreEqual(-_config.plugCrouch, pose.BodyY, 1e-3f);
            Assert.AreEqual(_config.plugReach, pose.ArmRaiseR, 1e-3f);
        }

        [Test]
        public void Duration_NoAction_IsZero()
        {
            // Act / Assert
            Assert.AreEqual(0f, RigAnimator.Duration(RigAction.None, _config));
            Assert.AreEqual(_config.throwTime, RigAnimator.Duration(RigAction.Throw, _config));
        }
    }
}
