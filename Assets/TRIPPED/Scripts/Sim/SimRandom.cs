using System;

namespace Trouble.Sim
{
    /// <summary>
    /// Small seeded random numbers (xorshift32). Same seed, same numbers on every platform and .NET version,
    /// which System.Random does not promise. Replays and tests rely on that.
    /// </summary>
    public sealed class SimRandom
    {
        private uint _state;

        /// <param name="seed">Any value; 0 is mapped to a fixed non-zero state.</param>
        public SimRandom(int seed)
        {
            _state = seed == 0 ? 0x9E3779B9u : (uint)seed;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>A whole number from min to max, both included.</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextUInt() % (uint)(max - min + 1));
        }

        /// <summary>A number in [0, 1).</summary>
        public double NextDouble() => NextUInt() / 4294967296.0;
    }
}
