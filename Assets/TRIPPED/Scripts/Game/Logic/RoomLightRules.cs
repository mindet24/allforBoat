using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Pure rules for the ceiling light of each room: it is on while the room has power, fades up when power comes
    /// back and flickers out when the breaker goes OFF, so a trip reads in the world without the HUD.
    /// </summary>
    public static class RoomLightRules
    {
        /// <summary>
        /// True while the room has power: its own circuit's breaker is ON (day mode, one circuit per room). A room
        /// with no circuit of its own (puzzle levels) is always lit.
        /// </summary>
        public static bool IsLit(CircuitState roomCircuit) => roomCircuit == null || roomCircuit.IsOn;

        /// <summary>
        /// Brightness 0..1 of the light <paramref name="seconds"/> after it last changed to <paramref name="lit"/>.
        /// On: fades up over <paramref name="fadeIn"/> seconds. Off: two short blinks over
        /// <paramref name="flickerOut"/> seconds, then dark.
        /// </summary>
        public static float Level(bool lit, float seconds, float fadeIn, float flickerOut)
        {
            if (lit) return fadeIn <= 0f ? 1f : Mathf.Clamp01(seconds / fadeIn);
            if (flickerOut <= 0f || seconds >= flickerOut) return 0f;
            // Out, back on dimmer, out, back on dimmer still, out.
            float t = seconds / flickerOut;
            if (t < 0.2f) return 0f;
            if (t < 0.4f) return 0.6f;
            if (t < 0.6f) return 0f;
            if (t < 0.75f) return 0.3f;
            return 0f;
        }

        /// <summary>
        /// Brightness multiplier of an overloading room's light: it stutters, more often and deeper as the breaker
        /// nears its trip (<paramref name="overload"/> 0..1). Each step of <paramref name="hz"/> (eased from
        /// <paramref name="hzMin"/> to <paramref name="hzMax"/>) either holds full light or dips by up to
        /// <paramref name="depth"/>. 1 when not overloading. Same inputs, same result.
        /// </summary>
        public static float OverloadFlicker(float overload, float time, float depth, float hzMin, float hzMax)
        {
            if (overload <= 0f || depth <= 0f) return 1f;
            float o = Mathf.Clamp01(overload);
            float hz = Mathf.Lerp(hzMin, hzMax, o);
            int step = Mathf.FloorToInt(time * hz);
            // Cheap hash of the step: 0..1, stable for the whole step.
            float noise = Mathf.Abs(Mathf.Sin(step * 12.9898f) * 43758.547f) % 1f;
            bool dip = noise < Mathf.Lerp(0.25f, 0.7f, o);
            return dip ? 1f - depth * Mathf.Lerp(0.4f, 1f, o) : 1f;
        }
    }
}
