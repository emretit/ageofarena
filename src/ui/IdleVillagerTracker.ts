import { UnitEntity } from '../entities/types';

export class IdleVillagerTracker {
  private lastIndex = -1;

  getIdleVillagers(units: UnitEntity[], playerIndex: number): UnitEntity[] {
    return units.filter(
      u => u.playerIndex === playerIndex && u.def.id === 'villager' && u.state === 'idle'
    );
  }

  cycleNext(units: UnitEntity[], playerIndex: number): UnitEntity | null {
    const idle = this.getIdleVillagers(units, playerIndex);
    if (idle.length === 0) return null;
    this.lastIndex = (this.lastIndex + 1) % idle.length;
    return idle[this.lastIndex];
  }

  getCount(units: UnitEntity[], playerIndex: number): number {
    return this.getIdleVillagers(units, playerIndex).length;
  }
}
