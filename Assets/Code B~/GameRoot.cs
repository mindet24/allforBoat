using UnityEngine;
using UnityEngine.InputSystem;

namespace Trouble.Game
{
    public sealed class GameRoot : MonoBehaviour
    {
        public const int LevelCount = 5;
        public System.Action EscAction;

        Canvas canvas;
        RectTransform screenRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<GameRoot>() != null) return;
            new GameObject("GameRoot").AddComponent<GameRoot>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Theme.Bg; }
            canvas = UIFactory.CreateCanvas(transform);
            UIFactory.EnsureEventSystem(transform);
            ShowTitle();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && EscAction != null) EscAction();
        }

        // พื้นที่ออกแบบ 1920x1080 วางกลางจอ
        RectTransform NewScreen()
        {
            if (screenRoot != null) Destroy(screenRoot.gameObject);
            EscAction = null;
            var go = new GameObject("Screen", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1920, 1080);
            rt.anchoredPosition = Vector2.zero;
            screenRoot = rt;
            return rt;
        }

        public void ShowTitle() { TitleScreen.Build(this, NewScreen()); }
        public void ShowLevelSelect() { LevelSelectScreen.Build(this, NewScreen()); }
        public void ShowLevel(int n) { LevelScreen.Build(this, NewScreen(), n); }
        public void ShowEnd() { EndScreen.Build(this, NewScreen()); }
    }
}