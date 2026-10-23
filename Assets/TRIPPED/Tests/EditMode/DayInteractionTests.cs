using System;
using System.Collections.Generic;
using NUnit.Framework;
using Trouble.EditorTools;
using Trouble.Game;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Tests
{
    /// <summary>
    /// Day-mode keyboard prompts (<see cref="DayInteraction"/>) must show exactly what the sim does with E, W and S,
    /// and the climb path (<see cref="HouseLayout.ClimbPoint"/>) must start and end where the sim puts the character.
    /// </summary>
    public class DayInteractionTests
    {
        // Two rooms side by side: a 0–6 m, b 6–12 m. Sockets A1 2 m, A2 4 m, B1 7.5 m (kettle), B2 9 m (toaster),
        // B3 10.5 m. The character starts at 9 m in b; the panel hangs at 6.6 m in b.
        private static LevelData TwoRooms()
        {
            var level = new LevelData { id = 901, dayLengthSec = 600f, startRoom = "b", panelRoom = "b" };
            level.rooms.Add(new RoomData { id = "a", name = "Room A", capacity = 6 });
            level.rooms.Add(new RoomData { id = "b", name = "Room B", capacity = 20 });
            level.doors.Add(new DoorData { id = "D", a = "a", b = "b" });
            foreach (var id in new[] { "A1", "A2" }) level.sockets.Add(new SocketData { id = id, room = "a" });
            foreach (var id in new[] { "B1", "B2", "B3" }) level.sockets.Add(new SocketData { id = id, room = "b" });
            level.appliances.Add(new ApplianceData { id = "kettle", type = "kettle", socket = "B1", load = 6, useSec = 8f });
            level.appliances.Add(new ApplianceData { id = "toaster", type = "toaster", socket = "B2", load = 3, useSec = 12f });
            var day = level.day;
            day.enabled = true;
            day.seed = 11;
            day.hearts = 3;
            day.firstRequestSec = 0f;
            day.intervalStartSec = 10_000f;
            day.intervalEndSec = 10_000f;
            day.intervalJitter = 0f;
            day.bundleChance = 1f;
            day.urgencies.Add(new UrgencyData { id = "urgent", dueSec = 300f, points = 3, costsHeart = true, weight = 1f });
            // Two urgencies so the bundle holds both the kettle and the toaster (as DayModeTests' overload setup does).
            day.urgencies.Add(new UrgencyData { id = "normal", dueSec = 300f, points = 2, costsHeart = false, weight = 1f });
            day.people.Add(new PersonData { name = "Mum", rooms = new List<string> { "a" }, wants = new List<string> { "kettle", "toaster" } });
            return level;
        }

        private static RulesConfig Rules()
        {
            var rules = TestData.V11Rules();
            rules.overTripTicks = 30;
            rules.counterAfterTrip = "reset";
            rules.sightDepth = 99;
            return rules;
        }

        private static HouseSim Started(LevelData level = null)
        {
            var sim = new HouseSim(level ?? TwoRooms(), Rules(), TestData.Catalog(), TestData.Strings());
            sim.Start();
            return sim;
        }

        private static HouseSim Demo()
        {
            var level = JsonUtility.FromJson<LevelData>(Resources.Load<TextAsset>("Levels/level_demo").text);
            level.day.seed = 3;
            var catalog = new Catalog(JsonUtility.FromJson<CatalogData>(Resources.Load<TextAsset>("Config/catalog").text));
            var sim = new HouseSim(level, Rules(), catalog, TestData.Strings());
            sim.Start();
            return sim;
        }

        private static void Submit(HouseSim sim, Command command)
        {
            Assert.IsTrue(sim.Submit(command, out var reason), reason);
        }

        private static void WalkTicks(HouseSim sim, int dir, int ticks)
        {
            Submit(sim, Command.Walk(dir));
            for (int i = 0; i < ticks && sim.Phase == LevelPhase.Running; i++) sim.Step();
            Submit(sim, Command.Walk(0));
        }

        private static void WalkTo(HouseSim sim, float metres)
        {
            int dir = Math.Sign(metres - sim.Character.X);
            if (dir == 0) return;
            Submit(sim, Command.Walk(dir));
            for (int guard = 0; guard < 1000 && Math.Abs(sim.Character.X - metres) > 0.15f; guard++)
            {
                if (Math.Sign(metres - sim.Character.X) != dir) break;
                sim.Step();
            }
            Submit(sim, Command.Walk(0));
        }

        // ---- E: what the prompt shows is what E does -------------------------------------

        [Test]
        public void HandsEmptyAnywhereOnTheFloor_PromptMatchesWhatInteractPicksUp()
        {
            for (int dir = -1; dir <= 1; dir += 2)
            for (int ticks = 0; ticks <= 40; ticks += 2)
            {
                // Arrange
                var sim = Started();
                WalkTicks(sim, dir, ticks);
                var target = DayInteraction.InteractTarget(sim);

                // Act
                bool accepted = sim.Submit(Command.Interact(), out _);

                // Assert
                string at = $"dir {dir}, {ticks} ticks, x {sim.Character.X}";
                Assert.AreEqual(accepted, target.Kind != DayTargetKind.None, at);
                if (accepted)
                {
                    Assert.AreEqual(DayTargetKind.Appliance, target.Kind, at);
                    Assert.AreEqual(target.Id, sim.Character.CarryingId, at);
                }
            }
        }

        [Test]
        public void CarryingAnywhereOnTheFloor_PromptMatchesTheSocketInteractPlugsInto()
        {
            for (int ticks = 0; ticks <= 40; ticks += 2)
            {
                // Arrange: pick up the kettle at 7.5 m, then walk left.
                var sim = Started();
                WalkTo(sim, 7.5f);
                Submit(sim, Command.Interact());
                WalkTicks(sim, -1, ticks);
                var target = DayInteraction.InteractTarget(sim);

                // Act
                bool accepted = sim.Submit(Command.Interact(), out _);

                // Assert
                string at = $"{ticks} ticks, x {sim.Character.X}";
                Assert.AreEqual(accepted, target.Kind != DayTargetKind.None, at);
                if (accepted)
                {
                    Assert.AreEqual(DayTargetKind.Socket, target.Kind, at);
                    Assert.AreEqual(target.Id, sim.FindAppliance("kettle").SocketId, at);
                }
            }
        }

        [Test]
        public void CarryingNearAFreeSocket_TargetsTheSocketNotTheApplianceBesideIt()
        {
            // Arrange: carry the kettle from B1 back towards B2 (toaster, occupied) and B3 (free).
            var sim = Started();
            WalkTo(sim, 7.5f);
            Submit(sim, Command.Interact());
            WalkTo(sim, 10.2f);

            // Act
            var target = DayInteraction.InteractTarget(sim);

            // Assert
            Assert.AreEqual(DayTargetKind.Socket, target.Kind);
            Assert.AreEqual("B3", target.Id);
            Assert.AreEqual(10.5f, target.X, 1e-4f);
        }

        [Test]
        public void BlackoutAtThePanel_TargetsThePanelBeforeAnything()
        {
            // Arrange: kettle 6 + toaster 3 into room a (capacity 6) trips it; walk back to the panel.
            var sim = Started();
            WalkTo(sim, 7.5f);
            Submit(sim, Command.Interact());
            WalkTo(sim, 2f);
            Submit(sim, Command.Interact());
            WalkTo(sim, 9f);
            Submit(sim, Command.Interact());
            WalkTo(sim, 4f);
            Submit(sim, Command.Interact());
            for (int i = 0; i < 60 && !DayInteraction.AnyBreakerOff(sim); i++) sim.Step();
            Assert.IsTrue(DayInteraction.AnyBreakerOff(sim), "The overload should have blacked out the house.");
            WalkTo(sim, 6.6f);

            // Act
            var target = DayInteraction.InteractTarget(sim);
            Submit(sim, Command.Interact());

            // Assert
            Assert.AreEqual(DayTargetKind.Panel, target.Kind);
            Assert.AreEqual("b", target.Id);
            Assert.AreEqual(sim.PanelX, target.X, 1e-4f);
        }

        [Test]
        public void NoBlackoutAtThePanel_DoesNotTargetThePanel()
        {
            // Arrange
            var sim = Started();
            WalkTo(sim, 6.6f);

            // Act
            var target = DayInteraction.InteractTarget(sim);

            // Assert
            Assert.AreNotEqual(DayTargetKind.Panel, target.Kind);
        }

        [Test]
        public void WhileClimbing_NothingIsPrompted()
        {
            // Arrange: the demo starts by the stairs up to the bedroom.
            var sim = Demo();
            Submit(sim, Command.Climb(1));
            sim.Step();
            var stairs = new List<StairsPrompt>();

            // Act
            var target = DayInteraction.InteractTarget(sim);
            DayInteraction.StairsInReach(sim, stairs);

            // Assert
            Assert.IsTrue(sim.Character.IsClimbing);
            Assert.AreEqual(DayTargetKind.None, target.Kind);
            Assert.IsEmpty(stairs);
        }

        // ---- W / S -----------------------------------------------------------------------

        [Test]
        public void AlongTheLivingRoom_StairsPromptMatchesWhetherClimbIsAccepted()
        {
            for (int ticks = 0; ticks <= 30; ticks += 2)
            {
                // Arrange
                var sim = Demo();
                WalkTicks(sim, 1, ticks);
                var stairs = new List<StairsPrompt>();
                DayInteraction.StairsInReach(sim, stairs);
                bool upShown = stairs.Exists(p => p.Direction == 1);

                // Act
                bool accepted = sim.Submit(Command.Climb(1), out _);

                // Assert
                Assert.AreEqual(accepted, upShown, $"{ticks} ticks, x {sim.Character.X}");
            }
        }

        [Test]
        public void AtTheBedroomStairs_OnlyDownIsPrompted()
        {
            // Arrange
            var sim = Demo();
            Submit(sim, Command.Climb(1));
            for (int i = 0; i < 100 && sim.Character.IsClimbing; i++) sim.Step();
            var stairs = new List<StairsPrompt>();

            // Act
            DayInteraction.StairsInReach(sim, stairs);

            // Assert
            Assert.AreEqual("bedroom", sim.Character.RoomId);
            Assert.AreEqual(1, stairs.Count);
            Assert.AreEqual(-1, stairs[0].Direction);
            Assert.AreEqual("S_living_bedroom", stairs[0].DoorId);
        }

        // ---- climb path --------------------------------------------------------------------

        [TestCase(0f, 5f, 0f)]
        [TestCase(1f, 5f, 3f)]
        [TestCase(0.5f, 5f, 1.5f)]
        public void ClimbPointGoingUp_StartsAndEndsAtStairsXAndPassesTheMiddle(float progress, float expectedX, float expectedY)
        {
            // Arrange + Act
            var p = HouseLayout.ClimbPoint(5f, 0f, 3f, progress, 1f, 0.15f);

            // Assert
            Assert.AreEqual(expectedX, p.x, 1e-4f);
            Assert.AreEqual(expectedY, p.y, 1e-4f);
        }

        [Test]
        public void ClimbPointAtEndOfLanding_IsTheFootOfTheStairs()
        {
            // Arrange + Act
            var up = HouseLayout.ClimbPoint(5f, 0f, 3f, 0.15f, 1f, 0.15f);
            var down = HouseLayout.ClimbPoint(5f, 3f, 0f, 0.85f, 1f, 0.15f);

            // Assert
            Assert.AreEqual(new Vector2(4f, 0f), up);
            Assert.AreEqual(new Vector2(4f, 0f), down, "Going down runs the same path backwards.");
        }

        [Test]
        public void DayCharacterPoint_StandingIsTheSimXOnTheRoomFloor()
        {
            // Arrange
            var sim = Started();
            WalkTicks(sim, -1, 5);

            // Act
            var p = HouseLayout.CharacterPoint(sim, 0.5f, 3f, 1f, 0.15f);

            // Assert
            Assert.AreEqual(sim.Character.X, p.x, 1e-4f);
            Assert.AreEqual(0f, p.y, 1e-4f);
        }

        [Test]
        public void DayCharacterPoint_HalfwayUpTheStairs_IsBetweenTheFloorsAtStairsX()
        {
            // Arrange: living (floor 1) to bedroom (floor 2) over 20 ticks.
            var sim = Demo();
            Submit(sim, Command.Climb(1));
            for (int i = 0; i < sim.Character.ClimbTicks / 2; i++) sim.Step();
            float stairsX = sim.StairsX(sim.FindDoor("S_living_bedroom"));

            // Act
            var p = HouseLayout.CharacterPoint(sim, 0f, 3f, 1f, 0.15f);

            // Assert
            Assert.AreEqual(stairsX, p.x, 1e-3f);
            Assert.AreEqual(4.5f, p.y, 1e-3f);
        }

        // ---- panel art import --------------------------------------------------------------

        [Test]
        public void PropArt_ImportsAsSpriteWithBottomCentrePivot()
        {
            // Act
            var plan = ArtImportRules.Plan("Assets/TRIPPED/Resources/Art/Props/panel.png", 512, 512, null);

            // Assert
            Assert.AreEqual(ArtKind.Prop, plan.Kind);
            Assert.AreEqual(new Vector2(0.5f, 0f), plan.Pivot);
            Assert.AreEqual(512f, plan.PixelsPerUnit);
            Assert.IsEmpty(plan.Problems);
        }
    }
}
