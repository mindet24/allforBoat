using System;

namespace Trouble.Game
{
    /// <summary>
    /// Pure rules of the game-feel layer: how an overload's counter maps to hum pitch, pulse speed and shake, how
    /// quick completions build a combo, the urgent ticking cadence, the popup easing, the slow-motion curve, the final
    /// stretch of the day and the relight flicker. No engine calls, so every rule has an EditMode test.
    /// </summary>
    public static class JuiceMath
    {
        /// <summary>How close an overload is to tripping: overTicks / tripTicks clamped to 0..1 (1 if tripTicks is 0).</summary>
        public static float OverloadFraction(int overTicks, int tripTicks)
        {
            if (tripTicks <= 0) return 1f;
            return Clamp01(overTicks / (float)tripTicks);
        }

        /// <summary>Linear blend from <paramref name="min"/> to <paramref name="max"/> by the overload fraction.</summary>
        public static float ByOverload(int overTicks, int tripTicks, float min, float max)
        {
            return min + (max - min) * OverloadFraction(overTicks, tripTicks);
        }

        /// <summary>Hum pitch: rises with the square of the overload fraction, so the last moments climb fastest.</summary>
        public static float HumPitch(int overTicks, int tripTicks, float min, float max)
        {
            float t = OverloadFraction(overTicks, tripTicks);
            return min + (max - min) * t * t;
        }

        /// <summary>
        /// Combo count after a completion at <paramref name="now"/>: one more than <paramref name="previousCount"/> if
        /// the last completion was at most <paramref name="window"/> seconds ago, else 1.
        /// </summary>
        public static int NextCombo(int previousCount, float lastTime, float now, float window)
        {
            if (previousCount <= 0 || now - lastTime > window || now < lastTime) return 1;
            return previousCount + 1;
        }

        /// <summary>Pitch of the n-th completion of a combo: 1 for the first, +step each, at most max.</summary>
        public static float ComboPitch(int combo, float step, float max)
        {
            float p = 1f + step * Math.Max(0, combo - 1);
            return Math.Min(p, Math.Max(1f, max));
        }

        /// <summary>
        /// Urgent ticking: true on whole seconds while 0 &lt; time left &lt;= <paramref name="warnSeconds"/>, and on
        /// half seconds too while time left &lt;= <paramref name="fastSeconds"/>.
        /// </summary>
        public static bool UrgentTickDue(int now, int dueTicks, int ticksPerSecond, float warnSeconds, float fastSeconds)
        {
            if (ticksPerSecond <= 0) return false;
            int left = dueTicks - now;
            if (left <= 0 || left > warnSeconds * ticksPerSecond) return false;
            if (left % ticksPerSecond == 0) return true;
            int half = ticksPerSecond / 2;
            return half > 0 && left <= fastSeconds * ticksPerSecond && left % half == 0;
        }

        /// <summary>True while the urgent tick is in its fast (last) phase.</summary>
        public static bool UrgentTickFast(int now, int dueTicks, int ticksPerSecond, float fastSeconds)
        {
            int left = dueTicks - now;
            return left > 0 && left <= fastSeconds * ticksPerSecond;
        }

        /// <summary>Ease-out-back, 0..1 to 0..1 with an overshoot past 1 (bigger <paramref name="overshoot"/> = more).</summary>
        public static float EaseOutBack(float t, float overshoot)
        {
            t = Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        /// <summary>Popup scale at <paramref name="age"/> seconds: pops in with overshoot, holds, then shrinks out.</summary>
        public static float PopupScale(float age, float life, float popSeconds, float overshoot)
        {
            if (age < 0f || age >= life || life <= 0f) return 0f;
            if (popSeconds > 0f && age < popSeconds) return EaseOutBack(age / popSeconds, overshoot);
            float outStart = life * 0.8f;
            if (age > outStart) return 1f - (age - outStart) / (life - outStart);
            return 1f;
        }

        /// <summary>Popup rise: eases out from 0 to <paramref name="rise"/> over the life.</summary>
        public static float PopupRise(float age, float life, float rise)
        {
            if (life <= 0f) return rise;
            float t = Clamp01(age / life);
            return rise * (1f - (1f - t) * (1f - t));
        }

        /// <summary>
        /// Squash and stretch at <paramref name="age"/> seconds after a hit: (x, y) scale factors. Starts squashed
        /// (wide and short), springs through a stretch and settles at (1, 1).
        /// </summary>
        public static void Squash(float age, float duration, float amount, out float x, out float y)
        {
            if (duration <= 0f || age < 0f || age >= duration)
            {
                x = 1f;
                y = 1f;
                return;
            }
            float t = age / duration;
            float s = amount * (float)Math.Cos(t * Math.PI * 2.5) * (1f - t);
            x = 1f + s;
            y = 1f - s;
        }

        /// <summary>Shake strength after a kick: <paramref name="kick"/> decaying linearly at <paramref name="decay"/> per second.</summary>
        public static float DecayShake(float trauma, float decay, float dt)
        {
            return Math.Max(0f, trauma - decay * dt);
        }

        /// <summary>The applied shake: the larger of the decaying kick and the steady shake, capped at <paramref name="max"/>.</summary>
        public static float ShakeAmount(float trauma, float steady, float max)
        {
            return Math.Min(Math.Max(trauma, steady), max);
        }

        /// <summary>
        /// Time scale during slow motion: <paramref name="low"/> at the start, easing back to 1 at
        /// <paramref name="duration"/> seconds (real time).
        /// </summary>
        public static float SlowMoScale(float elapsed, float duration, float low)
        {
            if (duration <= 0f || elapsed >= duration) return 1f;
            if (elapsed <= 0f) return low;
            float t = elapsed / duration;
            return low + (1f - low) * t * t;
        }

        /// <summary>0 before the last <paramref name="finalSeconds"/> of the day, rising to 1 at the end.</summary>
        public static float FinalStretch(int ticksLeft, int ticksPerSecond, float finalSeconds)
        {
            if (ticksPerSecond <= 0 || finalSeconds <= 0f) return 0f;
            float left = ticksLeft / (float)ticksPerSecond;
            if (left >= finalSeconds) return 0f;
            return Clamp01(1f - left / finalSeconds);
        }

        /// <summary>Music pitch: 1 before the final stretch, rising to <paramref name="finalPitch"/> at the end.</summary>
        public static float MusicPitch(float finalStretch, float finalPitch)
        {
            return 1f + (finalPitch - 1f) * Clamp01(finalStretch);
        }

        /// <summary>A pulse 0..1 (raised cosine) at <paramref name="hz"/> pulses per second.</summary>
        public static float Pulse(float time, float hz)
        {
            return 0.5f - 0.5f * (float)Math.Cos(time * hz * Math.PI * 2.0);
        }

        /// <summary>
        /// Darkness of a room while the house relights: 1 (dark) until its turn, then flickering, then 0 (lit).
        /// Room <paramref name="index"/> starts at index × <paramref name="stagger"/> seconds after the reset.
        /// The flicker is deterministic: it depends only on the time and the index.
        /// </summary>
        public static float RelightDarkness(float sinceReset, int index, float stagger, float flicker, float flickerHz)
        {
            float start = index * stagger;
            if (sinceReset < start) return 1f;
            float t = sinceReset - start;
            if (t >= flicker) return 0f;
            int step = (int)(t * flickerHz);
            // Mostly on, with a few dark frames that thin out as the flicker ends.
            uint h = (uint)(step * 73856093) ^ (uint)(index * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            float noise = (h & 1023) / 1023f;
            return noise < 0.55f * (1f - t / flicker) ? 1f : 0f;
        }

        /// <summary>Number of the spark particles to emit this frame from an accumulating fractional count.</summary>
        public static int TakeWhole(ref float accumulator)
        {
            int n = (int)Math.Floor(accumulator);
            if (n < 0) n = 0;
            accumulator -= n;
            return n;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
