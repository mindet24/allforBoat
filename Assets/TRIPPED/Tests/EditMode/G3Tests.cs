using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Game;
using Trouble.Sim;

namespace Trouble.Tests
{
    /// <summary>A dictionary standing in for PlayerPrefs.</summary>
    internal sealed class MemoryProgressStore : IProgressStore
    {
        public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public int GetInt(string key, int fallback) => Values.TryGetValue(key, out var v) ? v : fallback;
        public void SetInt(string key, int value) => Values[key] = value;
        public void Save() { }
    }

    /// <summary>Generated sounds (GDD v1.0 §7.2).</summary>
    public class ProceduralAudioTests
    {
        [TestCase("click", 0.03f)]
        [TestCase("buzz", 0.5f)]
        [TestCase("clunk", 0.15f)]
        [TestCase("chime", 0.2f)]
        [TestCase("wrong", 0.25f)]
        [TestCase("ding", 0.4f)]
        [TestCase("tick", 0.02f)]
        [TestCase("nope", 0.1f)]
        [TestCase("fail_small", 0.3f)]
        [TestCase("win", 0.5f)]
        [TestCase("lose", 0.8f)]
        public void Generate_EverySound_HasTheLengthOfTheGdd(string name, float seconds)
        {
            // Arrange + Act
            var samples = ProceduralAudio.Generate(name);

            // Assert
            Assert.AreEqual((int)System.Math.Round(seconds * 44100), samples.Length);
        }

        [Test]
        public void Generate_EverySound_PeaksAtOneAndStaysInRange()
        {
            foreach (var name in ProceduralAudio.Names)
            {
                // Arrange + Act
                var samples = ProceduralAudio.Generate(name);
                float max = 0f;
                foreach (var s in samples) max = System.Math.Max(max, System.Math.Abs(s));

                // Assert
                Assert.AreEqual(1f, max, 1e-4f, name);
            }
        }

        [Test]
        public void Generate_SameNameTwice_GivesIdenticalSamples()
        {
            // Arrange + Act
            var a = ProceduralAudio.Generate("clunk");
            var b = ProceduralAudio.Generate("clunk");

            // Assert
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Generate_Click_FadesToSilence()
        {
            // Arrange + Act
            var s = ProceduralAudio.Generate("click");

            // Assert
            Assert.Less(System.Math.Abs(s[s.Length - 1]), 0.05f);
        }

        [Test]
        public void Generate_PeakParameter_ScalesTheSound()
        {
            // Arrange + Act
            var s = ProceduralAudio.Generate("ding", 44100, 0.4f);
            float max = 0f;
            foreach (var v in s) max = System.Math.Max(max, System.Math.Abs(v));

            // Assert
            Assert.AreEqual(0.4f, max, 1e-4f);
        }

        [Test]
        public void Generate_UnknownName_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => ProceduralAudio.Generate("nothing"));
        }

        [Test]
        public void Loops_OnlyBuzz()
        {
            Assert.IsTrue(ProceduralAudio.Loops("buzz"));
            Assert.IsFalse(ProceduralAudio.Loops("click"));
        }
    }

    /// <summary>Event → sound table (GDD v1.0 §7.1) and buzz volume.</summary>
    public class FeedbackRulesTests
    {
        private static List<string> Sounds(SimEvent e)
        {
            var list = new List<string>();
            FeedbackRules.SoundsFor(e, list);
            return list;
        }

        [Test]
        public void CircuitTripped_PlaysClunk()
        {
            CollectionAssert.AreEqual(new[] { "clunk" }, Sounds(new SimEvent { Type = SimEventType.CircuitTripped }));
        }

        [Test]
        public void MainTripped_PlaysClunkTwice()
        {
            CollectionAssert.AreEqual(new[] { "clunk", "clunk" }, Sounds(new SimEvent { Type = SimEventType.MainTripped }));
        }

        [Test]
        public void SocketRevealed_RightAssumption_PlaysChimeOnly()
        {
            CollectionAssert.AreEqual(new[] { "chime" }, Sounds(new SimEvent { Type = SimEventType.SocketRevealed }));
        }

        [Test]
        public void SocketRevealed_WrongAssumption_PlaysChimeThenWrong()
        {
            CollectionAssert.AreEqual(new[] { "chime", "wrong" },
                Sounds(new SimEvent { Type = SimEventType.SocketRevealed, AssumptionWasWrong = true }));
        }

        [Test]
        public void ApplianceInterrupted_OnlyWithProgressLost_PlaysFailSmall()
        {
            CollectionAssert.IsEmpty(Sounds(new SimEvent { Type = SimEventType.ApplianceInterrupted }));
            CollectionAssert.AreEqual(new[] { "fail_small" },
                Sounds(new SimEvent { Type = SimEventType.ApplianceInterrupted, ProgressLost = true }));
        }

        [TestCase(SimEventType.RequestCompleted, "ding")]
        [TestCase(SimEventType.CommandRejected, "nope")]
        [TestCase(SimEventType.LevelWon, "win")]
        [TestCase(SimEventType.LevelLost, "lose")]
        public void SimpleEvents_PlayTheirSound(SimEventType type, string sound)
        {
            CollectionAssert.AreEqual(new[] { sound }, Sounds(new SimEvent { Type = type }));
        }

        [Test]
        public void ActionDone_SwitchOn_Clicks_SwitchOff_IsSilent()
        {
            CollectionAssert.AreEqual(new[] { "click" },
                Sounds(new SimEvent { Type = SimEventType.ActionDone, Command = Command.SetSwitch("kettle", true) }));
            CollectionAssert.IsEmpty(Sounds(new SimEvent { Type = SimEventType.ActionDone, Command = Command.SetSwitch("kettle", false) }));
            CollectionAssert.IsEmpty(Sounds(new SimEvent { Type = SimEventType.ActionDone, Command = Command.Plug("kettle", "K1") }));
        }

        [Test]
        public void OverloadEvents_PlayNoOneShot()
        {
            CollectionAssert.IsEmpty(Sounds(new SimEvent { Type = SimEventType.OverloadStarted }));
            CollectionAssert.IsEmpty(Sounds(new SimEvent { Type = SimEventType.OverloadCleared }));
        }

        [TestCase(0, 10, 0.25f)]
        [TestCase(5, 10, 0.425f)]
        [TestCase(10, 10, 0.6f)]
        [TestCase(30, 10, 0.6f)]
        public void BuzzVolume_RisesWithTheCounter(int over, int trip, float expected)
        {
            Assert.AreEqual(expected, FeedbackRules.BuzzVolume(over, trip, 0.25f, 0.6f), 1e-4f);
        }

        // Boundary values are the point: "under 5 s" means 1..50 ticks left at 10 ticks per second.
        [TestCase(150, 200, false, true)]
        [TestCase(149, 200, false, false)]
        [TestCase(160, 200, true, false)]
        [TestCase(200, 200, false, false)]
        [TestCase(0, 200, false, false)]
        public void ShouldTick_OnWholeSecondsWithinWarning(int now, int due, bool done, bool expected)
        {
            Assert.AreEqual(expected, FeedbackRules.ShouldTick(now, due, done, 10, 5f));
        }
    }

    /// <summary>First-time tips (Q33): once per session.</summary>
    public class TipTrackerTests
    {
        [Test]
        public void OnLevelStart_Level1_GivesTheTipOnce()
        {
            // Arrange
            var tips = new TipTracker();

            // Act + Assert
            Assert.AreEqual("tip_level1_start", tips.OnLevelStart(1));
            Assert.IsNull(tips.OnLevelStart(1));
        }

        [TestCase(2, "tip_level2_start")]
        [TestCase(3, "tip_level3_start")]
        public void OnLevelStart_OtherLevels_GiveTheirTip(int level, string key)
        {
            Assert.AreEqual(key, new TipTracker().OnLevelStart(level));
        }

        [Test]
        public void OnLevelStart_Level4_HasNoTip()
        {
            Assert.IsNull(new TipTracker().OnLevelStart(4));
        }

        [Test]
        public void OnEvent_FirstTripInLevel1_GivesTipOnce()
        {
            // Arrange
            var tips = new TipTracker();
            var trip = new SimEvent { Type = SimEventType.CircuitTripped };

            // Act + Assert
            Assert.AreEqual("tip_level1_first_trip", tips.OnEvent(1, trip));
            Assert.IsNull(tips.OnEvent(1, trip));
        }

        [Test]
        public void OnEvent_TripInLevel2_GivesNoTripTip()
        {
            Assert.IsNull(new TipTracker().OnEvent(2, new SimEvent { Type = SimEventType.CircuitTripped }));
        }

        [Test]
        public void OnEvent_FirstReveal_GivesTipOnceForTheSession()
        {
            // Arrange
            var tips = new TipTracker();
            var reveal = new SimEvent { Type = SimEventType.SocketRevealed };

            // Act + Assert
            Assert.AreEqual("tip_first_reveal", tips.OnEvent(2, reveal));
            Assert.IsNull(tips.OnEvent(3, reveal));
        }

        [Test]
        public void Disabled_GivesNothing_AndDoesNotUseUpTheTip()
        {
            // Arrange
            var tips = new TipTracker { Enabled = false };

            // Act
            var whileOff = tips.OnLevelStart(1);
            tips.Enabled = true;

            // Assert
            Assert.IsNull(whileOff);
            Assert.AreEqual("tip_level1_start", tips.OnLevelStart(1));
        }

        [Test]
        public void Reset_ShowsTipsAgain()
        {
            // Arrange
            var tips = new TipTracker();
            tips.OnLevelStart(1);

            // Act
            tips.Reset();

            // Assert
            Assert.AreEqual("tip_level1_start", tips.OnLevelStart(1));
        }
    }

    /// <summary>Unlock progress (GDD v1.0 §6.1).</summary>
    public class ProgressTests
    {
        [Test]
        public void Fresh_OnlyLevel1IsUnlocked()
        {
            // Arrange + Act
            var p = new Progress(new MemoryProgressStore());

            // Assert
            Assert.AreEqual(1, p.Unlocked);
            Assert.IsTrue(p.IsUnlocked(1));
            Assert.IsFalse(p.IsUnlocked(2));
        }

        [Test]
        public void RecordWin_UnlocksTheNextLevel()
        {
            // Arrange
            var store = new MemoryProgressStore();
            var p = new Progress(store);

            // Act
            p.RecordWin(1, 180);

            // Assert
            Assert.AreEqual(2, p.Unlocked);
            Assert.AreEqual(2, store.Values["trouble.unlocked"]);
        }

        [Test]
        public void RecordWin_OfAnEarlierLevel_DoesNotLockAnything()
        {
            // Arrange
            var p = new Progress(new MemoryProgressStore());
            p.RecordWin(1, 100);
            p.RecordWin(2, 100);

            // Act
            p.RecordWin(1, 90);

            // Assert
            Assert.AreEqual(3, p.Unlocked);
        }

        [Test]
        public void RecordWin_KeepsTheBestTime()
        {
            // Arrange
            var p = new Progress(new MemoryProgressStore());

            // Act
            p.RecordWin(1, 200);
            p.RecordWin(1, 150);
            p.RecordWin(1, 300);

            // Assert
            Assert.AreEqual(150, p.BestTicks(1));
        }

        [Test]
        public void BestTicks_NeverWon_IsMinusOne()
        {
            Assert.AreEqual(-1, new Progress(new MemoryProgressStore()).BestTicks(3));
        }

        [Test]
        public void TotalBestTicks_AddsTheWonLevels()
        {
            // Arrange
            var p = new Progress(new MemoryProgressStore());
            p.RecordWin(1, 100);
            p.RecordWin(2, 250);

            // Act + Assert
            Assert.AreEqual(350, p.TotalBestTicks(4));
        }

        [Test]
        public void SetUnlocked_BelowOne_BecomesOne()
        {
            // Arrange
            var p = new Progress(new MemoryProgressStore());

            // Act
            p.SetUnlocked(0);

            // Assert
            Assert.AreEqual(1, p.Unlocked);
        }
    }

    /// <summary>Screen flow rules (GDD v1.0 §6.1).</summary>
    public class FlowRulesTests
    {
        [TestCase(ScreenId.None, ScreenId.Pause)]
        [TestCase(ScreenId.Pause, ScreenId.None)]
        [TestCase(ScreenId.HowTo, ScreenId.Title)]
        [TestCase(ScreenId.LevelSelect, ScreenId.Title)]
        [TestCase(ScreenId.End, ScreenId.LevelSelect)]
        [TestCase(ScreenId.Title, ScreenId.Title)]
        public void EscapeTarget_GoesBackOneScreen(ScreenId from, ScreenId to)
        {
            Assert.AreEqual(to, FlowRules.EscapeTarget(from));
        }

        [Test]
        public void IsLastLevel_WhenNoNextLevelExists()
        {
            Assert.IsTrue(FlowRules.IsLastLevel(4, n => n <= 4));
            Assert.IsFalse(FlowRules.IsLastLevel(3, n => n <= 4));
        }
    }

    /// <summary>F1 panel helpers: rule editing and the event log.</summary>
    public class DebugToolsTests
    {
        [Test]
        public void TrySet_ReadsIntFloatAndBool()
        {
            // Arrange
            var rules = new RulesConfig();

            // Act
            bool a = RulesEditing.TrySet(rules, "overCoolEvery", "5", out _);
            bool b = RulesEditing.TrySet(rules, "walkSpeed", "2.5", out _);
            bool c = RulesEditing.TrySet(rules, "breakerToggleResetsCounter", "true", out _);

            // Assert
            Assert.IsTrue(a && b && c);
            Assert.AreEqual(5, rules.overCoolEvery);
            Assert.AreEqual(2.5f, rules.walkSpeed, 1e-5f);
            Assert.IsTrue(rules.breakerToggleResetsCounter);
        }

        [Test]
        public void TrySet_BadNumber_ReportsAndKeepsTheValue()
        {
            // Arrange
            var rules = new RulesConfig();

            // Act
            bool ok = RulesEditing.TrySet(rules, "overCoolEvery", "abc", out var error);

            // Assert
            Assert.IsFalse(ok);
            StringAssert.Contains("overCoolEvery", error);
            Assert.AreEqual(new RulesConfig().overCoolEvery, rules.overCoolEvery);
        }

        [Test]
        public void TrySet_UnknownRule_Reports()
        {
            Assert.IsFalse(RulesEditing.TrySet(new RulesConfig(), "nothing", "1", out var error));
            StringAssert.Contains("nothing", error);
        }

        [Test]
        public void Diff_ListsOnlyTheChangedFields_AndRoundTripsThroughWithOverrides()
        {
            // Arrange
            var baseline = new RulesConfig();
            var edited = baseline.Clone();
            edited.overCoolEvery = 7;
            edited.planningIsFree = false;
            edited.walkSpeed = 1.5f;

            // Act
            var diff = RulesEditing.Diff(edited, baseline);
            var rebuilt = baseline.WithOverrides(diff, null);

            // Assert
            Assert.AreEqual(3, diff.Count);
            Assert.AreEqual(7, rebuilt.overCoolEvery);
            Assert.IsFalse(rebuilt.planningIsFree);
            Assert.AreEqual(1.5f, rebuilt.walkSpeed, 1e-5f);
        }

        [Test]
        public void Diff_NoChange_IsEmpty()
        {
            CollectionAssert.IsEmpty(RulesEditing.Diff(new RulesConfig(), new RulesConfig()));
        }

        [Test]
        public void EventLog_KeepsOnlyTheLast30()
        {
            // Arrange
            var log = new EventLog(30);

            // Act
            for (int i = 0; i < 45; i++) log.Add(new SimEvent { Type = SimEventType.ActionDone, Tick = i });
            var lines = new List<string>(log.Lines);

            // Assert
            Assert.AreEqual(30, lines.Count);
            StringAssert.StartsWith("t15 ", lines[0]);
            StringAssert.StartsWith("t44 ", lines[29]);
        }

        [Test]
        public void EventLog_Format_ShowsTickTypeAndLoad()
        {
            // Arrange
            var e = new SimEvent { Type = SimEventType.CircuitTripped, Tick = 9, Circuit = "A", Load = 10, Capacity = 8 };

            // Act + Assert
            Assert.AreEqual("t9 CircuitTripped A 10/8", EventLog.Format(e));
        }
    }
}
