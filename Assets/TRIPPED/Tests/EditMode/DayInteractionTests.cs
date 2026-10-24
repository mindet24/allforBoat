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
    /// and the character's feet (<see cref="HouseLayout.DayCharacterPoint"/>) must be where the sim puts them.
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

        /// <summary>The shipped demo level, floors joined the way <paramref name="verticalMove"/> says (default: jumping).</summary>
        private static HouseSim Demo(string verticalMove = "jump")
        {
            var level = JsonUtility.FromJson<LevelData>(Resources.Load<TextAsset>("Levels/level_demo").text);
            level.day.seed = 3;
            var catalog = new Catalog(JsonUtility.FromJson<CatalogData>(Resources.Load<TextAsset>("Config/catalog").text));
            var rules = Rules();
            rules.verticalMove = verticalMove;
            var sim = new HouseSim(level, rules, catalog, TestData.Strings());
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

        /// <summary>Walks to <paramref name="metres"/> along the open front (where the doors are), then to the back wall, where sockets, plugged appliances and the panel are in reach.</summary>
        private static void WalkTo(HouseSim sim, float metres)
        {
            WalkDepthTo(sim, 0);
            WalkAlong(sim, metres);
            WalkDepthTo(sim, sim.BackZCm);
        }

        /// <summary>Holds A/D until the character is at <paramref name="metres"/>, at whatever depth it stands.</summary>
        private static void WalkAlong(HouseSim sim, float metres)
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

        /// <summary>Holds W/S until the character is <paramref name="zCm"/> in from the open front.</summary>
        private static void WalkDepthTo(HouseSim sim, int zCm)
        {
            int dir = Math.Sign(zCm - sim.Character.ZCm);
            if (dir == 0) return;
            Submit(sim, Command.WalkDepth(dir));
            for (int guard = 0; guard < 1000 && Math.Sign(zCm - sim.Character.ZCm) == dir && !sim.Character.IsAirborne && sim.Phase == LevelPhase.Running; guard++)
                sim.Step();
            Submit(sim, Command.WalkDepth(0));
        }

        /// <summary>Starts a jump up from x 2 in the demo's living room (under the bedroom).</summary>
        private static void JumpUp(HouseSim sim)
        {
            WalkAlong(sim, 2f);
            Submit(sim, Command.Jump(1));
        }

        /// <summary>Takes off with the jetpack from x 2 in the demo's living room (under the bedroom): Space held, one tick.</summary>
        private static void TakeOff(HouseSim sim)
        {
            WalkAlong(sim, 2f);
            Submit(sim, Command.Thrust(true));
            sim.Step();
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
            Submit(sim, Command.TogglePlug());
            WalkTo(sim, 9f);
            Submit(sim, Command.Interact());
            WalkTo(sim, 4f);
            Submit(sim, Command.Interact());
            Submit(sim, Command.TogglePlug());
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
        public void WhileJumping_NothingIsPrompted()
        {
            // Arrange
            var sim = Demo();
            JumpUp(sim);
            sim.Step();

            // Act
            var e = DayInteraction.InteractTarget(sim);
            var f = DayInteraction.PlugTarget(sim);

            // Assert
            Assert.IsTrue(sim.Character.IsJumping);
            Assert.AreEqual(DayTargetKind.None, e.Kind);
            Assert.AreEqual(DayTargetKind.None, f.Kind);
        }

        [TestCase(0f, 0f)]
        [TestCase(1f, 3f)]
        [TestCase(0.5f, 2.5f)]
        public void JumpPoint_RisesFromTheFloorToTheNextWithTheArcOnTop(float progress, float expectedY)
        {
            // Arrange + Act: floor 0 to floor 3, 1 m arc.
            float y = HouseLayout.JumpPoint(0f, 3f, progress, 1f);

            // Assert
            Assert.AreEqual(expectedY, y, 1e-4f);
        }

        [Test]
        public void DayCharacterPoint_HalfwayThroughAJump_IsAboveTheMiddleAtTheSimX()
        {
            // Arrange: living (floor 1, y 3) to bedroom (floor 2, y 6), 1 m arc.
            var sim = Demo();
            JumpUp(sim);
            for (int i = 0; i < sim.Character.JumpTicks / 2; i++) sim.Step();

            // Act
            var p = HouseLayout.CharacterPoint(sim, 0f, 3f, 1f);

            // Assert
            Assert.AreEqual(sim.Character.X, p.x, 1e-4f);
            Assert.AreEqual(5.5f, p.y, 1e-3f);
        }

        [Test]
        public void WhileFlying_NothingIsPrompted()
        {
            // Arrange
            var sim = Demo("jetpack");
            TakeOff(sim);

            // Act
            var e = DayInteraction.InteractTarget(sim);
            var f = DayInteraction.PlugTarget(sim);

            // Assert
            Assert.IsTrue(sim.Character.IsAirborne);
            Assert.AreEqual(DayTargetKind.None, e.Kind);
            Assert.AreEqual(DayTargetKind.None, f.Kind);
        }

        // ---- where the character is drawn -------------------------------------------------

        [Test]
        public void DayCharacterPoint_StandingIsTheSimXOnTheRoomFloor()
        {
            // Arrange: walked, then stood still for a tick.
            var sim = Started();
            WalkTicks(sim, -1, 5);
            sim.Step();

            // Act
            var p = HouseLayout.CharacterPoint(sim, 0.5f, 3f);

            // Assert
            Assert.AreEqual(sim.Character.X, p.x, 1e-4f);
            Assert.AreEqual(0f, p.y, 1e-4f);
        }

        [Test]
        public void DayCharacterPoint_Walking_GlidesBetweenWhereTheTickStartedAndEnded()
        {
            // Arrange: mid-walk to the left.
            var sim = Started();
            Submit(sim, Command.Walk(-1));
            sim.Step();
            sim.Step();
            float before = sim.Character.PrevXCm / 100f;
            float after = sim.Character.X;

            // Act
            float atStart = HouseLayout.CharacterPoint(sim, 0f, 3f).x;
            float half = HouseLayout.CharacterPoint(sim, 0.5f, 3f).x;
            float atEnd = HouseLayout.CharacterPoint(sim, 1f, 3f).x;

            // Assert
            Assert.Less(after, before);
            Assert.AreEqual(before, atStart, 1e-4f);
            Assert.AreEqual((before + after) * 0.5f, half, 1e-4f);
            Assert.AreEqual(after, atEnd, 1e-4f);
        }

        [Test]
        public void DayZ_WalkingIn_GlidesBetweenTicks()
        {
            // Arrange: one tick of W from the open front.
            var sim = Started();
            Submit(sim, Command.WalkDepth(1));
            sim.Step();

            // Act
            float half = HouseLayout.DayZ(sim.Character, 0.5f);

            // Assert
            Assert.AreEqual(0f, sim.Character.PrevZCm);
            Assert.Greater(sim.Character.ZCm, 0);
            Assert.AreEqual(sim.Character.ZCm / 200f, half, 1e-4f);
        }

        [Test]
        public void DayCharacterPoint_Flying_IsTheSimHeightAboveTheRoomFloor()
        {
            // Arrange: living room floor at y 3, a few ticks into the climb.
            var sim = Demo("jetpack");
            TakeOff(sim);
            sim.Step();
            sim.Step();

            // Act
            var p = HouseLayout.CharacterPoint(sim, 0f, 3f);

            // Assert
            Assert.AreEqual(sim.Character.X, p.x, 1e-4f);
            Assert.AreEqual(3f + sim.Character.AirCm / 100f, p.y, 1e-4f);
            Assert.Greater(sim.Character.AirCm, 0);
        }

        [Test]
        public void DayCharacterPoint_Flying_MovesOnBySpeedBetweenTicks()
        {
            // Arrange
            var sim = Demo("jetpack");
            TakeOff(sim);

            // Act
            var atTick = HouseLayout.CharacterPoint(sim, 0f, 3f);
            var half = HouseLayout.CharacterPoint(sim, 0.5f, 3f);

            // Assert
            Assert.AreEqual(atTick.y + sim.Character.VyCm * 0.5f / 100f, half.y, 1e-4f);
        }

        // ---- F: the plug prompt matches what F does --------------------------------------------

        [Test]
        public void PutDownUnplugged_PlugTargetIsThatApplianceToPlugIn()
        {
            // Arrange: pick the kettle up and put it straight back down (unplugged).
            var sim = Started();
            WalkTo(sim, 7.5f);
            Submit(sim, Command.Interact());
            Submit(sim, Command.Interact());

            // Act
            var target = DayInteraction.PlugTarget(sim);
            bool accepted = sim.Submit(Command.TogglePlug(), out _);

            // Assert
            Assert.AreEqual(DayTargetKind.Appliance, target.Kind);
            Assert.AreEqual("kettle", target.Id);
            Assert.IsTrue(target.PlugIn);
            Assert.IsTrue(accepted);
            Assert.IsFalse(sim.FindAppliance("kettle").Unplugged);
        }

        [Test]
        public void EverythingPluggedIn_PlugTargetWouldUnplug()
        {
            // Arrange: the day starts with every appliance plugged in.
            var sim = Started();
            WalkTo(sim, 7.5f);

            // Act
            var target = DayInteraction.PlugTarget(sim);

            // Assert
            Assert.AreEqual("kettle", target.Id);
            Assert.IsFalse(target.PlugIn);
        }

        [Test]
        public void FloorModeCarrying_InteractTargetsTheFloorUnderTheCharacter()
        {
            // Arrange
            var rules = Rules();
            rules.placeMode = "floor";
            var sim = new HouseSim(TwoRooms(), rules, TestData.Catalog(), TestData.Strings());
            sim.Start();
            WalkTo(sim, 7.5f);
            Submit(sim, Command.Interact());

            // Act
            var target = DayInteraction.InteractTarget(sim);

            // Assert
            Assert.AreEqual(DayTargetKind.Floor, target.Kind);
            Assert.AreEqual(sim.Character.X, target.X, 1e-4f);
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
