/**
 * CommandExecutor — single dispatch point for all Command objects.
 * Receives drained commands from CommandBus, resolves entity IDs,
 * and delegates to the appropriate game systems.
 * Invalid/illegal commands are silently dropped.
 */
import type { Unit } from '../game/Unit';
import type { Building } from '../game/Building';
import type { ResourceNode } from '../game/ResourceNode';
import type { GatherSystem } from '../game/GatherSystem';
import type { CombatSystem } from '../game/CombatSystem';
import type { TrainingQueue } from '../game/TrainingQueue';
import type { ResearchSystem, TechId } from '../game/ResearchSystem';
import type { MarketSystem } from '../game/MarketSystem';
import type { GarrisonSystem } from '../game/GarrisonSystem';
import type { PathQueue } from './PathQueue';
import type { ResourceManager } from '../core/ResourceManager';
import type { EntityId } from './EntityIds';
import { type Command, qDecode } from './Command';
import {
  orderMove, orderAttackUnit, orderAttackBuilding,
  orderGather, orderAttackMove, orderPatrol, orderStop,
} from '../game/Orders';

export class CommandExecutor {
  constructor(
    private readonly units: Unit[],
    private readonly buildings: Building[],
    private readonly nodes: ResourceNode[],
    private readonly gather: GatherSystem,
    private readonly combat: CombatSystem,
    private readonly training: TrainingQueue,
    private readonly research: ResearchSystem,
    private readonly market: MarketSystem,
    private readonly garrison: GarrisonSystem,
    private readonly pathQueue: PathQueue,
    private readonly teamRes: ResourceManager[],
  ) {}

  execute(cmds: Command[]): void {
    for (const cmd of cmds) {
      try { this._exec(cmd); } catch { /* illegal command — silent drop */ }
    }
  }

  private _exec(cmd: Command): void {
    switch (cmd.kind) {
      case 'move': {
        const units = this._resolveUnits(cmd.unitIds);
        if (units.length === 0) return;
        if (cmd.queued) {
          const x = qDecode(cmd.qx), z = qDecode(cmd.qz);
          for (const u of units) u.pendingGoals.push([x, z]);
        } else {
          orderMove(units, qDecode(cmd.qx), qDecode(cmd.qz), this.pathQueue);
        }
        break;
      }
      case 'attackMove': {
        const units = this._resolveUnits(cmd.unitIds);
        if (units.length === 0) return;
        orderAttackMove(units, qDecode(cmd.qx), qDecode(cmd.qz), this.pathQueue);
        break;
      }
      case 'patrol': {
        const units = this._resolveUnits(cmd.unitIds);
        if (units.length === 0) return;
        orderPatrol(units, qDecode(cmd.qx), qDecode(cmd.qz), this.pathQueue);
        break;
      }
      case 'stop': {
        const units = this._resolveUnits(cmd.unitIds);
        orderStop(units);
        break;
      }
      case 'attack': {
        const units = this._resolveUnits(cmd.unitIds);
        const target = this._findUnit(cmd.targetId);
        if (!target || !target.alive) return;
        orderAttackUnit(units, target, this.combat, this.pathQueue);
        break;
      }
      case 'attackBuilding': {
        const units = this._resolveUnits(cmd.unitIds);
        const target = this._findBuilding(cmd.targetId);
        if (!target || !target.alive) return;
        orderAttackBuilding(units, target, this.combat, this.pathQueue);
        break;
      }
      case 'gather': {
        const units = this._resolveUnits(cmd.unitIds);
        const node = this._findNode(cmd.nodeId);
        if (!node || node.depleted) return;
        orderGather(units, node, this.buildings, this.gather, this.pathQueue);
        break;
      }
      case 'garrison': {
        const units = this._resolveUnits(cmd.unitIds);
        const b = this._findBuilding(cmd.buildingId);
        if (!b || !b.alive) return;
        for (const u of units) {
          if (u.teamId === cmd.teamId) this.garrison.orderGarrison(u, b);
        }
        break;
      }
      case 'ungarrison': {
        const b = this._findBuilding(cmd.buildingId);
        if (b) this.garrison.ungarrisonAll(b);
        break;
      }
      case 'train': {
        const b = this._findBuilding(cmd.buildingId);
        const rm = this.teamRes[cmd.teamId];
        if (!b || !b.alive || !rm) return;
        this.training.train(b, cmd.unitType, rm);
        break;
      }
      case 'cancelTrain':
        // Not yet implemented in TrainingQueue — silently skip
        break;
      case 'research': {
        const b = this._findBuilding(cmd.buildingId);
        const rm = this.teamRes[cmd.teamId];
        if (!b || !b.alive || !rm) return;
        this.research.start(b, cmd.techId as TechId, rm);
        break;
      }
      case 'placeBuilding':
        // Building creation requires scene — handled by BuildingPlacement callbacks in main.ts.
        // AI building placement is still direct in EnemyAI._tryBuild (needs scene ref).
        break;
      case 'marketBuy': {
        const rm = this.teamRes[cmd.teamId];
        if (rm) this.market.buy(rm, cmd.resource as number);
        break;
      }
      case 'marketSell': {
        const rm = this.teamRes[cmd.teamId];
        if (rm) this.market.sell(rm, cmd.resource as number);
        break;
      }
    }
  }

  private _resolveUnits(ids: EntityId[]): Unit[] {
    return ids.flatMap(id => {
      const u = this.units.find(u => u.id === id);
      return u && u.alive && !u.isGarrisoned ? [u] : [];
    });
  }

  private _findUnit(id: EntityId): Unit | null {
    return this.units.find(u => u.id === id) ?? null;
  }

  private _findBuilding(id: EntityId): Building | null {
    return this.buildings.find(b => b.id === id) ?? null;
  }

  private _findNode(id: EntityId): ResourceNode | null {
    return this.nodes.find(n => n.id === id) ?? null;
  }
}
