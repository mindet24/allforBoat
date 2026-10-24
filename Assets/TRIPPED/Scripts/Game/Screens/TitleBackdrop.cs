using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The "scene camera trick" behind the Title, How to Play, Settings and Credits: the demo house is loaded but
    /// not started (planning phase, keys suspended, HUD hidden), the camera shows the whole cutaway on the right side
    /// of the screen, and a sky gradient, grass and earth are drawn behind it in the world. <see cref="Exit"/> puts
    /// everything back before a real level starts.
    /// </summary>
    public sealed class TitleBackdrop : MonoBehaviour
    {
        private LevelController _controller;
        private TrippedViewConfig _config;
        private TrippedUiConfig _ui;
        private GameObject _root;
        private SpriteRenderer _sky;
        private Color _oldBackground;
        private Sprite _white;
        private Sprite _gradient;

        /// <summary>True while the house is shown behind the menus.</summary>
        public bool Active { get; private set; }

        public void Setup(LevelController controller, TrippedViewConfig config, TrippedUiConfig ui)
        {
            _controller = controller;
            _config = config;
            _ui = ui;
        }

        /// <summary>Turns the loaded demo house into the menu backdrop.</summary>
        public void Enter()
        {
            if (_controller == null || _controller.Sim == null) return;
            _controller.KeysSuspended = true;
            if (_controller.Hud != null) _controller.Hud.gameObject.SetActive(false);
            var cam = _controller.HouseCam;
            if (cam != null)
            {
                cam.ForceOverview = true;
                cam.AreaOverride = _ui.titleHouseArea;
                _oldBackground = cam.Cam.backgroundColor;
                cam.Cam.backgroundColor = _ui.backdropBottom;
            }
            BuildWorld();
            Active = true;
        }

        /// <summary>Back to normal play (or a plain menu).</summary>
        public void Exit()
        {
            if (!Active) return;
            Active = false;
            if (_controller != null)
            {
                _controller.KeysSuspended = false;
                var cam = _controller.HouseCam;
                if (cam != null)
                {
                    cam.ForceOverview = false;
                    cam.AreaOverride = null;
                    cam.Cam.backgroundColor = _oldBackground;
                }
            }
            if (_root != null) Destroy(_root);
            _root = null;
        }

        private void BuildWorld()
        {
            if (_root != null) Destroy(_root);
            _root = new GameObject("TitleBackdrop");
            var house = _controller.House;
            var b = house != null ? house.Bounds : new Rect(0, 0, 17, 12);
            float ground = HouseLayout.FloorY(1, _config.roomHeight);
            if (_white == null) _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            if (_gradient == null) _gradient = UiArt.Gradient(_ui.backdropTop, _ui.backdropBottom);
            var white = _white;
            // Earth behind the basement, grass on top of it: both behind the house (bigger z = farther away).
            Quad("Earth", white, _ui.dirt, new Rect(b.xMin - 60f, ground - 60f, b.width + 120f, 60f), 6f);
            Quad("Grass", white, _ui.grass, new Rect(b.xMin - 60f, ground - 0.15f, b.width + 120f, 0.45f), 5.9f);
            _sky = Quad("Sky", _gradient, Color.white, new Rect(0, 0, 1, 1), 30f);
        }

        private SpriteRenderer Quad(string name, Sprite sprite, Color color, Rect r, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = -200;
            var size = sprite.bounds.size;
            go.transform.position = new Vector3(r.center.x, r.center.y, z);
            go.transform.localScale = new Vector3(r.width / size.x, r.height / size.y, 1f);
            return sr;
        }

        private void LateUpdate()
        {
            if (!Active || _sky == null || _controller == null || _controller.HouseCam == null) return;
            // Keep the sky filling the view wherever the camera eases to.
            var cam = _controller.HouseCam;
            var c = cam.Cam;
            float h = cam.ViewHeight * 1.2f;
            var p = cam.Position;
            if (!c.orthographic)
            {
                // Perspective (maybe tilted): the sky sits behind the house, where the view is wider and higher.
                var ray = c.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                float along = (30f - ray.origin.z) / Mathf.Max(0.0001f, ray.direction.z);
                var centre = ray.GetPoint(along);
                p = new Vector2(centre.x, centre.y);
                h = 2f * along * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.2f;
            }
            float w = h * c.aspect;
            var size = _sky.sprite.bounds.size;
            _sky.transform.position = new Vector3(p.x, p.y, 30f);
            _sky.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root);
        }
    }
}
