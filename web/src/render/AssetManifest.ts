/**
 * AssetManifest.ts — Maps game entity types to GLTF model paths + metadata.
 * If a model is unavailable, AssetLoader falls back to procedural geometry.
 */
import { UnitType, BuildingType } from "../core/GameTypes";

export interface UnitModelDef {
  file:    string;   // path relative to public/assets/models/units/
  scale:   number;   // desired world HEIGHT in units (bake normalises model to 1.0 tall)
  yawOffset: number; // extra rotation.y so model faces +Z
  tint?:   number;   // 0..1 team-colour mix strength applied to the material (default 0.4)
}

export interface BuildingModelDef {
  file:  string;   // path relative to public/assets/models/buildings/
  // Scale is NOT stored here — Building.ts normalises each model to its DIMS footprint.
}

/**
 * Unit model manifest — real CC0 GLB/glTF sources placed under public/assets/models/units/.
 * KayKit Adventurers (Knight/Rogue/Rogue_Hooded/Mage/Barbarian) cover infantry/archer/monk;
 * Quaternius animals (Horse/Donkey) cover mounted + trade. Several unit types intentionally
 * share one model (e.g. Militia + Spearman → knight). Siege has no model yet → procedural.
 * `scale` is the target world height; AssetLoader bakes each file to a 1.0-tall static mesh.
 * Missing files resolve to null → procedural fallback (Unit.ts).
 */
export const UNIT_MODELS: Partial<Record<UnitType, UnitModelDef>> = {
  [UnitType.Villager]:   { file: "rogue_hooded.glb", scale: 1.55, yawOffset: Math.PI },
  [UnitType.Militia]:    { file: "knight.glb",        scale: 1.75, yawOffset: Math.PI },
  [UnitType.Spearman]:   { file: "knight.glb",        scale: 1.75, yawOffset: Math.PI },
  [UnitType.Archer]:     { file: "rogue.glb",         scale: 1.65, yawOffset: Math.PI },
  [UnitType.Skirmisher]: { file: "rogue.glb",         scale: 1.65, yawOffset: Math.PI },
  [UnitType.Longbowman]: { file: "rogue_hooded.glb",  scale: 1.65, yawOffset: Math.PI },
  [UnitType.Monk]:       { file: "mage.glb",          scale: 1.72, yawOffset: Math.PI },
  // Mounted + trade — Quaternius animals (8-material, kept un-tinted; rider overlay is a follow-up).
  [UnitType.Cavalry]:    { file: "horse.gltf",        scale: 1.9, yawOffset: 0, tint: 0 },
  [UnitType.Scout]:      { file: "horse_white.gltf",  scale: 1.8, yawOffset: 0, tint: 0 },
  [UnitType.TradeCart]:  { file: "donkey.gltf",       scale: 1.5, yawOffset: 0, tint: 0 },
};

export const BUILDING_MODELS: Partial<Record<BuildingType, BuildingModelDef>> = {
  [BuildingType.TownCenter]:   { file: "town_center.glb" },
  [BuildingType.House]:        { file: "house.glb" },
  [BuildingType.Barracks]:     { file: "barracks.glb" },
  [BuildingType.ArcheryRange]: { file: "archery_range.glb" },
  [BuildingType.Stable]:       { file: "stable.glb" },
  [BuildingType.Farm]:         { file: "farm.glb" },
  [BuildingType.LumberCamp]:   { file: "lumber_camp.glb" },
  [BuildingType.MiningCamp]:   { file: "mining_camp.glb" },
  [BuildingType.Mill]:         { file: "mill.glb" },
  [BuildingType.Market]:       { file: "market.glb" },
  [BuildingType.Castle]:       { file: "castle.glb" },
  [BuildingType.Monastery]:    { file: "monastery.glb" },
  [BuildingType.University]:   { file: "university.glb" },
  [BuildingType.Blacksmith]:   { file: "blacksmith.glb" },
  [BuildingType.SiegeWorkshop]:{ file: "siege_workshop.glb" },
  [BuildingType.Dock]:         { file: "dock.glb" },
  [BuildingType.WatchTower]:   { file: "watch_tower.glb" },
  [BuildingType.Wonder]:       { file: "wonder.glb" },
};
