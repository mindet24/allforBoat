using UnityEngine;

namespace Trouble.Game
{
    /// <summary>A one-off move of the player rig, started by a sim event.</summary>
    public enum RigAction
    {
        None,
        /// <summary>E picked something up: crouch, then lift it overhead.</summary>
        Lift,
        /// <summary>E put what was carried down.</summary>
        PutDown,
        /// <summary>Hold E threw what was carried.</summary>
        Throw,
        /// <summary>F plugged in or unplugged.</summary>
        Plug,
    }

    /// <summary>What the rig should show this frame.</summary>
    public struct RigState
    {
        /// <summary>Seconds, for the looping cycles (walk, breathe).</summary>
        public float Time;
        public bool Walking;
        public bool Flying;
        /// <summary>Walking up or down a flight of stairs: a quicker, bigger, forward-leaning step.</summary>
        public bool Climbing;
        public bool Carrying;
        public RigAction Action;
        /// <summary>How far through <see cref="Action"/>, 0..1.</summary>
        public float ActionT;
    }

    /// <summary>
    /// Joint angles and offsets of the cut-out rig. Arm "raise" is how far an arm is lifted from hanging (0) towards
    /// straight up (180), mirrored for the two sides by the view. Leg angles swing in the picture plane. Lean tilts the
    /// whole body forward (positive) along the facing direction.
    /// </summary>
    public struct RigPose
    {
        public float ArmRaiseL, ArmRaiseR;
        public float LegL, LegR;
        public float LegLiftL, LegLiftR;
        public float Head;
        public float Lean;
        /// <summary>Whole body up (positive) or down (negative, a crouch), metres.</summary>
        public float BodyY;
        /// <summary>Body height scale (breathing), 1 = drawn size.</summary>
        public float BodyScaleY;
    }

    /// <summary>Pure pose maths for the player's cut-out rig: the same inputs always give the same pose.</summary>
    public static class RigAnimator
    {
        /// <summary>The pose for <paramref name="s"/> with the numbers in <paramref name="c"/>.</summary>
        public static RigPose Evaluate(RigState s, RigConfig c)
        {
            var p = new RigPose { BodyScaleY = 1f };

            // Base: idle breathing, a walking cycle, or the flying pose.
            if (s.Flying)
            {
                p.ArmRaiseL = p.ArmRaiseR = c.flyArmRaise;
                p.LegL = c.flyLegSpread;
                p.LegR = -c.flyLegSpread;
                p.LegLiftL = p.LegLiftR = c.flyLegTuck;
            }
            else if (s.Walking || s.Climbing)
            {
                bool st = s.Climbing;
                float phase = s.Time * (st ? c.stairsStepRate : c.walkStepRate) * Mathf.PI; // one leg per half cycle
                float swing = Mathf.Sin(phase);
                float legSwing = st ? c.stairsLegSwing : c.walkLegSwing;
                float lift = st ? c.stairsLegLift : c.walkLegLift;
                float armSwing = st ? c.stairsArmSwing : c.walkArmSwing;
                p.LegL = legSwing * swing;
                p.LegR = -legSwing * swing;
                p.LegLiftL = lift * Mathf.Max(0f, swing);
                p.LegLiftR = lift * Mathf.Max(0f, -swing);
                p.ArmRaiseL = armSwing * Mathf.Max(0f, -swing);
                p.ArmRaiseR = armSwing * Mathf.Max(0f, swing);
                p.BodyY = (st ? c.stairsBob : c.walkBob) * Mathf.Abs(swing);
                p.Lean = st ? c.stairsLean : c.walkLean;
                p.Head = -p.Lean * 0.4f;
            }
            else
            {
                float breathe = Mathf.Sin(s.Time * c.breatheSpeed);
                p.BodyScaleY = 1f + c.breatheAmount * breathe;
                p.ArmRaiseL = p.ArmRaiseR = c.idleArmSway * (0.5f + 0.5f * breathe);
            }

            if (s.Carrying)
            {
                float sway = c.carryArmSway * Mathf.Sin(s.Time * c.breatheSpeed * 2f);
                p.ArmRaiseL = c.carryArmRaise + sway;
                p.ArmRaiseR = c.carryArmRaise - sway;
            }

            float t = Mathf.Clamp01(s.ActionT);
            float hump = Mathf.Sin(t * Mathf.PI);
            switch (s.Action)
            {
                case RigAction.Lift:
                {
                    float raise = Mathf.Lerp(0f, c.carryArmRaise, Smooth(t));
                    p.ArmRaiseL = p.ArmRaiseR = raise;
                    p.BodyY = -c.liftCrouch * hump;
                    break;
                }
                case RigAction.PutDown:
                {
                    float raise = Mathf.Lerp(c.carryArmRaise, 0f, Smooth(t));
                    p.ArmRaiseL = p.ArmRaiseR = raise;
                    p.BodyY = -c.putDownCrouch * hump;
                    break;
                }
                case RigAction.Throw:
                {
                    float w = Mathf.Clamp(c.throwWindUp, 0.05f, 0.95f);
                    float raise;
                    if (t < w)
                    {
                        float k = Smooth(t / w);
                        raise = Mathf.Lerp(c.carryArmRaise, c.throwWindUpRaise, k);
                        p.Lean = -c.throwLeanBack * k;
                    }
                    else
                    {
                        float k = (t - w) / (1f - w);
                        // Fast swing to the release, then the arms settle down.
                        raise = k < 0.4f
                            ? Mathf.Lerp(c.throwWindUpRaise, c.throwReleaseRaise, Smooth(k / 0.4f))
                            : Mathf.Lerp(c.throwReleaseRaise, 0f, Smooth((k - 0.4f) / 0.6f));
                        p.Lean = Mathf.Lerp(-c.throwLeanBack, c.throwLeanForward, Smooth(Mathf.Min(1f, k / 0.4f))) * (1f - Mathf.Max(0f, (k - 0.4f) / 0.6f));
                    }
                    p.ArmRaiseL = p.ArmRaiseR = raise;
                    break;
                }
                case RigAction.Plug:
                {
                    p.BodyY = -c.plugCrouch * hump;
                    p.ArmRaiseR = c.plugReach * hump;
                    p.Lean = c.walkLean * hump;
                    break;
                }
            }
            return p;
        }

        /// <summary>Seconds a one-off move lasts.</summary>
        public static float Duration(RigAction action, RigConfig c)
        {
            switch (action)
            {
                case RigAction.Lift: return c.liftTime;
                case RigAction.PutDown: return c.putDownTime;
                case RigAction.Throw: return c.throwTime;
                case RigAction.Plug: return c.plugTime;
                default: return 0f;
            }
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
