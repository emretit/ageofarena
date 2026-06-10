using System.Collections.Generic;

/// <summary>
/// N4.registry: Static per-unit data table. Adding a new unit type requires only a
/// new entry here (data row) rather than changes to multiple switch statements.
/// </summary>
public struct BonusVsEntry { public ArmorClass cls; public float bonus; }

public readonly struct UnitRow
{
    public readonly float baseAtk;
    public readonly float baseRange;
    public readonly float attackInterval;
    public readonly float aggroRadius;
    public readonly ArmorClass armorClasses;
    public readonly DamageType damageKind;
    public readonly bool isRanged;
    public readonly float minAttackRange;
    public readonly float splashRadius;
    public readonly float healRadius;
    public readonly float healPower;
    public readonly float selfRegen;
    public readonly BonusVsEntry[] bonusVs;

    public UnitRow(float baseAtk, float baseRange, float attackInterval, float aggroRadius,
                   ArmorClass armorClasses, DamageType damageKind, bool isRanged,
                   float minAttackRange = 0f, float splashRadius = 0f,
                   float healRadius = 0f, float healPower = 0f, float selfRegen = 0f,
                   BonusVsEntry[] bonusVs = null)
    {
        this.baseAtk        = baseAtk;
        this.baseRange      = baseRange;
        this.attackInterval = attackInterval;
        this.aggroRadius    = aggroRadius;
        this.armorClasses   = armorClasses;
        this.damageKind     = damageKind;
        this.isRanged       = isRanged;
        this.minAttackRange = minAttackRange;
        this.splashRadius   = splashRadius;
        this.healRadius     = healRadius;
        this.healPower      = healPower;
        this.selfRegen      = selfRegen;
        this.bonusVs        = bonusVs;
    }
}

public static class UnitRegistry
{
    static readonly Dictionary<UnitType, UnitRow> _table = BuildTable();

    public static UnitRow Get(UnitType t)
        => _table.TryGetValue(t, out var row) ? row : DefaultRow;

    static readonly UnitRow DefaultRow = new UnitRow(2f, 1.1f, 1.6f, 0f, ArmorClass.None, DamageType.Melee, false);

    static BonusVsEntry[] BV(ArmorClass cls, float bonus)
        => new[] { new BonusVsEntry { cls = cls, bonus = bonus } };

    static BonusVsEntry[] BV2(ArmorClass cls1, float b1, ArmorClass cls2, float b2)
        => new[] { new BonusVsEntry { cls = cls1, bonus = b1 }, new BonusVsEntry { cls = cls2, bonus = b2 } };

    static Dictionary<UnitType, UnitRow> BuildTable() => new Dictionary<UnitType, UnitRow>
    {
        // ── Core units ──────────────────────────────────────────────────────────
        [UnitType.Militia]       = new UnitRow(5f,  1.3f, 1.0f, 7f,  ArmorClass.Infantry, DamageType.Melee,  false),
        [UnitType.Archer]        = new UnitRow(4f,  6.5f, 1.4f, 9f,  ArmorClass.Archer,   DamageType.Pierce, true),
        [UnitType.Cavalry]       = new UnitRow(8f,  1.4f, 1.1f, 8f,  ArmorClass.Cavalry,  DamageType.Melee,  false),
        [UnitType.Trebuchet]     = new UnitRow(35f, 15f,  5.5f, 15f, ArmorClass.Siege,    DamageType.Siege,  true,  minAttackRange: 3f,  bonusVs: BV(ArmorClass.Building, 70f)),
        [UnitType.Spearman]      = new UnitRow(4f,  1.5f, 1.3f, 7f,  ArmorClass.Infantry, DamageType.Melee,  false, bonusVs: BV(ArmorClass.Cavalry, 8f)),
        [UnitType.Longbowman]    = new UnitRow(5f,  8.5f, 1.6f, 11f, ArmorClass.Archer,   DamageType.Pierce, true),
        [UnitType.Galley]        = new UnitRow(8f,  5.5f, 2.0f, 8f,  ArmorClass.Ship,     DamageType.Pierce, true,  minAttackRange: 1.5f),
        [UnitType.Skirmisher]    = new UnitRow(3f,  5f,   2.0f, 9f,  ArmorClass.Archer,   DamageType.Pierce, true,  bonusVs: BV(ArmorClass.Archer, 3f)),
        [UnitType.Camel]         = new UnitRow(7f,  1.4f, 1.1f, 8f,  ArmorClass.Cavalry | ArmorClass.Camel, DamageType.Melee, false, bonusVs: BV(ArmorClass.Cavalry, 7f)),
        [UnitType.Ram]           = new UnitRow(4f,  1.3f, 3.0f, 4f,  ArmorClass.Siege,    DamageType.Siege,  false, bonusVs: BV(ArmorClass.Building, 16f)),
        [UnitType.Mangonel]      = new UnitRow(25f, 9f,   4.0f, 11f, ArmorClass.Siege,    DamageType.Siege,  true,  minAttackRange: 2f, splashRadius: 1.8f),
        [UnitType.Scorpion]      = new UnitRow(4f,  5.5f, 2.6f, 9f,  ArmorClass.Siege,    DamageType.Pierce, true,  minAttackRange: 1.5f, splashRadius: 1.2f, bonusVs: BV(ArmorClass.Infantry, 6f)),
        [UnitType.CavalryArcher] = new UnitRow(5f,  4f,   2.0f, 10f, ArmorClass.Archer | ArmorClass.Cavalry, DamageType.Pierce, true),
        [UnitType.FireShip]      = new UnitRow(6f,  3f,   0.8f, 8f,  ArmorClass.Ship,     DamageType.Pierce, true),
        [UnitType.DemoShip]      = new UnitRow(40f, 1.5f, 2.0f, 6f,  ArmorClass.Ship,     DamageType.Siege,  true,  splashRadius: 2.5f),
        // ── M9/CIVU unique units ────────────────────────────────────────────────
        [UnitType.TeutonicKnight]= new UnitRow(12f, 1.4f, 2.0f, 7f,  ArmorClass.Infantry, DamageType.Melee,  false),
        [UnitType.WarElephant]   = new UnitRow(20f, 1.4f, 2.5f, 8f,  ArmorClass.Cavalry,  DamageType.Melee,  false, bonusVs: BV(ArmorClass.Building, 30f)),
        [UnitType.Mangudai]      = new UnitRow(6f,  5f,   2.0f, 10f, ArmorClass.Archer | ArmorClass.Cavalry, DamageType.Pierce, true, bonusVs: BV(ArmorClass.Siege, 10f)),
        [UnitType.Samurai]       = new UnitRow(9f,  1.2f, 1.3f, 8f,  ArmorClass.Infantry, DamageType.Melee,  false),
        [UnitType.Eagle]         = new UnitRow(7f,  1.3f, 1.5f, 8f,  ArmorClass.Infantry, DamageType.Melee,  false),
        [UnitType.EliteEagle]    = new UnitRow(9f,  1.3f, 1.4f, 8f,  ArmorClass.Infantry, DamageType.Melee,  false),
        // ── N4/CIVU ────────────────────────────────────────────────────────────
        [UnitType.ThrowingAxeman]= new UnitRow(9f,  3f,   1.5f, 9f,  ArmorClass.Infantry, DamageType.Melee,  true),
        [UnitType.Cataphract]    = new UnitRow(10f, 1.4f, 1.5f, 8f,  ArmorClass.Cavalry,  DamageType.Melee,  false, bonusVs: BV(ArmorClass.Infantry, 12f)),
        [UnitType.Berserk]       = new UnitRow(9f,  1.3f, 1.2f, 8f,  ArmorClass.Infantry, DamageType.Melee,  false, selfRegen: 0.6f),
        [UnitType.Mameluke]      = new UnitRow(8f,  3f,   1.5f, 9f,  ArmorClass.Cavalry | ArmorClass.Camel, DamageType.Melee, true, bonusVs: BV(ArmorClass.Cavalry, 9f)),
        // ── N4/CIVC13 ──────────────────────────────────────────────────────────
        [UnitType.WoadRaider]    = new UnitRow(8f,  1.3f, 1.0f, 8f,  ArmorClass.Infantry, DamageType.Melee,  false),
        [UnitType.ChuKoNu]       = new UnitRow(8f,  6f,   1.0f, 10f, ArmorClass.Archer,   DamageType.Pierce, true),
        [UnitType.Huskarl]       = new UnitRow(10f, 1.3f, 1.1f, 8f,  ArmorClass.Infantry, DamageType.Melee,  false, bonusVs: BV(ArmorClass.Archer, 6f)),
        [UnitType.Janissary]     = new UnitRow(17f, 7f,   3.0f, 11f, ArmorClass.Archer,   DamageType.Pierce, true),
        // ── Support / special units ─────────────────────────────────────────────
        [UnitType.King]          = new UnitRow(6f,  1.1f, 1.6f, 0f,  ArmorClass.Infantry, DamageType.Melee,  false),
        [UnitType.Scout]         = new UnitRow(0f,  1.1f, 1.6f, 0f,  ArmorClass.Cavalry,  DamageType.Melee,  false), // aggro overridden by tech
        [UnitType.Monk]          = new UnitRow(0f,  1.1f, 1.6f, 0f,  ArmorClass.None,     DamageType.Melee,  false),
        [UnitType.Medic]         = new UnitRow(0f,  1.1f, 1.6f, 0f,  ArmorClass.None,     DamageType.Melee,  false, healRadius: 6f, healPower: 3f),
        [UnitType.Villager]      = new UnitRow(2f,  1.1f, 1.6f, 0f,  ArmorClass.None,     DamageType.Melee,  false),
        [UnitType.FishingShip]   = new UnitRow(0f,  1.1f, 1.6f, 0f,  ArmorClass.Ship,     DamageType.Melee,  false),
    };
}
