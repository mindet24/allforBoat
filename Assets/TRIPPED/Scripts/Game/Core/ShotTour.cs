using System;
using System.IO;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Store-page screenshots, off unless the player is started with <c>--shots FOLDER</c>: it walks through the game by
    /// itself (title, lobby, the day starting, carrying an appliance, the overview, and a busy moment)
    /// and saves a PNG of each at the window's resolution, then quits.
    /// </summary>
    /// <code>
    /// Tripped.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 --shots C:/shots
    /// </code>
    public sealed class ShotTour : MonoBehaviour
    {
        private string _folder;
        private float _stepAt;
        private int _step;
        private LevelController _lc;
        private ScreenFlow _flow;
        private NetRoom _room;
        private float _searchUntil;
        private int _dir = 1;
        private GameObject _menus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "--shots") continue;
                Begin(args[i + 1]);
                return;
            }
        }

        /// <summary>Starts the tour now, saving into <paramref name="folder"/> (also usable from the editor in play mode).</summary>
        public static ShotTour Begin(string folder)
        {
            var go = new GameObject(nameof(ShotTour));
            DontDestroyOnLoad(go);
            var tour = go.AddComponent<ShotTour>();
            tour._folder = folder;
            tour._stepAt = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            Directory.CreateDirectory(folder);
            return tour;
        }

        /// <summary>Which step the tour is on (for checks).</summary>
        public int Step => _step;

        private void Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"));
            Debug.Log("[TRIPPED shots] " + name);
        }

        private bool Wait(float seconds) => Time.realtimeSinceStartup < _stepAt + seconds;

        private void Next()
        {
            _step++;
            _stepAt = Time.realtimeSinceStartup;
            Debug.Log("[TRIPPED shots] step " + _step);
        }

        private void Update()
        {
            if (_lc == null) _lc = FindFirstObjectByType<LevelController>();
            if (_flow == null) _flow = FindFirstObjectByType<ScreenFlow>();
            if (_room == null) _room = FindFirstObjectByType<NetRoom>();
            if (_lc == null || _flow == null) return;
            var sim = _lc.Sim;
            switch (_step)
            {
                case 0:
                    if (Wait(5f)) break;
                    // First the scene alone (menus hidden), for the store cover; then the real title.
                    _menus = GameObject.Find("Menus");
                    if (_menus != null) _menus.SetActive(false);
                    Shot("00_scene");
                    _step = 100;
                    _stepAt = Time.realtimeSinceStartup;
                    break;
                case 100:
                    if (Wait(0.5f)) break;
                    if (_menus != null) _menus.SetActive(true);
                    _step = 101;
                    _stepAt = Time.realtimeSinceStartup;
                    break;
                case 101: if (!Wait(1f)) { Shot("01_title"); _step = 0; Next(); } break;
                case 1: if (!Wait(1f)) { PlayerCharacter.Selected = PlayerCharacter.Boy; _flow.ShowLobby(); Next(); } break;
                case 2: if (!Wait(15f) || (_room != null && _room.Code.Length > 0 && !Wait(2f))) { Shot("02_lobby"); Next(); } break;
                case 3: if (!Wait(1f)) { if (_room != null) _room.Ready(); else _flow.StartDemo(); Next(); } break;
                case 4:
                    if (Wait(3f) || sim == null) break;
                    if (sim.Phase == LevelPhase.Planning) _lc.StartOrTogglePause();
                    _lc.Submit(Command.Walk(1));
                    Next();
                    break;
                case 5: if (!Wait(2f)) { _lc.Submit(Command.Walk(0)); Next(); } break;
                case 6: if (!Wait(5f)) { Shot("03_day"); _searchUntil = Time.realtimeSinceStartup + 10f; Next(); } break;
                case 7:
                    // Walk until an appliance is in reach, pick it up.
                    if (sim == null) break;
                    if (DayInteraction.InteractTarget(sim).Kind == DayTargetKind.Appliance || Time.realtimeSinceStartup > _searchUntil)
                    {
                        _lc.Submit(Command.Walk(0));
                        _lc.Submit(Command.Interact());
                        Next();
                    }
                    else if (sim.Character.WalkDir == 0 || Time.frameCount % 240 == 0)
                    {
                        _dir = -_dir;
                        _lc.Submit(Command.Walk(_dir));
                    }
                    break;
                case 8: if (!Wait(1.5f)) { Shot("04_carry"); Next(); } break;
                case 9: if (!Wait(0.5f)) { _lc.HouseCam.ForceOverview = true; Next(); } break;
                case 10: if (!Wait(2f)) { Shot("05_overview"); _lc.HouseCam.ForceOverview = false; Next(); } break;
                case 11: if (!Wait(14f)) { Shot("06_busy"); Next(); } break;
                case 12: if (!Wait(2f)) Application.Quit(); break;
            }
        }
    }
}
