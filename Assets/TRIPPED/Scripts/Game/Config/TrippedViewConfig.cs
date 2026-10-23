using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Every value the view layer uses that is not a game rule: colours, house geometry, camera, UI timing,
    /// art paths (GDD v1.1 §4.2). Game rules live in rules.json; this is look and feel only.
    /// </summary>
    /// <remarks>
    /// Loaded from Resources/Config/TrippedViewConfig if that asset exists, otherwise built with the defaults
    /// below. All colours are kept here so the circuit palette or the greybox can change in one place.
    /// Colours are GDD v1.0 §4.2 for now.
    /// </remarks>
    [CreateAssetMenu(menuName = "TRIPPED/View Config", fileName = "TrippedViewConfig")]
    public sealed class TrippedViewConfig : ScriptableObject
    {
        /// <summary>Resources path of the config asset.</summary>
        public const string ResourcePath = "Config/TrippedViewConfig";

        [Header("Start")]
        [Tooltip("Level loaded when the Main scene starts. Number keys 1-9 switch level in play.")]
        public int startLevel = 1;

        [Header("Circuit colours (data colours, GDD v1.0 §4.2) - A, B, C, D, ...")]
        public Color[] circuitColors =
        {
            Hex("#F2994A"), Hex("#2D9CDB"), Hex("#27AE60"), Hex("#9B51E0"),
        };
        /// <summary>A socket whose circuit is not known yet ("?").</summary>
        public Color unknownSocket = Hex("#9E9E9E");

        [Header("Load bar")]
        public Color loadGreen = Hex("#4CAF50");
        public Color loadAmber = Hex("#FFB300");
        public Color loadRed = Hex("#E53935");
        [Tooltip("Load / capacity above this is amber.")]
        public float loadAmberAbove = 0.7f;
        [Tooltip("Load / capacity above this is red.")]
        public float loadRedAbove = 1.0f;

        [Header("HUD")]
        public Color hudBackground = Hex("#1E1E24");
        public Color hudPanel = Hex("#2B2B33");
        public Color hudOutline = Hex("#44444F");
        public Color textPrimary = Hex("#F5F5F5");
        public Color textSecondary = Hex("#A0A0AA");
        public Color breakerOn = Hex("#4CAF50");
        public Color breakerOff = Hex("#5A2A2A");
        public Color selection = Hex("#FFE066");
        public Color requestDone = Hex("#4CAF50");
        public Color resultWon = Hex("#4CAF50");
        public Color resultLost = Hex("#E53935");
        public float toastSeconds = 3f;
        public int maxToasts = 3;
        public Vector2 referenceResolution = new Vector2(1920, 1080);
        [Range(0f, 1f)] public float matchWidthOrHeight = 0.5f;

        [Header("World colours (greybox)")]
        public Color sceneBackground = Hex("#1E1E24");
        public Color wallColor = Hex("#B9B9C2");
        public Color backWallColor = Hex("#A6A6B0");
        public Color floorColor = Hex("#7D7D88");
        public Color slabColor = Hex("#4A4A55");
        public Color doorColor = Hex("#6B5E52");
        public Color panelColor = Hex("#3B3550");
        public Color socketBody = Hex("#3B3550");
        [Tooltip("Rooms the player cannot see are multiplied by this.")]
        public Color hiddenRoomTint = new Color(0.32f, 0.32f, 0.38f, 1f);
        [Tooltip("Opacity of an appliance in a room that is not visible (last-seen ghost, v1.1 §2.3 V4).")]
        [Range(0f, 1f)] public float ghostAlpha = 0.3f;
        public Color applianceCard = Hex("#EFE3CF");
        public Color applianceOutline = Hex("#2B2B33");
        public Color applianceOffCard = Hex("#C9C2B6");
        /// <summary>Switched on and powered (v1.1 §3.4).</summary>
        public Color glowOn = Hex("#FFE066");
        /// <summary>Switched on but no power: blinks.</summary>
        public Color glowNoPower = Hex("#E53935");
        public Color characterColor = Hex("#EFE3CF");
        public Color worldText = Hex("#2B2B33");
        public Color roomNameText = Hex("#F5F5F5");

        [Header("House geometry (metres, GDD v1.1 §3.5)")]
        public float roomHeight = 3f;
        public float roomDepth = 4f;
        public float wallThickness = 0.2f;
        public float floorThickness = 0.3f;
        public float doorWidth = 1.2f;
        public float doorHeight = 2.2f;
        public float socketSize = 0.34f;
        public float socketHeight = 0.45f;
        [Tooltip("Distance of appliances in front of the back wall.")]
        public float applianceInset = 0.6f;
        [Tooltip("Distance of the character in front of the back wall. Small = it walks behind sockets and appliances, so they stay readable.")]
        public float characterInset = 0.2f;
        [Tooltip("How far socket blocks stick out of the back wall (they must stick out past the character).")]
        public float socketDepth = 0.3f;
        public float playerWorldHeight = 1.4f;
        public float animationFps = 8f;

        [Header("Camera (GDD v1.1 §2.6)")]
        public float fieldOfView = 20f;
        [Tooltip("Zoom per wheel notch.")]
        public float zoomStep = 0.15f;
        public float zoomSmoothTime = 0.15f;
        [Tooltip("Extra space around the whole house at the farthest zoom.")]
        public float homeMargin = 0.1f;
        [Tooltip("Closest zoom: a room of nearRoomWidth metres fills this much of the screen width.")]
        public float nearRoomWidth = 4f;
        public float nearRoomScreenFraction = 0.6f;
        [Tooltip("Keyboard pan speed, in screen widths per second.")]
        public float keyPanSpeed = 0.6f;
        [Tooltip("Part of the screen not covered by the HUD (viewport units, origin bottom-left).")]
        public Rect houseViewport = new Rect(400f / 1920f, 200f / 1080f, 1120f / 1920f, 784f / 1080f);

        [Header("Ticking")]
        public int maxStepsPerFrame = 5;

        [Header("Art (Resources paths; a file with the right name replaces the placeholder)")]
        public string applianceArtFolder = "Art/Appliances";
        public string characterArtFolder = "Art/Characters";
        public string faceArtFolder = "Art/Faces";
        public string roomArtFolder = "Art/Rooms";

        /// <summary>The colour of a circuit: A is the first entry, B the second, and so on. Unknown ids are grey.</summary>
        public Color CircuitColor(string circuitId)
        {
            if (string.IsNullOrEmpty(circuitId)) return unknownSocket;
            int index = char.ToUpperInvariant(circuitId[0]) - 'A';
            return index >= 0 && index < circuitColors.Length ? circuitColors[index] : unknownSocket;
        }

        /// <summary>Colour of the load bar for this band.</summary>
        public Color LoadColor(LoadBand band)
        {
            switch (band)
            {
                case LoadBand.Green: return loadGreen;
                case LoadBand.Amber: return loadAmber;
                default: return loadRed;
            }
        }

        /// <summary>Loads the config asset, or makes one with default values if there is none.</summary>
        public static TrippedViewConfig LoadOrDefault()
        {
            var asset = Resources.Load<TrippedViewConfig>(ResourcePath);
            return asset != null ? asset : CreateInstance<TrippedViewConfig>();
        }

        /// <summary>
        /// "#RRGGBB" or "#RRGGBBAA" to a colour. Bad text gives magenta so the mistake is visible.
        /// Pure C# (no engine call) because it runs in field initialisers.
        /// </summary>
        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            int start = hex[0] == '#' ? 1 : 0;
            int length = hex.Length - start;
            if (length != 6 && length != 8) return Color.magenta;
            var bytes = new float[4] { 1f, 1f, 1f, 1f };
            for (int i = 0; i < length / 2; i++)
            {
                int hi = HexDigit(hex[start + i * 2]);
                int lo = HexDigit(hex[start + i * 2 + 1]);
                if (hi < 0 || lo < 0) return Color.magenta;
                bytes[i] = (hi * 16 + lo) / 255f;
            }
            return new Color(bytes[0], bytes[1], bytes[2], bytes[3]);
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }
    }
}
