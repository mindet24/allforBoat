using System;
using System.Collections.Generic;

namespace Trouble.Sim
{
    /// <summary>
    /// Day mode start-of-day rolls: room capacities that add up to the house capacity, and appliance loads.
    /// Each room and appliance can be fixed or random (set in the level).
    /// </summary>
    public static class HouseRoller
    {
        /// <summary>
        /// Capacity of every room, by room id. Fixed rooms keep their capacity. Random rooms start at capMin and
        /// share the rest of houseCapacity one unit at a time, never above capMax. With houseCapacity 0 there is
        /// no total and each random room rolls between capMin and capMax on its own.
        /// </summary>
        /// <exception cref="ArgumentException">The numbers cannot add up (the message says why).</exception>
        public static Dictionary<string, int> RollRoomCapacities(IReadOnlyList<RoomData> rooms, int houseCapacity, SimRandom rng)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            var random = new List<RoomData>();
            int used = 0;
            foreach (var r in rooms)
            {
                if (r.capRandom)
                {
                    if (r.capMax > 0 && r.capMax < r.capMin) throw new ArgumentException($"Room {r.id}: capMax {r.capMax} is below capMin {r.capMin}.");
                    result[r.id] = r.capMin;
                    used += r.capMin;
                    random.Add(r);
                }
                else
                {
                    result[r.id] = r.capacity;
                    used += r.capacity;
                }
            }

            if (houseCapacity <= 0)
            {
                foreach (var r in random) result[r.id] = rng.Range(r.capMin, r.capMax > 0 ? r.capMax : r.capMin);
                return result;
            }

            int left = houseCapacity - used;
            if (left < 0) throw new ArgumentException($"houseCapacity {houseCapacity} is below the fixed capacities plus every capMin ({used}).");
            if (left > 0 && random.Count == 0) throw new ArgumentException($"houseCapacity {houseCapacity} is not the sum of the room capacities ({used}) and no room is random.");

            var open = new List<RoomData>(random);
            while (left > 0)
            {
                open.RemoveAll(r => r.capMax > 0 && result[r.id] >= r.capMax);
                if (open.Count == 0) throw new ArgumentException($"houseCapacity {houseCapacity} is above the fixed capacities plus every capMax.");
                var pick = open[rng.Range(0, open.Count - 1)];
                result[pick.id]++;
                left--;
            }
            return result;
        }

        /// <summary>The load of one appliance: fixed, the catalog load, or rolled between loadMin and loadMax.</summary>
        public static int RollLoad(ApplianceData a, ApplianceDef def, SimRandom rng)
        {
            if (a.loadRandom)
            {
                if (a.loadMax < a.loadMin) throw new ArgumentException($"Appliance {a.id}: loadMax {a.loadMax} is below loadMin {a.loadMin}.");
                return rng.Range(a.loadMin, a.loadMax);
            }
            return a.load > 0 ? a.load : def.load;
        }
    }
}
