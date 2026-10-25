using System;
using System.Threading.Tasks;
using Trouble.Sim;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Trouble.Game
{
    /// <summary>
    /// Two-player co-op over Unity Relay: a Multiplayer Services session (join code = room code) carries a Netcode
    /// connection, and the day is played in lockstep. The host's sim is the clock: every command it applies (its own
    /// and the guest's, as sim player 1) and every tick it runs is sent to the guest in order, and the guest's sim
    /// applies exactly the same sequence, so both houses stay identical without sending any state.
    /// </summary>
    /// <remarks>
    /// Needs the project linked to Unity Cloud with Relay and Lobby on. Main thread only (the async calls continue on
    /// Unity's synchronization context).
    /// <code>
    /// var room = gameObject.AddComponent&lt;NetRoom&gt;();
    /// room.Setup(levelController, onStart: () => levelController.LoadLevel(LevelLoader.DemoLevel));
    /// room.Create("Weerak's Room");   // or room.Join("7F4K3P");
    /// </code>
    /// </remarks>
    public sealed class NetRoom : MonoBehaviour
    {
        private const string Channel = "tripped";
        private enum Op : byte { Start = 1, Command = 2, Tick = 3, GuestCommand = 4, Pick = 5, Phase = 6 }

        private LevelController _controller;
        private Action _startDay;
        private ISession _session;
        private NetworkManager _net;
        private bool _signedIn;
        private bool _busy;
        private bool _playing;
        private string _guestArt = "";
        private LevelPhase _sentPhase;

        public string Name { get; private set; } = "";
        public string Code => _session?.Code ?? "";
        public bool IsHost => _session == null || _session.IsHost;
        public int Players => _session?.PlayerCount ?? 1;
        public string Status { get; private set; } = "";
        /// <summary>Raised whenever the name, code, players or status changed.</summary>
        public event Action Changed;

        /// <summary>True once a guest is in the room (then the day is played together).</summary>
        public bool HasGuest => _session != null && _session.PlayerCount > 1 && _net != null && _net.IsListening
                                && (!_net.IsServer || _net.ConnectedClientsIds.Count > 1);

        /// <summary>
        /// Hooks the room to the controller. <paramref name="startDay"/> loads the day level; it runs on both machines
        /// when the host presses Ready.
        /// </summary>
        public void Setup(LevelController controller, Action startDay)
        {
            _controller = controller;
            _startDay = startDay;
        }

        public async void Create(string name)
        {
            Name = string.IsNullOrEmpty(name) ? "My Room" : name;
            if (_busy || _session != null) { Changed?.Invoke(); return; }
            await Run("Making the room…", async () =>
            {
                var options = new SessionOptions { MaxPlayers = 2, Name = Name, IsPrivate = true }.WithRelayNetwork();
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                Watch();
                Say("");
            });
        }

        public async void Join(string code)
        {
            if (string.IsNullOrEmpty(code)) { Say("Type a room code first."); return; }
            if (_busy) return;
            await Run("Joining " + code + "…", async () =>
            {
                if (_session != null) await LeaveSession();
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                Name = _session.Name;
                Debug.Log($"[TRIPPED net] joined session {_session.Id} players {_session.PlayerCount} listening {_net.IsListening} client {_net.IsClient}");
                Watch();
                Say("Joined! Waiting for the host to press Ready.");
                SendPick();
            });
        }

        public void Rename(string name)
        {
            Name = name;
            Changed?.Invoke();
        }

        public async void Leave()
        {
            await LeaveSession();
            Say("");
        }

        /// <summary>The local player picked a character: the guest tells the host (it is drawn on both machines).</summary>
        public void PickChanged() => SendPick();

        /// <summary>
        /// Ready: the host starts the day on both machines (alone if nobody joined); the guest waits for the host.
        /// </summary>
        public void Ready()
        {
            if (_session == null || !HasGuest || _net == null || !_net.IsListening)
            {
                Unhook();
                _startDay?.Invoke();
                return;
            }
            if (!IsHost)
            {
                Say("Waiting for the host to press Ready…");
                return;
            }
            var w = new FastBufferWriter(256, Allocator.Temp);
            using (w)
            {
                w.WriteValueSafe((byte)Op.Start);
                w.WriteValueSafe(PlayerCharacter.Selected);
                w.WriteValueSafe(string.IsNullOrEmpty(_guestArt) ? Other(PlayerCharacter.Selected) : _guestArt);
                _net.CustomMessagingManager.SendNamedMessageToAll(Channel, w, NetworkDelivery.ReliableSequenced);
            }
            BeginDay(0, PlayerCharacter.Selected, string.IsNullOrEmpty(_guestArt) ? Other(PlayerCharacter.Selected) : _guestArt);
        }

        // ---- the day in lockstep -------------------------------------------------------------------------

        private void BeginDay(int localPlayer, string hostArt, string guestArt)
        {
            _controller.Players = 2;
            _controller.LocalPlayer = localPlayer;
            _controller.PlayerArt = new[] { hostArt, guestArt };
            _controller.ExternalClock = localPlayer != 0;
            _controller.CommandRouter = localPlayer != 0 ? SendGuestCommand : (Func<Command, bool>)null;
            _playing = true;
            _sentPhase = LevelPhase.Planning;
            _startDay?.Invoke();
            if (localPlayer == 0)
            {
                _controller.CommandAccepted += OnHostCommand;
                _controller.TickEvents += OnHostTick;
            }
        }

        /// <summary>Back to single player (leaving the room or a solo Ready).</summary>
        private void Unhook()
        {
            if (_controller == null) return;
            _controller.CommandAccepted -= OnHostCommand;
            _controller.TickEvents -= OnHostTick;
            _controller.Players = 1;
            _controller.LocalPlayer = 0;
            _controller.PlayerArt = new string[0];
            _controller.ExternalClock = false;
            _controller.CommandRouter = null;
            _playing = false;
        }

        /// <summary>
        /// A short fingerprint of the co-op game state (tick, every player's place and hands, score): equal on both
        /// machines while the lockstep holds.
        /// </summary>
        public static int Checksum(HouseSim sim)
        {
            unchecked
            {
                int h = sim.Now * 31 + sim.Score;
                foreach (var c in sim.Characters)
                    h = h * 397 ^ c.XCm ^ (c.ZCm << 8) ^ c.RoomId.GetHashCode() ^ (c.CarryingId ?? "").GetHashCode();
                return h;
            }
        }

        private void LogChecksum()
        {
            var sim = _controller.Sim;
            if (_playing && sim != null && sim.Now % 50 == 0)
                Debug.Log($"[TRIPPED co-op] {(IsHost ? "host" : "guest")} tick {sim.Now} x0 {sim.Characters[0].XCm} x1 {(sim.Characters.Count > 1 ? sim.Characters[1].XCm : -1)} hash {Checksum(sim)}");
        }

        private void OnHostCommand(Command command)
        {
            SyncPhase();
            SendCommand(command, _controller.Sim != null ? _controller.Sim.Now : 0);
        }

        private void OnHostTick(System.Collections.Generic.IReadOnlyList<SimEvent> events)
        {
            SyncPhase();
            LogChecksum();
            if (_net == null || !_net.IsListening || _controller.Sim == null) return;
            var w = new FastBufferWriter(16, Allocator.Temp);
            using (w)
            {
                w.WriteValueSafe((byte)Op.Tick);
                w.WriteValueSafe(_controller.Sim.Now);
                _net.CustomMessagingManager.SendNamedMessageToAll(Channel, w, NetworkDelivery.ReliableSequenced);
            }
        }

        private void Update() => SyncPhase();

        /// <summary>
        /// Host: the start and pauses are not commands, so the phase is sent whenever it changed, and always before a
        /// command or tick (a walk sent in the frame the day starts must not reach a guest still in planning).
        /// </summary>
        private void SyncPhase()
        {
            if (!_playing || !IsHost || _controller == null || _controller.Sim == null || _net == null || !_net.IsListening) return;
            var phase = _controller.Sim.Phase;
            if (phase == _sentPhase) return;
            _sentPhase = phase;
            var w = new FastBufferWriter(16, Allocator.Temp);
            using (w)
            {
                w.WriteValueSafe((byte)Op.Phase);
                w.WriteValueSafe((int)phase);
                _net.CustomMessagingManager.SendNamedMessageToAll(Channel, w, NetworkDelivery.ReliableSequenced);
            }
        }

        private void SendCommand(Command c, int tick)
        {
            if (_net == null || !_net.IsListening) return;
            var w = new FastBufferWriter(512, Allocator.Temp);
            using (w)
            {
                w.WriteValueSafe((byte)Op.Command);
                WriteCommand(ref w, c);
                _net.CustomMessagingManager.SendNamedMessageToAll(Channel, w, NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>Guest: a key press goes to the host as player 1's; it comes back as a Command if accepted.</summary>
        private bool SendGuestCommand(Command c)
        {
            if (_net == null || !_net.IsListening) return false;
            var w = new FastBufferWriter(512, Allocator.Temp);
            using (w)
            {
                w.WriteValueSafe((byte)Op.GuestCommand);
                WriteCommand(ref w, c);
                _net.CustomMessagingManager.SendNamedMessage(Channel, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced);
            }
            return true;
        }

        private void SendPick()
        {
            if (_net == null || !_net.IsListening || IsHost) return;
            var w = new FastBufferWriter(64, Allocator.Temp);
            using (w)
            {
                w.WriteValueSafe((byte)Op.Pick);
                w.WriteValueSafe(PlayerCharacter.Selected);
                _net.CustomMessagingManager.SendNamedMessage(Channel, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced);
            }
        }

        private void OnMessage(ulong sender, FastBufferReader r)
        {
            r.ReadValueSafe(out byte op);
            var sim = _controller.Sim;
            switch ((Op)op)
            {
                case Op.Start:
                    r.ReadValueSafe(out string hostArt);
                    r.ReadValueSafe(out string guestArt);
                    if (!IsHost) BeginDay(1, hostArt, guestArt);
                    break;
                case Op.Pick:
                    r.ReadValueSafe(out string pick);
                    _guestArt = pick;
                    Say("A friend joined as the " + (pick == PlayerCharacter.Boy ? "Boy" : "Girl") + ". Press Ready to play together!");
                    break;
                case Op.GuestCommand:
                    if (!IsHost || sim == null) break;
                    var guest = ReadCommand(ref r).ForPlayer(1);
                    if (sim.Submit(guest, out _)) SendCommand(guest, sim.Now);
                    break;
                case Op.Command:
                    if (IsHost || sim == null) break;
                    sim.Submit(ReadCommand(ref r), out _);
                    break;
                case Op.Tick:
                    r.ReadValueSafe(out int tick);
                    if (IsHost || sim == null) break;
                    while (sim.Now < tick && sim.Phase == LevelPhase.Running)
                    {
                        _controller.StepTicks(1);
                        LogChecksum();
                    }
                    break;
                case Op.Phase:
                    r.ReadValueSafe(out int phase);
                    if (IsHost || sim == null) break;
                    var p = (LevelPhase)phase;
                    if (p == LevelPhase.Running && sim.Phase == LevelPhase.Planning) sim.Start();
                    else if (p == LevelPhase.Paused && sim.Phase == LevelPhase.Running) sim.SetPaused(true);
                    else if (p == LevelPhase.Running && sim.Phase == LevelPhase.Paused) sim.SetPaused(false);
                    break;
            }
        }

        private static void WriteCommand(ref FastBufferWriter w, Command c)
        {
            w.WriteValueSafe((int)c.Type);
            w.WriteValueSafe(c.A ?? "");
            w.WriteValueSafe(c.B ?? "");
            w.WriteValueSafe(c.On);
            w.WriteValueSafe(c.Append);
            w.WriteValueSafe(c.Index);
            w.WriteValueSafe(c.Player);
        }

        private static Command ReadCommand(ref FastBufferReader r)
        {
            r.ReadValueSafe(out int type);
            r.ReadValueSafe(out string a);
            r.ReadValueSafe(out string b);
            r.ReadValueSafe(out bool on);
            r.ReadValueSafe(out bool append);
            r.ReadValueSafe(out int index);
            r.ReadValueSafe(out int player);
            return new Command((CommandType)type, a, b, on, append, index, player);
        }

        private static string Other(string id) => id == PlayerCharacter.Boy ? PlayerCharacter.Girl : PlayerCharacter.Boy;

        // ---- services and the session --------------------------------------------------------------------

        private async Task Run(string working, Func<Task> work)
        {
            _busy = true;
            Say(working);
            try
            {
                await SignIn();
                await work();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TRIPPED] Online room: " + e);
                Say("Online play failed: " + Short(e.Message));
            }
            finally
            {
                _busy = false;
                Changed?.Invoke();
            }
        }

        private async Task SignIn()
        {
            EnsureNetworkManager();
            if (_signedIn) return;
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            _signedIn = true;
        }

        /// <summary>Makes the Netcode NetworkManager (with the Relay-ready transport) the session starts.</summary>
        private void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null)
            {
                _net = NetworkManager.Singleton;
                return;
            }
            var go = new GameObject("Network");
            DontDestroyOnLoad(go);
            go.SetActive(false);
            var transport = go.AddComponent<UnityTransport>();
            _net = go.AddComponent<NetworkManager>();
            if (_net.NetworkConfig == null) _net.NetworkConfig = new NetworkConfig();
            _net.NetworkConfig.NetworkTransport = transport;
            _net.NetworkConfig.ConnectionApproval = false;
            // No networked scenes or objects: the lockstep messages are all that is sent.
            _net.NetworkConfig.EnableSceneManagement = false;
            go.SetActive(true);
            _net.OnClientStarted += Register;
            _net.OnServerStarted += Register;
            _net.OnClientConnectedCallback += id =>
            {
                Debug.Log("[TRIPPED net] connected " + id);
                if (_net.IsServer && id != NetworkManager.ServerClientId) Say("A friend joined! Press Ready to play together.");
            };
            _net.OnClientDisconnectCallback += id => Debug.Log("[TRIPPED net] disconnected " + id + " reason '" + _net.DisconnectReason + "'");
            _net.OnTransportFailure += () => Debug.LogWarning("[TRIPPED net] transport failure");
        }

        private void Register()
        {
            Debug.Log($"[TRIPPED net] network started host {_net.IsHost} client {_net.IsClient}");
            _net.CustomMessagingManager?.UnregisterNamedMessageHandler(Channel);
            _net.CustomMessagingManager?.RegisterNamedMessageHandler(Channel, OnMessage);
        }

        private void Watch()
        {
            _session.PlayerJoined += _ => { if (IsHost && !HasGuest) Say("A friend is joining…"); };
            _session.PlayerHasLeft += _ => { Say("Your friend left the room."); if (_playing) Unhook(); };
            _session.RemovedFromSession += () => { Debug.Log("[TRIPPED net] removed from session"); _session = null; Say("The room closed."); if (_playing) Unhook(); };
            _session.Changed += () => Changed?.Invoke();
        }

        private async Task LeaveSession()
        {
            Unhook();
            var s = _session;
            _session = null;
            if (s != null)
            {
                try { await s.LeaveAsync(); }
                catch (Exception e) { Debug.LogWarning("[TRIPPED] Leaving the room: " + e.Message); }
            }
            Changed?.Invoke();
        }

        private void Say(string status)
        {
            Status = status ?? "";
            Changed?.Invoke();
        }

        private static string Short(string message)
        {
            if (string.IsNullOrEmpty(message)) return "unknown error";
            int nl = message.IndexOf('\n');
            if (nl > 0) message = message.Substring(0, nl);
            return message.Length > 90 ? message.Substring(0, 90) + "…" : message;
        }

        private void OnDestroy()
        {
            Unhook();
            _session?.LeaveAsync();
        }
    }
}
