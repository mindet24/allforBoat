using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Every number of the game-feel layer (G5): day-mode sound volumes and pitches, music, camera shake, overload
    /// and blackout drama, popups, particles and character juice. Loaded from Resources/Config/JuiceConfig; a missing
    /// asset falls back to these defaults. Kept apart from <see cref="TrippedViewConfig"/> so the feel can be tuned
    /// without touching the view layout values.
    /// </summary>
    [CreateAssetMenu(menuName = "TRIPPED/Juice Config", fileName = "JuiceConfig")]
    public sealed class JuiceConfig : ScriptableObject
    {
        /// <summary>Resources path of the asset.</summary>
        public const string ResourcePath = "Config/JuiceConfig";

        [Header("Mixer groups (multiplied with each sound's volume and the master volume)")]
        [Range(0f, 1f)] public float musicGroupVolume = 0.8f;
        [Range(0f, 1f)] public float sfxGroupVolume = 1f;
        [Range(0f, 1f)] public float uiGroupVolume = 0.8f;
        [Range(0f, 1f)] public float voiceGroupVolume = 0.7f;
        [Tooltip("Highest variant number looked for in Resources/Audio ({name}_1 … {name}_N).")]
        public int maxVariants = 8;

        [Header("Sound volumes, by name (a name missing here plays at 0.5)")]
        public SoundVolume[] soundVolumes =
        {
            new SoundVolume(SynthBank.Step, 0.2f), new SoundVolume(SynthBank.Jump, 0.3f), new SoundVolume(SynthBank.Jetpack, 0.25f),
            new SoundVolume(SynthBank.Pickup, 0.5f), new SoundVolume(SynthBank.PlugIn, 0.6f),
            new SoundVolume(SynthBank.Reject, 0.45f),
            new SoundVolume(SynthBank.RequestNewRelaxed, 0.4f), new SoundVolume(SynthBank.RequestNewNormal, 0.45f),
            new SoundVolume(SynthBank.RequestNewUrgent, 0.55f), new SoundVolume(SynthBank.RequestDone, 0.55f),
            new SoundVolume(SynthBank.RequestMissed, 0.5f), new SoundVolume(SynthBank.UrgentTick, 0.4f),
            new SoundVolume(SynthBank.HeartLost, 0.75f), new SoundVolume(SynthBank.ComboUp, 0.4f),
            new SoundVolume(SynthBank.OverloadHum, 0.6f), new SoundVolume(SynthBank.Spark, 0.3f),
            new SoundVolume(SynthBank.Blackout, 0.9f), new SoundVolume(SynthBank.PowerUp, 0.55f),
            new SoundVolume(SynthBank.BreakerFlip, 0.6f), new SoundVolume(SynthBank.ToasterPop, 0.5f),
            new SoundVolume("kettle_loop", 0.18f), new SoundVolume("toaster_loop", 0.12f),
            new SoundVolume("microwave_loop", 0.15f), new SoundVolume("fridge_loop", 0.1f),
            new SoundVolume("tv_loop", 0.12f), new SoundVolume("heater_loop", 0.12f),
            new SoundVolume("washer_loop", 0.16f), new SoundVolume("dryer_loop", 0.16f),
            new SoundVolume("blender_loop", 0.16f),
            new SoundVolume(SynthBank.UiHover, 0.2f), new SoundVolume(SynthBank.UiClick, 0.4f),
            new SoundVolume(SynthBank.UiBack, 0.35f), new SoundVolume(SynthBank.UiPause, 0.4f),
            new SoundVolume(SynthBank.CountdownBeep, 0.4f), new SoundVolume(SynthBank.DayStart, 0.55f),
            new SoundVolume(SynthBank.WinJingle, 0.6f), new SoundVolume(SynthBank.LoseJingle, 0.6f),
            new SoundVolume(SynthBank.StarPop, 0.5f), new SoundVolume(SynthBank.ScoreTick, 0.25f),
            new SoundVolume(SynthBank.MusicMenu, 0.3f), new SoundVolume(SynthBank.MusicDay, 0.28f),
            new SoundVolume(SynthBank.MusicDayFinal, 0.3f),
            new SoundVolume(SynthBank.VoiceHappy, 0.35f), new SoundVolume(SynthBank.VoiceAngry, 0.35f),
            new SoundVolume(SynthBank.VoiceHey, 0.3f),
        };

        [Header("Voices")]
        [Tooltip("Chance (0..1) that a new normal or relaxed request also plays voice_hey (urgent ones always do).")]
        [Range(0f, 1f)] public float heyChance = 0.3f;

        [Header("Countdown")]
        [Tooltip("countdown_beep every second in the last N seconds of the day.")]
        public float countdownSeconds = 5f;
        [Tooltip("Seconds between two spark crackles of an overloaded room.")]
        public float sparkSoundInterval = 0.22f;

        [Header("Footsteps")]
        [Tooltip("Seconds between two footsteps while walking.")]
        public float stepInterval = 0.26f;
        [Tooltip("Pitch of the left and right foot alternate by this much around 1.")]
        public float stepPitchSpread = 0.08f;

        [Header("Request sounds")]
        [Tooltip("A completion within this many seconds of the last one raises the combo.")]
        public float comboWindow = 4f;
        [Tooltip("Pitch added per combo step (1 = first completion).")]
        public float comboPitchStep = 0.12f;
        [Tooltip("Highest combo pitch.")]
        public float comboPitchMax = 1.6f;
        [Tooltip("Urgent request: tick every second in the last N seconds before it is missed.")]
        public float urgentTickSeconds = 5f;
        [Tooltip("Urgent request: tick twice a second in the last N seconds.")]
        public float urgentFastTickSeconds = 2f;
        [Tooltip("Pitch of the urgent tick in the fast phase.")]
        public float urgentFastTickPitch = 1.3f;

        [Header("Overload hum")]
        public float humPitchMin = 0.8f;
        public float humPitchMax = 2.0f;
        public float humVolumeMin = 0.15f;
        public float humVolumeMax = 0.7f;

        [Header("Music")]
        [Tooltip("Seconds of the crossfade between two music loops.")]
        public float musicFade = 0.8f;
        [Tooltip("Last N seconds of the day: music speeds up and the vignette pulses.")]
        public float finalSeconds = 30f;
        [Tooltip("Music pitch (= tempo) at the very end of the day; 1 before the final stretch.")]
        public float finalMusicPitch = 1.18f;
        [Tooltip("Music volume while the house is blacked out, as a share of normal.")]
        public float blackoutMusicDuck = 0.35f;

        [Header("Camera shake")]
        [Tooltip("Shake (world units) when an overload has just started.")]
        public float overloadShakeMin = 0.02f;
        [Tooltip("Shake just before the overload trips.")]
        public float overloadShakeMax = 0.12f;
        [Tooltip("Shake kick of a blackout.")]
        public float blackoutShake = 0.6f;
        [Tooltip("Shake kick of a lost heart.")]
        public float heartShake = 0.3f;
        [Tooltip("Shake kick of a rejected action.")]
        public float rejectShake = 0.05f;
        [Tooltip("Shake kick of a plug-in.")]
        public float plugShake = 0.04f;
        [Tooltip("How fast a kick dies away (per second).")]
        public float shakeDecay = 3.5f;
        [Tooltip("Shake jitter frequency (Hz).")]
        public float shakeFrequency = 28f;
        [Tooltip("Biggest shake offset ever applied.")]
        public float shakeMax = 0.8f;

        [Header("Overload drama")]
        public Color overloadColor = new Color(1f, 0.12f, 0.08f, 1f);
        [Tooltip("Overlay alpha at the low and high point of the pulse.")]
        public float overloadAlphaMin = 0.28f;
        public float overloadAlphaMax = 0.6f;
        [Tooltip("Pulses per second when the overload starts and just before it trips.")]
        public float overloadPulseMinHz = 2f;
        public float overloadPulseMaxHz = 9f;
        [Tooltip("Sparks per second at each busy socket of an overloaded room (scaled up toward the trip).")]
        public float overloadSparksPerSecond = 10f;

        [Header("Blackout")]
        [Tooltip("Scene lights are multiplied by this while the house is dark.")]
        public float blackoutLightFactor = 0.25f;
        [Tooltip("Alpha of the dark overlay drawn on every room in a blackout (on top of the house overlay).")]
        public float blackoutRoomAlpha = 0.35f;
        public Color blackoutRoomColor = new Color(0.02f, 0.02f, 0.06f, 1f);
        public Color flashColor = new Color(1f, 1f, 1f, 0.85f);
        [Tooltip("Seconds the white flash takes to fade.")]
        public float flashSeconds = 0.18f;
        [Tooltip("Slow motion time scale and length in real seconds.")]
        public float slowMoScale = 0.25f;
        public float slowMoSeconds = 0.3f;
        [Tooltip("Sparks fired from the panel on a blackout.")]
        public int panelSparkBurst = 40;

        [Header("Relight after reset")]
        [Tooltip("Seconds between one room coming back on and the next.")]
        public float relightStagger = 0.12f;
        [Tooltip("Seconds each room flickers before staying on.")]
        public float relightFlicker = 0.35f;
        [Tooltip("Alpha of a room that is still dark during the relight (the house overlay is gone by then).")]
        public float relightDarkAlpha = 0.75f;
        [Tooltip("Flicker changes per second.")]
        public float flickerHz = 22f;

        [Header("Squash and stretch")]
        [Tooltip("Squash amount (0.25 = 25 % wider and shorter at the peak).")]
        public float squashAmount = 0.28f;
        public float squashSeconds = 0.28f;
        [Tooltip("Carried item bob height and speed.")]
        public float carryBobHeight = 0.08f;
        public float carryBobHz = 3.2f;
        [Tooltip("Character lean (degrees) while walking.")]
        public float leanDegrees = 8f;
        [Tooltip("How fast the lean follows (per second).")]
        public float leanSpeed = 12f;
        [Tooltip("Pitch of the footstep played on landing.")]
        public float landPitch = 0.8f;
        [Tooltip("Seconds between two jetpack puffs while Space is held.")]
        public float jetpackPuffInterval = 0.14f;

        [Header("Running appliances")]
        [Tooltip("Wobble (degrees) and speed of an appliance working on a request.")]
        public float wobbleDegrees = 4f;
        public float wobbleHz = 7f;
        [Tooltip("Scale pulse of a working appliance.")]
        public float workPulse = 0.04f;

        [Header("Popups")]
        public Color scorePopupColor = new Color(1f, 0.85f, 0.15f, 1f);
        public Color heartPopupColor = new Color(1f, 0.25f, 0.3f, 1f);
        public Color missPopupColor = new Color(0.75f, 0.75f, 0.8f, 1f);
        [Tooltip("Text of the lost heart popup. {0} = the heart glyph if the font has it, else 'HEART'.")]
        public string heartPopupText = "-1 {0}";
        public string missPopupText = "MISSED";
        [Tooltip("Font size of popups (TMP world size).")]
        public float popupFontSize = 7f;
        [Tooltip("Seconds a popup lives, how far it rises and its overshoot.")]
        public float popupSeconds = 1.1f;
        public float popupRise = 1.2f;
        public float popupOvershoot = 1.8f;
        [Tooltip("Seconds of the pop-in part of the popup.")]
        public float popupPopSeconds = 0.25f;
        [Tooltip("Coins per completed request.")]
        public int coinBurst = 14;
        public Color coinColor = new Color(1f, 0.8f, 0.1f, 1f);

        [Header("Particles")]
        public Color sparkColor = new Color(1f, 0.9f, 0.4f, 1f);
        public float sparkSize = 0.08f;
        public float sparkSpeed = 3.5f;
        public float sparkLife = 0.35f;
        public float coinSize = 0.2f;
        public float coinSpeed = 5f;
        public float coinLife = 0.9f;
        public float gravity = 12f;
        [Tooltip("Particles alive at once, at most.")]
        public int maxParticles = 400;
        [Tooltip("z of every juice effect (in front of the house).")]
        public float fxZ = -0.6f;
        [Tooltip("Sorting order of world juice effects.")]
        public int fxSortingOrder = 40;

        [Header("Screen overlay (flash, vignette)")]
        [Tooltip("Sorting order of the flash and vignette canvas; kept under menus.")]
        public int overlaySortingOrder = 90;
        public Color vignetteColor = new Color(0.9f, 0.05f, 0.05f, 1f);
        public float vignetteAlphaMax = 0.35f;
        public float vignettePulseHz = 1.5f;
        [Tooltip("Vignette alpha while a room is overloaded (any time of day).")]
        public float vignetteOverloadAlpha = 0.25f;

        /// <summary>Volume of a day sound; false if it is not listed here.</summary>
        public bool TryVolume(string sound, out float volume)
        {
            if (soundVolumes != null)
                for (int i = 0; i < soundVolumes.Length; i++)
                    if (soundVolumes[i].name == sound)
                    {
                        volume = soundVolumes[i].volume;
                        return true;
                    }
            volume = 0f;
            return false;
        }

        /// <summary>Volume of a mixer group.</summary>
        public float GroupVolume(SoundGroup group)
        {
            switch (group)
            {
                case SoundGroup.Music: return musicGroupVolume;
                case SoundGroup.Ui: return uiGroupVolume;
                case SoundGroup.Voice: return voiceGroupVolume;
                default: return sfxGroupVolume;
            }
        }

        /// <summary>The asset from Resources, or a default instance.</summary>
        public static JuiceConfig LoadOrDefault()
        {
            var asset = Resources.Load<JuiceConfig>(ResourcePath);
            return asset != null ? asset : CreateInstance<JuiceConfig>();
        }
    }
}
