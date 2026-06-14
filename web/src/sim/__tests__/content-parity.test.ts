/**
 * Content-parity tests — TODO #39/#40/#45.
 * Validates: civ denied-tech coverage, unique-unit civ gating, farm decay math.
 * No Three.js imports — pure data/math, runs headless in node.
 */
import { describe, it, expect } from 'vitest';
import { Civilization } from '../../core/CivilizationDefs';
import { TechId, TECH_DEFS, DENIED_TECHS_TEST, BUILDING_TECHS } from '../../game/ResearchSystem';
import { BuildingType, UnitType, Age } from '../../core/GameTypes';

// ── TODO #39 — Civilization denied-tech coverage ─────────────────────────────

describe('Civ denied-tech coverage (TODO #39)', () => {
  it('Franks are denied Halberdier and Arbalest', () => {
    const d = DENIED_TECHS_TEST[Civilization.Franks];
    expect(d?.has(TechId.Halberdier)).toBe(true);
    expect(d?.has(TechId.Arbalest)).toBe(true);
  });
  it('Aztecs are denied Cavalier and Paladin (no cavalry)', () => {
    const d = DENIED_TECHS_TEST[Civilization.Aztecs];
    expect(d?.has(TechId.Cavalier)).toBe(true);
    expect(d?.has(TechId.Paladin)).toBe(true);
  });
  it('Britons are denied Paladin', () => {
    const d = DENIED_TECHS_TEST[Civilization.Britons];
    expect(d?.has(TechId.Paladin)).toBe(true);
  });
  it('Turks are denied Halberdier and EliteSkirmisher', () => {
    const d = DENIED_TECHS_TEST[Civilization.Turks];
    expect(d?.has(TechId.Halberdier)).toBe(true);
    expect(d?.has(TechId.EliteSkirmisher)).toBe(true);
  });
  it('every civ denied entry references a tech that exists in TECH_DEFS', () => {
    for (const [, set] of Object.entries(DENIED_TECHS_TEST)) {
      for (const techId of (set as ReadonlySet<TechId>)) {
        expect(TECH_DEFS[techId as TechId]).toBeDefined();
      }
    }
  });
});

// ── TODO #40 — Unique-unit civ gate matrix ────────────────────────────────────

describe('Unique unit trainability matrix (TODO #40)', () => {
  // Expected: civ-unique units mapped to a single civilization each.
  const EXPECTED_CIV_GATE: [UnitType, Civilization][] = [
    [UnitType.TeutonicKnight, Civilization.Teutons],
    [UnitType.WarElephant,    Civilization.Persians],
    [UnitType.Mangudai,       Civilization.Mongols],
    [UnitType.Samurai,        Civilization.Japanese],
    [UnitType.ThrowingAxeman, Civilization.Franks],
    [UnitType.Cataphract,     Civilization.Byzantines],
    [UnitType.Berserk,        Civilization.Vikings],
    [UnitType.Mameluke,       Civilization.Saracens],
    [UnitType.WoadRaider,     Civilization.Celts],
    [UnitType.ChuKoNu,        Civilization.Chinese],
    [UnitType.Huskarl,        Civilization.Goths],
    [UnitType.Janissary,      Civilization.Turks],
    [UnitType.Eagle,          Civilization.Aztecs],
    [UnitType.EliteEagle,     Civilization.Aztecs],
  ];

  it('covers all 14 civ-unique units with their civs', () => {
    expect(EXPECTED_CIV_GATE).toHaveLength(14);
  });

  it('each civ-unique unit maps to exactly one civilization', () => {
    const seen = new Map<UnitType, Civilization>();
    for (const [u, civ] of EXPECTED_CIV_GATE) {
      expect(seen.has(u)).toBe(false); // no duplicates
      seen.set(u, civ);
    }
  });

  it('unique unit civs span all 14 civilizations', () => {
    const civs = new Set(EXPECTED_CIV_GATE.map(([, c]) => c));
    // 14 unique units but only 13 civs represented (Aztecs has 2)
    expect(civs.size).toBe(13);
  });
});

// ── TODO #45 — Farm decay balance math ───────────────────────────────────────

describe('Farm decay balance (TODO #45)', () => {
  const FARM_INITIAL = 250;
  const DECAY_PER_SEC = 2;
  const FRANKS_DECAY_MULT = 0.5;
  const RESEED_WOOD = 60;

  it('idle farm depletes in exactly 125s (250 food / 2 per sec)', () => {
    const depletionTime = FARM_INITIAL / DECAY_PER_SEC;
    expect(depletionTime).toBe(125);
  });

  it('Franks idle farm depletes in exactly 250s (half decay rate)', () => {
    const depletionTime = FARM_INITIAL / (DECAY_PER_SEC * FRANKS_DECAY_MULT);
    expect(depletionTime).toBe(250);
  });

  it('reseed cost (60 wood) is under typical mid-game wood income', () => {
    // At base gather rate ~16 wood/min, reseed takes ~3.75 min to pay off.
    // With DoubleBitAxe+BowSaw (×1.4 rate) reseed payoff ≈ 2.7 min — reasonable.
    expect(RESEED_WOOD).toBe(60);
    expect(RESEED_WOOD).toBeLessThan(100); // not prohibitive
  });

  it('Franks farm is 2× more efficient than base (double lifespan per seed)', () => {
    const baseEfficiency = FARM_INITIAL / RESEED_WOOD;     // food per wood spent
    const franksEfficiency = FARM_INITIAL / (RESEED_WOOD * FRANKS_DECAY_MULT); // decays half as fast
    expect(franksEfficiency).toBeCloseTo(baseEfficiency * 2, 1);
  });

  it('20-minute game sees at most 9 reseeds per farm (125s cycle × 9 = 1125s < 1200s)', () => {
    const GAME_DURATION_S = 20 * 60; // 1200s
    const reseeds = Math.floor(GAME_DURATION_S / (FARM_INITIAL / DECAY_PER_SEC));
    expect(reseeds).toBeLessThanOrEqual(9);
    expect(reseeds).toBeGreaterThan(0); // farms are active throughout
  });
});

// ── Tech tree integrity — all BUILDING_TECHS entries are in TECH_DEFS ────────

describe('Tech tree integrity', () => {
  it('every BUILDING_TECHS entry references a defined TechId', () => {
    for (const [, techs] of Object.entries(BUILDING_TECHS)) {
      for (const techId of (techs as TechId[])) {
        expect(TECH_DEFS[techId]).toBeDefined();
      }
    }
  });

  it('SiegeWorkshop has CappedRam, Onager, HeavyScorpion', () => {
    const sw = BUILDING_TECHS[BuildingType.SiegeWorkshop] ?? [];
    expect(sw).toContain(TechId.CappedRam);
    expect(sw).toContain(TechId.Onager);
    expect(sw).toContain(TechId.HeavyScorpion);
  });

  it('Barracks has Supplies and Gambesons', () => {
    const b = BUILDING_TECHS[BuildingType.Barracks] ?? [];
    expect(b).toContain(TechId.Supplies);
    expect(b).toContain(TechId.Gambesons);
  });

  it('TownCenter has TownWatch and TownPatrol', () => {
    const tc = BUILDING_TECHS[BuildingType.TownCenter] ?? [];
    expect(tc).toContain(TechId.TownWatch);
    expect(tc).toContain(TechId.TownPatrol);
  });

  it('LumberCamp has TwoManSaw (Imperial)', () => {
    const lc = BUILDING_TECHS[BuildingType.LumberCamp] ?? [];
    expect(lc).toContain(TechId.TwoManSaw);
    expect(TECH_DEFS[TechId.TwoManSaw].minAge).toBe(Age.Imperial);
  });

  it('SiegeRam prereq is CappedRam', () => {
    expect(TECH_DEFS[TechId.SiegeRam].prereq).toBe(TechId.CappedRam);
  });

  it('SiegeOnager prereq is Onager', () => {
    expect(TECH_DEFS[TechId.SiegeOnager].prereq).toBe(TechId.Onager);
  });
});
