using UnityEngine;

namespace Trouble.Game
{
    /// <summary>One sound and how loud it plays (0..1).</summary>
    [System.Serializable]
    public struct SoundVolume
    {
        public string name;
        [Range(0f, 1f)] public float volume;

        public SoundVolume(string name, float volume)
        {
            this.name = name;
            this.volume = volume;
        }
    }

    /// <summary>A furnished room model for one sim room, and the parts of it to leave out.</summary>
    [System.Serializable]
    public struct RoomModelEntry
    {
        /// <summary>Sim room id (level JSON).</summary>
        public string roomId;
        /// <summary>Art/Models/Room_&lt;Id&gt;.fbx. None = the kit's plain floor and back wall.</summary>
        public GameObject model;
        [Tooltip("Child objects always left out (for example the 3D fridge: the PNG fridge replaces it).")]
        public string[] hideParts;
        [Tooltip("Child objects left out only when the model is not built at the room's real width (an old 6 m placeholder).")]
        public string[] hideWhenRescaled;

        public RoomModelEntry(string roomId, string[] hideParts = null, string[] hideWhenRescaled = null)
        {
            this.roomId = roomId;
            model = null;
            this.hideParts = hideParts ?? new string[0];
            this.hideWhenRescaled = hideWhenRescaled ?? new string[0];
        }
    }

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
        [Tooltip("Overlay over a room the player cannot see (v1.1 §3.3: #1E1E24 at 85%).")]
        public Color hiddenRoomOverlay = new Color(0x1E / 255f, 0x1E / 255f, 0x24 / 255f, 0.85f);
        [Tooltip("Overlay over a visible socket area whose breaker is OFF (v1.1 §3.3: black at 55%).")]
        public Color powerOutOverlay = new Color(0f, 0f, 0f, 0.55f);
        [Tooltip("Soft edge of the Sight overlay, metres.")]
        public float overlayFeather = 0.35f;
        [Tooltip("Plain grey of a wall wire whose socket is not revealed yet.")]
        public Color wireUnknown = Hex("#6E6E78");
        public float wireWidth = 0.035f;
        [Tooltip("Seconds for a revealed wire to light from socket to panel.")]
        public float wireSweepSeconds = 0.4f;
        public Color cordColor = Hex("#2B2B33");
        public float cordWidth = 0.045f;
        public float cordSag = 0.18f;
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

        [Header("Frame pacing (G7 perf)")]
        [Tooltip("Frames per second the game aims for when vSync is off (0 or less = platform default, uncapped on desktop).")]
        public int targetFrameRate = 60;
        [Tooltip("0 = vSync off (targetFrameRate caps the rate), 1 = every vertical blank. Applied at boot.")]
        [Range(0, 2)] public int vSyncCount = 1;

        [Header("Camera (GDD v1.1 §2.6)")]
        [Tooltip("Orthographic, straight-on side view. Off = perspective with fieldOfView, looking down by cameraTilt (shows floors and room depth).")]
        public bool orthographic = false;
        public float fieldOfView = 30f;
        [Tooltip("Perspective only: degrees the camera looks down at the house (0 = straight on). The camera orbits the point it frames.")]
        [Range(0f, 30f)] public float cameraTilt = 10f;

        [Header("Lighting (rooms lit by their own power)")]
        [Tooltip("Each room has a ceiling light that is on while the room's breaker is ON.")]
        public bool roomLights = true;
        public Color roomLightColor = Hex("#FFD49A");
        public float roomLightIntensity = 2.2f;
        [Tooltip("Light range, metres.")]
        public float roomLightRange = 7f;
        [Tooltip("Height of the light under the ceiling, metres.")]
        public float roomLightDrop = 0.45f;
        [Tooltip("Depth of the light into the room, as a fraction of roomDepth (0 = open front).")]
        [Range(0f, 1f)] public float roomLightDepth = 0.35f;
        [Tooltip("Seconds the light takes to come back on.")]
        public float roomLightFadeIn = 0.4f;
        [Tooltip("Seconds of flicker when the breaker goes OFF.")]
        public float roomLightFlicker = 0.35f;
        [Tooltip("Directional (sun) light intensity, applied at boot. Lower = the room lights matter more.")]
        public float sunIntensity = 0.55f;
        [Tooltip("Ambient light intensity, applied at boot.")]
        public float ambientIntensity = 0.65f;
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
        [Tooltip("Part of the screen the house may use: between the panels, under the timeline, above the tray (viewport units, origin bottom-left).")]
        public Rect houseArea = new Rect(400f / 1920f, 200f / 1080f, 1120f / 1920f, 704f / 1080f);

        [Header("Day mode (level_demo)")]
        [Tooltip("House area of the screen in day mode: no left panel, requests on the right (viewport units).")]
        public Rect dayHouseArea = new Rect(0.01f, 64f / 1080f, 1520f / 1920f, 922f / 1080f);
        [Tooltip("Day mode: extra space around the whole house at the farthest zoom (0.06 = the house fills about 94% of dayHouseArea's limiting side).")]
        public float dayHomeMargin = 0.06f;
        [Tooltip("Day mode: height of the bottom strip that holds the key hint, reference pixels.")]
        public float dayTrayHeight = 56f;
        [Tooltip("Day mode request markers: one row per room at this distance below the ceiling (the room label's height), metres.")]
        public float markerRowBelowCeiling = 0.7f;
        [Tooltip("Size of one request marker (circle + caption), reference pixels.")]
        public Vector2 markerSize = new Vector2(170f, 36f);
        public float markerGap = 6f;
        public Color urgencyUrgent = Hex("#E53935");
        public Color urgencyNormal = Hex("#FFB300");
        public Color urgencyRelaxed = Hex("#8A8A94");
        public Color heartColor = Hex("#E53950");
        public Color heartEmpty = Hex("#3A3A44");
        public Color daySocket = Hex("#EFE3CF");
        [Tooltip("Dark overlay over the whole house during a blackout.")]
        public Color blackoutOverlay = new Color(0f, 0f, 0f, 0.62f);
        public float cardExitSeconds = 0.7f;

        [Header("Day follow camera (G6)")]
        [Tooltip("Follow zoom: at least this many floors are on screen.")]
        public float followMinFloors = 1.9f;
        [Tooltip("Follow zoom: metres of each neighbouring room shown beside the character's room.")]
        public float followNeighbourPeek = 1.2f;
        [Tooltip("0 = the view follows the character exactly, 1 = it keeps the character's room centred.")]
        [Range(0f, 1f)] public float followRoomLock = 0.45f;
        [Tooltip("Where the middle of the character's room sits on screen (viewport units, origin bottom-left). Below the middle leaves room for the order tickets.")]
        public Vector2 followScreenAnchor = new Vector2(0.5f, 0.45f);
        [Tooltip("Seconds the camera takes to catch up sideways while the character walks.")]
        public float followSmoothTime = 0.28f;
        [Tooltip("Seconds the camera takes to ease to a new floor.")]
        public float followFloorSmoothTime = 0.45f;
        [Tooltip("Seconds to zoom between the follow view and the whole-house overview.")]
        public float overviewSmoothTime = 0.25f;
        [Tooltip("Metres the follow view may go past the house's outer walls.")]
        public float followBoundsPad = 0.6f;
        [Tooltip("Hold to see the whole house.")]
        public UnityEngine.InputSystem.Key overviewKey = UnityEngine.InputSystem.Key.Tab;
        public UnityEngine.InputSystem.Key overviewAltKey = UnityEngine.InputSystem.Key.Q;
        [Tooltip("Overview: part of the screen the whole house fits in, under the tickets and above the bottom HUD (viewport units).")]
        public Rect overviewArea = new Rect(0.03f, 0.13f, 0.94f, 0.66f);
        public float overviewMargin = 0.04f;

        [Header("Day mode keyboard (walk, stairs, E)")]
        [Tooltip("Half the horizontal run of a flight of stairs, metres. The flight is centred on sim.StairsX.")]
        public float stairsHalfRun = 1.0f;
        [Tooltip("Part of a climb spent stepping from sim.StairsX to the foot of the stairs (and off the top).")]
        [Range(0f, 0.45f)] public float stairsLanding = 0.15f;
        [Tooltip("How fast the drawn character catches up with the sim's position, metres per second.")]
        public float characterFollowSpeed = 6f;
        [Tooltip("Height of the carried appliance's sprite above the head, metres.")]
        public float carryIconHeight = 0.55f;
        [Tooltip("z of day-mode labels, load bars and key prompts: in front of the house so stairs and walls never cover them.")]
        public float dayOverlayZ = -0.45f;
        public Color promptBack = Hex("#1E1E24E6");
        public Color promptText = Hex("#FFE066");
        [Tooltip("Size of a key prompt bubble (\"E\", \"W ↑\"), metres.")]
        public Vector2 promptSize = new Vector2(0.5f, 0.42f);
        public Vector2 stairsPromptSize = new Vector2(0.8f, 0.42f);
        [Tooltip("Smallest space between the E prompt and a stairs prompt, metres.")]
        public float promptGap = 0.12f;
        [Tooltip("Day mode: z of the character and appliance sprites, just in front of the furnished room models' open front (z = 0) and behind the prompts (dayOverlayZ).")]
        public float dayFrontZ = -0.2f;
        [Tooltip("Resources path of the breaker panel sprite (bottom-centre pivot, 1 unit tall).")]
        public string panelArtPath = "Art/Props/panel";
        [Tooltip("Height the panel sprite is drawn at, metres.")]
        public float panelWorldHeight = 0.9f;
        [Tooltip("Height of the panel's bottom edge above the floor, metres.")]
        public float panelMountHeight = 1.0f;

        [Header("3D house kit (day mode)")]
        [Tooltip("true = build the rooms from the house kit FBX. false (or no kit assigned) = the greybox.")]
        public bool useHouseKit = true;
        [Tooltip("Assets/TRIPPED/Art/Models/HouseKit.fbx: room pieces 6 m wide, 4 m deep, 3 m tall.")]
        public GameObject houseKit;
        [Tooltip("Width of one kit room module, metres. Pieces are scaled in x by room width / this.")]
        public float kitModuleWidth = 6f;
        [Tooltip("Yaw applied to every kit piece so it faces the camera. The FBX comes in turned 180° (x and depth negated).")]
        public float kitYaw = 180f;
        [Tooltip("Furnished room models (Art/Models/Room_<Id>.fbx) by sim room id. A room with a model uses its own floor and back wall instead of the kit's.")]
        public RoomModelEntry[] roomModels =
        {
            new RoomModelEntry("living"), new RoomModelEntry("bedroom"), new RoomModelEntry("bathroom"),
            new RoomModelEntry("kidroom"), new RoomModelEntry("study"), new RoomModelEntry("laundry"),
            new RoomModelEntry("kitchen"),
        };
        [Tooltip("A room model is used at scale 1 when its width is within this many metres of the room's; otherwise it is stretched in x to fit.")]
        public float roomModelWidthTolerance = 0.1f;
        [Tooltip("Scale of the kit's Socket_Plate (0.26 m) on each socket.")]
        public float socketPlateScale = 1.5f;

        [Header("Alerts (GDD v1.1 §2.5)")]
        public float deadlineWarnSeconds = 5f;
        public float routineWarnSeconds = 3f;
        [Tooltip("Seconds an \"!\" interruption bubble stays.")]
        public float interruptAlertSeconds = 3f;
        public Color alertColor = Hex("#E53935");
        [Tooltip("Margin of off-screen alert arrows inside the house area of the screen (viewport units).")]
        public float alertEdgeMargin = 0.03f;

        [Header("Ticking")]
        public int maxStepsPerFrame = 5;

        [Header("Start (G3)")]
        [Tooltip("true = a fresh Play opens the Title screen. false = go straight into startLevel (quick test flow). The -startLevel N command-line argument and the editor pref TRIPPED.skipTitle do the same.")]
        public bool showTitle = true;

        [Header("Audio (GDD v1.0 §7.2 levels)")]
        [Tooltip("Resources folder checked for a file named like each sound before the generated one is used.")]
        public string audioFolder = "Audio";
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Tooltip("Volume of every sound, by name. A name missing here plays at 0.5.")]
        public SoundVolume[] soundVolumes =
        {
            new SoundVolume("click", 0.4f), new SoundVolume("clunk", 0.8f), new SoundVolume("chime", 0.4f),
            new SoundVolume("wrong", 0.35f), new SoundVolume("ding", 0.4f), new SoundVolume("tick", 0.25f),
            new SoundVolume("nope", 0.3f), new SoundVolume("fail_small", 0.4f), new SoundVolume("win", 0.5f),
            new SoundVolume("lose", 0.5f),
        };
        [Tooltip("Buzz volume when a circuit has just become overloaded.")]
        [Range(0f, 1f)] public float buzzMinVolume = 0.25f;
        [Tooltip("Buzz volume when the overload counter is about to trip the breaker.")]
        [Range(0f, 1f)] public float buzzMaxVolume = 0.6f;
        [Tooltip("Seconds between the two clunks of a main trip.")]
        public float secondClunkDelay = 0.18f;
        [Tooltip("Tick sound every second while a request has this many seconds or fewer left.")]
        public float tickWarnSeconds = 5f;

        [Header("Screens (G3)")]
        [Tooltip("Seconds after winning the last level before the End screen opens.")]
        public float endScreenDelay = 2f;

        /// <summary>Playing volume of a sound (before the master volume); 0.5 if it is not listed.</summary>
        public float VolumeOf(string sound)
        {
            if (soundVolumes != null)
                for (int i = 0; i < soundVolumes.Length; i++)
                    if (soundVolumes[i].name == sound) return soundVolumes[i].volume;
            return 0.5f;
        }

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

        /// <summary>The furnished model entry for a room id; false if there is none (or no model is assigned).</summary>
        public bool TryRoomModel(string roomId, out RoomModelEntry entry)
        {
            if (roomModels != null)
                foreach (var e in roomModels)
                    if (e.roomId == roomId && e.model != null)
                    {
                        entry = e;
                        return true;
                    }
            entry = default;
            return false;
        }

        /// <summary>Colour of a day request by urgency: urgent red, normal amber, relaxed grey.</summary>
        public Color UrgencyColor(UrgencyLevel level)
        {
            switch (level)
            {
                case UrgencyLevel.Urgent: return urgencyUrgent;
                case UrgencyLevel.Relaxed: return urgencyRelaxed;
                default: return urgencyNormal;
            }
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
