namespace Trouble.Game
{
    /// <summary>
    /// What a socket and its wall wire may show the player. The one rule that must never break: a circuit colour
    /// appears only once the socket is revealed (GDD v1.1 §2.4, decision Q28). An assumed circuit (level 4 labels)
    /// is shown only as a dashed outline, never as a filled face or a lit wire.
    /// </summary>
    public readonly struct SocketLook
    {
        /// <summary>Text on the socket face: the circuit id when revealed, "?" otherwise.</summary>
        public readonly string Text;
        /// <summary>Circuit whose colour fills the face and lights the wire. Empty = grey.</summary>
        public readonly string FillCircuit;
        /// <summary>Circuit whose colour draws a dashed outline (what the labels claim). Empty = none.</summary>
        public readonly string DashedCircuit;

        public SocketLook(string text, string fillCircuit, string dashedCircuit)
        {
            Text = text;
            FillCircuit = fillCircuit;
            DashedCircuit = dashedCircuit;
        }

        /// <summary>The look for a socket from what the player knows.</summary>
        public static SocketLook For(bool revealed, string circuitId, string assumedCircuitId)
        {
            if (revealed) return new SocketLook(circuitId ?? "", circuitId ?? "", "");
            return new SocketLook("?", "", assumedCircuitId ?? "");
        }
    }
}
