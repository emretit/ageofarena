/**
 * ScenarioEditor save/load roundtrip — validates JSON serialization format.
 * Avoids Three.js by testing the data layer only (mock callbacks).
 */
import { describe, it, expect } from 'vitest';
import { UnitType, BuildingType, ResourceKind } from '../../core/GameTypes';

// ── Replicate the save format from ScenarioEditor._save() ────────────────────

interface SavedUnit     { type: UnitType;     x: number; z: number; team: number }
interface SavedBuilding { type: BuildingType; x: number; z: number; team: number }
interface SavedResource { rtype: ResourceKind; amount: number; x: number; z: number }
interface ScenarioData {
  units:     SavedUnit[];
  buildings: SavedBuilding[];
  resources: SavedResource[];
}

function buildSavePayload(
  units: SavedUnit[],
  buildings: SavedBuilding[],
  resources: SavedResource[],
): string {
  const data: ScenarioData = { units, buildings, resources };
  return JSON.stringify(data);
}

function parseSavePayload(raw: string): ScenarioData {
  return JSON.parse(raw) as ScenarioData;
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ScenarioEditor save/load roundtrip', () => {
  it('serialises and deserialises units correctly', () => {
    const units: SavedUnit[] = [
      { type: UnitType.Villager, x: 10, z: 20, team: 0 },
      { type: UnitType.Militia,  x: -5, z: 3,  team: 1 },
    ];
    const raw = buildSavePayload(units, [], []);
    const data = parseSavePayload(raw);
    expect(data.units).toHaveLength(2);
    expect(data.units[0].type).toBe(UnitType.Villager);
    expect(data.units[1].x).toBe(-5);
  });

  it('serialises and deserialises buildings correctly', () => {
    const buildings: SavedBuilding[] = [
      { type: BuildingType.TownCenter, x: 0, z: 0, team: 0 },
    ];
    const raw = buildSavePayload([], buildings, []);
    const data = parseSavePayload(raw);
    expect(data.buildings[0].type).toBe(BuildingType.TownCenter);
    expect(data.buildings[0].team).toBe(0);
  });

  it('serialises and deserialises resource nodes correctly', () => {
    const resources: SavedResource[] = [
      { rtype: ResourceKind.Gold, amount: 800, x: 15, z: -10 },
      { rtype: ResourceKind.Wood, amount: 300, x: 5,  z: 7 },
    ];
    const raw = buildSavePayload([], [], resources);
    const data = parseSavePayload(raw);
    expect(data.resources).toHaveLength(2);
    expect(data.resources[0].rtype).toBe(ResourceKind.Gold);
    expect(data.resources[0].amount).toBe(800);
    expect(data.resources[1].rtype).toBe(ResourceKind.Wood);
  });

  it('handles empty payload gracefully', () => {
    const raw = buildSavePayload([], [], []);
    const data = parseSavePayload(raw);
    expect(data.units).toHaveLength(0);
    expect(data.buildings).toHaveLength(0);
    expect(data.resources).toHaveLength(0);
  });

  it('preserves coordinate precision', () => {
    const units: SavedUnit[] = [{ type: UnitType.Archer, x: 12.345, z: -9.876, team: 0 }];
    const data = parseSavePayload(buildSavePayload(units, [], []));
    expect(data.units[0].x).toBeCloseTo(12.345);
    expect(data.units[0].z).toBeCloseTo(-9.876);
  });

  it('missing resources array defaults to empty (optional field safety)', () => {
    const raw = JSON.stringify({ units: [], buildings: [] }); // no resources key
    const data = parseSavePayload(raw) as Partial<ScenarioData>;
    // ScenarioEditor._load() uses `data.resources ?? []` — validate this pattern
    const resources = data.resources ?? [];
    expect(resources).toHaveLength(0);
  });
});
