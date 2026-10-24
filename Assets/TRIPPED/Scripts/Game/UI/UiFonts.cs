using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// The two G6 fonts, made into dynamic TMP font assets at runtime from the TTFs in Resources/Fonts:
    /// Lilita One (rounded display font for titles, buttons and numbers) and Patrick Hand (handwritten notes).
    /// Both are SIL Open Font License 1.1 (licence texts beside the fonts). Missing glyphs fall back to TMP's
    /// default font; a missing TTF falls back to the default font entirely.
    /// </summary>
    public static class UiFonts
    {
        private static TMP_FontAsset _display;
        private static TMP_FontAsset _hand;
        private static bool _tried;

        /// <summary>The display font, or null (use TMP's default).</summary>
        public static TMP_FontAsset Display { get { Load(); return _display; } }

        /// <summary>The handwritten font, or null.</summary>
        public static TMP_FontAsset Hand { get { Load(); return _hand; } }

        private static void Load()
        {
            if (_tried) return;
            _tried = true;
            var ui = TrippedUiConfig.LoadOrDefault();
            _display = Make(ui.displayFont);
            _hand = Make(ui.handFont);
        }

        private static TMP_FontAsset Make(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var font = Resources.Load<Font>(path);
            if (font == null) return null;
            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
            if (asset == null) return null;
            asset.name = font.name + " (dynamic)";
            asset.hideFlags = HideFlags.DontSave;
            var fallback = TMP_Settings.defaultFontAsset;
            if (fallback != null) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            return asset;
        }

        /// <summary>Gives a label the display font (keeps TMP's default if it is missing).</summary>
        public static TextMeshProUGUI AsDisplay(this TextMeshProUGUI t)
        {
            if (Display != null)
            {
                t.font = Display;
                t.fontStyle &= ~FontStyles.Bold;
            }
            return t;
        }

        /// <summary>Gives a label the handwritten font.</summary>
        public static TextMeshProUGUI AsHand(this TextMeshProUGUI t)
        {
            if (Hand != null)
            {
                t.font = Hand;
                t.fontStyle &= ~FontStyles.Bold;
            }
            return t;
        }

        /// <summary>A dark outline around the label (its own material instance). Use on a few big labels only.</summary>
        public static TextMeshProUGUI Outlined(this TextMeshProUGUI t, Color color, float width)
        {
            var mat = t.fontMaterial; // instance
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            t.outlineColor = color;
            t.outlineWidth = width;
            return t;
        }
    }
}
