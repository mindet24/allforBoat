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

        /// <summary>No target: E would be refused.</summary>
        public static DayTarget None => new DayTarget { Kind = DayTargetKind.None, Id = "", RoomId = "" };
    }

    /// <summary>Stairs the character could take now with W (up, +1) or S (down, -1).</summary>
    public struct StairsPrompt
    {
        public string DoorId;
        /// <summary>+1 = up (W), -1 = down (S).</summary>
        public int Direction;
        /// <summary>Where the stairs are used (sim.StairsX), in metres.</summary>
        public float X;
        public string RoomId;
    }

    /// <summary>
    /// Day-mode keyboard prompts, worked out from public sim state with the sim's own distance rules (whole
    /// centimetres, reach = rules.reachMetres) so a prompt shows exactly when the key would be accepted.
    /// Rule order of E matches HouseSim: panel in a blackout, then the nearest free socket when carrying,
    /// then the nearest plugged appliance.
    /// </summary>
    public static class DayInteraction
    {
        /// <summary>Metres to whole centimetres, rounded like the sim.</summary>
        public static int Cm(float metres) => (int)Math.Round(metres * 100f, MidpointRounding.AwayFromZero);

        /// <summary>True when any room's breaker is off (the sim's blackout test for E at the panel).</summary>
        public static bool AnyBreakerOff(HouseSim sim)
        {
            foreach (var c in sim.Circuits)
                if (!c.IsOn) return true;
            return false;
        }

        /// <summary>
        /// What E would act on now, or <see cref="DayTarget.None"/>. Nothing while climbing or outside day mode.
        /// </summary>
        public static DayTarget InteractTarget(HouseSim sim)
        {
            if (sim == null || !sim.IsDayMode) return DayTarget.None;
            var ch = sim.Character;
            if (ch.IsClimbing) return DayTarget.None;
            int reach = Cm(sim.Rules.reachMetres);

            if (AnyBreakerOff(sim) && ch.RoomId == sim.PanelRoomId && Math.Abs(ch.XCm - Cm(sim.PanelX)) <= reach)
                return new DayTarget { Kind = DayTargetKind.Panel, Id = sim.PanelRoomId, X = sim.PanelX, RoomId = sim.PanelRoomId };

            if (!string.IsNullOrEmpty(ch.CarryingId))
            {
                SocketState best = null;
                int bestDist = int.MaxValue;
                foreach (var s in sim.Sockets)
                {
                    if (s.RoomId != ch.RoomId || !string.IsNullOrEmpty(s.OccupantId) || s.Reserved || !string.IsNullOrEmpty(s.CordSocketId)) continue;
                    int d = Math.Abs(ch.XCm - Cm(sim.SocketX(s)));
                    if (d <= reach && d < bestDist)
                    {
                        best = s;
                        bestDist = d;
                    }
                }
                return best == null
                    ? DayTarget.None
                    : new DayTarget { Kind = DayTargetKind.Socket, Id = best.Id, X = sim.SocketX(best), RoomId = best.RoomId };
            }

            ApplianceState pick = null;
            SocketState pickSocket = null;
            int pickDist = int.MaxValue;
            foreach (var app in sim.Appliances)
            {
                if (string.IsNullOrEmpty(app.SocketId)) continue;
                var s = sim.FindSocket(app.SocketId);
                if (s == null || s.RoomId != ch.RoomId) continue;
                int d = Math.Abs(ch.XCm - Cm(sim.SocketX(s)));
                if (d <= reach && d < pickDist)
                {
                    pick = app;
                    pickSocket = s;
                    pickDist = d;
                }
            }
            return pick == null
                ? DayTarget.None
                : new DayTarget { Kind = DayTargetKind.Appliance, Id = pick.Id, X = sim.SocketX(pickSocket), RoomId = pickSocket.RoomId };
        }

        /// <summary>
        /// Fills <paramref name="into"/> with the stairs W or S would take now (open stairs from this room to the
        /// floor just above or below, within reach). Empty while climbing.
        /// </summary>
        public static void StairsInReach(HouseSim sim, List<StairsPrompt> into)
        {
            into.Clear();
            if (sim == null || !sim.IsDayMode) return;
            var ch = sim.Character;
            if (ch.IsClimbing) return;
            var here = sim.FindRoom(ch.RoomId);
            if (here == null) return;
            int reach = Cm(sim.Rules.reachMetres);
            foreach (var door in sim.Doors)
            {
                if (!door.IsStairs || !door.IsOpen || (door.RoomA != here.Id && door.RoomB != here.Id)) continue;
                var other = sim.FindRoom(door.OtherRoom(here.Id));
                if (other == null) continue;
                int dir = other.Floor - here.Floor;
                if (dir != 1 && dir != -1) continue;
                float x = sim.StairsX(door);
                if (Math.Abs(ch.XCm - Cm(x)) > reach) continue;
                // The sim takes the first matching door per direction; show only that one.
                bool taken = false;
                foreach (var p in into)
                    if (p.Direction == dir) taken = true;
                if (taken) continue;
                into.Add(new StairsPrompt { DoorId = door.Id, Direction = dir, X = x, RoomId = here.Id });
            }
        }

        /// <summary>The stairs door joining two rooms, or null.</summary>
        public static DoorState StairsBetween(HouseSim sim, string roomA, string roomB)
        {
            foreach (var door in sim.Doors)
            {
                if (!door.IsStairs) continue;
                if ((door.RoomA == roomA && door.RoomB == roomB) || (door.RoomA == roomB && door.RoomB == roomA)) return door;
            }
            return null;
        }
    }
}
