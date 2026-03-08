import { GAME_CONFIG } from '../config';
import { BuildingEntity, UnitEntity } from '../entities/types';

export interface PlayerResources {
  food: number;
  wood: number;
  gold: number;
  pop: number;
  popCap: number;
}

const TC_BASE_POP = 5;

export class ResourceManager {
  private players: PlayerResources[];

  constructor(playerCount: number) {
    const sr = GAME_CONFIG.startingResources;
    this.players = [];
    for (let i = 0; i < playerCount; i++) {
      this.players.push({
        food: sr.food,
        wood: sr.wood,
        gold: sr.gold,
        pop: 0,
        popCap: TC_BASE_POP,
      });
    }
  }

  getResources(playerIndex: number): PlayerResources {
    return this.players[playerIndex];
  }

  canAfford(playerIndex: number, cost: { food: number; wood: number; gold: number }): boolean {
    const r = this.players[playerIndex];
    return r.food >= cost.food && r.wood >= cost.wood && r.gold >= cost.gold;
  }

  deduct(playerIndex: number, cost: { food: number; wood: number; gold: number }): void {
    const r = this.players[playerIndex];
    r.food -= cost.food;
    r.wood -= cost.wood;
    r.gold -= cost.gold;
  }

  refund(playerIndex: number, cost: { food: number; wood: number; gold: number }, fraction: number): void {
    const r = this.players[playerIndex];
    r.food += Math.floor(cost.food * fraction);
    r.wood += Math.floor(cost.wood * fraction);
    r.gold += Math.floor(cost.gold * fraction);
  }

  recalcPopCap(playerIndex: number, buildings: BuildingEntity[]): void {
    let cap = TC_BASE_POP;
    for (const b of buildings) {
      if (b.playerIndex === playerIndex && b.def.popSpace) {
        cap += b.def.popSpace;
      }
    }
    this.players[playerIndex].popCap = cap;
  }

  recalcPop(playerIndex: number, units: UnitEntity[]): void {
    let count = 0;
    for (const u of units) {
      if (u.playerIndex === playerIndex) count++;
    }
    this.players[playerIndex].pop = count;
  }

  hasPopSpace(playerIndex: number): boolean {
    const r = this.players[playerIndex];
    return r.pop < r.popCap;
  }
}
