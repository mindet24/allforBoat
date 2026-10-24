using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trouble.Game
{
    /// <summary>
    /// Day mode: fades any piece of the house (walls, floor slabs, furniture) that stands between the camera and a
    /// character or an appliance down to <see cref="TrippedViewConfig.occluderAlpha"/>, and back up once it no longer
    /// does, so nothing in the house ever hides who or what the player is looking for.
    /// </summary>
    /// <remarks>
    /// Each mesh under the occluder root gets a box trigger on the Ignore Raycast layer (so click picking never sees it),
    /// and every frame a few rays go from the camera to each visible sprite under the target roots. A faded mesh draws
    /// with transparent copies of its materials; at full alpha it goes back to the shared opaque ones.
    /// </remarks>
    public sealed class OccluderFader : MonoBehaviour
    {
        private const int OccluderLayer = 2; // Ignore Raycast: outside Physics.DefaultRaycastLayers.
        private const int MaxHits = 32;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private sealed class Occluder
        {
            public Renderer Renderer;
            public Material[] Opaque;
            public Material[] Faded;
            public Color[] BaseColors;
            public float Alpha = 1f;
            public int HitFrame = -1;
        }

        private readonly Dictionary<Collider, Occluder> _byCollider = new Dictionary<Collider, Occluder>();
        private readonly List<Occluder> _occluders = new List<Occluder>();
        private readonly List<Transform> _targets = new List<Transform>();
        private readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private TrippedViewConfig _config;
        private Camera _camera;

        /// <summary>
        /// Makes every mesh under <paramref name="occluders"/> fadeable, and watches the sprites under
        /// <paramref name="targets"/> (the character, the appliances, the family).
        /// </summary>
        public void Build(Transform occluders, IEnumerable<Transform> targets, Camera cam, TrippedViewConfig config)
        {
            _config = config;
            _camera = cam;
            _targets.AddRange(targets);
            foreach (var r in occluders.GetComponentsInChildren<MeshRenderer>())
            {
                if (NeverFades(r.name, config.neverFadeParts)) continue;
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var box = new GameObject("Occluder").AddComponent<BoxCollider>();
                box.gameObject.layer = OccluderLayer;
                box.transform.SetParent(r.transform, false);
                var b = filter.sharedMesh.bounds;
                box.center = b.center;
                box.size = b.size;
                box.isTrigger = true;
                var o = new Occluder { Renderer = r, Opaque = r.sharedMaterials };
                _byCollider[box] = o;
                _occluders.Add(o);
            }
            Physics.SyncTransforms();
        }

        /// <summary>True when a mesh's name contains one of <paramref name="parts"/> (case-insensitive): it always stays solid.</summary>
        public static bool NeverFades(string meshName, string[] parts)
        {
            if (parts == null || string.IsNullOrEmpty(meshName)) return false;
            foreach (var p in parts)
                if (!string.IsNullOrEmpty(p) && meshName.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private void LateUpdate()
        {
            if (_camera == null || _config == null) return;
            int frame = Time.frameCount;
            Vector3 eye = _camera.transform.position;
            foreach (var root in _targets)
            {
                if (root == null || !root.gameObject.activeInHierarchy) continue;
                root.GetComponentsInChildren(false, _sprites);
                foreach (var s in _sprites)
                {
                    if (!s.enabled || s.sprite == null || s.color.a < 0.05f) continue;
                    var b = s.bounds;
                    // Down the middle of the sprite: low, middle and near the top (the head of a character).
                    for (int i = 0; i < 3; i++)
                        Cast(eye, b.center + new Vector3(0f, b.extents.y * (i - 0.5f) * 1.1f, 0f), frame);
                }
            }

            float step = _config.occluderFadeSpeed * Time.unscaledDeltaTime;
            foreach (var o in _occluders)
            {
                float want = o.HitFrame == frame ? _config.occluderAlpha : 1f;
                if (Mathf.Approximately(o.Alpha, want)) continue;
                o.Alpha = Mathf.MoveTowards(o.Alpha, want, step);
                Apply(o);
            }
        }

        private void Cast(Vector3 eye, Vector3 point, int frame)
        {
            var dir = point - eye;
            float dist = dir.magnitude - 0.05f;
            if (dist <= 0f) return;
            int n = Physics.RaycastNonAlloc(eye, dir.normalized, _hits, dist, 1 << OccluderLayer, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
                if (_byCollider.TryGetValue(_hits[i].collider, out var o)) o.HitFrame = frame;
        }

        private static void Apply(Occluder o)
        {
            if (o.Renderer == null) return;
            if (o.Alpha >= 0.999f)
            {
                o.Alpha = 1f;
                o.Renderer.sharedMaterials = o.Opaque;
                o.Renderer.shadowCastingMode = ShadowCastingMode.On;
                return;
            }
            if (o.Faded == null)
            {
                o.Faded = new Material[o.Opaque.Length];
                o.BaseColors = new Color[o.Opaque.Length];
                for (int i = 0; i < o.Opaque.Length; i++)
                {
                    if (o.Opaque[i] == null) continue;
                    var m = new Material(o.Opaque[i]) { name = o.Opaque[i].name + " (faded)" };
                    MakeTransparent(m);
                    o.Faded[i] = m;
                    o.BaseColors[i] = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
                }
            }
            if (o.Renderer.sharedMaterials[0] != o.Faded[0])
            {
                o.Renderer.sharedMaterials = o.Faded;
                o.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            for (int i = 0; i < o.Faded.Length; i++)
            {
                var m = o.Faded[i];
                if (m == null) continue;
                var c = o.BaseColors[i];
                c.a *= o.Alpha;
                if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
                if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
            }
        }

        /// <summary>Switches a URP Lit material to alpha-blended transparency (what the inspector's Surface Type does).</summary>
        private static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        private void OnDestroy()
        {
            foreach (var o in _occluders)
            {
                if (o.Faded == null) continue;
                foreach (var m in o.Faded)
                    if (m != null) Destroy(m);
            }
        }
    }
}
