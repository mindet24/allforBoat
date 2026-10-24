using System;
using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Trouble.Game
{
    /// <summary>Which menu is open. None = the player is in a level with nothing over it.</summary>
    public enum ScreenId
    {
        None,
        Title,
        LevelSelect,
        End,
        HowTo,
        /// <summary>The pause overlay; the level stays loaded behind it.</summary>
        Pause,
    }

    /// <summary>The pure rules of the screen flow.</summary>
    public static class FlowRules
    {
        /// <summary>
        /// Where Esc leads from a screen: in a level it pauses, from the pause it resumes, How to Play and Level Select go
        /// back to the Title, the End to Level Select, the Title nowhere.
        /// </summary>
        public static ScreenId EscapeTarget(ScreenId current)
        {
            switch (current)
            {
                case ScreenId.None: return ScreenId.Pause;
                case ScreenId.Pause: return ScreenId.None;
                case ScreenId.End: return ScreenId.LevelSelect;
                case ScreenId.LevelSelect: return ScreenId.Title;
                case ScreenId.HowTo: return ScreenId.Title;
                default: return ScreenId.Title;
            }
        }

        /// <summary>True if the screen covers the whole view and the level is unloaded while it shows.</summary>
        public static bool IsFullScreen(ScreenId screen) => screen != ScreenId.None && screen != ScreenId.Pause;

        /// <summary>True if winning <paramref name="level"/> ends the game (there is no level after it).</summary>
        public static bool IsLastLevel(int level, Func<int, bool> exists) => !exists(level + 1);
    }

    /// <summary>
    /// Title → (How to Play) → the day → end-of-day panel → Retry or Main Menu; Esc pauses a level (Resume / Restart /
    /// Main Menu). The classic levels 1-4 stay reachable from the Title's "Classic levels" button with their unlock
    /// progress and End screen. Lives next to the <see cref="LevelController"/> and listens to its events.
    /// </summary>
    /// <remarks>
    /// Any code that loads a level directly (<c>controller.LoadLevel(n)</c>, the 1-9 keys) closes the menus,
    /// so quick tests that skip the Title still work. Pausing with Space also opens the pause overlay.
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

        /// <summary>The unlock progress and best scores (PlayerPrefs).</summary>
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
            var canvas = UiKit.Canvas(canvasGo, config, 30);

            _menus = new MenuScreens(config, TrippedUiConfig.LoadOrDefault(), new HudText(controller.Loader.Strings));
            _menus.Build(canvas, new MenuActions
            {
                Play = StartDemo,
                HowTo = ShowHowTo,
                Quit = Quit,
                Classic = ShowLevelSelect,
                BackToTitle = ShowTitle,
                EndLevels = ShowLevelSelect,
                PickLevel = StartLevel,
                Resume = Resume,
                Restart = RestartFromPause,
                MainMenu = ShowTitle,
            });

            controller.LevelLoaded += OnLevelLoaded;
            controller.TickEvents += OnTickEvents;
            controller.EscapePressed += OnEscapeInLevel;
        }

        /// <summary>Opens the Title screen (the level, if any, is removed).</summary>
        public void ShowTitle() => Show(ScreenId.Title);

        /// <summary>Opens How to Play.</summary>
        public void ShowHowTo() => Show(ScreenId.HowTo);

        /// <summary>Opens the classic Level Select with the current unlock progress.</summary>
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

        /// <summary>Pauses the running level and opens the pause overlay (also the HUD's pause button).</summary>
        public void Pause()
        {
            var sim = _controller.Sim;
            if (sim == null || sim.Phase == LevelPhase.Won || sim.Phase == LevelPhase.Lost) return;
            if (sim.Phase == LevelPhase.Running) _controller.StartOrTogglePause();
            SetPause(true);
        }

        /// <summary>Closes the pause overlay and lets the level run again.</summary>
        public void Resume()
        {
            var sim = _controller.Sim;
            if (sim != null && sim.Phase == LevelPhase.Paused) _controller.StartOrTogglePause();
            SetPause(false);
        }

        private void RestartFromPause()
        {
            SetPause(false);
            _controller.Restart();
        }

        private void SetPause(bool on)
        {
            Current = on ? ScreenId.Pause : ScreenId.None;
            _shownFrame = Time.frameCount;
            _menus.Pause.SetActive(on);
        }

        private void Show(ScreenId screen)
        {
            if (_controller.Sim != null) _controller.Unload();
            _endAt = -1f;
            Current = screen;
            _shownFrame = Time.frameCount;
            if (screen == ScreenId.LevelSelect) _menus.ShowCards(BuildCards());
            if (screen == ScreenId.End) _menus.SetEndTime(HudFormat.Clock(_progress.TotalBestTicks(LevelCount()), TicksPerSecond()));
            if (screen == ScreenId.Title) _menus.SetBestScore(_progress.BestScore(LevelLoader.DemoLevel));
            _menus.Title.SetActive(screen == ScreenId.Title);
            _menus.HowTo.SetActive(screen == ScreenId.HowTo);
            _menus.Select.SetActive(screen == ScreenId.LevelSelect);
            _menus.End.SetActive(screen == ScreenId.End);
            _menus.Pause.SetActive(false);
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
            _menus.HowTo.SetActive(false);
            _menus.Select.SetActive(false);
            _menus.End.SetActive(false);
            _menus.Pause.SetActive(false);
        }

        private void OnTickEvents(IReadOnlyList<SimEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                var type = events[i].Type;
                if (type != SimEventType.LevelWon && type != SimEventType.LevelLost) continue;
                int level = _controller.LevelIndex;
                if (level == LevelLoader.DemoLevel)
                {
                    // The day keeps its best score instead of an unlock chain.
                    if (_controller.Sim != null) _progress.RecordScore(level, _controller.Sim.Score);
                    continue;
                }
                if (type != SimEventType.LevelWon) continue;
                _progress.RecordWin(level, events[i].Tick);
                if (FlowRules.IsLastLevel(level, _controller.HasLevel)) _endAt = Time.unscaledTime + _config.endScreenDelay;
            }
        }

        /// <summary>Esc in a level: pause, resume, or (on the result panel) leave for the menu.</summary>
        private void OnEscapeInLevel()
        {
            if (_shownFrame == Time.frameCount) return;
            var sim = _controller.Sim;
            bool ended = sim != null && (sim.Phase == LevelPhase.Won || sim.Phase == LevelPhase.Lost);
            if (ended)
            {
                if (sim.IsDayMode) ShowTitle();
                else ShowLevelSelect();
                return;
            }
            if (FlowRules.EscapeTarget(Current) == ScreenId.Pause) Pause();
            else Resume();
        }

        private void Update()
        {
            if (_controller == null || _menus == null || _menus.Pause == null) return;
            if (_endAt >= 0f && Current == ScreenId.None && Time.unscaledTime >= _endAt) ShowEnd();

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && FlowRules.IsFullScreen(Current) && _shownFrame != Time.frameCount)
            {
                var target = FlowRules.EscapeTarget(Current);
                if (target != Current) Show(target);
            }

            // Space pauses and resumes the sim directly: keep the overlay in step with it.
            var sim = _controller.Sim;
            if (sim != null)
            {
                if (Current == ScreenId.None && sim.Phase == LevelPhase.Paused) SetPause(true);
                else if (Current == ScreenId.Pause && sim.Phase == LevelPhase.Running) SetPause(false);
            }
            else if (Current == ScreenId.Pause) SetPause(false);
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
