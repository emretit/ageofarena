using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Auto-fire for defensive buildings (Castle). Any building whose
/// <see cref="BuildingDefs"/> entry has attackRange &gt; 0 scans for the nearest
/// enemy unit in range and looses an arrow on a cooldown. Reuses the unit combat
/// infrastructure (<see cref="Projectile.Spawn"/> + <see cref="IDamageable"/>);
/// buildings don't chase, so this stays separate from the unit-centric
/// <see cref="CombatSystem"/>. Ticked by <see cref="GameManager"/> after combat.
/// </summary>
public class BuildingCombatSystem : MonoBehaviour
{
    const float MuzzleHeight = 5f; // arrows leave from the top of the keep

    // Garrison defensive fire: each sheltered unit adds an arrow (capped), and a
    // passive building (Town Center) gains a defensive range while it shelters anyone.
    const float GarrisonRange       = 8f;
    const float GarrisonArrowDamage = 6f;
    const float GarrisonInterval    = 1.2f;
    const int   MaxGarrisonArrows   = 5;

    public void Tick(List<BuildingEntity> buildings, List<UnitEntity> units, float dt)
    {
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b == null || b.underConstruction) continue;

            var def = BuildingDefs.Get(b.type);
            int garr = b.GarrisonCount;

            // A passive building fires only while garrisoned; an armed one (Castle)
            // always fires and shoots farther of the two ranges.
            float range = def.attackRange > 0f ? def.attackRange : (garr > 0 ? GarrisonRange : 0f);
            if (range <= 0f) continue;

            if (b.attackCooldown > 0f) { b.attackCooldown -= dt; continue; }

            var target = NearestEnemyUnit(b, units, range);
            if (target == null) continue;

            Vector3 muzzle = b.transform.position + Vector3.up * MuzzleHeight;
            if (def.attackDamage > 0f)
                Projectile.Spawn(muzzle, target, def.attackDamage, DamageType.Pierce);      // Castle arrow
            int extra = Mathf.Min(garr, MaxGarrisonArrows);
            for (int a = 0; a < extra; a++)
                Projectile.Spawn(muzzle, target, GarrisonArrowDamage, DamageType.Pierce);  // garrison arrow

            b.attackCooldown = def.attackInterval > 0f ? def.attackInterval : GarrisonInterval;
        }
    }

    static UnitEntity NearestEnemyUnit(BuildingEntity b, List<UnitEntity> units, float range)
    {
        UnitEntity best = null;
        float bestSq = range * range;
        Vector3 pos = b.transform.position;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null || u.teamId == b.teamId || u.isGarrisoned) continue;
            float sq = FlatSq(pos, u.transform.position);
            if (sq < bestSq) { bestSq = sq; best = u; }
        }
        return best;
    }

    static float FlatSq(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return dx * dx + dz * dz;
    }
}
