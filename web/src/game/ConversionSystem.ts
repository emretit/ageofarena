/**
 * ConversionSystem — Monk faith and enemy unit conversion.
 * Port of ConversionSystem.cs.
 */
import { UnitType, UnitState } from '../core/GameTypes';
import { simRng } from '../sim/SimRng';
import type { Unit } from './Unit';
import type { Building } from './Building';
import type { ResearchSystem } from './ResearchSystem';
import { TechId } from './ResearchSystem';

const FAITH_MIN     = 4;   // minimum seconds to convert
const FAITH_MAX     = 10;  // maximum seconds (longer if target near allies)
const RECHARGE_TIME = 30;  // seconds before monk can convert again
const MONK_RANGE    = 5;   // world units — must be within this to convert
const BUILDING_FAITH_MIN = 8;  // buildings take longer to convert than units

export class ConversionSystem {
  /** Called when a unit is converted — view layer re-tints */
  onConverted: ((u: Unit, newTeam: number) => void) | null = null;
  /** Called when a building is converted (Redemption tech) — view layer re-tints */
  onBuildingConverted: ((b: Building, newTeam: number) => void) | null = null;

  /** Injected after construction so tick can check Theocracy state. */
  research: ResearchSystem | null = null;

  /** faith[monk] → accumulated seconds of current conversion */
  private readonly _faith = new Map<Unit, number>();
  /** faithGoal[monk] → required seconds for this conversion (rolled once on start) */
  private readonly _faithGoal = new Map<Unit, number>();
  /** recharge[unit] → seconds until next conversion available */
  private readonly _recharge = new Map<Unit, number>();
  /** faith toward building target (Redemption tech) */
  private readonly _faithBuilding = new Map<Unit, number>();

  tick(units: Unit[], dt: number): void {
    for (const monk of units) {
      if (!monk.alive || monk.unitType !== UnitType.Monk) continue;
      if (monk.teamId !== 0) continue; // only player monks for now (AI TODO)

      // Recharge countdown
      const rch = this._recharge.get(monk) ?? 0;
      if (rch > 0) { this._recharge.set(monk, rch - dt); continue; }

      // ── Building conversion (Redemption tech) ──────────────────────────
      const targetBuilding = monk.attackTargetBuilding;
      if (targetBuilding && targetBuilding.alive && targetBuilding.teamId !== monk.teamId) {
        if (this.research?.isResearched(monk.teamId, TechId.Redemption)) {
          const dx = targetBuilding.pos.x - monk.x;
          const dz = targetBuilding.pos.z - monk.z;
          if (Math.sqrt(dx * dx + dz * dz) <= MONK_RANGE) {
            const faith = (this._faithBuilding.get(monk) ?? 0) + dt;
            this._faithBuilding.set(monk, faith);
            if (faith >= BUILDING_FAITH_MIN + simRng.range(0, FAITH_MAX - BUILDING_FAITH_MIN)) {
              const theocracy = this.research?.isResearched(monk.teamId, TechId.Theocracy) ?? false;
              this._faithBuilding.delete(monk);
              this._recharge.set(monk, theocracy ? RECHARGE_TIME * 0.5 : RECHARGE_TIME);
              (targetBuilding as { teamId: number }).teamId = monk.teamId;
              monk.attackTargetBuilding = null;
              this.onBuildingConverted?.(targetBuilding, monk.teamId);
            }
            continue;
          }
        }
        this._faithBuilding.delete(monk);
      }

      // Must be targeting an enemy unit
      const target = monk.attackTarget;
      if (!target || !target.alive || target.teamId === monk.teamId) {
        // Conversion cancelled — reset faith for this monk
        this._faith.delete(monk);
        this._faithGoal.delete(monk);
        this._faithBuilding.delete(monk);
        continue;
      }

      const dx = target.x - monk.x; const dz = target.z - monk.z;
      if (Math.sqrt(dx * dx + dz * dz) > MONK_RANGE) {
        this._faith.delete(monk);
        this._faithGoal.delete(monk);
        continue;
      }

      // Roll faithGoal once when conversion begins (not every tick)
      if (!this._faithGoal.has(monk)) {
        this._faithGoal.set(monk, FAITH_MIN + simRng.range(0, FAITH_MAX - FAITH_MIN));
      }
      const faithTime = this._faithGoal.get(monk)!;
      const faith = (this._faith.get(monk) ?? 0) + dt;
      this._faith.set(monk, faith);

      if (faith >= faithTime) {
        // Convert! Theocracy: halve recharge time for the converting monk's team.
        const theocracy = this.research?.isResearched(monk.teamId, TechId.Theocracy) ?? false;
        this._faith.delete(monk);
        this._faithGoal.delete(monk);
        this._recharge.set(monk, theocracy ? RECHARGE_TIME * 0.5 : RECHARGE_TIME);
        (target as { teamId: number }).teamId = monk.teamId;
        target.attackTarget = null;
        target.attackTargetBuilding = null;
        target.state = UnitState.Idle;
        monk.attackTarget = null;
        this.onConverted?.(target, monk.teamId);
      }
    }
  }

  /** Faith progress for a monk [0..1], or -1 if not converting. */
  faithProgress(monk: Unit): number {
    const f = this._faith.get(monk);
    if (f === undefined) return -1;
    return Math.min(1, f / FAITH_MAX);
  }

  /** Recharge progress [0..1], or 1 if ready. */
  rechargeProgress(monk: Unit): number {
    const r = this._recharge.get(monk) ?? 0;
    if (r <= 0) return 1;
    return 1 - r / RECHARGE_TIME;
  }
}
