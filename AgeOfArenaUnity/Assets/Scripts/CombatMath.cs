using UnityEngine;

/// <summary>
/// N2 (ROADMAP-V2): pure, MonoBehaviour-free combat math. The most regression-prone rules — the
/// AoE2 net-damage formula and the damage-class → armor mapping (including the N0.1 fix that makes
/// siege melee-class instead of armor-bypassing) — live here in one testable place.
/// <see cref="UnitEntity.TakeDamage"/> and <see cref="BuildingEntity.TakeDamage"/> call into it,
/// and <see cref="SelfTest"/> pins the behaviour so later combat changes can't silently regress it.
/// </summary>
public static class CombatMath
{
    /// <summary>AoE2 net damage: at least 1 always lands, regardless of armor.</summary>
    public static float NetDamage(float amount, float armor) => Mathf.Max(1f, amount - armor);

    /// <summary>Which base armor value resists a hit of the given damage class. N0.1: Siege is
    /// melee-class (reduced by melee armor), NOT armor-bypassing as it used to be.</summary>
    public static float ArmorFor(DamageType dt, float meleeArmor, float pierceArmor) => dt switch
    {
        DamageType.Pierce => pierceArmor,
        DamageType.Melee  => meleeArmor,
        DamageType.Siege  => meleeArmor,   // N0.1
        // Fail closed: an unmapped damage class resists with melee armor instead of 0
        // (which silently let the full hit through, bypassing all armor).
        _                 => meleeArmor,
    };

    /// <summary>Returns "" if all assertions pass, otherwise the first failure. Invoked from the
    /// editor (RunCommand) until a formal EditMode test assembly lands (N2.asmdef).</summary>
    public static string SelfTest()
    {
        if (!Mathf.Approximately(NetDamage(10f, 3f), 7f)) return "NetDamage(10,3)!=7";
        if (!Mathf.Approximately(NetDamage(2f, 5f), 1f))  return "NetDamage min-1 broken";
        if (!Mathf.Approximately(ArmorFor(DamageType.Pierce, 4f, 2f), 2f)) return "ArmorFor pierce";
        if (!Mathf.Approximately(ArmorFor(DamageType.Melee,  4f, 2f), 4f)) return "ArmorFor melee";
        if (!Mathf.Approximately(ArmorFor(DamageType.Siege,  4f, 2f), 4f)) return "ArmorFor siege (N0.1) must read melee armor";
        return "";
    }
}
