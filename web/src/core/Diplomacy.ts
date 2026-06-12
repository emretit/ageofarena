/**
 * Diplomacy — Enemy/Neutral/Allied relationship matrix.
 * Port of DiplomacySystem.cs.
 * All combat/AI/conversion/garrison reads isEnemy/isAlly instead of teamId checks.
 */

export type DiplomaticStance = 'enemy' | 'neutral' | 'ally';

export class Diplomacy {
  private readonly _matrix = new Map<string, DiplomaticStance>();

  private _key(a: number, b: number): string {
    return a < b ? `${a}:${b}` : `${b}:${a}`;
  }

  setStance(teamA: number, teamB: number, stance: DiplomaticStance): void {
    if (teamA === teamB) return;
    this._matrix.set(this._key(teamA, teamB), stance);
  }

  getStance(teamA: number, teamB: number): DiplomaticStance {
    if (teamA === teamB) return 'ally';
    return this._matrix.get(this._key(teamA, teamB)) ?? 'enemy';
  }

  isEnemy(teamA: number, teamB: number): boolean {
    return this.getStance(teamA, teamB) === 'enemy';
  }

  isAlly(teamA: number, teamB: number): boolean {
    return this.getStance(teamA, teamB) === 'ally';
  }

  alliesOf(team: number, allTeams: number[]): number[] {
    return allTeams.filter(t => t !== team && this.isAlly(team, t));
  }
}

/** Singleton for the current game session — set by main.ts on game start. */
export let diplomacy = new Diplomacy();

export function resetDiplomacy(): void {
  diplomacy = new Diplomacy();
}
