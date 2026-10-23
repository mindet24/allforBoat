using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Pure geometry for the wires in the walls (decision Q28) and the sagging extension cords. Points are on the
    /// house front plane (x right, y up, metres). Views add z.
    /// </summary>
    public static class WireRouting
    {
        /// <summary>
        /// The route of a wall wire: up from the socket to just under the ceiling, across to the panel, down to
        /// the panel's top. <paramref name="lane"/> moves the horizontal run down a little so wires from different
        /// sockets do not lie on top of each other.
        /// </summary>
        public static void Route(Vector2 socketTop, Vector2 panelTop, float ceilingY, float lane, List<Vector2> into)
        {
            into.Clear();
            float runY = ceilingY - lane;
            into.Add(socketTop);
            into.Add(new Vector2(socketTop.x, runY));
            into.Add(new Vector2(panelTop.x, runY));
            into.Add(panelTop);
        }

        /// <summary>Total length of a polyline.</summary>
        public static float Length(IReadOnlyList<Vector2> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++) length += Vector2.Distance(points[i - 1], points[i]);
            return length;
        }

        /// <summary>
        /// The first <paramref name="fraction"/> (0..1) of a polyline, by length, written into
        /// <paramref name="into"/>. Used for the reveal sweep from socket to panel.
        /// </summary>
        public static void Truncate(IReadOnlyList<Vector2> points, float fraction, List<Vector2> into)
        {
            into.Clear();
            if (points.Count == 0) return;
            into.Add(points[0]);
            float left = Mathf.Clamp01(fraction) * Length(points);
            for (int i = 1; i < points.Count; i++)
            {
                float seg = Vector2.Distance(points[i - 1], points[i]);
                if (left >= seg)
                {
                    into.Add(points[i]);
                    left -= seg;
                    continue;
                }
                if (left > 0f) into.Add(Vector2.Lerp(points[i - 1], points[i], left / seg));
                return;
            }
        }

        /// <summary>
        /// A cable hanging between anchor points: each span sags by <paramref name="sag"/> metres at its middle
        /// (a parabola), sampled with <paramref name="segmentsPerSpan"/> segments.
        /// </summary>
        public static void SagPath(IReadOnlyList<Vector2> anchors, float sag, int segmentsPerSpan, List<Vector2> into)
        {
            into.Clear();
            if (anchors.Count == 0) return;
            into.Add(anchors[0]);
            int n = Mathf.Max(1, segmentsPerSpan);
            for (int a = 1; a < anchors.Count; a++)
            {
                var p0 = anchors[a - 1];
                var p1 = anchors[a];
                for (int i = 1; i <= n; i++)
                {
                    float t = (float)i / n;
                    var p = Vector2.Lerp(p0, p1, t);
                    p.y -= sag * 4f * t * (1f - t);
                    into.Add(p);
                }
            }
        }
    }
}
