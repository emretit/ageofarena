import { UnitId, BuildingId, VILLAGER_BUILDABLE, Stance, UnitEntity } from '../entities/types';
import { SelectionSystem } from './Selection';
import { TrainingQueue } from './TrainingQueue';
import { IdleVillagerTracker } from '../ui/IdleVillagerTracker';
import { GameWorld } from '../entities/GameWorld';

interface HotkeyDeps {
  selection: SelectionSystem;
  trainingQueue: TrainingQueue;
  idleTracker: IdleVillagerTracker;
  gameWorld: GameWorld;
  moveTo: (x: number, z: number) => void;
  onBuildClick?: (buildingId: BuildingId) => void;
  onStanceClick?: (units: UnitEntity[], stance: Stance) => void;
  onDeleteClick?: () => void;
}

// AoE2-style 5x3 grid hotkeys mapped to keyboard rows
const GRID_KEYS: Record<string, number> = {
  q: 0,  w: 1,  e: 2,  r: 3,  t: 4,
  a: 5,  s: 6,  d: 7,  f: 8,  g: 9,
  z: 10, x: 11, c: 12, v: 13, b: 14,
};

const STANCE_MAP: Stance[] = ['aggressive', 'defensive', 'standGround', 'noAttack'];

export class HotkeySystem {
  private deps: HotkeyDeps;

  constructor(deps: HotkeyDeps) {
    this.deps = deps;

    window.addEventListener('keydown', (e) => {
      // Don't handle if typing in input
      if ((e.target as HTMLElement).tagName === 'INPUT') return;

      const key = e.key.toLowerCase();

      // Grid hotkeys — building train or villager build commands
      if (key in GRID_KEYS) {
        const sel = this.deps.selection.getSelected();
        const gridIndex = GRID_KEYS[key];

        // Building selected: train unit
        if (sel.length === 1 && sel[0].type === 'building' &&
            sel[0].playerIndex === 0 && sel[0].def.trainable.length > 0) {
          this.handleTrainKey(gridIndex);
          e.preventDefault();
          return;
        }

        // Villager(s) selected: build command
        const villagers = sel.filter(s => s.type === 'unit' && s.def.id === 'villager' && s.playerIndex === 0);
        if (villagers.length > 0 && gridIndex < VILLAGER_BUILDABLE.length) {
          this.deps.onBuildClick?.(VILLAGER_BUILDABLE[gridIndex]);
          e.preventDefault();
          return;
        }

        // Military units selected: stance command
        const military = sel.filter(
          (s): s is UnitEntity => s.type === 'unit' && s.def.id !== 'villager' && s.playerIndex === 0
        );
        if (military.length > 0 && gridIndex < STANCE_MAP.length) {
          this.deps.onStanceClick?.(military, STANCE_MAP[gridIndex]);
          e.preventDefault();
          return;
        }
      }

      // Period: cycle idle villager
      if (key === '.') {
        this.handleIdleCycle();
        return;
      }

      // Delete: cancel last queue entry OR delete selected entities
      if (key === 'delete') {
        // If building with queue, cancel last entry
        const sel = this.deps.selection.getSelected();
        if (sel.length === 1 && sel[0].type === 'building' && sel[0].playerIndex === 0) {
          const queue = this.deps.trainingQueue.getQueue(sel[0]);
          if (queue.length > 0) {
            this.handleCancelLast();
            return;
          }
        }
        // Otherwise delete selected entities
        this.deps.onDeleteClick?.();
        return;
      }
    });
  }

  private handleTrainKey(index: number): void {
    const selected = this.deps.selection.getSelected();
    if (selected.length !== 1 || selected[0].type !== 'building') return;
    const building = selected[0];
    if (building.playerIndex !== 0) return;

    const trainable = building.def.trainable;
    if (index >= trainable.length) return;

    this.deps.trainingQueue.enqueue(building, trainable[index] as UnitId);
  }

  private handleIdleCycle(): void {
    const unit = this.deps.idleTracker.cycleNext(this.deps.gameWorld.units, 0);
    if (unit) {
      this.deps.selection.select(unit);
      this.deps.moveTo(unit.position.x, unit.position.z);
    }
  }

  private handleCancelLast(): void {
    const selected = this.deps.selection.getSelected();
    if (selected.length !== 1 || selected[0].type !== 'building') return;
    const building = selected[0];
    if (building.playerIndex !== 0) return;

    const queue = this.deps.trainingQueue.getQueue(building);
    if (queue.length > 0) {
      this.deps.trainingQueue.cancelAt(building, queue.length - 1);
    }
  }
}
