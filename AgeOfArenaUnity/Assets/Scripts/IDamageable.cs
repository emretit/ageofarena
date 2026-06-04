using UnityEngine;

/// <summary>
/// Anything that can be the target of an attack order: an enemy
/// <see cref="UnitEntity"/> or a <see cref="BuildingEntity"/>. Lets
/// <see cref="CombatSystem"/> and <see cref="CommandSystem"/> treat units and
/// buildings uniformly when resolving targets, range and damage.
/// </summary>
public interface IDamageable
{
    int TeamId { get; }
    /// <summary>False once destroyed or out of hp. Implementers must guard the
    /// Unity fake-null so a Destroyed object referenced via this interface still
    /// reports dead (interface <c>==</c> does not use Unity's operator overload).</summary>
    bool IsAlive { get; }
    Transform Transform { get; }
    /// <summary>Footprint radius added to the attacker's range so large buildings
    /// can be hit from their edge rather than their centre.</summary>
    float Radius { get; }
    /// <summary>Armor classes this target belongs to (M7/ARMC); drives attackers'
    /// additive bonus damage (e.g. Spearman vs <see cref="ArmorClass.Cavalry"/>).</summary>
    ArmorClass ArmorClasses { get; }
    void TakeDamage(float amount, DamageType damageType = DamageType.Melee);
}
