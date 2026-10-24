using System.Text;
using TMPro;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The player character standing on the floor, moved along the path the sim reports (ADR-0001). Drawn as the
    /// cut-out rig (<see cref="RigView"/>, Art/Characters/Rig) when its art is there: it walks, runs up stairs, carries
    /// overhead, and plays a lift, put-down, throw or plug move when that command is accepted. Otherwise the
    /// player_{idle,walk,interact,panic}.png strips, else a placeholder capsule.
    /// The queued actions are listed above its head.
    /// </summary>
    public sealed class CharacterView : MonoBehaviour
    {
        /// <summary>Day mode sorting order: above appliance sprites (0..3) and family (-2), below room overlays (10).</summary>
        private const int DaySortingOrder = 9;
        private TrippedViewConfig _config;
        private ArtLibrary _art;
        private SpriteRenderer _sprite;
        private TextMeshPro _queueText;
        private SpriteRenderer _carryCard;
        private TextMeshPro _carryText;
        private SpriteRenderer _carrySprite;
        private SpriteRenderer _flame;
        private RigView _rig;
        /// <summary>What was carried at the end of the last frame, to tell a pick-up from a put-down.</summary>
        private string _lastCarry = "";
        private string _shownCarry = "~";
        private Vector3 _shown3;
        private float _lastX;
        private bool _facingLeft;
        private readonly System.Collections.Generic.List<Command> _shownCommands = new System.Collections.Generic.List<Command>();
        private readonly StringBuilder _sb = new StringBuilder();

        /// <summary>
        /// World x, y rect from the character's shoulders to the top of what it carries: the part HUD markers must
        /// never cover.
        /// </summary>
        public Rect HeadWorldRect
        {
            get
            {
                var p = transform.position;
                float top = _config.playerWorldHeight + 0.1f + (_carrySprite != null && _carrySprite.gameObject.activeSelf ? _config.carryIconHeight : 0f);
                float bottom = _config.playerWorldHeight * 0.6f;
                return new Rect(p.x - 0.45f, p.y + bottom, 0.9f, top - bottom);
            }
        }

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
            // Other levels: drawn before appliances and socket faces so gameplay objects stay readable in front of it
            // (day mode draws it on top, see Sync).
            _sprite.sortingOrder = -1;
            _rig = RigView.TryCreate(transform, RigConfig.LoadOrDefault(), config.playerWorldHeight, -1);
            if (_rig != null) _sprite.enabled = false;
            _queueText = Prims.Text(transform, "Queue", new Vector3(0f, config.playerWorldHeight + 0.35f, 0f),
                new Vector2(2.4f, 0.6f), "", config.roomNameText, 1.6f, TextAlignmentOptions.Bottom, false);
            _queueText.textWrappingMode = TextWrappingModes.Normal;
            _queueText.sortingOrder = 6;

            // Day mode jetpack flame, under the feet, shown while Space is held.
            var flame = new GameObject("JetpackFlame");
            flame.transform.SetParent(transform, false);
            flame.transform.localPosition = new Vector3(0f, -0.05f, 0.02f);
            _flame = flame.AddComponent<SpriteRenderer>();
            _flame.sprite = PlaceholderArt.WorldRounded;
            _flame.drawMode = SpriteDrawMode.Sliced;
            _flame.size = config.jetpackFlameSize;
            _flame.color = config.jetpackFlameColor;
            _flame.sortingOrder = DaySortingOrder - 1;
            flame.SetActive(false);

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
            // Day mode: the carried appliance's own sprite above the head (the card is the fallback without art).
            var icon = new GameObject("CarriedSprite");
            icon.transform.SetParent(transform, false);
            // In front of the house (dayOverlayZ) so no wall, shelf or slab ever covers what is carried.
            icon.transform.localPosition = new Vector3(0f, config.playerWorldHeight + 0.05f,
                config.dayOverlayZ - (config.roomDepth - config.characterInset) + 0.05f);
            _carrySprite = icon.AddComponent<SpriteRenderer>();
            _carrySprite.sortingOrder = 30;
            icon.SetActive(false);
            carry.SetActive(false);
        }

        /// <summary>Moves and animates the character for this frame.</summary>
        /// <param name="tickFraction">How far the clock is into the next tick, 0..1, for smooth walking.</param>
        public void Sync(HouseSim sim, float tickFraction, float time)
        {
            var ch = sim.Character;
            var p = HouseLayout.CharacterPoint(sim, tickFraction, _config.roomHeight, _config.jumpArc);
            bool day = sim.IsDayMode;
            if (day)
            {
                // The sim moves in whole ticks; the point and depth are already glided between ticks
                // (HouseLayout.DayX / DayZ), so walking along, in and out moves every frame at a steady speed.
                _shown3 = new Vector3(p.x, p.y, _config.dayFrontZ + HouseLayout.DayZ(ch, tickFraction));
                p = _shown3;
                _facingLeft = ch.Facing < 0;
            }
            else
            {
                if (Mathf.Abs(p.x - _lastX) > 0.0005f) _facingLeft = p.x < _lastX;
            }
            // Day mode: at its depth on the walkable floor (W/S), from just in front of the furnished room models'
            // open front (z = 0) inward, so furniture it walks behind hides it.
            float z = day ? _shown3.z : _config.roomDepth - _config.characterInset;
            transform.localPosition = new Vector3(p.x, p.y, z);
            if (_carrySprite != null)
            {
                // Keep the carried sprite on the overlay plane whatever depth the character is at (position only:
                // the juice layer owns its rotation and scale).
                var lp = _carrySprite.transform.localPosition;
                float wantZ = _config.dayOverlayZ - z + 0.05f;
                if (!Mathf.Approximately(lp.z, wantZ)) _carrySprite.transform.localPosition = new Vector3(lp.x, lp.y, wantZ);
            }
            _lastX = p.x;
            // Day mode: the player is drawn over the appliance and family sprites standing at the same depth (but
            // under a dark room's overlay); other levels keep it behind gameplay objects.
            int order = day ? DaySortingOrder : -1;
            if (_sprite.sortingOrder != order) _sprite.sortingOrder = order;
            if (_rig != null) _rig.SortingOrder = order;

            bool walking = day ? ch.WalkDir != 0 && !ch.IsAirborne : ch.IsWalking;
            bool flame = day && ch.Thrusting && sim.Phase == LevelPhase.Running;
            if (_flame.gameObject.activeSelf != flame) _flame.gameObject.SetActive(flame);
            if (flame) _flame.transform.localScale = new Vector3(1f, 0.8f + 0.35f * Mathf.Abs(Mathf.Sin(time * 31f)), 1f);
            string state = sim.Phase == LevelPhase.Lost ? "panic"
                : walking ? "walk"
                : ch.InteractTicksLeft > 0 ? "interact"
                : "idle";
            var frames = _rig != null ? null : _art.CharacterFrames(state) ?? _art.CharacterFrames("idle");
            if (_rig != null)
            {
                bool climbing = day && ch.OnStairs && ch.WalkDir != 0;
                bool carrying = (ch.CarryingId ?? "").Length > 0;
                _rig.Sync(walking && !climbing, climbing, day && ch.IsAirborne, carrying, _facingLeft, time);
            }
            else if (frames != null && frames.Length > 0)
            {
                int frame = (int)(time * _config.animationFps) % frames.Length;
                _sprite.sprite = frames[frame];
                _sprite.flipX = _facingLeft;
                // One drawn frame (no walk strip): bob while walking so the art still reads as moving.
                float bob = walking && frames.Length == 1 ? Mathf.Abs(Mathf.Sin(time * 12f)) * 0.06f : 0f;
                _sprite.transform.localPosition = new Vector3(0f, bob, 0f);
            }
            else
            {
                _sprite.sprite = PlaceholderArt.Character;
                _sprite.color = _config.characterColor;
                _sprite.flipX = _facingLeft;
                // A small bob while walking so the placeholder reads as moving.
                float bob = walking ? Mathf.Abs(Mathf.Sin(time * 12f)) * 0.06f : 0f;
                _sprite.transform.localPosition = new Vector3(0f, bob, 0f);
            }

            SyncCarry(sim);
            SyncQueue(sim);
            _lastCarry = ch.CarryingId ?? "";
        }

        /// <summary>
        /// Plays the rig's one-off move for an accepted day-mode command: E that picked something up lifts it, E that
        /// put it down sets it down, hold-E throws, F reaches for the socket.
        /// </summary>
        public void OnCommandAccepted(Command command, HouseSim sim)
        {
            if (_rig == null || sim == null || !sim.IsDayMode) return;
            string now = sim.Character.CarryingId ?? "";
            switch (command.Type)
            {
                case CommandType.Throw:
                    _rig.Play(RigAction.Throw);
                    break;
                case CommandType.TogglePlug:
                    _rig.Play(RigAction.Plug);
                    break;
                case CommandType.Interact:
                    if (_lastCarry.Length == 0 && now.Length > 0) _rig.Play(RigAction.Lift);
                    else if (_lastCarry.Length > 0 && now.Length == 0) _rig.Play(RigAction.PutDown);
                    break;
            }
            _lastCarry = now;
        }

        private void SyncCarry(HouseSim sim)
        {
            string id = sim.Character.CarryingId ?? "";
            if (id == _shownCarry) return;
            _shownCarry = id;
            var app = id.Length > 0 ? sim.FindAppliance(id) : null;
            var def = app == null ? null : sim.Catalog.Find(app.Type);
            var sprite = app != null ? _art.Appliance(app.Type) : null;
            _carrySprite.gameObject.SetActive(sprite != null);
            _carryCard.gameObject.SetActive(id.Length > 0 && sprite == null);
            float top = sprite != null ? _config.carryIconHeight + 0.15f : 0.95f;
            var lift = new Vector3(0f, _config.playerWorldHeight + (id.Length > 0 ? top : 0.35f), 0f);
            _queueText.transform.localPosition = lift;
            if (id.Length == 0) return;
            if (sprite != null)
            {
                _carrySprite.sprite = sprite;
                float h = sprite.rect.height / Mathf.Max(1f, sprite.pixelsPerUnit);
                _carrySprite.transform.localScale = Vector3.one * (_config.carryIconHeight / Mathf.Max(0.01f, h));
                return;
            }
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
