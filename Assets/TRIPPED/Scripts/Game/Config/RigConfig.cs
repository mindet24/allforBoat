using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Every number of the player's cut-out rig animation (Resources/Config/RigConfig.asset). Angles in degrees,
    /// distances in metres, times in seconds. Edited in Tools > TRIPPED > Studio; changes show at once in Play mode.
    /// </summary>
    [CreateAssetMenu(menuName = "TRIPPED/Rig Config", fileName = "RigConfig")]
    public sealed class RigConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/RigConfig";

        [Tooltip("Use the cut-out rig (Art/Characters/Rig/player_*.png) instead of the single player sprite.")]
        public bool useRig = true;
        [Tooltip("Resources folder holding player_{head,body,armL,armR,legL,legR}.png and player_rig.json.")]
        public string rigFolder = "Art/Characters/Rig";

        [Header("Idle")]
        public float breatheAmount = 0.015f;
        public float breatheSpeed = 2.4f;
        public float idleArmSway = 3f;

        [Header("Walk")]
        [Tooltip("Steps per second (one step = one leg).")]
        public float walkStepRate = 4f;
        public float walkLegSwing = 14f;
        [Tooltip("How high a stepping foot lifts, metres.")]
        public float walkLegLift = 0.06f;
        public float walkArmSwing = 16f;
        public float walkBob = 0.035f;
        public float walkLean = 4f;

        [Header("Run up / down stairs")]
        public float stairsStepRate = 6f;
        public float stairsLegSwing = 22f;
        public float stairsLegLift = 0.12f;
        public float stairsArmSwing = 26f;
        public float stairsBob = 0.06f;
        public float stairsLean = 10f;

        [Header("Flying (jetpack: arms out, legs dangling)")]
        [Tooltip("How far the arms are raised in the air (0 = hanging, 180 = straight up).")]
        public float flyArmRaise = 70f;
        [Tooltip("How far both feet tuck up in the air, metres.")]
        public float flyLegTuck = 0.05f;
        [Tooltip("Legs apart in the air, degrees each side.")]
        public float flyLegSpread = 8f;

        [Header("Carry (arms up, the appliance over the head)")]
        [Tooltip("How far the arms are raised while carrying (0 = hanging, 180 = straight up).")]
        public float carryArmRaise = 160f;
        public float carryArmSway = 6f;

        [Header("Lift (E: pick up)")]
        public float liftTime = 0.35f;
        public float liftCrouch = 0.14f;

        [Header("Put down (E)")]
        public float putDownTime = 0.3f;
        public float putDownCrouch = 0.12f;

        [Header("Throw (hold E)")]
        public float throwTime = 0.45f;
        [Tooltip("Part of the throw spent winding up (0..1).")]
        [Range(0.05f, 0.9f)] public float throwWindUp = 0.4f;
        [Tooltip("Arm raise at the end of the wind-up (past straight up = behind the head).")]
        public float throwWindUpRaise = 200f;
        [Tooltip("Arm raise at the release, pointing ahead.")]
        public float throwReleaseRaise = 70f;
        public float throwLeanBack = 8f;
        public float throwLeanForward = 14f;

        [Header("Plug in / unplug (F)")]
        public float plugTime = 0.35f;
        public float plugCrouch = 0.18f;
        [Tooltip("How far the reaching arm swings out to the socket.")]
        public float plugReach = 45f;

        /// <summary>The asset under Resources, or a default instance if there is none.</summary>
        public static RigConfig LoadOrDefault()
        {
            var c = Resources.Load<RigConfig>(ResourcePath);
            return c != null ? c : CreateInstance<RigConfig>();
        }
    }
}
