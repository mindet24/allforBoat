using UnityEngine;
using UnityEngine.InputSystem;

namespace Trouble.Game
{
    /// <summary>
    /// Turns mouse clicks on the 3D house into <see cref="ClickInput"/>s for the <see cref="LevelController"/>.
    /// Uses a manual physics raycast against <see cref="ClickTarget"/> colliders; clicks over HUD graphics are
    /// ignored so the UI and the world never both react. Click meaning is decided by <see cref="ClickResolver"/>.
    /// </summary>
    public sealed class WorldPointer : MonoBehaviour
    {
        private const int MaxHits = 32;
        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private LevelController _controller;
        private Camera _camera;

        /// <summary>Connects the pointer to the controller and the camera it casts from.</summary>
        public void Setup(LevelController controller, Camera camera)
        {
            _controller = controller;
            _camera = camera;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || _controller == null || _camera == null || _controller.Sim == null) return;

            Vector2 pos = mouse.position.ReadValue();
            bool overUi = UiHit.IsOverUi(pos);
            var target = overUi ? null : Pick(pos);

            var house = _controller.House;
            if (house != null)
                {
                var held = _controller.Holding.Kind;
                bool carrying = held == HeldKind.Appliance || held == HeldKind.Cord;
                house.HighlightSocket(carrying && target != null && target.Kind == ClickKind.Socket ? target.Id : null);
            }

            bool left = mouse.leftButton.wasPressedThisFrame;
            bool right = mouse.rightButton.wasPressedThisFrame;
            if ((!left && !right) || overUi) return;

            var kb = Keyboard.current;
            bool shift = kb != null && kb.shiftKey.isPressed;
            _controller.HandleClick(Describe(target, right, shift));
        }

        /// <summary>The clickable target under a screen point: highest priority, then nearest.</summary>
        public ClickTarget Pick(Vector2 screenPos)
        {
            // Appliances move by transform; make sure their colliders are where they are drawn.
            Physics.SyncTransforms();
            var ray = _camera.ScreenPointToRay(screenPos);
            int count = Physics.RaycastNonAlloc(ray, _hits, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            ClickTarget best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var t = _hits[i].collider.GetComponent<ClickTarget>();
                if (t == null || !t.isActiveAndEnabled) continue;
                bool better = best == null || t.Priority > best.Priority
                    || (t.Priority == best.Priority && _hits[i].distance < bestDistance);
                if (!better) continue;
                best = t;
                bestDistance = _hits[i].distance;
            }
            return best;
        }

        /// <summary>Describes a click using only what the player can see (Seen* fields for appliances).</summary>
        private ClickInput Describe(ClickTarget target, bool right, bool shift)
        {
            var click = new ClickInput { Kind = ClickKind.Nothing, Right = right, Shift = shift };
            if (target == null) return click;
            var sim = _controller.Sim;
            click.Kind = target.Kind;
            click.Id = target.Id;
            click.RoomId = target.RoomId;

            switch (target.Kind)
            {
                case ClickKind.WorldAppliance:
                {
                    var app = sim.FindAppliance(target.Id);
                    click.SwitchedOn = app != null && app.SeenSwitchedOn;
                    break;
                }
                case ClickKind.Socket:
                {
                    var socketState = sim.FindSocket(target.Id);
                    click.SocketIsCord = socketState != null && socketState.IsCord;
                    // The appliance the player sees in this socket, which may be a last-seen ghost.
                    foreach (var app in sim.Appliances)
                    {
                        if (app.SeenSocketId != target.Id) continue;
                        click.OccupantId = app.Id;
                        click.OccupantFixed = app.Fixed;
                        break;
                    }
                    break;
                }
            }
            return click;
        }
    }
}
