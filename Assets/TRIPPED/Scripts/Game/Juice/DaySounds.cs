using System.Collections.Generic;
using Trouble.Sim;

namespace Trouble.Game
{
    /// <summary>
    /// The day-mode event → sound table. Pure; <see cref="JuiceController"/> plays the result. Pickup and plug-in
    /// sounds are not here: they play the moment E is accepted (see JuiceController), not a tick later.
    /// </summary>
    public static class DaySounds
    {
        /// <summary>The new-request sound for an urgency: sharper the more urgent.</summary>
        public static string NewRequest(UrgencyLevel urgency)
        {
            switch (urgency)
            {
                case UrgencyLevel.Urgent: return SynthBank.RequestNewUrgent;
                case UrgencyLevel.Relaxed: return SynthBank.RequestNewRelaxed;
                default: return SynthBank.RequestNewNormal;
            }
        }

        /// <summary>
        /// Adds the one-shot sounds a day event plays to <paramref name="into"/>, in order.
        /// <paramref name="urgency"/> is the urgency of the request the event is about (spawned events only).
        /// </summary>
        public static void For(SimEventType type, UrgencyLevel urgency, List<string> into)
        {
            switch (type)
            {
                case SimEventType.RequestSpawned: into.Add(NewRequest(urgency)); break;
                case SimEventType.RequestCompleted: into.Add(SynthBank.RequestDone); break;
                case SimEventType.RequestMissed: into.Add(SynthBank.RequestMissed); break;
                case SimEventType.HeartLost: into.Add(SynthBank.HeartLost); break;
                case SimEventType.CircuitTripped:
                case SimEventType.MainTripped: into.Add(SynthBank.Blackout); break;
                case SimEventType.BreakerSwitched: into.Add(SynthBank.BreakerFlip); into.Add(SynthBank.PowerUp); break;
                case SimEventType.LevelWon: into.Add(SynthBank.WinJingle); break;
                case SimEventType.LevelLost: into.Add(SynthBank.LoseJingle); break;
                case SimEventType.CommandRejected: into.Add(SynthBank.Reject); break;
            }
        }

        /// <summary>True for event types that play at most once per tick (a reset switches every room back on).</summary>
        public static bool OncePerTick(SimEventType type)
        {
            return type == SimEventType.BreakerSwitched || type == SimEventType.CircuitTripped || type == SimEventType.MainTripped
                || type == SimEventType.HeartLost;
        }
    }
}
