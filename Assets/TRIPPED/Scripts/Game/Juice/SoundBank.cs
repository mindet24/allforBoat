using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The clips of the <see cref="SynthBank"/> sounds. For each name, a file Resources/{folder}/{name} (.wav, .ogg …)
    /// replaces the generated clip, and files {name}_1 … {name}_N are variants: when any exist, one is picked at random
    /// on every play. Without a file, the generated clip plays, so no sound is ever missing.
    /// </summary>
    public sealed class SoundBank
    {
        private readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        private readonly HashSet<string> _fromFile = new HashSet<string>();
        private readonly System.Random _random = new System.Random();

        /// <summary>Loads every sound of <see cref="SynthBank.Names"/>.</summary>
        /// <param name="folder">Resources folder checked for files (TrippedViewConfig.audioFolder).</param>
        /// <param name="maxVariants">Highest variant number looked for.</param>
        public SoundBank(string folder, int maxVariants)
        {
            string prefix = string.IsNullOrEmpty(folder) ? "" : folder.TrimEnd('/') + "/";
            var found = new List<AudioClip>();
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
                if (found.Count > 0) _fromFile.Add(name);
                else found.Add(Make(name));
                _clips[name] = found.ToArray();
            }
        }

        /// <summary>True if the sound comes from a file (or files) rather than the generator.</summary>
        public bool IsFromFile(string name) => _fromFile.Contains(name);

        /// <summary>True if the name is in the bank.</summary>
        public bool Has(string name) => name != null && _clips.ContainsKey(name);

        /// <summary>A clip of the sound (a random variant when there are several), or null for an unknown name.</summary>
        public AudioClip Pick(string name)
        {
            if (name == null || !_clips.TryGetValue(name, out var list) || list.Length == 0) return null;
            return list.Length == 1 ? list[0] : list[_random.Next(list.Length)];
        }

        private static AudioClip Make(string name)
        {
            var samples = SynthBank.Generate(name);
            var clip = AudioClip.Create(name, samples.Length, 1, ProceduralAudio.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
