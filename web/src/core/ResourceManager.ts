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

  /** Rolling income rates (per second) — updated every ~2s via tickRate(). */
  rateFood = 0; rateWood = 0; rateGold = 0; rateStone = 0;
  private _accFood = 0; private _accWood = 0; private _accGold = 0; private _accStone = 0;
  private _rateWindow = 0;
  private static readonly RATE_INTERVAL = 2;

  /** Call from the sim loop each tick to maintain rolling income display. */
  tickRate(dt: number): void {
    this._rateWindow += dt;
    if (this._rateWindow >= ResourceManager.RATE_INTERVAL) {
      this.rateFood  = this._accFood  / this._rateWindow;
      this.rateWood  = this._accWood  / this._rateWindow;
      this.rateGold  = this._accGold  / this._rateWindow;
      this.rateStone = this._accStone / this._rateWindow;
      this._accFood = this._accWood = this._accGold = this._accStone = 0;
      this._rateWindow = 0;
    }
  }

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
      case ResourceKind.Food:  this.food  = Math.max(0, this.food  + amount); if (amount > 0) this._accFood  += amount; break;
      case ResourceKind.Wood:  this.wood  = Math.max(0, this.wood  + amount); if (amount > 0) this._accWood  += amount; break;
      case ResourceKind.Gold:  this.gold  = Math.max(0, this.gold  + amount); if (amount > 0) this._accGold  += amount; break;
      case ResourceKind.Stone: this.stone = Math.max(0, this.stone + amount); if (amount > 0) this._accStone += amount; break;
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
