using System;
using Trouble.Sim;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Two-machine co-op check, off unless the player is started with <c>--join CODE</c>: it joins that room as the
    /// Girl, and once the day runs it walks right for a while and back (sim player 1). Both machines log
    /// <see cref="NetRoom.Checksum"/> every 50 ticks ("[TRIPPED co-op]"), so the two logs show whether the houses match.
    /// </summary>
    /// <code>
    /// Tripped.exe -logFile guest.log --join L8PHFT
    /// </code>
    public sealed class CoopSmokeTest : MonoBehaviour
    {
        private string _code;
        private NetRoom _room;
        private LevelController _controller;
        private bool _joined;
        private int _walkFrom = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "--join") continue;
                var go = new GameObject(nameof(CoopSmokeTest));
                DontDestroyOnLoad(go);
                go.AddComponent<CoopSmokeTest>()._code = args[i + 1];
                Application.runInBackground = true;
                return;
            }
        }

        private void Update()
        {
            if (_room == null) _room = FindFirstObjectByType<NetRoom>();
            if (_controller == null) _controller = FindFirstObjectByType<LevelController>();
            if (_room == null || _controller == null) return;
            if (!_joined && Time.realtimeSinceStartup > 3f)
            {
                _joined = true;
                PlayerCharacter.Selected = PlayerCharacter.Girl;
                Debug.Log("[TRIPPED co-op] joining " + _code);
                _room.Join(_code);
            }
            var sim = _controller.Sim;
            if (sim == null || !_controller.ExternalClock || sim.Phase != LevelPhase.Running) return;
            if (_walkFrom < 0)
            {
                _walkFrom = sim.Now;
                _controller.Submit(Command.Walk(1));
            }
            else if (sim.Now == _walkFrom + 20) _controller.Submit(Command.Walk(-1));
            else if (sim.Now == _walkFrom + 40) _controller.Submit(Command.Walk(0));
        }
    }
}
