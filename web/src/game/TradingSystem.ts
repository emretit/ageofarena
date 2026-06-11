/**
 * TradingSystem.cs port — Trade Carts travel from the player's Market to an
 * enemy/neutral Market and back, earning gold proportional to trip distance.
 * AoE2 formula: max(8, dist × 0.18) gold per round-trip.
 */
import * as THREE from "three";
import { BuildingType, UnitType } from "../core/GameTypes";
import { ResourceKind } from "../core/GameTypes";
import type { Unit } from "./Unit";
import type { Building } from "./Building";
import type { ResourceManager } from "../core/ResourceManager";

const TRADE_GOLD_PER_UNIT = 0.18;
const MIN_GOLD = 8;
const DEPOSIT_RANGE = 4;

interface TradeRoute {
  homePos: THREE.Vector3;
  targetPos: THREE.Vector3;
  returning: boolean;
  active: boolean;
}

export class TradingSystem {
  private readonly _routes = new Map<Unit, TradeRoute>();

  tick(
    units: Unit[],
    buildings: Building[],
    teamRes: ResourceManager[],
    dt: number,
  ): void {
    for (const u of units) {
      if (!u.alive || u.unitType !== UnitType.TradeCart) continue;
      this._step(u, buildings, teamRes);
    }
  }

  private _step(u: Unit, buildings: Building[], teamRes: ResourceManager[]): void {
    let route = this._routes.get(u);

    if (!route || !route.active) {
      const home   = this._nearestMarket(buildings, u.pos, u.teamId, true);
      const target = this._nearestMarket(buildings, u.pos, u.teamId, false);
      if (!home || !target) return;
      route = { homePos: home.pos.clone(), targetPos: target.pos.clone(), returning: false, active: true };
      this._routes.set(u, route);
      u.moveTo(route.targetPos.clone());
      return;
    }

    if (!route.returning) {
      if (u.pos.distanceTo(route.targetPos) < DEPOSIT_RANGE) {
        route.returning = true;
        u.moveTo(route.homePos.clone());
      }
      return;
    }

    if (u.pos.distanceTo(route.homePos) < DEPOSIT_RANGE) {
      const tripDist = route.homePos.distanceTo(route.targetPos);
      const gold = Math.round(Math.max(MIN_GOLD, tripDist * TRADE_GOLD_PER_UNIT));
      const rm = teamRes[u.teamId];
      if (rm) {
        rm.gold += gold;
        rm.onChange?.();
      }
      route.active = false;
    }
  }

  private _nearestMarket(
    buildings: Building[],
    pos: THREE.Vector3,
    teamId: number,
    ownTeam: boolean,
  ): Building | null {
    let best: Building | null = null;
    let bestDist = Infinity;
    for (const b of buildings) {
      if (!b.alive || b.buildingType !== BuildingType.Market) continue;
      if (ownTeam && b.teamId !== teamId) continue;
      if (!ownTeam && b.teamId === teamId) continue;
      const d = pos.distanceTo(b.pos);
      if (d < bestDist) { bestDist = d; best = b; }
    }
    return best;
  }

  /** Remove dead or deselected carts from state. */
  prune(units: Unit[]): void {
    for (const u of this._routes.keys()) {
      if (!units.includes(u) || !u.alive) this._routes.delete(u);
    }
  }
}
