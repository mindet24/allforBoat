using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The clips of the <see cref="SynthBank"/> sounds. For each name, a file Resources/{folder}/{name} (.wav, .ogg …)
    /// replaces the generated clip, and files {name}_1 … {name}_N are variants: when any exist, one is picked at random
    /// on every play. Without a file, the generated clip plays, so no sound is ever missing.
    /// </summary>
    /// <remarks>
    /// Generating the whole set takes about 0.4 s, so it is not done in the constructor. The samples are synthesised on
    /// a worker thread (<see cref="SynthBank.Generate"/> is pure), UI sounds first; the music tracks only once asked
    /// for (<see cref="IsReady"/>). Each clip is created on the main thread the first time it is picked. A sound picked before the worker reached it is synthesised on the spot,
    /// so <see cref="Pick"/> never returns null for a known name. Create and use on the main thread only.
    /// <code>
    /// var bank = new SoundBank("Audio", 8);
    /// source.PlayOneShot(bank.Pick(SynthBank.UiClick));
    /// </code>
    /// </remarks>
    public sealed class SoundBank
    {
        private readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        private readonly HashSet<string> _fromFile = new HashSet<string>();
        private readonly HashSet<string> _generated = new HashSet<string>();
        private readonly System.Random _random = new System.Random();

        // Shared with the worker thread; guarded by _lock.
        private readonly object _lock = new object();
        private readonly Dictionary<string, float[]> _ready = new Dictionary<string, float[]>();
        private readonly HashSet<string> _claimed = new HashSet<string>();

        /// <summary>Loads every sound of <see cref="SynthBank.Names"/> (files now, generated sounds in the background).</summary>
        /// <param name="folder">Resources folder checked for files (TrippedViewConfig.audioFolder).</param>
        /// <param name="maxVariants">Highest variant number looked for.</param>
        public SoundBank(string folder, int maxVariants)
        {
            string prefix = string.IsNullOrEmpty(folder) ? "" : folder.TrimEnd('/') + "/";
            var found = new List<AudioClip>();
            var toGenerate = new List<string>();
            foreach (var name in SynthBank.Names)
            {
                found.Clear();
                for (int v = 1; v <= maxVariants; v++)
                {
                    var clip = Resources.Load<AudioClip>(prefix + name + "_" + v);
                    if (clip != null) found.Add(clip);
                }
                if (found.Count == 0)
                {
                    var file = Resources.Load<AudioClip>(prefix + name);
                    if (file != null) found.Add(file);
                }
                if (found.Count > 0)
                {
                    _fromFile.Add(name);
                    _clips[name] = found.ToArray();
                }
                else
                {
                    _generated.Add(name);
                    toGenerate.Add(name);
                }
            }
            // Music tracks are the big ones (about 1–1.7 MB each) and a session may never play some of them: they are
            // made only when first asked for (IsReady / Pick). Everything else is made now, UI sounds first.
            toGenerate.RemoveAll(n => SynthBank.GroupOf(n) == SoundGroup.Music);
            toGenerate.Sort((a, b) => Priority(a).CompareTo(Priority(b)));
            Queue(toGenerate.ToArray());
        }

        private static int Priority(string name) => SynthBank.GroupOf(name) == SoundGroup.Ui ? 0 : 1;

        private void Queue(string[] names)
        {
            try
            {
                ThreadPool.QueueUserWorkItem(_ => GenerateAll(names));
            }
            catch (System.Exception)
            {
                // No worker available (some platforms): every clip is then made on its first Pick.
            }
        }

        private void GenerateAll(string[] order)
        {
            foreach (var name in order)
            {
                lock (_lock)
                {
                    if (!_claimed.Add(name)) continue;
                }
                float[] samples;
                try
                {
                    samples = SynthBank.Generate(name);
                }
                catch (System.Exception)
                {
                    samples = null;
                }
                lock (_lock)
                {
                    if (samples != null) _ready[name] = samples;
                    else _claimed.Remove(name); // let the main thread try (and report) it
                }
            }
        }

        /// <summary>True if the sound comes from a file (or files) rather than the generator.</summary>
        public bool IsFromFile(string name) => _fromFile.Contains(name);

        /// <summary>
        /// True if <see cref="Pick"/> can return this sound now without synthesising it on the calling thread (a file,
        /// a clip already made, or samples the worker finished). False for an unknown name. A generated sound nobody
        /// has started yet is handed to the worker, so polling this each frame makes it ready a little later.
        /// </summary>
        /// <example><code>if (bank.IsReady(SynthBank.MusicMenu)) music.clip = bank.Pick(SynthBank.MusicMenu);</code></example>
        public bool IsReady(string name)
        {
            if (name == null) return false;
            if (_clips.ContainsKey(name)) return true;
            if (!_generated.Contains(name)) return false;
            bool start;
            lock (_lock)
            {
                if (_ready.ContainsKey(name)) return true;
                start = !_claimed.Contains(name);
            }
            if (start) Queue(new[] { name });
            return false;
        }

        /// <summary>True if the name is in the bank.</summary>
        public bool Has(string name) => name != null && (_clips.ContainsKey(name) || _generated.Contains(name));

        /// <summary>A clip of the sound (a random variant when there are several), or null for an unknown name.</summary>
        public AudioClip Pick(string name)
        {
            if (name == null) return null;
            if (!_clips.TryGetValue(name, out var list))
            {
                if (!_generated.Contains(name)) return null;
                list = new[] { Make(name) };
                _clips[name] = list;
            }
            if (list.Length == 0) return null;
            return list.Length == 1 ? list[0] : list[_random.Next(list.Length)];
        }

        /// <summary>Creates the clip of a generated sound: the worker's samples if ready, otherwise synthesised now.</summary>
        private AudioClip Make(string name)
        {
            float[] samples = null;
            bool mine = false;
            lock (_lock)
            {
                if (_ready.TryGetValue(name, out samples)) _ready.Remove(name);
                else mine = _claimed.Add(name);
            }
            if (samples == null)
            {
                // The worker is on this very sound: wait for it rather than doing the work twice.
                if (!mine)
                {
                    while (true)
                    {
                        lock (_lock)
                        {
                            if (_ready.TryGetValue(name, out samples))
                            {
                                _ready.Remove(name);
                                break;
                            }
                            if (!_claimed.Contains(name))
                            {
                                _claimed.Add(name);
                                break;
                            }
                        }
                        Thread.Sleep(1);
                    }
                }
                if (samples == null) samples = SynthBank.Generate(name);
            }
            var clip = AudioClip.Create(name, samples.Length, 1, ProceduralAudio.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
