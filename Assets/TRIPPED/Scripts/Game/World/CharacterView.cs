using System.Text;
using TMPro;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The player character: a sprite standing on the floor, moved along the path the sim reports (ADR-0001).
    /// Uses player_{idle,walk,interact,panic}.png strips when they exist, else a placeholder capsule.
    /// The queued actions are listed above its head.
    /// </summary>
    public sealed class CharacterView : MonoBehaviour
    {
        private TrippedViewConfig _config;
        private ArtLibrary _art;
        private SpriteRenderer _sprite;
        private TextMeshPro _queueText;
        private SpriteRenderer _carryCard;
        private TextMeshPro _carryText;
        private string _shownCarry = "~";
        private float _lastX;
        private bool _facingLeft;
        private readonly System.Collections.Generic.List<Command> _shownCommands = new System.Collections.Generic.List<Command>();
        private readonly StringBuilder _sb = new StringBuilder();

        /// <summary>Builds the sprite and the queue label.</summary>
        public void Build(TrippedViewConfig config, ArtLibrary art)
        {
            _config = config;
            _art = art;
            var go = new GameObject("Sprite");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * config.playerWorldHeight;
            _sprite = go.AddComponent<SpriteRenderer>();
            _sprite.sprite = PlaceholderArt.Character;
            // Drawn before appliances and socket faces so gameplay objects stay readable in front of it.
            _sprite.sortingOrder = -1;
            _queueText = Prims.Text(transform, "Queue", new Vector3(0f, config.playerWorldHeight + 0.35f, 0f),
                new Vector2(2.4f, 0.6f), "", config.roomNameText, 1.6f, TextAlignmentOptions.Bottom, false);
            _queueText.textWrappingMode = TextWrappingModes.Normal;
            _queueText.sortingOrder = 6;

            // What the character carries (day mode): a small card above the head.
            var carry = new GameObject("Carrying");
            carry.transform.SetParent(transform, false);
            carry.transform.localPosition = new Vector3(0f, config.playerWorldHeight + 0.3f, -0.05f);
            _carryCard = carry.AddComponent<SpriteRenderer>();
            _carryCard.sprite = PlaceholderArt.WorldRounded;
            _carryCard.drawMode = SpriteDrawMode.Sliced;
            _carryCard.size = new Vector2(1.1f, 0.5f);
            _carryCard.color = config.glowOn;
            _carryCard.sortingOrder = 30;
            _carryText = Prims.Text(carry.transform, "Text", new Vector3(0f, 0f, -0.01f), new Vector2(1.0f, 0.4f), "", config.worldText, 3f);
            _carryText.sortingOrder = 31;
            carry.SetActive(false);
        }

        /// <summary>Moves and animates the character for this frame.</summary>
        /// <param name="tickFraction">How far the clock is into the next tick, 0..1, for smooth walking.</param>
        public void Sync(HouseSim sim, float tickFraction, float time)
        {
            var ch = sim.Character;
            var p = HouseLayout.CharacterPoint(sim, tickFraction, _config.roomHeight);
            transform.localPosition = new Vector3(p.x, p.y, _config.roomDepth - _config.characterInset);
            if (Mathf.Abs(p.x - _lastX) > 0.0005f) _facingLeft = p.x < _lastX;
            _lastX = p.x;

            string state = sim.Phase == LevelPhase.Lost ? "panic"
                : ch.IsWalking ? "walk"
                : ch.InteractTicksLeft > 0 ? "interact"
                : "idle";
            var frames = _art.CharacterFrames(state) ?? _art.CharacterFrames("idle");
            if (frames != null && frames.Length > 0)
            {
                int frame = (int)(time * _config.animationFps) % frames.Length;
                _sprite.sprite = frames[frame];
                _sprite.flipX = _facingLeft;
            }
            else
            {
                _sprite.sprite = PlaceholderArt.Character;
                _sprite.color = _config.characterColor;
                // A small bob while walking so the placeholder reads as moving.
                float bob = ch.IsWalking ? Mathf.Abs(Mathf.Sin(time * 12f)) * 0.06f : 0f;
                _sprite.transform.localPosition = new Vector3(0f, bob, 0f);
            }

            SyncCarry(sim);
            SyncQueue(sim);
        }

        private void SyncCarry(HouseSim sim)
        {
            string id = sim.Character.CarryingId ?? "";
            if (id == _shownCarry) return;
            _shownCarry = id;
            _carryCard.gameObject.SetActive(id.Length > 0);
            var lift = new Vector3(0f, _config.playerWorldHeight + (id.Length > 0 ? 0.95f : 0.35f), 0f);
            _queueText.transform.localPosition = lift;
            if (id.Length == 0) return;
            var app = sim.FindAppliance(id);
            var def = app == null ? null : sim.Catalog.Find(app.Type);
            _carryText.text = (def != null ? def.name : id) + " " + (app != null ? app.Load : 0);
        }

        private void SyncQueue(HouseSim sim)
        {
            var queue = sim.Character.Queue;
            if (!QueueChanged(queue)) return;
            _shownCommands.Clear();
            _sb.Clear();
            for (int i = 0; i < queue.Count; i++)
            {
                var c = queue[i].Command;
                _shownCommands.Add(c);
                if (i > 0) _sb.Append('\n');
                _sb.Append(i + 1).Append(". ").Append(HudFormat.ActionLabel(c, id => NameOf(sim, id), id => sim.FindRoom(id)?.Name ?? id));
            }
            _queueText.text = _sb.ToString();
        }

        private bool QueueChanged(System.Collections.Generic.IReadOnlyList<CharacterAction> queue)
        {
            if (queue.Count != _shownCommands.Count) return true;
            for (int i = 0; i < queue.Count; i++)
            {
                var a = queue[i].Command;
                var b = _shownCommands[i];
                if (a.Type != b.Type || a.A != b.A || a.B != b.B || a.On != b.On) return true;
            }
            return false;
        }

        private static string NameOf(HouseSim sim, string applianceId)
        {
            var app = sim.FindAppliance(applianceId);
            if (app == null) return applianceId;
            return sim.Catalog.Find(app.Type)?.name ?? app.Type;
        }
    }
}
