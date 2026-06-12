/**
 * RelicSystem.cs port — relics on the map; Monks pick them up and deposit in
 * a Monastery for passive gold income. No conversion (Monk attacks converted
 * units; that's a separate system). Pure gameplay loop only.
 */
import * as THREE from "three";
import { BuildingType, UnitType } from "../core/GameTypes";
import type { Unit } from "./Unit";
import type { Building } from "./Building";
import type { ResourceManager } from "../core/ResourceManager";
import { DMath } from "../sim/DMath";

export const RELIC_GOLD_RATE = 0.5; // gold/sec per held relic (AoE2: ~31.25/min ≈ 0.52/s)
const CAPTURE_RANGE  = 3.5;
const DEPOSIT_RANGE  = 4.0;
const RELIC_COLOR    = 0xf5d060;

export class RelicEntity {
  readonly root: THREE.Mesh;
  teamId = -1;        // -1 = neutral / on ground
  carrier: Unit | null = null;
  heldInMonastery = false;

  get pos(): THREE.Vector3 { return this.root.position; }
  get available(): boolean { return !this.carrier && !this.heldInMonastery; }

  constructor(scene: THREE.Scene, pos: THREE.Vector3) {
    const geo = new THREE.SphereGeometry(0.28, 8, 6);
    const mat = new THREE.MeshLambertMaterial({ color: RELIC_COLOR, emissive: 0xf5a000, emissiveIntensity: 0.4 });
    this.root = new THREE.Mesh(geo, mat);
    this.root.position.copy(pos);
    this.root.position.y = 0.3;
    scene.add(this.root);
  }
}

export class RelicSystem {
  /** Accumulated fractional gold per team. */
  private readonly _gold: number[] = [0, 0, 0, 0];

  tick(
    units: Unit[],
    relics: RelicEntity[],
    buildings: Building[],
    teamRes: ResourceManager[],
    dt: number,
  ): void {
    if (relics.length === 0) return;

    // 1. Carried relics follow their Monk; deposit at friendly Monastery.
    for (const r of relics) {
      if (!r.carrier) continue;
      const monk = r.carrier;
      if (!monk.alive || monk.unitType !== UnitType.Monk) {
        r.carrier = null;
        continue;
      }
      r.root.position.copy(monk.pos);
      r.root.position.y = 1.2;

      // Check deposit into friendly Monastery
      for (const b of buildings) {
        if (!b.alive || b.buildingType !== BuildingType.Monastery || b.teamId !== monk.teamId) continue;
        if (b.pos.distanceTo(monk.pos) > DEPOSIT_RANGE) continue;
        r.heldInMonastery = true;
        r.teamId = monk.teamId;
        r.carrier = null;
        r.root.visible = false;
        break;
      }
    }

    // 2. Nearby Monks pick up available relics (max one relic per Monk).
    const carrying = new Set<Unit>(relics.filter(r => r.carrier).map(r => r.carrier!));
    for (const u of units) {
      if (!u.alive || u.unitType !== UnitType.Monk || carrying.has(u)) continue;
      for (const r of relics) {
        if (!r.available) continue;
        if (u.pos.distanceTo(r.pos) <= CAPTURE_RANGE) {
          r.carrier = u;
          r.teamId = u.teamId;
          carrying.add(u); // prevent same Monk picking up a second relic this tick
          break;
        }
      }
    }

    // 3. Passive gold from relics held in Monasteries.
    for (const r of relics) {
      if (!r.heldInMonastery || r.teamId < 0) continue;
      const team = r.teamId;
      if (team >= teamRes.length) continue;
      this._gold[team] = (this._gold[team] ?? 0) + RELIC_GOLD_RATE * dt;
      if (this._gold[team] >= 1) {
        const whole = Math.floor(this._gold[team]);
        teamRes[team].gold += whole;
        this._gold[team] -= whole;
        teamRes[team].onChange?.();
      }
    }
  }

  /** Spawn N relics at map-centre positions. */
  static spawnRelics(scene: THREE.Scene, count: number, seed = 7): RelicEntity[] {
    const relics: RelicEntity[] = [];
    for (let i = 0; i < count; i++) {
      const ang = (i / count) * Math.PI * 2 + 0.3;
      const r = 18 + (i % 2) * 8;
      relics.push(new RelicEntity(scene, new THREE.Vector3(DMath.cos(ang) * r, 0, DMath.sin(ang) * r)));
    }
    return relics;
  }
}
