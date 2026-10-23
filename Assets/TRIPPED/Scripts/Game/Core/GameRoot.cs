using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Trouble.Game
{
    /// <summary>
    /// Boots TRIPPED! (GDD v1.0 §8.5): after the first scene loads, and only if it is the TRIPPED Main scene,
    /// creates the GameRoot object with the EventSystem (Input System UI module), the camera rig, the world
    /// pointer and the <see cref="LevelController"/>, then loads the start level. Nothing has to be placed in
    /// the scene by hand except a camera and a light. Other games' scenes in this project are left alone.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        /// <summary>The only scene this bootstrap acts in.</summary>
        public const string MainScenePath = "Assets/TRIPPED/Scenes/Main.unity";

        /// <summary>The level controller of this session.</summary>
        public LevelController Controller { get; private set; }

        /// <summary>Sound and first-time tips (G3).</summary>
        public FeedbackController Feedback { get; private set; }

        /// <summary>The screen flow: Title, Level Select, End and unlock progress (G3).</summary>
        public ScreenFlow Flow { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (SceneManager.GetActiveScene().path != MainScenePath) return;
            if (FindAnyObjectByType<GameRoot>() != null) return;
            new GameObject("GameRoot").AddComponent<GameRoot>();
        }

        private void Awake()
        {
            var config = TrippedViewConfig.LoadOrDefault();
            EnsureEventSystem();

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }
            var houseCamera = cam.GetComponent<HouseCamera>();
            if (houseCamera == null) houseCamera = cam.gameObject.AddComponent<HouseCamera>();
            houseCamera.Setup(config);

            Controller = gameObject.AddComponent<LevelController>();
            Controller.Setup(config, houseCamera);
            gameObject.AddComponent<WorldPointer>().Setup(Controller, cam);

            // G3: sound and tips, the screens (Title, Level Select, End) and, in dev builds, the F1 panel.
            Feedback = gameObject.AddComponent<FeedbackController>();
            Feedback.Setup(Controller, config);
            Flow = gameObject.AddComponent<ScreenFlow>();
            Flow.Setup(Controller, config, new Progress(new PlayerPrefsProgressStore()));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<TuningPanel>().Setup(Controller, Feedback, config);
#endif

            // A fresh Play opens the Title. The quick test flow goes straight into a level instead:
            // showTitle off in the view config, the -startLevel N argument, or the editor pref TRIPPED.skipTitle.
            int start = DevStartLevel(config);
            if (start > 0) Controller.LoadLevel(start);
            else Flow.ShowTitle();
        }

        /// <summary>The level to open at once, or 0 to show the Title.</summary>
        private static int DevStartLevel(TrippedViewConfig config)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-startLevel" && int.TryParse(args[i + 1], out int n) && LevelLoader.Exists(n)) return n;
            bool skip = !config.showTitle;
#if UNITY_EDITOR
            skip |= UnityEditor.EditorPrefs.GetBool("TRIPPED.skipTitle", false);
#endif
            if (!skip) return 0;
            return LevelLoader.Exists(config.startLevel) ? config.startLevel : 1;
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
