using System.Collections.Generic;

namespace Trouble.Sim
{
    /// <summary>One row of the Run Sim table.</summary>
    public sealed class VerifyRow
    {
        public string Name;
        /// <summary>"won", "lost" or "notFasterThanReference".</summary>
        public string Expect;
        public LevelPhase Phase;
        public LoseReason Reason;
        public int EndTick;
        public int TripCount;
        public int Rejections;
        /// <summary>The win or lose sentence the player would read.</summary>
        public string Cause;
        /// <summary>Every trip as "A 10>8 @0:00.9".</summary>
        public string Trips;
        public bool MetExpectation;
        /// <summary>Planning commands the sim refused, or why the run could not start.</summary>
        public string Problem = "";
    }

    /// <summary>
    /// Runs every check for one level (GDD v1.1 §4.3 Verify): the reference solution, naive play, the two
    /// pumping players, and the level's own scenarios. Rows report results against their stated expectation.
    /// </summary>
    public static class Verifier
    {
        public const string Won = "won";
        public const string Lost = "lost";
        public const string NotFasterThanReference = "notFasterThanReference";

        public static List<VerifyRow> RunAll(LevelData level, RulesConfig rules, Catalog catalog, Strings strings)
        {
            var rows = new List<VerifyRow>();
            var formatter = new CauseFormatter(strings);

            var reference = Run("reference", Won, level, level.reference, null, rules, catalog, strings, formatter);
            rows.Add(reference);
            rows.Add(Run("naive (all on in planning)", Lost, level, Strategies.NaiveAllOnInPlanning(level), null, rules, catalog, strings, formatter));
            rows.Add(Run("pump breakers", NotFasterThanReference, level, Strategies.NaiveAllOnInPlanning(level), Strategies.PumpBreakers(), rules, catalog, strings, formatter));
            rows.Add(Run("pump switches", NotFasterThanReference, level, Strategies.NaiveAllOnInPlanning(level), Strategies.PumpSwitches(), rules, catalog, strings, formatter));
            foreach (var s in level.scenarios)
                rows.Add(Run(s.name, s.expect, level, s.commands, null, rules, catalog, strings, formatter));

            foreach (var row in rows) row.MetExpectation = Meets(row, reference);
            return rows;
        }

        private static bool Meets(VerifyRow row, VerifyRow reference)
        {
            if (row.Problem.Length > 0) return false;
            switch (row.Expect)
            {
                case Won: return row.Phase == LevelPhase.Won;
                case Lost: return row.Phase == LevelPhase.Lost;
                case NotFasterThanReference:
                    if (row.Phase != LevelPhase.Won) return true;
                    return reference.Phase == LevelPhase.Won && row.EndTick >= reference.EndTick;
                default: return false;
            }
        }

        private static VerifyRow Run(string name, string expect, LevelData level, IReadOnlyList<CommandData> commands,
            System.Action<HouseSim> player, RulesConfig rules, Catalog catalog, Strings strings, CauseFormatter formatter)
        {
            var row = new VerifyRow { Name = name, Expect = expect, Cause = "", Trips = "" };
            HouseSim sim;
            try
            {
                sim = new HouseSim(level, rules, catalog, strings);
            }
            catch (System.ArgumentException e)
            {
                row.Problem = e.Message;
                return row;
            }

            var result = Replay.Run(sim, commands, player);
            row.Phase = result.Phase;
            row.Reason = result.Reason;
            row.EndTick = result.EndTick;
            row.TripCount = result.TripCount;
            if (result.SetupRejections.Count > 0) row.Problem = string.Join("; ", result.SetupRejections);

            var trips = new List<string>();
            foreach (var e in result.Events)
            {
                switch (e.Type)
                {
                    case SimEventType.CommandRejected:
                        row.Rejections++;
                        break;
                    case SimEventType.CircuitTripped:
                        trips.Add($"{e.Circuit} {e.Load}>{e.Capacity} @{Time(e.Tick, sim)}");
                        break;
                    case SimEventType.MainTripped:
                        trips.Add($"MAIN {e.Load}>{e.Capacity} @{Time(e.Tick, sim)}");
                        break;
                    case SimEventType.LevelWon:
                    case SimEventType.LevelLost:
                        row.Cause = formatter.Describe(e, sim) ?? "";
                        break;
                }
            }
            row.Trips = string.Join(", ", trips);
            return row;
        }

        /// <summary>Ticks as m:ss.t.</summary>
        public static string Time(int ticks, HouseSim sim)
        {
            int tps = sim.Rules.ticksPerSecond;
            return $"{CauseFormatter.Clock(ticks, tps)}.{ticks % tps * 10 / tps}";
        }
    }
}
