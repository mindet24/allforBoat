using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Game;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>The generated day/UI sound set.</summary>
    public class SynthBankTests
    {
        private static readonly string[] Required =
        {
            "step", "jump", "running", "drop", "clock", "jetpack", "pickup", "plug_in", "reject",
            "request_new_relaxed", "request_new_normal", "request_new_urgent", "request_done", "request_missed",
            "urgent_tick", "heart_lost", "combo_up", "overload_hum", "spark", "blackout", "power_up", "breaker_flip",
            "kettle_loop", "toaster_loop", "microwave_loop", "fridge_loop", "tv_loop", "heater_loop", "washer_loop",
            "dryer_loop", "blender_loop", "toaster_pop", "ui_hover", "ui_click", "ui_back", "ui_pause", "countdown_beep",
            "day_start", "win_jingle", "lose_jingle", "star_pop", "score_tick", "music_menu", "music_day",
            "music_day_final", "voice_happy", "voice_angry", "voice_hey",
        };

        [Test]
        public void Names_ContainEveryAgreedName()
        {
            foreach (var name in Required) Assert.IsTrue(SynthBank.Knows(name), name);
        }

        [Test]
        public void Generate_EveryName_PeaksAtOne()
        {
            foreach (var name in SynthBank.Names)
            {
                // Arrange + Act
                var s = SynthBank.Generate(name, 8000);
                float max = 0f;
                foreach (var v in s) max = Mathf.Max(max, Mathf.Abs(v));

                // Assert
                Assert.AreEqual(1f, max, 1e-4f, name);
            }
        }

        [Test]
        public void Generate_SameName_IsDeterministic()
        {
            CollectionAssert.AreEqual(SynthBank.Generate("blackout", 8000), SynthBank.Generate("blackout", 8000));
        }

        [Test]
        public void Generate_UnknownName_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => SynthBank.Generate("nope_not_here"));
        }

        [Test]
        public void Loops_HumMusicAndApplianceLoops_Only()
        {
            Assert.IsTrue(SynthBank.Loops("overload_hum"));
            Assert.IsTrue(SynthBank.Loops("music_day"));
            Assert.IsTrue(SynthBank.Loops("washer_loop"));
            Assert.IsFalse(SynthBank.Loops("pickup"));
        }

        [Test]
        public void GroupOf_SortsIntoMixerGroups()
        {
            Assert.AreEqual(SoundGroup.Music, SynthBank.GroupOf("music_menu"));
            Assert.AreEqual(SoundGroup.Ui, SynthBank.GroupOf("ui_click"));
            Assert.AreEqual(SoundGroup.Ui, SynthBank.GroupOf("countdown_beep"));
            Assert.AreEqual(SoundGroup.Voice, SynthBank.GroupOf("voice_hey"));
            Assert.AreEqual(SoundGroup.Sfx, SynthBank.GroupOf("blackout"));
        }

        [Test]
        public void MusicDay_LastsFourBars()
        {
            Assert.AreEqual(4 * 4 * 60f / SynthBank.DayBpm, SynthBank.Duration("music_day"), 1e-4f);
        }

        [Test]
        public void JuiceConfig_DefaultVolumes_CoverEveryName()
        {
            // Arrange
            var config = ScriptableObject.CreateInstance<JuiceConfig>();

            // Act + Assert
            foreach (var name in SynthBank.Names) Assert.IsTrue(config.TryVolume(name, out _), name);
            Object.DestroyImmediate(config);
        }
    }
}
