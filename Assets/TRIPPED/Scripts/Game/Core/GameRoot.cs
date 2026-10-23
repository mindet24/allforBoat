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

            int start = LevelLoader.Exists(config.startLevel) ? config.startLevel : 1;
            Controller.LoadLevel(start);
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
