using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// The level HUD, built in code (GDD v1.0 §6.2 adapted to v1.1): a Screen Space Overlay canvas scaled from
    /// 1920×1080 with top bar, request panel, breaker panel, tray, toasts and result panel. Reads the sim every
    /// frame in LateUpdate; actions go back through the <see cref="LevelController"/>. One HUD per level load:
    /// destroy the object to remove it.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        private LevelController _controller;
        private HouseSim _sim;
        private readonly TopBar _topBar = new TopBar();
        private readonly BreakerPanel _breakers = new BreakerPanel();
        private readonly RequestPanel _requests = new RequestPanel();
        private readonly TrayView _tray = new TrayView();
        private readonly ToastView _toasts = new ToastView();
        private readonly ResultPanel _result = new ResultPanel();
        private readonly TimelineView _timeline = new TimelineView();
        private readonly AlertView _alerts = new AlertView();
        private readonly QueueStrip _queue = new QueueStrip();
        private TrippedViewConfig _config;
        private readonly DayTopBar _dayBar = new DayTopBar();
        private readonly DayRequestStack _dayStack = new DayRequestStack();
        private readonly DayMarkers _dayMarkers = new DayMarkers();
        private bool _day;

        /// <summary>Texts of the toasts on screen now, oldest first.</summary>
        public IEnumerable<string> VisibleToasts => _toasts.Visible;

        /// <summary>The result panel, for checks.</summary>
        public ResultPanel Result => _result;

        /// <summary>The alert layer, for checks.</summary>
        public AlertView Alerts => _alerts;

        /// <summary>The Day-mode top bar (hearts, score, banner), for checks. Not built in other levels.</summary>
        public DayTopBar DayBar => _dayBar;

        /// <summary>The Day-mode request cards, for checks.</summary>
        public DayRequestStack DayStack => _dayStack;

        /// <summary>The Day-mode markers over rooms, for checks.</summary>
        public DayMarkers DayRoomMarkers => _dayMarkers;

        /// <summary>The queue strip, for checks.</summary>
        public QueueStrip Queue => _queue;

        /// <summary>Builds the canvas and every panel for the controller's current sim.</summary>
        public void Build(LevelController controller, TrippedViewConfig config, HudText text, ArtLibrary art)
        {
            _controller = controller;
            _sim = controller.Sim;
            _config = config;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = config.referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = config.matchWidthOrHeight;
            gameObject.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)transform;

            _day = _sim.IsDayMode;
            if (_day)
            {
                // Day: hearts, score and time left on top, request cards on the right, markers over rooms, the key
                // hint in the tray. No queue strip, breaker cards, left request panel, timeline or alerts.
                _dayBar.Build(root, controller, config, text);
                _dayStack.Build(root, config, text);
                _tray.Build(root, controller, config, text, config.dayTrayHeight);
                // No queue strip: day mode is played live with the keyboard, nothing is queued.
                _dayMarkers.Build(root, controller, config);
                _toasts.Build(root, config, 300f);
                _result.Build(root, controller, config, text);
                LateUpdate();
                return;
            }
            _topBar.Build(root, controller, config, text);
            _requests.Build(root, controller, config, text, art);
            _breakers.Build(root, controller, config, text);
            _tray.Build(root, controller, config, text);
            _queue.Build(_tray.Root, controller, config, text);
            _timeline.Build(root, controller, config);
            _alerts.Build(root, controller, config, text);
            _toasts.Build(root, config);
            _result.Build(root, controller, config, text);
            LateUpdate();
        }

        /// <summary>Shows a short message at the top (refusals, trips, reveals).</summary>
        public void Toast(string message) => _toasts.Show(message, Time.unscaledTime);

        /// <summary>A lost save: "!" and the owner's line over the room for a few seconds.</summary>
        public void AddInterrupt(string owner, string roomId) => _alerts.AddInterrupt(owner, roomId, Time.unscaledTime);

        /// <summary>Shows the end-of-level panel.</summary>
        public void ShowResult(bool won, string cause, string detail, string time, int trips, bool hasNext, string stats = null, string title = null)
        {
            _result.Show(won, cause, detail, time, trips, hasNext, stats, title);
        }

        private void LateUpdate()
        {
            if (_sim == null || _controller == null || _controller.Sim != _sim) return;
            float time = Time.unscaledTime;
            if (_day)
            {
                _dayBar.Sync(_sim, time);
                _dayStack.Sync(_sim, time, Time.unscaledDeltaTime);
                _dayMarkers.Sync(_sim, _controller.HouseCam);
                _tray.Sync(_sim);
                _toasts.Sync(time);
                return;
            }
            _topBar.Sync(_sim);
            _breakers.Sync(_sim, time, Time.unscaledDeltaTime);
            _requests.Sync(_sim, time);
            _tray.Sync(_sim);
            _toasts.Sync(time);
            _queue.Sync(_sim);
            _timeline.Sync(_sim, _config);
            _alerts.Sync(_sim, _controller.HouseCam, time);
        }
    }
}
