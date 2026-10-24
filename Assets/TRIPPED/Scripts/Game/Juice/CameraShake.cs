using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Camera shake that sits on the camera object beside the camera rig and never touches the rig's code. It runs
    /// last in LateUpdate (after <see cref="HouseCamera"/> placed the camera) and adds an offset to the camera's
    /// LOCAL position (the rig moves a parent pivot, see HouseCamera.ShakeTarget). If nobody rewrote the local
    /// position since last frame, last frame's offset is taken back first, so the offset never accumulates.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class CameraShake : MonoBehaviour
    {
        private JuiceConfig _juice;
        private float _trauma;
        private Vector3 _applied;
        private Vector3 _lastShaken;
        private bool _hasLast;
        private float _seed;

        /// <summary>Steady shake set every frame by the juice layer (an overload building up). Reset to 0 after use.</summary>
        public float Steady { get; set; }

        /// <summary>The offset applied this frame (for checks).</summary>
        public Vector3 Offset => _applied;

        /// <summary>The camera shake on the main camera, added if missing; null without a main camera.</summary>
        public static CameraShake On(Camera cam, JuiceConfig juice)
        {
            if (cam == null) return null;
            var shake = cam.GetComponent<CameraShake>();
            if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
            shake._juice = juice;
            return shake;
        }

        /// <summary>A kick of shake that dies away (blackout, heart lost). The strongest kick wins.</summary>
        public void Kick(float amount)
        {
            _trauma = Mathf.Max(_trauma, amount);
        }

        private void Awake()
        {
            _seed = Random.Range(0f, 100f);
        }

        private void LateUpdate()
        {
            if (_juice == null) return;
            var pos = transform.localPosition;
            // Nobody placed the camera this frame: remove our old offset before adding the new one.
            if (_hasLast && pos == _lastShaken) pos -= _applied;

            float dt = Time.unscaledDeltaTime;
            _trauma = JuiceMath.DecayShake(_trauma, _juice.shakeDecay, dt);
            float amount = JuiceMath.ShakeAmount(_trauma, Steady, _juice.shakeMax);
            Steady = 0f;
            float t = Time.unscaledTime * _juice.shakeFrequency;
            _applied = amount <= 0f ? Vector3.zero : new Vector3(
                (Mathf.PerlinNoise(_seed, t) * 2f - 1f) * amount,
                (Mathf.PerlinNoise(_seed + 7.3f, t) * 2f - 1f) * amount, 0f);
            pos += _applied;
            transform.localPosition = pos;
            _lastShaken = pos;
            _hasLast = true;
        }
    }
}
