using System;

namespace Belote.Core;

/// <summary>SplitMix64 generator, used for seeding and for seed mixing.</summary>
public struct SplitMix64
{
    private ulong _state;

    public SplitMix64(ulong seed)
    {
        _state = seed;
    }

    public ulong Next()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public static ulong Mix(ulong value) => new SplitMix64(value).Next();
}

/// <summary>
/// Deterministic xoshiro256** generator. The algorithm is fixed: its output sequences are part of the
/// reproducibility contract (same seed, same game), so it must never change silently.
/// </summary>
public sealed class SeededRandom
{
    private readonly ulong _seed;
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public SeededRandom(ulong seed)
    {
        _seed = seed;
        var sm = new SplitMix64(seed);
        _s0 = sm.Next();
        _s1 = sm.Next();
        _s2 = sm.Next();
        _s3 = sm.Next();
    }

    public ulong NextULong()
    {
        ulong result = Rotl(_s1 * 5UL, 7) * 9UL;
        ulong t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = Rotl(_s3, 45);
        return result;
    }

    /// <summary>Unbiased integer in [0, bound).</summary>
    public int NextInt(int bound)
    {
        if (bound <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bound));
        }

        // Rejection sampling: discard the first (2^64 mod bound) values so the rest map evenly.
        ulong b = (ulong)bound;
        ulong threshold = (0UL - b) % b;
        ulong x;
        do
        {
            x = NextULong();
        }
        while (x < threshold);

        return (int)(x % b);
    }

    /// <summary>Independent child generator for a salt; does not advance this generator.</summary>
    public SeededRandom Derive(ulong salt) => new(SplitMix64.Mix(_seed ^ SplitMix64.Mix(salt)));

    /// <summary>Fisher-Yates shuffle in place.</summary>
    public void Shuffle<T>(T[] items)
    {
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));
}

/// <summary>Deterministic derivation of per-game, per-deal and per-seat seeds.</summary>
public static class Seeds
{
    public static ulong ForGame(ulong baseSeed, int gameIndex) =>
        Combine(baseSeed, 0x47414D4553454544UL, (ulong)gameIndex);

    public static ulong ForHand(ulong gameSeed, int dealNumber) =>
        Combine(gameSeed, 0x48414E4453454544UL, (ulong)dealNumber);

    public static ulong ForSeat(ulong gameSeed, Seat seat) =>
        Combine(gameSeed, 0x534541545345454DUL, (ulong)seat);

    public static ulong ForDealer(ulong gameSeed) => Combine(gameSeed, 0x4445414C45525344UL, 0);

    private static ulong Combine(ulong a, ulong salt, ulong b) =>
        SplitMix64.Mix(SplitMix64.Mix(a ^ salt) + b * 0x9E3779B97F4A7C15UL);
}
