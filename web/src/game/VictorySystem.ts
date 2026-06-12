/**
 * VictorySystem — team elimination + allied shared victory.
 * Port of VictorySystem.cs.
 * A team is eliminated when all their Town Centers are destroyed.
 * Last surviving team (or alliance group) wins.
 */
import { BuildingType } from '../core/GameTypes';
import { diplomacy } from '../core/Diplomacy';
import type { Building } from './Building';

export class VictorySystem {
  /** Called when a team wins — pass winning teamId. */
  onVictory: ((winnerTeam: number) => void) | null = null;

  private _eliminated = new Set<number>();

  /** Check for newly eliminated teams and determine winner. Returns winning teamId or -1. */
  tick(buildings: Building[], allTeamIds: number[]): number {
    // Find teams that still have at least one TC alive
    const teamsWithTC = new Set<number>();
    for (const b of buildings) {
      if (b.alive && b.buildingType === BuildingType.TownCenter) {
        teamsWithTC.add(b.teamId);
      }
    }

    // Mark newly eliminated teams
    for (const t of allTeamIds) {
      if (!teamsWithTC.has(t) && !this._eliminated.has(t)) {
        this._eliminated.add(t);
      }
    }

    // Determine surviving teams
    const surviving = allTeamIds.filter(t => !this._eliminated.has(t));
    if (surviving.length === 0) return -1;
    if (surviving.length === 1) {
      this.onVictory?.(surviving[0]);
      return surviving[0];
    }

    // Check if all survivors are allies (shared victory)
    const [first, ...rest] = surviving;
    if (rest.every(t => diplomacy.isAlly(first, t))) {
      this.onVictory?.(first); // report primary team's victory
      return first;
    }

    return -1; // game continues
  }

  isEliminated(team: number): boolean {
    return this._eliminated.has(team);
  }
}
