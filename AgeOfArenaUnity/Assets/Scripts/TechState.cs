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

    /// <summary>Additive attack bonus for a unit type (read live by CombatSystem).</summary>
    public float AttackBonus(UnitType t) => t switch
    {
        UnitType.Militia => MeleeAttackBonus,
        UnitType.Cavalry => MeleeAttackBonus,
        UnitType.Archer  => ArcherAttackBonus,
        _ => 0f,
    };

    /// <summary>Additive attack-range bonus (archers only).</summary>
    public float RangeBonus(UnitType t) =>
        t == UnitType.Archer && Has(TechType.Fletching) ? 0.5f : 0f;

    /// <summary>Additive max-hp bonus for a unit type.</summary>
    public float HpBonus(UnitType t) => t switch
    {
        UnitType.Militia => Has(TechType.ScaleMail) ? 20f : 0f,
        UnitType.Cavalry => (Has(TechType.ScaleMail) ? 20f : 0f) + (Has(TechType.Bloodlines) ? 20f : 0f),
        _ => 0f,
    };

    /// <summary>Multiplier on resources gained per deposit of a given kind.</summary>
    public float GatherMult(ResourceKind kind)
    {
        float m = 1f;
        if (Has(TechType.Wheelbarrow)) m += 0.2f;
        if (kind == ResourceKind.Wood && Has(TechType.DoubleBitAxe)) m += 0.25f;
        return m;
    }
}
