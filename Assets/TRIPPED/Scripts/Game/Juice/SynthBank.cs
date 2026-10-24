using System;

namespace Trouble.Game
{
    /// <summary>Mixer group of a sound; each has its own volume in <see cref="JuiceConfig"/>.</summary>
    public enum SoundGroup { Sfx, Music, Ui, Voice }

    /// <summary>
    /// The day-mode (and UI) sound set, made in code so the game is never silent. A file Resources/Audio/{name}.wav
    /// or .ogg replaces a generated sound, and files {name}_1 … {name}_n are variants picked at random
    /// (see <see cref="SoundBank"/>). <see cref="Generate"/> is pure and deterministic (fixed noise seed per name).
    /// </summary>
    public static class SynthBank
    {
        // Player
        public const string Step = "step";
        public const string Jump = "jump";
        public const string Jetpack = "jetpack";
        public const string Pickup = "pickup";
        public const string PlugIn = "plug_in";
        public const string Reject = "reject";
        // Requests
        public const string RequestNewRelaxed = "request_new_relaxed";
        public const string RequestNewNormal = "request_new_normal";
        public const string RequestNewUrgent = "request_new_urgent";
        public const string RequestDone = "request_done";
        public const string RequestMissed = "request_missed";
        public const string UrgentTick = "urgent_tick";
        public const string HeartLost = "heart_lost";
        public const string ComboUp = "combo_up";
        // Power
        public const string OverloadHum = "overload_hum";
        public const string Spark = "spark";
        public const string Blackout = "blackout";
        public const string PowerUp = "power_up";
        public const string BreakerFlip = "breaker_flip";
        // Appliances
        public const string ToasterPop = "toaster_pop";
        /// <summary>Appliance types with a running loop named {type}_loop.</summary>
        public static readonly string[] LoopAppliances = { "kettle", "toaster", "microwave", "fridge", "tv", "heater", "washer", "dryer", "blender" };
        // UI
        public const string UiHover = "ui_hover";
        public const string UiClick = "ui_click";
        public const string UiBack = "ui_back";
        public const string UiPause = "ui_pause";
        public const string CountdownBeep = "countdown_beep";
        public const string DayStart = "day_start";
        // End
        public const string WinJingle = "win_jingle";
        public const string LoseJingle = "lose_jingle";
        public const string StarPop = "star_pop";
        public const string ScoreTick = "score_tick";
        // Music
        public const string MusicMenu = "music_menu";
        public const string MusicDay = "music_day";
        public const string MusicDayFinal = "music_day_final";
        // People
        public const string VoiceHappy = "voice_happy";
        public const string VoiceAngry = "voice_angry";
        public const string VoiceHey = "voice_hey";

        /// <summary>Loop name of an appliance type ("kettle" → "kettle_loop").</summary>
        public static string LoopOf(string applianceType) => applianceType + "_loop";

        /// <summary>Every sound of the set.</summary>
        public static readonly string[] Names = BuildNames();

        private static string[] BuildNames()
        {
            string[] fixedNames =
            {
                Step, Jump, Jetpack, Pickup, PlugIn, Reject,
                RequestNewRelaxed, RequestNewNormal, RequestNewUrgent, RequestDone, RequestMissed, UrgentTick, HeartLost, ComboUp,
                OverloadHum, Spark, Blackout, PowerUp, BreakerFlip, ToasterPop,
                UiHover, UiClick, UiBack, UiPause, CountdownBeep, DayStart,
                WinJingle, LoseJingle, StarPop, ScoreTick,
                MusicMenu, MusicDay, MusicDayFinal,
                VoiceHappy, VoiceAngry, VoiceHey,
            };
            var all = new string[fixedNames.Length + LoopAppliances.Length];
            fixedNames.CopyTo(all, 0);
            for (int i = 0; i < LoopAppliances.Length; i++) all[fixedNames.Length + i] = LoopOf(LoopAppliances[i]);
            return all;
        }

        /// <summary>True if <paramref name="name"/> is one of <see cref="Names"/>.</summary>
        public static bool Knows(string name) => Array.IndexOf(Names, name) >= 0;

        /// <summary>True for sounds played as seamless loops (hum, appliance loops, music).</summary>
        public static bool Loops(string name)
        {
            return name == OverloadHum || name == MusicMenu || name == MusicDay || name == MusicDayFinal
                || (name != null && name.EndsWith("_loop", StringComparison.Ordinal));
        }

        /// <summary>The mixer group a sound plays through.</summary>
        public static SoundGroup GroupOf(string name)
        {
            if (name == MusicMenu || name == MusicDay || name == MusicDayFinal) return SoundGroup.Music;
            if (name == VoiceHappy || name == VoiceAngry || name == VoiceHey) return SoundGroup.Voice;
            if (name != null && (name.StartsWith("ui_", StringComparison.Ordinal) || name == CountdownBeep || name == DayStart)) return SoundGroup.Ui;
            return SoundGroup.Sfx;
        }

        /// <summary>Music tempo of the generated loops.</summary>
        public const float DayBpm = 132f;
        public const float FinalBpm = 156f;
        public const float MenuBpm = 96f;

        /// <summary>Length in seconds of a generated sound (0 for an unknown name).</summary>
        public static float Duration(string name)
        {
            switch (name)
            {
                case Step: return 0.09f;
                case Jump: return 0.18f;
                case Jetpack: return 0.16f;
                case Pickup: return 0.2f;
                case PlugIn: return 0.11f;
                case Reject: return 0.28f;
                case RequestNewRelaxed: return 0.5f;
                case RequestNewNormal: return 0.45f;
                case RequestNewUrgent: return 0.36f;
                case RequestDone: return 0.6f;
                case RequestMissed: return 1.0f;
                case UrgentTick: return 0.05f;
                case HeartLost: return 0.45f;
                case ComboUp: return 0.24f;
                case OverloadHum: return 1f;
                case Spark: return 0.12f;
                case Blackout: return 1.4f;
                case PowerUp: return 0.8f;
                case BreakerFlip: return 0.09f;
                case ToasterPop: return 0.2f;
                case UiHover: return 0.035f;
                case UiClick: return 0.06f;
                case UiBack: return 0.1f;
                case UiPause: return 0.2f;
                case CountdownBeep: return 0.12f;
                case DayStart: return 0.7f;
                case WinJingle: return 1.5f;
                case LoseJingle: return 1.6f;
                case StarPop: return 0.18f;
                case ScoreTick: return 0.03f;
                case MusicMenu: return Bars(4, MenuBpm);
                case MusicDay: return Bars(4, DayBpm);
                case MusicDayFinal: return Bars(4, FinalBpm);
                case VoiceHappy: return 0.32f;
                case VoiceAngry: return 0.34f;
                case VoiceHey: return 0.26f;
            }
            if (name != null && name.EndsWith("_loop", StringComparison.Ordinal) && Array.IndexOf(LoopAppliances, name.Substring(0, name.Length - 5)) >= 0)
                return 1f;
            return 0f;
        }

        private static float Bars(int bars, float bpm) => bars * 4f * 60f / bpm;

        /// <summary>Number of samples of a sound at <paramref name="sampleRate"/>.</summary>
        public static int SampleCount(string name, int sampleRate) => (int)Math.Round(Duration(name) * sampleRate, MidpointRounding.AwayFromZero);

        /// <summary>Mono samples of one sound, normalised to <paramref name="peak"/>.</summary>
        /// <exception cref="ArgumentException">Unknown sound name.</exception>
        public static float[] Generate(string name, int sampleRate = ProceduralAudio.SampleRate, float peak = 1f)
        {
            int n = SampleCount(name, sampleRate);
            if (n <= 0) throw new ArgumentException($"Unknown sound '{name}'.", nameof(name));
            var s = new float[n];
            var g = new Gen(s, sampleRate, Seed(name));
            switch (name)
            {
                case Step:
                {
                    // Barefoot on a wooden floor: a soft fleshy thud from the ball of the foot, the bright slap of skin
                    // landing flat a few milliseconds later, and a dull pat under both. No click of a heel or sole.
                    int slap = (int)(0.006 * sampleRate);
                    g.Thump(0, n, 120, 58, 42, 0.7);
                    g.Noise(0, n, 0.45, 70, 0.25);
                    g.Noise(slap, n, 0.8, 150, 0.9);
                    break;
                }
                case Jump: g.Sweep(0, n, 240, 760, Wave.Triangle, 0.9, 6, true); g.Noise(0, n / 4, 0.4, 50, 0.3); break;
                case Jetpack: g.Noise(0, n, 0.8, 14, 0.25); g.Sweep(0, n, 180, 90, Wave.Triangle, 0.35, 10, true); break;
                case Pickup: g.Sweep(0, n, 320, 1100, Wave.Sine, 1, 6, true); g.Sweep(0, n, 640, 2200, Wave.Sine, 0.25, 6, true); break;
                case PlugIn:
                {
                    int second = (int)(0.035 * sampleRate);
                    g.Noise(0, second, 1, 60, 0.9); g.Tone(0, second, 1900, Wave.Square, 0.4, 60);
                    g.Noise(second, n, 0.9, 40, 0.5); g.Thump(second, n, 260, 120, 25, 0.9);
                    break;
                }
                case Reject:
                {
                    int split = (int)(0.13 * sampleRate);
                    g.Tone(0, split, 110, Wave.Square, 0.7, 2); g.Tone(0, split, 116, Wave.Saw, 0.5, 2);
                    g.Thump(split, n, 120, 45, 9, 1.0); g.Noise(split, n, 0.4, 14, 0.15);
                    break;
                }
                case RequestNewRelaxed:
                    g.Bell(0, n, 660, 0.7, 5); g.Bell((int)(0.12 * sampleRate), n, 880, 0.7, 5); break;
                case RequestNewNormal:
                    g.Bell(0, n, 1047, 1, 7); g.Bell((int)(0.09 * sampleRate), n, 1319, 0.8, 7); break;
                case RequestNewUrgent:
                {
                    int gap = (int)(0.1 * sampleRate);
                    for (int k = 0; k < 3; k++) g.Tone(k * gap, Math.Min(n, k * gap + gap), 1568 + k * 200, Wave.Square, 0.6, 22);
                    g.Bell(0, n, 3136, 0.3, 14);
                    break;
                }
                case RequestDone:
                {
                    int cha = (int)(0.06 * sampleRate);
                    g.Noise(0, cha, 0.8, 30, 0.95); g.Noise(cha, 2 * cha, 0.6, 30, 0.95);
                    int bell = 2 * cha;
                    g.Bell(bell, n, 1319, 0.8, 5); g.Bell(bell, n, 1760, 0.7, 5); g.Bell(bell, n, 2637, 0.5, 6);
                    g.Tone(bell, n, 5274, Wave.Sine, 0.12, 8);
                    break;
                }
                case RequestMissed:
                {
                    double[] notes = { 392, 370, 349, 311 };
                    int len = (int)(0.2 * sampleRate);
                    for (int k = 0; k < notes.Length; k++)
                    {
                        int a = k * len, b = k == notes.Length - 1 ? n : (k + 1) * len;
                        g.Horn(a, b, notes[k], k == notes.Length - 1 ? 7 : 0, 0.9);
                    }
                    g.LowPass(0.18);
                    break;
                }
                case UrgentTick: g.Tone(0, n, 2600, Wave.Square, 0.6, 60); g.Noise(0, n / 3, 0.6, 80, 0.9); break;
                case HeartLost: g.Thump(0, n, 110, 38, 6, 1.0); g.Noise(0, n / 3, 0.5, 18, 0.2); g.Tone(0, n, 55, Wave.Sine, 0.5, 6); break;
                case ComboUp:
                {
                    double[] notes = { 1047, 1319, 1568, 2093 };
                    int len = n / notes.Length;
                    for (int k = 0; k < notes.Length; k++) g.Tone(k * len, n, notes[k], Wave.Triangle, 0.6, 14);
                    break;
                }
                case OverloadHum:
                    // Whole cycles in 1 s, so the loop joins without a click.
                    g.Tone(0, n, 100, Wave.Saw, 0.6, 0); g.Tone(0, n, 200, Wave.Square, 0.2, 0); g.Tone(0, n, 50, Wave.Sine, 0.5, 0);
                    g.Crackle(0, n, 0.35, 40);
                    g.Am(0, n, 8, 0.25);
                    break;
                case Spark: g.Crackle(0, n, 1, 300); g.Noise(0, n, 0.5, 25, 0.95); break;
                case Blackout:
                {
                    int bzzt = (int)(0.28 * sampleRate);
                    int clunk = bzzt;
                    int sweep = (int)(0.34 * sampleRate);
                    g.Tone(0, bzzt, 90, Wave.Saw, 0.9, 0); g.Tone(0, bzzt, 180, Wave.Square, 0.4, 0); g.Crackle(0, bzzt, 0.9, 400);
                    g.Thump(clunk, n, 90, 40, 10, 1.2); g.Noise(clunk, clunk + (int)(0.12 * sampleRate), 0.8, 30, 0.3);
                    g.Sweep(sweep, n, 520, 35, Wave.Saw, 0.55, 2.2, false);
                    g.Clip(0.8);
                    break;
                }
                case PowerUp: g.Sweep(0, n, 70, 1000, Wave.Saw, 0.5, 0, false); g.Sweep(0, n, 140, 2000, Wave.Sine, 0.3, 0, false); g.FadeOut(0.25); g.FadeIn(0.5); break;
                case BreakerFlip: g.Noise(0, n / 2, 1, 50, 0.6); g.Thump(0, n, 240, 90, 30, 1); break;
                case ToasterPop:
                    g.Noise(0, n / 6, 0.8, 60, 0.9);
                    g.Vibrato(0, n, 620, 30, 18, 0.7, 9);
                    break;
                case UiHover: g.Tone(0, n, 1500, Wave.Sine, 0.7, 50); break;
                case UiClick: g.Tone(0, n, 1000, Wave.Triangle, 0.8, 45); g.Noise(0, n / 4, 0.4, 80, 0.9); break;
                case UiBack: g.Sweep(0, n, 760, 420, Wave.Triangle, 0.8, 18, false); break;
                case UiPause: g.Tone(0, n / 2, 880, Wave.Triangle, 0.7, 12); g.Tone(n / 2, n, 660, Wave.Triangle, 0.7, 12); break;
                case CountdownBeep: g.Tone(0, n, 880, Wave.Square, 0.5, 10); break;
                case DayStart:
                {
                    double[] notes = { 523, 659, 784, 1047 };
                    int len = (int)(0.09 * sampleRate);
                    for (int k = 0; k < notes.Length; k++) g.Bell(k * len, n, notes[k], 0.7, 4);
                    break;
                }
                case WinJingle:
                {
                    double[] notes = { 523, 659, 784, 1047 };
                    int len = (int)(0.13 * sampleRate);
                    for (int k = 0; k < notes.Length; k++) g.Tone(k * len, (k + 1) * len, notes[k], Wave.Square, 0.4, 3);
                    int chord = notes.Length * len;
                    g.Bell(chord, n, 1047, 0.6, 2.5); g.Bell(chord, n, 1319, 0.5, 2.5); g.Bell(chord, n, 1568, 0.5, 2.5);
                    g.Tone(chord, n, 262, Wave.Triangle, 0.5, 2.5);
                    break;
                }
                case LoseJingle:
                {
                    double[] notes = { 392, 311, 262, 196 };
                    int len = (int)(0.3 * sampleRate);
                    for (int k = 0; k < notes.Length; k++)
                        g.Horn(k * len, k == notes.Length - 1 ? n : (k + 1) * len, notes[k], k == notes.Length - 1 ? 6 : 0, 0.8);
                    g.LowPass(0.2);
                    break;
                }
                case StarPop: g.Noise(0, n / 5, 0.7, 60, 0.9); g.Bell(0, n, 1760, 0.8, 9); g.Bell(0, n, 2637, 0.4, 10); break;
                case ScoreTick: g.Tone(0, n, 2000, Wave.Triangle, 0.7, 60); break;
                case MusicDay: Music(g, n, sampleRate, DayBpm, false); break;
                case MusicDayFinal: Music(g, n, sampleRate, FinalBpm, true); break;
                case MusicMenu: MenuMusic(g, n, sampleRate); break;
                case VoiceHappy: g.Voice(0, n, 280, 460, 0.9); break;
                case VoiceAngry: g.Voice(0, n, 220, 140, 0.9); g.Am(0, n, 14, 0.5); break;
                case VoiceHey: g.Voice(0, n / 2, 360, 420, 0.9); g.Voice(n / 2, n, 420, 300, 0.9); break;
                default:
                    ApplianceLoop(g, n, name.Substring(0, name.Length - 5));
                    break;
            }
            if (!Loops(name)) g.EdgeFade(0.004);
            Normalise(s, peak);
            return s;
        }

        private static void ApplianceLoop(Gen g, int n, string type)
        {
            // All modulation rates are whole numbers of cycles per second so the 1 s loop joins seamlessly.
            switch (type)
            {
                case "kettle": g.Noise(0, n, 0.6, 0, 0.92); g.Am(0, n, 9, 0.6); g.Tone(0, n, 60, Wave.Sine, 0.2, 0); break;
                case "toaster": g.Tone(0, n, 60, Wave.Sine, 0.4, 0); g.Tone(0, n, 120, Wave.Sine, 0.2, 0); g.Crackle(0, n, 0.2, 6); break;
                case "microwave": g.Tone(0, n, 120, Wave.Saw, 0.4, 0); g.Tone(0, n, 240, Wave.Sine, 0.3, 0); g.Am(0, n, 2, 0.2); break;
                case "fridge": g.Tone(0, n, 50, Wave.Sine, 0.6, 0); g.Tone(0, n, 100, Wave.Saw, 0.15, 0); break;
                case "tv": g.Noise(0, n, 0.3, 0, 0.6); g.Tone(0, n, 15, Wave.Sine, 0.0, 0); g.Voice(0, n / 2, 220, 260, 0.3); g.Voice(n / 2, n, 260, 200, 0.3); break;
                case "heater": g.Noise(0, n, 0.7, 0, 0.97); g.Tone(0, n, 80, Wave.Sine, 0.2, 0); break;
                case "washer": g.Noise(0, n, 0.6, 0, 0.96); g.Am(0, n, 3, 0.8); g.Tone(0, n, 70, Wave.Sine, 0.3, 0); break;
                case "dryer": g.Noise(0, n, 0.5, 0, 0.95); g.Thumps(n, 2, 80, 0.8); break;
                case "blender": g.Tone(0, n, 300, Wave.Saw, 0.6, 0); g.Am(0, n, 6, 0.4); g.Noise(0, n, 0.2, 0, 0.5); break;
                default: g.Tone(0, n, 60, Wave.Sine, 0.4, 0); break;
            }
        }

        /// <summary>Four bars of an upbeat loop: kick, offbeat hats, a bouncing bass and a sixteenth-note arpeggio.</summary>
        private static void Music(Gen g, int n, int sr, float bpm, bool final)
        {
            double beat = 60.0 / bpm;
            int beatLen = (int)Math.Round(beat * sr);
            // C – Am – F – G, one bar each.
            double[][] chords =
            {
                new[] { 261.63, 329.63, 392.00 }, new[] { 220.00, 261.63, 329.63 },
                new[] { 174.61, 220.00, 261.63 }, new[] { 196.00, 246.94, 293.66 },
            };
            for (int b = 0; b < 16; b++)
            {
                int at = b * beatLen;
                if (at >= n) break;
                var chord = chords[b / 4];
                g.Thump(at, Math.Min(n, at + beatLen / 2), 130, 45, 14, 0.9);                      // kick
                g.Noise(at + beatLen / 2, Math.Min(n, at + beatLen / 2 + beatLen / 6), 0.25, 45, 0.98); // offbeat hat
                if (final) g.Noise(at, Math.Min(n, at + beatLen / 8), 0.12, 70, 0.98);
                if (b % 4 == 1 || b % 4 == 3) g.Noise(at, Math.Min(n, at + beatLen / 3), 0.35, 20, 0.6); // snare
                for (int e = 0; e < 2; e++)
                {
                    int s = at + e * beatLen / 2;
                    g.Tone(s, Math.Min(n, s + beatLen / 2), chord[0] / 2 * (e == 1 ? 2 : 1), Wave.Square, 0.22, 8); // bass
                }
                for (int q = 0; q < 4; q++)
                {
                    int s = at + q * beatLen / 4;
                    double hz = chord[(b * 4 + q) % 3] * 2;
                    g.Tone(s, Math.Min(n, s + beatLen / 4), hz, Wave.Triangle, final ? 0.16 : 0.12, 18); // arpeggio
                }
            }
        }

        /// <summary>A calm four-bar menu loop: soft pad chords and a gentle bass.</summary>
        private static void MenuMusic(Gen g, int n, int sr)
        {
            double beat = 60.0 / MenuBpm;
            int bar = (int)Math.Round(beat * 4 * sr);
            double[][] chords =
            {
                new[] { 261.63, 329.63, 392.00 }, new[] { 220.00, 261.63, 329.63 },
                new[] { 174.61, 220.00, 261.63 }, new[] { 196.00, 246.94, 293.66 },
            };
            for (int b = 0; b < 4; b++)
            {
                int at = b * bar, end = Math.Min(n, at + bar);
                foreach (var hz in chords[b]) g.Pad(at, end, hz, 0.15);
                g.Tone(at, end, chords[b][0] / 2, Wave.Triangle, 0.25, 1.5);
                int beatLen = bar / 4;
                for (int q = 0; q < 4; q++) g.Bell(at + q * beatLen, end, chords[b][q % 3] * 2, 0.08, 6);
            }
        }

        private static int Seed(string name)
        {
            int h = 17;
            foreach (char c in name) h = h * 31 + c;
            return h & 0x7fffffff;
        }

        private static void Normalise(float[] s, float peak)
        {
            float max = 0f;
            for (int i = 0; i < s.Length; i++) max = Math.Max(max, Math.Abs(s[i]));
            if (max <= 0f) return;
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
        }

        private enum Wave { Sine, Square, Saw, Triangle }

        /// <summary>A tiny additive synth writing into one buffer.</summary>
        private sealed class Gen
        {
            private readonly float[] _s;
            private readonly int _sr;
            private readonly Random _rng;

            public Gen(float[] s, int sr, int seed)
            {
                _s = s;
                _sr = sr;
                _rng = new Random(seed);
            }

            private static double Osc(Wave w, double phase)
            {
                double p = phase - Math.Floor(phase);
                switch (w)
                {
                    case Wave.Square: return p < 0.5 ? 1 : -1;
                    case Wave.Saw: return 2 * p - 1;
                    case Wave.Triangle: return 1 - 4 * Math.Abs(p - 0.5);
                    default: return Math.Sin(2 * Math.PI * p);
                }
            }

            private int Clamp(int i) => Math.Max(0, Math.Min(_s.Length, i));

            /// <summary>A steady tone with exponential decay <paramref name="decay"/> (per second; 0 = none).</summary>
            public void Tone(int a, int b, double hz, Wave w, double amp, double decay)
            {
                a = Clamp(a); b = Clamp(b);
                for (int i = a; i < b; i++)
                {
                    double t = (i - a) / (double)_sr;
                    _s[i] += (float)(amp * Osc(w, hz * t) * Math.Exp(-decay * t));
                }
            }

            /// <summary>A glide from one pitch to another (exponential when <paramref name="expo"/>).</summary>
            public void Sweep(int a, int b, double from, double to, Wave w, double amp, double decay, bool expo)
            {
                a = Clamp(a); b = Clamp(b);
                double phase = 0;
                int len = Math.Max(1, b - a);
                for (int i = a; i < b; i++)
                {
                    double u = (i - a) / (double)len;
                    double hz = expo ? from * Math.Pow(to / from, u) : from + (to - from) * u;
                    phase += hz / _sr;
                    double t = (i - a) / (double)_sr;
                    _s[i] += (float)(amp * Osc(w, phase) * Math.Exp(-decay * t));
                }
            }

            /// <summary>A pitch-dropping sine thump (kick, clunk, step).</summary>
            public void Thump(int a, int b, double from, double to, double decay, double amp)
            {
                a = Clamp(a); b = Clamp(b);
                double phase = 0;
                for (int i = a; i < b; i++)
                {
                    double t = (i - a) / (double)_sr;
                    double hz = to + (from - to) * Math.Exp(-t * 30);
                    phase += hz / _sr;
                    _s[i] += (float)(amp * Math.Sin(2 * Math.PI * phase) * Math.Exp(-decay * t));
                }
            }

            /// <summary>White noise through a one-pole filter: <paramref name="highPass"/> near 1 = bright hiss.</summary>
            public void Noise(int a, int b, double amp, double decay, double highPass)
            {
                a = Clamp(a); b = Clamp(b);
                double prev = 0, lp = 0;
                for (int i = a; i < b; i++)
                {
                    double t = (i - a) / (double)_sr;
                    double x = _rng.NextDouble() * 2 - 1;
                    lp += (x - lp) * (1 - highPass * 0.98);
                    double hp = highPass > 0.5 ? x - prev * highPass : lp;
                    prev = x;
                    _s[i] += (float)(amp * hp * Math.Exp(-decay * t));
                }
            }

            /// <summary>Random sharp clicks, <paramref name="perSecond"/> on average (electric crackle).</summary>
            public void Crackle(int a, int b, double amp, double perSecond)
            {
                a = Clamp(a); b = Clamp(b);
                double p = perSecond / _sr;
                for (int i = a; i < b; i++)
                {
                    if (_rng.NextDouble() >= p) continue;
                    int len = 20 + _rng.Next(60);
                    double sign = _rng.NextDouble() < 0.5 ? -1 : 1;
                    for (int k = 0; k < len && i + k < b; k++)
                        _s[i + k] += (float)(sign * amp * (1 - k / (double)len) * (_rng.NextDouble() * 2 - 1));
                }
            }

            /// <summary>A bell: sine plus an inharmonic partial, decaying.</summary>
            public void Bell(int a, int b, double hz, double amp, double decay)
            {
                Tone(a, b, hz, Wave.Sine, amp, decay);
                Tone(a, b, hz * 2.76, Wave.Sine, amp * 0.3, decay * 1.8);
            }

            /// <summary>A brassy horn note (saw with vibrato and a soft attack).</summary>
            public void Horn(int a, int b, double hz, double vibratoHz, double amp)
            {
                a = Clamp(a); b = Clamp(b);
                double phase = 0;
                int len = Math.Max(1, b - a);
                for (int i = a; i < b; i++)
                {
                    double t = (i - a) / (double)_sr;
                    double f = hz * (1 + 0.02 * Math.Sin(2 * Math.PI * vibratoHz * t));
                    phase += f / _sr;
                    double env = Math.Min(1, t / 0.03) * (1 - 0.4 * (i - a) / (double)len);
                    _s[i] += (float)(amp * env * (0.7 * Osc(Wave.Saw, phase) + 0.3 * Osc(Wave.Square, phase)));
                }
            }

            /// <summary>A tone with strong vibrato (a spring "boing").</summary>
            public void Vibrato(int a, int b, double hz, double depthHz, double rate, double amp, double decay)
            {
                a = Clamp(a); b = Clamp(b);
                double phase = 0;
                for (int i = a; i < b; i++)
                {
                    double t = (i - a) / (double)_sr;
                    phase += (hz + depthHz * Math.Sin(2 * Math.PI * rate * t)) / _sr;
                    _s[i] += (float)(amp * Math.Sin(2 * Math.PI * phase) * Math.Exp(-decay * t));
                }
            }

            /// <summary>A soft sustained pad (detuned sines with slow attack and release).</summary>
            public void Pad(int a, int b, double hz, double amp)
            {
                a = Clamp(a); b = Clamp(b);
                int len = Math.Max(1, b - a);
                for (int i = a; i < b; i++)
                {
                    double t = (i - a) / (double)_sr;
                    double u = (i - a) / (double)len;
                    double env = Math.Min(1, u / 0.15) * Math.Min(1, (1 - u) / 0.15);
                    _s[i] += (float)(amp * env * (Math.Sin(2 * Math.PI * hz * t) + 0.5 * Math.Sin(2 * Math.PI * hz * 1.004 * t)));
                }
            }

            /// <summary>A cartoon voice syllable: a buzzy glottal tone gliding in pitch through two formants.</summary>
            public void Voice(int a, int b, double from, double to, double amp)
            {
                a = Clamp(a); b = Clamp(b);
                double phase = 0;
                int len = Math.Max(1, b - a);
                for (int i = a; i < b; i++)
                {
                    double u = (i - a) / (double)len;
                    double hz = from + (to - from) * u;
                    phase += hz / _sr;
                    double env = Math.Sin(Math.PI * u);
                    double src = Osc(Wave.Saw, phase);
                    double f1 = Math.Sin(2 * Math.PI * phase * 3), f2 = Math.Sin(2 * Math.PI * phase * 7);
                    _s[i] += (float)(amp * env * (0.4 * src + 0.4 * f1 + 0.2 * f2));
                }
            }

            /// <summary>Regular low thumps (a tumbling drum), <paramref name="perSecond"/> per second.</summary>
            public void Thumps(int n, int perSecond, double hz, double amp)
            {
                int every = _sr / Math.Max(1, perSecond);
                for (int at = 0; at < n; at += every) Thump(at, Math.Min(n, at + every / 2), hz * 1.5, hz, 12, amp);
            }

            /// <summary>Amplitude modulation at <paramref name="hz"/> with <paramref name="depth"/> 0..1.</summary>
            public void Am(int a, int b, double hz, double depth)
            {
                a = Clamp(a); b = Clamp(b);
                for (int i = a; i < b; i++)
                {
                    double t = (i - a) / (double)_sr;
                    _s[i] *= (float)(1 - depth * (0.5 + 0.5 * Math.Sin(2 * Math.PI * hz * t)));
                }
            }

            /// <summary>One-pole low-pass over the whole buffer (0..1, smaller = darker).</summary>
            public void LowPass(double k)
            {
                double y = 0;
                for (int i = 0; i < _s.Length; i++)
                {
                    y += (_s[i] - y) * k;
                    _s[i] = (float)y;
                }
            }

            /// <summary>Soft clip (tanh) for a grittier sound.</summary>
            public void Clip(double drive)
            {
                for (int i = 0; i < _s.Length; i++) _s[i] = (float)Math.Tanh(_s[i] / drive);
            }

            /// <summary>Linear fade in over the first <paramref name="share"/> of the buffer.</summary>
            public void FadeIn(double share)
            {
                int len = (int)(_s.Length * share);
                for (int i = 0; i < len; i++) _s[i] *= (float)(0.3 + 0.7 * i / (double)len);
            }

            /// <summary>Linear fade out over the last <paramref name="share"/> of the buffer.</summary>
            public void FadeOut(double share)
            {
                int len = (int)(_s.Length * share);
                for (int i = 0; i < len; i++) _s[_s.Length - 1 - i] *= (float)(i / (double)len);
            }

            /// <summary>Very short fades at both ends so a one-shot never pops.</summary>
            public void EdgeFade(double seconds)
            {
                int fade = Math.Min(_s.Length / 4, (int)(seconds * _sr));
                for (int i = 0; i < fade; i++)
                {
                    float k = (float)i / fade;
                    _s[i] *= k;
                    _s[_s.Length - 1 - i] *= k;
                }
            }
        }
    }
}
