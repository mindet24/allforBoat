using System.Collections.Generic;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// One room of the greybox house: floor, back wall (with an optional background picture), ceiling, name and a
    /// click collider (click = walk here). Rooms the player cannot see are darkened (minimal G1 Sight).
    /// </summary>
    public sealed class RoomView : MonoBehaviour
    {
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Color> _colors = new List<Color>();
        private MaterialPropertyBlock _block;
        private RoomState _room;
        private TrippedViewConfig _config;
        private bool _shownVisible = true;
        private Material _backgroundMaterial;

        /// <summary>The sim room this view shows.</summary>
        public RoomState Room => _room;

        /// <summary>Builds the room's pieces under this object, which sits at world origin.</summary>
        public void Build(RoomState room, bool isPanelRoom, TrippedViewConfig config, ArtLibrary art)
        {
            _room = room;
            _config = config;
            _block = new MaterialPropertyBlock();
            var rect = HouseLayout.RoomRect(room, config.roomHeight);
            float depth = config.roomDepth;
            float floorT = config.floorThickness;
            float wallT = config.wallThickness;

            Add(Prims.Box(transform, "Floor", new Vector3(rect.center.x, rect.yMin - floorT * 0.5f, depth * 0.5f),
                new Vector3(rect.width, floorT, depth), config.floorColor), config.floorColor);
            Add(Prims.Box(transform, "BackWall", new Vector3(rect.center.x, rect.center.y, depth + wallT * 0.5f),
                new Vector3(rect.width, rect.height, wallT), config.backWallColor), config.backWallColor);
            Add(Prims.Box(transform, "Ceiling", new Vector3(rect.center.x, rect.yMax + floorT * 0.5f, depth * 0.5f),
                new Vector3(rect.width, floorT, depth), config.slabColor), config.slabColor);

            var background = art.RoomBackground(room.Theme);
            if (background != null)
            {
                var quad = Prims.TexturedQuad(transform, "Background", new Vector3(rect.center.x, rect.center.y, depth - 0.01f),
                    new Vector2(rect.width, rect.height), background);
                _backgroundMaterial = quad.sharedMaterial;
                Add(quad, Color.white);
            }

            if (isPanelRoom)
            {
                var panelColor = config.panelColor;
                Add(Prims.Box(transform, "Panel", new Vector3(rect.xMin + 0.7f, rect.yMin + 1.5f, depth - 0.08f),
                    new Vector3(0.55f, 0.75f, 0.16f), panelColor), panelColor);
                Prims.Text(transform, "PanelLabel", new Vector3(rect.xMin + 0.7f, rect.yMin + 1.5f, depth - 0.17f),
                    new Vector2(0.5f, 0.2f), "PANEL", config.roomNameText, 1.2f);
            }

            // Inset from the left wall so the wall's side face (seen in perspective) never covers the name.
            Prims.Text(transform, "Name", new Vector3(rect.xMin + 0.5f + rect.width * 0.25f, rect.yMax - 0.3f, depth - 0.02f),
                new Vector2(rect.width * 0.5f, 0.35f), room.Name, config.roomNameText, 3f, TMPro.TextAlignmentOptions.Left);

            // Click anywhere in the room volume to walk there. Appliances and sockets win over it (priority).
            ClickTarget.Add(gameObject, ClickKind.Room, room.Id, room.Id, 0,
                new Vector3(rect.center.x, rect.center.y, depth * 0.5f), new Vector3(rect.width, rect.height, depth));
        }

        /// <summary>Darkens or restores the room to match whether the player can see it.</summary>
        public void Sync()
        {
            if (_room == null || _room.Visible == _shownVisible) return;
            _shownVisible = _room.Visible;
            for (int i = 0; i < _renderers.Count; i++)
            {
                var c = _shownVisible ? _colors[i] : _colors[i] * _config.hiddenRoomTint;
                c.a = 1f;
                Prims.Tint(_renderers[i], c, _block);
            }
        }

        private void Add(Renderer r, Color baseColor)
        {
            _renderers.Add(r);
            _colors.Add(baseColor);
        }

        private void OnDestroy()
        {
            if (_backgroundMaterial != null) Destroy(_backgroundMaterial);
        }
    }
}
