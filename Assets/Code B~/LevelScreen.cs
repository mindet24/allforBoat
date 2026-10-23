using System.Collections.Generic;
using TMPro;
using Trouble.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Trouble.Game
{
    public sealed class LevelScreen : MonoBehaviour
    {
        public LevelController Ctrl { get; private set; }
        public RectTransform ChipLayer { get; private set; }
        public RectTransform DragLayer { get; private set; }
        public RectTransform TimelineSlot { get; private set; }   // A: TimelineView.Create(TimelineSlot, ...)

        GameRoot root;
        int index, dayTicks;
        LevelData data;
        FeedbackController fb;
        ResultPanel result;
        HouseView house;
        TrayView tray;
        TextMeshProUGUI clock, pauseLabel, bannerIntro, bannerHint;
        Button pauseBtn;
        readonly List<BreakerView> breakers = new List<BreakerView>();
        readonly List<RequestCardView> cards = new List<RequestCardView>();
        readonly Dictionary<string, ApplianceView> chips = new Dictionary<string, ApplianceView>();
        readonly Dictionary<string, string> roomNames = new Dictionary<string, string>();
        readonly List<RaycastResult> hits = new List<RaycastResult>();

        public static void Build(GameRoot root, RectTransform sr, int index)
        {
            UIFactory.Box(sr, "BG", 0, 0, 1920, 1080, Theme.Bg);
            var data = LevelLoader.Load(index, out var errors);
            if (data == null || errors.Count > 0) { BuildErrors(root, sr, index, errors); return; }
            sr.gameObject.AddComponent<LevelScreen>().Init(root, sr, index, data);
        }

        static void BuildErrors(GameRoot root, RectTransform sr, int index, List<string> errors)
        {
            UIFactory.Label(sr, 100, 80, 1720, 60, "Level " + index + " data is invalid", 44, Theme.LoadBad, TextAlignmentOptions.Left, FontStyles.Bold);
            UIFactory.Label(sr, 100, 160, 1720, 700, string.Join("\n", errors), 26, Theme.Text, TextAlignmentOptions.TopLeft);
            UIFactory.MakeButton(sr, 100, 900, 300, 70, "Back", root.ShowLevelSelect, 30);
            root.EscAction = root.ShowLevelSelect;
        }

        void Init(GameRoot root, RectTransform sr, int index, LevelData data)
        {
            this.root = root; this.index = index; this.data = data;
            Ctrl = new LevelController(data);
            var sim = Ctrl.Sim;
            dayTicks = Mathf.RoundToInt(data.dayLengthSec * 10f);
            foreach (var r in data.rooms) roomNames[r.id] = r.name;

            // ---- แผงพื้นหลัง ----
            TimelineSlot = UIFactory.MakeRect(sr, "TimelineSlot", 400, 96, 1120, 80);
            UIFactory.Box(TimelineSlot, "Bg", 0, 0, 1120, 80, Theme.Panel);
            UIFactory.Box(sr, "RequestPanel", 0, 96, 380, 784, Theme.Panel);
            UIFactory.Box(sr, "BreakerPanel", 1540, 96, 380, 784, Theme.Panel);

            // ---- การ์ดคำขอ ----
            int nReq = sim.Requests.Count;
            float rStep = Mathf.Min(132f, 772f / Mathf.Max(1, nReq));
            for (int i = 0; i < nReq; i++)
                cards.Add(RequestCardView.Create(sr, 10, 108 + i * rStep, sim.Requests[i].Owner));

            // ---- การ์ดเบรกเกอร์ (ช่องบนสุดเว้นไว้ให้การ์ด MAIN ของ A ถ้า MainCapacity > 0) ----
            bool hasMain = sim.MainCapacity > 0;
            int slots = sim.Circuits.Count + (hasMain ? 1 : 0);
            float bStep = Mathf.Min(162f, 772f / Mathf.Max(1, slots));
            for (int i = 0; i < sim.Circuits.Count; i++)
                breakers.Add(BreakerView.Create(this, sr, 1550, 108 + (i + (hasMain ? 1 : 0)) * bStep, sim.Circuits[i].Id));

            // ---- บ้าน ----
            house = new HouseView(this, sr, data);

            // ---- แถบบน ----
            UIFactory.Box(sr, "TopBar", 0, 0, 1920, 96, Theme.Room);
            UIFactory.Label(sr, 24, 24, 640, 48, "L" + index + "  " + data.title, 36, Theme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            clock = UIFactory.Label(sr, 700, 20, 520, 56, "0:00 / 0:00", 44, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            pauseBtn = UIFactory.MakeButton(sr, 1250, 20, 230, 56, "Start (Space)", Ctrl.TogglePause, 22);
            pauseLabel = pauseBtn.GetComponentInChildren<TextMeshProUGUI>();
            UIFactory.MakeButton(sr, 1490, 20, 190, 56, "Retry (R)", () => root.ShowLevel(index), 22);
            UIFactory.MakeButton(sr, 1690, 20, 206, 56, "Menu (Esc)", root.ShowLevelSelect, 22);

            // ---- ข้อความแนะนำกลางบ้าน ----
            bannerIntro = UIFactory.Label(sr, 560, 236, 800, 40, data.intro, 26, Theme.TextDim);
            bannerHint = UIFactory.Label(sr, 560, 280, 800, 44, "PAUSED - plan, then press SPACE", 32, Theme.Glow, TextAlignmentOptions.Center, FontStyles.Bold);

            // ---- ถาด -> ชั้นชิป -> ชั้นลาก (ลำดับสำคัญต่อการซ้อน) ----
            tray = new TrayView(this, sr);
            ChipLayer = UIFactory.MakeRect(sr, "ChipLayer", 0, 0, 1920, 1080);
            DragLayer = UIFactory.MakeRect(sr, "DragLayer", 0, 0, 1920, 1080);
            tray.CreateCordChip(this, ChipLayer);

            // ---- ของ A (stub ไปก่อน) ----
            fb = new FeedbackController(sr);
            result = ResultPanel.Create(sr);

            Ctrl.StepEvents += HandleEvents;
            Ctrl.Rejected += fb.Rejected;
            root.EscAction = OnEsc;

            if (index == 2) fb.Toast("Tip: a lamp shows which breaker feeds a socket.");
        }

        void OnDestroy() { if (fb != null) fb.StopAll(); }

        // ---------------- วนทุกเฟรม ----------------
        void Update()
        {
            if (Ctrl == null) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame) { root.ShowLevel(index); return; }
                if (kb.spaceKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) Ctrl.TogglePause();
            }
            Ctrl.Tick(Time.unscaledDeltaTime);
        }

        void LateUpdate()
        {
            if (Ctrl == null) return;
            var sim = Ctrl.Sim;
            clock.text = Fmt(sim.Now) + " / " + Fmt(dayTicks);

            house.Sync(sim);
            house.ShowCordTargets(Ctrl.PendingCordSource);

            // เครื่องใช้ไฟฟ้า: ถาด หรือยืนเหนือปลั๊ก
            int slot = 0;
            var apps = sim.Appliances;
            var reqs = sim.Requests;
            for (int i = 0; i < apps.Count; i++)
            {
                var a = apps[i];
                if (!chips.TryGetValue(a.Id, out var v))
                {
                    v = ApplianceView.Create(this, a.Id, a.Type.ToString());
                    chips[a.Id] = v;
                }
                float prog = -1f; bool done = false;
                for (int k = 0; k < reqs.Count; k++)
                {
                    if (reqs[k].ApplianceId != a.Id) continue;
                    done = reqs[k].Done;
                    prog = reqs[k].DurationTicks > 0 ? Mathf.Clamp01((float)reqs[k].ProgressTicks / reqs[k].DurationTicks) : 0f;
                    break;
                }
                bool plugged = !string.IsNullOrEmpty(a.SocketId);
                v.Refresh(a, prog, done, plugged && house.IsDark(a.SocketId));
                if (plugged) house.PlaceChip(v, a.SocketId);
                else tray.PlaceChip(v, slot++);
            }
            tray.Refresh(sim.CordsLeft);

            for (int i = 0; i < breakers.Count && i < sim.Circuits.Count; i++) breakers[i].Refresh(sim.Circuits[i]);
            for (int i = 0; i < cards.Count && i < reqs.Count; i++)
            {
                string rn;
                if (!roomNames.TryGetValue(reqs[i].RoomId, out rn)) rn = reqs[i].RoomId;
                cards[i].Refresh(reqs[i], sim.Now, rn);
            }

            var ph = sim.Phase;
            bannerIntro.gameObject.SetActive(ph == LevelPhase.Planning);
            bannerHint.gameObject.SetActive(ph == LevelPhase.Planning || ph == LevelPhase.Paused);
            pauseLabel.text = ph == LevelPhase.Running ? "Pause (Space)" : "Start (Space)";
            pauseBtn.interactable = ph != LevelPhase.Won && ph != LevelPhase.Lost;
        }

        // ---------------- เหตุการณ์จากซิม ----------------
        void HandleEvents(IReadOnlyList<SimEvent> evs)
        {
            for (int i = 0; i < evs.Count; i++)
            {
                var e = evs[i];
                fb.Handle(e, Ctrl.Sim);
                if (e.Type == SimEventType.LevelWon) OnWon();
                else if (e.Type == SimEventType.LevelLost) OnLost(e);
            }
        }

        void OnWon()
        {
            var sim = Ctrl.Sim;
            Progress.Unlock(index + 1);
            Progress.SaveTime(index, sim.Now / 10f);
            string t = Fmt(sim.Now);
            result.Show(true, "Everyone's happy! Finished at " + t + ".", t, sim.TripCount, true,
                () => root.ShowLevel(index), Next, root.ShowLevelSelect);
        }

        void OnLost(SimEvent e)
        {
            var sim = Ctrl.Sim;
            string cause = string.IsNullOrEmpty(e.Message) ? "The day is over." : e.Message;
            result.Show(false, cause, Fmt(sim.Now), sim.TripCount, false,
                () => root.ShowLevel(index), null, root.ShowLevelSelect);
        }

        void Next()
        {
            if (index < GameRoot.LevelCount) root.ShowLevel(index + 1);
            else root.ShowEnd();
        }

        void OnEsc()
        {
            if (!string.IsNullOrEmpty(Ctrl.PendingCordSource)) Ctrl.PendingCordSource = null;
            else root.ShowLevelSelect();
        }

        // ---------------- API ให้ View เรียก ----------------
        public void Submit(Command c) { Ctrl.Submit(c); }

        public void RoomClicked(string roomId)
        {
            var src = Ctrl.PendingCordSource;
            if (string.IsNullOrEmpty(src)) return;
            Ctrl.PendingCordSource = null;
            Ctrl.Submit(Cmds.Cord(src, roomId));
        }

        public void EndDragAppliance(ApplianceView v, PointerEventData e)
        {
            string target = SocketUnder(e, v);
            if (target != null) { if (target != v.SocketId) Ctrl.Submit(Cmds.Plug(v.Id, target)); }
            else if (!string.IsNullOrEmpty(v.SocketId)) Ctrl.Submit(Cmds.Unplug(v.Id));
        }

        // ลากสายพ่วงไปปล่อยบนปลั๊กต้นทาง -> เข้าโหมดเลือกห้อง (ห้องข้างเคียงไฮไลต์) -> คลิกห้องปลายทาง
        public void EndDragCord(PointerEventData e)
        {
            string src = SocketUnder(e, null);
            if (src == null) { Ctrl.PendingCordSource = null; return; }
            Ctrl.PendingCordSource = src;
            fb.Toast("Now click the room next door.");
        }

        string SocketUnder(PointerEventData e, ApplianceView ignore)
        {
            hits.Clear();
            EventSystem.current.RaycastAll(e, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                var go = hits[i].gameObject;
                var sv = go.GetComponentInParent<SocketView>();
                if (sv != null) return sv.Id;
                var av = go.GetComponentInParent<ApplianceView>();
                if (av != null && av != ignore && !string.IsNullOrEmpty(av.SocketId)) return av.SocketId;
            }
            return null;
        }

        static string Fmt(int ticks)
        {
            int s = ticks / 10;
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}