using System;
using System.Collections.Generic;
using Trouble.Sim;

namespace Trouble.Game
{
    public sealed class LevelController
    {
        public HouseSim Sim { get; private set; }
        public LevelData Data { get; private set; }
        public string PendingCordSource;            // ปลั๊กต้นทางสายพ่วงที่รอเลือกห้อง

        public event Action<IReadOnlyList<SimEvent>> StepEvents;
        public event Action<string> Rejected;

        float acc;

        public LevelController(LevelData data)
        {
            Data = data;
            Sim = new HouseSim(data);
        }

        public void TogglePause()
        {
            switch (Sim.Phase)
            {
                case LevelPhase.Running: Sim.SetRunning(false); break;
                case LevelPhase.Planning:
                case LevelPhase.Paused: acc = 0f; Sim.SetRunning(true); break;
            }
        }

        public bool Submit(Command c)
        {
            if (Sim.Phase == LevelPhase.Won || Sim.Phase == LevelPhase.Lost) return false;
            if (Sim.Phase == LevelPhase.Running) { Sim.Enqueue(c); return true; }
            bool ok = Sim.TryApply(c, out var reason);
            if (!ok && Rejected != null) Rejected(reason);
            return ok;
        }

        public void Tick(float dt)
        {
            if (Sim.Phase != LevelPhase.Running) return;
            acc += dt;
            int steps = 0;
            while (acc >= 0.1f && steps < 5)
            {
                acc -= 0.1f;
                steps++;
                var ev = Sim.Step();
                if (StepEvents != null) StepEvents(ev);
                if (Sim.Phase != LevelPhase.Running) break;
            }
            if (steps >= 5) acc = 0f;
        }
    }
}