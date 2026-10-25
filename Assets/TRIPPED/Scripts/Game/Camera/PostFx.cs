using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Trouble.Game
{
    /// <summary>
    /// Builds the URP post-processing for the house camera from the view config: a global Volume with bloom
    /// (so lit rooms glow), ACES tonemapping, colour grading and a vignette, plus SMAA on the camera.
    /// Built in code like the rest of the scene, so every number stays in <see cref="TrippedViewConfig"/>.
    /// </summary>
    public static class PostFx
    {
        private const string VolumeName = "PostFxVolume";
        private static Volume _volume;

        /// <summary>
        /// How much of the effect shows: 1 on the Title, <see cref="TrippedViewConfig.gameplayPostFxWeight"/> while
        /// playing. Does nothing before <see cref="Apply"/> or with post-processing off.
        /// </summary>
        public static void SetWeight(float weight)
        {
            if (_volume != null) _volume.weight = Mathf.Clamp01(weight);
        }

        /// <summary>
        /// Turns post-processing on for <paramref name="cam"/> and creates or refreshes the global Volume, at the
        /// gameplay weight (the Title raises it with <see cref="SetWeight"/>).
        /// </summary>
        public static void Apply(Camera cam, TrippedViewConfig config)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = config.postProcessing;
            data.antialiasing = config.smaa ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            data.antialiasingQuality = AntialiasingQuality.High;
            if (!config.postProcessing) return;

            var go = GameObject.Find(VolumeName) ?? new GameObject(VolumeName);
            var volume = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = config.gameplayPostFxWeight;
            _volume = volume;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.value = config.bloomIntensity;
            bloom.threshold.value = config.bloomThreshold;
            bloom.scatter.value = config.bloomScatter;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.ACES;

            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.value = config.postExposure;
            grade.contrast.value = config.contrast;
            grade.saturation.value = config.saturation;
            grade.colorFilter.value = config.colorFilter;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = config.vignetteIntensity;
            vignette.smoothness.value = 0.45f;
        }
    }
}
