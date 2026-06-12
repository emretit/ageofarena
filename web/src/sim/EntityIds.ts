/**
 * EntityIds.ts — monotonic entity ID allocation.
 * All units, buildings, and resource nodes share a single counter.
 * IDs start at 1 (0 = null/invalid).
 */

let _nextId = 1;

export function allocId(): number {
  return _nextId++;
}

/** Reset for new game (call before startGame). */
export function resetIds(): void {
  _nextId = 1;
}

/** Type-safe entity ID (just a number, but semantically distinct). */
export type EntityId = number;
