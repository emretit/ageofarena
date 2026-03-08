import * as THREE from 'three';
import { GAME_CONFIG, BaseConfig } from '../config';
import {
  BuildingId, UnitId, BUILDING_DEFS, UNIT_DEFS,
  BuildingEntity, UnitEntity, GameEntity,
} from './types';
import { createBuildingMesh } from './BuildingFactory';
import { createUnitMesh } from './UnitFactory';

export class GameWorld {
  private scene: THREE.Scene;
  entities: GameEntity[] = [];
  buildings: BuildingEntity[] = [];
  units: UnitEntity[] = [];

  constructor(scene: THREE.Scene) {
    this.scene = scene;
  }

  /** Place initial buildings for all bases */
  placeInitialBuildings(): void {
    for (let pi = 0; pi < GAME_CONFIG.bases.length; pi++) {
      const base = GAME_CONFIG.bases[pi];
      const cx = base.center.x;
      const cz = base.center.z;

      // Town Center
      this.addBuilding('townCenter', cx, cz, pi, base);

      // Houses
      this.addBuilding('house', cx - 5, cz - 4, pi, base);
      this.addBuilding('house', cx + 5, cz - 4, pi, base);
      this.addBuilding('house', cx - 5, cz + 4, pi, base);
      this.addBuilding('house', cx + 5, cz + 4, pi, base);

      // Barracks
      this.addBuilding('barracks', cx, cz - 6, pi, base);

      // Archery Range
      this.addBuilding('archeryRange', cx + 6, cz + 2, pi, base);

      // Stable
      this.addBuilding('stable', cx - 6, cz + 2, pi, base);

      // Blacksmith
      this.addBuilding('blacksmith', cx + 3, cz + 6, pi, base);

      // Market
      this.addBuilding('market', cx - 3, cz + 6, pi, base);
    }
  }

  /** Spawn initial units for all bases */
  spawnInitialUnits(): void {
    for (let pi = 0; pi < GAME_CONFIG.bases.length; pi++) {
      const base = GAME_CONFIG.bases[pi];
      const cx = base.center.x;
      const cz = base.center.z;

      // 3 villagers near town center
      for (let i = 0; i < 3; i++) {
        const angle = (i / 3) * Math.PI * 2;
        const ux = cx + Math.cos(angle) * 2.5;
        const uz = cz + Math.sin(angle) * 2.5;
        this.addUnit('villager', ux, uz, pi, base);
      }

      // 2 militia near barracks
      for (let i = 0; i < 2; i++) {
        this.addUnit('militia', cx + (i - 0.5) * 1.2, cz - 7.5, pi, base);
      }

      // 1 archer near archery range
      this.addUnit('archer', cx + 7, cz + 3.5, pi, base);

      // 1 scout cavalry near stable
      this.addUnit('scoutCavalry', cx - 7, cz + 3.5, pi, base);
    }
  }

  addBuilding(buildingId: BuildingId, x: number, z: number, playerIndex: number, base: BaseConfig): BuildingEntity {
    const def = BUILDING_DEFS[buildingId];
    const mesh = createBuildingMesh(buildingId, base.roofColor, base.teamColor);
    mesh.position.set(x, 0, z);
    mesh.userData = { entityType: 'building', entityIndex: this.entities.length };
    this.scene.add(mesh);

    const entity: BuildingEntity = {
      type: 'building',
      def,
      mesh,
      hp: def.hp,
      maxHp: def.hp,
      playerIndex,
      position: new THREE.Vector3(x, 0, z),
      rallyPoint: null,
    };

    this.entities.push(entity);
    this.buildings.push(entity);
    return entity;
  }

  addUnit(unitId: UnitId, x: number, z: number, playerIndex: number, base: BaseConfig): UnitEntity {
    const def = UNIT_DEFS[unitId];
    const mesh = createUnitMesh(unitId, base.teamColor);
    mesh.position.set(x, 0, z);
    mesh.userData = { entityType: 'unit', entityIndex: this.entities.length };
    this.scene.add(mesh);

    const entity: UnitEntity = {
      type: 'unit',
      def,
      mesh,
      hp: def.hp,
      maxHp: def.hp,
      playerIndex,
      position: new THREE.Vector3(x, 0, z),
      targetPos: null,
      state: 'idle',
      stance: 'aggressive',
    };

    this.entities.push(entity);
    this.units.push(entity);
    return entity;
  }

  /** Remove an entity from the world */
  removeEntity(entity: GameEntity): void {
    this.scene.remove(entity.mesh);
    const eIdx = this.entities.indexOf(entity);
    if (eIdx !== -1) this.entities.splice(eIdx, 1);

    if (entity.type === 'unit') {
      const uIdx = this.units.indexOf(entity);
      if (uIdx !== -1) this.units.splice(uIdx, 1);
    } else {
      const bIdx = this.buildings.indexOf(entity);
      if (bIdx !== -1) this.buildings.splice(bIdx, 1);
    }

    // Re-index remaining entities
    for (let i = 0; i < this.entities.length; i++) {
      this.entities[i].mesh.userData.entityIndex = i;
    }
  }

  /** Simple unit movement update */
  update(dt: number): void {
    for (const unit of this.units) {
      if (unit.state === 'moving' && unit.targetPos) {
        const dx = unit.targetPos.x - unit.position.x;
        const dz = unit.targetPos.z - unit.position.z;
        const dist = Math.sqrt(dx * dx + dz * dz);

        if (dist < 0.2) {
          unit.state = 'idle';
          unit.targetPos = null;
        } else {
          const speed = unit.def.speed * dt;
          const moveX = (dx / dist) * Math.min(speed, dist);
          const moveZ = (dz / dist) * Math.min(speed, dist);
          unit.position.x += moveX;
          unit.position.z += moveZ;
          unit.mesh.position.set(unit.position.x, 0, unit.position.z);

          // Face movement direction
          unit.mesh.rotation.y = Math.atan2(dx, dz);
        }
      }
    }
  }

  /** Get entity at a given scene intersection */
  getEntityFromObject(object: THREE.Object3D): GameEntity | null {
    let current: THREE.Object3D | null = object;
    while (current) {
      if (current.userData?.entityIndex !== undefined) {
        return this.entities[current.userData.entityIndex] || null;
      }
      current = current.parent;
    }
    return null;
  }
}
