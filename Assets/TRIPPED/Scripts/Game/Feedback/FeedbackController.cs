using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Trouble.Game
{
    /// <summary>
    /// Plays the sounds of GDD v1.0 §7.1 and shows the first-time tips. It listens to the
    /// <see cref="LevelController"/> events and never changes sim state. M mutes and unmutes.
    /// </summary>
    /// <remarks>
    /// Visual feedback in the level belongs to the world and HUD views; this class does sound and tips only.
    /// Every play is recorded in <see cref="PlayLog"/> so a check can prove a sound played without hearing it.
    /// </remarks>
    public sealed class FeedbackController : MonoBehaviour
    {
        /// <summary>PlayerPrefs key of the mute switch (1 = muted).</summary>
        public const string MutedKey = "trouble.muted";

        private const int PlayLogCapacity = 300;

        private struct Scheduled
        {
            public string Sound;
            public float At;
        }

        private LevelController _controller;
        private TrippedViewConfig _config;
        private AudioSource _oneShot;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, AudioSource> _buzz = new Dictionary<string, AudioSource>();
        private readonly List<Scheduled> _scheduled = new List<Scheduled>();
        private readonly List<string> _scratch = new List<string>();
        private readonly List<string> _playLog = new List<string>();
        private readonly List<string> _buzzKeys = new List<string>();
        private readonly EventLog _events = new EventLog(30);

        /// <summary>First-time tips; the F1 panel toggles <see cref="TipTracker.Enabled"/>.</summary>
        public TipTracker Tips { get; } = new TipTracker();

        /// <summary>The last 30 sim events, for the F1 panel.</summary>
        public EventLog Events => _events;

        /// <summary>Names of the sounds played since start (one-shots and buzz starts), oldest first, capped.</summary>
        public IReadOnlyList<string> PlayLog => _playLog;

        /// <summary>Total number of plays of each sound since start.</summary>
        public Dictionary<string, int> PlayCounts { get; } = new Dictionary<string, int>();

        /// <summary>true = also write each play to the Console (checks without speakers).</summary>
        public bool LogToConsole { get; set; }

        /// <summary>Sound is off (M).</summary>
        public bool Muted { get; private set; }

        /// <summary>Number of buzz loops running now.</summary>
        public int ActiveBuzzCount => _buzz.Count;

        /// <summary>Connects to the controller. Call once, before the first level loads.</summary>
        public void Setup(LevelController controller, TrippedViewConfig config)
        {
            _controller = controller;
            _config = config;
            Muted = SafeGetMuted();

            if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
                Camera.main.gameObject.AddComponent<AudioListener>();

            _oneShot = gameObject.AddComponent<AudioSource>();
            _oneShot.playOnAwake = false;
            _oneShot.spatialBlend = 0f;

            foreach (var name in ProceduralAudio.Names)
                _clips[name] = ProceduralAudio.Load(name, config.audioFolder);

            controller.LevelLoaded += OnLevelLoaded;
            controller.LevelUnloaded += StopAll;
            controller.TickEvents += OnTickEvents;
            controller.CommandAccepted += OnCommandAccepted;
        }

        /// <summary>The clip of a sound (generated or from a file), or null if the name is unknown.</summary>
        public AudioClip Clip(string sound) => _clips.TryGetValue(sound, out var c) ? c : null;

        /// <summary>Mutes or unmutes everything, and remembers the choice.</summary>
        public void SetMuted(bool muted)
        {
            Muted = muted;
            if (muted) _oneShot.Stop();
            foreach (var kv in _buzz) kv.Value.mute = muted;
            try
            {
                PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            }
            catch (System.Exception)
            {
                // Saving the mute switch is a convenience; never break the game over it.
            }
        }

        /// <summary>Plays a one-shot sound at its configured volume. Does nothing when muted.</summary>
        public void Play(string sound)
        {
            if (!_clips.TryGetValue(sound, out var clip) || clip == null) return;
            Record(sound);
            if (Muted) return;
            _oneShot.PlayOneShot(clip, _config.VolumeOf(sound) * _config.masterVolume);
        }

        /// <summary>Stops the buzz loops and every pending or playing sound (restart, level change).</summary>
        public void StopAll()
        {
            foreach (var kv in _buzz)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _buzz.Clear();
            _scheduled.Clear();
            if (_oneShot != null) _oneShot.Stop();
        }

        private void OnLevelLoaded(int level)
        {
            StopAll();
            _events.Clear();
            Tip(Tips.OnLevelStart(level));
        }

        private void OnCommandAccepted(Command command)
        {
            // In planning a switch happens at once; while running it clicks when the character does it (ActionDone).
            if (_controller.Sim != null && _controller.Sim.Phase == LevelPhase.Planning && FeedbackRules.IsSwitchOn(command))
                Play(ProceduralAudio.Click);
        }

        private void OnTickEvents(IReadOnlyList<SimEvent> events)
        {
            var sim = _controller.Sim;
            if (sim == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                _events.Add(e);
                switch (e.Type)
                {
                    case SimEventType.OverloadStarted:
                        StartBuzz(e.Circuit ?? "");
                        break;
                    case SimEventType.OverloadCleared:
                    case SimEventType.CircuitTripped:
                    case SimEventType.MainTripped:
                        StopBuzz(e.Circuit ?? "");
                        break;
                    case SimEventType.LevelWon:
                    case SimEventType.LevelLost:
                        StopBuzzes();
                        break;
                }

                _scratch.Clear();
                FeedbackRules.SoundsFor(e, _scratch);
                for (int s = 0; s < _scratch.Count; s++)
                {
                    // The second clunk of a main trip follows a moment later.
                    if (s > 0 && _scratch[s] == _scratch[s - 1] && _scratch[s] == ProceduralAudio.Clunk)
                        _scheduled.Add(new Scheduled { Sound = _scratch[s], At = Time.unscaledTime + _config.secondClunkDelay });
                    else
                        Play(_scratch[s]);
                }
                Tip(Tips.OnEvent(_controller.LevelIndex, e));
            }

            // Deadline ticks: once a second for every request that is close to its deadline.
            if (sim.Phase == LevelPhase.Running)
            {
                for (int r = 0; r < sim.Requests.Count; r++)
                {
                    var req = sim.Requests[r];
                    if (FeedbackRules.ShouldTick(sim.Now, req.DueTicks, req.Done, sim.Rules.ticksPerSecond, _config.tickWarnSeconds))
                    {
                        Play(ProceduralAudio.Tick);
                        break;
                    }
                }
            }
        }

        private void Tip(string key)
        {
            if (key == null) return;
            _controller.Toast(_controller.Loader.Strings.Get(key));
        }

        private void StartBuzz(string circuit)
        {
            if (_buzz.ContainsKey(circuit)) return;
            var go = new GameObject("Buzz " + (circuit.Length == 0 ? "MAIN" : circuit));
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = _clips[ProceduralAudio.Buzz];
            src.loop = true;
            src.spatialBlend = 0f;
            src.mute = Muted;
            src.volume = _config.buzzMinVolume * _config.masterVolume;
            src.Play();
            _buzz[circuit] = src;
            Record(ProceduralAudio.Buzz);
        }

        private void StopBuzz(string circuit)
        {
            if (!_buzz.TryGetValue(circuit, out var src)) return;
            if (src != null) Destroy(src.gameObject);
            _buzz.Remove(circuit);
        }

        private void StopBuzzes()
        {
            foreach (var kv in _buzz)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _buzz.Clear();
        }

        private void Record(string sound)
        {
            _playLog.Add(sound);
            if (_playLog.Count > PlayLogCapacity) _playLog.RemoveAt(0);
            PlayCounts.TryGetValue(sound, out int n);
            PlayCounts[sound] = n + 1;
            if (LogToConsole) Debug.Log("[TRIPPED] sound: " + sound);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame && !_controller.KeysSuspended)
            {
                SetMuted(!Muted);
                _controller.Toast(_controller.Loader.Strings.Get(Muted ? "sound_off" : "sound_on"));
            }

            for (int i = _scheduled.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < _scheduled[i].At) continue;
                Play(_scheduled[i].Sound);
                _scheduled.RemoveAt(i);
            }

            UpdateBuzzVolumes();
        }

        /// <summary>Buzz grows with the overload counter; it is silent while the level is not running.</summary>
        private void UpdateBuzzVolumes()
        {
            var sim = _controller.Sim;
            if (_buzz.Count == 0 || sim == null) return;
            bool running = sim.Phase == LevelPhase.Running;
            _buzzKeys.Clear();
            foreach (var kv in _buzz) _buzzKeys.Add(kv.Key);
            for (int i = 0; i < _buzzKeys.Count; i++)
            {
                string id = _buzzKeys[i];
                var src = _buzz[id];
                if (src == null) continue;
                int over = id.Length == 0 ? sim.MainOverTicks : (sim.FindCircuit(id)?.OverTicks ?? 0);
                float v = FeedbackRules.BuzzVolume(over, sim.Rules.overTripTicks, _config.buzzMinVolume, _config.buzzMaxVolume);
                src.volume = running ? v * _config.masterVolume : 0f;
            }
        }

        private static bool SafeGetMuted()
        {
            try
            {
                return PlayerPrefs.GetInt(MutedKey, 0) == 1;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private void OnDestroy()
        {
            if (_controller == null) return;
            _controller.LevelLoaded -= OnLevelLoaded;
            _controller.LevelUnloaded -= StopAll;
            _controller.TickEvents -= OnTickEvents;
            _controller.CommandAccepted -= OnCommandAccepted;
        }
    }
}
