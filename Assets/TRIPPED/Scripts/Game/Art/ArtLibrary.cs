using System.Collections.Generic;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Finds real art by name under Resources (GDD v1.1 §3.6–3.8) and reports when there is none, so views fall
    /// back to placeholders. One library per level load: art added while the game runs shows after R.
    /// </summary>
    /// <remarks>
    /// Expected files (all under Assets/TRIPPED/Resources/):
    /// Art/Appliances/{catalog id}.png · Art/Characters/player_{idle|walk|interact|panic}.png (square frames side by
    /// side) · Art/Faces/face_{owner lower case}.png · Art/Rooms/{room theme}.png (back wall background).
    /// </remarks>
    public sealed class ArtLibrary
    {
        private readonly TrippedViewConfig _config;
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, Sprite[]> _strips = new Dictionary<string, Sprite[]>();
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private readonly List<Object> _created = new List<Object>();

        public ArtLibrary(TrippedViewConfig config)
        {
            _config = config;
        }

        /// <summary>The appliance sprite for a catalog id, or null to use the placeholder card.</summary>
        public Sprite Appliance(string type) => LoadSprite(_config.applianceArtFolder + "/" + type);

        /// <summary>A prop sprite by Resources path (for example the breaker panel, Art/Props/panel), or null.</summary>
        public Sprite Prop(string path) => string.IsNullOrEmpty(path) ? null : LoadSprite(path);

        /// <summary>The face icon of a family member, or null to use the placeholder circle.</summary>
        public Sprite Face(string owner) => string.IsNullOrEmpty(owner) ? null : LoadSprite(_config.faceArtFolder + "/face_" + owner.ToLowerInvariant());

        /// <summary>Background texture for a room theme, or null.</summary>
        public Texture2D RoomBackground(string theme)
        {
            if (string.IsNullOrEmpty(theme)) return null;
            string path = _config.roomArtFolder + "/" + theme;
            if (!_textures.TryGetValue(path, out var tex))
            {
                tex = Resources.Load<Texture2D>(path);
                _textures[path] = tex;
            }
            return tex;
        }

        /// <summary>
        /// Frames of a character strip ("idle", "walk", "interact", "panic"), sliced as square frames of the
        /// strip's height, pivot at the feet, 1 unit tall. Null if the file is missing.
        /// </summary>
        public Sprite[] CharacterFrames(string state)
        {
            // Called every frame by the character view: look the state up first so no path string is built (G7).
            if (state != null && _stripsByState.TryGetValue(state, out var known)) return known;
            var result = LoadStrip(state);
            if (state != null) _stripsByState[state] = result;
            return result;
        }

        private readonly Dictionary<string, Sprite[]> _stripsByState = new Dictionary<string, Sprite[]>();

        private Sprite[] LoadStrip(string state)
        {
            string path = _config.characterArtFolder + "/player_" + state;
            if (_strips.TryGetValue(path, out var frames)) return frames;
            frames = null;
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null && tex.height > 0 && tex.width >= tex.height)
            {
                int h = tex.height;
                int count = tex.width / h;
                frames = new Sprite[count];
                for (int i = 0; i < count; i++)
                {
                    frames[i] = Sprite.Create(tex, new Rect(i * h, 0, h, h), new Vector2(0.5f, 0f), h);
                    _created.Add(frames[i]);
                }
            }
            _strips[path] = frames;
            return frames;
        }

        /// <summary>Destroys sprites this library made. Loaded assets are left to Unity.</summary>
        public void Dispose()
        {
            foreach (var o in _created)
                if (o != null) Object.Destroy(o);
            _created.Clear();
        }

        private Sprite LoadSprite(string path)
        {
            if (_sprites.TryGetValue(path, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>(path);
            _sprites[path] = sprite;
            return sprite;
        }
    }
}
