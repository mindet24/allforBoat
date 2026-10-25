using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Flat-shaded low-poly meshes for the cute diorama look: an icosahedron blob (trees, bushes, rocks, clouds) and an
    /// n-sided cone or prism (pine trees, trunks, posts). Every triangle has its own vertices, so faces read as facets.
    /// Meshes are made once per shape and shared. Main thread only.
    /// </summary>
    /// <code>
    /// var tree = LowPoly.Place(parent, "Canopy", LowPoly.Blob(), new Vector3(2f, 1.5f, 3f), Vector3.one * 1.2f, green);
    /// </code>
    public static class LowPoly
    {
        private static Mesh _blob, _cone, _prism, _wedge;
        private static Material _unlitWhite;

        /// <summary>A unit icosahedron (radius 0.5), flat shaded.</summary>
        public static Mesh Blob()
        {
            if (_blob != null) return _blob;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized * 0.5f;
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            return _blob = Faceted("LowPoly Blob", v, f);
        }

        /// <summary>A unit 7-sided cone (base radius 0.5 at y 0, tip at y 1).</summary>
        public static Mesh Cone() => _cone != null ? _cone : _cone = Round("LowPoly Cone", 7, 0.5f, 0f);

        /// <summary>A unit 6-sided prism (radius 0.5, y 0 to 1).</summary>
        public static Mesh Prism() => _prism != null ? _prism : _prism = Round("LowPoly Prism", 6, 0.5f, 0.5f);

        /// <summary>A unit roof wedge: a triangular prism 1 wide (x), 1 deep (z), ridge at y 1 along z.</summary>
        public static Mesh Wedge()
        {
            if (_wedge != null) return _wedge;
            var a = new Vector3(-0.5f, 0f, -0.5f); var b = new Vector3(0.5f, 0f, -0.5f); var c = new Vector3(0f, 1f, -0.5f);
            var d = new Vector3(-0.5f, 0f, 0.5f); var e = new Vector3(0.5f, 0f, 0.5f); var f = new Vector3(0f, 1f, 0.5f);
            var v = new List<Vector3>();
            var t = new List<int>();
            void Tri(Vector3 p, Vector3 q, Vector3 r) { t.Add(v.Count); v.Add(p); t.Add(v.Count); v.Add(q); t.Add(v.Count); v.Add(r); }
            Tri(a, b, c); Tri(d, f, e);
            Tri(a, c, f); Tri(a, f, d);
            Tri(b, e, f); Tri(b, f, c);
            Tri(a, d, e); Tri(a, e, b);
            Outward(v, t, new Vector3(0f, 0.33f, 0f));
            var mesh = new Mesh { name = "LowPoly Wedge" };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return _wedge = mesh;
        }

        /// <summary>A mesh object under <paramref name="parent"/> with the shared lit material of <paramref name="color"/>.</summary>
        public static MeshRenderer Place(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Prims.Lit(color);
            return r;
        }

        /// <summary>Like <see cref="Place"/>, but always bright white (no shading): clouds.</summary>
        public static MeshRenderer PlaceUnlit(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale)
        {
            var r = Place(parent, name, mesh, position, scale, Color.white);
            if (_unlitWhite == null)
            {
                var shipped = Resources.Load<Material>("Materials/PrimUnlit");
                if (shipped != null) _unlitWhite = new Material(shipped) { name = "LowPoly Unlit White", color = Color.white };
            }
            if (_unlitWhite != null) r.sharedMaterial = _unlitWhite;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        private static Mesh Round(string name, int sides, float bottomRadius, float topRadius)
        {
            var v = new List<Vector3>();
            var f = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c) { f.Add(v.Count); v.Add(a); f.Add(v.Count); v.Add(b); f.Add(v.Count); v.Add(c); }
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var b0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * bottomRadius;
                var b1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * bottomRadius;
                var t0 = new Vector3(Mathf.Cos(a0) * topRadius, 1f, Mathf.Sin(a0) * topRadius);
                var t1 = new Vector3(Mathf.Cos(a1) * topRadius, 1f, Mathf.Sin(a1) * topRadius);
                Tri(b0, t0, b1);
                if (topRadius > 0f)
                {
                    Tri(b1, t0, t1);
                    Tri(new Vector3(0f, 1f, 0f), t1, t0);
                }
                Tri(Vector3.zero, b0, b1);
            }
            Outward(v, f, new Vector3(0f, 0.4f, 0f));
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetTriangles(f, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Turns every triangle to face away from <paramref name="centre"/> (Unity draws clockwise fronts).</summary>
        private static void Outward(List<Vector3> v, List<int> f, Vector3 centre)
        {
            for (int i = 0; i < f.Count; i += 3)
            {
                Vector3 a = v[f[i]], b = v[f[i + 1]], c = v[f[i + 2]];
                var n = Vector3.Cross(b - a, c - a);
                var mid = (a + b + c) / 3f;
                var outward = mid - centre;
                // Flat bottoms sit on the centre's level: use straight down for them.
                if (Mathf.Abs(mid.y - centre.y) > 0.39f && Mathf.Abs(n.normalized.y) > 0.99f) outward = new Vector3(0f, mid.y - centre.y, 0f);
                if (Vector3.Dot(n, outward) < 0f) (f[i + 1], f[i + 2]) = (f[i + 2], f[i + 1]);
            }
        }

        private static Mesh Faceted(string name, Vector3[] corners, int[] faces)
        {
            var v = new Vector3[faces.Length];
            var f = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++)
            {
                v[i] = corners[faces[i]];
                f[i] = i;
            }
            var list = new List<Vector3>(v);
            var tris = new List<int>(f);
            Outward(list, tris, Vector3.zero);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(list);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
