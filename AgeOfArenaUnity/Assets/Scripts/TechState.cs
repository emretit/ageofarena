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
    float MeleeAttackBonus  => Has(TechType.Forging) ? 2f : 0f;
    float ArcherAttackBonus => (Has(TechType.Fletching) ? 1f : 0f) + (Has(TechType.Bodkin) ? 1f : 0f);

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
                             + (Has(TechType.Galleon) ? 2f : 0f);

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

    /// <summary>Additive attack-range bonus (archers only).</summary>
    public float RangeBonus(UnitType t) =>
        t == UnitType.Archer
            ? (Has(TechType.Fletching) ? 0.5f : 0f)
            + (Has(TechType.Crossbowman) ? 0.5f : 0f)
            + (Has(TechType.Arbalest) ? 0.5f : 0f)
            : 0f;

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
        _ => 0f,
    };

    /// <summary>Extra melee armor from Masonry/Fortified Wall (University techs).</summary>
    public float BuildingMeleeArmor => (Has(TechType.Masonry) ? 2f : 0f) + (Has(TechType.Fortified) ? 3f : 0f);
    public float BuildingPierceArmor => (Has(TechType.Masonry) ? 2f : 0f) + (Has(TechType.Fortified) ? 3f : 0f);

    /// <summary>Watch Tower line upgrades (Guard Tower / Keep): tower attack + range bonus.</summary>
    public float TowerAttackBonus => (Has(TechType.GuardTower) ? 3f : 0f) + (Has(TechType.Keep) ? 4f : 0f);
    public float TowerRangeBonus  => Has(TechType.Keep) ? 1.5f : 0f;

    /// <summary>Farm food capacity bonus from Horse Collar / Heavy Plow (Mill techs).</summary>
    public int FarmCapacityBonus => (Has(TechType.HorseCollar) ? 75 : 0) + (Has(TechType.HeavyPlow) ? 75 : 0);

    /// <summary>Multiplier on resources gained per deposit of a given kind.</summary>
    public float GatherMult(ResourceKind kind)
    {
        float m = 1f;
        if (Has(TechType.Wheelbarrow)) m += 0.2f;
        if (kind == ResourceKind.Wood && Has(TechType.DoubleBitAxe)) m += 0.25f;
        return m;
    }
}
