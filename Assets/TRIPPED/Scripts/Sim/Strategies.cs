using System;
using System.Collections.Generic;

namespace Trouble.Sim
{
    /// <summary>
    /// Scripted players for Run Sim. They are rough stand-ins for real players, used to see whether a level
    /// can be beaten without thinking. They report what happened; they do not judge the level.
    /// </summary>
    public static class Strategies
    {
        /// <summary>
        /// Naive placement (<see cref="Replay.Naive"/>) with every request appliance switched on during
        /// planning instead of at tick 0, so no time goes on walking around to switch things on.
        /// </summary>
        public static List<CommandData> NaiveAllOnInPlanning(LevelData level)
        {
            var commands = Replay.Naive(level);
            foreach (var c in commands)
                if (c.cmd == "on") c.tick = -1;
            return commands;
        }

        /// <summary>
        /// A player who stands at the panel and flips an overloaded breaker off just before it would trip,
        /// then straight back on, and resets any breaker that tripped anyway.
        /// </summary>
        public static Action<HouseSim> PumpBreakers()
        {
            return sim =>
            {
                if (IsBusy(sim)) return;
                if (sim.Character.RoomId != sim.PanelRoomId)
                {
                    sim.Submit(Command.MoveTo(sim.PanelRoomId), out _);
                    return;
                }
                foreach (var c in sim.Circuits)
                {
                    if (!c.IsOn)
                    {
                        sim.Submit(Command.ToggleBreaker(c.Id), out _);
                        return;
                    }
                }
                foreach (var c in sim.Circuits)
                {
                    if (c.Overloading && c.OverTicks >= TriggerAt(sim))
                    {
                        sim.Submit(Command.ToggleBreaker(c.Id), out _);
                        return;
                    }
                }
            };
        }

        /// <summary>
        /// A player who switches one appliance on an overloaded circuit off just before the breaker would trip,
        /// then straight back on, and walks to the panel to reset any breaker that tripped anyway.
        /// </summary>
        public static Action<HouseSim> PumpSwitches()
        {
            string switchBackOn = null;
            return sim =>
            {
                if (IsBusy(sim)) return;
                if (switchBackOn != null)
                {
                    sim.Submit(Command.SetSwitch(switchBackOn, true), out _);
                    switchBackOn = null;
                    return;
                }
                foreach (var c in sim.Circuits)
                {
                    if (!c.IsOn)
                    {
                        sim.Submit(Command.ToggleBreaker(c.Id), out _);
                        return;
                    }
                }
                foreach (var c in sim.Circuits)
                {
                    if (!c.Overloading || c.OverTicks < TriggerAt(sim)) continue;
                    var victim = SmallestRunningRequestOn(sim, c.Id);
                    if (victim == null) continue;
                    sim.Submit(Command.SetSwitch(victim.Id, false), out _);
                    switchBackOn = victim.Id;
                    return;
                }
            };
        }

        /// <summary>Counter value at which a pumping player acts, leaving time for the action itself.</summary>
        private static int TriggerAt(HouseSim sim) => Math.Max(1, sim.Rules.overTripTicks - 1 - sim.Rules.interactTicks);

        private static bool IsBusy(HouseSim sim) => sim.Character.IsWalking || sim.Character.Queue.Count > 0;

        private static ApplianceState SmallestRunningRequestOn(HouseSim sim, string circuitId)
        {
            ApplianceState best = null;
            foreach (var app in sim.Appliances)
            {
                if (!app.SwitchedOn || app.Fixed || app.SocketId.Length == 0) continue;
                if (sim.FindSocket(app.SocketId).CircuitId != circuitId) continue;
                var req = sim.FindRequest(app.Id);
                if (req == null || req.Done) continue;
                if (best == null || app.Load < best.Load) best = app;
            }
            return best;
        }
    }
}
