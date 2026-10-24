using System.Collections.Generic;
using NUnit.Framework;
using Trouble.Game;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>Pure rules behind the Day-mode view (level_demo): cards, blackout, house layout over four floors.</summary>
    public class DayViewTests
    {
        private static HouseSim Demo(int seed = 7)
        {
            var level = JsonUtility.FromJson<LevelData>(Resources.Load<TextAsset>("Levels/level_demo").text);
            level.day.seed = seed;
            var rules = JsonUtility.FromJson<RulesConfig>(Resources.Load<TextAsset>("Config/rules").text);
            var catalog = new Catalog(JsonUtility.FromJson<CatalogData>(Resources.Load<TextAsset>("Config/catalog").text));
            return new HouseSim(level, rules, catalog, TestData.Strings());
        }

        [TestCase("urgent", UrgencyLevel.Urgent)]
        [TestCase("URGENT", UrgencyLevel.Urgent)]
        [TestCase("normal", UrgencyLevel.Normal)]
        [TestCase("relaxed", UrgencyLevel.Relaxed)]
        [TestCase("", UrgencyLevel.Normal)]
        [TestCase("whatever", UrgencyLevel.Normal)]
        public void ParseUrgency_MapsIdsAndDefaultsToNormal(string id, UrgencyLevel expected)
        {
            // Arrange + Act
            var level = DayRules.ParseUrgency(id);

            // Assert
            Assert.AreEqual(expected, level);
        }

        [TestCase(0, 200, 0, 1f)]
        [TestCase(0, 200, 100, 0.5f)]
        [TestCase(0, 200, 200, 0f)]
        [TestCase(0, 200, 300, 0f)]
        [TestCase(50, 50, 60, 0f)]
        public void WaitFraction_DrainsFromSpawnToDueAndNeverGoesBelowZero(int spawn, int due, int now, float expected)
        {
            // Arrange + Act
            float f = DayRules.WaitFraction(spawn, due, now);

            // Assert
            Assert.AreEqual(expected, f, 1e-5f);
        }

        [TestCase(1800, 0, 1800)]
        [TestCase(1800, 1790, 10)]
        [TestCase(1800, 1900, 0)]
        public void TicksLeft_IsDayLengthMinusNowNotNegative(int day, int now, int expected)
        {
            // Arrange + Act + Assert
            Assert.AreEqual(expected, DayRules.TicksLeft(day, now));
        }

        [Test]
        public void RoomLoadText_IsNameThenLoadSlashCapacity()
        {
            // Arrange + Act
            var text = DayRules.RoomLoadText("Kitchen", 6, 8);

            // Assert
            Assert.AreEqual("Kitchen 6/8", text);
        }

        [TestCase(3, 3, 3)]
        [TestCase(2, 3, 2)]
        [TestCase(0, 3, 0)]
        [TestCase(-1, 3, 0)]
        [TestCase(5, 3, 3)]
        public void FilledHearts_IsClampedToZeroAndMax(int hearts, int max, int expected)
        {
            // Arrange + Act + Assert
            Assert.AreEqual(expected, DayRules.FilledHearts(hearts, max));
        }

        [Test]
        public void DemoLevel_LoadsAsADayWithEightRoomsOnFourFloorsAndARoomCircuitEach()
        {
            // Arrange + Act
            var sim = Demo();

            // Assert
            Assert.IsTrue(sim.IsDayMode);
            Assert.AreEqual(8, sim.Rooms.Count);
            Assert.AreEqual(8, sim.Circuits.Count);
            int maxFloor = 0;
            foreach (var room in sim.Rooms)
            {
                maxFloor = Mathf.Max(maxFloor, room.Floor);
                Assert.IsNotNull(sim.FindCircuit(room.Id), room.Id);
                Assert.IsTrue(room.Visible, room.Id + " should be visible (sightDepth 99)");
            }
            Assert.AreEqual(3, maxFloor);
            Assert.AreEqual("laundry", sim.PanelRoomId);
        }

        [Test]
        public void IsBlackout_FreshDemo_IsFalse()
        {
            // Arrange
            var sim = Demo();

            // Act + Assert
            Assert.IsFalse(DayRules.IsBlackout(sim));
        }

        [Test]
        public void IsBlackout_ScriptedLevelWithoutDayMode_IsAlwaysFalse()
        {
            // Arrange
            var sim = new LevelBuilder().Room("a").Circuit("A", 4).Socket("S1", "a", "A").Sim(TestData.V11Rules());

            // Act + Assert
            Assert.IsFalse(DayRules.IsBlackout(sim));
        }

        [Test]
        public void ActiveCount_FreshDemo_IsZeroBeforeAnyRequestSpawns()
        {
            // Arrange
            var sim = Demo();

            // Act + Assert
            Assert.AreEqual(0, DayRules.ActiveCount(sim));
        }

        [Test]
        public void ActiveCount_AfterTheFirstRequestSpawns_CountsIt()
        {
            // Arrange: firstRequestSec is 3, so a request exists after 4 s of running.
            var sim = Demo();
            sim.Start();
            for (int i = 0; i < 40; i++) sim.Step();

            // Act
            int active = DayRules.ActiveCount(sim);

            // Assert
            Assert.GreaterOrEqual(active, 1);
            var first = sim.Requests[0];
            Assert.Greater(first.Serial, 0);
            Assert.IsTrue(DayRules.WaitFraction(first.SpawnTick, first.DueTicks, sim.Now) > 0f);
        }

        [Test]
        public void Walls_DemoHouse_HasTwelveWallsAndOnlyDoorsMakeOpenings()
        {
            // Arrange
            var sim = Demo();

            // Act
            var walls = HouseLayout.Walls(sim.Rooms, sim.Doors);

            // Assert: 3 + 3 + 4 + 2 edges; doors at x 4 (floor 0), 9 (floor 1), 6 and 11 (floor 2); stairs never open a wall.
            Assert.AreEqual(12, walls.Count);
            var withDoor = walls.FindAll(w => !string.IsNullOrEmpty(w.DoorId));
            Assert.AreEqual(4, withDoor.Count);
            Assert.IsTrue(withDoor.Exists(w => w.DoorId == "D_workshop_laundry" && Mathf.Approximately(w.X, 4f) && w.Floor == 0));
            Assert.IsTrue(withDoor.Exists(w => w.DoorId == "D_living_kitchen" && Mathf.Approximately(w.X, 9f) && w.Floor == 1));
            Assert.IsTrue(withDoor.Exists(w => w.DoorId == "D_bedroom_bathroom" && Mathf.Approximately(w.X, 6f) && w.Floor == 2));
            Assert.IsTrue(withDoor.Exists(w => w.DoorId == "D_bathroom_kidroom" && Mathf.Approximately(w.X, 11f) && w.Floor == 2));
        }

        [Test]
        public void HouseBounds_DemoHouse_IsSeventeenMetresWideAndFourStoreysHigh()
        {
            // Arrange
            var sim = Demo();

            // Act
            var bounds = HouseLayout.HouseBounds(sim.Rooms, 3f);

            // Assert
            Assert.AreEqual(new Rect(0f, 0f, 17f, 12f), bounds);
        }

        [Test]
        public void UncoveredSpans_NothingAbove_IsTheWholeSpan()
        {
            // Arrange + Act
            var spans = HouseLayout.UncoveredSpans(0f, 6f, new List<Vector2>());

            // Assert
            Assert.AreEqual(1, spans.Count);
            Assert.AreEqual(new Vector2(0f, 6f), spans[0]);
        }

        [Test]
        public void UncoveredSpans_RoomAboveCoversPartOfIt_KeepsTheRest()
        {
            // Arrange: bedroom 0..6 with the study 4..13 above it.
            var covers = new List<Vector2> { new Vector2(4f, 13f) };

            // Act
            var spans = HouseLayout.UncoveredSpans(0f, 6f, covers);

            // Assert
            Assert.AreEqual(1, spans.Count);
            Assert.AreEqual(new Vector2(0f, 4f), spans[0]);
        }

        [Test]
        public void UncoveredSpans_FullyCovered_LeavesNothing()
        {
            // Arrange: the basement 4..13 under living 0..9 and kitchen 9..17.
            var covers = new List<Vector2> { new Vector2(0f, 9f), new Vector2(9f, 17f) };

            // Act
            var spans = HouseLayout.UncoveredSpans(4f, 13f, covers);

            // Assert
            Assert.AreEqual(0, spans.Count);
        }

        [Test]
        public void UncoveredSpans_CoverInTheMiddle_SplitsInTwo()
        {
            // Arrange + Act
            var spans = HouseLayout.UncoveredSpans(0f, 10f, new List<Vector2> { new Vector2(4f, 6f) });

            // Assert
            Assert.AreEqual(2, spans.Count);
            Assert.AreEqual(new Vector2(0f, 4f), spans[0]);
            Assert.AreEqual(new Vector2(6f, 10f), spans[1]);
        }

        [Test]
        public void OverlapSpan_KitchenAboveBasement_IsTheSharedFourMetres()
        {
            // Arrange
            var sim = Demo();

            // Act
            var span = HouseLayout.OverlapSpan(sim.FindRoom("laundry"), sim.FindRoom("kitchen"));

            // Assert
            Assert.AreEqual(9f, span.x, 1e-4f);
            Assert.AreEqual(13f, span.y, 1e-4f);
        }

        [Test]
        public void UrgencyColor_UsesTheConfigColours()
        {
            // Arrange
            var config = ScriptableObject.CreateInstance<TrippedViewConfig>();

            // Act
            var urgent = config.UrgencyColor(UrgencyLevel.Urgent);
            var normal = config.UrgencyColor(UrgencyLevel.Normal);
            var relaxed = config.UrgencyColor(UrgencyLevel.Relaxed);
            Object.DestroyImmediate(config);

            // Assert
            Assert.AreEqual(TrippedViewConfig.Hex("#E53935"), urgent);
            Assert.AreEqual(TrippedViewConfig.Hex("#FFB300"), normal);
            Assert.AreEqual(TrippedViewConfig.Hex("#8A8A94"), relaxed);
        }

        [Test]
        public void PanelClick_InDay_ResetsTheBreakersWithOneCommandOnTheEmptyCircuit()
        {
            // Arrange
            var click = new ClickInput { Kind = ClickKind.Panel, Id = "laundry", RoomId = "laundry", DayMode = true };

            // Act
            var outcome = ClickResolver.Resolve(Held.Appliance("kettle"), click);

            // Assert
            Assert.IsTrue(outcome.HasCommand);
            Assert.AreEqual(CommandType.ToggleBreaker, outcome.Command.Type);
            Assert.AreEqual("", outcome.Command.A);
            Assert.IsTrue(outcome.Selection.IsNone);
        }

        [Test]
        public void PanelClick_OutsideDay_JustWalksToThePanelRoom()
        {
            // Arrange
            var click = new ClickInput { Kind = ClickKind.Panel, Id = "kitchen", RoomId = "kitchen", DayMode = false };

            // Act
            var outcome = ClickResolver.Resolve(Held.None, click);

            // Assert
            Assert.AreEqual(CommandType.MoveTo, outcome.Command.Type);
            Assert.AreEqual("kitchen", outcome.Command.A);
        }

        [Test]
        public void ApplianceClick_InDay_SelectsItInsteadOfSwitchingIt()
        {
            // Arrange
            var click = new ClickInput { Kind = ClickKind.WorldAppliance, Id = "kettle", SwitchedOn = false, DayMode = true };

            // Act
            var outcome = ClickResolver.Resolve(Held.None, click);
            var again = ClickResolver.Resolve(outcome.Selection, click);

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.AreEqual("kettle", outcome.Selection.ApplianceId);
            Assert.IsFalse(again.HasCommand);
            Assert.IsTrue(again.Selection.IsNone);
        }

        [Test]
        public void SocketClick_InDayWithAnApplianceSelected_PlugsIt()
        {
            // Arrange
            var click = new ClickInput { Kind = ClickKind.Socket, Id = "L1", RoomId = "living", DayMode = true };

            // Act
            var outcome = ClickResolver.Resolve(Held.Appliance("kettle"), click);

            // Assert
            Assert.AreEqual(CommandType.Plug, outcome.Command.Type);
            Assert.AreEqual("kettle", outcome.Command.A);
            Assert.AreEqual("L1", outcome.Command.B);
        }

        [Test]
        public void RightClick_InDay_NeverUnplugs()
        {
            // Arrange
            var click = new ClickInput { Kind = ClickKind.WorldAppliance, Id = "kettle", Right = true, DayMode = true };

            // Act
            var outcome = ClickResolver.Resolve(Held.Appliance("kettle"), click);

            // Assert
            Assert.IsFalse(outcome.HasCommand);
            Assert.IsTrue(outcome.Selection.IsNone);
        }
    }
}
