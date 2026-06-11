/**
 * MarketSystem.ts — port of MarketSystem.cs.
 * Fluctuating-price resource exchange. Sell/Buy shift prices; drift restores base.
 * Costs: BaseSell 0.7 / BaseBuy 1.3, batch=100 resources, PriceShift=0.05.
 */
import { ResourceKind } from "../core/GameTypes";
import type { ResourceManager } from "../core/ResourceManager";

const BaseSell   = 0.7;
const BaseBuy    = 1.3;
const PriceShift = 0.05;
const DriftRate  = 0.003;  // per second
const MinSell    = 0.3;
const MaxBuy     = 2.5;
const Batch      = 100;    // resources per transaction

/** Market prices are shared across all player instances (1v1). */
const _sellRate: number[] = [BaseSell, BaseSell, BaseSell]; // food, wood, stone
const _buyRate:  number[] = [BaseBuy,  BaseBuy,  BaseBuy];

function idx(kind: ResourceKind): number | null {
  switch (kind) {
    case ResourceKind.Food:  return 0;
    case ResourceKind.Wood:  return 1;
    case ResourceKind.Stone: return 2;
    default: return null; // Gold can't be traded for gold
  }
}

export class MarketSystem {
  /** Drift prices back toward base each tick. */
  tick(dt: number) {
    for (let i = 0; i < 3; i++) {
      _sellRate[i] = moveTowards(_sellRate[i], BaseSell, DriftRate * dt);
      _buyRate[i]  = moveTowards(_buyRate[i],  BaseBuy,  DriftRate * dt);
      // Maintain minimum spread
      if (_buyRate[i] < _sellRate[i] + 0.2) _buyRate[i] = _sellRate[i] + 0.2;
    }
  }

  /** Gold received for selling 100 of kind. */
  sellGold(kind: ResourceKind): number {
    const i = idx(kind);
    return i === null ? 0 : Math.round(Batch * _sellRate[i]);
  }

  /** Gold cost to buy 100 of kind. */
  buyCost(kind: ResourceKind): number {
    const i = idx(kind);
    return i === null ? 0 : Math.round(Batch * _buyRate[i]);
  }

  /** Sell 100 of kind for gold. Returns gold received, or 0 on failure. */
  sell(rm: ResourceManager, kind: ResourceKind): number {
    const i = idx(kind);
    if (i === null) return 0;
    if (rm.get(kind) < Batch) return 0;
    const gold = this.sellGold(kind);
    rm.deduct(
      kind === ResourceKind.Food  ? Batch : 0,
      kind === ResourceKind.Wood  ? Batch : 0,
      0,
      kind === ResourceKind.Stone ? Batch : 0,
    );
    rm.gain(ResourceKind.Gold, gold);
    _sellRate[i] = Math.max(MinSell, _sellRate[i] - PriceShift);
    _buyRate[i]  = Math.max(_sellRate[i] + 0.2, _buyRate[i] - PriceShift * 0.5);
    return gold;
  }

  /** Buy 100 of kind with gold. Returns true on success. */
  buy(rm: ResourceManager, kind: ResourceKind): boolean {
    const i = idx(kind);
    if (i === null) return false;
    const cost = this.buyCost(kind);
    if (!rm.canAfford(0, 0, cost)) return false;
    rm.deduct(0, 0, cost);
    rm.gain(kind, Batch);
    _buyRate[i]  = Math.min(MaxBuy, _buyRate[i]  + PriceShift);
    _sellRate[i] = Math.min(_buyRate[i] - 0.2, _sellRate[i] + PriceShift * 0.5);
    return true;
  }

  /** Reset prices (call on game start). */
  reset() {
    for (let i = 0; i < 3; i++) { _sellRate[i] = BaseSell; _buyRate[i] = BaseBuy; }
  }
}

function moveTowards(current: number, target: number, delta: number): number {
  return Math.abs(target - current) <= delta ? target
    : current + Math.sign(target - current) * delta;
}
