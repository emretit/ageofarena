/**
 * Versions.ts — single source of truth for the game version triple.
 * Import from both web and server.
 *
 * [schema, major, minor]
 * - schema: incremented when replay/protocol format changes (breaks back-compat)
 * - major:  incremented for balance/gameplay changes (invalidates golden replays)
 * - minor:  incremented for bug fixes / content additions
 *
 * Network protocol validation: [0] and [1] must match on both endpoints.
 * Replay validation: all three must match.
 */
export const GAME_VERSION: [number, number, number] = [1, 0, 0];

export function versionString(): string {
  return GAME_VERSION.join('.');
}

/** Check if two version triples are compatible for network play (schema+major match). */
export function versionsCompatible(a: [number, number, number], b: [number, number, number]): boolean {
  return a[0] === b[0] && a[1] === b[1];
}
