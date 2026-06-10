using System;
using UnityEngine;

/// <summary>
/// ⚠️ NOT YET USED (2026-06 audit): zero callers outside this file — the live sim still runs on
/// float/Vector3. This is groundwork for the future deterministic-sim (MP2) migration. See NetworkMode.cs.
///
/// N16.fixed: Deterministic fixed-point arithmetic (Q16.16 format).
/// 16 integer bits + 16 fractional bits → range ≈ ±32767, precision ~0.000015.
/// All operations use integer arithmetic only — no floating-point involvement.
/// Cross-platform: identical results on all IEEE-754 platforms that support C# int/long.
///
/// Render interpolation: sim positions stay in FP; Unity Transform (float) receives
/// FP.ToFloat() each render frame. Between fixed-sim ticks the visual position is
/// lerped by LockstepSystem toward the next sim position (render interpolation).
/// </summary>
[Serializable]
public struct FP : IEquatable<FP>, IComparable<FP>
{
    const int SHIFT = 16;
    const long ONE  = 1L << SHIFT;

    public long raw; // internal Q16.16 representation

    // ── Construction ──────────────────────────────────────────────────────────

    FP(long raw) { this.raw = raw; }

    public static FP FromInt(int v)   => new FP((long)v << SHIFT);
    public static FP FromFloat(float v) => new FP((long)(v * ONE));

    public static readonly FP Zero  = new FP(0);
    public static readonly FP One   = new FP(ONE);
    public static readonly FP Half  = new FP(ONE >> 1);
    public static readonly FP Two   = new FP(ONE << 1);

    // ── Conversion ────────────────────────────────────────────────────────────

    public float   ToFloat()  => (float)raw / ONE;
    public int     ToInt()    => (int)(raw >> SHIFT);
    public Vector3 ToVector3(FP z) => new Vector3(ToFloat(), 0f, z.ToFloat());

    // ── Arithmetic ────────────────────────────────────────────────────────────

    public static FP operator +(FP a, FP b) => new FP(a.raw + b.raw);
    public static FP operator -(FP a, FP b) => new FP(a.raw - b.raw);
    public static FP operator -(FP a)       => new FP(-a.raw);
    public static FP operator *(FP a, FP b) => new FP((a.raw * b.raw) >> SHIFT);
    public static FP operator /(FP a, FP b) => new FP(b.raw == 0 ? 0 : (a.raw << SHIFT) / b.raw);

    public static FP operator *(FP a, int b) => new FP(a.raw * b);
    public static FP operator *(int a, FP b) => new FP((long)a * b.raw);
    public static FP operator /(FP a, int b) => new FP(b == 0 ? 0 : a.raw / b);

    // ── Comparison ────────────────────────────────────────────────────────────

    public static bool operator ==(FP a, FP b) => a.raw == b.raw;
    public static bool operator !=(FP a, FP b) => a.raw != b.raw;
    public static bool operator  <(FP a, FP b) => a.raw  < b.raw;
    public static bool operator  >(FP a, FP b) => a.raw  > b.raw;
    public static bool operator <=(FP a, FP b) => a.raw <= b.raw;
    public static bool operator >=(FP a, FP b) => a.raw >= b.raw;

    public bool Equals(FP other) => raw == other.raw;
    public int CompareTo(FP other) => raw.CompareTo(other.raw);
    public override bool Equals(object obj) => obj is FP f && Equals(f);
    public override int GetHashCode() => raw.GetHashCode();
    public override string ToString() => ToFloat().ToString("F4");

    // ── Math utilities ────────────────────────────────────────────────────────

    public static FP Abs(FP v)     => new FP(v.raw < 0 ? -v.raw : v.raw);
    public static FP Max(FP a, FP b) => a.raw >= b.raw ? a : b;
    public static FP Min(FP a, FP b) => a.raw <= b.raw ? a : b;
    public static FP Clamp(FP v, FP lo, FP hi) => v < lo ? lo : v > hi ? hi : v;

    /// <summary>Integer square root (no floating-point).</summary>
    public static FP Sqrt(FP v)
    {
        if (v.raw <= 0) return Zero;
        ulong n = (ulong)v.raw << SHIFT; // scale up before root so we recover fractional part
        return new FP((long)IntSqrt(n));
    }

    static ulong IntSqrt(ulong n)
    {
        ulong root = 0;
        ulong bit = 1UL << 62;
        while (bit > n) bit >>= 2;

        while (bit != 0)
        {
            if (n >= root + bit)
            {
                n -= root + bit;
                root = (root >> 1) + bit;
            }
            else
            {
                root >>= 1;
            }
            bit >>= 2;
        }
        return root;
    }

    /// <summary>
    /// Distance between two XZ points, fully integer.
    /// Equivalent to Mathf.Sqrt(dx*dx + dz*dz) but deterministic.
    /// </summary>
    public static FP Distance(FP ax, FP az, FP bx, FP bz)
    {
        var dx = ax - bx;
        var dz = az - bz;
        return Sqrt(dx * dx + dz * dz);
    }
}

// ── FP-based 2D vector ────────────────────────────────────────────────────────

/// <summary>Deterministic 2D XZ vector using fixed-point components.</summary>
[Serializable]
public struct FPVec2
{
    public FP x, z;

    public FPVec2(FP x, FP z) { this.x = x; this.z = z; }
    public static FPVec2 FromFloat(float x, float z) =>
        new FPVec2(FP.FromFloat(x), FP.FromFloat(z));

    public FP MagnitudeSq => x * x + z * z;
    public FP Magnitude   => FP.Sqrt(MagnitudeSq);

    public FPVec2 Normalized()
    {
        var m = Magnitude;
        return m == FP.Zero ? new FPVec2(FP.Zero, FP.Zero)
                            : new FPVec2(x / m, z / m);
    }

    public static FPVec2 operator +(FPVec2 a, FPVec2 b) => new FPVec2(a.x + b.x, a.z + b.z);
    public static FPVec2 operator -(FPVec2 a, FPVec2 b) => new FPVec2(a.x - b.x, a.z - b.z);
    public static FPVec2 operator *(FPVec2 a, FP s)     => new FPVec2(a.x * s, a.z * s);

    public Vector3 ToVector3() => new Vector3(x.ToFloat(), 0f, z.ToFloat());
    public override string ToString() => $"({x},{z})";
}

// ── FPMath ────────────────────────────────────────────────────────────────────

/// <summary>
/// Deterministic fixed-point combat math used by CombatMath and LockstepSystem.
/// Delegates to the same CombatMath.NetDamage formula but via FP arithmetic.
/// </summary>
public static class FPMath
{
    /// <summary>
    /// Deterministic net damage: max(1, attack + bonus - armor).
    /// Identical to CombatMath.NetDamage but with integer rounding.
    /// </summary>
    public static int NetDamageInt(int attack, int bonus, int armor)
    {
        int net = attack + bonus - armor;
        return net < 1 ? 1 : net;
    }

    /// <summary>
    /// Deterministic distance check (squared, avoids Sqrt for simple threshold checks).
    /// Returns true if the two XZ points are within range² units.
    /// </summary>
    public static bool InRangeSq(FP ax, FP az, FP bx, FP bz, FP rangeSq)
    {
        var dx = ax - bx;
        var dz = az - bz;
        return (dx * dx + dz * dz) <= rangeSq;
    }
}
