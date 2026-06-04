using System.Collections.Generic;

/// <summary>
/// Per-team technology state: current <see cref="Age"/> plus the set of researched
/// <see cref="TechType"/>. Mirrors the per-team <see cref="ResourceManager"/> model
/// (one instance per team in <c>GameManager.teamTech</c>). Stat bonuses are derived
/// from the researched set so combat/gather systems can read them live each frame.
/// </summary>
public class TechState
{
    public Age age = Age.Dark;

    readonly HashSet<TechType> _researched = new();

    /// <summary>Bumps whenever the researched set changes, so UI can cheaply detect
    /// that available techs/age may have changed without re-querying every frame.</summary>
    public int Version { get; private set; }

    public bool Has(TechType t) => _researched.Contains(t);
    public void Mark(TechType t) { if (_researched.Add(t)) Version++; }

    // ── Attack bonuses (additive) ────────────────────────────────────────────
    // Blacksmith melee line: Forging +2, Iron Casting +1, Blast Furnace +2 — shared
    // by infantry AND cavalry (read once, no double-count: BFUR cavalry flows here).
    float MeleeAttackBonus  => (Has(TechType.Forging) ? 2f : 0f)
                             + (Has(TechType.IronCasting) ? 1f : 0f)
                             + (Has(TechType.BlastFurnace) ? 2f : 0f);
    // Blacksmith missile line: Fletching +1, Bodkin +1, Bracer +1; University Chemistry +1.
    float ArcherAttackBonus => (Has(TechType.Fletching) ? 1f : 0f)
                             + (Has(TechType.Bodkin) ? 1f : 0f)
                             + (Has(TechType.Bracer) ? 1f : 0f)
                             + ChemistryBonus;
    /// <summary>University Chemistry: +1 to every missile attack (archers, towers, galleys).</summary>
    float ChemistryBonus => Has(TechType.Chemistry) ? 1f : 0f;

    // Tier-promotion bonuses stack (Castle + Imperial tiers).
    float MilitiaLineAtk => (Has(TechType.ManAtArms) ? 1f : 0f)
                          + (Has(TechType.Longswordsman) ? 2f : 0f)
                          + (Has(TechType.TwoHandedSwordsman) ? 2f : 0f)
                          + (Has(TechType.Champion) ? 2f : 0f);
    float CavalryLineAtk => (Has(TechType.Cavalier) ? 2f : 0f)
                          + (Has(TechType.Paladin) ? 3f : 0f);
    float ArcherLineAtk  => (Has(TechType.Crossbowman) ? 2f : 0f)
                          + (Has(TechType.Arbalest) ? 2f : 0f);
    // Counter-unit tier lines (M2): Spearman→Pikeman→Halberdier, Skirmisher→Elite, Camel→Heavy.
    float SpearmanLineAtk   => (Has(TechType.Pikeman) ? 2f : 0f)
                             + (Has(TechType.Halberdier) ? 3f : 0f);
    float SkirmisherLineAtk => Has(TechType.EliteSkirmisher) ? 1f : 0f;
    float CamelLineAtk      => Has(TechType.HeavyCamel) ? 3f : 0f;
    // Mobile-unit lines (M4): Scout→Light Cav→Hussar grants combat; Cavalry Archer tier.
    float ScoutLineAtk      => (Has(TechType.LightCavalry) ? 5f : 0f)
                             + (Has(TechType.Hussar) ? 2f : 0f);
    float CavArcherLineAtk  => Has(TechType.HeavyCavalryArcher) ? 2f : 0f;
    float GalleyLineAtk     => (Has(TechType.WarGalley) ? 2f : 0f)
                             + (Has(TechType.Galleon) ? 2f : 0f)
                             + ChemistryBonus;

    /// <summary>Additive attack bonus for a unit type (read live by CombatSystem).</summary>
    public float AttackBonus(UnitType t) => t switch
    {
        UnitType.Militia    => MeleeAttackBonus + MilitiaLineAtk,
        UnitType.Cavalry    => MeleeAttackBonus + CavalryLineAtk,
        UnitType.Archer     => ArcherAttackBonus + ArcherLineAtk,
        UnitType.Spearman   => MeleeAttackBonus + SpearmanLineAtk,
        UnitType.Skirmisher => ArcherAttackBonus + SkirmisherLineAtk,
        UnitType.Camel      => MeleeAttackBonus + CamelLineAtk,
        UnitType.Scout      => ScoutLineAtk,
        UnitType.CavalryArcher => ArcherAttackBonus + CavArcherLineAtk,
        UnitType.Galley     => GalleyLineAtk,
        _ => 0f,
    };

    /// <summary>Additive attack-range bonus for archer-class units. Bracer (+0.5) applies
    /// to every archer; the Archer's own tier upgrades (Fletching/Crossbow/Arbalest) stack.</summary>
    public float RangeBonus(UnitType t)
    {
        bool archerClass = t == UnitType.Archer || t == UnitType.Longbowman
                        || t == UnitType.Skirmisher || t == UnitType.CavalryArcher;
        if (!archerClass) return 0f;
        float r = Has(TechType.Bracer) ? 0.5f : 0f;
        if (t == UnitType.Archer)
            r += (Has(TechType.Fletching) ? 0.5f : 0f)
               + (Has(TechType.Crossbowman) ? 0.5f : 0f)
               + (Has(TechType.Arbalest) ? 0.5f : 0f);
        return r;
    }

    /// <summary>Additive max-hp bonus for a unit type.</summary>
    public float HpBonus(UnitType t) => t switch
    {
        UnitType.Militia => (Has(TechType.ScaleMail) ? 20f : 0f)
                          + (Has(TechType.ManAtArms) ? 10f : 0f)
                          + (Has(TechType.Longswordsman) ? 15f : 0f)
                          + (Has(TechType.TwoHandedSwordsman) ? 15f : 0f)
                          + (Has(TechType.Champion) ? 20f : 0f),
        UnitType.Cavalry => (Has(TechType.ScaleMail) ? 20f : 0f)
                          + (Has(TechType.Bloodlines) ? 20f : 0f)
                          + (Has(TechType.Cavalier) ? 20f : 0f)
                          + (Has(TechType.Paladin) ? 25f : 0f),
        UnitType.Archer  => (Has(TechType.Crossbowman) ? 10f : 0f)
                          + (Has(TechType.Arbalest) ? 15f : 0f),
        UnitType.Spearman => (Has(TechType.ScaleMail) ? 20f : 0f)
                          + (Has(TechType.Pikeman) ? 15f : 0f)
                          + (Has(TechType.Halberdier) ? 20f : 0f),
        UnitType.Skirmisher => Has(TechType.EliteSkirmisher) ? 10f : 0f,
        UnitType.Camel   => (Has(TechType.Bloodlines) ? 20f : 0f)
                          + (Has(TechType.HeavyCamel) ? 20f : 0f),
        UnitType.Scout   => (Has(TechType.LightCavalry) ? 15f : 0f)
                          + (Has(TechType.Hussar) ? 15f : 0f)
                          + (Has(TechType.Bloodlines) ? 20f : 0f),
        UnitType.CavalryArcher => (Has(TechType.Bloodlines) ? 20f : 0f)
                          + (Has(TechType.HeavyCavalryArcher) ? 20f : 0f),
        UnitType.Galley  => (Has(TechType.WarGalley) ? 20f : 0f)
                          + (Has(TechType.Galleon) ? 30f : 0f),
        UnitType.Villager => Has(TechType.Loom) ? 15f : 0f,   // ECON: Loom +15 hp
        UnitType.Monk    => Has(TechType.Sanctity) ? 15f : 0f, // MONK: Sanctity +15 hp
        _ => 0f,
    };

    // ── M7 (MONK/CONV): Monastery conversion tuning ──────────────────────────
    /// <summary>Monk conversion range: base 2.5 + Block Printing (+1.5).</summary>
    public float MonkConvertRange => 2.5f + (Has(TechType.BlockPrinting) ? 1.5f : 0f);
    /// <summary>Theocracy: a converting monk keeps half its faith (recharges faster).</summary>
    public bool MonkHasTheocracy => Has(TechType.Theocracy);
    /// <summary>Redemption: monks may also convert buildings/siege.</summary>
    public bool MonkHasRedemption => Has(TechType.Redemption);

    /// <summary>
    /// Additive armor bonus for a unit type and damage class (read live by
    /// <see cref="UnitEntity.TakeDamage"/>). Blacksmith armor lines: infantry
    /// (ChainMail/PlateMail), cavalry barding (Scale/Chain/Plate), archer armor
    /// (Padded/Leather/Ring). Loom gives villagers a little armor too.
    /// </summary>
    /// <summary>Blacksmith armor (equal melee+pierce) + ScaleMail base armor — shared by both
    /// damage types. BSMT infantry (Chain/Plate), BFUR cavalry barding, ARRM archer armor, Loom.</summary>
    float BlacksmithArmor(UnitType t) => t switch
    {
        UnitType.Militia or UnitType.Spearman =>
            (Has(TechType.ScaleMail) ? 1f : 0f) + (Has(TechType.ChainMail) ? 1f : 0f) + (Has(TechType.PlateMail) ? 2f : 0f),
        UnitType.Cavalry or UnitType.Camel or UnitType.Scout =>
            (Has(TechType.ScaleBarding) ? 1f : 0f) + (Has(TechType.ChainBarding) ? 1f : 0f) + (Has(TechType.PlateBarding) ? 2f : 0f),
        UnitType.Archer or UnitType.Longbowman or UnitType.Skirmisher or UnitType.CavalryArcher =>
            (Has(TechType.PaddedArcherArmor) ? 1f : 0f) + (Has(TechType.LeatherArcherArmor) ? 1f : 0f) + (Has(TechType.RingArcherArmor) ? 1f : 0f),
        UnitType.Villager => Has(TechType.Loom) ? 1f : 0f,
        _ => 0f,
    };

    /// <summary>Additive melee armor bonus (ARMR): Blacksmith/ScaleMail armor + melee-side
    /// tier-promotion armor (infantry & cavalry upgrades each add +1 melee armor).</summary>
    public float MeleeArmorBonus(UnitType t)
    {
        float a = BlacksmithArmor(t);
        switch (t)
        {
            case UnitType.Militia:  a += (Has(TechType.Longswordsman) ? 1f : 0f) + (Has(TechType.Champion) ? 1f : 0f); break;
            case UnitType.Spearman: a += (Has(TechType.Pikeman) ? 1f : 0f) + (Has(TechType.Halberdier) ? 1f : 0f); break;
            case UnitType.Cavalry:  a += (Has(TechType.Cavalier) ? 1f : 0f) + (Has(TechType.Paladin) ? 1f : 0f); break;
        }
        return a;
    }

    /// <summary>Additive pierce armor bonus (ARMR): Blacksmith/ScaleMail armor + pierce-side
    /// tier-promotion armor (archer upgrades each add +1 pierce armor).</summary>
    public float PierceArmorBonus(UnitType t)
    {
        float a = BlacksmithArmor(t);
        if (t == UnitType.Archer) a += (Has(TechType.Crossbowman) ? 1f : 0f) + (Has(TechType.Arbalest) ? 1f : 0f);
        return a;
    }

    /// <summary>Effective armor bonus for a damage class (read live by UnitEntity.TakeDamage).</summary>
    public float ArmorBonus(UnitType t, DamageType dmg) => dmg switch
    {
        DamageType.Pierce => PierceArmorBonus(t),
        DamageType.Melee  => MeleeArmorBonus(t),
        _                 => 0f,
    };

    /// <summary>Cavalry/villager move-speed multiplier: Husbandry (cavalry ×1.1),
    /// Wheelbarrow (villager ×1.1). Read live by <see cref="UnitEntity.RecomputeSpeed"/>.</summary>
    public float MoveSpeedMult(UnitType t)
    {
        float m = 1f;
        bool cav = t == UnitType.Cavalry || t == UnitType.Camel
                || t == UnitType.Scout || t == UnitType.CavalryArcher;
        if (cav && Has(TechType.Husbandry)) m *= 1.1f;
        if (t == UnitType.Villager && Has(TechType.Wheelbarrow)) m *= 1.1f;
        return m;
    }

    /// <summary>Villager carry-capacity multiplier (Wheelbarrow ×1.25) — more per trip.</summary>
    public float CarryCapacityMult => Has(TechType.Wheelbarrow) ? 1.25f : 1f;
    /// <summary>Flat carry bonus (reserved for a future Hand Cart tier).</summary>
    public int CarryBonus => 0;

    /// <summary>Trade-cart gold multiplier: Caravan ×1.5, Banking ×1.2 (stack) — read by TradingSystem.</summary>
    public float TradeGoldMult => (Has(TechType.Caravan) ? 1.5f : 1f) * (Has(TechType.Banking) ? 1.2f : 1f);

    /// <summary>Guilds narrows the Market sell/buy spread (better sell, cheaper buy).</summary>
    public bool HasGuilds => Has(TechType.Guilds);

    /// <summary>University Architecture: +10% building max-HP (applied in BuildingEntity.Start).</summary>
    public float BuildingHpMult => Has(TechType.Architecture) ? 1.10f : 1f;

    /// <summary>Extra melee armor from Masonry/Fortified Wall + Architecture (University techs).</summary>
    public float BuildingMeleeArmor => (Has(TechType.Masonry) ? 2f : 0f) + (Has(TechType.Fortified) ? 3f : 0f)
                                     + (Has(TechType.Architecture) ? 1f : 0f);
    public float BuildingPierceArmor => (Has(TechType.Masonry) ? 2f : 0f) + (Has(TechType.Fortified) ? 3f : 0f)
                                      + (Has(TechType.Architecture) ? 1f : 0f);

    /// <summary>Watch Tower line upgrades (Guard Tower / Keep) + Chemistry: tower attack + range bonus.</summary>
    public float TowerAttackBonus => (Has(TechType.GuardTower) ? 3f : 0f) + (Has(TechType.Keep) ? 4f : 0f)
                                   + ChemistryBonus;
    public float TowerRangeBonus  => Has(TechType.Keep) ? 1.5f : 0f;

    /// <summary>Farm food capacity bonus from Horse Collar / Heavy Plow / Crop Rotation (Mill techs).</summary>
    public int FarmCapacityBonus => (Has(TechType.HorseCollar) ? 75 : 0) + (Has(TechType.HeavyPlow) ? 75 : 0)
                                  + (Has(TechType.CropRotation) ? 75 : 0);

    /// <summary>Multiplier on resources gained per deposit of a given kind. Kind-specific
    /// economy techs: Double-Bit Axe/Bow Saw (wood), Gold Mining (gold), Stone Mining (stone).
    /// (Wheelbarrow no longer scales the deposit — it now raises carry capacity & villager speed.)</summary>
    public float GatherMult(ResourceKind kind)
    {
        float m = 1f;
        switch (kind)
        {
            case ResourceKind.Wood:
                if (Has(TechType.DoubleBitAxe)) m += 0.25f;
                if (Has(TechType.BowSaw))       m += 0.2f;
                break;
            case ResourceKind.Gold:
                if (Has(TechType.GoldMining))   m += 0.15f;
                break;
            case ResourceKind.Stone:
                if (Has(TechType.StoneMining))  m += 0.15f;
                break;
        }
        return m;
    }
}
