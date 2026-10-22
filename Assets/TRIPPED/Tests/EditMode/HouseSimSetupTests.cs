using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>
    /// The sim builds the right starting state from the shipped level files.
    /// These load the real JSON on purpose: they check the data files as much as the code.
    /// </summary>
    public class HouseSimSetupTests
    {
        private static HouseSim LoadLevel(int n)
        {
            var rules = JsonUtility.FromJson<RulesConfig>(Resources.Load<TextAsset>("Config/rules").text);
            var catalog = new Catalog(JsonUtility.FromJson<CatalogData>(Resources.Load<TextAsset>("Config/catalog").text));
            var level = JsonUtility.FromJson<LevelData>(Resources.Load<TextAsset>($"Levels/level{n}").text);
            return new HouseSim(level, rules, catalog);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void LoadLevel_StartsInPlanningWithWholeHouseVisible(int n)
        {
            // Arrange + Act
            var sim = LoadLevel(n);

            // Assert
            Assert.AreEqual(LevelPhase.Planning, sim.Phase);
            Assert.AreEqual(0, sim.Now);
            foreach (var room in sim.Rooms) Assert.IsTrue(room.Visible, room.Id);
        }

        [Test]
        public void LoadLevel1_BuildsRoomsSocketsAndRequestAppliances()
        {
            // Arrange + Act
            var sim = LoadLevel(1);

            // Assert
            Assert.AreEqual(2, sim.Rooms.Count);
            Assert.AreEqual(1, sim.Doors.Count, "rooms next to each other get an automatic door");
            Assert.AreEqual(3, sim.Sockets.Count);
            Assert.AreEqual(2, sim.Appliances.Count);
            Assert.AreEqual(6, sim.FindAppliance("kettle").Load);
            Assert.AreEqual(80, sim.FindRequest("kettle").DurationTicks);
            Assert.AreEqual(350, sim.DayLengthTicks);
            Assert.AreEqual("kitchen", sim.Character.RoomId);
            Assert.AreEqual("kitchen", sim.PanelRoomId);
        }

        [Test]
        public void LoadLevel3_RoutineHoldsItsSocket()
        {
            // Arrange + Act
            var sim = LoadLevel(3);

            // Assert
            var heater = sim.FindAppliance("heater");
            Assert.IsTrue(heater.IsRoutine);
            Assert.AreEqual("L1", heater.SocketId);
            Assert.IsTrue(sim.FindSocket("L1").Reserved);
            Assert.AreEqual(200, sim.Routines[0].StartTick);
            Assert.AreEqual(400, sim.Routines[0].EndTick);
            Assert.AreEqual(HouseSim.FridgeId, sim.FindSocket("K2").OccupantId);
        }

        [Test]
        public void WithOverrides_ChangesOnlyNamedRulesAndReportsUnknownKeys()
        {
            // Arrange
            var errors = new List<string>();
            var overrides = new[]
            {
                new RuleOverrideData { key = "sightDepth", value = "2" },
                new RuleOverrideData { key = "planningIsFree", value = "false" },
                new RuleOverrideData { key = "noSuchRule", value = "1" },
            };

            // Act
            var rules = new RulesConfig().WithOverrides(overrides, errors);

            // Assert
            Assert.AreEqual(2, rules.sightDepth);
            Assert.IsFalse(rules.planningIsFree);
            Assert.AreEqual(3, rules.overCoolEvery);
            Assert.AreEqual(1, errors.Count);
        }
    }
}
