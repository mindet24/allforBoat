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

        [Header("Camera (GDD v1.1 §2.6)")]
        [Tooltip("Orthographic side view (art direction, 23 Oct). Off = perspective with fieldOfView.")]
        public bool orthographic = true;
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
        [Tooltip("Part of the screen the house may use: between the panels, under the timeline, above the tray (viewport units, origin bottom-left).")]
        public Rect houseArea = new Rect(400f / 1920f, 200f / 1080f, 1120f / 1920f, 704f / 1080f);

        [Header("Day mode (level_demo)")]
        [Tooltip("House area of the screen in day mode: no left panel, requests on the right (viewport units).")]
        public Rect dayHouseArea = new Rect(0.01f, 200f / 1080f, 1520f / 1920f, 790f / 1080f);
        public Color urgencyUrgent = Hex("#E53935");
        public Color urgencyNormal = Hex("#FFB300");
        public Color urgencyRelaxed = Hex("#8A8A94");
        public Color heartColor = Hex("#E53950");
        public Color heartEmpty = Hex("#3A3A44");
        public Color daySocket = Hex("#EFE3CF");
        [Tooltip("Dark overlay over the whole house during a blackout.")]
        public Color blackoutOverlay = new Color(0f, 0f, 0f, 0.62f);
        public float cardExitSeconds = 0.7f;

        [Header("Day mode keyboard (walk, stairs, E)")]
        [Tooltip("Half the horizontal run of a flight of stairs, metres. The flight is centred on sim.StairsX.")]
        public float stairsHalfRun = 1.0f;
        [Tooltip("Part of a climb spent stepping from sim.StairsX to the foot of the stairs (and off the top).")]
        [Range(0f, 0.45f)] public float stairsLanding = 0.15f;
        [Tooltip("How fast the drawn character catches up with the sim's position, metres per second.")]
        public float characterFollowSpeed = 6f;
        [Tooltip("Height of the carried appliance's sprite above the head, metres.")]
        public float carryIconHeight = 0.75f;
        [Tooltip("z of day-mode labels, load bars and key prompts: in front of the house so stairs and walls never cover them.")]
        public float dayOverlayZ = -0.45f;
        public Color promptBack = Hex("#1E1E24E6");
        public Color promptText = Hex("#FFE066");
        [Tooltip("Size of a key prompt bubble (\"E\", \"W ↑\"), metres.")]
        public Vector2 promptSize = new Vector2(0.5f, 0.42f);
        public Vector2 stairsPromptSize = new Vector2(0.8f, 0.42f);
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
        [Tooltip("Assets/TRIPPED/Art/Models/Room_Kitchen.fbx: a dressed kitchen, used for rooms with theme \"kitchen\".")]
        public GameObject kitchenRoom;
        [Tooltip("Width of one kit room module, metres. Pieces are scaled in x by room width / this.")]
        public float kitModuleWidth = 6f;
        [Tooltip("Yaw applied to every kit piece so it faces the camera. The FBX comes in turned 180° (x and depth negated).")]
        public float kitYaw = 180f;
        [Tooltip("Kitchen parts left out: the 3D fridge (the PNG fridge replaces it) and furniture standing in front of the sockets and the character.")]
        public string[] kitchenSkipParts =
        {
            "Fridge", "Kitchen_Counter", "Kitchen_CounterItems", "Kitchen_Chair_01", "Kitchen_Chair_02",
            "Kitchen_DiningTable", "Kitchen_Bin", "Kitchen_Pantry", "Kitchen_PendantLamp",
        };
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
