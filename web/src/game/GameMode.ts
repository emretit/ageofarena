/**
 * GameMode.ts — Conquest / Wonder / Relic / Regicide game modes.
 * Port of GameMode.cs. VictorySystem calls checkWin() each tick.
 */
import type { Building } from "./Building";
import type { Unit } from "./Unit";
import { BuildingType, UnitType } from "../core/GameTypes";

export type GameModeType =
  | 'Conquest' | 'Wonder' | 'Relic' | 'Regicide'
  | 'Deathmatch' | 'Nomad' | 'EmpireWars' | 'KingOfTheHill' | 'SuddenDeath' | 'Treaty' | 'Turbo';

export interface GameModeResult {
  winner: number; // teamId of winner, -1 = no winner yet
  reason: string;
}

const NO_WINNER: GameModeResult = { winner: -1, reason: "" };

export class GameMode {
  readonly type: GameModeType;

  // Wonder: 300s countdown after a Wonder is built
  private _wonderTeam     = -1;
  private _wonderTimer    = 0;
  readonly wonderDuration = 300;

  // Relic: 200s after holding all relics
  private _relicHoldTeam  = -1;
  private _relicTimer     = 0;
  readonly relicDuration  = 200;

  /** Called when Wonder/Relic timer starts — pass team and duration. */
  onTimerStart: ((team: number, duration: number, mode: GameModeType) => void) | null = null;

  constructor(type: GameModeType = 'Conquest') {
    this.type = type;
  }

  /**
   * Tick per fixed step. Returns winner info or NO_WINNER.
   * allTeams: array of all team IDs in the game.
   */
  tick(
    buildings: Building[],
    units: Unit[],
    allTeams: number[],
    dt: number,
    garrisonedRelics?: Map<number, number>, // teamId → relic count (from RelicSystem)
  ): GameModeResult {
    switch (this.type) {
      case 'Conquest':      return this._tickConquest(buildings, allTeams);
      case 'Wonder':        return this._tickWonder(buildings, allTeams, dt);
      case 'Relic':         return this._tickRelic(allTeams, dt, garrisonedRelics ?? new Map());
      case 'Regicide':      return this._tickRegicide(units, buildings, allTeams);
      case 'KingOfTheHill': return this._tickKingOfTheHill(units, allTeams, dt);
      case 'SuddenDeath':   return this._tickSuddenDeath(buildings, allTeams);
      case 'Deathmatch':
      case 'Nomad':
      case 'EmpireWars':
      case 'Treaty':
      case 'Turbo':         return NO_WINNER; // VictorySystem (Conquest rules) handles these
    }
  }

  // ── Conquest: last team with alive TC wins (handled by VictorySystem; always NO_WINNER here)
  private _tickConquest(_buildings: Building[], _allTeams: number[]): GameModeResult {
    return NO_WINNER; // VictorySystem handles TC elimination
  }

  // ── Wonder: first team to survive 300s after finishing a Wonder
  private _tickWonder(buildings: Building[], allTeams: number[], dt: number): GameModeResult {
    // Check if a new Wonder completed
    for (const b of buildings) {
      if (!b.alive || b.buildingType !== BuildingType.Wonder) continue;
      if (this._wonderTeam === -1 || this._wonderTeam === b.teamId) {
        if (this._wonderTeam === -1) {
          this._wonderTeam  = b.teamId;
          this._wonderTimer = this.wonderDuration;
          this.onTimerStart?.(b.teamId, this.wonderDuration, 'Wonder');
        }
      }
    }

    if (this._wonderTeam < 0) return NO_WINNER;

    // Check Wonder still alive for current team
    const wonderAlive = buildings.some(b => b.alive && b.buildingType === BuildingType.Wonder && b.teamId === this._wonderTeam);
    if (!wonderAlive) {
      // Reset — Wonder destroyed
      this._wonderTeam  = -1;
      this._wonderTimer = 0;
      return NO_WINNER;
    }

    this._wonderTimer -= dt;
    if (this._wonderTimer <= 0) {
      return { winner: this._wonderTeam, reason: "Wonder" };
    }
    return NO_WINNER;
  }

  // ── Relic: hold all relics in Monasteries for 200s
  private _tickRelic(allTeams: number[], dt: number, relicCounts: Map<number, number>): GameModeResult {
    const totalRelics = [...relicCounts.values()].reduce((a, b) => a + b, 0);
    if (totalRelics === 0) return NO_WINNER;

    // Find the single team holding ALL relics (read-only scan, no state mutation inside loop)
    let holdingTeam = -1;
    for (const [team, count] of relicCounts) {
      if (count === totalRelics) { holdingTeam = team; break; }
    }

    // State transition: switch holding team or clear if nobody holds all
    if (holdingTeam !== this._relicHoldTeam) {
      this._relicHoldTeam = holdingTeam;
      if (holdingTeam >= 0) {
        this._relicTimer = this.relicDuration;
        this.onTimerStart?.(holdingTeam, this.relicDuration, 'Relic');
      } else {
        this._relicTimer = 0;
      }
    }

    if (this._relicHoldTeam < 0) return NO_WINNER;

    this._relicTimer -= dt;
    if (this._relicTimer <= 0) {
      return { winner: this._relicHoldTeam, reason: "Relic" };
    }
    return NO_WINNER;
  }

  // ── Regicide: a team whose King unit dies is eliminated. Last team with a living King wins.
  private _tickRegicide(units: Unit[], _buildings: Building[], allTeams: number[]): GameModeResult {
    // Track which teams have ever fielded a King so we don't declare a winner before
    // kings are spawned (all-absent at frame 0 must NOT instantly end the match).
    for (const u of units) {
      if (u.unitType === UnitType.King) this._kingTeams.add(u.teamId);
    }
    if (this._kingTeams.size === 0) return NO_WINNER; // kings not spawned yet

    const teamsWithKing = new Set<number>();
    for (const u of units) {
      if (u.alive && u.unitType === UnitType.King) teamsWithKing.add(u.teamId);
    }
    // Only consider teams that started with a king.
    const surviving = allTeams.filter(t => this._kingTeams.has(t) && teamsWithKing.has(t));
    if (surviving.length === 1) return { winner: surviving[0], reason: "Regicide" };
    if (surviving.length === 0) return { winner: allTeams[0] ?? -1, reason: "Regicide" };
    return NO_WINNER;
  }

  /** Teams that have spawned a King at least once (guards the frame-0 all-absent case). */
  private readonly _kingTeams = new Set<number>();

  // ── KingOfTheHill ────────────────────────────────────────────────────────────
  private _kothHoldTeam = -1;
  private _kothTimer    = 0;
  readonly kothDuration = 200;
  private readonly KOTH_RADIUS = 15;

  private _tickKingOfTheHill(units: Unit[], allTeams: number[], dt: number): GameModeResult {
    const counts = new Map<number, number>();
    for (const u of units) {
      if (!u.alive) continue;
      const dx = u.x; const dz = u.z; // center of map is (0, 0)
      if (dx * dx + dz * dz <= this.KOTH_RADIUS * this.KOTH_RADIUS) {
        counts.set(u.teamId, (counts.get(u.teamId) ?? 0) + 1);
      }
    }
    let dominant = -1;
    let maxCount = 0;
    let contested = false;
    for (const [team, cnt] of counts) {
      if (cnt > maxCount) { dominant = team; maxCount = cnt; contested = false; }
      else if (cnt === maxCount) { contested = true; }
    }
    if (dominant < 0 || contested || maxCount === 0) {
      this._kothHoldTeam = -1;
      this._kothTimer    = 0;
      return NO_WINNER;
    }
    if (this._kothHoldTeam !== dominant) {
      this._kothHoldTeam = dominant;
      this._kothTimer    = this.kothDuration;
      this.onTimerStart?.(dominant, this.kothDuration, 'KingOfTheHill');
    }
    this._kothTimer -= dt;
    if (this._kothTimer <= 0) return { winner: this._kothHoldTeam, reason: "KingOfTheHill" };
    return NO_WINNER;
  }

  // ── SuddenDeath ──────────────────────────────────────────────────────────────
  private readonly _sdTeams = new Set<number>(); // teams that have ever had a TC

  private _tickSuddenDeath(buildings: Building[], allTeams: number[]): GameModeResult {
    for (const b of buildings) {
      if (b.buildingType === BuildingType.TownCenter) this._sdTeams.add(b.teamId);
    }
    if (this._sdTeams.size === 0) return NO_WINNER;
    const teamsWithTC = new Set<number>();
    for (const b of buildings) {
      if (b.alive && b.buildingType === BuildingType.TownCenter) teamsWithTC.add(b.teamId);
    }
    const surviving = allTeams.filter(t => this._sdTeams.has(t) && teamsWithTC.has(t));
    if (surviving.length === 1) return { winner: surviving[0], reason: "SuddenDeath" };
    if (surviving.length === 0) return { winner: allTeams[0] ?? -1, reason: "SuddenDeath" };
    return NO_WINNER;
  }

  /** Wonder/Relic/KoTH countdown remaining (0 if not active). */
  get timerRemaining(): number {
    if (this.type === 'Wonder')        return Math.max(0, this._wonderTimer);
    if (this.type === 'Relic')         return Math.max(0, this._relicTimer);
    if (this.type === 'KingOfTheHill') return Math.max(0, this._kothTimer);
    return 0;
  }

  get timerActive(): boolean {
    return (this.type === 'Wonder'        && this._wonderTeam >= 0) ||
           (this.type === 'Relic'         && this._relicHoldTeam >= 0) ||
           (this.type === 'KingOfTheHill' && this._kothHoldTeam >= 0);
  }

  get timerTeam(): number {
    if (this.type === 'Wonder')        return this._wonderTeam;
    if (this.type === 'Relic')         return this._relicHoldTeam;
    if (this.type === 'KingOfTheHill') return this._kothHoldTeam;
    return -1;
  }
}
