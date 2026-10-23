using System;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The game's sounds, made in code from the formulas of GDD v1.0 §7.2 at 44100 Hz, so no audio file is needed.
    /// A file with the same name in Resources/{audioFolder}/ (for example Resources/Audio/click.wav) replaces the
    /// generated sound.
    /// </summary>
    /// <remarks>
    /// <see cref="Generate"/> is pure (no engine call) and deterministic: the noise comes from a fixed seed, so the
    /// same name always gives the same samples. Generated clips are normalised to a peak of 1; how loud each sound
    /// plays is set by the volumes in <see cref="TrippedViewConfig"/>.
    /// </remarks>
    public static class ProceduralAudio
    {
        /// <summary>Sample rate of every generated clip.</summary>
        public const int SampleRate = 44100;

        public const string Click = "click";
        public const string Buzz = "buzz";
        public const string Clunk = "clunk";
        public const string Chime = "chime";
        public const string Wrong = "wrong";
        public const string Ding = "ding";
        public const string Tick = "tick";
        public const string Nope = "nope";
        public const string FailSmall = "fail_small";
        public const string Win = "win";
        public const string Lose = "lose";

        /// <summary>Every sound the game knows, in GDD v1.0 §7.2 order.</summary>
        public static readonly string[] Names = { Click, Buzz, Clunk, Chime, Wrong, Ding, Tick, Nope, FailSmall, Win, Lose };

        /// <summary>Fade in and out at the ends of one-shot sounds, so they start and stop without a pop.</summary>
        private const float EdgeFadeSeconds = 0.004f;

        /// <summary>Length of a sound in seconds (GDD v1.0 §7.2), or 0 for an unknown name.</summary>
        public static float Duration(string name)
        {
            switch (name)
            {
                case Click: return 0.03f;
                case Buzz: return 0.5f;
                case Clunk: return 0.15f;
                case Chime: return 0.2f;
                case Wrong: return 0.25f;
                case Ding: return 0.4f;
                case Tick: return 0.02f;
                case Nope: return 0.1f;
                case FailSmall: return 0.3f;
                case Win: return 0.5f;
                case Lose: return 0.8f;
                default: return 0f;
            }
        }

        /// <summary>True for sounds that play as a seamless loop (buzz).</summary>
        public static bool Loops(string name) => name == Buzz;

        /// <summary>Number of samples a sound has at <paramref name="sampleRate"/>.</summary>
        public static int SampleCount(string name, int sampleRate = SampleRate)
        {
            return (int)Math.Round(Duration(name) * sampleRate, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Mono samples of one sound, scaled so the loudest sample is exactly <paramref name="peak"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Unknown sound name.</exception>
        public static float[] Generate(string name, int sampleRate = SampleRate, float peak = 1f)
        {
            int n = SampleCount(name, sampleRate);
            if (n <= 0) throw new ArgumentException($"Unknown sound '{name}'.", nameof(name));
            var s = new float[n];
            double dt = 1.0 / sampleRate;
            var random = new System.Random(7919);
            switch (name)
            {
                case Click:
                    for (int i = 0; i < n; i++) s[i] = (float)(Sine(1200, i * dt) * (1.0 - (double)i / n));
                    break;
                case Buzz:
                    // 120 Hz × 0.5 s = 60 whole cycles, so the loop joins without a click.
                    for (int i = 0; i < n; i++)
                    {
                        double phase = i * dt * 120.0;
                        double saw = 2.0 * (phase - Math.Floor(phase + 0.5));
                        s[i] = (float)(0.9 * saw + 0.1 * (random.NextDouble() * 2.0 - 1.0));
                    }
                    break;
                case Clunk:
                {
                    var noise = new double[n];
                    for (int i = 0; i < n; i++) noise[i] = random.NextDouble() * 2.0 - 1.0;
                    for (int i = 0; i < n; i++)
                    {
                        double sum = 0;
                        int count = 0;
                        for (int k = 0; k < 8 && i - k >= 0; k++, count++) sum += noise[i - k];
                        double lowPassed = sum / count;
                        double env = Math.Exp(-6.0 * i / n);
                        s[i] = (float)((0.5 * lowPassed * 2.5 + 0.5 * Sine(80, i * dt)) * env);
                    }
                    break;
                }
                case Chime:
                {
                    int split = (int)Math.Round(0.08 * sampleRate);
                    for (int i = 0; i < n; i++) s[i] = (float)(i < split ? Sine(880, i * dt) : Sine(1320, (i - split) * dt) * (1.0 - (double)(i - split) / (n - split)));
                    break;
                }
                case Wrong:
                    for (int i = 0; i < n; i++) s[i] = (float)Square(i < n / 2 ? 330 : 247, i * dt);
                    break;
                case Ding:
                    for (int i = 0; i < n; i++) s[i] = (float)(Sine(1568, i * dt) * Math.Exp(-8.0 * i / n));
                    break;
                case Tick:
                    for (int i = 0; i < n; i++) s[i] = (float)Sine(2000, i * dt);
                    break;
                case Nope:
                    for (int i = 0; i < n; i++) s[i] = (float)Square(180, i * dt);
                    break;
                case FailSmall:
                    Sweep(s, 600, 200, dt);
                    break;
                case Win:
                {
                    double[] notes = { 523.25, 659.25, 783.99, 1046.50 };
                    int noteLength = (int)Math.Round(0.12 * sampleRate);
                    for (int i = 0; i < n; i++)
                    {
                        int note = Math.Min(i / noteLength, notes.Length - 1);
                        int local = i - note * noteLength;
                        double env = note < notes.Length - 1 ? 1.0 : Math.Max(0.0, 1.0 - (double)local / (n - note * noteLength));
                        s[i] = (float)(Sine(notes[note], local * dt) * env);
                    }
                    break;
                }
                case Lose:
                    Sweep(s, 400, 80, dt);
                    for (int i = 0; i < n; i++) s[i] *= (float)(1.0 - 0.6 * i / n);
                    break;
            }
            if (!Loops(name)) FadeEdges(s, sampleRate);
            Normalise(s, peak);
            return s;
        }

        /// <summary>
        /// The clip for a sound: the file Resources/{folder}/{name} if there is one, otherwise a generated clip.
        /// </summary>
        public static AudioClip Load(string name, string folder)
        {
            if (!string.IsNullOrEmpty(folder))
            {
                var file = Resources.Load<AudioClip>(folder.TrimEnd('/') + "/" + name);
                if (file != null) return file;
            }
            var samples = Generate(name);
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static double Sine(double hz, double t) => Math.Sin(2.0 * Math.PI * hz * t);

        private static double Square(double hz, double t) => Sine(hz, t) >= 0 ? 1.0 : -1.0;

        /// <summary>A sine whose pitch glides linearly from <paramref name="fromHz"/> to <paramref name="toHz"/>.</summary>
        private static void Sweep(float[] s, double fromHz, double toHz, double dt)
        {
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double hz = fromHz + (toHz - fromHz) * i / s.Length;
                phase += 2.0 * Math.PI * hz * dt;
                s[i] = (float)Math.Sin(phase);
            }
        }

        private static void FadeEdges(float[] s, int sampleRate)
        {
            int fade = Math.Min(s.Length / 4, (int)(EdgeFadeSeconds * sampleRate));
            for (int i = 0; i < fade; i++)
            {
                float g = (float)i / fade;
                s[i] *= g;
                s[s.Length - 1 - i] *= g;
            }
        }

        private static void Normalise(float[] s, float peak)
        {
            float max = 0f;
            for (int i = 0; i < s.Length; i++) max = Math.Max(max, Math.Abs(s[i]));
            if (max <= 0f) return;
            float scale = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= scale;
        }
    }
}
