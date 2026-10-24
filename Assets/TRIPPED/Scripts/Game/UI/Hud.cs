using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The level HUD, built in code on a Screen Space Overlay canvas scaled from 1920×1080. Reads the sim every frame
    /// in LateUpdate (after the camera has moved, so world-pinned pieces do not lag); actions go back through the
    /// <see cref="LevelController"/>. One HUD per level load: destroy the object to remove it.
    /// </summary>
    /// <remarks>
    /// Day mode (G6, Overcooked style): order tickets top-left, hearts and pause top-right, coin score and current
    /// room meter bottom-left, timer bottom-right, carry slot and key hints bottom-centre, a power meter over every
    /// room, request pins with edge arrows, the blackout banner, and the end-of-day panel. It also tells the camera
    /// to follow the character. Classic levels keep the G1-G3 panels.
    /// </remarks>
    [DefaultExecutionOrder(100)]
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
        private TrippedUiConfig _ui;
        private readonly OrderTickets _tickets = new OrderTickets();
        private readonly DayStatus _status = new DayStatus();
        private readonly RoomMeters _meters = new RoomMeters();
        private readonly RequestPins _pins = new RequestPins();
        private bool _day;
        private readonly List<Rect> _hudRects = new List<Rect>();

        /// <summary>Texts of the toasts on screen now, oldest first.</summary>
        public IEnumerable<string> VisibleToasts => _toasts.Visible;

        /// <summary>The result panel, for checks.</summary>
        public ResultPanel Result => _result;

        /// <summary>The alert layer, for checks.</summary>
        public AlertView Alerts => _alerts;

        /// <summary>Day mode: the order tickets, for checks.</summary>
        public OrderTickets Tickets => _tickets;

        /// <summary>Day mode: hearts, score, timer, carry, readout and banner, for checks.</summary>
        public DayStatus Status => _status;

        /// <summary>Day mode: the per-room power meters, for checks.</summary>
        public RoomMeters Meters => _meters;

        /// <summary>Day mode: request pins and edge arrows, for checks.</summary>
        public RequestPins Pins => _pins;

        /// <summary>The queue strip, for checks.</summary>
        public QueueStrip Queue => _queue;

        /// <summary>Builds the canvas and every panel for the controller's current sim.</summary>
        public void Build(LevelController controller, TrippedViewConfig config, HudText text, ArtLibrary art)
        {
            _controller = controller;
            _sim = controller.Sim;
            _config = config;
            _ui = TrippedUiConfig.LoadOrDefault();
            UiArt.Configure(_ui);
            var root = UiKit.Canvas(gameObject, config, 10);
            var flow = controller.GetComponent<ScreenFlow>();

            _day = _sim.IsDayMode;
            if (_day)
            {
                var cx = new DayHudContext { Controller = controller, Config = config, Ui = _ui, Text = text, Art = art, Canvas = root };
                _meters.Build(cx, _sim);
                _pins.Build(cx);
                _tickets.Build(cx);
                _status.Build(cx, _sim, () => { if (flow != null) flow.Pause(); });
                _toasts.Build(root, config, _ui.dayToastTop);
                _result.Build(root, controller, config, text, () => { if (flow != null) flow.ShowTitle(); });
                if (controller.HouseCam != null) controller.HouseCam.FollowDay(_sim);
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
            _result.Build(root, controller, config, text, () => { if (flow != null) flow.ShowLevelSelect(); });
            LateUpdate();
        }

        /// <summary>Shows a short message at the top (refusals, trips, reveals).</summary>
        public void Toast(string message) => _toasts.Show(message, Time.unscaledTime);

        /// <summary>A lost save: "!" and the owner's line over the room for a few seconds.</summary>
        public void AddInterrupt(string owner, string roomId) => _alerts.AddInterrupt(owner, roomId, Time.unscaledTime);

        /// <summary>Shows the end-of-level panel. Day mode builds its own numbers from the sim (stars, best score).</summary>
        public void ShowResult(bool won, string cause, string detail, string time, int trips, bool hasNext, string stats = null, string title = null)
        {
            if (_day && _sim != null)
            {
                var flow = _controller != null ? _controller.GetComponent<ScreenFlow>() : null;
                int previous = flow != null && flow.Progress != null ? flow.Progress.BestScore(_controller.LevelIndex) : 0;
                int stars = StarRules.Stars(_sim.Score, _ui.starScores, won);
                bool newBest = _sim.Score > previous && _sim.Score > 0;
                _result.ShowDay(won, _sim.Score, _sim.RequestsDone, _sim.RequestsMissed, stars, Mathf.Max(previous, _sim.Score), newBest);
                return;
            }
            _result.Show(won, cause, detail, time, trips, hasNext, stats, title);
        }

        private void LateUpdate()
        {
            if (_sim == null || _controller == null || _controller.Sim != _sim) return;
            float time = Time.unscaledTime;
            if (_day)
            {
                _hudRects.Clear();
                _status.CollectHudRects(_hudRects);
                _meters.Sync(_sim, time, _hudRects);
                _pins.Sync(_sim, time);
                _tickets.Sync(_sim, time, Time.unscaledDeltaTime);
                _status.Sync(_sim, time);
                _toasts.Sync(time);
                _result.Sync(time);
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

        private void OnDestroy()
        {
            if (_controller != null && _controller.HouseCam != null && _sim != null) _controller.HouseCam.StopDay(_sim);
        }
    }
}
