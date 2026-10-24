using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Game;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>The day event → sound table.</summary>
    public class DaySoundsTests
    {
        private static List<string> For(SimEventType type, UrgencyLevel u = UrgencyLevel.Normal)
        {
            var list = new List<string>();
            DaySounds.For(type, u, list);
            return list;
        }

        [Test]
        public void RequestSpawned_Urgent_PlaysUrgentDing()
        {
            CollectionAssert.AreEqual(new[] { "request_new_urgent" }, For(SimEventType.RequestSpawned, UrgencyLevel.Urgent));
        }

        [Test]
        public void RequestSpawned_Relaxed_PlaysRelaxedDing()
        {
            CollectionAssert.AreEqual(new[] { "request_new_relaxed" }, For(SimEventType.RequestSpawned, UrgencyLevel.Relaxed));
        }

        [Test]
        public void CircuitTripped_PlaysBlackout()
        {
            CollectionAssert.AreEqual(new[] { "blackout" }, For(SimEventType.CircuitTripped));
        }

        [Test]
        public void BreakerSwitched_PlaysFlipThenPowerUp()
        {
            CollectionAssert.AreEqual(new[] { "breaker_flip", "power_up" }, For(SimEventType.BreakerSwitched));
        }

        [Test]
        public void HeartLost_PlaysHeartLost()
        {
            CollectionAssert.AreEqual(new[] { "heart_lost" }, For(SimEventType.HeartLost));
        }

        [Test]
        public void RequestMissedAndDone_PlayTheirSounds()
        {
            CollectionAssert.AreEqual(new[] { "request_missed" }, For(SimEventType.RequestMissed));
            CollectionAssert.AreEqual(new[] { "request_done" }, For(SimEventType.RequestCompleted));
        }

        [Test]
        public void WinAndLose_PlayJingles()
        {
            CollectionAssert.AreEqual(new[] { "win_jingle" }, For(SimEventType.LevelWon));
            CollectionAssert.AreEqual(new[] { "lose_jingle" }, For(SimEventType.LevelLost));
        }

        [Test]
        public void CharacterEnteredRoom_PlaysNothing()
        {
            CollectionAssert.IsEmpty(For(SimEventType.CharacterEnteredRoom));
        }

        [Test]
        public void OncePerTick_BreakerSwitched_True_RequestSpawned_False()
        {
            Assert.IsTrue(DaySounds.OncePerTick(SimEventType.BreakerSwitched));
            Assert.IsFalse(DaySounds.OncePerTick(SimEventType.RequestSpawned));
        }
    }
}
