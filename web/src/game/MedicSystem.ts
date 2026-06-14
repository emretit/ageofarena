/**
 * MedicSystem — Medic units heal nearby damaged allies.
 * Port of the Unity Medic role (UnitEntity.cs heal branch). Unlike the Monk's
 * ConversionSystem (player-only TODO), medics work for every team.
 */
import { UnitType } from '../core/GameTypes';
import { SpatialHash } from '../sim/SpatialHash';
import type { Unit } from './Unit';

const MEDIC_RANGE = 5;    // world units — allies within this radius are healed
const HEAL_RATE   = 6;    // HP per second restored to each nearby damaged ally

export class MedicSystem {
  private readonly hash = new SpatialHash<Unit>();
  private readonly _scratch: Unit[] = [];

  tick(units: Unit[], dt: number): void {
    let anyMedic = false;
    for (const u of units) {
      if (u.alive && u.unitType === UnitType.Medic) { anyMedic = true; break; }
    }
    if (!anyMedic) return;

    this.hash.rebuild(units);
    const heal = HEAL_RATE * dt;

    for (const medic of units) {
      if (!medic.alive || medic.isGarrisoned || medic.unitType !== UnitType.Medic) continue;
      this._scratch.length = 0;
      for (const ally of this.hash.query(medic.x, medic.z, MEDIC_RANGE, this._scratch)) {
        if (ally === medic || !ally.alive || ally.isGarrisoned) continue;
        if (ally.teamId !== medic.teamId) continue;
        if (ally.hp >= ally.maxHp) continue;
        ally.hp = Math.min(ally.maxHp, ally.hp + heal);
        ally.refreshHpBar();
      }
    }
  }
}
