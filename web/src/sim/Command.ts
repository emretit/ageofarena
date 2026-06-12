/**
 * Command.ts — serializable game command discriminated union.
 * All player + AI actions flow through CommandBus as Command objects.
 * Positions encoded as q-integers (round(x*256)) for determinism.
 *
 * Port of: Command.cs intent (Faz 13 refactor).
 */
import type { EntityId } from "./EntityIds";
import type { BuildingType, ResourceKind, UnitType } from "../core/GameTypes";
import type { TechId } from "../game/ResearchSystem";

/** Quantize world position to fixed-point integer (round × 256). */
export function qEncode(v: number): number { return Math.round(v * 256); }
export function qDecode(q: number): number { return q / 256; }

interface BaseCmd {
  tick:   number;   // sim tick when command was issued
  seq:    number;   // per-team sequence number (for ordering)
  teamId: number;
  ai:     boolean;  // true = EnemyAI command (not sent over network)
}

export interface MoveCmd extends BaseCmd        { kind: 'move';          unitIds: EntityId[]; qx: number; qz: number; queued: boolean; }
export interface AttackCmd extends BaseCmd       { kind: 'attack';        unitIds: EntityId[]; targetId: EntityId; }
export interface AttackBuildingCmd extends BaseCmd{ kind: 'attackBuilding'; unitIds: EntityId[]; targetId: EntityId; }
export interface AttackMoveCmd extends BaseCmd  { kind: 'attackMove';    unitIds: EntityId[]; qx: number; qz: number; }
export interface PatrolCmd extends BaseCmd      { kind: 'patrol';        unitIds: EntityId[]; qx: number; qz: number; }
export interface StopCmd extends BaseCmd        { kind: 'stop';          unitIds: EntityId[]; }
export interface GatherCmd extends BaseCmd      { kind: 'gather';        unitIds: EntityId[]; nodeId: EntityId; }
export interface GarrisonCmd extends BaseCmd    { kind: 'garrison';      unitIds: EntityId[]; buildingId: EntityId; }
export interface UngarrisonCmd extends BaseCmd  { kind: 'ungarrison';    buildingId: EntityId; }
export interface TrainCmd extends BaseCmd       { kind: 'train';         buildingId: EntityId; unitType: UnitType; }
export interface CancelTrainCmd extends BaseCmd { kind: 'cancelTrain';   buildingId: EntityId; }
export interface ResearchCmd extends BaseCmd    { kind: 'research';      buildingId: EntityId; techId: TechId; }
export interface PlaceBuildingCmd extends BaseCmd{ kind: 'placeBuilding'; unitIds: EntityId[]; buildingType: BuildingType; qx: number; qz: number; }
export interface MarketBuyCmd extends BaseCmd   { kind: 'marketBuy';     resource: ResourceKind; }
export interface MarketSellCmd extends BaseCmd  { kind: 'marketSell';    resource: ResourceKind; }

export type Command =
  | MoveCmd | AttackCmd | AttackBuildingCmd | AttackMoveCmd | PatrolCmd
  | StopCmd | GatherCmd | GarrisonCmd | UngarrisonCmd
  | TrainCmd | CancelTrainCmd | ResearchCmd | PlaceBuildingCmd
  | MarketBuyCmd | MarketSellCmd;

export type CommandKind = Command['kind'];
