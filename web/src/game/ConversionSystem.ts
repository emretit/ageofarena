/**
 * ConversionSystem — Monk faith and enemy unit conversion.
 * Port of ConversionSystem.cs.
 */
import { UnitType, UnitState } from '../core/GameTypes';
import { simRng } from '../sim/SimRng';
import type { Unit } from './Unit';

const FAITH_MIN     = 4;   // minimum seconds to convert
const FAITH_MAX     = 10;  // maximum seconds (longer if target near allies)
const RECHARGE_TIME = 30;  // seconds before monk can convert again
const MONK_RANGE    = 5;   // world units — must be within this to convert

export class ConversionSystem {
  /** Called when a unit is converted — view layer re-tints */
  onConverted: ((u: Unit, newTeam: number) => void) | null = null;

  /** faith[unit] → seconds remaining to complete conversion */
  private readonly _faith = new Map<Unit, number>();
  /** recharge[unit] → seconds until next conversion available */
  private readonly _recharge = new Map<Unit, number>();

  tick(units: Unit[], dt: number): void {
    for (const monk of units) {
      if (!monk.alive || monk.unitType !== UnitType.Monk) continue;
      if (monk.teamId !== 0) continue; // only player monks for now (AI TODO)

      // Recharge countdown
      const rch = this._recharge.get(monk) ?? 0;
      if (rch > 0) { this._recharge.set(monk, rch - dt); continue; }

      // Must be targeting an enemy unit
      const target = monk.attackTarget;
      if (!target || !target.alive || target.teamId === monk.teamId) {
        this._faith.delete(monk);
        continue;
      }

      const dx = target.x - monk.x; const dz = target.z - monk.z;
      if (Math.sqrt(dx * dx + dz * dz) > MONK_RANGE) {
        this._faith.delete(monk);
        continue;
      }

      // Advance faith timer
      const faithTime = FAITH_MIN + simRng.range(0, FAITH_MAX - FAITH_MIN);
      let faith = (this._faith.get(monk) ?? 0) + dt;
      this._faith.set(monk, faith);

      if (faith >= faithTime) {
        // Convert!
        this._faith.delete(monk);
        this._recharge.set(monk, RECHARGE_TIME);
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
