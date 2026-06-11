/**
 * CombatSystem.cs port — drives unit vs unit / unit vs building combat.
 * Aggro detection, approach, attack timing, CombatMath damage.
 */
import * as THREE from "three";
import { ArmorClass, BuildingType, DamageType, UnitState } from "../core/GameTypes";
import { ArmorClassFlags } from "../core/GameTypes";
import type { Unit } from "./Unit";
import type { Building } from "./Building";

/** Buildings that auto-shoot: range, pierce damage, fire interval (seconds). */
const BUILDING_COMBAT: Partial<Record<BuildingType, { range: number; dmg: number; interval: number }>> = {
  [BuildingType.TownCenter]: { range: 8, dmg: 5,  interval: 2.5 },
  [BuildingType.Castle]:     { range: 9, dmg: 18, interval: 1.5 },
};

/** CombatMath.NetDamage equivalent. */
function netDamage(
  baseAtk: number,
  bonusVs: Array<{ cls: ArmorClassFlags; bonus: number }>,
  targetArmorClass: ArmorClassFlags,
  targetArmorMelee: number,
  targetArmorPierce: number,
  damageKind: DamageType,
): number {
  let bonus = 0;
  for (const b of bonusVs) {
    if (targetArmorClass & b.cls) bonus += b.bonus;
  }

  let armor = 0;
  if (damageKind === DamageType.Melee)  armor = targetArmorMelee;
  if (damageKind === DamageType.Pierce) armor = targetArmorPierce;
  // Siege bypasses armor (armor = 0)

  return Math.max(1, baseAtk + bonus - armor);
}

export class CombatSystem {
  /** Called whenever a hit lands — (hitWorldPos, damage). */
  onHit: ((pos: THREE.Vector3, dmg: number) => void) | null = null;

  /** Called when a ranged unit fires — (fromPos, toPos, isSplash). */
  onRangedFire: ((from: THREE.Vector3, to: THREE.Vector3, splash: boolean) => void) | null = null;

  private readonly _bldTimers = new Map<Building, number>();

  tick(units: Unit[], buildings: Building[], dt: number) {
    for (const u of units) {
      if (!u.alive || u.isGarrisoned) continue;
      if (u.state === UnitState.Gathering || u.state === UnitState.ReturningToDropoff) continue;

      // Try to find an attack target if idle and has aggro
      if (u.aggroRadius > 0 &&
          u.state === UnitState.Idle &&
          !u.attackTarget &&
          !u.attackTargetBuilding) {
        this._findAggro(u, units, buildings);
      }

      // Advance combat
      if (u.attackTarget || u.attackTargetBuilding) {
        this._tickCombat(u, dt);
      }
    }
  }

  /** Assign a specific attack target (from right-click command). */
  attackUnit(attacker: Unit, target: Unit) {
    attacker.attackTarget = target;
    attacker.attackTargetBuilding = null;
    attacker.state = UnitState.MovingToAttack;
    attacker.moveTo(target.pos);
  }

  attackBuilding(attacker: Unit, target: Building) {
    attacker.attackTargetBuilding = target;
    attacker.attackTarget = null;
    attacker.state = UnitState.MovingToAttack;
    attacker.moveTo(target.pos);
  }

  private _findAggro(u: Unit, units: Unit[], buildings: Building[]) {
    let best: Unit | null = null;
    let bestDist = u.aggroRadius;

    for (const t of units) {
      if (!t.alive || t.teamId === u.teamId) continue;
      const d = u.pos.distanceTo(t.pos);
      if (d < bestDist) { bestDist = d; best = t; }
    }

    if (best) {
      u.attackTarget = best;
      u.state = UnitState.MovingToAttack;
      u.moveTo(best.pos);
    }
  }

  private _tickCombat(u: Unit, dt: number) {
    const target = u.attackTarget;
    const targetB = u.attackTargetBuilding;

    // Validate target still alive
    if (target && !target.alive) {
      u.attackTarget = null;
      u.gatherTarget = null; // resume gather requires explicit re-assign
      u.state = UnitState.Idle;
      return;
    }
    if (targetB && !targetB.alive) {
      u.attackTargetBuilding = null;
      u.gatherTarget = null;
      u.state = UnitState.Idle;
      return;
    }

    const targetPos = target ? target.pos : targetB!.pos;
    const dist = u.pos.distanceTo(targetPos);

    if (dist > u.attackRange) {
      // Approach
      u.state = UnitState.MovingToAttack;
      u.moveTo(targetPos.clone());
      return;
    }

    // In range — stop and attack
    u.stopMoving();
    u.state = UnitState.Attacking;

    // Face target
    const dir = targetPos.clone().sub(u.pos); dir.y = 0;
    if (dir.length() > 0.01) u.root.rotation.y = Math.atan2(dir.x, dir.z);

    u.attackTimer -= dt;
    if (u.attackTimer <= 0) {
      u.attackTimer = u.attackInterval;
      this._applyDamage(u, target, targetB);
    }
  }

  private _applyDamage(
    attacker: Unit,
    target: Unit | null,
    targetB: Building | null,
  ) {
    if (target) {
      const dmg = netDamage(
        attacker.baseAtk,
        attacker.bonusVs,
        target.armorClass,
        target.armorMelee,
        target.armorPierce,
        attacker.damageKind,
      );
      target.takeDamage(dmg);
      this.onHit?.(target.pos, dmg);
      if (attacker.isRanged) {
        this.onRangedFire?.(attacker.pos.clone(), target.pos.clone(), attacker.splashRadius > 0);
      }
    }

    if (targetB) {
      const dmg = netDamage(
        attacker.baseAtk,
        attacker.bonusVs,
        ArmorClass.Building,
        targetB.def.armorMelee,
        targetB.def.armorPierce,
        attacker.damageKind,
      );
      targetB.takeDamage(dmg);
      this.onHit?.(targetB.pos, dmg);
      if (attacker.isRanged) {
        this.onRangedFire?.(attacker.pos.clone(), targetB.pos.clone(), attacker.splashRadius > 0);
      }
    }
  }

  /** Port of BuildingCombatSystem — TC and Castle auto-shoot nearby enemies. */
  tickBuildings(buildings: Building[], units: Unit[], dt: number) {
    for (const b of buildings) {
      if (!b.alive) continue;
      const stats = BUILDING_COMBAT[b.buildingType];
      if (!stats) continue;

      const remaining = (this._bldTimers.get(b) ?? 0) - dt;
      if (remaining > 0) { this._bldTimers.set(b, remaining); continue; }

      // Find closest enemy unit in range
      let target: Unit | null = null;
      let bestDist = stats.range;
      for (const u of units) {
        if (!u.alive || u.isGarrisoned || u.teamId === b.teamId) continue;
        const d = b.pos.distanceTo(u.pos);
        if (d < bestDist) { bestDist = d; target = u; }
      }

      if (target) {
        // Pierce damage against unit armor
        const dmg = Math.max(1, stats.dmg - target.armorPierce);
        target.takeDamage(dmg);
        this.onHit?.(target.pos, dmg);
      }
      this._bldTimers.set(b, stats.interval);
    }
  }
}
