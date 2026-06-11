/**
 * CivState — module-level per-team civilization selection.
 * Systems call getTeamBonus(teamId) to read multipliers without prop-drilling.
 */
import { Civilization, CIVILIZATION_DEFS, CivBonus } from './CivilizationDefs';

const _civs: Civilization[] = [Civilization.None, Civilization.None];

export function setTeamCiv(teamId: number, civ: Civilization): void {
  _civs[teamId] = civ;
}

export function getTeamCiv(teamId: number): Civilization {
  return _civs[teamId] ?? Civilization.None;
}

export function getTeamBonus(teamId: number): CivBonus {
  return CIVILIZATION_DEFS[_civs[teamId] ?? Civilization.None];
}
