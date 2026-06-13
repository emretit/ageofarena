/**
 * CombatSystem.cs port — drives unit vs unit / unit vs building combat.
 * Aggro detection, approach, attack timing, CombatMath damage.
 */
import * as THREE from "three";
import { ArmorClass, AttackStance, BuildingType, DamageType, UnitState } from "../core/GameTypes";
import { ArmorClassFlags } from "../core/GameTypes";
import { SpatialHash } from "../sim/SpatialHash";
import { diplomacy } from "../core/Diplomacy";
import type { Unit } from "./Unit";
import type { Building } from "./Building";

/** Buildings that auto-shoot: range, pierce damage, fire interval (seconds). */
const BUILDING_COMBAT: Partial<Record<BuildingType, { range: number; dmg: number; interval: number }>> = {
  [BuildingType.TownCenter]: { range: 8, dmg: 5,  interval: 2.5 },
  [BuildingType.Castle]:     { range: 9, dmg: 18, interval: 1.5 },
  [BuildingType.WatchTower]: { range: 7, dmg: 6,  interval: 2.0 },
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

  /** Called when a unit is killed (hp → 0). */
  onUnitKilled: ((u: import("./Unit").Unit) => void) | null = null;

  /** Called when a building is destroyed (hp → 0). */
  onBuildingDestroyed: ((b: import("./Building").Building) => void) | null = null;

  private readonly _bldTimers = new Map<Building, number>();
  private readonly _spatial = new SpatialHash<Unit>();

  // Pending ranged hits — damage applied on arrival, not instantly
  private readonly _pending: Array<{
    target: Unit | null; targetB: Building | null;
    damage: number; timeLeft: number;
    fromPos: THREE.Vector3; toPos: THREE.Vector3;
    splash: boolean; splashRadius: number; attackerTeam: number;
    ballistics: boolean;
  }> = [];

  tick(units: Unit[], buildings: Building[], dt: number) {
    // Advance pending ranged hits (damage on arrival)
    for (let i = this._pending.length - 1; i >= 0; i--) {
      const p = this._pending[i];
      p.timeLeft -= dt;
      if (p.timeLeft <= 0) {
        this._pending.splice(i, 1);
        if (p.splash && p.splashRadius > 0) {
          // Splash damage: apply to all alive enemy units within splashRadius of landing point
          const r2 = p.splashRadius * p.splashRadius;
          for (const u of units) {
            if (!u.alive || u.isGarrisoned || !diplomacy.isEnemy(u.teamId, p.attackerTeam)) continue;
            const dx = u.x - p.toPos.x; const dz = u.z - p.toPos.z;
            const d2 = dx * dx + dz * dz;
            if (d2 <= r2) {
              const falloff = 1 - Math.sqrt(d2) / p.splashRadius;
              const dmg = Math.max(1, Math.round(p.damage * falloff));
              u.takeDamage(dmg);
              this.onHit?.(u.pos, dmg);
              if (!u.alive) this.onUnitKilled?.(u);
            }
          }
        } else if (p.target && p.target.alive) {
          // Without Ballistics a projectile lands at the fire-time point — if the
          // target has moved away from that landing spot, it misses.
          const HIT_RADIUS = 0.9;
          const dx = p.target.x - p.toPos.x; const dz = p.target.z - p.toPos.z;
          const hit = p.ballistics || (dx * dx + dz * dz) <= HIT_RADIUS * HIT_RADIUS;
          if (hit) {
            p.target.takeDamage(p.damage);
            this.onHit?.(p.target.pos, p.damage);
            if (!p.target.alive) this.onUnitKilled?.(p.target);
          }
          // else: misses, lands harmlessly (DoD: "Ballistics'siz koşan hedefe ıska")
        } else if (p.targetB && p.targetB.alive) {
          p.targetB.takeDamage(p.damage);
          this.onHit?.(p.targetB.pos, p.damage);
          if (!p.targetB.alive) this.onBuildingDestroyed?.(p.targetB);
        }
        // If target already dead and no splash: lands harmlessly (DoD: "ölü hedefe boşa")
      }
    }

    // Rebuild spatial hash once per tick for O(1) aggro queries
    this._spatial.rebuild(units.filter(u => u.alive && !u.isGarrisoned));

    for (const u of units) {
      if (!u.alive || u.isGarrisoned) continue;
      if (u.state === UnitState.Gathering || u.state === UnitState.ReturningToDropoff) continue;

      // NoAttack stance: ignore all enemies
      if (u.stance === AttackStance.NoAttack) continue;

      // Try to find an attack target if idle/attack-moving and has aggro
      const canAggro = u.state === UnitState.Idle || u.state === UnitState.AttackMove || u.state === UnitState.Patrol;
      if (u.aggroRadius > 0 && canAggro && !u.attackTarget && !u.attackTargetBuilding) {
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

  private _findAggro(u: Unit, _units: Unit[], _buildings: Building[]) {
    // Defensive stance: only aggro if already attacked (attackTarget set externally)
    if (u.stance === AttackStance.Defensive && u.state !== UnitState.MovingToAttack) {
      // Defensive units don't self-aggro; they respond when hit (handled in takeDamage)
      return;
    }

    let best: Unit | null = null;
    let bestDist = u.aggroRadius;

    const candidates: Unit[] = [];
    this._spatial.query(u.x, u.z, u.aggroRadius, candidates);
    for (const t of candidates) {
      if (!diplomacy.isEnemy(t.teamId, u.teamId)) continue;
      const dx = t.x - u.x; const dz = t.z - u.z;
      const d = Math.sqrt(dx * dx + dz * dz);
      if (d < bestDist) { bestDist = d; best = t; }
    }

    if (best) {
      // Record home pos for Defensive leash before chasing
      if (u.stance === AttackStance.Defensive) {
        u.defensiveHomeX = u.x;
        u.defensiveHomeZ = u.z;
      }
      const prevState = u.state;
      u.attackTarget = best;
      // AttackMove/Patrol: preserve state so we resume route when target dies
      if (prevState !== UnitState.AttackMove && prevState !== UnitState.Patrol) {
        u.state = UnitState.MovingToAttack;
        u.moveTo(best.pos);
      }
    }
  }

  private _tickCombat(u: Unit, dt: number) {
    const target = u.attackTarget;
    const targetB = u.attackTargetBuilding;

    // Validate target still alive
    if (target && !target.alive) {
      u.attackTarget = null;
      u.gatherTarget = null;
      // Preserve AttackMove/Patrol state; revert Attacking→Idle
      if (u.state === UnitState.Attacking) u.state = UnitState.Idle;
      return;
    }
    if (targetB && !targetB.alive) {
      u.attackTargetBuilding = null;
      u.gatherTarget = null;
      if (u.state === UnitState.Attacking) u.state = UnitState.Idle;
      return;
    }

    const targetPos = target ? target.pos : targetB!.pos;
    const dist = u.pos.distanceTo(targetPos);

    if (dist > u.attackRange) {
      // StandGround: don't move to chase
      if (u.stance === AttackStance.StandGround) {
        u.attackTarget = null;
        u.attackTargetBuilding = null;
        // Resume attack-move path if applicable
        if (u.state === UnitState.AttackMove) { /* keep state */ }
        else u.state = UnitState.Idle;
        return;
      }
      // Defensive leash: retreat if too far from home
      if (u.stance === AttackStance.Defensive) {
        const hDx = u.x - u.defensiveHomeX; const hDz = u.z - u.defensiveHomeZ;
        if (Math.sqrt(hDx * hDx + hDz * hDz) > 8) {
          u.attackTarget = null;
          u.attackTargetBuilding = null;
          u.moveTo(new THREE.Vector3(u.defensiveHomeX, 0, u.defensiveHomeZ));
          u.state = UnitState.Moving;
          return;
        }
      }
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
    const PROJ_SPEED = 22; // world units/s — matches game/ProjectileSystem

    if (target) {
      const dmg = netDamage(
        attacker.baseAtk,
        attacker.bonusVs,
        target.armorClass,
        target.armorMelee,
        target.armorPierce,
        attacker.damageKind,
      );
      const fromPos = attacker.pos.clone();
      const toPos   = target.pos.clone();
      if (attacker.isRanged) {
        const dist = fromPos.distanceTo(toPos);
        const flightTime = dist / PROJ_SPEED;
        // Ballistics: lead the target by its velocity so the projectile arrives where it will be.
        if (attacker.hasBallistics) {
          toPos.x += target.velX * flightTime;
          toPos.z += target.velZ * flightTime;
        }
        this.onRangedFire?.(fromPos, toPos, attacker.splashRadius > 0);
        this._pending.push({ target, targetB: null, damage: dmg, timeLeft: flightTime, fromPos, toPos, splash: attacker.splashRadius > 0, splashRadius: attacker.splashRadius, attackerTeam: attacker.teamId, ballistics: attacker.hasBallistics });
      } else {
        target.takeDamage(dmg);
        this.onHit?.(toPos, dmg);
        if (!target.alive) this.onUnitKilled?.(target);
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
      const fromPos = attacker.pos.clone();
      const toPos   = targetB.pos.clone();
      if (attacker.isRanged) {
        const dist = fromPos.distanceTo(toPos);
        this.onRangedFire?.(fromPos, toPos, attacker.splashRadius > 0);
        this._pending.push({ target: null, targetB, damage: dmg, timeLeft: dist / PROJ_SPEED, fromPos, toPos, splash: attacker.splashRadius > 0, splashRadius: attacker.splashRadius, attackerTeam: attacker.teamId, ballistics: true });
      } else {
        targetB.takeDamage(dmg);
        this.onHit?.(toPos, dmg);
        if (!targetB.alive) this.onBuildingDestroyed?.(targetB);
      }
    }
  }

  /** Port of BuildingCombatSystem — TC/Castle/WatchTower auto-shoot nearby enemies.
   *  garrisonCount: optional callback for garrison arrow bonus ×(1+0.4n) cap 5. */
  tickBuildings(
    buildings: Building[], units: Unit[], dt: number,
    garrisonCount?: (b: Building) => number,
  ) {
    for (const b of buildings) {
      if (!b.alive) continue;
      const stats = BUILDING_COMBAT[b.buildingType];
      if (!stats) continue;

      const remaining = (this._bldTimers.get(b) ?? 0) - dt;
      if (remaining > 0) { this._bldTimers.set(b, remaining); continue; }

      // Find closest enemy unit in range via SpatialHash
      let target: Unit | null = null;
      let bestDist = stats.range;
      const nearby: Unit[] = [];
      this._spatial.query(b.pos.x, b.pos.z, stats.range, nearby);
      for (const u of nearby) {
        if (u.isGarrisoned || !diplomacy.isEnemy(u.teamId, b.teamId)) continue;
        const dx = u.x - b.pos.x; const dz = u.z - b.pos.z;
        const d = Math.sqrt(dx * dx + dz * dz);
        if (d < bestDist) { bestDist = d; target = u; }
      }

      if (target) {
        const n    = garrisonCount ? garrisonCount(b) : 0;
        const mult = Math.min(5, 1 + 0.4 * n); // ×(1+0.4n) cap 5
        const dmg  = Math.max(1, Math.round((stats.dmg - target.armorPierce) * mult));
        target.takeDamage(dmg);
        this.onHit?.(target.pos, dmg);
        if (!target.alive) this.onUnitKilled?.(target);
        this.onRangedFire?.(b.pos.clone(), target.pos.clone(), false);
      }
      this._bldTimers.set(b, stats.interval);
    }
  }
}
