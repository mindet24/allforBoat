using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>
    /// GDD v1.0 §9.2 rows R1–R22 on the real level files, with the v1.0 rules they were verified under.
    /// These prove the C# port matches the Python reference (Appendix A). If a row fails, the sim is wrong,
    /// not the level data.
    /// </summary>
    public class ReplayTests
    {
        private const string Ref = "reference";
        private const string Naive = "naive";

        public readonly struct Row
        {
            public readonly string Id;
            public readonly int Level;
            public readonly string Script;
            public readonly LevelPhase Result;
            public readonly LoseReason Reason;
            public readonly int EndTick;
            /// <summary>Every trip as "A@9", "MAIN@9", joined by commas.</summary>
            public readonly string Trips;

            public Row(string id, int level, string script, LevelPhase result, LoseReason reason, int endTick, string trips)
            {
                Id = id;
                Level = level;
                Script = script;
                Result = result;
                Reason = reason;
                EndTick = endTick;
                Trips = trips;
            }

            public override string ToString() => Id;
        }

        private const LevelPhase Won = LevelPhase.Won;
        private const LevelPhase Lost = LevelPhase.Lost;
        private const LoseReason None = LoseReason.None;
        private const LoseReason Deadline = LoseReason.Deadline;
        private const LoseReason Fridge = LoseReason.FridgeSpoiled;

        private const string L2Plugs = "-1 plug dryer BA1; -1 plug microwave K1; -1 plug kettle K2; -1 plug phone BR1";
        private const string L4Plugs = "-1 plug kettle K1; -1 plug microwave K2; -1 plug dryer BA1; -1 plug washer LA1; -1 plug iron LA2";
        private const string L5Plugs = "-1 plug kettle K1; -1 plug console BR1; -1 plug phone BR3; -1 plug washer LA1; -1 plug microwave K2; -1 plug dryer BA1";

        public static IEnumerable<Row> Rows()
        {
            yield return new Row("R1", 1, Ref, Won, None, 180, "");
            yield return new Row("R2", 1, "-1 cord L1 kitchen; -1 plug kettle CORD-L1; -1 plug toaster K1; 0 on kettle; 0 on toaster", Won, None, 100, "");
            yield return new Row("R3", 1, Naive, Lost, Deadline, 301, "A@9");
            yield return new Row("R4", 2, Ref, Won, None, 200, "");
            yield return new Row("R5", 2, "-1 plug lamp1 K1; -1 plug dryer BA1; -1 plug kettle K2; -1 plug phone BR1; 0 on lamp1; 10 breaker A; 20 breaker A; " +
                                          "30 unplug lamp1; 30 plug microwave K1; 31 on dryer; 31 on microwave; 31 on kettle; 31 on phone", Won, None, 231, "");
            yield return new Row("R6", 2, "-1 plug dryer BA1; -1 plug kettle K1; -1 plug microwave K2; -1 plug phone BR1; 0 on dryer; 0 on kettle; 0 on microwave; 0 on phone; " +
                                          "15 unplug kettle; 15 unplug microwave; 15 plug kettle K2; 15 plug microwave K1; 15 breaker A; 16 on kettle; 16 on microwave; 16 on dryer", Won, None, 200, "A@9");
            yield return new Row("R7", 2, "-1 cord BR1 bathroom; -1 plug dryer CORD-BR1; -1 plug microwave K1; -1 plug kettle K2; 0 on dryer; 0 on microwave; 0 on kettle; " +
                                          "100 uncord CORD-BR1; 100 plug phone BR1; 100 on phone", Won, None, 300, "");
            yield return new Row("R8", 2, L2Plugs + "; 0 on dryer; 0 on phone; 0 on microwave; 120 on kettle", Lost, Deadline, 181, "");
            yield return new Row("R9", 2, Naive, Lost, Deadline, 181, "A@9");
            yield return new Row("R10", 3, Ref, Won, None, 300, "");
            yield return new Row("R11", 3, "-1 cord L2 bedroom; -1 plug console CORD-L2; -1 plug kettle K1; 0 on console; 0 on kettle; " +
                                           "300 uncord CORD-L2; 300 plug iron L2; 300 on iron", Won, None, 400, "");
            yield return new Row("R12", 3, Naive, Lost, Deadline, 451, "B@209");
            yield return new Row("R13", 4, Ref, Won, None, 400, "");
            yield return new Row("R14", 4, "-1 cord L2 kitchen; -1 plug microwave CORD-L2; -1 plug kettle K1; -1 plug dryer BA1; -1 plug washer LA1; -1 plug iron LA2; " +
                                           "0 on kettle; 0 on microwave; 0 on dryer; 0 on washer; 300 on iron", Won, None, 400, "");
            yield return new Row("R15", 4, "-1 plug kettle K1; -1 plug microwave K2; -1 plug dryer BA1; -1 plug washer LA1; 0 on kettle; 0 on dryer; 0 on washer; " +
                                           "80 on microwave; 200 unplug kettle; 200 cord K1 laundry; 200 plug iron CORD-K1; 200 on iron", Won, None, 300, "");
            yield return new Row("R16", 4, L4Plugs + "; 0 on kettle; 0 on microwave; 0 on dryer; 0 on washer", Lost, Fridge, 110, "B@9");
            yield return new Row("R17", 4, Naive, Lost, Fridge, 110, "B@9,C@9");
            yield return new Row("R18", 5, Ref, Won, None, 300, "");
            yield return new Row("R19", 5, "-1 plug console BR3; -1 plug phone BR1; -1 plug kettle K1; -1 plug microwave K2; -1 plug washer LA1; -1 plug dryer BA1; " +
                                           "0 on kettle; 0 on phone; 0 on washer; 80 on microwave; 200 on dryer; 300 on console", Won, None, 600, "");
            yield return new Row("R20", 5, L5Plugs + "; 0 on kettle; 0 on console; 0 on phone; 0 on dryer; 0 on washer; 80 on microwave", Lost, Fridge, 110, "MAIN@9");
            yield return new Row("R21", 5, L5Plugs + "; 0 on kettle; 0 on phone; 0 on washer; 80 on microwave; 200 on dryer; 300 on console", Lost, Deadline, 751, "B@409");
            yield return new Row("R22", 5, Naive, Lost, Fridge, 110, "A@9");
        }

        [TestCaseSource(nameof(Rows))]
        public void ReplayRow_MatchesVerifiedResult(Row row)
        {
            // Arrange
            var level = TestData.LevelAsInV10(row.Level);
            var commands = row.Script == Ref ? level.reference : row.Script == Naive ? Replay.Naive(level) : Replay.Parse(row.Script);
            var sim = new HouseSim(level, TestData.V10Rules(), TestData.Catalog(), TestData.Strings());

            // Act
            var result = Replay.Run(sim, commands);

            // Assert
            Assert.IsEmpty(result.SetupRejections, "planning commands were refused");
            Assert.AreEqual(row.Result, result.Phase);
            Assert.AreEqual(row.Reason, result.Reason);
            Assert.AreEqual(row.EndTick, result.EndTick);
            Assert.AreEqual(row.Trips, TripLog(result));
            Assert.AreEqual(row.Trips.Length == 0 ? 0 : row.Trips.Split(',').Length, result.TripCount);
        }

        private static string TripLog(ReplayResult result)
        {
            var parts = new List<string>();
            foreach (var e in result.Events)
            {
                if (e.Type == SimEventType.CircuitTripped) parts.Add($"{e.Circuit}@{e.Tick}");
                if (e.Type == SimEventType.MainTripped) parts.Add($"MAIN@{e.Tick}");
            }
            return string.Join(",", parts);
        }
    }
}
