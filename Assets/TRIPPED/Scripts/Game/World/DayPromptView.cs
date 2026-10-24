using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Day mode key prompts in the house: a small "E" bubble over whatever E would act on now (the panel in a
    /// blackout, the free socket for what is carried, or the appliance to pick up), and "W ↑" / "S ↓" at stairs in
    /// reach. What to show comes from <see cref="DayInteraction"/>, so a prompt appears exactly when the key works.
    /// </summary>
    public sealed class DayPromptView : MonoBehaviour
    {
        private const int MaxStairsPrompts = 2;
        private TrippedViewConfig _config;
        private HouseView _house;
        private Bubble _interact;
        private readonly Bubble[] _stairs = new Bubble[MaxStairsPrompts];
        private readonly List<StairsPrompt> _stairsInReach = new List<StairsPrompt>();

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
            for (int i = 0; i < MaxStairsPrompts; i++) Add(_stairs[i], _config.stairsPromptSize, into);
        }

        private static void Add(Bubble bubble, Vector2 size, List<Rect> into)
        {
            if (bubble == null || !bubble.Root.activeSelf) return;
            var p = bubble.Root.transform.position;
            into.Add(new Rect(p.x - size.x * 0.5f, p.y - size.y * 0.5f, size.x, size.y));
        }

        /// <summary>Number of stairs prompts on screen now (for checks).</summary>
        public int StairsShown { get; private set; }

        /// <summary>Builds the (hidden) bubbles.</summary>
        public void Build(HouseView house, TrippedViewConfig config)
        {
            _house = house;
            _config = config;
            _interact = MakeBubble("PromptE", "E", config.promptSize);
            for (int i = 0; i < MaxStairsPrompts; i++) _stairs[i] = MakeBubble("PromptStairs" + i, "", config.stairsPromptSize);
        }

        /// <summary>Moves and shows the prompts for this frame.</summary>
        public void Sync(HouseSim sim)
        {
            bool live = sim.Phase == LevelPhase.Running || sim.Phase == LevelPhase.Paused;
            var target = live ? DayInteraction.InteractTarget(sim) : DayTarget.None;
            Shown = target;
            var at = target.Kind == DayTargetKind.None ? (Vector3?)null : PointOver(sim, target);
            Place(_interact, at);

            _stairsInReach.Clear();
            if (live) DayInteraction.StairsInReach(sim, _stairsInReach);
            StairsShown = 0;
            var room = sim.FindRoom(sim.Character.RoomId);
            // The E prompt wins its spot; a stairs prompt that would touch it moves to the other side of the character.
            Rect? eRect = null;
            if (at.HasValue) eRect = new Rect(at.Value.x - _config.promptSize.x * 0.5f, at.Value.y - _config.promptSize.y * 0.5f, _config.promptSize.x, _config.promptSize.y);
            for (int i = 0; i < MaxStairsPrompts; i++)
            {
                if (i >= _stairsInReach.Count || room == null)
                {
                    Place(_stairs[i], null);
                    continue;
                }
                var p = _stairsInReach[i];
                bool up = p.Direction > 0;
                string text = up ? "W ↑" : "S ↓";
                if (_stairs[i].Text.text != text) _stairs[i].Text.text = text;
                float floorY = HouseLayout.FloorY(room.Floor, _config.roomHeight);
                // Beside the flight, right of the character: W at head height (below the room's label and load bar),
                // S lower, above the sockets.
                float x = p.X + _config.stairsHalfRun * 0.5f + 0.3f;
                float y = floorY + (up ? _config.playerWorldHeight + 0.15f : 0.95f);
                var c = PromptLayout.StairsCentre(new Vector2(x, y), _config.stairsPromptSize, eRect, sim.Character.X, _config.promptGap);
                Place(_stairs[i], new Vector3(c.x, c.y, _config.dayOverlayZ));
                StairsShown++;
            }
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
