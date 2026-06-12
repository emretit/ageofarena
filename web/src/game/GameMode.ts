/**
 * GameMode.ts — Conquest / Wonder / Relic / Regicide game modes.
 * Port of GameMode.cs. VictorySystem calls checkWin() each tick.
 */
import type { Building } from "./Building";
import type { Unit } from "./Unit";
import { BuildingType, UnitType } from "../core/GameTypes";

export type GameModeType = 'Conquest' | 'Wonder' | 'Relic' | 'Regicide';

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
      case 'Conquest': return this._tickConquest(buildings, allTeams);
      case 'Wonder':   return this._tickWonder(buildings, allTeams, dt);
      case 'Relic':    return this._tickRelic(allTeams, dt, garrisonedRelics ?? new Map());
      case 'Regicide': return this._tickRegicide(units, buildings, allTeams);
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

  // ── Relic: hold all relics in TCs for 200s
  private _tickRelic(allTeams: number[], dt: number, relicCounts: Map<number, number>): GameModeResult {
    const totalRelics = [...relicCounts.values()].reduce((a, b) => a + b, 0);
    if (totalRelics === 0) return NO_WINNER;

    // Find team holding all relics
    for (const [team, count] of relicCounts) {
      if (count === totalRelics) {
        if (this._relicHoldTeam !== team) {
          this._relicHoldTeam = team;
          this._relicTimer    = this.relicDuration;
          this.onTimerStart?.(team, this.relicDuration, 'Relic');
        }
        break;
      } else {
        this._relicHoldTeam = -1;
        this._relicTimer    = 0;
      }
    }

    if (this._relicHoldTeam < 0) return NO_WINNER;

    this._relicTimer -= dt;
    if (this._relicTimer <= 0) {
      return { winner: this._relicHoldTeam, reason: "Relic" };
    }
    return NO_WINNER;
  }

  // ── Regicide: team whose King unit is killed is eliminated (King = special Scout unit with high HP)
  private _tickRegicide(units: Unit[], _buildings: Building[], allTeams: number[]): GameModeResult {
    // A team is eliminated if their King (is marked by isKing flag, or falls back to: no alive units)
    // Simple approach: each team needs at least one alive King (UnitType.Scout with high HP as proxy)
    // We track via `unit.isKing` field if set, otherwise skip
    const teamsWithKing = new Set<number>();
    for (const u of units) {
      if (u.alive && (u as { isKing?: boolean }).isKing) {
        teamsWithKing.add(u.teamId);
      }
    }
    // If a team has no King entry in set, they're eliminated
    const surviving = allTeams.filter(t => teamsWithKing.has(t));
    if (surviving.length === 1) {
      return { winner: surviving[0], reason: "Regicide" };
    }
    if (surviving.length === 0) {
      return { winner: allTeams[0] ?? -1, reason: "Regicide" }; // edge case
    }
    return NO_WINNER;
  }

  /** Wonder/Relic countdown remaining (0 if not active). */
  get timerRemaining(): number {
    if (this.type === 'Wonder') return Math.max(0, this._wonderTimer);
    if (this.type === 'Relic')  return Math.max(0, this._relicTimer);
    return 0;
  }

  get timerActive(): boolean {
    return (this.type === 'Wonder' && this._wonderTeam >= 0) ||
           (this.type === 'Relic'  && this._relicHoldTeam >= 0);
  }

  get timerTeam(): number {
    if (this.type === 'Wonder') return this._wonderTeam;
    if (this.type === 'Relic')  return this._relicHoldTeam;
    return -1;
  }
}
