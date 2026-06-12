/**
 * AssetManifest.ts — Maps game entity types to GLTF model paths + metadata.
 * If a model is unavailable, AssetLoader falls back to procedural geometry.
 */
import { UnitType, BuildingType } from "../core/GameTypes";

export interface UnitModelDef {
  file:    string;   // path relative to public/assets/models/units/
  scale:   number;
  yawOffset: number; // extra rotation.y so model faces +Z
}

export interface BuildingModelDef {
  file:  string;   // path relative to public/assets/models/buildings/
  scale: number;
}

/** Unit model manifest — paths for future KayKit / Quaternius models.
 *  Until the GLB files exist, AssetLoader returns null → procedural fallback. */
export const UNIT_MODELS: Partial<Record<UnitType, UnitModelDef>> = {
  [UnitType.Villager]:   { file: "villager.glb",   scale: 0.9, yawOffset: Math.PI },
  [UnitType.Militia]:    { file: "knight.glb",      scale: 1.0, yawOffset: Math.PI },
  [UnitType.Archer]:     { file: "archer.glb",      scale: 0.95,yawOffset: Math.PI },
  [UnitType.Spearman]:   { file: "spearman.glb",    scale: 1.0, yawOffset: Math.PI },
  [UnitType.Cavalry]:    { file: "cavalry.glb",     scale: 1.2, yawOffset: Math.PI },
  [UnitType.Monk]:       { file: "monk.glb",        scale: 0.9, yawOffset: Math.PI },
  [UnitType.Trebuchet]:  { file: "trebuchet.glb",   scale: 1.8, yawOffset: Math.PI },
  [UnitType.Mangonel]:   { file: "mangonel.glb",    scale: 1.5, yawOffset: Math.PI },
  [UnitType.Scout]:      { file: "scout.glb",       scale: 1.1, yawOffset: Math.PI },
  [UnitType.Skirmisher]: { file: "skirmisher.glb",  scale: 0.95,yawOffset: Math.PI },
  [UnitType.Longbowman]: { file: "longbowman.glb",  scale: 0.95,yawOffset: Math.PI },
  [UnitType.Ram]:        { file: "ram.glb",          scale: 1.6, yawOffset: Math.PI },
  [UnitType.TradeCart]:  { file: "trade_cart.glb",  scale: 1.2, yawOffset: Math.PI },
};

export const BUILDING_MODELS: Partial<Record<BuildingType, BuildingModelDef>> = {
  [BuildingType.TownCenter]:   { file: "town_center.glb",   scale: 1.6 },
  [BuildingType.House]:        { file: "house.glb",          scale: 1.0 },
  [BuildingType.Barracks]:     { file: "barracks.glb",       scale: 1.4 },
  [BuildingType.ArcheryRange]: { file: "archery_range.glb",  scale: 1.4 },
  [BuildingType.Stable]:       { file: "stable.glb",         scale: 1.4 },
  [BuildingType.Farm]:         { file: "farm.glb",           scale: 1.2 },
  [BuildingType.LumberCamp]:   { file: "lumber_camp.glb",    scale: 1.3 },
  [BuildingType.MiningCamp]:   { file: "mining_camp.glb",    scale: 1.3 },
  [BuildingType.Mill]:         { file: "mill.glb",           scale: 1.2 },
  [BuildingType.Market]:       { file: "market.glb",         scale: 1.4 },
  [BuildingType.Castle]:       { file: "castle.glb",         scale: 2.0 },
  [BuildingType.Monastery]:    { file: "monastery.glb",      scale: 1.5 },
  [BuildingType.University]:   { file: "university.glb",     scale: 1.5 },
  [BuildingType.Blacksmith]:   { file: "blacksmith.glb",     scale: 1.3 },
  [BuildingType.SiegeWorkshop]:{ file: "siege_workshop.glb", scale: 1.4 },
  [BuildingType.Dock]:         { file: "dock.glb",           scale: 1.5 },
  [BuildingType.WatchTower]:   { file: "watch_tower.glb",    scale: 1.2 },
  [BuildingType.Wonder]:       { file: "wonder.glb",         scale: 2.5 },
};
