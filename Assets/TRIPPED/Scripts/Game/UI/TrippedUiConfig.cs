using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Look, layout and timing of the Overcooked-style HUD and the menus (G6). Every UI number lives here so it can be
    /// tuned without code. Loaded from Resources/Config/TrippedUiConfig if that asset exists, otherwise built with the
    /// defaults below. Positions and sizes are canvas reference pixels (1920x1080).
    /// </summary>
    [CreateAssetMenu(menuName = "TRIPPED/UI Config", fileName = "TrippedUiConfig")]
    public sealed class TrippedUiConfig : ScriptableObject
    {
        /// <summary>Resources path of the config asset.</summary>
        public const string ResourcePath = "Config/TrippedUiConfig";

        [Header("Palette (art sheet: yellow, sky, green, purple, red, navy, tan, charcoal)")]
        public Color yellow = TrippedViewConfig.Hex("#F6C933");
        public Color sky = TrippedViewConfig.Hex("#3FA2F6");
        public Color green = TrippedViewConfig.Hex("#7DBE4E");
        public Color purple = TrippedViewConfig.Hex("#8E5BD9");
        public Color red = TrippedViewConfig.Hex("#E8423F");
        public Color navy = TrippedViewConfig.Hex("#1F2A44");
        public Color navyLight = TrippedViewConfig.Hex("#2E3D5E");
        public Color tan = TrippedViewConfig.Hex("#D6B98C");
        public Color ink = TrippedViewConfig.Hex("#2F3640");
        [Tooltip("Light card colour (request bubbles, how-to cards, settings rows).")]
        public Color cream = TrippedViewConfig.Hex("#FFF9EE");
        public Color creamDark = TrippedViewConfig.Hex("#F0E2C8");
        [Tooltip("Dark text and dark panels (navy).")]
        public Color charcoal = TrippedViewConfig.Hex("#1F2A44");
        public Color charcoalSoft = TrippedViewConfig.Hex("#56627F");
        public Color shadow = new Color(0.06f, 0.08f, 0.16f, 0.32f);
        public Color accent = TrippedViewConfig.Hex("#F6C933");
        public Color good = TrippedViewConfig.Hex("#5DB447");
        public Color bad = TrippedViewConfig.Hex("#E8423F");
        public Color coin = TrippedViewConfig.Hex("#F6C933");
        public Color coinRim = TrippedViewConfig.Hex("#D9901A");
        [Tooltip("Menu sky gradient (top, bottom).")]
        public Color backdropTop = TrippedViewConfig.Hex("#3A8FE0");
        public Color backdropBottom = TrippedViewConfig.Hex("#BFE6FB");
        public Color grass = TrippedViewConfig.Hex("#6DB24A");
        public Color dirt = TrippedViewConfig.Hex("#7A4A2A");
        public Color dim = new Color(0.06f, 0.08f, 0.16f, 0.72f);
        public Color segmentEmpty = TrippedViewConfig.Hex("#46557A");
        public Color meterBack = new Color(0.12f, 0.16f, 0.27f, 0.94f);
        public Color running = TrippedViewConfig.Hex("#3FA2F6");
        [Tooltip("Navy HUD panels (slightly see-through).")]
        public Color panel = new Color(0.12f, 0.16f, 0.27f, 0.95f);
        public Color iconOutline = TrippedViewConfig.Hex("#1F2A44");

        [Header("Family colours (placeholder portraits until Art/Faces/face_{name}.png exist)")]
        public string[] personNames = { "Mom", "Dad", "Daughter", "Grandma" };
        public Color[] personColors =
        {
            TrippedViewConfig.Hex("#E85D75"), TrippedViewConfig.Hex("#3FA2F6"), TrippedViewConfig.Hex("#8E5BD9"), TrippedViewConfig.Hex("#7DBE4E"),
        };
        public Color personFallback = TrippedViewConfig.Hex("#D6B98C");
        [Tooltip("Resources folder of face portraits, named face_mom.png, face_dad.png, face_daughter.png, face_grandma.png.")]
        public string faceFolder = "Art/Faces";

        [Header("Fonts (OFL, Resources/Fonts)")]
        public string displayFont = "Fonts/LilitaOne-Regular";
        public string handFont = "Fonts/PatrickHand-Regular";
        [Tooltip("Outline width of the display font on the logo and big titles (TMP units).")]
        public float logoOutline = 0.28f;
        [Tooltip("Corner radius of panels and buttons, in sprite pixels (the sprite is 128 px).")]
        public int cornerRadius = 30;
        public Vector2 shadowOffset = new Vector2(0f, -7f);

        [Header("Buttons")]
        public float buttonHoverScale = 1.06f;
        public float buttonPressScale = 0.94f;
        public float buttonAnimSpeed = 18f;
        public Color buttonPrimary = TrippedViewConfig.Hex("#F6C933");
        public Color buttonPrimaryText = TrippedViewConfig.Hex("#1F2A44");
        public Color buttonSecondary = TrippedViewConfig.Hex("#25324F");
        public Color buttonSecondaryText = Color.white;
        public Color buttonFocusRing = TrippedViewConfig.Hex("#FFFFFF");

        [Header("Order tickets (top-left)")]
        public Vector2 ticketOrigin = new Vector2(20f, 18f);
        public Vector2 ticketSize = new Vector2(352f, 132f);
        public float ticketGap = 12f;
        public int ticketsPerRow = 4;
        public float ticketIconSize = 84f;
        public float portraitSize = 96f;
        public Vector2 clockPillSize = new Vector2(128f, 40f);
        public float ticketHeaderHeight = 40f;
        public float ticketSlideSeconds = 0.35f;
        public float ticketSlideDistance = 260f;
        [Tooltip("A waiting ticket shakes when this many seconds or fewer are left.")]
        public float ticketShakeBelowSeconds = 5f;
        public float ticketShakeAmplitude = 5f;
        public float ticketShakeFrequency = 38f;
        public float ticketExitSeconds = 0.6f;
        public float ticketPopScale = 1.25f;
        public float ticketGlideSpeed = 12f;
        [Tooltip("Timer bar colour: above this fraction green, above the next amber, else red.")]
        public float timerGreenAbove = 0.5f;
        public float timerAmberAbove = 0.25f;

        [Header("Bottom HUD")]
        public float edgeInset = 20f;
        public Vector2 scoreSize = new Vector2(250f, 104f);
        public Vector2 roomReadoutSize = new Vector2(470f, 104f);
        public Vector2 timerSize = new Vector2(300f, 104f);
        public Vector2 carrySize = new Vector2(124f, 124f);
        public Vector2 keyHintSize = new Vector2(820f, 46f);
        public Vector2 heartsSize = new Vector2(230f, 80f);
        public float heartSize = 56f;
        public float heartLostBounceSeconds = 0.6f;
        [Tooltip("The timer pulses red when this many seconds or fewer are left in the day.")]
        public float timerWarnSeconds = 30f;
        public float scorePopSeconds = 0.35f;

        [Header("Room power meters")]
        [Tooltip("Where a room meter sits: metres in from the room's left wall and down from its ceiling.")]
        public Vector2 meterRoomInset = new Vector2(0.35f, 0.42f);
        public float meterHeight = 40f;
        public Vector2 meterSegment = new Vector2(15f, 22f);
        public float meterSegmentGap = 3f;
        [Tooltip("More capacity than this is drawn with this many segments, each worth several load points.")]
        public int meterMaxSegments = 12;
        public float meterNameSize = 22f;
        public Vector2 overloadBadgeSize = new Vector2(250f, 50f);
        public float overloadPulseSpeed = 9f;
        [Tooltip("Room meters are full size at this many screen pixels per metre (the follow zoom) and shrink when the camera is farther out.")]
        public float meterReferencePxPerMetre = 170f;
        public float meterMinScale = 0.5f;
        [Tooltip("Hide the world-space room labels and load bars drawn by RoomView (the HUD meters replace them).")]
        public bool hideWorldRoomLabels = true;

        [Header("Request pins and edge arrows")]
        public Vector2 pinSize = new Vector2(72f, 72f);
        public float pinGap = 8f;
        [Tooltip("Pins sit this far below the ceiling of their room, metres.")]
        public float pinBelowCeiling = 0.55f;
        public float arrowSize = 92f;
        [Tooltip("Arrows stay inside this part of the screen (viewport units, origin bottom-left).")]
        public Rect arrowSafeArea = new Rect(0.05f, 0.3f, 0.9f, 0.64f);
        [Tooltip("Smallest distance between two arrows along the same edge, reference pixels.")]
        public float arrowSpacing = 104f;

        [Header("Blackout banner")]
        public Vector2 blackoutSize = new Vector2(760f, 150f);
        public float blackoutY = 236f;
        public float blackoutPulseSpeed = 6f;

        [Header("End of day stars (score needed for 1, 2, 3 stars)")]
        public int[] starScores = { 10, 20, 30 };
        public float starPopDelay = 0.35f;

        [Header("Menus")]
        public Vector2 menuButtonSize = new Vector2(420f, 92f);
        public float menuButtonGap = 22f;
        public Vector2 howToCardSize = new Vector2(400f, 480f);
        public float howToCardGap = 28f;
        public Vector2 pausePanelSize = new Vector2(600f, 560f);
        public Vector2 resultPanelSize = new Vector2(820f, 640f);

        [Header("Toasts and prompts")]
        [Tooltip("Top of the toast column in day mode, reference pixels from the top (under the blackout banner).")]
        public float dayToastTop = 492f;
        public Vector2 startCardSize = new Vector2(760f, 300f);
        public Vector2 pauseButtonSize = new Vector2(84f, 80f);

        [Header("Power panel (top-right, under the hearts)")]
        public float powerPanelWidth = 380f;
        public float powerHeaderHeight = 96f;
        public float powerRowHeight = 44f;
        public float powerPanelTop = 116f;
        [Tooltip("Room row bar colours cycle through these (the sheet's room colours); the bar turns amber/red as it fills.")]
        public Color[] roomColors =
        {
            TrippedViewConfig.Hex("#3FA2F6"), TrippedViewConfig.Hex("#8E5BD9"), TrippedViewConfig.Hex("#7DBE4E"), TrippedViewConfig.Hex("#F6C933"),
            TrippedViewConfig.Hex("#E85D75"), TrippedViewConfig.Hex("#5BC7C2"), TrippedViewConfig.Hex("#D6B98C"),
        };
        public Color rowHighlight = new Color(0.96f, 0.79f, 0.2f, 0.22f);

        [Header("Title screen")]
        public float logoSize = 184f;
        public float logoTilt = 6f;
        public Vector2 logoPosition = new Vector2(80f, 92f);
        public Vector2 playButtonSize = new Vector2(470f, 116f);
        public Vector2 titleButtonSize = new Vector2(420f, 82f);
        public float titleButtonGap = 14f;
        public float titleMenuTop = 452f;
        public float titleMenuLeft = 110f;
        public float titleButtonTilt = 2.5f;
        [Tooltip("Title: the game's own house is shown in this part of the screen (viewport units). Off = a flat backdrop.")]
        public bool titleHouseBackdrop = true;
        public Rect titleHouseArea = new Rect(0.38f, 0.03f, 0.6f, 0.8f);
        public float noteSize = 40f;

        [Header("Settings")]
        public int volumeStep = 10;
        public Vector2 settingsPanelSize = new Vector2(1000f, 860f);
        public float settingsRowHeight = 70f;

        [Header("Credits")]
        [Tooltip("Resources text file with one third-party credit per line (# starts a comment).")]
        public string thirdPartyCreditsPath = "Config/credits_thirdparty";
        public Vector2 creditsPanelSize = new Vector2(1100f, 860f);

        /// <summary>Colour of a family member's placeholder portrait.</summary>
        public Color PersonColor(string name)
        {
            if (personNames != null && personColors != null)
                for (int i = 0; i < personNames.Length && i < personColors.Length; i++)
                    if (string.Equals(personNames[i], name, System.StringComparison.OrdinalIgnoreCase)) return personColors[i];
            return personFallback;
        }

        /// <summary>Loads the config asset, or makes one with default values if there is none.</summary>
        public static TrippedUiConfig LoadOrDefault()
        {
            var asset = Resources.Load<TrippedUiConfig>(ResourcePath);
            return asset != null ? asset : CreateInstance<TrippedUiConfig>();
        }
    }
}
