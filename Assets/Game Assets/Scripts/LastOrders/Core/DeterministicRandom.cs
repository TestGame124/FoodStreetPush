using System;

namespace LastOrders.Core
{
    /// <summary>
    /// A small, explicit PRNG so that anything the player is told in advance can be
    /// made to come true.
    ///
    /// The forecast in §16.1 is a promise: the radio says "Burners tonight" at dawn and
    /// the WaveDirector has to honour it at dusk. That only works if the wave is a pure
    /// function of (day, Heat, seed) — which in turn only works if the generator is not
    /// UnityEngine.Random, whose state is global, shared with every particle system in
    /// the scene, and different between a play session and a reload.
    ///
    /// xorshift128 rather than System.Random because it is a value type with a visible,
    /// serialisable four-integer state, so a night can be replayed exactly by storing
    /// four ints in the save file.
    /// </summary>
    [Serializable]
    public struct DeterministicRandom
    {
        uint x, y, z, w;

        public DeterministicRandom(int seed)
        {
            // Splitmix-style avalanche on the way in. Seeding the state directly from a
            // small integer leaves the first several outputs strongly correlated with it,
            // which shows up as day 1 and day 2 drawing near-identical waves.
            uint s = (uint)seed;
            x = Scramble(ref s);
            y = Scramble(ref s);
            z = Scramble(ref s);
            w = Scramble(ref s);

            // An all-zero state is a fixed point of xorshift and only ever emits zero.
            if ((x | y | z | w) == 0u) x = 0x9E3779B9u;
        }

        static uint Scramble(ref uint s)
        {
            s += 0x9E3779B9u;
            uint r = s;
            r = (r ^ (r >> 16)) * 0x85EBCA6Bu;
            r = (r ^ (r >> 13)) * 0xC2B2AE35u;
            return r ^ (r >> 16);
        }

        /// <summary>
        /// Builds a generator for one specific decision. Two systems that want independent
        /// streams from the same night pass different <paramref name="salt"/> values, so the
        /// wave composition and the Dispatch outcomes cannot correlate with each other.
        /// </summary>
        public static DeterministicRandom ForNight(int day, int heat, int campaignSeed, int salt = 0)
        {
            unchecked
            {
                int seed = campaignSeed;
                seed = seed * 486187739 + day;
                seed = seed * 486187739 + heat;
                seed = seed * 486187739 + salt;
                return new DeterministicRandom(seed);
            }
        }

        /// <summary>
        /// A hash that is the same in every process, on every platform, forever.
        ///
        /// string.GetHashCode is explicitly not that: .NET randomises it per process, so a
        /// value seeded from one would produce a different result after every restart. Any
        /// determinism that has to survive a save file being closed and reopened - a pending
        /// Dispatch outcome, a night replayed from a snapshot - has to seed from this instead.
        /// </summary>
        public static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            unchecked
            {
                // FNV-1a, 32-bit.
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }

        public uint NextUInt()
        {
            uint t = x ^ (x << 11);
            x = y; y = z; z = w;
            w = w ^ (w >> 19) ^ t ^ (t >> 8);
            return w;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float minInclusive, float maxExclusive) =>
            minInclusive + NextFloat() * (maxExclusive - minInclusive);

        /// <summary>Uniform in [minInclusive, maxExclusive). Returns min when the range is empty.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            int span = maxExclusive - minInclusive;
            if (span <= 0) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)span);
        }

        public bool Chance(float probability) => NextFloat() < probability;

        /// <summary>
        /// Picks an index from a weight table. Returns -1 when every weight is zero, which
        /// callers should read as "this table has nothing to offer tonight" rather than
        /// silently defaulting to entry zero.
        /// </summary>
        public int WeightedIndex(IReadOnlyWeights weights)
        {
            float total = 0f;
            int count = weights.Count;
            for (int i = 0; i < count; i++) total += weights.WeightAt(i);
            if (total <= 0f) return -1;

            float roll = NextFloat() * total;
            for (int i = 0; i < count; i++)
            {
                roll -= weights.WeightAt(i);
                if (roll <= 0f) return i;
            }
            return count - 1;
        }
    }

    /// <summary>Lets <see cref="DeterministicRandom.WeightedIndex"/> read weights out of any
    /// container without forcing callers to allocate a float[] to ask a question.</summary>
    public interface IReadOnlyWeights
    {
        int Count { get; }
        float WeightAt(int index);
    }
}
