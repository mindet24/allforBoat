using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Game;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>G6 Settings screen rules.</summary>
    public class SettingsRulesTests
    {
        [TestCase(50, 1, 10, 60)]
        [TestCase(95, 1, 10, 100)]
        [TestCase(5, -1, 10, 0)]
        public void StepVolume_StepAndClamp_StaysInRange(int start, int dir, int step, int expected)
        {
            // Act / Assert
            Assert.AreEqual(expected, SettingsRules.StepVolume(start, dir, step));
        }

        [Test]
        public void Factor_Percent_IsZeroToOne()
        {
            // Act / Assert
            Assert.AreEqual(0.4f, SettingsRules.Factor(40), 1e-5f);
            Assert.AreEqual(1f, SettingsRules.Factor(150), 1e-5f);
        }

        [Test]
        public void StepIndex_AtEnds_DoesNotWrap()
        {
            // Act / Assert
            Assert.AreEqual(0, SettingsRules.StepIndex(0, -1, 4));
            Assert.AreEqual(3, SettingsRules.StepIndex(3, 1, 4));
            Assert.AreEqual(-1, SettingsRules.StepIndex(0, 1, 0));
        }

        [Test]
        public void DistinctSizes_DuplicatesAndOrder_AreCollapsedAndSorted()
        {
            // Arrange
            var sizes = new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(0, 0) };

            // Act
            var list = SettingsRules.DistinctSizes(sizes);

            // Assert
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(new Vector2Int(1280, 720), list[0]);
        }

        [Test]
        public void ClosestSize_ExactMatch_FindsIt()
        {
            // Arrange
            var list = new List<Vector2Int> { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) };

            // Act / Assert
            Assert.AreEqual(1, SettingsRules.ClosestSize(list, new Vector2Int(1600, 900)));
            Assert.AreEqual(-1, SettingsRules.ClosestSize(new List<Vector2Int>(), new Vector2Int(1, 1)));
        }
    }

    /// <summary>G6 saved settings.</summary>
    public class UserSettingsTests
    {
        [Test]
        public void Volume_NeverSet_IsHundred()
        {
            // Arrange
            var s = new UserSettings(new MemoryProgressStore());

            // Act / Assert
            Assert.AreEqual(100, s.Volume(VolumeChannel.Music));
            Assert.IsTrue(s.CameraShake);
        }

        [Test]
        public void SetVolume_OutOfRange_IsClampedAndKept()
        {
            // Arrange
            var store = new MemoryProgressStore();
            var s = new UserSettings(store);

            // Act
            s.SetVolume(VolumeChannel.Sfx, 140);
            s.SetVolume(VolumeChannel.Voice, 30);
            var reloaded = new UserSettings(store);

            // Assert
            Assert.AreEqual(100, reloaded.Volume(VolumeChannel.Sfx));
            Assert.AreEqual(30, reloaded.Volume(VolumeChannel.Voice));
        }

        [Test]
        public void CameraShake_TurnedOff_IsRemembered()
        {
            // Arrange
            var store = new MemoryProgressStore();

            // Act
            new UserSettings(store).CameraShake = false;

            // Assert
            Assert.IsFalse(new UserSettings(store).CameraShake);
        }

        [Test]
        public void GroupFactor_MusicAtHalf_IsHalf()
        {
            // Arrange
            var previous = UserSettings.Current;
            var s = new UserSettings(new MemoryProgressStore());
            s.SetVolume(VolumeChannel.Music, 50);
            UserSettings.Current = s;
            try
            {
                // Act / Assert
                Assert.AreEqual(0.5f, UserSettings.GroupFactor(SoundGroup.Music), 1e-5f);
                Assert.AreEqual(1f, UserSettings.GroupFactor(SoundGroup.Sfx), 1e-5f);
            }
            finally
            {
                UserSettings.Current = previous;
            }
        }
    }

    /// <summary>G6 third-party credits file.</summary>
    public class CreditsRulesTests
    {
        [Test]
        public void Parse_CommentsAndBlanks_AreSkipped()
        {
            // Arrange
            const string text = "# header\r\nFont A, OFL\n\n  Sound B, CC0  \n# end";

            // Act
            var lines = CreditsRules.Parse(text);

            // Assert
            Assert.AreEqual(new[] { "Font A, OFL", "Sound B, CC0" }, lines.ToArray());
        }

        [Test]
        public void Parse_Empty_IsEmptyList()
        {
            // Act / Assert
            Assert.AreEqual(0, CreditsRules.Parse(null).Count);
        }
    }

    /// <summary>G6 power panel.</summary>
    public class PowerPanelRulesTests
    {
        private static List<RoomPower> House() => new List<RoomPower>
        {
            new RoomPower { Id = "kitchen", Load = 6, Capacity = 8, On = true },
            new RoomPower { Id = "living", Load = 9, Capacity = 7, On = true, Overloading = true },
            new RoomPower { Id = "study", Load = 4, Capacity = 5, On = false },
        };

        [Test]
        public void Total_DarkRoom_AddsCapacityButNoLoad()
        {
            // Act
            var t = PowerPanelRules.Total(House());

            // Assert
            Assert.AreEqual(new Vector2Int(15, 20), t);
        }

        [Test]
        public void RowsToShow_Overview_ListsEveryRoom()
        {
            // Act / Assert
            Assert.AreEqual(3, PowerPanelRules.RowsToShow(House(), false, "kitchen").Count);
        }

        [Test]
        public void RowsToShow_Following_ListsCurrentAndOverloading()
        {
            // Act
            var rows = PowerPanelRules.RowsToShow(House(), true, "kitchen");

            // Assert
            Assert.AreEqual(new[] { "kitchen", "living" }, rows.ToArray());
        }

        [Test]
        public void AnyOverload_OverloadingRoomOn_IsTrue()
        {
            // Act / Assert
            Assert.IsTrue(PowerPanelRules.AnyOverload(House()));
            Assert.IsFalse(PowerPanelRules.AnyOverload(new List<RoomPower> { new RoomPower { Id = "a", Load = 1, Capacity = 5, On = true } }));
        }

        [TestCase(42, "00:42")]
        [TestCase(125, "02:05")]
        [TestCase(-3, "00:00")]
        public void Clock_Seconds_IsMinutesAndSeconds(int seconds, string expected)
        {
            // Act / Assert
            Assert.AreEqual(expected, PowerPanelRules.Clock(seconds));
        }
    }

    /// <summary>G6 screen flow additions and the family colours.</summary>
    public class G6FlowAndPaletteTests
    {
        [TestCase(ScreenId.Settings, ScreenId.Title)]
        [TestCase(ScreenId.Credits, ScreenId.Title)]
        public void EscapeTarget_SettingsAndCredits_GoBackToTitle(ScreenId from, ScreenId to)
        {
            // Act / Assert
            Assert.AreEqual(to, FlowRules.EscapeTarget(from));
        }

        [Test]
        public void UsesHouseBackdrop_MenusOnly_NotLevelSelectOrPause()
        {
            // Act / Assert
            Assert.IsTrue(FlowRules.UsesHouseBackdrop(ScreenId.Title));
            Assert.IsTrue(FlowRules.UsesHouseBackdrop(ScreenId.Settings));
            Assert.IsFalse(FlowRules.UsesHouseBackdrop(ScreenId.LevelSelect));
            Assert.IsFalse(FlowRules.UsesHouseBackdrop(ScreenId.Pause));
        }

        [Test]
        public void PersonColor_KnownAndUnknown_UseTableOrFallback()
        {
            // Arrange
            var ui = ScriptableObject.CreateInstance<TrippedUiConfig>();
            try
            {
                // Act / Assert
                Assert.AreEqual(ui.personColors[1], ui.PersonColor("dad"));
                Assert.AreEqual(ui.personFallback, ui.PersonColor("Stranger"));
            }
            finally
            {
                Object.DestroyImmediate(ui);
            }
        }
    }
}
