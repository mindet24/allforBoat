using System;
using System.Collections.Generic;

namespace Trouble.Sim
{
    /// <summary>What happened when a command list was played to the end.</summary>
    public sealed class ReplayResult
    {
        public LevelPhase Phase;
        public LoseReason Reason;
        /// <summary>The appliance whose deadline was missed, or "fridge". Empty when won.</summary>
        public string LostApplianceId = "";
        /// <summary>Value of <see cref="HouseSim.Now"/> when the level ended.</summary>
        public int EndTick;
        public int TripCount;
        /// <summary>Every event in order, copied.</summary>
        public readonly List<SimEvent> Events = new List<SimEvent>();
        /// <summary>Planning commands (tick -1) the sim refused, as "command: reason".</summary>
        public readonly List<string> SetupRejections = new List<string>();
        /// <summary>The sim in its final state.</summary>
        public HouseSim Sim;
    }

    /// <summary>Plays a scripted command list through a <see cref="HouseSim"/>. Used by tests and Run Sim.</summary>
    public static class Replay
    {
        /// <summary>
        /// Applies tick -1 commands during planning, starts the level, then before each tick submits the
        /// commands for that tick, until the level is won or lost.
        /// </summary>
        public static ReplayResult Run(HouseSim sim, IReadOnlyList<CommandData> commands)
        {
            var result = new ReplayResult { Sim = sim };
            var ordered = new List<CommandData>(commands);
            // Stable sort by tick: commands on the same tick keep their written order.
            var indexed = new List<(CommandData cmd, int index)>();
            for (int i = 0; i < ordered.Count; i++) indexed.Add((ordered[i], i));
            indexed.Sort((x, y) => x.cmd.tick != y.cmd.tick ? x.cmd.tick.CompareTo(y.cmd.tick) : x.index.CompareTo(y.index));

            int next = 0;
            while (next < indexed.Count && indexed[next].cmd.tick < 0)
            {
                var data = indexed[next++].cmd;
                if (!sim.Submit(ToCommand(data), out var reason)) result.SetupRejections.Add($"{data.cmd} {data.a} {data.b}: {reason}");
            }

            sim.Start();
            int safety = sim.DayLengthTicks + 2;
            while (sim.Phase == LevelPhase.Running && safety-- > 0)
            {
                while (next < indexed.Count && indexed[next].cmd.tick <= sim.Now)
                    sim.Submit(ToCommand(indexed[next++].cmd), out _);
                foreach (var e in sim.Step()) result.Events.Add(e);
            }

            result.Phase = sim.Phase;
            result.Reason = sim.LoseReason;
            result.EndTick = sim.Now;
            result.TripCount = sim.TripCount;
            foreach (var e in result.Events)
                if (e.Type == SimEventType.LevelLost) result.LostApplianceId = e.Appliance ?? "";
            return result;
        }

        /// <summary>
        /// Reads a short script: commands separated by ';', each "tick cmd a [b]".
        /// Example: "-1 plug kettle K1; 0 on kettle; 80 on toaster".
        /// </summary>
        /// <exception cref="FormatException">A command has the wrong shape.</exception>
        public static List<CommandData> Parse(string script)
        {
            var list = new List<CommandData>();
            foreach (var part in script.Split(';'))
            {
                var words = part.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0) continue;
                if (words.Length < 3 || words.Length > 4 || !int.TryParse(words[0], out int tick))
                    throw new FormatException($"Expected 'tick cmd a [b]' but got '{part.Trim()}'.");
                list.Add(new CommandData { tick = tick, cmd = words[1], a = words[2], b = words.Length == 4 ? words[3] : "" });
            }
            return list;
        }

        /// <summary>
        /// Turns one scripted command (see <see cref="CommandData"/>) into a sim command. Scripts are sequences,
        /// so by default each command joins the end of the character's queue instead of replacing it.
        /// </summary>
        /// <exception cref="ArgumentException">Unknown cmd word.</exception>
        public static Command ToCommand(CommandData d, bool append = true)
        {
            switch (d.cmd)
            {
                case "plug": return Command.Plug(d.a, d.b, append);
                case "unplug": return Command.Unplug(d.a, append);
                case "on": return Command.SetSwitch(d.a, true, append);
                case "off": return Command.SetSwitch(d.a, false, append);
                case "breaker": return Command.ToggleBreaker(d.a, append);
                case "cord": return Command.PlaceCord(d.a, d.b, append);
                case "uncord": return Command.RemoveCord(d.a, append);
                case "door": return Command.ToggleDoor(d.a, append);
                case "move": return Command.MoveTo(d.a, append);
                default: throw new ArgumentException($"Unknown command '{d.cmd}'.");
            }
        }

        /// <summary>
        /// The thoughtless play (GDD v1.0 §9.2): each request appliance, in level order, goes into the first free
        /// socket of its room by ordinal id that no routine holds and the fridge does not use; at tick 0 every
        /// plugged request appliance is switched on; nothing is ever reset.
        /// </summary>
        public static List<CommandData> Naive(LevelData level)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(level.fridgeSocket)) used.Add(level.fridgeSocket);
            foreach (var r in level.routines) used.Add(r.socket);

            var sockets = new List<SocketData>(level.sockets);
            sockets.Sort((x, y) => string.CompareOrdinal(x.id, y.id));

            var commands = new List<CommandData>();
            var plugged = new List<string>();
            foreach (var req in level.requests)
            {
                foreach (var s in sockets)
                {
                    if (s.room != req.room || used.Contains(s.id)) continue;
                    used.Add(s.id);
                    commands.Add(new CommandData { tick = -1, cmd = "plug", a = req.appliance, b = s.id });
                    plugged.Add(req.appliance);
                    break;
                }
            }
            foreach (var id in plugged)
                commands.Add(new CommandData { tick = 0, cmd = "on", a = id });
            return commands;
        }
    }
}
