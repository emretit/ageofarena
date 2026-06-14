/**
 * Tech bonus tests — Gambeson, Supplies, TwoManSaw, Capped Ram, HeavyScorpion, etc.
 * Pure data layer — no Three.js.
 */
import { describe, it, expect } from 'vitest';
import { TechId, TECH_DEFS, BUILDING_TECHS } from '../../game/ResearchSystem';
import { BuildingType, Age } from '../../core/GameTypes';

// ── Tech metadata existence ───────────────────────────────────────────────────

describe('New tech definitions exist', () => {
  const newTechs = [
    TechId.Gambesons, TechId.Supplies, TechId.TwoManSaw,
    TechId.TownWatch, TechId.TownPatrol,
    TechId.CappedRam, TechId.SiegeRam,
    TechId.Onager, TechId.SiegeOnager,
    TechId.HeavyScorpion,
    TechId.Redemption,
  ];
  for (const t of newTechs) {
    it(`TECH_DEFS has entry for ${t}`, () => {
      expect(TECH_DEFS[t]).toBeDefined();
      expect(TECH_DEFS[t].label.length).toBeGreaterThan(0);
    });
  }
});

// ── Tech host building assignments ────────────────────────────────────────────

describe('New techs assigned to correct buildings', () => {
  it('Gambeson is at Barracks', () => {
    expect(BUILDING_TECHS[BuildingType.Barracks]).toContain(TechId.Gambesons);
  });
  it('Supplies is at Barracks', () => {
    expect(BUILDING_TECHS[BuildingType.Barracks]).toContain(TechId.Supplies);
  });
  it('TwoManSaw is at LumberCamp', () => {
    expect(BUILDING_TECHS[BuildingType.LumberCamp]).toContain(TechId.TwoManSaw);
  });
  it('TownWatch is at TownCenter', () => {
    expect(BUILDING_TECHS[BuildingType.TownCenter]).toContain(TechId.TownWatch);
  });
  it('TownPatrol is at TownCenter', () => {
    expect(BUILDING_TECHS[BuildingType.TownCenter]).toContain(TechId.TownPatrol);
  });
  it('CappedRam and SiegeRam are at SiegeWorkshop', () => {
    expect(BUILDING_TECHS[BuildingType.SiegeWorkshop]).toContain(TechId.CappedRam);
    expect(BUILDING_TECHS[BuildingType.SiegeWorkshop]).toContain(TechId.SiegeRam);
  });
  it('Onager and SiegeOnager are at SiegeWorkshop', () => {
    expect(BUILDING_TECHS[BuildingType.SiegeWorkshop]).toContain(TechId.Onager);
    expect(BUILDING_TECHS[BuildingType.SiegeWorkshop]).toContain(TechId.SiegeOnager);
  });
  it('HeavyScorpion is at SiegeWorkshop', () => {
    expect(BUILDING_TECHS[BuildingType.SiegeWorkshop]).toContain(TechId.HeavyScorpion);
  });
  it('Redemption is at Monastery', () => {
    expect(BUILDING_TECHS[BuildingType.Monastery]).toContain(TechId.Redemption);
  });
});

// ── Tech age gates ─────────────────────────────────────────────────────────────

describe('Tech age gates are correct', () => {
  it('TownWatch requires Feudal Age (1)', () => {
    expect(TECH_DEFS[TechId.TownWatch].minAge).toBe(Age.Feudal);
  });
  it('TownPatrol requires Castle Age', () => {
    expect(TECH_DEFS[TechId.TownPatrol].minAge).toBe(Age.Castle);
  });
  it('CappedRam requires Castle Age', () => {
    expect(TECH_DEFS[TechId.CappedRam].minAge).toBe(Age.Castle);
  });
  it('SiegeRam requires Imperial Age', () => {
    expect(TECH_DEFS[TechId.SiegeRam].minAge).toBe(Age.Imperial);
  });
  it('HeavyScorpion requires Imperial Age', () => {
    expect(TECH_DEFS[TechId.HeavyScorpion].minAge).toBe(Age.Imperial);
  });
  it('Redemption requires Castle Age', () => {
    expect(TECH_DEFS[TechId.Redemption].minAge).toBe(Age.Castle);
  });
});

// ── TownPatrol prereq chain ───────────────────────────────────────────────────

describe('Tech prerequisite chains', () => {
  it('TownPatrol requires TownWatch as prereq', () => {
    expect(TECH_DEFS[TechId.TownPatrol].prereq).toBe(TechId.TownWatch);
  });
  it('SiegeRam requires CappedRam as prereq', () => {
    expect(TECH_DEFS[TechId.SiegeRam].prereq).toBe(TechId.CappedRam);
  });
  it('SiegeOnager requires Onager as prereq', () => {
    expect(TECH_DEFS[TechId.SiegeOnager].prereq).toBe(TechId.Onager);
  });
});
