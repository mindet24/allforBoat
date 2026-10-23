using System;
using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>Which full-screen menu is open. None = the player is in a level.</summary>
    public enum ScreenId
    {
        None,
        Title,
        LevelSelect,
        End,
    }

    /// <summary>The pure rules of the screen flow (GDD v1.0 §6.1).</summary>
    public static class FlowRules
    {
        /// <summary>Where Esc leads from a screen: a level and the End go to Level Select, Level Select to Title, Title nowhere.</summary>
        public static ScreenId EscapeTarget(ScreenId current)
        {
            switch (current)
            {
                case ScreenId.None: return ScreenId.LevelSelect;
                case ScreenId.End: return ScreenId.LevelSelect;
                case ScreenId.LevelSelect: return ScreenId.Title;
                default: return ScreenId.Title;
            }
        }

        /// <summary>True if winning <paramref name="level"/> ends the game (there is no level after it).</summary>
        public static bool IsLastLevel(int level, Func<int, bool> exists) => !exists(level + 1);
    }

    /// <summary>
    /// Title → Level Select → level → Result → next level or End (GDD v1.0 §6.1), plus the unlock progress.
    /// Lives next to the <see cref="LevelController"/> and listens to its events; the controller does not know it.
    /// </summary>
    /// <remarks>
    /// Any code that loads a level directly (<c>controller.LoadLevel(n)</c>, the 1-9 keys) closes the menus,
    /// so quick tests that skip the Title still work.
    /// </remarks>
    public sealed class ScreenFlow : MonoBehaviour
    {
        private LevelController _controller;
        private TrippedViewConfig _config;
        private MenuScreens _menus;
        private Progress _progress;
        private int _shownFrame = -1;
        private float _endAt = -1f;

        /// <summary>The menu that is open now.</summary>
        public ScreenId Current { get; private set; }

        /// <summary>The unlock progress (PlayerPrefs).</summary>
        public Progress Progress => _progress;

        /// <summary>The menu screens, for checks.</summary>
        public MenuScreens Menus => _menus;

        /// <summary>Builds the menu canvas. Call once after the controller is set up.</summary>
        public void Setup(LevelController controller, TrippedViewConfig config, Progress progress)
        {
            _controller = controller;
            _config = config;
            _progress = progress;

            var canvasGo = new GameObject("Menus", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = config.referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = config.matchWidthOrHeight;
            canvasGo.AddComponent<GraphicRaycaster>();

            _menus = new MenuScreens(config, new HudText(controller.Loader.Strings));
            _menus.Build((RectTransform)canvasGo.transform, StartDemo, Quit, ShowTitle, ShowLevelSelect, ShowLevelSelect, StartLevel, ShowLevelSelect);

            controller.LevelLoaded += OnLevelLoaded;
            controller.TickEvents += OnTickEvents;
            controller.EscapePressed += OnEscapeInLevel;
        }

        /// <summary>Opens the Title screen (the level, if any, is removed).</summary>
        public void ShowTitle() => Show(ScreenId.Title);

        /// <summary>Opens the Level Select with the current unlock progress.</summary>
        public void ShowLevelSelect() => Show(ScreenId.LevelSelect);

        /// <summary>Opens the End screen with the total of the best times.</summary>
        public void ShowEnd() => Show(ScreenId.End);

        /// <summary>The main entry: starts the Day-mode demo "One Busy Day".</summary>
        public void StartDemo()
        {
            if (_controller.HasLevel(LevelLoader.DemoLevel)) _controller.LoadLevel(LevelLoader.DemoLevel);
            else ShowLevelSelect();
        }

        /// <summary>Starts level n if it exists and is unlocked.</summary>
        public void StartLevel(int n)
        {
            if (!_controller.HasLevel(n) || !_progress.IsUnlocked(n)) return;
            _controller.LoadLevel(n);
        }

        private void Show(ScreenId screen)
        {
            if (_controller.Sim != null) _controller.Unload();
            _endAt = -1f;
            Current = screen;
            _shownFrame = Time.frameCount;
            if (screen == ScreenId.LevelSelect) _menus.ShowCards(BuildCards());
            if (screen == ScreenId.End) _menus.SetEndTime(HudFormat.Clock(_progress.TotalBestTicks(LevelCount()), TicksPerSecond()));
            _menus.Title.SetActive(screen == ScreenId.Title);
            _menus.Select.SetActive(screen == ScreenId.LevelSelect);
            _menus.End.SetActive(screen == ScreenId.End);
            _menus.ResultLevels.SetActive(false);
        }

        private List<LevelCard> BuildCards()
        {
            var cards = new List<LevelCard>();
            int tps = TicksPerSecond();
            for (int n = 1; _controller.HasLevel(n); n++)
            {
                var data = LevelLoader.ReadLevel(n);
                int best = _progress.BestTicks(n);
                cards.Add(new LevelCard
                {
                    Number = n,
                    Title = data != null ? data.title : "",
                    Teaches = data != null ? data.teaches : "",
                    Locked = !_progress.IsUnlocked(n),
                    Best = best >= 0 ? HudFormat.Clock(best, tps) : "",
                });
            }
            return cards;
        }

        private int LevelCount()
        {
            int n = 0;
            while (_controller.HasLevel(n + 1)) n++;
            return n;
        }

        private int TicksPerSecond() => Mathf.Max(1, _controller.Loader.Rules.ticksPerSecond);

        private void OnLevelLoaded(int level)
        {
            Current = ScreenId.None;
            _endAt = -1f;
            _menus.Title.SetActive(false);
            _menus.Select.SetActive(false);
            _menus.End.SetActive(false);
            _menus.ResultLevels.SetActive(false);
        }

        private void OnTickEvents(IReadOnlyList<SimEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type != SimEventType.LevelWon) continue;
                int level = _controller.LevelIndex;
                if (level == LevelLoader.DemoLevel) continue; // the day has no unlock chain or best time
                _progress.RecordWin(level, events[i].Tick);
                if (FlowRules.IsLastLevel(level, _controller.HasLevel)) _endAt = Time.unscaledTime + _config.endScreenDelay;
            }
        }

        private void OnEscapeInLevel() => Show(FlowRules.EscapeTarget(ScreenId.None));

        private void Update()
        {
            // Not set up yet (or already torn down): nothing to drive.
            if (_controller == null || _menus == null || _menus.ResultLevels == null) return;
            if (_endAt >= 0f && Current == ScreenId.None && Time.unscaledTime >= _endAt) ShowEnd();

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && Current != ScreenId.None && _shownFrame != Time.frameCount)
            {
                var target = FlowRules.EscapeTarget(Current);
                if (target != Current) Show(target);
            }

            bool resultShown = Current == ScreenId.None && _controller.Hud != null && _controller.Hud.Result != null && _controller.Hud.Result.IsShown;
            if (_menus.ResultLevels.activeSelf != resultShown) _menus.ResultLevels.SetActive(resultShown);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (_controller == null) return;
            _controller.LevelLoaded -= OnLevelLoaded;
            _controller.TickEvents -= OnTickEvents;
            _controller.EscapePressed -= OnEscapeInLevel;
        }
    }
}
