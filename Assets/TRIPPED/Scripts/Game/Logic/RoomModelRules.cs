using System;

namespace Trouble.Game
{
    /// <summary>
    /// How a furnished room model (Art/Models/Room_&lt;Id&gt;.fbx) is fitted into its sim room: at scale 1 when it was
    /// built at the room's real width, otherwise stretched in x; and which of its parts are left out.
    /// </summary>
    public static class RoomModelRules
    {
        /// <summary>
        /// x scale for a model <paramref name="modelWidth"/> metres wide in a room <paramref name="roomWidth"/> wide:
        /// 1 when they differ by at most <paramref name="tolerance"/>, else roomWidth / modelWidth. 1 for a model
        /// with no width.
        /// </summary>
        public static float ScaleX(float modelWidth, float roomWidth, float tolerance)
        {
            if (modelWidth <= 0.001f) return 1f;
            return Math.Abs(modelWidth - roomWidth) <= tolerance ? 1f : roomWidth / modelWidth;
        }

        /// <summary>
        /// True if a part named <paramref name="part"/> is left out: it is in <paramref name="always"/>, or the model
        /// was stretched (<paramref name="rescaled"/>) and it is in <paramref name="whenRescaled"/>.
        /// </summary>
        public static bool Hides(string part, string[] always, string[] whenRescaled, bool rescaled)
        {
            if (Contains(always, part)) return true;
            return rescaled && Contains(whenRescaled, part);
        }

        /// <summary>
        /// True if an optional part (its name contains "_Random") of a room model is there today: a fixed roll per
        /// part, room and day <paramref name="seed"/>, under <paramref name="chance"/>. Every other part is always there.
        /// </summary>
        public static bool ShowsRandomPart(string part, string roomId, int seed, float chance)
        {
            if (part == null || part.IndexOf("_Random", StringComparison.Ordinal) < 0) return true;
            // FNV-1a over room and part names, mixed with the seed: stable across runs and platforms.
            uint h = 2166136261;
            foreach (char c in roomId + "/" + part)
            {
                h ^= c;
                h *= 16777619;
            }
            h ^= (uint)seed;
            h *= 2654435761;
            return (h >> 8) / (float)(1 << 24) < chance;
        }

        private static bool Contains(string[] names, string name)
        {
            if (names == null) return false;
            foreach (var n in names)
                if (n == name) return true;
            return false;
        }
    }
}
