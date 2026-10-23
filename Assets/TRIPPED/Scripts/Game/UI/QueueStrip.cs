using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// The character's action queue as chips in the tray (GDD v1.1 §2.2): "1. Plug Kettle", "2. On Toaster".
    /// Right-click a chip to cancel that action (Command.CancelAction). Rebuilt only when the queue changes.
    /// </summary>
    public sealed class QueueStrip
    {
        private const float ChipW = 250f, ChipH = 56f, Gap = 12f;

        private readonly List<GameObject> _chips = new List<GameObject>();
        private readonly List<Command> _shown = new List<Command>();
        private RectTransform _root;
        private LevelController _controller;
        private TrippedViewConfig _config;

        /// <summary>Number of queue chips on screen (for checks).</summary>
        public int ChipCount => _chips.Count;

        /// <summary>Places the strip in the right half of the tray (which is 200 high, at the canvas bottom).</summary>
        public void Build(RectTransform tray, LevelController controller, TrippedViewConfig config, HudText text)
        {
            _controller = controller;
            _config = config;
            _root = UIFactory.Rect(tray, "Queue", 1000, 0, 900, 200);
            UIFactory.Label(_root, "Caption", 0, 6, 880, 34, text.Get("hud_queue_help"), 20, config.textSecondary);
        }

        public void Sync(HouseSim sim)
        {
            var queue = sim.Character.Queue;
            bool same = queue.Count == _shown.Count;
            for (int i = 0; same && i < queue.Count; i++)
            {
                var a = queue[i].Command;
                var b = _shown[i];
                same = a.Type == b.Type && a.A == b.A && a.B == b.B && a.On == b.On;
            }
            if (same) return;

            foreach (var c in _chips) Object.Destroy(c);
            _chips.Clear();
            _shown.Clear();
            float x = 0f;
            for (int i = 0; i < queue.Count; i++)
            {
                var cmd = queue[i].Command;
                _shown.Add(cmd);
                int index = i;
                string label = (i + 1) + ". " + HudFormat.ActionLabel(cmd, id => NameOf(sim, id), id => sim.FindRoom(id)?.Name ?? id);
                var img = UIFactory.Panel(_root, "Queued " + i, x, 45, ChipW, ChipH, _config.hudPanel);
                var outline = UIFactory.Panel(img.transform, "Outline", 0, 0, ChipW, ChipH, i == 0 ? _config.glowOn : _config.hudOutline, true, false);
                outline.sprite = PlaceholderArt.UiRoundedOutline;
                var text = UIFactory.LabelOn(UIFactory.Stretch(UIFactory.Rect(img.transform, "Text", 0, 0, ChipW, ChipH)), label, 22, _config.textPrimary,
                    TextAlignmentOptions.Center, true);
                text.enableAutoSizing = true;
                text.fontSizeMin = 14;
                text.fontSizeMax = 22;
                img.gameObject.AddComponent<RightClickHandler>().OnRight = () => _controller.Submit(Command.CancelAction(index));
                _chips.Add(img.gameObject);
                x += ChipW + Gap;
            }
        }

        private static string NameOf(HouseSim sim, string applianceId)
        {
            var app = sim.FindAppliance(applianceId);
            return app == null ? applianceId : sim.Catalog.Find(app.Type)?.name ?? app.Type;
        }
    }
}
