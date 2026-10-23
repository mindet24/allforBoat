using System.Collections.Generic;
using Trouble.Sim;

namespace Trouble.Game
{
    /// <summary>
    /// The pure rules of sound feedback (GDD v1.0 §7.1): which sounds a sim event plays, how loud the buzz is,
    /// and when the deadline tick sounds. <see cref="FeedbackController"/> does the playing.
    /// </summary>
    public static class FeedbackRules
    {
        /// <summary>
        /// Adds the one-shot sounds this event plays to <paramref name="into"/>, in order. The buzz loop is not
        /// listed here (see <see cref="BuzzVolume"/>). A main trip lists clunk twice.
        /// </summary>
        public static void SoundsFor(SimEvent e, List<string> into)
        {
            switch (e.Type)
            {
                case SimEventType.CircuitTripped:
                    into.Add(ProceduralAudio.Clunk);
                    break;
                case SimEventType.MainTripped:
                    into.Add(ProceduralAudio.Clunk);
                    into.Add(ProceduralAudio.Clunk);
                    break;
                case SimEventType.SocketRevealed:
                    into.Add(ProceduralAudio.Chime);
                    if (e.AssumptionWasWrong) into.Add(ProceduralAudio.Wrong);
                    break;
                case SimEventType.RequestCompleted:
                    into.Add(ProceduralAudio.Ding);
                    break;
                case SimEventType.ApplianceInterrupted:
                    if (e.ProgressLost) into.Add(ProceduralAudio.FailSmall);
                    break;
                case SimEventType.CommandRejected:
                    into.Add(ProceduralAudio.Nope);
                    break;
                case SimEventType.LevelWon:
                    into.Add(ProceduralAudio.Win);
                    break;
                case SimEventType.LevelLost:
                    into.Add(ProceduralAudio.Lose);
                    break;
                case SimEventType.ActionDone:
                    if (e.Command.Type == CommandType.SetSwitch && e.Command.On) into.Add(ProceduralAudio.Click);
                    break;
            }
        }

        /// <summary>True for a switch-on command, which clicks the moment it is accepted during planning.</summary>
        public static bool IsSwitchOn(Command c) => c.Type == CommandType.SetSwitch && c.On;

        /// <summary>
        /// Buzz volume: <paramref name="min"/> when the overload counter has just started, rising linearly to
        /// <paramref name="max"/> at <paramref name="tripTicks"/>.
        /// </summary>
        public static float BuzzVolume(int overTicks, int tripTicks, float min, float max)
        {
            if (tripTicks <= 0) return max;
            float t = overTicks / (float)tripTicks;
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return min + (max - min) * t;
        }

        /// <summary>
        /// True if a request earns a tick sound at this moment: not done, 0 &lt; time left &lt;= warn seconds, and
        /// <paramref name="now"/> is on a whole second.
        /// </summary>
        public static bool ShouldTick(int now, int dueTicks, bool done, int ticksPerSecond, float warnSeconds)
        {
            if (done || ticksPerSecond <= 0 || now % ticksPerSecond != 0) return false;
            int left = dueTicks - now;
            return left > 0 && left <= warnSeconds * ticksPerSecond;
        }
    }
}
