using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>
    /// The timeline under the top bar (GDD v1.0 §6.2): an axis from 0 to the day length, a white tick with the
    /// owner's initial at every request deadline, a bar for every routine (grey until that routine's socket is
    /// revealed, then the circuit colour) and a needle at the current time.
    /// </summary>
    public sealed class TimelineView
    {
        private const float X = 400f, Y = 96f, W = 1120f, H = 80f;
        private const float Left = 24f, TrackW = W - 2f * Left;

        private sealed class RoutineBar
        {
            public RoutineState Routine;
            public Image Bar;
            public Color Shown;
        }

        private readonly List<RoutineBar> _bars = new List<RoutineBar>();
        private RectTransform _needle;
        private int _dayTicks;
        private int _shownNow = -1;

        /// <summary>Builds the axis, deadline ticks and routine bars for the controller's sim.</summary>
        public void Build(RectTransform canvas, LevelController controller, TrippedViewConfig config)
        {
            var sim = controller.Sim;
            _dayTicks = Mathf.Max(1, sim.DayLengthTicks);
            int tps = sim.Rules.ticksPerSecond;

            var bg = config.hudBackground;
            bg.a = 0.8f;
            var panel = UIFactory.Panel(canvas, "Timeline", X, Y, W, H, bg, false, false);
            var root = panel.rectTransform;

            // Axis with a label at both ends.
            UIFactory.Panel(root, "Axis", Left, 46, TrackW, 3, config.hudOutline, false, false);
            UIFactory.Label(root, "T0", Left - 6, 52, 80, 22, "0:00", 16, config.textSecondary);
            UIFactory.Label(root, "T1", Left + TrackW - 74, 52, 80, 22, HudFormat.Clock(_dayTicks, tps), 16, config.textSecondary, TextAlignmentOptions.MidlineRight);

            // Deadlines: white tick and the owner's initial above it.
            foreach (var r in sim.Requests)
            {
                float x = Left + AlertRules.TimelineFraction(r.DueTicks, _dayTicks) * TrackW;
                UIFactory.Panel(root, "Deadline " + r.Owner, x - 1.5f, 22, 3, 26, config.textPrimary, false, false);
                UIFactory.Label(root, "Who " + r.Owner, x - 14, 0, 28, 22, AlertRules.Initial(r.Owner), 18, config.textPrimary, TextAlignmentOptions.Center, true);
            }

            // Routines: a bar over the time the appliance runs. Grey until its socket is revealed.
            foreach (var r in sim.Routines)
            {
                float x0 = Left + AlertRules.TimelineFraction(r.StartTick, _dayTicks) * TrackW;
                float x1 = Left + AlertRules.TimelineFraction(r.EndTick, _dayTicks) * TrackW;
                var bar = UIFactory.Panel(root, "Routine " + r.Owner, x0, 28, Mathf.Max(4f, x1 - x0), 14, config.wireUnknown, false, false);
                UIFactory.Label(root, "RoutineWho " + r.Owner, x0 + 2, 26, 120, 16, r.Owner, 12, config.textPrimary);
                _bars.Add(new RoutineBar { Routine = r, Bar = bar, Shown = config.wireUnknown });
            }

            _needle = UIFactory.Panel(root, "Needle", Left - 1.5f, 6, 3, 66, config.glowOn, false, false).rectTransform;
        }

        /// <summary>Moves the needle and colours routine bars from what the player knows.</summary>
        public void Sync(HouseSim sim, TrippedViewConfig config)
        {
            if (sim.Now != _shownNow)
            {
                _shownNow = sim.Now;
                float x = Left + AlertRules.TimelineFraction(sim.Now, _dayTicks) * TrackW;
                _needle.anchoredPosition = new Vector2(x - 1.5f, -6f);
            }
            foreach (var b in _bars)
            {
                var socket = sim.FindSocket(b.Routine.SocketId);
                // Never colour a bar for a socket the player has not revealed.
                var want = socket != null && socket.Revealed ? config.CircuitColor(socket.CircuitId) : config.wireUnknown;
                if (want == b.Shown) continue;
                b.Shown = want;
                b.Bar.color = want;
            }
        }
    }
}
