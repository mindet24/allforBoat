using System;
using System.Collections.Generic;

namespace Trouble.Sim
{
    /// <summary>Checks level data before it is played (GDD v1.0 §8.6, plus the v1.1 fields). Empty list = valid.</summary>
    public static class LevelValidator
    {
        public static List<string> Validate(LevelData level, Catalog catalog, RulesConfig rules)
        {
            var errors = new List<string>();
            var rooms = Ids(level.rooms, r => r.id, "room", errors);
            var circuits = Ids(level.circuits, c => c.id, "circuit", errors);
            var sockets = Ids(level.sockets, s => s.id, "socket", errors);
            var doors = Ids(level.doors, d => d.id, "door", errors);

            // 1–2. Rooms, doors and sockets refer to things that exist.
            if (level.rooms.Count == 0) errors.Add("The level has no rooms.");
            foreach (var d in level.doors)
            {
                if (!rooms.Contains(d.a) || !rooms.Contains(d.b)) errors.Add($"Door {d.id} joins unknown rooms '{d.a}' and '{d.b}'.");
                if (d.kind != "door" && d.kind != "stairs") errors.Add($"Door {d.id} has kind '{d.kind}'; use 'door' or 'stairs'.");
            }
            foreach (var s in level.sockets)
            {
                if (!rooms.Contains(s.room)) errors.Add($"Socket {s.id} is in unknown room '{s.room}'.");
                if (!circuits.Contains(s.circuit)) errors.Add($"Socket {s.id} is on unknown circuit '{s.circuit}'.");
                if (!string.IsNullOrEmpty(s.assumed) && !circuits.Contains(s.assumed)) errors.Add($"Socket {s.id} assumes unknown circuit '{s.assumed}'.");
                if (s.pos > 1f) errors.Add($"Socket {s.id} has pos {s.pos}; use 0..1.");
            }
            if (!string.IsNullOrEmpty(level.startRoom) && !rooms.Contains(level.startRoom)) errors.Add($"startRoom '{level.startRoom}' does not exist.");
            if (!string.IsNullOrEmpty(level.panelRoom) && !rooms.Contains(level.panelRoom)) errors.Add($"panelRoom '{level.panelRoom}' does not exist.");

            // 3. Appliances are in the catalog and appear once.
            var appliances = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in level.requests) AddAppliance(r.appliance, $"{r.owner}'s request", catalog, appliances, errors);
            foreach (var r in level.routines) AddAppliance(r.appliance, $"{r.owner}'s routine", catalog, appliances, errors);
            if (!string.IsNullOrEmpty(level.fridgeSocket)) AddAppliance(HouseSim.FridgeId, "The fridge", catalog, appliances, errors);
            for (int i = 1; i <= level.lampCount; i++) AddAppliance("lamp" + i, "Lamp " + i, catalog, appliances, errors, "lamp");

            // 6. Routine and fridge sockets exist and are all different.
            var held = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in level.routines) HoldSocket(r.socket, $"{r.owner}'s routine", sockets, held, errors);
            if (!string.IsNullOrEmpty(level.fridgeSocket)) HoldSocket(level.fridgeSocket, "The fridge", sockets, held, errors);

            // 4. Every request can be plugged in somewhere.
            foreach (var r in level.requests)
            {
                if (!rooms.Contains(r.room))
                {
                    errors.Add($"{r.owner}'s request is in unknown room '{r.room}'.");
                    continue;
                }
                bool hasSocket = false;
                foreach (var s in level.sockets)
                    if (s.room == r.room && !held.Contains(s.id)) hasSocket = true;
                if (!hasSocket && !(level.cordCount > 0 && NeighbourHasFreeSocket(level, r.room, held, rules.cordThroughStairs)))
                    errors.Add($"{r.owner}'s {r.appliance} has no free socket in the {r.room} and no cord can reach it.");
            }

            // 5. Times make sense.
            foreach (var r in level.requests)
            {
                if (r.durationSec <= 0f) errors.Add($"{r.owner}'s {r.appliance} needs durationSec > 0.");
                if (r.dueSec < r.durationSec) errors.Add($"{r.owner}'s {r.appliance} is due before it could finish.");
                if (r.dueSec > level.dayLengthSec) errors.Add($"{r.owner}'s {r.appliance} is due after the day ends.");
            }
            foreach (var r in level.routines)
                if (r.startSec < 0f || r.durationSec < 0f) errors.Add($"{r.owner}'s routine has a negative time.");

            // 7–8. Numbers.
            if (!string.IsNullOrEmpty(level.fridgeSocket) && level.fridgeSpoilSec <= 0f) errors.Add("A level with a fridge needs fridgeSpoilSec > 0.");
            foreach (var c in level.circuits)
                if (c.capacity <= 0) errors.Add($"Circuit {c.id} needs capacity > 0.");
            if (level.mainCapacity < 0) errors.Add("mainCapacity cannot be negative.");
            if (level.dayLengthSec <= 0f) errors.Add("dayLengthSec must be > 0.");

            // Rule overrides name real rules.
            rules.WithOverrides(level.rulesOverride, errors);

            // 9. Scripted commands refer to things that exist, in tick order.
            var ctx = new Names(rooms, circuits, sockets, doors, appliances);
            CheckCommands(level.reference, "reference", ctx, errors);
            foreach (var s in level.scenarios)
            {
                CheckCommands(s.commands, $"scenario '{s.name}'", ctx, errors);
                if (s.expect != "won" && s.expect != "lost" && s.expect != "notFasterThanReference")
                    errors.Add($"Scenario '{s.name}' expects '{s.expect}'; use won, lost or notFasterThanReference.");
            }

            return errors;
        }

        private readonly struct Names
        {
            public readonly HashSet<string> Rooms, Circuits, Sockets, Doors, Appliances;

            public Names(HashSet<string> rooms, HashSet<string> circuits, HashSet<string> sockets, HashSet<string> doors, HashSet<string> appliances)
            {
                Rooms = rooms;
                Circuits = circuits;
                Sockets = sockets;
                Doors = doors;
                Appliances = appliances;
            }

            /// <summary>A real socket, or the cord socket that a real socket can grow.</summary>
            public bool IsSocket(string id) =>
                Sockets.Contains(id) || (id.StartsWith(HouseSim.CordSocketPrefix, StringComparison.Ordinal) && Sockets.Contains(id.Substring(HouseSim.CordSocketPrefix.Length)));
        }

        private static void CheckCommands(List<CommandData> commands, string where, Names n, List<string> errors)
        {
            int lastTick = int.MinValue;
            foreach (var c in commands)
            {
                if (c.tick < lastTick) errors.Add($"{where}: tick {c.tick} comes after tick {lastTick}; keep commands in tick order.");
                lastTick = Math.Max(lastTick, c.tick);
                if (c.tick < -1) errors.Add($"{where}: tick {c.tick} is before planning (-1).");

                string what = $"{where} at tick {c.tick}: {c.cmd}";
                switch (c.cmd)
                {
                    case "plug":
                        if (!n.Appliances.Contains(c.a)) errors.Add($"{what} unknown appliance '{c.a}'.");
                        if (!n.IsSocket(c.b)) errors.Add($"{what} unknown socket '{c.b}'.");
                        break;
                    case "unplug":
                    case "on":
                    case "off":
                        if (!n.Appliances.Contains(c.a)) errors.Add($"{what} unknown appliance '{c.a}'.");
                        break;
                    case "breaker":
                        if (!n.Circuits.Contains(c.a)) errors.Add($"{what} unknown circuit '{c.a}'.");
                        break;
                    case "cord":
                        if (!n.Sockets.Contains(c.a)) errors.Add($"{what} unknown socket '{c.a}'.");
                        if (!n.Rooms.Contains(c.b)) errors.Add($"{what} unknown room '{c.b}'.");
                        break;
                    case "uncord":
                        if (!n.IsSocket(c.a) || n.Sockets.Contains(c.a)) errors.Add($"{what} '{c.a}' is not a cord socket.");
                        break;
                    case "door":
                        if (!n.Doors.Contains(c.a)) errors.Add($"{what} unknown door '{c.a}'.");
                        break;
                    case "move":
                        if (!n.Rooms.Contains(c.a)) errors.Add($"{what} unknown room '{c.a}'.");
                        break;
                    default:
                        errors.Add($"{what} is not a command.");
                        break;
                }
            }
        }

        private static bool NeighbourHasFreeSocket(LevelData level, string room, HashSet<string> held, bool viaStairs)
        {
            var neighbours = new HashSet<string>(StringComparer.Ordinal);
            if (level.doors.Count == 0)
            {
                for (int i = 0; i < level.rooms.Count; i++)
                {
                    if (level.rooms[i].id != room) continue;
                    if (i > 0) neighbours.Add(level.rooms[i - 1].id);
                    if (i + 1 < level.rooms.Count) neighbours.Add(level.rooms[i + 1].id);
                }
            }
            else
            {
                foreach (var d in level.doors)
                {
                    if (d.kind == "stairs" && !viaStairs) continue;
                    if (d.a == room) neighbours.Add(d.b);
                    if (d.b == room) neighbours.Add(d.a);
                }
            }
            foreach (var s in level.sockets)
                if (neighbours.Contains(s.room) && !held.Contains(s.id)) return true;
            return false;
        }

        private static void AddAppliance(string id, string who, Catalog catalog, HashSet<string> seen, List<string> errors, string type = null)
        {
            if (catalog.Find(type ?? id) == null) errors.Add($"{who} uses '{type ?? id}', which is not in the catalog.");
            if (!seen.Add(id)) errors.Add($"Appliance '{id}' appears more than once in the level.");
        }

        private static void HoldSocket(string socket, string who, HashSet<string> sockets, HashSet<string> held, List<string> errors)
        {
            if (!sockets.Contains(socket)) errors.Add($"{who} uses unknown socket '{socket}'.");
            else if (!held.Add(socket)) errors.Add($"Socket {socket} is used by more than one fixed appliance.");
        }

        private static HashSet<string> Ids<T>(List<T> items, Func<T, string> id, string kind, List<string> errors)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                var value = id(item);
                if (string.IsNullOrEmpty(value)) errors.Add($"A {kind} has no id.");
                else if (!set.Add(value)) errors.Add($"The {kind} id '{value}' is used twice.");
            }
            return set;
        }
    }
}
