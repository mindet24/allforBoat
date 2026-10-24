using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Trouble.Game
{
    /// <summary>
    /// The player as a cut-out rig: head, body, two arms and two legs from Art/Characters/Rig/player_*.png, which
    /// share one canvas, jointed at the points in player_rig.json. Poses come from <see cref="RigAnimator"/> with the
    /// numbers in <see cref="RigConfig"/>. The rig's origin is between the feet; it faces right and mirrors to face left.
    /// </summary>
    public sealed class RigView : MonoBehaviour
    {
        [Serializable]
        private sealed class RigPart
        {
            public string name = "";
            public float pivotX;
            public float pivotY;
            public int order;
        }

        [Serializable]
        private sealed class RigFile
        {
            public int canvas = 512;
            public int contentHeight = 1;
            public List<RigPart> parts = new List<RigPart>();
        }

        private RigConfig _config;
        private SortingGroup _group;
        private Transform _lean, _body, _head, _armL, _armR, _legL, _legR;
        private Vector3 _legLHome, _legRHome, _leanHome;
        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        private readonly List<Sprite> _created = new List<Sprite>();
        private RigAction _action;
        private float _actionStart;
        private float _metresPerPixel;

        /// <summary>Height of the hands above the feet when the arms point straight up, metres (where a carried item sits).</summary>
        public float ReachHeight { get; private set; }

        /// <summary>The one-off move playing now (None when it has finished).</summary>
        public RigAction Action => _action;

        /// <summary>
        /// Builds the rig under <paramref name="parent"/>, <paramref name="worldHeight"/> metres tall, drawn as one unit at
        /// <paramref name="sortingOrder"/>. Null (and nothing built) when the rig art or player_rig.json is missing.
        /// </summary>
        public static RigView TryCreate(Transform parent, RigConfig config, float worldHeight, int sortingOrder)
        {
            if (config == null || !config.useRig) return null;
            var json = Resources.Load<TextAsset>(config.rigFolder + "/player_rig");
            if (json == null) return null;
            var file = JsonUtility.FromJson<RigFile>(json.text);
            if (file == null || file.parts.Count == 0 || file.contentHeight <= 0) return null;

            var go = new GameObject("Rig");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<RigView>();
            if (!view.Build(file, config, worldHeight, sortingOrder))
            {
                Destroy(go);
                return null;
            }
            return view;
        }

        private bool Build(RigFile file, RigConfig config, float worldHeight, int sortingOrder)
        {
            _config = config;
            _metresPerPixel = worldHeight / file.contentHeight;
            _group = gameObject.AddComponent<SortingGroup>();
            _group.sortingOrder = sortingOrder;

            var joints = new Dictionary<string, Vector2>();
            var sprites = new Dictionary<string, (Sprite sprite, int order)>();
            foreach (var part in file.parts)
            {
                var tex = Resources.Load<Texture2D>(_config.rigFolder + "/player_" + part.name);
                if (tex == null) return false;
                // The texture may be imported at another size than the canvas: scale the pivot with it.
                float sx = tex.width / (float)file.canvas, sy = tex.height / (float)file.canvas;
                var pivot = new Vector2(part.pivotX * sx / tex.width, part.pivotY * sy / tex.height);
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, sy / _metresPerPixel);
                _created.Add(sprite);
                sprites[part.name] = (sprite, part.order);
                joints[part.name] = new Vector2((part.pivotX - file.canvas * 0.5f) * _metresPerPixel, part.pivotY * _metresPerPixel);
            }
            foreach (var need in new[] { "body", "head", "armL", "armR", "legL", "legR" })
                if (!joints.ContainsKey(need)) return false;

            _legL = Part("LegL", transform, joints["legL"], sprites["legL"]);
            _legR = Part("LegR", transform, joints["legR"], sprites["legR"]);
            _legLHome = _legL.localPosition;
            _legRHome = _legR.localPosition;
            _lean = new GameObject("Lean").transform;
            _lean.SetParent(transform, false);
            _lean.localPosition = joints["body"];
            _leanHome = _lean.localPosition;
            _body = Part("Body", _lean, Vector2.zero, sprites["body"]);
            _head = Part("Head", _body, joints["head"] - joints["body"], sprites["head"]);
            _armL = Part("ArmL", _body, joints["armL"] - joints["body"], sprites["armL"]);
            _armR = Part("ArmR", _body, joints["armR"] - joints["body"], sprites["armR"]);
            ReachHeight = joints["armL"].y + (joints["armL"].y - joints["body"].y) * 0.6f;
            return true;
        }

        private Transform Part(string name, Transform parent, Vector2 local, (Sprite sprite, int order) art)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art.sprite;
            sr.sortingOrder = art.order;
            _renderers.Add(sr);
            return go.transform;
        }

        /// <summary>Starts a one-off move (lift, put down, throw, plug); it replaces one already playing.</summary>
        public void Play(RigAction action)
        {
            _action = action;
            _actionStart = Time.unscaledTime;
        }

        /// <summary>Sorting order the whole rig is drawn at (its parts keep their order inside it).</summary>
        public int SortingOrder
        {
            get => _group.sortingOrder;
            set { if (_group.sortingOrder != value) _group.sortingOrder = value; }
        }

        /// <summary>Poses the rig for this frame.</summary>
        public void Sync(bool walking, bool climbing, bool flying, bool carrying, bool facingLeft, float time)
        {
            float actionT = 0f;
            if (_action != RigAction.None)
            {
                float d = Mathf.Max(0.01f, RigAnimator.Duration(_action, _config));
                actionT = (Time.unscaledTime - _actionStart) / d;
                if (actionT >= 1f)
                {
                    _action = RigAction.None;
                    actionT = 0f;
                }
            }
            var pose = RigAnimator.Evaluate(new RigState
            {
                Time = time,
                Walking = walking,
                Climbing = climbing,
                Flying = flying,
                Carrying = carrying,
                Action = _action,
                ActionT = actionT,
            }, _config);

            var s = transform.localScale;
            float wantX = facingLeft ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
            if (!Mathf.Approximately(s.x, wantX)) transform.localScale = new Vector3(wantX, s.y, s.z);

            _lean.localPosition = _leanHome + new Vector3(0f, pose.BodyY, 0f);
            _lean.localRotation = Quaternion.Euler(0f, 0f, -pose.Lean);
            _body.localScale = new Vector3(1f, pose.BodyScaleY, 1f);
            _head.localRotation = Quaternion.Euler(0f, 0f, pose.Head);
            // The left arm hangs down-left and lifts clockwise; the right arm mirrors it.
            _armL.localRotation = Quaternion.Euler(0f, 0f, -pose.ArmRaiseL);
            _armR.localRotation = Quaternion.Euler(0f, 0f, pose.ArmRaiseR);
            _legL.localRotation = Quaternion.Euler(0f, 0f, pose.LegL);
            _legR.localRotation = Quaternion.Euler(0f, 0f, pose.LegR);
            // A crouch bends the knees: legs stay on the floor, only lifted by a step.
            _legL.localPosition = _legLHome + new Vector3(0f, pose.LegLiftL, 0f);
            _legR.localPosition = _legRHome + new Vector3(0f, pose.LegLiftR, 0f);
        }

        /// <summary>Tints every part (for the Sight overlay or a fade). Alpha included.</summary>
        public void SetColor(Color c)
        {
            for (int i = 0; i < _renderers.Count; i++)
                if (_renderers[i].color != c) _renderers[i].color = c;
        }

        private void OnDestroy()
        {
            foreach (var s in _created)
                if (s != null) Destroy(s);
        }
    }
}
