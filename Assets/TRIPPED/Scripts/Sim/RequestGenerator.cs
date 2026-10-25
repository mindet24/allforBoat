using System;
using System.Collections.Generic;

namespace Trouble.Sim
{
    /// <summary>
    /// Day mode: makes requests while the day runs, from the level's people and urgencies. Deterministic for a
    /// given seed. It never makes a request the player cannot do:
    /// the appliance fits the room's capacity, it is not already wanted, it is not already in that room,
    /// and every urgent request in a room fits that room together. With day.conflictChance it sometimes picks, on
    /// purpose, an appliance that clashes with the room but can still be done (<see cref="ConflictRules"/>).
    /// </summary>
    internal sealed class RequestGenerator
    {
        private readonly DayData _day;
        private readonly SimRandom _rng;
        private readonly RulesConfig _rules;
        private readonly int _dayTicks;
        private int _nextTick;
        private int _serial;

        public RequestGenerator(DayData day, SimRandom rng, RulesConfig rules, int dayTicks)
        {
            _day = day;
            _rng = rng;
            _rules = rules;
            _dayTicks = dayTicks;
            _nextTick = rules.SecondsToTicks(day.firstRequestSec);
        }

        /// <summary>Tick at which the next request is due to appear.</summary>
        public int NextTick => _nextTick;

        /// <summary>Adds the requests due at this tick to <paramref name="into"/>.</summary>
        public void Tick(HouseSim sim, int now, List<RequestState> into)
        {
            if (now < _nextTick) return;
            Spawn(sim, now, into);
            _nextTick = now + Math.Max(1, Gap(now));
        }

        private int Gap(int now)
        {
            double t = _dayTicks <= 0 ? 0 : Math.Min(1.0, (double)now / _dayTicks);
            double sec = _day.intervalStartSec + (_day.intervalEndSec - _day.intervalStartSec) * t;
            double jitter = 1.0 + _day.intervalJitter * (_rng.NextDouble() * 2.0 - 1.0);
            return _rules.SecondsToTicks((float)(sec * jitter));
        }

        private void Spawn(HouseSim sim, int now, List<RequestState> into)
        {
            if (_day.people.Count == 0 || _day.urgencies.Count == 0) return;
            if (ActiveCount(into) >= _day.maxActive) return;

            var person = _day.people[_rng.Range(0, _day.people.Count - 1)];
            if (person.rooms.Count == 0) return;
            string room = person.rooms[_rng.Range(0, person.rooms.Count - 1)];

            int count = _rng.NextDouble() < _day.bundleChance ? 2 : 1;
            string usedUrgency = null;
            for (int i = 0; i < count && ActiveCount(into) < _day.maxActive; i++)
            {
                var urgency = PickUrgency(usedUrgency);
                if (urgency == null) return;
                var app = PickAppliance(sim, person, room, urgency, into, out bool conflict);
                if (app == null) continue;
                usedUrgency = urgency.id;
                into.Add(new RequestState
                {
                    Serial = ++_serial,
                    Owner = person.name,
                    ApplianceId = app.Id,
                    RoomId = room,
                    DurationTicks = UseTicks(sim, app.Id),
                    SpawnTick = now,
                    DueTicks = now + _rules.SecondsToTicks(urgency.dueSec),
                    Urgency = urgency.id,
                    Points = urgency.points,
                    CostsHeart = urgency.costsHeart,
                    Conflict = conflict,
                });
            }
        }

        /// <summary>Weighted pick, skipping <paramref name="except"/> so a bundle mixes urgencies.</summary>
        private UrgencyData PickUrgency(string except)
        {
            double total = 0;
            foreach (var u in _day.urgencies)
                if (u.id != except) total += Math.Max(0f, u.weight);
            if (total <= 0) return null;
            double roll = _rng.NextDouble() * total;
            foreach (var u in _day.urgencies)
            {
                if (u.id == except) continue;
                roll -= Math.Max(0f, u.weight);
                if (roll < 0) return u;
            }
            return null;
        }

        private ApplianceState PickAppliance(HouseSim sim, PersonData person, string room, UrgencyData urgency, List<RequestState> requests,
            out bool conflict)
        {
            conflict = false;
            int capacity = sim.FindCircuit(room)?.Capacity ?? 0;
            int urgentLoad = 0;
            if (urgency.costsHeart)
            {
                foreach (var r in requests)
                    if (r.IsActive && r.CostsHeart && r.RoomId == room) urgentLoad += sim.FindAppliance(r.ApplianceId).Load;
            }

            var candidates = new List<ApplianceState>();
            foreach (var app in sim.Appliances)
            {
                if (person.wants.Count > 0 && !person.wants.Contains(app.Id)) continue;
                if (app.Load > capacity) continue;
                if (urgency.costsHeart && urgentLoad + app.Load > capacity) continue;
                if (IsWanted(app.Id, requests)) continue;
                if (app.SocketId.Length > 0 && sim.FindSocket(app.SocketId).RoomId == room) continue;
                candidates.Add(app);
            }
            if (candidates.Count == 0) return null;

            // No roll at all with conflictChance 0, so days without it keep their exact request sequence.
            if (_day.conflictChance > 0f && _rng.NextDouble() < _day.conflictChance)
            {
                var clashes = Clashes(sim, room, urgency, requests, candidates, capacity);
                if (clashes.Count > 0)
                {
                    conflict = true;
                    return clashes[_rng.Range(0, clashes.Count - 1)];
                }
            }
            return candidates[_rng.Range(0, candidates.Count - 1)];
        }

        /// <summary>The candidates that clash with <paramref name="room"/> and can still be done in time.</summary>
        private List<ApplianceState> Clashes(HouseSim sim, string room, UrgencyData urgency, List<RequestState> requests,
            List<ApplianceState> candidates, int capacity)
        {
            int roomLoad = sim.FindCircuit(room)?.Load ?? 0;
            int pendingLoad = 0, blockingTicks = 0;
            foreach (var r in requests)
            {
                if (!r.IsActive || r.RoomId != room) continue;
                if (r.ProgressTicks == 0) pendingLoad += sim.FindAppliance(r.ApplianceId).Load;
                blockingTicks += Math.Max(0, r.DurationTicks - r.ProgressTicks);
            }
            int travel = _rules.SecondsToTicks(_day.conflictTravelSec);
            int due = _rules.SecondsToTicks(urgency.dueSec);
            var clashes = new List<ApplianceState>();
            foreach (var app in candidates)
                if (ConflictRules.IsSolvableConflict(capacity, roomLoad, pendingLoad, app.Load, blockingTicks, travel, due)) clashes.Add(app);
            return clashes;
        }

        private int UseTicks(HouseSim sim, string applianceId)
        {
            if (_day.uniformUseSec > 0) return _rules.SecondsToTicks(_day.uniformUseSec);
            foreach (var a in sim.Level.appliances)
                if (a.id == applianceId) return _rules.SecondsToTicks(a.useSec);
            return _rules.SecondsToTicks(10f);
        }

        private static bool IsWanted(string applianceId, List<RequestState> requests)
        {
            foreach (var r in requests)
                if (r.IsActive && r.ApplianceId == applianceId) return true;
            return false;
        }

        private static int ActiveCount(List<RequestState> requests)
        {
            int n = 0;
            foreach (var r in requests)
                if (r.IsActive) n++;
            return n;
        }
    }
}
