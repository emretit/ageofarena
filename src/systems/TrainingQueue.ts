import { UnitId, UNIT_DEFS, BuildingEntity } from '../entities/types';
import { ResourceManager } from './ResourceManager';
import { GameWorld } from '../entities/GameWorld';
import { GAME_CONFIG } from '../config';

export interface QueueEntry {
  unitId: UnitId;
  remainingTime: number;
  totalTime: number;
}

const MAX_QUEUE_SIZE = 5;

export class TrainingQueue {
  private queues = new Map<BuildingEntity, QueueEntry[]>();
  private resourceManager: ResourceManager;
  private gameWorld: GameWorld;

  constructor(resourceManager: ResourceManager, gameWorld: GameWorld) {
    this.resourceManager = resourceManager;
    this.gameWorld = gameWorld;
  }

  enqueue(building: BuildingEntity, unitId: UnitId): boolean {
    const queue = this.queues.get(building) || [];
    if (queue.length >= MAX_QUEUE_SIZE) return false;

    const unitDef = UNIT_DEFS[unitId];
    if (!unitDef) return false;

    const pi = building.playerIndex;
    if (!this.resourceManager.canAfford(pi, unitDef.cost)) return false;
    if (!this.resourceManager.hasPopSpace(pi)) return false;

    this.resourceManager.deduct(pi, unitDef.cost);

    queue.push({
      unitId,
      remainingTime: unitDef.trainTime,
      totalTime: unitDef.trainTime,
    });

    if (!this.queues.has(building)) {
      this.queues.set(building, queue);
    }

    return true;
  }

  cancelAt(building: BuildingEntity, index: number): void {
    const queue = this.queues.get(building);
    if (!queue || index < 0 || index >= queue.length) return;

    const entry = queue[index];
    const unitDef = UNIT_DEFS[entry.unitId];
    // 75% refund
    this.resourceManager.refund(building.playerIndex, unitDef.cost, 0.75);

    queue.splice(index, 1);
    if (queue.length === 0) {
      this.queues.delete(building);
    }
  }

  getQueue(building: BuildingEntity): QueueEntry[] {
    return this.queues.get(building) || [];
  }

  update(dt: number): void {
    for (const [building, queue] of this.queues) {
      if (queue.length === 0) continue;

      const entry = queue[0];
      entry.remainingTime -= dt;

      if (entry.remainingTime <= 0) {
        // Spawn the unit
        const pi = building.playerIndex;
        const base = GAME_CONFIG.bases[pi];
        const spawnOffset = building.def.size.w / 2 + 1.5;

        // Spawn position: in front of building
        const sx = building.position.x + spawnOffset;
        const sz = building.position.z;

        const unit = this.gameWorld.addUnit(entry.unitId, sx, sz, pi, base);

        // If building has rally point, send unit there
        if (building.rallyPoint) {
          unit.targetPos = building.rallyPoint.clone();
          unit.state = 'moving';
        }

        this.resourceManager.recalcPop(pi, this.gameWorld.units);

        queue.shift();
        if (queue.length === 0) {
          this.queues.delete(building);
        }
      }
    }
  }
}
