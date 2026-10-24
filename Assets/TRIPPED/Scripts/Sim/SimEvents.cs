using System.Collections.Generic;

namespace Trouble.Sim
{
    public enum SimEventType
    {
        OverloadStarted,
        OverloadCleared,
        CircuitTripped,
        MainTripped,
        BreakerSwitched,
        SocketRevealed,
        ApplianceInterrupted,
        RequestCompleted,
        FridgeSpoiled,
        CommandRejected,
        LevelWon,
        LevelLost,
        /// <summary>The character crossed into a new room.</summary>
        CharacterEnteredRoom,
        /// <summary>The character finished an action from its queue.</summary>
        ActionDone,
        DoorToggled,
        /// <summary>The set of visible rooms changed.</summary>
        SightChanged,
        /// <summary>Day mode: a new request appeared (Appliance, Room).</summary>
        RequestSpawned,
        /// <summary>Day mode: a request was not started in time (Appliance, Room).</summary>
        RequestMissed,
        /// <summary>Day mode: a heart was lost (by a blackout or a missed urgent request).</summary>
        HeartLost,
        /// <summary>Day mode: the character took an appliance out of its socket to carry it (Appliance, Socket).</summary>
        PickedUp,
        /// <summary>Day mode: the character put what it carried down beside a socket or on the floor, unplugged (Appliance, Socket or Room).</summary>
        PutDown,
        /// <summary>Day mode: the character threw what it carried (Appliance, Socket or Room where it landed).</summary>
        Thrown,
        /// <summary>Day mode: an appliance was plugged in with F (Appliance, Socket).</summary>
        PluggedIn,
        /// <summary>Day mode: an appliance was unplugged with F (Appliance, Socket).</summary>
        Unplugged,
    }

    /// <summary>One appliance that adds to a load sum, for messages like "Kettle 6 + Toaster 4 = 10 > 8".</summary>
    public readonly struct LoadTerm
    {
        public readonly string ApplianceId;
        public readonly int Load;

        public LoadTerm(string applianceId, int load)
        {
            ApplianceId = applianceId;
            Load = load;
        }
    }

    /// <summary>Something the sim tells the view about. The view turns these into sound, animation and text.</summary>
    public struct SimEvent
    {
        public SimEventType Type;
        /// <summary>
        /// The tick being processed when the event happened (a trip "at tick 9").
        /// LevelWon, LevelLost and FridgeSpoiled come after the clock moves, so theirs is the end tick.
        /// </summary>
        public int Tick;
        public string Circuit;
        public string Socket;
        public string Appliance;
        public string Room;
        public string Door;
        public int Load;
        public int Capacity;
        /// <summary>Parts of the load sum, sorted by socket id.</summary>
        public List<LoadTerm> Terms;
        public bool ProgressLost;
        public bool AssumptionWasWrong;
        /// <summary>CircuitTripped only: the player had reset this breaker while it was still hot.</summary>
        public bool WasHot;
        public LoseReason Reason;
        /// <summary>Rejection text for CommandRejected.</summary>
        public string Message;
        public Command Command;
    }
}
