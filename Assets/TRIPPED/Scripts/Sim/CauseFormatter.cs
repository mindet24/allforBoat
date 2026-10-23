using System.Collections.Generic;
using System.Text;

namespace Trouble.Sim
{
    /// <summary>Turns sim events into the sentences the player reads (GDD v1.0 §4.3). Texts come from strings.json.</summary>
    public sealed class CauseFormatter
    {
        private readonly Strings _strings;

        public CauseFormatter(Strings strings)
        {
            _strings = strings;
        }

        /// <summary>The sentence for this event, or null if the event has none.</summary>
        public string Describe(SimEvent e, HouseSim sim)
        {
            switch (e.Type)
            {
                case SimEventType.CircuitTripped:
                {
                    var c = sim.FindCircuit(e.Circuit);
                    return _strings.Format("trip",
                        ("id", c.Id), ("label", c.ShownLabel), ("terms", Terms(e.Terms, sim)), ("sum", e.Load), ("cap", e.Capacity));
                }
                case SimEventType.MainTripped:
                    return _strings.Format("main_trip", ("sum", e.Load), ("cap", e.Capacity));
                case SimEventType.LevelLost:
                    return Lost(e, sim);
                case SimEventType.LevelWon:
                    return _strings.Format("won", ("time", Clock(e.Tick, sim.Rules.ticksPerSecond)));
                case SimEventType.ApplianceInterrupted when e.ProgressLost:
                    return _strings.Format("save_lost", ("owner", sim.FindRequest(e.Appliance)?.Owner ?? ""));
                case SimEventType.SocketRevealed when e.AssumptionWasWrong:
                    return _strings.Format("label_wrong", ("id", e.Circuit));
                case SimEventType.CommandRejected:
                    return e.Message;
                default:
                    return null;
            }
        }

        /// <summary>
        /// A second line that tells the player what to do about this event, or null. Today: a breaker that tripped
        /// because it was reset while still hot (GDD v1.1 R5b).
        /// </summary>
        public string Hint(SimEvent e)
        {
            if (e.Type == SimEventType.CircuitTripped && e.WasHot) return _strings.Format("hot_retrip", ("id", e.Circuit));
            return null;
        }

        private string Lost(SimEvent e, HouseSim sim)
        {
            switch (e.Reason)
            {
                case LoseReason.Deadline:
                {
                    var req = sim.FindRequest(e.Appliance);
                    return _strings.Format("lost_deadline",
                        ("owner", req.Owner), ("appliance", NameOf(e.Appliance, sim)), ("time", Clock(req.DueTicks, sim.Rules.ticksPerSecond)));
                }
                case LoseReason.FridgeSpoiled:
                    return _strings.Format("lost_fridge", ("n", sim.SpoilLimitTicks / sim.Rules.ticksPerSecond));
                case LoseReason.DayOver:
                    return _strings.Get("lost_day");
                default:
                    return null;
            }
        }

        /// <summary>"Kettle 6 + Toaster 4".</summary>
        private static string Terms(List<LoadTerm> terms, HouseSim sim)
        {
            if (terms == null) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < terms.Count; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(NameOf(terms[i].ApplianceId, sim)).Append(' ').Append(terms[i].Load);
            }
            return sb.ToString();
        }

        private static string NameOf(string applianceId, HouseSim sim)
        {
            var app = sim.FindAppliance(applianceId);
            if (app == null) return applianceId;
            return sim.Catalog.Find(app.Type)?.name ?? app.Type;
        }

        /// <summary>Ticks as m:ss, rounded down to the second.</summary>
        public static string Clock(int ticks, int ticksPerSecond)
        {
            int seconds = ticks / ticksPerSecond;
            return $"{seconds / 60}:{seconds % 60:00}";
        }
    }
}
