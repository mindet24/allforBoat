using System.Collections.Generic;
using System.Text;
using Trouble.Sim;

namespace Trouble.Game
{
    /// <summary>
    /// The last N sim events as one-line texts, newest last (F1 panel). Plain C#; the panel is the only user in
    /// release code paths that are compiled out, but the log itself is cheap and testable.
    /// </summary>
    public sealed class EventLog
    {
        private readonly Queue<string> _lines = new Queue<string>();
        private readonly int _capacity;

        /// <param name="capacity">How many lines to keep (the panel shows 30).</param>
        public EventLog(int capacity = 30)
        {
            _capacity = capacity < 1 ? 1 : capacity;
        }

        /// <summary>Lines kept now, oldest first.</summary>
        public IEnumerable<string> Lines => _lines;

        /// <summary>Number of lines kept.</summary>
        public int Count => _lines.Count;

        /// <summary>Adds one line for the event, dropping the oldest when full.</summary>
        public void Add(SimEvent e)
        {
            _lines.Enqueue(Format(e));
            while (_lines.Count > _capacity) _lines.Dequeue();
        }

        /// <summary>Forgets every line.</summary>
        public void Clear() => _lines.Clear();

        /// <summary>"t12 CircuitTripped A 12/10" - the tick, the type and whatever ids the event carries.</summary>
        public static string Format(SimEvent e)
        {
            var sb = new StringBuilder();
            sb.Append('t').Append(e.Tick).Append(' ').Append(e.Type);
            if (!string.IsNullOrEmpty(e.Circuit)) sb.Append(' ').Append(e.Circuit);
            if (!string.IsNullOrEmpty(e.Socket)) sb.Append(' ').Append(e.Socket);
            if (!string.IsNullOrEmpty(e.Appliance)) sb.Append(' ').Append(e.Appliance);
            if (!string.IsNullOrEmpty(e.Room)) sb.Append(' ').Append(e.Room);
            if (!string.IsNullOrEmpty(e.Door)) sb.Append(' ').Append(e.Door);
            if (e.Capacity > 0) sb.Append(' ').Append(e.Load).Append('/').Append(e.Capacity);
            if (!string.IsNullOrEmpty(e.Message)) sb.Append(" \"").Append(e.Message).Append('"');
            return sb.ToString();
        }
    }
}
