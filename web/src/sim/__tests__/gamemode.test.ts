/**
 * GameMode tests — Relic win condition, SuddenDeath, Wonder.
 * Pure logic, no Three.js imports needed (Building/Unit used as type-only references).
 */
import { describe, it, expect, beforeEach } from 'vitest';
import { GameMode } from '../../game/GameMode';
import { BuildingType, UnitType } from '../../core/GameTypes';
import type { Building } from '../../game/Building';
import type { Unit } from '../../game/Unit';

function mockBuilding(teamId: number, type: BuildingType, alive = true): Building {
  return { alive, teamId, buildingType: type, hp: 100, maxHp: 100, pos: { x: 0, z: 0 }, def: {}, sightBonus: 0, id: Math.random() } as unknown as Building;
}

function mockUnit(teamId: number, type: UnitType, alive = true): Unit {
  return { alive, teamId, unitType: type, x: 0, z: 0 } as unknown as Unit;
}

// ── Relic mode ────────────────────────────────────────────────────────────────

describe('GameMode.Relic', () => {
  let gm: GameMode;
  beforeEach(() => { gm = new GameMode('Relic'); });

  it('returns NO_WINNER when no relics exist', () => {
    const result = gm.tick([], [], [0, 1], 1, new Map());
    expect(result.winner).toBe(-1);
  });

  it('returns NO_WINNER while timer counting down but not expired', () => {
    const relics = new Map([[0, 2]]); // team 0 holds 2 relics
    // tick enough to start timer but not expire (relicDuration = 200s)
    gm.tick([], [], [0, 1], 1, relics); // starts timer at 200
    const result = gm.tick([], [], [0, 1], 50, relics); // 50s off timer
    expect(result.winner).toBe(-1);
  });

  it('declares winner when timer expires after holding all relics', () => {
    const relics = new Map([[0, 1]]);
    // Tick once to initialize hold state (sets timer = relicDuration = 200)
    gm.tick([], [], [0, 1], 1, relics);
    // Tick 199s more to expire the timer (total: 200s)
    const result = gm.tick([], [], [0, 1], 200, relics);
    expect(result.winner).toBe(0);
    expect(result.reason).toBe('Relic');
  });

  it('resets timer when holding team loses all relics', () => {
    const team0Relics = new Map([[0, 1]]);
    gm.tick([], [], [0, 1], 1, team0Relics);
    // Team 0 loses the relic
    const noRelics = new Map<number, number>();
    gm.tick([], [], [0, 1], 10, noRelics);
    // Timer should reset — ticking more should not trigger win
    const result = gm.tick([], [], [0, 1], 250, noRelics);
    expect(result.winner).toBe(-1);
  });

  it('does NOT reset timer mid-loop for the holding team (bug fix verification)', () => {
    // Regression: old code reset _relicHoldTeam inside the loop for non-holding teams,
    // cancelling the timer before reaching the holding team.
    const relics = new Map([[0, 2], [1, 0]]); // team 0 has 2, team 1 has 0
    gm.tick([], [], [0, 1], 1, relics); // start timer
    const result = gm.tick([], [], [0, 1], 200, relics);
    expect(result.winner).toBe(0);
  });
});

// ── SuddenDeath mode ──────────────────────────────────────────────────────────

describe('GameMode.SuddenDeath', () => {
  let gm: GameMode;
  beforeEach(() => { gm = new GameMode('SuddenDeath'); });

  it('returns NO_WINNER before any TC is seen', () => {
    const result = gm.tick([], [], [0, 1], 1);
    expect(result.winner).toBe(-1);
  });

  it('returns NO_WINNER while both teams have TCs', () => {
    const buildings = [
      mockBuilding(0, BuildingType.TownCenter),
      mockBuilding(1, BuildingType.TownCenter),
    ];
    const result = gm.tick(buildings, [], [0, 1], 1);
    expect(result.winner).toBe(-1);
  });

  it('declares winner when enemy TC is destroyed', () => {
    const buildings = [
      mockBuilding(0, BuildingType.TownCenter),
      mockBuilding(1, BuildingType.TownCenter),
    ];
    // First tick to register both TCs
    gm.tick(buildings, [], [0, 1], 1);
    // Now team 1 TC is destroyed
    (buildings[1] as { alive: boolean }).alive = false;
    const result = gm.tick(buildings, [], [0, 1], 1);
    expect(result.winner).toBe(0);
    expect(result.reason).toBe('SuddenDeath');
  });
});

// ── Wonder mode ───────────────────────────────────────────────────────────────

describe('GameMode.Wonder', () => {
  let gm: GameMode;
  beforeEach(() => { gm = new GameMode('Wonder'); });

  it('returns NO_WINNER with no Wonder', () => {
    const result = gm.tick([], [], [0, 1], 1);
    expect(result.winner).toBe(-1);
  });

  it('starts timer when Wonder is built and declares winner after 300s', () => {
    const wonder = mockBuilding(0, BuildingType.Wonder);
    const buildings = [wonder];
    gm.tick(buildings, [], [0, 1], 1); // timer starts at 300
    const result = gm.tick(buildings, [], [0, 1], 300);
    expect(result.winner).toBe(0);
    expect(result.reason).toBe('Wonder');
  });

  it('resets timer if Wonder is destroyed before countdown expires', () => {
    const wonder = mockBuilding(0, BuildingType.Wonder);
    const buildings = [wonder];
    gm.tick(buildings, [], [0, 1], 1);
    (wonder as { alive: boolean }).alive = false; // Wonder destroyed
    gm.tick(buildings, [], [0, 1], 200);
    // Wonder rebuilt
    const wonder2 = mockBuilding(0, BuildingType.Wonder);
    buildings.push(wonder2);
    gm.tick(buildings, [], [0, 1], 1);
    const result = gm.tick(buildings, [], [0, 1], 300);
    expect(result.winner).toBe(0);
  });
});
