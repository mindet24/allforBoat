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

        [Header("Palette (warm cream + charcoal)")]
        public Color cream = TrippedViewConfig.Hex("#FFF4DE");
        public Color creamDark = TrippedViewConfig.Hex("#F1DDB8");
        public Color charcoal = TrippedViewConfig.Hex("#2B2B33");
        public Color charcoalSoft = TrippedViewConfig.Hex("#4A4A55");
        public Color shadow = new Color(0.08f, 0.06f, 0.1f, 0.35f);
        public Color accent = TrippedViewConfig.Hex("#FFB300");
        public Color good = TrippedViewConfig.Hex("#43B047");
        public Color bad = TrippedViewConfig.Hex("#E53935");
        public Color coin = TrippedViewConfig.Hex("#FFC93C");
        public Color coinRim = TrippedViewConfig.Hex("#D9901A");
        public Color backdropTop = TrippedViewConfig.Hex("#FFB547");
        public Color backdropBottom = TrippedViewConfig.Hex("#F0703C");
        public Color dim = new Color(0.08f, 0.07f, 0.1f, 0.72f);
        public Color segmentEmpty = TrippedViewConfig.Hex("#5A5A66");
        public Color meterBack = new Color(0.17f, 0.17f, 0.2f, 0.9f);
        public Color running = TrippedViewConfig.Hex("#3DA5F4");
        [Tooltip("Corner radius of panels and buttons, in sprite pixels (the sprite is 128 px).")]
        public int cornerRadius = 30;
        public Vector2 shadowOffset = new Vector2(0f, -7f);

        [Header("Buttons")]
        public float buttonHoverScale = 1.06f;
        public float buttonPressScale = 0.94f;
        public float buttonAnimSpeed = 18f;
        public Color buttonPrimary = TrippedViewConfig.Hex("#43B047");
        public Color buttonSecondary = TrippedViewConfig.Hex("#FFF4DE");
        public Color buttonFocusRing = TrippedViewConfig.Hex("#FFE066");

        [Header("Order tickets (top-left)")]
        public Vector2 ticketOrigin = new Vector2(20f, 18f);
        public Vector2 ticketSize = new Vector2(196f, 212f);
        public float ticketGap = 12f;
        public int ticketsPerRow = 6;
        public float ticketIconSize = 86f;
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

        /// <summary>Loads the config asset, or makes one with default values if there is none.</summary>
        public static TrippedUiConfig LoadOrDefault()
        {
            var asset = Resources.Load<TrippedUiConfig>(ResourcePath);
            return asset != null ? asset : CreateInstance<TrippedUiConfig>();
        }
    }
}
