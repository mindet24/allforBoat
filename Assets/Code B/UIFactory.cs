using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Trouble.Game
{
    public static class UIFactory
    {
        static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();

        // สี่เหลี่ยมมุมมน 9-slice สร้างจากโค้ด (ring > 0 = เฉพาะขอบ)
        public static Sprite Rounded(int radius, int ring = 0)
        {
            int key = radius * 100 + ring;
            if (sprites.TryGetValue(key, out var cached) && cached != null) return cached;

            int size = radius * 2 + 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float half = size * 0.5f, inner = half - radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - inner;
                float qy = Mathf.Abs(y + 0.5f - half) - inner;
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float a = Mathf.Clamp01(0.5f - d);
                if (ring > 0) a = Mathf.Clamp01(a - Mathf.Clamp01(0.5f - (d + ring)));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            float b = radius + 2;
            var sp = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprites[key] = sp;
            return sp;
        }

        // พิกัดแบบซ้ายบน (x ไปขวา, y ลงล่าง)
        public static RectTransform MakeRect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static void SetPos(RectTransform rt, float x, float y) { rt.anchoredPosition = new Vector2(x, -y); }

        public static Image Box(Transform parent, string name, float x, float y, float w, float h,
            Color color, int radius = 0, bool raycast = false)
        {
            var rt = MakeRect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            if (radius > 0) { img.sprite = Rounded(radius); img.type = Image.Type.Sliced; }
            return img;
        }

        public static Image Ring(Transform parent, string name, float x, float y, float w, float h,
            Color color, int radius, int thickness)
        {
            var rt = MakeRect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded(radius, thickness);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, float x, float y, float w, float h, string text,
            float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center,
            FontStyles style = FontStyles.Normal)
        {
            var rt = MakeRect(parent, "Label", x, y, w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color;
            t.alignment = align; t.fontStyle = style;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        public static Button MakeButton(Transform parent, float x, float y, float w, float h, string text,
            UnityAction onClick, float fontSize = 28)
        {
            var img = Box(parent, "Btn_" + text, x, y, w, h, Theme.Border, 12, true);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            Label(img.transform, 0, 0, w, h, text, fontSize, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            return b;
        }

        public static GameObject DashedBorder(Transform parent, float w, float h, Color c,
            float dash = 8f, float gap = 6f, float t = 3f)
        {
            var root = MakeRect(parent, "Dashed", 0, 0, w, h).gameObject;
            for (float x = 6f; x < w - 6f; x += dash + gap)
            {
                float len = Mathf.Min(dash, w - 6f - x);
                Box(root.transform, "d", x, 0, len, t, c);
                Box(root.transform, "d", x, h - t, len, t, c);
            }
            for (float y = 6f; y < h - 6f; y += dash + gap)
            {
                float len = Mathf.Min(dash, h - 6f - y);
                Box(root.transform, "d", 0, y, t, len, c);
                Box(root.transform, "d", w - t, y, t, len, c);
            }
            return root;
        }

        public static void Tint(GameObject g, Color c)
        {
            foreach (var img in g.GetComponentsInChildren<Image>()) img.color = c;
        }

        public static Canvas CreateCanvas(Transform parent)
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static void EnsureEventSystem(Transform parent)
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                go.transform.SetParent(parent, false);
                es = go.AddComponent<EventSystem>();
                var m = go.AddComponent<InputSystemUIInputModule>();
                m.AssignDefaultActions();
            }
            es.pixelDragThreshold = 12; // คลิก vs ลาก (GDD 6.4)
        }
    }
}