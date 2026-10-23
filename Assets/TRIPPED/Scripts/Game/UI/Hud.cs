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

        /// <summary>Texts of the toasts on screen now, oldest first.</summary>
        public IEnumerable<string> VisibleToasts => _toasts.Visible;

        /// <summary>The result panel, for checks.</summary>
        public ResultPanel Result => _result;

        /// <summary>Builds the canvas and every panel for the controller's current sim.</summary>
        public void Build(LevelController controller, TrippedViewConfig config, HudText text, ArtLibrary art)
        {
            _controller = controller;
            _sim = controller.Sim;

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

            _topBar.Build(root, controller, config, text);
            _requests.Build(root, controller, config, text, art);
            _breakers.Build(root, controller, config, text);
            _tray.Build(root, controller, config, text);
            _toasts.Build(root, config);
            _result.Build(root, controller, config, text);
            LateUpdate();
        }

        /// <summary>Shows a short message at the top (refusals, trips, reveals).</summary>
        public void Toast(string message) => _toasts.Show(message, Time.unscaledTime);

        /// <summary>Shows the end-of-level panel.</summary>
        public void ShowResult(bool won, string cause, string detail, string time, int trips, bool hasNext)
        {
            _result.Show(won, cause, detail, time, trips, hasNext);
        }

        private void LateUpdate()
        {
            if (_sim == null || _controller == null || _controller.Sim != _sim) return;
            float time = Time.unscaledTime;
            _topBar.Sync(_sim);
            _breakers.Sync(_sim, time, Time.unscaledDeltaTime);
            _requests.Sync(_sim, time);
            _tray.Sync(_sim);
            _toasts.Sync(time);
        }
    }
}
