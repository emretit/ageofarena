/// <summary>
/// N3 (ROADMAP-V2): deterministic PRNG for SIMULATION randomness — AI decisions, Monk conversion
/// timing, agent avoidance, spawn jitter. Kept deliberately separate from
/// <see cref="UnityEngine.Random"/>, which stays for cosmetic/visual noise (particles, camera,
/// décor) so that visual variety never perturbs the simulation stream.
///
/// Seeded from the map seed at game start (<see cref="WorldRoot.Build"/>) so a given seed replays
/// identically — a prerequisite for lockstep multiplayer (N15-N17) and reproducible tests.
/// Xorshift32: tiny, fast, fully deterministic across platforms (integer-only).
/// </summary>
public static class SimRandom
{
    static uint _state = 0x9E3779B9u;

    /// <summary>Reset the simulation stream. Call once per match from the map seed.</summary>
    public static void Seed(int seed)
    {
        _state = (uint)seed ^ 0x9E3779B9u;
        if (_state == 0u) _state = 1u;
        NextUInt(); // discard the first value (poor mixing right after seeding)
    }

    static uint NextUInt()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }

    /// <summary>Uniform float in [0, 1).</summary>
    public static float Value => (NextUInt() & 0xFFFFFFu) / (float)0x1000000;

    /// <summary>Uniform float in [min, max).</summary>
    public static float Range(float min, float max) => min + Value * (max - min);

    /// <summary>Uniform int in [minInclusive, maxExclusive) — matches UnityEngine.Random.Range(int,int).</summary>
    public static int Range(int minInclusive, int maxExclusive)
        => maxExclusive <= minInclusive
            ? minInclusive
            : minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
}
