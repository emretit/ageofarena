using NUnit.Framework;
using UnityEngine;

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

    [Test]
    public void CommandSystem_FormationOffsets_Line_IsCentered()
    {
        var offsets = CommandSystem.FormationOffsets(3, CommandSystem.FormationType.Line);
        Assert.AreEqual(3, offsets.Length);
        Assert.AreEqual(-1.5f, offsets[0].x, 0.001f);
        Assert.AreEqual(0f, offsets[1].x, 0.001f);
        Assert.AreEqual(1.5f, offsets[2].x, 0.001f);
        Assert.AreEqual(0f, offsets[0].z, 0.001f);
        Assert.AreEqual(0f, offsets[2].z, 0.001f);
    }

    [Test]
    public void Hotkeys_ActionFor_IgnoresUnconsumedActions()
    {
        try
        {
            Hotkeys.Set(HotkeyAction.Repair, KeyCode.H);
            Hotkeys.Set(HotkeyAction.TownBell, KeyCode.H);

            Assert.AreEqual(HotkeyAction.TownBell, Hotkeys.ActionFor(KeyCode.H));
        }
        finally
        {
            Hotkeys.Reset(HotkeyAction.Repair);
            Hotkeys.Reset(HotkeyAction.TownBell);
        }
    }

    [Test]
    public void Hotkeys_StabilizeQa_DefaultGlobalBindingsStayPinned()
    {
        try
        {
            Hotkeys.Reset(HotkeyAction.Patrol);
            Hotkeys.Reset(HotkeyAction.Formation);
            Hotkeys.Reset(HotkeyAction.TownBell);
            Hotkeys.Reset(HotkeyAction.Repair);

            Assert.AreEqual(KeyCode.P, Hotkeys.Get(HotkeyAction.Patrol));
            Assert.AreEqual(KeyCode.F, Hotkeys.Get(HotkeyAction.Formation));
            Assert.AreEqual(KeyCode.H, Hotkeys.Get(HotkeyAction.TownBell));
            Assert.AreEqual(KeyCode.None, Hotkeys.Get(HotkeyAction.Repair));
            Assert.IsFalse(Hotkeys.IsBindableAction(HotkeyAction.Repair));
        }
        finally
        {
            Hotkeys.Reset(HotkeyAction.Patrol);
            Hotkeys.Reset(HotkeyAction.Formation);
            Hotkeys.Reset(HotkeyAction.TownBell);
            Hotkeys.Reset(HotkeyAction.Repair);
        }
    }

    [Test]
    public void CommandSystem_BeginPatrol_ExposesPendingState()
    {
        var go = new GameObject("CommandSystemPatrolTestRoot");
        try
        {
            var command = go.AddComponent<CommandSystem>();
            Assert.IsFalse(command.PatrolPending);
            command.BeginPatrol();
            Assert.IsTrue(command.PatrolPending);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void VisualFactories_BuildingPolishTargets_SpawnRenderableRoots()
    {
        var parent = new GameObject("BuildingVisualFactoryTestRoot");
        try
        {
            var targets = new[]
            {
                BuildingType.ArcheryRange,
                BuildingType.Stable,
                BuildingType.Market,
                BuildingType.Farm,
                BuildingType.MiningCamp,
                BuildingType.Blacksmith,
                BuildingType.Monastery,
                BuildingType.University,
                BuildingType.Dock,
                BuildingType.SiegeWorkshop,
                BuildingType.Outpost,
                BuildingType.WatchTower,
                BuildingType.BombardTower,
                BuildingType.Wonder,
            };

            foreach (var type in targets)
            {
                var go = BuildingFactory.Create(type, parent.transform, Vector3.zero, Color.red);
                Assert.IsNotNull(go.GetComponent<BuildingEntity>(), $"{type} missing BuildingEntity");
                Assert.IsNotNull(go.GetComponent<BoxCollider>(), $"{type} missing root collider");
                Assert.Greater(go.GetComponentsInChildren<Renderer>(true).Length, 0, $"{type} has no renderers");
            }
        }
        finally
        {
            Object.DestroyImmediate(parent);
        }
    }

    [Test]
    public void VisualFactories_MountedAndTradeTargets_SpawnSelectableRenderers()
    {
        var parent = new GameObject("UnitVisualFactoryTestRoot");
        try
        {
            var targets = new[]
            {
                UnitType.Scout,
                UnitType.Cavalry,
                UnitType.CavalryArcher,
                UnitType.Mangudai,
                UnitType.Cataphract,
                UnitType.TradeCart,
            };

            foreach (var type in targets)
            {
                var unit = UnitFactory.Spawn(type, parent.transform, Vector3.zero, 0);
                Assert.IsNotNull(unit, $"{type} did not spawn");
                Assert.IsNotNull(unit.GetComponent<CapsuleCollider>(), $"{type} missing root collider");
                Assert.IsNotNull(unit.GetComponentInChildren<SelectionRing>(true), $"{type} missing selection ring");
                var ring = unit.GetComponentInChildren<LineRenderer>(true);
                Assert.IsNotNull(ring, $"{type} missing selection ring renderer");
                Assert.IsFalse(ring.useWorldSpace, $"{type} selection ring must be local-space before Play mode");
                Assert.Greater(unit.GetComponentsInChildren<Renderer>(true).Length, 0, $"{type} has no renderers");
            }
        }
        finally
        {
            Object.DestroyImmediate(parent);
        }
    }
}
