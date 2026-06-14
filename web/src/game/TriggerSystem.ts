/**
 * TriggerSystem.ts — Port of TriggerSystem.cs (N11.trig).
 * Generic condition→effect trigger runtime. Fires effects when conditions are met.
 */
import { ResourceKind } from "../core/GameTypes";
import type { Unit } from "./Unit";
import type { Building } from "./Building";
import type { ResourceManager } from "../core/ResourceManager";

export type ConditionType =
  | 'None' | 'Timer' | 'OwnUnits' | 'OwnBuildings' | 'ResourceGathered'
  | 'EnemyEliminated' | 'TechResearched' | 'AgeReached';

export type EffectType =
  | 'None' | 'YouWin' | 'YouLose' | 'ShowMessage' | 'ShowObjective'
  | 'AddResource' | 'ActivateTrigger' | 'DeactivateTrigger' | 'SetGameOver';

export interface TriggerData {
  id:         number;
  enabled:    boolean;
  fired:      boolean;
  oneShot:    boolean;
  conditionType: ConditionType;
  condFloat1: number;
  condInt1:   number;
  condInt2:   number;
  effectType:  EffectType;
  effectStr1:  string;
  effectFloat1: number;
  effectInt1:   number;
  effectInt2:   number;
  effect2Type:  EffectType;
  effect2Str1:  string;
  effect2Float1: number;
  effect2Int1:   number;
}

export function makeTrigger(partial: Partial<TriggerData> & { id: number }): TriggerData {
  return {
    enabled: true, fired: false, oneShot: true,
    conditionType: 'None', condFloat1: 0, condInt1: 0, condInt2: 0,
    effectType: 'None', effectStr1: '', effectFloat1: 0, effectInt1: 0, effectInt2: 0,
    effect2Type: 'None', effect2Str1: '', effect2Float1: 0, effect2Int1: 0,
    ...partial,
  };
}

export class TriggerSystem {
  /** Called by TriggerSystem when YouWin fires. */
  onWin:  ((msg: string) => void) | null = null;
  /** Called by TriggerSystem when YouLose fires. */
  onLose: ((msg: string) => void) | null = null;
  /** Called by TriggerSystem to show a message overlay. */
  onMessage: ((text: string, duration: number) => void) | null = null;

  private _triggers: TriggerData[] = [];
  private _matchTime = 0;
  private _sawEnemy  = false;
  // Accumulated resource counters (not current balance — tracks total deposited)
  private readonly _gathered: Map<ResourceKind, number[]> = new Map([
    [ResourceKind.Food,  []],
    [ResourceKind.Wood,  []],
    [ResourceKind.Gold,  []],
    [ResourceKind.Stone, []],
  ]);

  get matchTime(): number { return this._matchTime; }

  add(t: TriggerData): void { this._triggers.push(t); }
  clear(): void { this._triggers = []; this._matchTime = 0; this._sawEnemy = false; }

  /** Called by GatherSystem when a unit deposits resources. */
  onResourceDeposited(teamId: number, kind: ResourceKind, amount: number): void {
    const arr = this._gathered.get(kind);
    if (!arr) return;
    while (arr.length <= teamId) arr.push(0);
    arr[teamId] += amount;
  }

  tick(
    units: Unit[],
    buildings: Building[],
    teamRes: ResourceManager[],
    hasResearched: (teamId: number, tech: number) => boolean,
    dt: number,
  ): void {
    this._matchTime += dt;

    // Latch: once any enemy unit/building observed, EnemyEliminated can fire
    if (!this._sawEnemy) {
      outer: for (const u of units) {
        if (u.teamId !== 0) { this._sawEnemy = true; break outer; }
      }
      if (!this._sawEnemy) {
        for (const b of buildings) {
          if (b.teamId !== 0) { this._sawEnemy = true; break; }
        }
      }
    }

    for (const t of this._triggers) {
      if (!t.enabled || t.fired) continue;
      if (!this._evalCond(t, units, buildings, teamRes, hasResearched)) continue;

      this._fireEffect(t.effectType, t.effectStr1, t.effectFloat1, t.effectInt1, t.effectInt2, teamRes);
      if (t.effect2Type !== 'None') {
        this._fireEffect(t.effect2Type, t.effect2Str1, t.effect2Float1, t.effect2Int1, 0, teamRes);
      }

      if (t.oneShot) { t.fired = true; t.enabled = false; }
    }
  }

  private _evalCond(
    t: TriggerData, units: Unit[], buildings: Building[],
    teamRes: ResourceManager[], hasResearched: (teamId: number, tech: number) => boolean,
  ): boolean {
    switch (t.conditionType) {
      case 'Timer':            return this._matchTime >= t.condFloat1;
      case 'OwnUnits':         return this._countUnits(t.condInt1, t.condInt2, units) >= t.condFloat1;
      case 'OwnBuildings':     return this._countBuildings(t.condInt1, t.condInt2, buildings) >= t.condFloat1;
      case 'ResourceGathered': return this._getGathered(t.condInt1, t.condInt2) >= t.condFloat1;
      case 'EnemyEliminated':  return this._sawEnemy && this._allEnemiesGone(units, buildings);
      case 'TechResearched':   return hasResearched(t.condInt1, t.condInt2);
      case 'AgeReached':       return (teamRes[t.condInt1]?.age ?? 0) >= t.condInt2;
      default:                 return false;
    }
  }

  private _countUnits(teamId: number, typeFilter: number, units: Unit[]): number {
    let n = 0;
    for (const u of units) {
      if (!u.alive) continue;
      if (u.teamId !== teamId) continue;
      if (typeFilter > 0 && u.unitType !== typeFilter) continue;
      n++;
    }
    return n;
  }

  private _countBuildings(teamId: number, typeFilter: number, buildings: Building[]): number {
    let n = 0;
    for (const b of buildings) {
      if (!b.alive) continue;
      if (b.teamId !== teamId) continue;
      if (typeFilter > 0 && b.buildingType !== typeFilter) continue;
      n++;
    }
    return n;
  }

  private _getGathered(teamId: number, kind: number): number {
    const arr = this._gathered.get(kind as ResourceKind);
    return arr ? (arr[teamId] ?? 0) : 0;
  }

  private _allEnemiesGone(units: Unit[], buildings: Building[]): boolean {
    for (const u of units) if (u.teamId !== 0 && u.alive) return false;
    for (const b of buildings) if (b.teamId !== 0 && b.alive) return false;
    return true;
  }

  private _fireEffect(
    et: EffectType, str1: string, f1: number, i1: number, i2: number,
    teamRes: ResourceManager[],
  ): void {
    switch (et) {
      case 'YouWin':
        this.onWin?.(str1);
        break;
      case 'YouLose':
        this.onLose?.(str1);
        break;
      case 'ShowMessage':
        this.onMessage?.(str1, 3.5);
        break;
      case 'ShowObjective':
        this.onMessage?.(str1, 5.0);
        break;
      case 'AddResource': {
        const rm = teamRes[i1];
        if (!rm) break;
        const kind = i2 as ResourceKind;
        rm.gain(kind, f1);
        break;
      }
      case 'ActivateTrigger':
        this._setEnabled(i1, true);
        break;
      case 'DeactivateTrigger':
        this._setEnabled(i1, false);
        break;
      case 'SetGameOver':
        if (i1 === 0) this.onWin?.(str1);
        else          this.onLose?.(str1);
        break;
    }
  }

  private _setEnabled(id: number, value: boolean): void {
    for (const t of this._triggers) {
      if (t.id === id) { t.enabled = value; break; }
    }
  }
}
