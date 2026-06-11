/**
 * GarrisonSystem.cs port — units walk to a friendly building, hide inside,
 * heal over time, and ungarrison on command.
 * Extra arrows from garrisoned units are handled in CombatSystem.tickBuildings.
 */
import * as THREE from "three";
import { BuildingType, UnitState } from "../core/GameTypes";
import { getTeamBonus } from "../core/CivState";
import type { Unit } from "./Unit";
import type { Building } from "./Building";

const HEAL_RATE = 5;   // hp/sec per garrisoned unit
const GATE_BACK = 3.5; // emerge-offset in front of building

/** Buildings that can accept garrison (TC, Castle; extend as needed). */
const GARRISON_CAP: Partial<Record<BuildingType, number>> = {
  [BuildingType.TownCenter]: 15,
  [BuildingType.Castle]:     20,
};

export class GarrisonSystem {
  /** Map of building → list of garrisoned units. */
  private readonly _garrisons = new Map<Building, Unit[]>();

  garrisonCount(b: Building): number {
    return this._garrisons.get(b)?.length ?? 0;
  }

  canGarrison(b: Building): boolean {
    const cap = GARRISON_CAP[b.buildingType];
    if (!cap) return false;
    return this.garrisonCount(b) < cap;
  }

  /** Order a unit to walk toward a building and garrison. */
  orderGarrison(u: Unit, b: Building): void {
    if (u.isGarrisoned || !this.canGarrison(b)) return;
    u.garrisonTarget = b;
    u.moveTo(b.pos.clone());
  }

  tick(units: Unit[], buildings: Building[], dt: number): void {
    // 1. Entry — unit arrives at its garrison target.
    for (const u of units) {
      if (!u.alive || u.isGarrisoned || !u.garrisonTarget) continue;
      const b = u.garrisonTarget;
      if (!b.alive) { u.garrisonTarget = null; continue; }
      const reach = 3.0;
      if (u.pos.distanceTo(b.pos) > reach) continue;
      // Re-check cap at the moment of entry (not just at order-time) to prevent overflow.
      if (!this.canGarrison(b)) { u.garrisonTarget = null; continue; }

      let list = this._garrisons.get(b);
      if (!list) { list = []; this._garrisons.set(b, list); }
      list.push(u);
      u.garrisonTarget = null;
      u.isGarrisoned = true;
      u.root.visible = false;
    }

    // 2. Heal garrisoned units.
    for (const [b, list] of this._garrisons) {
      if (!b.alive) {
        // Building destroyed → kill all inside; restore visibility so dead units clean up normally.
        for (const u of list) {
          u.hp = 0;
          u.isGarrisoned = false;
          u.root.visible = true; // needed so dead-unit removal loop can process them
        }
        this._garrisons.delete(b);
        continue;
      }
      for (const u of list) {
        u.hp = Math.min(u.maxHp, u.hp + HEAL_RATE * getTeamBonus(u.teamId).healRateMult * dt);
      }
    }
  }

  /** Eject all units from building b to its rally point or gate. */
  ungarrisonAll(b: Building): void {
    const list = this._garrisons.get(b);
    if (!list?.length) return;
    const gate = b.pos.clone().add(new THREE.Vector3(0, 0, -GATE_BACK));
    const dest = b.rallyPoint ?? gate;
    list.forEach((u, i) => {
      const emerge = gate.clone().add(new THREE.Vector3((i % 4) * 1.1 - 1.65, 0, -(Math.floor(i / 4)) * 1.1));
      u.isGarrisoned = false;
      u.root.visible = true;
      u.root.position.copy(emerge);
      u.attackTarget = null;
      u.attackTargetBuilding = null;
      u.state = UnitState.Idle;
      u.moveTo(dest.clone());
    });
    list.length = 0;
  }

  /** Ungarrison a single unit (used when player clicks Eject One in HUD). */
  ungarrisonOne(b: Building): Unit | null {
    const list = this._garrisons.get(b);
    if (!list?.length) return null;
    const u = list.pop()!;
    const gate = b.pos.clone().add(new THREE.Vector3(0, 0, -GATE_BACK));
    u.isGarrisoned = false;
    u.root.visible = true;
    u.root.position.copy(gate);
    u.attackTarget = null;
    u.attackTargetBuilding = null;
    u.state = UnitState.Idle;
    u.moveTo((b.rallyPoint ?? gate).clone());
    return u;
  }
}
