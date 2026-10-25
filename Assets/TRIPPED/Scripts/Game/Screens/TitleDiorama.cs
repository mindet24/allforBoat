using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The Title's cute low-poly diorama around the demo house, seen from an isometric angle: a block of land cut away
    /// at the front (grass on top, layered soil with pebbles below, the basement dug into it), faceted trees and pines,
    /// bushes with flowers, a picket fence, a mailbox, a power pole and drifting cloud puffs. House coordinates
    /// (open front at z = 0). Built by <see cref="TitleBackdrop"/>, destroyed when the title closes.
    /// </summary>
    /// <code>
    /// var d = new GameObject("Diorama").AddComponent&lt;TitleDiorama&gt;();
    /// d.transform.SetParent(houseView.transform, false);
    /// d.Build(houseView.Bounds, sim.Rooms, roomHeight);
    /// </code>
    public sealed class TitleDiorama : MonoBehaviour
    {
        private struct Drift { public Transform T; public float Speed; public float Phase; }

        private static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

        private readonly List<Drift> _clouds = new List<Drift>();
        private readonly List<Drift> _sway = new List<Drift>();
        private float _left, _right;

        /// <summary>Builds the land and props around a house whose front is <paramref name="house"/>.</summary>
        public void Build(Rect house, IReadOnlyList<Trouble.Sim.RoomState> rooms, float roomHeight)
        {
            float g = HouseLayout.FloorY(1, roomHeight);
            float bx0 = float.MaxValue, bx1 = float.MinValue;
            foreach (var r in rooms)
                if (r.Floor == 0) { bx0 = Mathf.Min(bx0, r.X); bx1 = Mathf.Max(bx1, r.X + r.Width); }
            if (bx0 > bx1) bx0 = bx1 = house.center.x;
            float hx0 = house.xMin, hx1 = house.xMax;
            // Wide and deep enough that no edge of the land is ever on screen.
            float L = hx0 - 45f, R = hx1 + 45f, D = 45f, back = 4.4f;
            float bottom = HouseLayout.FloorY(0, roomHeight) - 30f, grassT = 0.35f;
            _left = L - 6f;
            _right = R + 6f;

            var grass = Hex("#7CC456");
            var grassLip = Hex("#9AD86A");
            var soilTop = Hex("#9A6438");
            var soilLow = Hex("#744627");
            float mid = (g - grassT + bottom) * 0.5f;

            // Grass top (never over the house), with a lighter lip along the cut-away front.
            Slab("Grass", grass, L, hx0, 0f, D, g - grassT, g);
            Slab("Grass", grass, hx1, R, 0f, D, g - grassT, g);
            Slab("Grass", grass, hx0, hx1, back, D, g - grassT, g);
            Slab("Grass lip", grassLip, L, hx0, -0.12f, 0.15f, g - grassT - 0.05f, g + 0.03f);
            Slab("Grass lip", grassLip, hx1, R, -0.12f, 0.15f, g - grassT - 0.05f, g + 0.03f);

            // Soil in two strata, leaving the basement rooms open.
            void Soil(float x0, float x1, float z0, float z1, float y0, float y1)
            {
                if (y1 <= y0 || x1 <= x0) return;
                float m = Mathf.Clamp(mid, y0, y1);
                Slab("Soil", soilTop, x0, x1, z0, z1, m, y1);
                Slab("Soil low", soilLow, x0, x1, z0, z1, y0, m);
            }
            Soil(L, bx0, 0f, D, bottom, g - grassT);
            Soil(bx1, R, 0f, D, bottom, g - grassT);
            Soil(bx0, bx1, 0f, D, bottom, HouseLayout.FloorY(0, roomHeight) - 0.35f);
            Soil(bx0, bx1, back, D, HouseLayout.FloorY(0, roomHeight) - 0.35f, g - grassT);

            // Pebbles in the cut face.
            var rnd = new System.Random(7);
            for (int i = 0; i < 160; i++)
            {
                float x = Mathf.Lerp(L + 0.5f, R - 0.5f, (float)rnd.NextDouble());
                if (x > bx0 - 0.3f && x < bx1 + 0.3f) continue;
                float y = Mathf.Lerp(bottom + 0.4f, g - grassT - 0.4f, (float)rnd.NextDouble());
                float s = 0.25f + (float)rnd.NextDouble() * 0.35f;
                var stone = rnd.NextDouble() < 0.5 ? Hex("#B9A58F") : Hex("#8E7C6B");
                LowPoly.Place(transform, "Pebble", LowPoly.Blob(), new Vector3(x, y, -0.02f), new Vector3(s * 1.4f, s, s), stone);
            }

            // Trees, pines and bushes on the lawn.
            Tree(new Vector3(hx0 - 3.2f, g, 2.2f), 1.25f, 0);
            Tree(new Vector3(hx0 - 7.5f, g, 5.5f), 1.5f, 1);
            Pine(new Vector3(hx0 - 5.2f, g, 9.5f), 1.4f);
            Tree(new Vector3(hx1 + 3.5f, g, 3f), 1.35f, 2);
            Pine(new Vector3(hx1 + 7.5f, g, 6.5f), 1.6f);
            Pine(new Vector3(hx1 + 4.5f, g, 10.5f), 1.2f);
            Tree(new Vector3((hx0 + hx1) * 0.5f - 2f, g, 9f), 1.7f, 1);
            Pine(new Vector3((hx0 + hx1) * 0.5f + 4f, g, 11f), 1.8f);
            for (int i = 0; i < 8; i++)
            {
                float x = i < 4 ? hx0 - 1.2f - i * 2.2f : hx1 + 1.2f + (i - 4) * 2.3f;
                Bush(new Vector3(x, g, 1.2f + (i % 2) * 0.6f), 0.8f + (i % 3) * 0.15f, i);
            }

            // Picket fence along the front edge of the lawn, both sides.
            Fence(L + 0.6f, hx0 - 0.8f, g, 0.45f);
            Fence(hx1 + 0.8f, R - 0.6f, g, 0.45f);

            // Mailbox by the house, a power pole on the right.
            Prims.Box(transform, "Mailbox post", new Vector3(hx0 - 1.4f, g + 0.55f, 0.6f), new Vector3(0.12f, 1.1f, 0.12f), Hex("#6B4428"));
            Prims.Box(transform, "Mailbox", new Vector3(hx0 - 1.4f, g + 1.2f, 0.6f), new Vector3(0.55f, 0.38f, 0.32f), Hex("#D93A33"));
            Prims.Box(transform, "Flag", new Vector3(hx0 - 1.1f, g + 1.38f, 0.6f), new Vector3(0.05f, 0.3f, 0.05f), Hex("#F6C343"));
            LowPoly.Place(transform, "Pole", LowPoly.Prism(), new Vector3(R - 2.5f, g, 3f), new Vector3(0.3f, 9f, 0.3f), Hex("#7B5638"));
            Prims.Box(transform, "Cross arm", new Vector3(R - 2.5f, g + 8.4f, 3f), new Vector3(1.8f, 0.15f, 0.18f), Hex("#5E3F27"));

            // A lawn full of trees out to the edges (never on the house or the front strip).
            for (int i = 0; i < 70; i++)
            {
                float x = Mathf.Lerp(L + 2f, R - 2f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(3f, D - 3f, (float)rnd.NextDouble());
                if (x > hx0 - 2.5f && x < hx1 + 2.5f && z < back + 3f) continue;
                if (z < 4.5f && x > hx0 - 12f && x < hx1 + 12f) continue;
                float s = 1.1f + (float)rnd.NextDouble() * 0.9f;
                if (rnd.NextDouble() < 0.45) Pine(new Vector3(x, g, z), s);
                else Tree(new Vector3(x, g, z), s, i % 3);
            }
            for (int i = 0; i < 6; i++)
            {
                float x = i < 3 ? hx0 - 16f - i * 8f : hx1 + 16f + (i - 3) * 8f;
                Neighbour(new Vector3(x, g, 7f + (i % 2) * 3f), i % 2 == 0 ? Hex("#F3E3C3") : Hex("#E6D3EE"), i % 2 == 0 ? Hex("#8C5A3C") : Hex("#5E6E96"));
            }

            // Rolling hills and a tree line behind the lawn, neighbours on both sides.
            var hill = Hex("#8FCB6A");
            var hillFar = Hex("#A9D98B");
            for (int i = 0; i < 7; i++)
            {
                float x = Mathf.Lerp(L - 14f, R + 14f, i / 6f);
                if (i > 6) break;
                float s = 14f + (i % 3) * 4f;
                LowPoly.Place(transform, "Hill", LowPoly.Blob(), new Vector3(x, g - 2.5f, D + 4f + (i % 2) * 8f), new Vector3(s * 1.6f, s * 0.55f, s), i % 2 == 0 ? hill : hillFar);
            }
            for (int i = 0; i < 12; i++)
            {
                float x = Mathf.Lerp(L - 4f, R + 4f, i / 11f) + (i % 3 - 1) * 0.8f;
                if (i % 3 == 0) Pine(new Vector3(x, g, 17f + (i % 4)), 1.6f + (i % 2) * 0.4f);
                else Tree(new Vector3(x, g, 16f + (i % 5)), 1.4f + (i % 3) * 0.3f, i % 3);
            }
            Neighbour(new Vector3(L - 1.5f, g, 9f), Hex("#F2D9B8"), Hex("#C2614B"));
            Neighbour(new Vector3(R + 2.5f, g, 11f), Hex("#D8E4F0"), Hex("#4B5A78"));

            // A stepping-stone path to the door, flower patches, a lamp post and a bin.
            for (int i = 0; i < 5; i++)
                LowPoly.Place(transform, "Stone", LowPoly.Blob(), new Vector3(hx0 - 4.2f + i * 0.75f, g + 0.02f, 0.7f + (i % 2) * 0.15f), new Vector3(0.6f, 0.12f, 0.45f), Hex("#D9CBB8"));
            var petals = new[] { Hex("#F48FB1"), Hex("#FFD54F"), Hex("#FFFFFF"), Hex("#B39DDB") };
            for (int i = 0; i < 26; i++)
            {
                float x = i < 13 ? Mathf.Lerp(L + 1f, hx0 - 5f, i / 12f) : Mathf.Lerp(hx1 + 2f, R - 3.5f, (i - 13) / 12f);
                float z = 1.6f + (i * 37 % 10) * 0.35f;
                LowPoly.Place(transform, "Stem", LowPoly.Prism(), new Vector3(x, g, z), new Vector3(0.04f, 0.32f, 0.04f), Hex("#4E9E48"));
                LowPoly.Place(transform, "Flower", LowPoly.Blob(), new Vector3(x, g + 0.36f, z), Vector3.one * 0.2f, petals[i % petals.Length]);
            }
            LowPoly.Place(transform, "Lamp post", LowPoly.Prism(), new Vector3(hx1 + 1.6f, g, 0.9f), new Vector3(0.14f, 2.6f, 0.14f), Hex("#3B4A5E"));
            Prims.Box(transform, "Lamp head", new Vector3(hx1 + 1.6f, g + 2.7f, 0.9f), new Vector3(0.42f, 0.3f, 0.42f), Hex("#3B4A5E"));
            LowPoly.PlaceUnlit(transform, "Lamp glow", LowPoly.Blob(), new Vector3(hx1 + 1.6f, g + 2.5f, 0.9f), Vector3.one * 0.32f);
            LowPoly.Place(transform, "Bin", LowPoly.Prism(), new Vector3(R - 1.2f, g, 1.2f), new Vector3(0.6f, 0.85f, 0.6f), Hex("#4F6B5A"));
            Prims.Box(transform, "Bin lid", new Vector3(R - 1.2f, g + 0.9f, 1.2f), new Vector3(0.66f, 0.08f, 0.66f), Hex("#3D5547"));

            // Clouds: clusters of white facets, drifting.
            for (int i = 0; i < 18; i++)
            {
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(transform, false);
                cloud.localPosition = new Vector3(Mathf.Lerp(_left, _right, (i + 0.3f * (i % 2)) / 18f), g + 13f + (i % 3) * 1.6f, 6f + (i % 4) * 4f);
                for (int k = 0; k < 4; k++)
                {
                    float s = 1.6f + (k == 1 ? 0.9f : 0f) + (i % 2) * 0.3f;
                    LowPoly.PlaceUnlit(cloud, "Puff", LowPoly.Blob(), new Vector3((k - 1.5f) * 1.1f, k == 1 || k == 2 ? 0.45f : 0f, 0f), new Vector3(s, s * 0.8f, s));
                }
                _clouds.Add(new Drift { T = cloud, Speed = 0.35f + (i % 3) * 0.15f });
            }
        }

        private void Slab(string name, Color color, float x0, float x1, float z0, float z1, float y0, float y1)
        {
            if (x1 <= x0 || z1 <= z0 || y1 <= y0) return;
            Prims.Box(transform, name, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, y1 - y0, z1 - z0), color);
        }

        private void Tree(Vector3 at, float scale, int variant)
        {
            var root = new GameObject("Tree").transform;
            root.SetParent(transform, false);
            root.localPosition = at;
            root.localScale = Vector3.one * scale;
            LowPoly.Place(root, "Trunk", LowPoly.Prism(), Vector3.zero, new Vector3(0.35f, 1.5f, 0.35f), Hex("#8A5A36"));
            var leaf = variant == 1 ? Hex("#5FB34A") : Hex("#6CC04F");
            var light = Hex("#93D766");
            LowPoly.Place(root, "Canopy", LowPoly.Blob(), new Vector3(0f, 2.1f, 0f), new Vector3(2.2f, 2f, 2.2f), leaf);
            LowPoly.Place(root, "Canopy", LowPoly.Blob(), new Vector3(0.55f, 2.75f, -0.2f), new Vector3(1.4f, 1.3f, 1.4f), light);
            LowPoly.Place(root, "Canopy", LowPoly.Blob(), new Vector3(-0.6f, 2.5f, 0.3f), new Vector3(1.3f, 1.2f, 1.3f), leaf * 0.92f + new Color(0, 0, 0, 0.08f));
            if (variant == 2)
                for (int i = 0; i < 4; i++)
                    LowPoly.Place(root, "Fruit", LowPoly.Blob(), new Vector3(Mathf.Cos(i * 1.6f) * 0.9f, 1.8f + (i % 2) * 0.6f, -0.95f), Vector3.one * 0.22f, Hex("#E2504A"));
            _sway.Add(new Drift { T = root, Speed = 0.7f + variant * 0.2f, Phase = at.x });
        }

        private void Pine(Vector3 at, float scale)
        {
            var root = new GameObject("Pine").transform;
            root.SetParent(transform, false);
            root.localPosition = at;
            root.localScale = Vector3.one * scale;
            LowPoly.Place(root, "Trunk", LowPoly.Prism(), Vector3.zero, new Vector3(0.3f, 0.9f, 0.3f), Hex("#7A4E2D"));
            var dark = Hex("#3F8F4A");
            LowPoly.Place(root, "Tier", LowPoly.Cone(), new Vector3(0f, 0.7f, 0f), new Vector3(2f, 1.8f, 2f), dark);
            LowPoly.Place(root, "Tier", LowPoly.Cone(), new Vector3(0f, 1.6f, 0f), new Vector3(1.55f, 1.6f, 1.55f), Hex("#4CA055"));
            LowPoly.Place(root, "Tier", LowPoly.Cone(), new Vector3(0f, 2.45f, 0f), new Vector3(1.05f, 1.35f, 1.05f), Hex("#5BB060"));
            _sway.Add(new Drift { T = root, Speed = 0.9f, Phase = at.z });
        }

        /// <summary>A small neighbour house: walls, a wedge roof, a door and lit windows.</summary>
        private void Neighbour(Vector3 at, Color wall, Color roof)
        {
            var root = new GameObject("Neighbour").transform;
            root.SetParent(transform, false);
            root.localPosition = at;
            Prims.Box(root, "Walls", new Vector3(0f, 1.6f, 0f), new Vector3(4.2f, 3.2f, 3.6f), wall);
            LowPoly.Place(root, "Roof", LowPoly.Wedge(), new Vector3(0f, 3.2f, 0f), new Vector3(4.8f, 1.9f, 4f), roof);
            LowPoly.Place(root, "Chimney", LowPoly.Prism(), new Vector3(1.2f, 3.6f, 0.6f), new Vector3(0.45f, 1.4f, 0.45f), Hex("#9B5B45"));
            Prims.Box(root, "Door", new Vector3(0f, 0.75f, -1.81f), new Vector3(0.8f, 1.5f, 0.05f), Hex("#7A4A2E"));
            foreach (float x in new[] { -1.3f, 1.3f })
                Prims.Box(root, "Window", new Vector3(x, 1.9f, -1.81f), new Vector3(0.75f, 0.7f, 0.05f), Hex("#FFE7A3"));
        }

        private void Bush(Vector3 at, float scale, int variant)
        {
            var root = new GameObject("Bush").transform;
            root.SetParent(transform, false);
            root.localPosition = at;
            root.localScale = Vector3.one * scale;
            LowPoly.Place(root, "Leaves", LowPoly.Blob(), new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 1f, 1.1f), Hex("#5AA845"));
            LowPoly.Place(root, "Leaves", LowPoly.Blob(), new Vector3(0.5f, 0.35f, -0.1f), new Vector3(0.8f, 0.7f, 0.8f), Hex("#73BF52"));
            var petal = variant % 2 == 0 ? Hex("#F48FB1") : Hex("#FFD54F");
            for (int i = 0; i < 4; i++)
                LowPoly.Place(root, "Flower", LowPoly.Blob(), new Vector3(-0.4f + i * 0.3f, 0.65f + (i % 2) * 0.25f, -0.5f), Vector3.one * 0.16f, petal);
        }

        private void Fence(float x0, float x1, float g, float z)
        {
            var white = Hex("#F7F4EC");
            for (float x = x0; x <= x1; x += 0.55f)
                Prims.Box(transform, "Picket", new Vector3(x, g + 0.45f, z), new Vector3(0.14f, 0.9f, 0.08f), white);
            Prims.Box(transform, "Rail", new Vector3((x0 + x1) * 0.5f, g + 0.3f, z + 0.06f), new Vector3(x1 - x0 + 0.2f, 0.08f, 0.05f), white);
            Prims.Box(transform, "Rail", new Vector3((x0 + x1) * 0.5f, g + 0.68f, z + 0.06f), new Vector3(x1 - x0 + 0.2f, 0.08f, 0.05f), white);
        }

        private void Update()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime, width = _right - _left;
            for (int i = 0; i < _clouds.Count; i++)
            {
                var p = _clouds[i].T.localPosition;
                p.x += _clouds[i].Speed * dt;
                if (p.x > _right) p.x -= width;
                _clouds[i].T.localPosition = p;
            }
            for (int i = 0; i < _sway.Count; i++)
                _sway[i].T.localRotation = Quaternion.Euler(Mathf.Sin(now * _sway[i].Speed + _sway[i].Phase) * 1.2f, 0f, Mathf.Sin(now * _sway[i].Speed * 0.8f + _sway[i].Phase) * 2f);
        }
    }
}
