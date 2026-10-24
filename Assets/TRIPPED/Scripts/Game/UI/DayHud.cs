using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Trouble.Game
{
    /// <summary>What every day-HUD piece needs. Built once per level by <see cref="Hud"/>.</summary>
    public sealed class DayHudContext
    {
        public LevelController Controller;
        public TrippedViewConfig Config;
        public TrippedUiConfig Ui;
        public HudText Text;
        public ArtLibrary Art;
        public RectTransform Canvas;

        /// <summary>The appliance's type id ("kettle"), or the id itself.</summary>
        public static string TypeOf(HouseSim sim, string applianceId)
        {
            var app = sim.FindAppliance(applianceId);
            return app != null ? app.Type : applianceId;
        }

        /// <summary>The appliance's display name.</summary>
        public static string NameOf(HouseSim sim, string applianceId)
        {
            var app = sim.FindAppliance(applianceId);
            return app == null ? applianceId : sim.Catalog.Find(app.Type)?.name ?? app.Type;
        }

        /// <summary>The PNG sprite of an appliance (Resources/Art/Appliances/{type}.png), or null.</summary>
        public Sprite IconOf(HouseSim sim, string applianceId) => Art?.Appliance(TypeOf(sim, applianceId));

        /// <summary>A face portrait from Resources/{faceFolder}/{name}.png (lower case), or null until the art exists.</summary>
        public static Sprite FaceOf(TrippedUiConfig ui, string owner)
        {
            if (string.IsNullOrEmpty(owner)) return null;
            string key = owner.ToLowerInvariant();
            if (Faces.TryGetValue(key, out var s)) return s;
            s = Resources.Load<Sprite>(ui.faceFolder + "/" + key);
            Faces[key] = s;
            return s;
        }

        private static readonly Dictionary<string, Sprite> Faces = new Dictionary<string, Sprite>();

        /// <summary>Canvas size in reference pixels.</summary>
        public Vector2 CanvasSize => Canvas.rect.size;

        /// <summary>Canvas position (anchored to the top-left, y down negative) of a house-front point as drawn now.</summary>
        /// <param name="z">Depth of the point (0 = the house front; the panel hangs on the back wall).</param>
        public Vector2 ToCanvas(Vector2 world, out Vector2 viewport, float z = 0f)
        {
            var cam = Controller.HouseCam;
            viewport = cam != null ? cam.WorldToViewportNow(world, z) : new Vector2(0.5f, 0.5f);
            var size = CanvasSize;
            return new Vector2(viewport.x * size.x, -(1f - viewport.y) * size.y);
        }
    }

    // =====================================================================================================
    // Order tickets
    // =====================================================================================================

    /// <summary>
    /// Overcooked-style order tickets across the top-left: who wants it, the appliance PNG, the target room, an urgency
    /// header and a shrinking timer (a progress fill once running). New tickets slide in, waiting tickets shake in their
    /// last seconds, finished ones pop (done) or flash red (missed) and leave; the rest glide into the gap.
    /// </summary>
    public sealed class OrderTickets
    {
        private sealed class Ticket
        {
            public RequestState Request;
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Body;
            public Image Header;
            public Image IconBack;
            public Image BarFill;
            public TextMeshProUGUI Time;
            public TextMeshProUGUI Stamp;
            public Image Pill;
            public Image PillIcon;
            public float BornAt;
            public float EndAt = -1f;
            public TicketPhase EndPhase;
            public Vector2 Pos;
            public bool Placed;
            public string ShownTime = "";
            public int ShownBand = -1;
        }

        private readonly Dictionary<int, Ticket> _tickets = new Dictionary<int, Ticket>();
        private readonly List<Ticket> _ordered = new List<Ticket>();
        private readonly List<int> _dead = new List<int>();
        private DayHudContext _cx;
        private RectTransform _root;

        /// <summary>Tickets on screen, including leaving ones (for checks).</summary>
        public int Count => _tickets.Count;

        public void Build(DayHudContext cx)
        {
            _cx = cx;
            _root = UIFactory.Stretch(UIFactory.Rect(cx.Canvas, "OrderTickets", 0, 0, 0, 0));
        }

        public void Sync(HouseSim sim, float now, float dt)
        {
            var ui = _cx.Ui;
            int tps = Mathf.Max(1, sim.Rules.ticksPerSecond);
            bool dayOver = sim.Phase == LevelPhase.Won || sim.Phase == LevelPhase.Lost;
            foreach (var r in sim.Requests)
            {
                if (r.Serial <= 0) continue;
                if (!_tickets.TryGetValue(r.Serial, out var t))
                {
                    if (!r.IsActive || dayOver) continue;
                    t = Make(sim, r, now);
                    _tickets[r.Serial] = t;
                }
                UpdateTicket(t, sim, r, now, tps, dayOver);
            }

            _ordered.Clear();
            _dead.Clear();
            foreach (var pair in _tickets)
            {
                var t = pair.Value;
                if (t.EndAt >= 0f && now - t.EndAt > ui.ticketExitSeconds) _dead.Add(pair.Key);
                else _ordered.Add(t);
            }
            foreach (int serial in _dead)
            {
                Object.Destroy(_tickets[serial].Root.gameObject);
                _tickets.Remove(serial);
            }
            _ordered.Sort((a, b) => a.Request.Serial.CompareTo(b.Request.Serial));
            int slot = 0;
            float glide = 1f - Mathf.Exp(-ui.ticketGlideSpeed * dt);
            foreach (var t in _ordered)
            {
                var target = TicketRules.SlotPosition(slot, ui.ticketOrigin, ui.ticketSize, ui.ticketGap, ui.ticketsPerRow);
                if (t.EndAt < 0f) slot++;
                if (!t.Placed)
                {
                    t.Pos = target;
                    t.Placed = true;
                }
                else if (t.EndAt < 0f) t.Pos = Vector2.Lerp(t.Pos, target, glide);

                var phase = TicketRules.Phase(t.Request.Done, t.Request.Missed, t.Request.ProgressTicks);
                float secondsLeft = (t.Request.DueTicks - sim.Now) / (float)tps;
                float shake = TicketRules.ShakeOffset(t.EndAt < 0f ? phase : TicketPhase.Done, secondsLeft, ui.ticketShakeBelowSeconds, now,
                    ui.ticketShakeAmplitude, ui.ticketShakeFrequency);
                float slide = TicketRules.SlideOffset(now - t.BornAt, ui.ticketSlideSeconds) * ui.ticketSlideDistance;
                float scale = 1f;
                float alpha = 1f;
                if (t.EndAt >= 0f)
                {
                    float since = now - t.EndAt;
                    scale = TicketRules.ExitScale(t.EndPhase, since, ui.ticketExitSeconds, ui.ticketPopScale);
                    alpha = TicketRules.ExitAlpha(since, ui.ticketExitSeconds);
                    if (t.EndPhase == TicketPhase.Missed) shake = Mathf.Sin(since * 60f) * ui.ticketShakeAmplitude * 2f * (1f - since / ui.ticketExitSeconds);
                }
                // The root's pivot is its centre, so scaling pops in place.
                t.Root.anchoredPosition = new Vector2(t.Pos.x + ui.ticketSize.x * 0.5f + shake, -(t.Pos.y + ui.ticketSize.y * 0.5f) + slide);
                t.Root.localScale = new Vector3(scale, scale, 1f);
                t.Group.alpha = alpha;
            }
        }

        private void UpdateTicket(Ticket t, HouseSim sim, RequestState r, float now, int tps, bool dayOver)
        {
            var ui = _cx.Ui;
            var phase = TicketRules.Phase(r.Done, r.Missed, r.ProgressTicks);
            if ((phase == TicketPhase.Done || phase == TicketPhase.Missed || dayOver) && t.EndAt < 0f)
            {
                t.EndAt = now;
                t.EndPhase = phase == TicketPhase.Missed ? TicketPhase.Missed : TicketPhase.Done;
                if (dayOver && phase != TicketPhase.Done && phase != TicketPhase.Missed) t.EndPhase = TicketPhase.Waiting;
                t.Stamp.gameObject.SetActive(t.EndPhase != TicketPhase.Waiting);
                if (t.EndPhase == TicketPhase.Done)
                {
                    t.Stamp.text = _cx.Text.Get("g6_done").ToUpperInvariant() + "!";
                    t.Stamp.color = ui.good;
                    t.Header.color = ui.good;
                }
                else if (t.EndPhase == TicketPhase.Missed)
                {
                    t.Stamp.text = _cx.Text.Get("g6_missed").ToUpperInvariant();
                    t.Stamp.color = ui.bad;
                    t.Header.color = ui.bad;
                    t.Body.color = Color.Lerp(ui.cream, ui.bad, 0.45f);
                }
            }
            if (t.EndAt >= 0f) return;

            float fill;
            Color barColor;
            string time;
            if (phase == TicketPhase.Running)
            {
                fill = HudFormat.Progress(r.ProgressTicks, r.DurationTicks);
                barColor = ui.running;
                time = "ON";
                t.IconBack.color = Color.Lerp(ui.creamDark, ui.running, 0.35f + 0.15f * Mathf.Sin(now * 6f));
                if (t.Pill.color != ui.running)
                {
                    t.Pill.color = ui.running;
                    t.PillIcon.sprite = UiIcons.Plug;
                }
            }
            else
            {
                fill = DayRules.WaitFraction(r.SpawnTick, r.DueTicks, sim.Now);
                int band = TicketRules.TimerBand(fill, ui.timerGreenAbove, ui.timerAmberAbove);
                barColor = band == 0 ? ui.good : band == 1 ? ui.accent : ui.bad;
                int secs = Mathf.Max(0, Mathf.CeilToInt((r.DueTicks - sim.Now) / (float)tps));
                time = PowerPanelRules.Clock(secs);
                if (t.Pill.color != ui.red)
                {
                    t.Pill.color = ui.red;
                    t.PillIcon.sprite = UiIcons.Clock;
                }
                if (band != t.ShownBand)
                {
                    t.ShownBand = band;
                    t.IconBack.color = ui.creamDark;
                }
            }
            UIFactory.SetBar(t.BarFill, fill);
            t.BarFill.color = barColor;
            if (time != t.ShownTime)
            {
                t.ShownTime = time;
                t.Time.text = time;
            }
        }

        private Ticket Make(HouseSim sim, RequestState r, float now)
        {
            // A request bubble like the art sheet: the person's portrait, "Need a Kettle!", the appliance and a red
            // clock pill; a thin bar under the speech shows the time left (or the progress once it runs).
            var ui = _cx.Ui;
            var cfg = _cx.Config;
            var size = ui.ticketSize;
            var urgency = cfg.UrgencyColor(DayRules.ParseUrgency(r.Urgency));
            var t = new Ticket { Request = r, BornAt = now };
            var body = UiKit.Card(_root, "Ticket " + r.Serial, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, size, ui.cream, ui);
            t.Body = body;
            t.Root = (RectTransform)body.transform.parent;
            t.Group = t.Root.gameObject.AddComponent<CanvasGroup>();
            t.Group.blocksRaycasts = false;
            var bt = body.transform;
            float w = size.x, h = size.y;

            // Portrait: an urgency-coloured ring around the person's face (or a coloured circle with the initial).
            float ps = ui.portraitSize;
            t.Header = UIFactory.Panel(bt, "Ring", 10, (h - ps) * 0.5f - 6f, ps, ps, urgency, false, false);
            t.Header.sprite = PlaceholderArt.Circle;
            var face = UIFactory.Panel(t.Header.transform, "Face", 6, 6, ps - 12, ps - 12, ui.PersonColor(r.Owner), false, false);
            face.sprite = PlaceholderArt.Circle;
            var faceArt = DayHudContext.FaceOf(ui, r.Owner);
            if (faceArt != null)
            {
                var img = UiKit.Icon(face.transform, "Art", 0, 0, ps - 12, ps - 12, faceArt, Color.white);
                img.preserveAspect = true;
            }
            else UiKit.Fill(face.transform, "Initial", AlertRules.Initial(r.Owner), ps * 0.5f, Color.white);
            var nameTag = UiKit.Card(t.Header.transform, "Name", new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(ps + 6f, 26f), ui.navy, ui, false);
            nameTag.sprite = UiArt.Pill;
            var nameText = UiKit.Fill(nameTag.transform, "Text", r.Owner, 18, Color.white);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 10;
            nameText.fontSizeMax = 18;

            // Speech.
            float sx = ps + 22f, sw = w - sx - ui.ticketIconSize - 18f;
            string appName = DayHudContext.NameOf(sim, r.ApplianceId);
            var need = UiKit.Text(bt, "Speech", sx, 10, sw, 56, _cx.Text.Format("g7_need", ("appliance", appName)), 28, ui.navy, TextAlignmentOptions.TopLeft);
            need.textWrappingMode = TextWrappingModes.Normal;
            need.enableAutoSizing = true;
            need.fontSizeMin = 16;
            need.fontSizeMax = 28;
            string roomName = sim.FindRoom(r.RoomId)?.Name ?? r.RoomId;
            var room = UiKit.Text(bt, "Room", sx, 64, sw, 24, _cx.Text.Format("g7_in_room", ("room", roomName)), 20, ui.charcoalSoft, TextAlignmentOptions.MidlineLeft, false);
            room.overflowMode = TextOverflowModes.Ellipsis;

            // Appliance icon on the right.
            float iconBack = ui.ticketIconSize + 8f;
            t.IconBack = UIFactory.Panel(bt, "IconBack", w - iconBack - 10f, 6, iconBack, iconBack, ui.creamDark, false, false);
            t.IconBack.sprite = PlaceholderArt.Circle;
            var icon = _cx.IconOf(sim, r.ApplianceId);
            if (icon != null)
                UiKit.Icon(t.IconBack.transform, "Icon", 4, 4, ui.ticketIconSize, ui.ticketIconSize, icon, Color.white);
            else
                UiKit.Text(t.IconBack.transform, "IconText", 0, 0, iconBack, iconBack, appName, 18, ui.charcoal);

            // Time bar under the speech.
            var barBack = UIFactory.Panel(bt, "Timer", sx, h - 30, w - sx - ui.clockPillSize.x - 22f, 12, ui.creamDark, false, false);
            barBack.sprite = UiArt.Pill;
            barBack.type = Image.Type.Sliced;
            t.BarFill = UIFactory.Fill(UIFactory.Stretch(UIFactory.Rect(barBack.transform, "Fill", 0, 0, 0, 0)), ui.good, false, false);
            t.BarFill.sprite = UiArt.Pill;
            t.BarFill.type = Image.Type.Sliced;

            // Red clock pill, bottom-right, overlapping the edge like the sheet.
            var pill = ui.clockPillSize;
            t.Pill = UIFactory.Panel(bt, "Clock", w - pill.x - 8f, h - pill.y - 4f, pill.x, pill.y, ui.red, false, false);
            t.Pill.sprite = UiArt.Pill;
            t.Pill.type = Image.Type.Sliced;
            t.PillIcon = UiKit.Icon(t.Pill.transform, "Icon", 4, 2, pill.y - 4f, pill.y - 4f, UiIcons.Clock, Color.white);
            t.Time = UiKit.Text(t.Pill.transform, "Time", pill.y, 0, pill.x - pill.y - 6f, pill.y, "", 26, Color.white, TextAlignmentOptions.Center);

            t.Stamp = UiKit.Text(bt, "Stamp", 0, 20, w, 80, "", 52, ui.good);
            t.Stamp.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            t.Stamp.gameObject.SetActive(false);
            t.Root.anchoredPosition = new Vector2(-1000f, 1000f);
            return t;
        }
    }

    // =====================================================================================================
    // Segmented power meter
    // =====================================================================================================

    /// <summary>
    /// A chunky segmented power meter: "[Kitchen ■■■■■□□ 5/7]" on a dark pill, segments green → amber → red, and a
    /// big red "OVERLOAD 2.1s" badge while the room is over capacity. Used over each room and on the HUD.
    /// </summary>
    public sealed class MeterWidget
    {
        private Image[] _segments;
        private TextMeshProUGUI _count;
        private TextMeshProUGUI _name;
        private RectTransform _badge;
        private Image _badgeImage;
        private TextMeshProUGUI _badgeText;
        private string _shownCount = "";
        private string _shownBadge = "";
        private int _shownKey = -1;
        private TrippedUiConfig _ui;
        private TrippedViewConfig _config;
        private HudText _text;

        /// <summary>The whole meter (pivot top-left).</summary>
        public RectTransform Root { get; private set; }

        /// <summary>Width of the pill in reference pixels.</summary>
        public float Width { get; private set; }

        /// <summary>True while the OVERLOAD badge shows (for checks).</summary>
        public bool OverloadShown => _badge != null && _badge.gameObject.activeSelf;

        /// <summary>Builds the meter for a room of <paramref name="capacity"/>.</summary>
        /// <param name="scale">Size multiplier (the HUD readout is bigger).</param>
        /// <param name="badgeAbove">Put the OVERLOAD badge above the pill instead of below.</param>
        public void Build(Transform parent, string roomName, int capacity, DayHudContext cx, float scale, bool badgeAbove, bool background = true)
        {
            _ui = cx.Ui;
            _config = cx.Config;
            _text = cx.Text;
            var ui = _ui;
            int segments = MeterRules.Compute(0, capacity, 0.7f, 1f, ui.meterMaxSegments).Segments;
            float h = ui.meterHeight * scale;
            float pad = 12f * scale;
            Root = UIFactory.Anchored(parent, "Meter " + roomName, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(10f, h));

            var nameRt = UIFactory.Anchored(Root, "Name", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(pad, 0f), new Vector2(10f, h));
            _name = UIFactory.LabelOn(nameRt, roomName, ui.meterNameSize * scale, ui.cream, TextAlignmentOptions.MidlineLeft, true);
            _name.overflowMode = TextOverflowModes.Overflow;
            float nameW = string.IsNullOrEmpty(roomName) ? 0f : _name.GetPreferredValues(roomName).x + 2f;
            nameRt.sizeDelta = new Vector2(nameW, h);

            float x = pad + nameW + (nameW > 0f ? 10f * scale : 0f);
            var seg = ui.meterSegment * scale;
            float gap = ui.meterSegmentGap * scale;
            _segments = new Image[segments];
            for (int i = 0; i < segments; i++)
            {
                var rt = UIFactory.Anchored(Root, "Seg " + i, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), seg);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = PlaceholderArt.UiRounded;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 3f;
                img.color = ui.segmentEmpty;
                img.raycastTarget = false;
                _segments[i] = img;
                x += seg.x + gap;
            }
            x += 8f * scale - gap;
            var countRt = UIFactory.Anchored(Root, "Count", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(70f * scale, h));
            _count = UIFactory.LabelOn(countRt, "", ui.meterNameSize * scale, ui.cream, TextAlignmentOptions.MidlineLeft, true);
            _count.overflowMode = TextOverflowModes.Overflow;
            Width = x + 66f * scale;
            Root.sizeDelta = new Vector2(Width, h);
            if (background)
            {
                var back = UIFactory.Stretch(UIFactory.Rect(Root, "Back", 0, 0, 0, 0));
                back.SetAsFirstSibling();
                var bimg = back.gameObject.AddComponent<Image>();
                bimg.sprite = UiArt.Pill;
                bimg.type = Image.Type.Sliced;
                bimg.color = ui.meterBack;
                bimg.raycastTarget = false;
            }

            var bs = ui.overloadBadgeSize * scale;
            _badge = UIFactory.Anchored(Root, "Overload", new Vector2(0f, badgeAbove ? 1f : 0f), new Vector2(0f, badgeAbove ? 0f : 1f),
                new Vector2(0f, badgeAbove ? 8f * scale : -6f * scale), bs);
            _badgeImage = _badge.gameObject.AddComponent<Image>();
            _badgeImage.sprite = UiArt.Rounded;
            _badgeImage.type = Image.Type.Sliced;
            _badgeImage.color = ui.bad;
            _badgeImage.raycastTarget = false;
            _badgeText = UiKit.Fill(_badge, "Text", "", 28f * scale, Color.white);
            _badgeText.rectTransform.offsetMin = new Vector2(bs.y * 0.9f, 0f);
            var warn = UIFactory.Anchored(_badge, "Warning", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f * scale, 0f), new Vector2(bs.y * 0.8f, bs.y * 0.8f));
            var warnImg = warn.gameObject.AddComponent<Image>();
            warnImg.sprite = UiIcons.Warning;
            warnImg.raycastTarget = false;
            _badge.localRotation = Quaternion.Euler(0f, 0f, 4f);
            _badge.gameObject.SetActive(false);
        }

        /// <summary>Changes the room name shown (the HUD readout follows the player).</summary>
        public void SetName(string name)
        {
            if (_name != null && _name.text != name) _name.text = name;
        }

        /// <summary>Updates segments, count and the overload badge from a circuit.</summary>
        public void Sync(CircuitState c, RulesConfig rules, float now)
        {
            if (Root == null || c == null) return;
            var m = MeterRules.Compute(c.Load, c.Capacity, _config.loadAmberAbove, _config.loadRedAbove, _ui.meterMaxSegments);
            int key = (m.Filled * 8 + (int)m.Band) * 2 + (c.IsOn ? 1 : 0);
            if (key != _shownKey)
            {
                _shownKey = key;
                var lit = c.IsOn ? _config.LoadColor(m.Band) : _ui.segmentEmpty;
                for (int i = 0; i < _segments.Length; i++) _segments[i].color = i < m.Filled ? lit : _ui.segmentEmpty;
            }
            string count = c.IsOn ? c.Load + "/" + c.Capacity : _text.Get("g6_off");
            if (count != _shownCount)
            {
                _shownCount = count;
                _count.text = count;
                _count.color = c.IsOn ? (m.Over ? _ui.bad : _ui.cream) : _ui.bad;
            }
            bool over = c.IsOn && c.Overloading;
            if (_badge.gameObject.activeSelf != over) _badge.gameObject.SetActive(over);
            if (over)
            {
                string s = _text.Format("g6_overload", ("s", MeterRules.OverloadSeconds(c.OverTicks, rules.overTripTicks, rules.ticksPerSecond)));
                if (s != _shownBadge)
                {
                    _shownBadge = s;
                    _badgeText.text = s;
                }
                float p = 0.5f + 0.5f * Mathf.Sin(now * _ui.overloadPulseSpeed);
                _badgeImage.color = Color.Lerp(_ui.bad, Color.Lerp(_ui.bad, Color.white, 0.35f), p);
                _badge.localScale = Vector3.one * (1f + 0.05f * p);
            }
        }
    }

    // =====================================================================================================
    // Room meters over the house
    // =====================================================================================================

    /// <summary>
    /// One <see cref="MeterWidget"/> per room, pinned to the room's top-left corner where the camera draws it now, and
    /// scaled down when the camera is far out (overview). Hides the old world-space room labels so nothing doubles up.
    /// </summary>
    public sealed class RoomMeters
    {
        private readonly List<(RoomState room, MeterWidget meter)> _meters = new List<(RoomState, MeterWidget)>();
        private DayHudContext _cx;
        private RectTransform _root;

        /// <summary>Meters built (one per room with a circuit).</summary>
        public int Count => _meters.Count;

        /// <summary>The meter of a room, or null (for checks).</summary>
        public MeterWidget Find(string roomId)
        {
            foreach (var m in _meters)
                if (m.room.Id == roomId) return m.meter;
            return null;
        }

        public void Build(DayHudContext cx, HouseSim sim)
        {
            _cx = cx;
            _root = UIFactory.Stretch(UIFactory.Rect(cx.Canvas, "RoomMeters", 0, 0, 0, 0));
            foreach (var room in sim.Rooms)
            {
                var c = sim.FindCircuit(room.Id);
                if (c == null) continue;
                var meter = new MeterWidget();
                meter.Build(_root, room.Name, c.Capacity, cx, 1f, false);
                _meters.Add((room, meter));
            }
            if (cx.Ui.hideWorldRoomLabels) HideWorldLabels();
        }

        /// <summary>The HUD meters replace RoomView's name text, load bar and countdown: switch their renderers off.</summary>
        private void HideWorldLabels()
        {
            var house = _cx.Controller.House;
            if (house == null) return;
            foreach (var view in house.GetComponentsInChildren<RoomView>(true))
                foreach (var part in new[] { "Name", "LoadBarBack", "LoadBarFill", "Countdown" })
                {
                    var t = view.transform.Find(part);
                    if (t == null) continue;
                    foreach (var r in t.GetComponents<Renderer>()) r.enabled = false;
                }
        }

        /// <summary>
        /// Places every meter over its room. While following, only the character's room and rooms fully on screen
        /// keep a meter, and a meter that would touch a HUD panel (<paramref name="hudRects"/>, canvas-local) hides.
        /// </summary>
        public void Sync(HouseSim sim, float now, IReadOnlyList<Rect> hudRects = null)
        {
            var cam = _cx.Controller.HouseCam;
            if (cam == null) return;
            var ui = _cx.Ui;
            bool following = cam.DayFollowing && !cam.Overview;
            var ch = sim.Character;
            string currentRoom = ch.IsClimbing && !string.IsNullOrEmpty(ch.ClimbToRoomId) ? ch.ClimbToRoomId : ch.RoomId;
            float rh = _cx.Config.roomHeight;
            var size = _cx.CanvasSize;
            float pxPerMetre = size.y / Mathf.Max(0.01f, cam.ViewHeight);
            float scale = Mathf.Clamp(pxPerMetre / Mathf.Max(1f, ui.meterReferencePxPerMetre), ui.meterMinScale, 1f);
            foreach (var (room, meter) in _meters)
            {
                var corner = new Vector2(room.X + ui.meterRoomInset.x, HouseLayout.FloorY(room.Floor, rh) + rh - ui.meterRoomInset.y);
                var pos = _cx.ToCanvas(corner, out var vp);
                // Never wider than the room as drawn.
                float roomPx = room.Width * pxPerMetre - 2f * ui.meterRoomInset.x * pxPerMetre;
                float s = Mathf.Min(scale, roomPx / Mathf.Max(1f, meter.Width));
                var r0 = cam.WorldToViewportNow(new Vector2(room.X, HouseLayout.FloorY(room.Floor, rh)));
                var r1 = cam.WorldToViewportNow(new Vector2(room.X + room.Width, HouseLayout.FloorY(room.Floor, rh) + rh));
                float h = (ui.meterHeight + (meter.OverloadShown ? ui.overloadBadgeSize.y + 6f : 0f)) * s;
                var meterRect = new Rect(pos.x - size.x * 0.5f, pos.y + size.y * 0.5f - h, meter.Width * s, h);
                bool visible = vp.x > -0.3f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.2f
                               && MeterVisibility.Show(following, room.Id == currentRoom, Rect.MinMaxRect(r0.x, r0.y, r1.x, r1.y), meterRect, hudRects);
                if (meter.Root.gameObject.activeSelf != visible) meter.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                meter.Root.anchoredPosition = pos;
                meter.Root.localScale = new Vector3(s, s, 1f);
                meter.Sync(sim.FindCircuit(room.Id), sim.Rules, now);
            }
        }
    }

    // =====================================================================================================
    // Request pins and off-screen edge arrows
    // =====================================================================================================

    /// <summary>
    /// A bubble with the appliance icon in every room that has an active request; when that spot is off screen it
    /// becomes an arrow on the screen edge (icon, seconds left) pointing the way. During a blackout the breaker panel
    /// gets one too, so the player always knows where to run.
    /// </summary>
    public sealed class RequestPins
    {
        private sealed class Pin
        {
            public RectTransform Root;
            public Image Ring;
            public Image Icon;
            public TextMeshProUGUI Label;
            public RectTransform Arrow;
            public Image ArrowImage;
            public Sprite ShownSprite;
        }

        private struct Target
        {
            public Vector2 Canvas; // y up
            public Sprite Icon;
            public Color Color;
            public string Label;
            public bool Pulse;
        }

        private readonly List<Pin> _pins = new List<Pin>();
        private readonly List<Target> _targets = new List<Target>();
        private readonly List<Vector2> _points = new List<Vector2>();
        private readonly List<EdgeSlot> _slots = new List<EdgeSlot>();
        private readonly List<RequestState> _roomRequests = new List<RequestState>();
        private DayHudContext _cx;
        private RectTransform _root;
        private Sprite _panelSprite;

        /// <summary>Pins shown last frame.</summary>
        public int Shown { get; private set; }

        /// <summary>Of those, how many are edge arrows.</summary>
        public int Arrows { get; private set; }

        public void Build(DayHudContext cx)
        {
            _cx = cx;
            _root = UIFactory.Stretch(UIFactory.Rect(cx.Canvas, "RequestPins", 0, 0, 0, 0));
            _panelSprite = cx.Art?.Prop(cx.Config.panelArtPath);
        }

        public void Sync(HouseSim sim, float now)
        {
            Shown = 0;
            Arrows = 0;
            if (sim.Phase == LevelPhase.Won || sim.Phase == LevelPhase.Lost || _cx.Controller.HouseCam == null)
            {
                Hide(0);
                return;
            }
            var ui = _cx.Ui;
            var cfg = _cx.Config;
            int tps = Mathf.Max(1, sim.Rules.ticksPerSecond);
            var size = _cx.CanvasSize;
            _targets.Clear();

            foreach (var room in sim.Rooms)
            {
                _roomRequests.Clear();
                foreach (var r in sim.Requests)
                    if (r.Serial > 0 && r.IsActive && r.RoomId == room.Id) _roomRequests.Add(r);
                if (_roomRequests.Count == 0) continue;
                float rowY = HouseLayout.FloorY(room.Floor, cfg.roomHeight) + cfg.roomHeight - ui.pinBelowCeiling;
                var right = _cx.ToCanvas(new Vector2(room.X + room.Width - 0.3f, rowY), out _);
                var left = _cx.ToCanvas(new Vector2(room.X + room.Width * 0.45f, rowY), out _);
                float yUp = size.y + right.y;
                var slots = MarkerLayout.Row(left.x, right.x, yUp - ui.pinSize.y * 0.5f, ui.pinSize, ui.pinGap, _roomRequests.Count, null);
                for (int i = 0; i < _roomRequests.Count; i++)
                {
                    var r = _roomRequests[i];
                    bool running = r.ProgressTicks > 0;
                    int secs = Mathf.Max(0, Mathf.CeilToInt((r.DueTicks - sim.Now) / (float)tps));
                    _targets.Add(new Target
                    {
                        Canvas = slots[i].center,
                        Icon = _cx.IconOf(sim, r.ApplianceId),
                        Color = running ? ui.running : cfg.UrgencyColor(DayRules.ParseUrgency(r.Urgency)),
                        Label = running ? "ON" : secs + "s",
                        Pulse = !running && secs <= ui.ticketShakeBelowSeconds,
                    });
                }
            }

            if (DayRules.IsBlackout(sim))
            {
                var panelRoom = sim.FindRoom(sim.PanelRoomId);
                float py = (panelRoom != null ? HouseLayout.FloorY(panelRoom.Floor, cfg.roomHeight) : 0f) + cfg.panelMountHeight + cfg.panelWorldHeight + 0.45f;
                var p = _cx.ToCanvas(new Vector2(sim.PanelX, py), out _, cfg.roomDepth);
                _targets.Add(new Target
                {
                    Canvas = new Vector2(p.x, size.y + p.y),
                    Icon = _panelSprite,
                    Color = ui.bad,
                    Label = _cx.Text.Get("g6_breaker"),
                    Pulse = true,
                });
            }

            _points.Clear();
            foreach (var t in _targets) _points.Add(t.Canvas);
            var a = ui.arrowSafeArea;
            var safe = new Rect(a.x * size.x, a.y * size.y, a.width * size.x, a.height * size.y);
            EdgeArrowLayout.Place(_points, safe, ui.arrowSpacing, _slots);

            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                var s = _slots[i];
                var pin = Get(Shown++);
                if (!pin.Root.gameObject.activeSelf) pin.Root.gameObject.SetActive(true);
                pin.Root.anchoredPosition = new Vector2(s.Position.x, s.Position.y - size.y);
                float pulse = t.Pulse ? 1f + 0.08f * Mathf.Sin(now * 10f) : 1f;
                float baseScale = s.OnScreen ? 1f : ui.arrowSize / Mathf.Max(1f, ui.pinSize.x);
                pin.Root.localScale = Vector3.one * (baseScale * pulse);
                pin.Ring.color = t.Color;
                pin.ArrowImage.color = t.Color;
                if (pin.ShownSprite != t.Icon)
                {
                    pin.ShownSprite = t.Icon;
                    pin.Icon.sprite = t.Icon;
                    pin.Icon.enabled = t.Icon != null;
                }
                if (pin.Label.text != t.Label) pin.Label.text = t.Label;
                pin.Arrow.gameObject.SetActive(!s.OnScreen);
                if (!s.OnScreen)
                {
                    Arrows++;
                    float rad = s.Angle * Mathf.Deg2Rad;
                    pin.Arrow.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (ui.pinSize.x * 0.5f + 14f);
                    pin.Arrow.localRotation = Quaternion.Euler(0f, 0f, s.Angle);
                }
            }
            Hide(Shown);
        }

        private Pin Get(int i)
        {
            while (_pins.Count <= i) _pins.Add(Make(_pins.Count));
            return _pins[i];
        }

        private void Hide(int from)
        {
            for (int i = from; i < _pins.Count; i++)
                if (_pins[i].Root.gameObject.activeSelf) _pins[i].Root.gameObject.SetActive(false);
        }

        private Pin Make(int index)
        {
            var ui = _cx.Ui;
            var s = ui.pinSize;
            var pin = new Pin();
            pin.Root = UIFactory.Anchored(_root, "Pin " + index, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, s);
            // Arrow first so the bubble draws over its base.
            pin.Arrow = UIFactory.Anchored(pin.Root, "Arrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(s.x * 0.62f, s.x * 0.62f));
            pin.ArrowImage = pin.Arrow.gameObject.AddComponent<Image>();
            pin.ArrowImage.sprite = UiArt.Arrow;
            pin.ArrowImage.raycastTarget = false;
            var shadow = UIFactory.Anchored(pin.Root, "Shadow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), ui.shadowOffset * 0.6f, s);
            var shImg = shadow.gameObject.AddComponent<Image>();
            shImg.sprite = PlaceholderArt.Circle;
            shImg.color = ui.shadow;
            shImg.raycastTarget = false;
            var ringRt = UIFactory.Anchored(pin.Root, "Ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, s);
            pin.Ring = ringRt.gameObject.AddComponent<Image>();
            pin.Ring.sprite = PlaceholderArt.Circle;
            pin.Ring.raycastTarget = false;
            var inner = UIFactory.Anchored(pin.Root, "Inner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, s - new Vector2(12f, 12f));
            var innerImg = inner.gameObject.AddComponent<Image>();
            innerImg.sprite = PlaceholderArt.Circle;
            innerImg.color = ui.cream;
            innerImg.raycastTarget = false;
            var iconRt = UIFactory.Anchored(pin.Root, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), s - new Vector2(22f, 22f));
            pin.Icon = iconRt.gameObject.AddComponent<Image>();
            pin.Icon.preserveAspect = true;
            pin.Icon.raycastTarget = false;
            var tag = UIFactory.Anchored(pin.Root, "Tag", new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(s.x * 0.95f, 26f));
            var tagImg = tag.gameObject.AddComponent<Image>();
            tagImg.sprite = UiArt.Pill;
            tagImg.type = Image.Type.Sliced;
            tagImg.color = ui.charcoal;
            tagImg.raycastTarget = false;
            pin.Label = UiKit.Fill(tag, "Text", "", 18, ui.cream);
            pin.Label.enableAutoSizing = true;
            pin.Label.fontSizeMin = 10;
            pin.Label.fontSizeMax = 18;
            return pin;
        }
    }
}
