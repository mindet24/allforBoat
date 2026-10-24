using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// World effects of the game-feel layer (G5): pooled sprite particles (sparks, coins) and floating text popups
    /// ("+3", "-1 ♥"). Lives on the house object, so it is destroyed with the level. Particles run on scaled time,
    /// so a blackout's slow motion slows them too; popups run on unscaled time so they always read.
    /// </summary>
    public sealed class JuiceFx : MonoBehaviour
    {
        private struct Particle
        {
            public SpriteRenderer Renderer;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public float Spin;
            public bool Gravity;
            public Color Color;
        }

        private sealed class Popup
        {
            public TextMeshPro Text;
            public Vector3 Origin;
            public float Born;
            public Color Color;
        }

        private JuiceConfig _juice;
        private readonly List<Particle> _live = new List<Particle>();
        private readonly Stack<SpriteRenderer> _free = new Stack<SpriteRenderer>();
        private readonly List<Popup> _popups = new List<Popup>();
        private int _created;
        private TMP_FontAsset _font;

        /// <summary>Particles alive now (for checks).</summary>
        public int LiveParticles => _live.Count;

        /// <summary>Popups on screen now (for checks).</summary>
        public int LivePopups => _popups.Count;

        /// <summary>Texts of the popups on screen now, newest last (for checks).</summary>
        public IEnumerable<string> PopupTexts
        {
            get
            {
                foreach (var p in _popups) yield return p.Text.text;
            }
        }

        /// <summary>Connects the config. Call once after AddComponent.</summary>
        public void Setup(JuiceConfig juice)
        {
            _juice = juice;
        }

        /// <summary>True if the popup font can draw the character (used to fall back from "♥" to "HEART").</summary>
        public bool FontHas(char c)
        {
            if (_font == null) _font = TMP_Settings.defaultFontAsset;
            return _font != null && _font.HasCharacter(c, true, true);
        }

        /// <summary>A burst of electric sparks flying out from <paramref name="at"/> (world x, y).</summary>
        public void Sparks(Vector2 at, int count, float speedScale = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float speed = _juice.sparkSpeed * speedScale * Random.Range(0.4f, 1.2f);
                var c = Color.Lerp(_juice.sparkColor, Color.white, Random.value * 0.5f);
                Spawn(at, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) + 0.4f) * speed, _juice.sparkLife * Random.Range(0.6f, 1.2f),
                    _juice.sparkSize * Random.Range(0.6f, 1.4f), c, true, PlaceholderArt.Circle);
            }
        }

        /// <summary>A fountain of coins from <paramref name="at"/>, mostly upward.</summary>
        public void Coins(Vector2 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(Mathf.PI * 0.2f, Mathf.PI * 0.8f);
                float speed = _juice.coinSpeed * Random.Range(0.6f, 1.15f);
                var c = Color.Lerp(_juice.coinColor, Color.white, Random.value * 0.25f);
                Spawn(at, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, _juice.coinLife * Random.Range(0.8f, 1.2f),
                    _juice.coinSize * Random.Range(0.8f, 1.2f), c, true, PlaceholderArt.Circle, Random.Range(-720f, 720f));
            }
        }

        /// <summary>A floating text that pops in with overshoot, rises and fades (score, lost heart, missed).</summary>
        public void Text(Vector2 at, string text, Color color)
        {
            var tmp = Prims.Text(transform, "Popup " + text, new Vector3(at.x, at.y, _juice.fxZ), new Vector2(3f, 1f), text, color,
                _juice.popupFontSize);
            tmp.enableAutoSizing = false;
            tmp.fontSize = _juice.popupFontSize;
            tmp.sortingOrder = _juice.fxSortingOrder + 2;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(30, 20, 10, 255);
            tmp.transform.localScale = Vector3.zero;
            _popups.Add(new Popup { Text = tmp, Origin = tmp.transform.localPosition, Born = Time.unscaledTime, Color = color });
        }

        private void Spawn(Vector2 at, Vector2 velocity, float life, float size, Color color, bool gravity, Sprite sprite, float spin = 0f)
        {
            if (_live.Count >= Mathf.Max(1, _juice.maxParticles)) return;
            SpriteRenderer sr;
            if (_free.Count > 0) sr = _free.Pop();
            else
            {
                var go = new GameObject("Fx " + _created++);
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = _juice.fxSortingOrder;
            }
            sr.sprite = sprite;
            sr.color = color;
            sr.gameObject.SetActive(true);
            sr.transform.localPosition = new Vector3(at.x, at.y, _juice.fxZ);
            sr.transform.localScale = Vector3.one * size;
            _live.Add(new Particle
            {
                Renderer = sr, Position = at, Velocity = velocity, Life = Mathf.Max(0.01f, life), Size = size,
                Gravity = gravity, Color = color, Spin = spin,
            });
        }

        private void Update()
        {
            if (_juice == null) return;
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Renderer.gameObject.SetActive(false);
                    _free.Push(p.Renderer);
                    _live.RemoveAt(i);
                    continue;
                }
                if (p.Gravity) p.Velocity.y -= _juice.gravity * dt;
                p.Position += p.Velocity * dt;
                float t = p.Age / p.Life;
                var c = p.Color;
                c.a *= 1f - t * t;
                p.Renderer.color = c;
                var tr = p.Renderer.transform;
                tr.localPosition = new Vector3(p.Position.x, p.Position.y, _juice.fxZ);
                // Coins spin (squash x); sparks shrink.
                if (p.Spin != 0f) tr.localScale = new Vector3(p.Size * Mathf.Abs(Mathf.Cos(p.Age * p.Spin * Mathf.Deg2Rad)), p.Size, 1f);
                else tr.localScale = Vector3.one * (p.Size * (1f - 0.6f * t));
                _live[i] = p;
            }

            float now = Time.unscaledTime;
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var p = _popups[i];
                float age = now - p.Born;
                if (age >= _juice.popupSeconds || p.Text == null)
                {
                    if (p.Text != null) Destroy(p.Text.gameObject);
                    _popups.RemoveAt(i);
                    continue;
                }
                float scale = JuiceMath.PopupScale(age, _juice.popupSeconds, _juice.popupPopSeconds, _juice.popupOvershoot);
                float rise = JuiceMath.PopupRise(age, _juice.popupSeconds, _juice.popupRise);
                p.Text.transform.localScale = Vector3.one * scale;
                p.Text.transform.localPosition = p.Origin + new Vector3(0f, rise, 0f);
                var c = p.Color;
                c.a = Mathf.Clamp01(scale * 1.5f);
                p.Text.color = c;
            }
        }
    }
}
