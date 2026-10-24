using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>Pure rules of the Settings screen.</summary>
    public static class SettingsRules
    {
        /// <summary>A volume in percent after moving it one step (clamped to 0..100).</summary>
        public static int StepVolume(int percent, int direction, int step)
        {
            return Mathf.Clamp(percent + direction * Mathf.Max(1, step), 0, 100);
        }

        /// <summary>Percent to the 0..1 factor the audio code multiplies by.</summary>
        public static float Factor(int percent) => Mathf.Clamp01(percent / 100f);

        /// <summary>The next index in a list of <paramref name="count"/>, clamped at both ends (no wrap).</summary>
        public static int StepIndex(int index, int direction, int count)
        {
            if (count <= 0) return -1;
            return Mathf.Clamp(index + direction, 0, count - 1);
        }

        /// <summary>Distinct sizes, smallest first (refresh rates collapsed).</summary>
        public static List<Vector2Int> DistinctSizes(IEnumerable<Vector2Int> sizes)
        {
            var list = new List<Vector2Int>();
            foreach (var s in sizes)
                if (s.x > 0 && s.y > 0 && !list.Contains(s)) list.Add(s);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        /// <summary>Index of the size closest to <paramref name="want"/> (by pixel count), or -1 for an empty list.</summary>
        public static int ClosestSize(IReadOnlyList<Vector2Int> sizes, Vector2Int want)
        {
            int best = -1;
            long bestDiff = long.MaxValue;
            for (int i = 0; i < sizes.Count; i++)
            {
                long d = System.Math.Abs((long)sizes[i].x * sizes[i].y - (long)want.x * want.y) + System.Math.Abs(sizes[i].x - want.x);
                if (d < bestDiff) { bestDiff = d; best = i; }
            }
            return best;
        }
    }

    /// <summary>A volume channel of the Settings screen. Music, SFX, UI and Voice match G5's mixer groups.</summary>
    public enum VolumeChannel { Master, Music, Sfx, Ui, Voice }

    /// <summary>
    /// The player's settings, kept in PlayerPrefs (through <see cref="IProgressStore"/>, so tests use a dictionary):
    /// five volumes in percent, camera shake on/off, fullscreen and resolution. <see cref="Current"/> is what the audio
    /// and the camera shake read at runtime (G5 hooks: <see cref="GroupFactor"/> and <see cref="ShakeEnabled"/>).
    /// </summary>
    public sealed class UserSettings
    {
        private const string Prefix = "tripped.settings.";
        private readonly IProgressStore _store;

        /// <summary>The settings in use; defaults (everything at 100%, shake on) until the game sets them.</summary>
        public static UserSettings Current { get; set; } = new UserSettings(null);

        public UserSettings(IProgressStore store)
        {
            _store = store;
        }

        private int Get(string key, int fallback) => _store != null ? _store.GetInt(Prefix + key, fallback) : fallback;

        private void Set(string key, int value)
        {
            if (_store == null) return;
            _store.SetInt(Prefix + key, value);
            _store.Save();
        }

        /// <summary>Volume of a channel in percent (default 100).</summary>
        public int Volume(VolumeChannel channel) => Mathf.Clamp(Get("vol." + channel, 100), 0, 100);

        /// <summary>Sets a channel's volume (clamped) and applies the master volume at once.</summary>
        public void SetVolume(VolumeChannel channel, int percent)
        {
            Set("vol." + channel, Mathf.Clamp(percent, 0, 100));
            if (channel == VolumeChannel.Master) ApplyMaster();
        }

        /// <summary>Camera shake on (default) or off, for players sensitive to motion.</summary>
        public bool CameraShake
        {
            get => Get("shake", 1) != 0;
            set => Set("shake", value ? 1 : 0);
        }

        /// <summary>Saved fullscreen choice (default: whatever the player started with).</summary>
        public bool Fullscreen
        {
            get => Get("fullscreen", Screen.fullScreen ? 1 : 0) != 0;
            set => Set("fullscreen", value ? 1 : 0);
        }

        /// <summary>Saved resolution, or (0, 0) for "leave it".</summary>
        public Vector2Int Resolution
        {
            get => new Vector2Int(Get("resW", 0), Get("resH", 0));
            set { Set("resW", value.x); Set("resH", value.y); }
        }

        /// <summary>The factor G5's audio multiplies a mixer group by (Music, SFX, UI, Voice). Master is applied through the listener.</summary>
        public static float GroupFactor(SoundGroup group)
        {
            var s = Current;
            switch (group)
            {
                case SoundGroup.Music: return SettingsRules.Factor(s.Volume(VolumeChannel.Music));
                case SoundGroup.Ui: return SettingsRules.Factor(s.Volume(VolumeChannel.Ui));
                case SoundGroup.Voice: return SettingsRules.Factor(s.Volume(VolumeChannel.Voice));
                default: return SettingsRules.Factor(s.Volume(VolumeChannel.Sfx));
            }
        }

        /// <summary>False when the player turned camera shake off.</summary>
        public static bool ShakeEnabled => Current.CameraShake;

        /// <summary>Applies everything that lives outside the audio mixer: master volume, fullscreen and resolution.</summary>
        public void ApplyAll()
        {
            ApplyMaster();
            if (Application.isEditor) return; // the Game view sets its own size
            var res = Resolution;
            if (res.x > 0 && res.y > 0) Screen.SetResolution(res.x, res.y, Fullscreen);
            else if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        }

        private void ApplyMaster() => AudioListener.volume = SettingsRules.Factor(Volume(VolumeChannel.Master));
    }
}
