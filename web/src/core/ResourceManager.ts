/**
 * Per-team resource ledger — ResourceManager.cs port.
 * Raises onChange whenever a value changes so the HUD can refresh without polling.
 */
import { Age, ResourceKind } from "./GameTypes";

export class ResourceManager {
  food = 200;
  wood = 200;
  gold = 100;
  stone = 100;
  pop = 0;
  popCap = 5;
  age: Age = Age.Dark;

  // Research-based gather rate multipliers (start at 1.0, bumped by techs)
  techGatherFoodMult  = 1.0;
  techGatherWoodMult  = 1.0;
  techGatherGoldMult  = 1.0;
  techGatherStoneMult = 1.0;
  // Research-based unit multipliers
  techCavalrySpeedMult    = 1.0;
  techTradeCartSpeedMult  = 1.0;
  /** Supplies: flat food discount on Militia training cost. */
  techMilitiaFoodDiscount = 0;

  onChange: (() => void) | null = null;

  get(kind: ResourceKind): number {
    switch (kind) {
      case ResourceKind.Food:  return this.food;
      case ResourceKind.Wood:  return this.wood;
      case ResourceKind.Gold:  return this.gold;
      case ResourceKind.Stone: return this.stone;
    }
  }

  gain(kind: ResourceKind, amount: number) {
    if (amount === 0) return;
    switch (kind) {
      case ResourceKind.Food:  this.food  = Math.max(0, this.food  + amount); break;
      case ResourceKind.Wood:  this.wood  = Math.max(0, this.wood  + amount); break;
      case ResourceKind.Gold:  this.gold  = Math.max(0, this.gold  + amount); break;
      case ResourceKind.Stone: this.stone = Math.max(0, this.stone + amount); break;
    }
    this.onChange?.();
  }

  canAfford(food: number, wood: number, gold: number, stone = 0): boolean {
    return this.food >= food && this.wood >= wood && this.gold >= gold && this.stone >= stone;
  }

  deduct(food: number, wood: number, gold: number, stone = 0) {
    this.food  = Math.max(0, this.food  - food);
    this.wood  = Math.max(0, this.wood  - wood);
    this.gold  = Math.max(0, this.gold  - gold);
    this.stone = Math.max(0, this.stone - stone);
    this.onChange?.();
  }
}
