using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>Kinds of room alert (GDD v1.1 §2.5). Overload is deliberately not one: it would give away wiring.</summary>
    public enum AlertKind
    {
        /// <summary>A request has little time left before its deadline.</summary>
        DeadlineSoon,
        /// <summary>A routine is about to switch its appliance on.</summary>
        RoutineSoon,
        /// <summary>The fridge has no power; counts down to spoiling.</summary>
        FridgeOff,
        /// <summary>A fragile appliance lost its progress ("Kid: MY SAVE!"). Comes from an event, not from state.</summary>
        Interrupted,
    }

    /// <summary>One alert shown over a room.</summary>
    public struct Alert
    {
        public AlertKind Kind;
        public string RoomId;
        /// <summary>Family member it is about. Empty for the fridge.</summary>
        public string Owner;
        /// <summary>Ticks until the thing happens (deadline, routine start, spoiling).</summary>
        public int TicksLeft;
    }

    /// <summary>
    /// Pure rules that turn sim state into the state-based alerts of GDD v1.1 §2.5. Only reads what the player is
    /// meant to know (deadlines, the routine timetable, the fridge alarm); never circuit loads.
    /// </summary>
    public static class AlertRules
    {
        /// <summary>Fills <paramref name="into"/> with the alerts for this moment. Nothing during planning or after the end.</summary>
        public static void Collect(HouseSim sim, float deadlineWarnSeconds, float routineWarnSeconds, List<Alert> into)
        {
            into.Clear();
            if (sim.Phase != LevelPhase.Running && sim.Phase != LevelPhase.Paused) return;
            int tps = sim.Rules.ticksPerSecond;
            int deadlineWarn = Mathf.RoundToInt(deadlineWarnSeconds * tps);
            int routineWarn = Mathf.RoundToInt(routineWarnSeconds * tps);

            foreach (var r in sim.Requests)
            {
                int left = r.DueTicks - sim.Now;
                if (r.Done || left < 0 || left >= deadlineWarn) continue;
                into.Add(new Alert { Kind = AlertKind.DeadlineSoon, RoomId = r.RoomId, Owner = r.Owner, TicksLeft = left });
            }

            foreach (var r in sim.Routines)
            {
                int left = r.StartTick - sim.Now;
                if (left <= 0 || left > routineWarn) continue;
                var socket = sim.FindSocket(r.SocketId);
                if (socket == null) continue;
                into.Add(new Alert { Kind = AlertKind.RoutineSoon, RoomId = socket.RoomId, Owner = r.Owner, TicksLeft = left });
            }

            var fridge = sim.FindAppliance(HouseSim.FridgeId);
            if (fridge != null && !fridge.Powered && fridge.SocketId.Length > 0)
            {
                var socket = sim.FindSocket(fridge.SocketId);
                if (socket != null)
                    into.Add(new Alert
                    {
                        Kind = AlertKind.FridgeOff,
                        RoomId = socket.RoomId,
                        Owner = "",
                        TicksLeft = Mathf.Max(0, sim.SpoilLimitTicks - sim.SpoilTicks),
                    });
            }
        }

        /// <summary>
        /// Where to draw an alert whose room is outside <paramref name="area"/> (viewport units): the point on the
        /// area's edge in the direction of the room, and the arrow angle in degrees (0 = right, 90 = up).
        /// Returns false when the point is inside the area (draw it in place, no arrow).
        /// </summary>
        public static bool EdgePoint(Vector2 viewportPoint, Rect area, out Vector2 onEdge, out float angleDeg)
        {
            onEdge = viewportPoint;
            angleDeg = 0f;
            if (area.Contains(viewportPoint)) return false;
            var center = area.center;
            var d = viewportPoint - center;
            float tx = Mathf.Abs(d.x) > 1e-6f ? area.width * 0.5f / Mathf.Abs(d.x) : float.MaxValue;
            float ty = Mathf.Abs(d.y) > 1e-6f ? area.height * 0.5f / Mathf.Abs(d.y) : float.MaxValue;
            onEdge = center + d * Mathf.Min(tx, ty);
            angleDeg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>Position of a tick on a timeline 0..1 (clamped).</summary>
        public static float TimelineFraction(int tick, int dayTicks)
        {
            return dayTicks <= 0 ? 0f : Mathf.Clamp01((float)tick / dayTicks);
        }

        /// <summary>First letter of a name, upper case ("?" if empty).</summary>
        public static string Initial(string name)
        {
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }
    }
}
