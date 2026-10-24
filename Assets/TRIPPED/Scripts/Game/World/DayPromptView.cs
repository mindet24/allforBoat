using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Day mode key prompts in the house: a small "E" bubble over whatever E would act on now (the panel in a
    /// blackout, where what is carried goes down, or the appliance to pick up), an "F" bubble over an appliance F
    /// would plug in. What to show comes from <see cref="DayInteraction"/>, so a prompt appears exactly when the key works.
    /// </summary>
    public sealed class DayPromptView : MonoBehaviour
    {
        private TrippedViewConfig _config;
        private HouseView _house;
        private Bubble _interact;
        private Bubble _plug;

        private sealed class Bubble
        {
            public GameObject Root;
            public TextMeshPro Text;
        }

        /// <summary>What E would act on, as last shown (for checks).</summary>
        public DayTarget Shown { get; private set; } = DayTarget.None;

        /// <summary>Adds the world x, y rect of every prompt on screen now (HUD markers keep clear of them).</summary>
        public void CollectShownRects(List<Rect> into)
        {
            Add(_interact, _config.promptSize, into);
            Add(_plug, _config.promptSize, into);
        }

        private static void Add(Bubble bubble, Vector2 size, List<Rect> into)
        {
            if (bubble == null || !bubble.Root.activeSelf) return;
            var p = bubble.Root.transform.position;
            into.Add(new Rect(p.x - size.x * 0.5f, p.y - size.y * 0.5f, size.x, size.y));
        }

        /// <summary>Builds the (hidden) bubbles.</summary>
        public void Build(HouseView house, TrippedViewConfig config)
        {
            _house = house;
            _config = config;
            _interact = MakeBubble("PromptE", "E", config.promptSize);
            _plug = MakeBubble("PromptF", "F", config.promptSize);
        }

        /// <summary>Moves and shows the prompts for this frame.</summary>
        public void Sync(HouseSim sim)
        {
            bool live = sim.Phase == LevelPhase.Running || sim.Phase == LevelPhase.Paused;
            var target = live ? DayInteraction.InteractTarget(sim) : DayTarget.None;
            Shown = target;
            var at = target.Kind == DayTargetKind.None ? (Vector3?)null : PointOver(sim, target);
            Place(_interact, at);

            // F only when it would plug something in (unplugging is allowed but not advertised); beside E if they share a spot.
            var plug = live ? DayInteraction.PlugTarget(sim) : DayTarget.None;
            Vector3? plugAt = null;
            if (plug.Kind != DayTargetKind.None && plug.PlugIn)
            {
                var p = PointOver(sim, plug);
                if (at.HasValue && (at.Value - p).sqrMagnitude < _config.promptSize.x * _config.promptSize.x)
                    p.x = at.Value.x + _config.promptSize.x + _config.promptGap;
                plugAt = p;
            }
            Place(_plug, plugAt);
        }

        private Vector3 PointOver(HouseSim sim, DayTarget target)
        {
            var room = sim.FindRoom(target.RoomId);
            float floorY = room == null ? 0f : HouseLayout.FloorY(room.Floor, _config.roomHeight);
            float lift = _config.promptSize.y * 0.5f + 0.12f;
            switch (target.Kind)
            {
                case DayTargetKind.Panel:
                    // Beside the panel, not above it: the room's label and load bar hang just over it.
                    return new Vector3(target.X + _config.panelWorldHeight * 0.5f + _config.promptSize.x * 0.5f + 0.1f,
                        floorY + _config.panelMountHeight + _config.panelWorldHeight * 0.5f, _config.dayOverlayZ);
                case DayTargetKind.Floor:
                    return new Vector3(target.X, floorY + _config.playerWorldHeight * 0.5f + lift, _config.dayOverlayZ);
                case DayTargetKind.Socket:
                    return new Vector3(target.X, floorY + _config.socketHeight + _config.socketSize * 0.5f + lift, _config.dayOverlayZ);
                default:
                {
                    var view = _house != null ? _house.FindApplianceView(target.Id) : null;
                    if (view != null && view.gameObject.activeSelf)
                    {
                        var top = view.TopPoint;
                        return new Vector3(top.x, top.y + lift, _config.dayOverlayZ);
                    }
                    return new Vector3(target.X, floorY + 1f + lift, _config.dayOverlayZ);
                }
            }
        }

        private static void Place(Bubble bubble, Vector3? at)
        {
            bool show = at.HasValue;
            if (bubble.Root.activeSelf != show) bubble.Root.SetActive(show);
            if (show) bubble.Root.transform.localPosition = at.Value;
        }

        private Bubble MakeBubble(string name, string text, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var back = go.AddComponent<SpriteRenderer>();
            back.sprite = PlaceholderArt.WorldRounded;
            back.drawMode = SpriteDrawMode.Sliced;
            back.size = size;
            back.color = _config.promptBack;
            back.sortingOrder = 40;
            var label = Prims.Text(go.transform, "Key", new Vector3(0f, 0f, -0.01f), size * 0.85f, text, _config.promptText, 3f);
            label.sortingOrder = 41;
            go.SetActive(false);
            return new Bubble { Root = go, Text = label };
        }
    }
}
