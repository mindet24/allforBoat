using System;
using System.Collections.Generic;
using Trouble.Sim;

namespace Trouble.Game
{
    /// <summary>What E would act on right now in day mode.</summary>
    public enum DayTargetKind
    {
        None,
        /// <summary>The breaker panel, during a blackout.</summary>
        Panel,
        /// <summary>A free socket for the appliance being carried.</summary>
        Socket,
        /// <summary>The floor where the character stands, to put what is carried down (rules placeMode "floor").</summary>
        Floor,
        /// <summary>A plugged-in appliance to pick up.</summary>
        Appliance,
    }

    /// <summary>The thing E would act on, and where it is (metres) so the view can put the "E" prompt over it.</summary>
    public struct DayTarget
    {
        public DayTargetKind Kind;
        /// <summary>Socket id, appliance id, or the panel's room id.</summary>
        public string Id;
        /// <summary>Horizontal position the sim measures reach from, in metres.</summary>
        public float X;
        public string RoomId;
        /// <summary>F targets only: true = F would plug the appliance in, false = unplug it.</summary>
        public bool PlugIn;

        /// <summary>No target: E would be refused.</summary>
        public static DayTarget None => new DayTarget { Kind = DayTargetKind.None, Id = "", RoomId = "" };
    }

    /// <summary>
    /// Day-mode keyboard prompts, worked out from public sim state with the sim's own distance rules (whole
    /// centimetres, reach = rules.reachMetres across the floor) so a prompt shows exactly when the key would be accepted.
    /// The targets come from the sim's own lookups (nearest free socket, nearest appliance), in the sim's order:
    /// E = panel in a blackout, then where what is carried goes down, then the nearest appliance to pick up;
    /// F = the nearest unplugged appliance, then the nearest plugged one.
    /// </summary>
    public static class DayInteraction
    {
        /// <summary>Metres to whole centimetres, rounded like the sim.</summary>
        public static int Cm(float metres) => (int)Math.Round(metres * 100f, MidpointRounding.AwayFromZero);

        /// <summary>True when any room's breaker is off (the sim's blackout test for E at the panel).</summary>
        public static bool AnyBreakerOff(HouseSim sim)
        {
            // Indexed loops in this class: they run every frame (key prompts), and foreach over an IReadOnlyList boxes.
            var circuits = sim.Circuits;
            for (int i = 0; i < circuits.Count; i++)
                if (!circuits[i].IsOn) return true;
            return false;
        }

        /// <summary>
        /// What E would act on now, or <see cref="DayTarget.None"/>. Nothing in the air or outside day mode.
        /// </summary>
        public static DayTarget InteractTarget(HouseSim sim)
        {
            if (sim == null || !sim.IsDayMode) return DayTarget.None;
            var ch = sim.Character;
            if (!ch.CanUseHands) return DayTarget.None;
            if (AnyBreakerOff(sim) && sim.CanReachPanel)
                return new DayTarget { Kind = DayTargetKind.Panel, Id = sim.PanelRoomId, X = sim.PanelX, RoomId = sim.PanelRoomId };

            if (!string.IsNullOrEmpty(ch.CarryingId))
            {
                // Put down (or plug straight in when placeUnplugged is off): on the floor where the character stands,
                // or at the nearest free socket in reach.
                if (sim.Rules.placeUnplugged && sim.FloorMode)
                    return new DayTarget { Kind = DayTargetKind.Floor, Id = ch.RoomId, X = ch.X, RoomId = ch.RoomId };
                var free = sim.NearestFreeSocket(ch.RoomId, ch.XCm, ch.ZCm, sim.ReachCm, 0);
                return free == null
                    ? DayTarget.None
                    : new DayTarget { Kind = DayTargetKind.Socket, Id = free.Id, X = sim.SocketX(free), RoomId = free.RoomId };
            }

            var pick = sim.NearestAppliance(ch.RoomId, ch.XCm, ch.ZCm, sim.ReachCm);
            return pick == null
                ? DayTarget.None
                : new DayTarget { Kind = DayTargetKind.Appliance, Id = pick.Id, X = sim.ApplianceX(pick), RoomId = ch.RoomId };
        }

        /// <summary>
        /// What F would act on now: the appliance it would plug in or unplug (<see cref="DayTarget.PlugIn"/> says
        /// which), or <see cref="DayTarget.None"/>. Same order as the sim: the nearest unplugged appliance in reach,
        /// else the nearest plugged one; never a fixed appliance; an appliance on the floor needs a free socket.
        /// </summary>
        public static DayTarget PlugTarget(HouseSim sim)
        {
            if (sim == null || !sim.IsDayMode) return DayTarget.None;
            var ch = sim.Character;
            if (!ch.CanUseHands) return DayTarget.None;
            var app = sim.NearestAppliance(ch.RoomId, ch.XCm, ch.ZCm, sim.ReachCm, unpluggedOnly: true)
                      ?? sim.NearestAppliance(ch.RoomId, ch.XCm, ch.ZCm, sim.ReachCm);
            if (app == null || app.Fixed) return DayTarget.None;
            bool onFloor = !string.IsNullOrEmpty(app.FloorRoomId);
            if (onFloor && sim.NearestFreeSocket(app.FloorRoomId, app.FloorXCm, app.FloorZCm, sim.ReachCm, 0) == null) return DayTarget.None;
            return new DayTarget
            {
                Kind = DayTargetKind.Appliance, Id = app.Id, X = sim.ApplianceX(app), RoomId = ch.RoomId,
                PlugIn = onFloor || app.Unplugged,
            };
        }
    }
}
