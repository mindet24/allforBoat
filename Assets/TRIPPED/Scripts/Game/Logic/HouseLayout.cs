using System;
using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>One wall between two rooms (or at the end of the house) on one floor.</summary>
    public struct WallSpec
    {
        /// <summary>x of the wall's centre line, in metres.</summary>
        public float X;
        public int Floor;
        /// <summary>The door through this wall, or empty for a solid wall.</summary>
        public string DoorId;
        /// <summary>Only one room touches this wall (house end).</summary>
        public bool IsOuter;
    }

    /// <summary>
    /// Pure geometry that turns sim rooms (x, width, floor in metres) into world positions. The world uses
    /// metres: x to the right, y up (floor n starts at n × room height), z away from the camera (0 = the open
    /// front of the house, roomDepth = back wall). The house must match the sim's distances (ADR-0001).
    /// </summary>
    public static class HouseLayout
    {
        private const float Epsilon = 0.01f;

        /// <summary>Height of the floor surface of a storey.</summary>
        public static float FloorY(int floor, float roomHeight) => floor * roomHeight;

        /// <summary>A room's front face as a world rect (x right, y up).</summary>
        public static Rect RoomRect(float x, float width, int floor, float roomHeight)
        {
            return new Rect(x, FloorY(floor, roomHeight), width, roomHeight);
        }

        /// <summary>A room's front face as a world rect.</summary>
        public static Rect RoomRect(RoomState room, float roomHeight) => RoomRect(room.X, room.Width, room.Floor, roomHeight);

        /// <summary>The smallest rect holding every room. Zero rect if there are no rooms.</summary>
        public static Rect HouseBounds(IReadOnlyList<RoomState> rooms, float roomHeight)
        {
            if (rooms == null || rooms.Count == 0) return new Rect(0, 0, 0, 0);
            var r = RoomRect(rooms[0], roomHeight);
            float xMin = r.xMin, xMax = r.xMax, yMin = r.yMin, yMax = r.yMax;
            for (int i = 1; i < rooms.Count; i++)
            {
                r = RoomRect(rooms[i], roomHeight);
                xMin = Mathf.Min(xMin, r.xMin);
                xMax = Mathf.Max(xMax, r.xMax);
                yMin = Mathf.Min(yMin, r.yMin);
                yMax = Mathf.Max(yMax, r.yMax);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>World x of a socket: its room's left edge plus pos (0..1) of the room width.</summary>
        public static float SocketX(float roomX, float roomWidth, float pos) => roomX + pos * roomWidth;

        /// <summary>
        /// Every wall of the house, one per room edge (shared edges once), with the door that passes through it.
        /// Only doors (not stairs) between side-by-side rooms on the same floor make an opening.
        /// </summary>
        public static List<WallSpec> Walls(IReadOnlyList<RoomState> rooms, IReadOnlyList<DoorState> doors)
        {
            var walls = new List<WallSpec>();
            foreach (var room in rooms)
            {
                AddEdge(walls, room.X, room.Floor);
                AddEdge(walls, room.X + room.Width, room.Floor);
            }

            for (int w = 0; w < walls.Count; w++)
            {
                var wall = walls[w];
                int touching = 0;
                foreach (var room in rooms)
                {
                    if (room.Floor != wall.Floor) continue;
                    if (Near(room.X, wall.X) || Near(room.X + room.Width, wall.X)) touching++;
                }
                wall.IsOuter = touching < 2;
                wall.DoorId = "";
                foreach (var door in doors)
                {
                    if (door.IsStairs) continue;
                    var a = Find(rooms, door.RoomA);
                    var b = Find(rooms, door.RoomB);
                    if (a == null || b == null || a.Floor != wall.Floor || b.Floor != wall.Floor) continue;
                    bool aLeftOfB = Near(a.X + a.Width, wall.X) && Near(b.X, wall.X);
                    bool bLeftOfA = Near(b.X + b.Width, wall.X) && Near(a.X, wall.X);
                    if (aLeftOfB || bLeftOfA)
                    {
                        wall.DoorId = door.Id;
                        break;
                    }
                }
                walls[w] = wall;
            }
            walls.Sort((p, q) => p.Floor != q.Floor ? p.Floor.CompareTo(q.Floor) : p.X.CompareTo(q.X));
            return walls;
        }

        /// <summary>
        /// Where the character stands on the house front (x, y of its feet). While walking it moves between the
        /// two room centres by the sim's segment progress; <paramref name="tickFraction"/> (0..1, how far the
        /// clock is into the next tick) smooths the motion between ticks without changing the sim.
        /// </summary>
        public static Vector2 CharacterPoint(HouseSim sim, float tickFraction, float roomHeight)
        {
            var ch = sim.Character;
            if (ch.IsWalking)
            {
                var from = sim.FindRoom(ch.FromRoomId);
                var to = sim.FindRoom(ch.ToRoomId);
                if (from != null && to != null)
                {
                    float p = WalkProgress(ch.SegmentElapsed, ch.SegmentTicks, tickFraction);
                    return new Vector2(
                        Mathf.Lerp(from.CenterX, to.CenterX, p),
                        Mathf.Lerp(FloorY(from.Floor, roomHeight), FloorY(to.Floor, roomHeight), p));
                }
            }
            var room = sim.FindRoom(ch.RoomId);
            return room == null ? Vector2.zero : new Vector2(room.CenterX, FloorY(room.Floor, roomHeight));
        }

        /// <summary>Segment progress 0..1 with the fraction of the next tick added.</summary>
        public static float WalkProgress(int elapsedTicks, int segmentTicks, float tickFraction)
        {
            if (segmentTicks <= 0) return 1f;
            return Mathf.Clamp01((elapsedTicks + Mathf.Clamp01(tickFraction)) / segmentTicks);
        }

        /// <summary>
        /// The parts of [<paramref name="xMin"/>, <paramref name="xMax"/>] not covered by any of <paramref name="covers"/>
        /// (each cover is x = start, y = end). Used to draw a ceiling only where no room above provides the floor.
        /// </summary>
        public static List<Vector2> UncoveredSpans(float xMin, float xMax, IReadOnlyList<Vector2> covers)
        {
            var spans = new List<Vector2> { new Vector2(xMin, xMax) };
            foreach (var c in covers)
            {
                var next = new List<Vector2>();
                foreach (var s in spans)
                {
                    if (c.y <= s.x + Epsilon || c.x >= s.y - Epsilon) { next.Add(s); continue; }
                    if (c.x > s.x + Epsilon) next.Add(new Vector2(s.x, c.x));
                    if (c.y < s.y - Epsilon) next.Add(new Vector2(c.y, s.y));
                }
                spans = next;
            }
            return spans;
        }

        /// <summary>
        /// The overlap in x of two rooms on different floors, or width 0 if they do not overlap. Stairs between
        /// them are drawn inside this span.
        /// </summary>
        public static Vector2 OverlapSpan(RoomState a, RoomState b)
        {
            float lo = Mathf.Max(a.X, b.X);
            float hi = Mathf.Min(a.X + a.Width, b.X + b.Width);
            return hi > lo ? new Vector2(lo, hi) : new Vector2(lo, lo);
        }

        private static void AddEdge(List<WallSpec> walls, float x, int floor)
        {
            foreach (var w in walls)
                if (w.Floor == floor && Near(w.X, x)) return;
            walls.Add(new WallSpec { X = x, Floor = floor, DoorId = "" });
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < Epsilon;

        private static RoomState Find(IReadOnlyList<RoomState> rooms, string id)
        {
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Id == id) return rooms[i];
            return null;
        }
    }
}
