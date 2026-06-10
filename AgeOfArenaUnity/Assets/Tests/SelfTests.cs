using NUnit.Framework;
using System.Collections.Generic;
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
    public void TechState_NewEconomyTechs_AffectVillager()
    {
        var tech = new TechState();
        Assert.AreEqual(1f, tech.CarryCapacityMult, 0.0001f);
        Assert.AreEqual(1f, tech.MoveSpeedMult(UnitType.Villager), 0.0001f);

        tech.Mark(TechType.Wheelbarrow);
        Assert.AreEqual(1.25f, tech.CarryCapacityMult, 0.0001f);
        Assert.AreEqual(1.1f, tech.MoveSpeedMult(UnitType.Villager), 0.0001f);

        tech.Mark(TechType.HandCart);
        Assert.AreEqual(1.5f, tech.CarryCapacityMult, 0.0001f);
        Assert.AreEqual(1.2f, tech.MoveSpeedMult(UnitType.Villager), 0.0001f);

        tech.Mark(TechType.DoubleBitAxe);
        tech.Mark(TechType.BowSaw);
        tech.Mark(TechType.TwoManSaw);
        Assert.Greater(tech.GatherMult(ResourceKind.Wood), 1.4f);
    }

    [Test]
    public void TechState_NewMilitaryTechs_AffectCombat()
    {
        var tech = new TechState();
        Assert.AreEqual(0f, tech.PierceArmorBonus(UnitType.Militia), 0.0001f);
        Assert.AreEqual(1f, tech.AttackIntervalMult(UnitType.Archer), 0.0001f);

        tech.Mark(TechType.Squires);
        tech.Mark(TechType.Gambesons);
        tech.Mark(TechType.ThumbRing);

        Assert.Greater(tech.MoveSpeedMult(UnitType.Militia), 1f);
        Assert.Greater(tech.PierceArmorBonus(UnitType.Militia), 0f);
        Assert.Less(tech.AttackIntervalMult(UnitType.Archer), 1f);
    }

    [Test]
    public void Hud_AgeAdvanceReasonText_MapsBuildingRequirement()
    {
        Assert.AreEqual("2 tamamlanmis ana bina gerekli",
            HUD.AgeAdvanceReasonText("needs 2 substantial buildings"));
        Assert.AreEqual("medeniyet bu arastirmayi acamaz",
            HUD.TechAvailabilityReasonText("civilization denied"));
    }

    [Test]
    public void Hud_ShouldShowCivDeniedTech_ForProductionBuildings()
    {
        var tech = new TechState { age = Age.Imperial };
        Assert.IsTrue(HUD.ShouldShowCivDeniedTech(BuildingType.Barracks,
            Civilization.Franks, TechDefs.Get(TechType.Halberdier), tech));
        Assert.IsTrue(HUD.ShouldShowCivDeniedTech(BuildingType.ArcheryRange,
            Civilization.Franks, TechDefs.Get(TechType.Arbalest), tech));
        Assert.IsTrue(HUD.ShouldShowCivDeniedTech(BuildingType.Stable,
            Civilization.Britons, TechDefs.Get(TechType.Paladin), tech));
        Assert.IsFalse(HUD.ShouldShowCivDeniedTech(BuildingType.Blacksmith,
            Civilization.Franks, TechDefs.Get(TechType.Arbalest), tech));
    }

    [Test]
    public void Hud_MonasteryTechDetails_ShowFaithAndConversionState()
    {
        var tech = new TechState { age = Age.Castle };
        string blockPrinting = HUD.MonasteryTechDetails(tech, TechType.BlockPrinting);
        Assert.IsTrue(blockPrinting.Contains("Donusum menzili: 2.5 -> 4.0"));
        Assert.IsTrue(blockPrinting.Contains("Faith: 100"));

        string redemption = HUD.MonasteryTechDetails(tech, TechType.Redemption);
        Assert.IsTrue(redemption.Contains("Redemption: bina/kusatma donusumu acik"));
    }

    [Test]
    public void TributeSystem_TaxTiers_MatchHudLine()
    {
        var tech = new TechState();
        Assert.AreEqual(0.30f, TributeSystem.TaxFor(tech), 0.0001f);
        Assert.AreEqual(70, TributeSystem.ReceivedAmount(100, tech));
        Assert.IsTrue(HUD.TributeInfoLine(tech).Contains("%30"));

        tech.Mark(TechType.Coinage);
        Assert.AreEqual(0.20f, TributeSystem.TaxFor(tech), 0.0001f);
        Assert.AreEqual(80, TributeSystem.ReceivedAmount(100, tech));
        Assert.IsTrue(HUD.TributeInfoLine(tech).Contains("Coinage"));

        tech.Mark(TechType.Banking);
        Assert.AreEqual(0f, TributeSystem.TaxFor(tech), 0.0001f);
        Assert.AreEqual(100, TributeSystem.ReceivedAmount(100, tech));
        Assert.IsTrue(HUD.TributeInfoLine(tech).Contains("Banking"));
    }

    [Test]
    public void Hud_MarketSpreadLine_ShowsSellBuyDifference()
    {
        MarketSystem.Reset();
        string line = HUD.MarketSpreadLine();
        Assert.IsTrue(line.Contains("Fark"));
        Assert.IsTrue(line.Contains("Y 70/130 (+60)"));
    }

    [Test]
    public void Hud_ResourceNodeInfoLine_ShowsFarmReseedAndCapacity()
    {
        var go = new GameObject("FarmInfoTest");
        try
        {
            var node = go.AddComponent<ResourceNode>();
            node.Init(ResourceKind.Food, 300);
            node.renewable = true;
            node.reseedWoodCost = 60;

            var tech = new TechState();
            tech.Mark(TechType.HorseCollar);

            string line = HUD.ResourceNodeInfoLine(node, tech);
            Assert.IsTrue(line.Contains("300 / 375"));
            Assert.IsTrue(line.Contains("Reseed 60O"));
            Assert.IsTrue(line.Contains("Kapasite 375 (+75)"));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Hud_RelicHudLine_SplitsControlledDepositedAndCarried()
    {
        var controlledGo = new GameObject("RelicControlledTest");
        var depositedGo = new GameObject("RelicDepositedTest");
        var carriedGo = new GameObject("RelicCarriedTest");
        var monkGo = new GameObject("RelicCarrierTest");
        try
        {
            var controlled = controlledGo.AddComponent<RelicEntity>();
            controlled.controllingTeam = 0;

            var deposited = depositedGo.AddComponent<RelicEntity>();
            deposited.controllingTeam = 0;
            deposited.heldInMonastery = true;

            var carried = carriedGo.AddComponent<RelicEntity>();
            var monk = monkGo.AddComponent<UnitEntity>();
            monk.teamId = 0;
            carried.carrier = monk;

            string line = HUD.RelicHudLine(new List<RelicEntity> { controlled, deposited, carried }, 0);
            Assert.AreEqual("Kontrol 2/3 | Man 1 | Tas 1", line);
        }
        finally
        {
            Object.DestroyImmediate(controlledGo);
            Object.DestroyImmediate(depositedGo);
            Object.DestroyImmediate(carriedGo);
            Object.DestroyImmediate(monkGo);
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

    // ── BAL: balance pins (gather economy + combat rhythm + counters) ─────────
    // Values were tuned against AoE2:DE on the game's ×0.5 match-time scale; see
    // docs/PLAN.md BAL.eco/BAL.combat. Retuning is fine — update the pin with it.

    [Test]
    public void Gather_IntervalTable_Pinned()
    {
        Assert.AreEqual(1.0f,  GatherSystem.GatherIntervalFor(ResourceKind.Food),  0.0001f);
        Assert.AreEqual(1.1f,  GatherSystem.GatherIntervalFor(ResourceKind.Gold),  0.0001f);
        Assert.AreEqual(1.1f,  GatherSystem.GatherIntervalFor(ResourceKind.Stone), 0.0001f);
        Assert.AreEqual(1.25f, GatherSystem.GatherIntervalFor(ResourceKind.Wood),  0.0001f);
        // Effective rate ordering must hold: Food fastest, Wood slowest.
        Assert.Less(GatherSystem.GatherIntervalFor(ResourceKind.Food),
                    GatherSystem.GatherIntervalFor(ResourceKind.Gold));
        Assert.Less(GatherSystem.GatherIntervalFor(ResourceKind.Gold),
                    GatherSystem.GatherIntervalFor(ResourceKind.Wood));
    }

    [Test]
    public void UnitRegistry_AttackIntervals_Pinned()
    {
        Assert.AreEqual(1.9f, UnitRegistry.Get(UnitType.Militia).attackInterval,    0.0001f);
        Assert.AreEqual(1.9f, UnitRegistry.Get(UnitType.Archer).attackInterval,     0.0001f);
        Assert.AreEqual(1.7f, UnitRegistry.Get(UnitType.Cavalry).attackInterval,    0.0001f);
        Assert.AreEqual(2.6f, UnitRegistry.Get(UnitType.Spearman).attackInterval,   0.0001f);
        Assert.AreEqual(1.9f, UnitRegistry.Get(UnitType.Longbowman).attackInterval, 0.0001f);
        Assert.AreEqual(3.0f, UnitRegistry.Get(UnitType.Galley).attackInterval,     0.0001f);
        Assert.AreEqual(2.8f, UnitRegistry.Get(UnitType.Skirmisher).attackInterval, 0.0001f);
        Assert.AreEqual(1.8f, UnitRegistry.Get(UnitType.Camel).attackInterval,      0.0001f);
        Assert.AreEqual(5.0f, UnitRegistry.Get(UnitType.Ram).attackInterval,        0.0001f);
        Assert.AreEqual(5.5f, UnitRegistry.Get(UnitType.Mangonel).attackInterval,   0.0001f);
        Assert.AreEqual(3.6f, UnitRegistry.Get(UnitType.Scorpion).attackInterval,   0.0001f);
        Assert.AreEqual(9.0f, UnitRegistry.Get(UnitType.Trebuchet).attackInterval,  0.0001f);
    }

    static float BonusOf(UnitType u, ArmorClass cls)
    {
        var bv = UnitRegistry.Get(u).bonusVs;
        if (bv == null) return 0f;
        float sum = 0f;
        foreach (var e in bv)
            if ((e.cls & cls) != 0) sum += e.bonus;
        return sum;
    }

    [Test]
    public void UnitRegistry_CounterBonuses_Pinned()
    {
        Assert.AreEqual(15f, BonusOf(UnitType.Spearman,  ArmorClass.Cavalry),  0.0001f);
        Assert.AreEqual(9f,  BonusOf(UnitType.Camel,     ArmorClass.Cavalry),  0.0001f);
        Assert.AreEqual(40f, BonusOf(UnitType.Ram,       ArmorClass.Building), 0.0001f);
        Assert.AreEqual(70f, BonusOf(UnitType.Trebuchet, ArmorClass.Building), 0.0001f);
    }

    [Test]
    public void TechState_SpearmanLine_AntiCavalryBonus()
    {
        var cavGo = new GameObject("SpearLadderCavTarget");
        try
        {
            var cav = cavGo.AddComponent<UnitEntity>();
            cav.type = UnitType.Cavalry;

            var tech = new TechState();
            Assert.AreEqual(0f, tech.BonusTechDamage(UnitType.Spearman, cav), 0.0001f);

            tech.Mark(TechType.Pikeman);
            Assert.AreEqual(7f, tech.BonusTechDamage(UnitType.Spearman, cav), 0.0001f);

            tech.Mark(TechType.Halberdier);
            Assert.AreEqual(17f, tech.BonusTechDamage(UnitType.Spearman, cav), 0.0001f);

            // Ladder never applies against non-cavalry.
            cav.type = UnitType.Militia;
            Assert.AreEqual(0f, tech.BonusTechDamage(UnitType.Spearman, cav), 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(cavGo);
        }
    }

    [Test]
    public void UnitEntity_ChargeMultiplier_Pinned()
    {
        var go = new GameObject("ChargeMultTest");
        try
        {
            var u = go.AddComponent<UnitEntity>();
            u.type = UnitType.Cavalry;
            Assert.AreEqual(2.0f, u.ChargeMultiplier, 0.0001f);
            u.type = UnitType.Militia;
            Assert.AreEqual(1.0f, u.ChargeMultiplier, 0.0001f);
            u.type = UnitType.Camel;
            Assert.AreEqual(1.0f, u.ChargeMultiplier, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Balance_TTK_Bands()
    {
        // Pure data math: TTK = shots-to-kill × attack interval, using the same
        // CombatMath the sim runs. Bands (not exact pins) so small retunes survive.
        var militia = UnitRegistry.Get(UnitType.Militia);
        var spear   = UnitRegistry.Get(UnitType.Spearman);
        var cav     = UnitRegistry.Get(UnitType.Cavalry);

        // UnitFactory values (hp / melee armor): Militia 40/0, Spearman 45/0, Cavalry 75/2.
        const float MilitiaHp = 40f, SpearHp = 45f, CavHp = 75f, CavMeleeArmor = 2f;

        // Militia mirror duel: fights should last seconds, not melt instantly.
        float milNet = CombatMath.NetDamage(militia.baseAtk, 0f);
        float milTtk = Mathf.Ceil(MilitiaHp / milNet) * militia.attackInterval;
        Assert.GreaterOrEqual(milTtk, 12f, $"Militia duel too fast: {milTtk:0.0}s");
        Assert.LessOrEqual(milTtk, 20f, $"Militia duel too slow: {milTtk:0.0}s");

        // AoE2 cost-counter rule: 1 Cavalry beats 1 Spearman, 2 Spearmen beat 1 Cavalry.
        float cavKillsSpear = Mathf.Ceil(SpearHp / CombatMath.NetDamage(cav.baseAtk, 0f)) * cav.attackInterval;
        float spearDmg      = CombatMath.NetDamage(
            spear.baseAtk + BonusOf(UnitType.Spearman, ArmorClass.Cavalry), CavMeleeArmor);
        float spearKillsCav = Mathf.Ceil(CavHp / spearDmg) * spear.attackInterval;
        Assert.Less(cavKillsSpear, spearKillsCav, "1v1: Cavalry must beat Spearman");
        Assert.Less(spearKillsCav / 2f, cavKillsSpear * 2f, "2 Spearmen must beat 1 Cavalry");

        // Siege pacing vs a Town Center (600 hp, 3 melee armor; siege reads melee armor
        // per N0.1). Ram should take ~a minute+, Trebuchet a bit under.
        const float TcHp = 600f, TcMeleeArmor = 3f;
        var ram  = UnitRegistry.Get(UnitType.Ram);
        var treb = UnitRegistry.Get(UnitType.Trebuchet);
        float ramNet  = CombatMath.NetDamage(ram.baseAtk + BonusOf(UnitType.Ram, ArmorClass.Building), TcMeleeArmor);
        float ramTtk  = Mathf.Ceil(TcHp / ramNet) * ram.attackInterval;
        float trebNet = CombatMath.NetDamage(treb.baseAtk + BonusOf(UnitType.Trebuchet, ArmorClass.Building), TcMeleeArmor);
        float trebTtk = Mathf.Ceil(TcHp / trebNet) * treb.attackInterval;
        Assert.GreaterOrEqual(ramTtk, 55f, $"Ram kills TC too fast: {ramTtk:0.0}s");
        Assert.LessOrEqual(ramTtk, 85f, $"Ram kills TC too slowly: {ramTtk:0.0}s");
        Assert.GreaterOrEqual(trebTtk, 40f, $"Trebuchet kills TC too fast: {trebTtk:0.0}s");
        Assert.LessOrEqual(trebTtk, 65f, $"Trebuchet kills TC too slowly: {trebTtk:0.0}s");
    }
}
