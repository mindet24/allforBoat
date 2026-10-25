using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The neighbourhood around the day-mode house, behind both the title and the game: a sky that fills the view,
    /// the sun, drifting clouds, rolling hills, neighbour houses, swaying trees, a picket fence with bushes, power
    /// poles with sagging wires, birds, chimney smoke, grass, and the soil the basement is dug into. All sprites are
    /// <see cref="SceneryArt"/>; nothing here is gameplay and nothing has a collider.
    /// </summary>
    /// <remarks>
    /// House coordinates (x right, y up, z into the screen; the open front is z = 0). Bigger z = farther away, so far
    /// layers are drawn larger to keep their size on screen. Build once per house; destroy with the house.
    /// <code>
    /// var scenery = new GameObject("Scenery").AddComponent&lt;SceneryView&gt;();
    /// scenery.transform.SetParent(houseView.transform, false);
    /// scenery.Build(houseView.Bounds, groundY, basementSpan, camera, ui, kitRoot);
    /// </code>
    /// </remarks>
    public sealed class SceneryView : MonoBehaviour
    {
        private struct Drifter { public Transform T; public float Speed; public float Phase; }
        private struct Puff { public Transform T; public SpriteRenderer R; public float Born; }

        private const float SkyZ = 60f;
        private readonly List<Drifter> _clouds = new List<Drifter>();
        private readonly List<Drifter> _birds = new List<Drifter>();
        private readonly List<Drifter> _canopies = new List<Drifter>();
        private readonly List<Puff> _puffs = new List<Puff>();
        private readonly Stack<Puff> _freePuffs = new Stack<Puff>();
        private Camera _camera;
        private SpriteRenderer _sky;
        private Rect _span;
        private Vector3 _chimneyTop;
        private bool _hasChimney;
        private float _nextPuff;
        private int _order = -2000;

        /// <summary>Lays the scenery out around a house of <paramref name="house"/> (front rect, metres).</summary>
        /// <param name="groundY">Height of the ground outside (the floor of the first storey above the basement).</param>
        /// <param name="basementX">x range (min, max) of the rooms under the ground; the soil leaves them open.</param>
        /// <param name="kit">The house kit root, searched for a "Chimney" to smoke; may be null.</param>
        public void Build(Rect house, float groundY, Vector2 basementX, Camera cam, TrippedUiConfig ui, Transform kit)
        {
            _camera = cam;
            float g = groundY;
            float l = house.xMin, r = house.xMax, mid = house.center.x;
            _span = new Rect(l - 70f, g, house.width + 140f, 30f);

            _sky = Sprite("Sky", UiArt.Gradient(ui.backdropTop, ui.backdropBottom), Vector3.zero, Vector3.one);
            Sprite("Sun", SceneryArt.Sun(), new Vector3(r + 14f, g + 26f, SkyZ - 4f), Vector3.one * 9f);

            for (int i = 0; i < 9; i++)
            {
                float x = Mathf.Lerp(_span.xMin, _span.xMax, (i + 0.37f * (i % 3)) / 9f);
                float z = 40f + (i % 3) * 5f;
                var c = Sprite("Cloud", SceneryArt.Cloud(i % 4), new Vector3(x, g + 17f + (i * 7 % 5) * 2.2f, z), Vector3.one * (4f + (i % 3) * 1.2f));
                _clouds.Add(new Drifter { T = c.transform, Speed = 0.25f + (i % 4) * 0.12f });
            }

            var far = new Color(0.62f, 0.80f, 0.56f);
            Band("Far hills", SceneryArt.Hill(0, far, far * 0.82f), g - 0.2f, 36f, 10f, 4.2f);
            var near = new Color(0.47f, 0.73f, 0.36f);
            Band("Hills", SceneryArt.Hill(1, near, near * 0.8f), g - 0.2f, 24f, 8f, 2.6f);
            Ground(house, g, ui);

            // Neighbours, trees, the fence and the poles, out to both sides.
            Sprite("Neighbour", SceneryArt.House(0), new Vector3(l - 12f, g, 16f), Vector3.one * 2.6f);
            Sprite("Neighbour", SceneryArt.House(1), new Vector3(r + 15f, g, 18f), Vector3.one * 2.8f);
            Sprite("Neighbour", SceneryArt.House(2), new Vector3(l - 26f, g, 20f), Vector3.one * 2.9f);
            Tree(new Vector3(l - 4.5f, g, 7f), 1.9f, 0);
            Tree(new Vector3(l - 9f, g, 9f), 2.2f, 1);
            Tree(new Vector3(r + 5f, g, 8f), 2.0f, 2);
            Tree(new Vector3(r + 22f, g, 12f), 2.4f, 1);
            Tree(new Vector3(mid - 3f, g, 12f), 2.6f, 2);
            Tree(new Vector3(mid + 5f, g, 13f), 2.4f, 0);
            for (float x = l - 20f; x < l - 0.8f; x += 3f) Sprite("Fence", SceneryArt.Fence(), new Vector3(x, g, 5f), Vector3.one);
            for (float x = r + 0.8f; x < r + 20f; x += 3f) Sprite("Fence", SceneryArt.Fence(), new Vector3(x, g, 5f), Vector3.one);
            for (int i = 0; i < 6; i++)
            {
                float x = i < 3 ? l - 2.5f - i * 5.5f : r + 2.5f + (i - 3) * 6f;
                Sprite("Bush", SceneryArt.Bush(i % 2), new Vector3(x, g, 4.6f), Vector3.one * (1f + (i % 3) * 0.2f));
            }
            Sprite("Mailbox", SceneryArt.Mailbox(), new Vector3(l - 1.6f, g, 1.5f), Vector3.one * 1.3f);
            var poleA = new Vector3(r + 3.5f, g, 10f);
            var poleB = new Vector3(r + 19f, g, 10f);
            Sprite("Pole", SceneryArt.Pole(), poleA, Vector3.one * 1.45f);
            Sprite("Pole", SceneryArt.Pole(), poleB, Vector3.one * 1.45f);
            Wire(poleA + new Vector3(-0.47f, 9.6f, -0.05f), poleB + new Vector3(-0.47f, 9.6f, -0.05f));
            Wire(poleA + new Vector3(0.47f, 9.6f, -0.05f), poleB + new Vector3(0.47f, 9.6f, -0.05f));

            for (int i = 0; i < 4; i++)
            {
                var b = Sprite("Bird", SceneryArt.Bird(), new Vector3(Mathf.Lerp(_span.xMin, _span.xMax, i / 4f), g + 12f + i * 1.3f, 26f), Vector3.one * 1.4f);
                _birds.Add(new Drifter { T = b.transform, Speed = 1.6f + i * 0.35f, Phase = i * 1.7f });
            }

            if (kit != null)
                foreach (var rr in kit.GetComponentsInChildren<Renderer>())
                    if (rr.name.Contains("Chimney"))
                    {
                        var bb = rr.bounds;
                        _chimneyTop = transform.InverseTransformPoint(new Vector3(bb.center.x, bb.max.y, bb.center.z));
                        _hasChimney = true;
                        break;
                    }

            // Soil in the cut-away front: everywhere below the ground except the basement rooms.
            var soil = SceneryArt.Soil();
            float deep = house.yMin - 6f;
            Tiled("Soil", soil, Rect.MinMaxRect(_span.xMin, deep - 30f, basementX.x, g), 0.04f);
            Tiled("Soil", soil, Rect.MinMaxRect(basementX.y, deep - 30f, _span.xMax, g), 0.04f);
            Tiled("Soil", soil, Rect.MinMaxRect(basementX.x, deep - 30f, basementX.y, house.yMin), 0.04f);
            var grass = SceneryArt.Grass();
            Tiled("Grass edge", grass, Rect.MinMaxRect(_span.xMin, g - 0.25f, l, g + 0.4f), 0.02f);
            Tiled("Grass edge", grass, Rect.MinMaxRect(r, g - 0.25f, _span.xMax, g + 0.4f), 0.02f);
            LateUpdate();
        }

        private SpriteRenderer Sprite(string name, Sprite sprite, Vector3 at, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = _order++;
            return sr;
        }

        /// <summary>A hill band <paramref name="height"/> high, repeated across the whole span at depth z.</summary>
        private void Band(string name, Sprite sprite, float baseY, float z, float tileWidth, float height)
        {
            var size = sprite.bounds.size;
            for (float x = _span.xMin - 20f; x < _span.xMax + 20f; x += tileWidth)
                Sprite(name, sprite, new Vector3(x, baseY, z), new Vector3(tileWidth / size.x * 1.01f, height / size.y, 1f));
        }

        /// <summary>The green ground plane, left, right and behind the house (it never cuts through a room).</summary>
        private void Ground(Rect house, float g, TrippedUiConfig ui)
        {
            var flat = UiArt.Gradient(ui.grass, ui.grass * 0.85f);
            void Plane(Rect xz)
            {
                var sr = Sprite("Ground", flat, new Vector3(xz.center.x, g - 0.02f, xz.center.y), Vector3.one);
                sr.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var s = flat.bounds.size;
                sr.transform.localScale = new Vector3(xz.width / s.x, xz.height / s.y, 1f);
            }
            Plane(Rect.MinMaxRect(_span.xMin, 0f, house.xMin, 60f));
            Plane(Rect.MinMaxRect(house.xMax, 0f, _span.xMax, 60f));
            Plane(Rect.MinMaxRect(house.xMin, 4.6f, house.xMax, 60f));
        }

        private void Tiled(string name, Sprite sprite, Rect r, float z)
        {
            // Tiled sprites grow from their pivot: place the pivot where it falls in the rect.
            var pivot = sprite.pivot / sprite.rect.size;
            var sr = Sprite(name, sprite, new Vector3(r.xMin + r.width * pivot.x, r.yMin + r.height * pivot.y, z), Vector3.one);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = r.size;
        }

        private void Tree(Vector3 at, float scale, int variant)
        {
            var sr = Sprite("Tree", SceneryArt.Tree(variant), at, Vector3.one * scale);
            _canopies.Add(new Drifter { T = sr.transform, Speed = 0.6f + variant * 0.15f, Phase = at.x });
        }

        private void Wire(Vector3 a, Vector3 b)
        {
            var go = new GameObject("Wire");
            go.transform.SetParent(transform, false);
            var lr = Prims.Line(go.transform, "Line", 0.05f, _order++);
            const int n = 12;
            var pts = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                var p = Vector3.Lerp(a, b, t);
                pts.Add(new Vector2(p.x, p.y - 0.9f * 4f * t * (1f - t)));
            }
            Prims.SetLine(lr, pts, a.z, new Color(0.16f, 0.17f, 0.2f));
        }

        /// <summary>Shows or hides everything but the sky (the Title's 3D diorama replaces the painted layers).</summary>
        public void ShowPainted(bool on)
        {
            foreach (Transform child in transform)
                if (_sky == null || child != _sky.transform) child.gameObject.SetActive(on);
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            if (_camera != null && _sky != null)
            {
                // The sky fills the view behind everything, wherever the camera looks.
                var ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                float along = (transform.TransformPoint(0f, 0f, SkyZ).z - ray.origin.z) / Mathf.Max(0.0001f, ray.direction.z);
                var centre = transform.InverseTransformPoint(ray.GetPoint(along));
                float h = 2f * along * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.4f;
                var size = _sky.sprite.bounds.size;
                _sky.transform.localPosition = new Vector3(centre.x, centre.y, SkyZ);
                _sky.transform.localScale = new Vector3(h * _camera.aspect / size.x, h / size.y, 1f);
            }

            float width = _span.width;
            for (int i = 0; i < _clouds.Count; i++)
            {
                var c = _clouds[i];
                var p = c.T.localPosition;
                p.x += c.Speed * dt;
                if (p.x > _span.xMax) p.x -= width;
                c.T.localPosition = p;
            }
            for (int i = 0; i < _birds.Count; i++)
            {
                var b = _birds[i];
                var p = b.T.localPosition;
                p.x += b.Speed * dt;
                p.y += Mathf.Sin(now * 0.8f + b.Phase) * 0.3f * dt;
                if (p.x > _span.xMax) p.x -= width;
                b.T.localPosition = p;
                float flap = Mathf.Sin(now * 9f + b.Phase);
                b.T.localScale = new Vector3(1.4f, 1.4f * (0.45f + 0.55f * Mathf.Abs(flap)), 1f);
            }
            for (int i = 0; i < _canopies.Count; i++)
            {
                var t = _canopies[i];
                t.T.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(now * t.Speed + t.Phase) * 1.6f);
            }
            UpdateSmoke(now);
        }

        private void UpdateSmoke(float now)
        {
            if (!_hasChimney) return;
            if (now >= _nextPuff)
            {
                _nextPuff = now + 0.55f;
                var puff = _freePuffs.Count > 0 ? _freePuffs.Pop() : NewPuff();
                puff.Born = now;
                puff.T.gameObject.SetActive(true);
                _puffs.Add(puff);
            }
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                var p = _puffs[i];
                float age = (now - p.Born) / 3.2f;
                if (age >= 1f)
                {
                    p.T.gameObject.SetActive(false);
                    _puffs.RemoveAt(i);
                    _freePuffs.Push(p);
                    continue;
                }
                p.T.localPosition = _chimneyTop + new Vector3(age * 1.6f + Mathf.Sin(age * 6f) * 0.2f, 0.3f + age * 3.2f, 0.1f);
                p.T.localScale = Vector3.one * (0.6f + age * 1.8f);
                p.R.color = new Color(0.95f, 0.95f, 0.97f, (1f - age) * 0.7f);
            }
        }

        private Puff NewPuff()
        {
            var sr = Sprite("Smoke", SceneryArt.Puff(), _chimneyTop, Vector3.one);
            sr.sortingOrder = 50;
            return new Puff { T = sr.transform, R = sr };
        }
    }
}
