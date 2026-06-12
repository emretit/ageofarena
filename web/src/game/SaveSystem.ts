/**
 * SaveSystem.ts — snapshot-based save/load to localStorage.
 * Schema v1: settings header + serialized game state.
 * Port of SaveSystem.cs snapshot approach (command-log save is Faz 15).
 */
import type { ResourceManager } from "../core/ResourceManager";
import { Age } from "../core/GameTypes";
// Age is available via ResourceManager.age

export const SAVE_SCHEMA_VERSION = 1;
const SAVE_KEY_PREFIX = "aoa_save_";
const SAVE_SLOTS = 3;

export interface SavedResource {
  food: number; wood: number; gold: number; stone: number;
  pop: number; popCap: number; age: Age;
}

export interface SavedUnit {
  teamId: number; unitType: number;
  x: number; z: number;
  hp: number; maxHp: number;
}

export interface SavedBuilding {
  teamId: number; buildingType: number;
  x: number; z: number;
  hp: number; maxHp: number;
}

export interface SaveSnapshot {
  schemaVersion: number;
  timestamp: number;
  elapsed: number;          // seconds since game start
  teamResources: SavedResource[];
  units: SavedUnit[];
  buildings: SavedBuilding[];
}

export function buildSnapshot(
  elapsed: number,
  teamRes: ResourceManager[],
  units: Array<{ teamId: number; unitType: number; x: number; z: number; hp: number; maxHp: number; alive: boolean }>,
  buildings: Array<{ teamId: number; buildingType: number; pos: { x: number; z: number }; hp: number; maxHp: number; alive: boolean }>,
): SaveSnapshot {
  return {
    schemaVersion: SAVE_SCHEMA_VERSION,
    timestamp: Date.now(),
    elapsed,
    teamResources: teamRes.map(rm => ({
      food: rm.food, wood: rm.wood, gold: rm.gold, stone: rm.stone,
      pop: rm.pop,   popCap: rm.popCap,
      age: rm.age,
    })),
    units: units
      .filter(u => u.alive)
      .map(u => ({ teamId: u.teamId, unitType: u.unitType, x: u.x, z: u.z, hp: u.hp, maxHp: u.maxHp })),
    buildings: buildings
      .filter(b => b.alive)
      .map(b => ({ teamId: b.teamId, buildingType: b.buildingType, x: b.pos.x, z: b.pos.z, hp: b.hp, maxHp: b.maxHp })),
  };
}

export function saveToSlot(slot: number, snap: SaveSnapshot): void {
  const key = SAVE_KEY_PREFIX + slot;
  try {
    localStorage.setItem(key, JSON.stringify(snap));
  } catch {
    console.warn("[SaveSystem] localStorage write failed (quota?)");
  }
}

export function loadFromSlot(slot: number): SaveSnapshot | null {
  const raw = localStorage.getItem(SAVE_KEY_PREFIX + slot);
  if (!raw) return null;
  try {
    const snap = JSON.parse(raw) as SaveSnapshot;
    if (snap.schemaVersion !== SAVE_SCHEMA_VERSION) return null; // schema changed → incompatible
    return snap;
  } catch {
    return null;
  }
}

export function listSlots(): Array<{ slot: number; timestamp: number; elapsed: number } | null> {
  return Array.from({ length: SAVE_SLOTS }, (_, i) => {
    const snap = loadFromSlot(i + 1);
    if (!snap) return null;
    return { slot: i + 1, timestamp: snap.timestamp, elapsed: snap.elapsed };
  });
}

export function deleteSlot(slot: number): void {
  localStorage.removeItem(SAVE_KEY_PREFIX + slot);
}
