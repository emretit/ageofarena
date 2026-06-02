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

    public void Tick(List<BuildingEntity> buildings, List<UnitEntity> units, float dt)
    {
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b == null || b.underConstruction) continue;

            var def = BuildingDefs.Get(b.type);
            if (def.attackRange <= 0f) continue;

            if (b.attackCooldown > 0f) { b.attackCooldown -= dt; continue; }

            var target = NearestEnemyUnit(b, units, def.attackRange);
            if (target == null) continue;

            Projectile.Spawn(b.transform.position + Vector3.up * MuzzleHeight, target, def.attackDamage);
            b.attackCooldown = def.attackInterval;
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
            if (u == null || u.teamId == b.teamId) continue;
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
