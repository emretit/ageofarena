using NUnit.Framework;

/// <summary>
/// N2.asmdef: EditMode self-tests. These pin the pure-logic behaviour of
/// CombatMath, FixedPoint, and GridPathfinder so regressions are caught
/// before Play mode is entered.
/// </summary>
[TestFixture]
public class SelfTests
{
    // ── CombatMath ────────────────────────────────────────────────────────────

    [Test]
    public void CombatMath_SelfTest_Passes()
    {
        string result = CombatMath.SelfTest();
        Assert.IsTrue(string.IsNullOrEmpty(result), $"CombatMath.SelfTest() failed: {result}");
    }

    [Test]
    public void CombatMath_NetDamage_MinOne()
    {
        // Even if armor > attack, damage is always at least 1.
        float dmg = CombatMath.NetDamage(1f, 100f);
        Assert.AreEqual(1f, dmg);
    }

    [Test]
    public void CombatMath_NetDamage_NormalCase()
    {
        // amount=15, armor=3 → 12
        float dmg = CombatMath.NetDamage(15f, 3f);
        Assert.AreEqual(12f, dmg);
    }

    // ── FixedPoint ────────────────────────────────────────────────────────────

    [Test]
    public void FP_AddSubtract()
    {
        var a = FP.FromFloat(3.5f);
        var b = FP.FromFloat(1.5f);
        Assert.AreEqual(FP.FromFloat(5.0f), a + b);
        Assert.AreEqual(FP.FromFloat(2.0f), a - b);
    }

    [Test]
    public void FP_Multiply()
    {
        var a = FP.FromFloat(3f);
        var b = FP.FromFloat(4f);
        // 3 × 4 = 12 (exact in Q16.16)
        Assert.AreEqual(FP.FromInt(12), a * b);
    }

    [Test]
    public void FP_Sqrt_Exact()
    {
        // sqrt(4) = 2
        var four  = FP.FromInt(4);
        var two   = FP.Sqrt(four);
        // Allow ±1 raw bit tolerance for rounding.
        Assert.IsTrue(UnityEngine.Mathf.Abs(two.ToFloat() - 2f) < 0.001f,
            $"sqrt(4) = {two.ToFloat()}, expected ~2");
    }

    [Test]
    public void FP_Distance()
    {
        // 3-4-5 triangle
        var d = FP.Distance(FP.Zero, FP.Zero, FP.FromInt(3), FP.FromInt(4));
        Assert.IsTrue(UnityEngine.Mathf.Abs(d.ToFloat() - 5f) < 0.01f,
            $"Distance = {d.ToFloat()}, expected 5");
    }

    // ── GridPathfinder ────────────────────────────────────────────────────────

    [Test]
    public void GridPathfinder_SelfTest_Passes()
    {
        Assert.IsTrue(GridPathfinder.SelfTest(),
            "GridPathfinder.SelfTest() failed — open-disc path not found");
    }

    [Test]
    public void FPMath_NetDamageInt_MinOne()
    {
        Assert.AreEqual(1, FPMath.NetDamageInt(1, 0, 999));
    }

    [Test]
    public void FPMath_InRangeSq_True()
    {
        // Points at (0,0) and (3,4) — distance=5, range=6 → in range
        var rangeSq = FP.FromInt(36); // 6²
        Assert.IsTrue(FPMath.InRangeSq(FP.Zero, FP.Zero, FP.FromInt(3), FP.FromInt(4), rangeSq));
    }

    [Test]
    public void FPMath_InRangeSq_False()
    {
        // range=4 → 4²=16 < 5²=25 → not in range
        var rangeSq = FP.FromInt(16);
        Assert.IsFalse(FPMath.InRangeSq(FP.Zero, FP.Zero, FP.FromInt(3), FP.FromInt(4), rangeSq));
    }
}
