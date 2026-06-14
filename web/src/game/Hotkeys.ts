/**
 * Hotkeys.ts — Rebindable action→key map.
 * Default bindings match AoE2 conventions. Persisted to localStorage.
 */

/** Action IDs used throughout input handlers. */
export type HotkeyAction =
  | 'attackMove' | 'patrol' | 'stop' | 'formation'
  | 'garrison'   | 'ungarrison' | 'idleVillager'
  | 'editor';

const DEFAULTS: Record<HotkeyAction, string> = {
  attackMove:   'a',
  patrol:       'z',
  stop:         's',
  formation:    'f',
  garrison:     'g',
  ungarrison:   'u',
  idleVillager: '.',
  editor:       'e',
};

const LS_PREFIX = "hotkey_";

export const ACTION_LABELS: Record<HotkeyAction, string> = {
  attackMove:   'Saldır-Hareket',
  patrol:       'Devriye',
  stop:         'Dur',
  formation:    'Formasyon',
  garrison:     'Garnizon',
  ungarrison:   'Garnizondan Çık',
  idleVillager: 'Boş Köylü',
  editor:       'Senaryo Editörü',
};

/** Get the current key bound to an action. */
export function getKey(action: HotkeyAction): string {
  return localStorage.getItem(`${LS_PREFIX}${action}`) ?? DEFAULTS[action];
}

/** Bind a key to an action. */
export function setKey(action: HotkeyAction, key: string): void {
  if (!key) {
    localStorage.removeItem(`${LS_PREFIX}${action}`);
  } else {
    localStorage.setItem(`${LS_PREFIX}${action}`, key.toLowerCase());
  }
}

/** Reset all bindings to defaults. */
export function resetHotkeys(): void {
  for (const action of Object.keys(DEFAULTS) as HotkeyAction[]) {
    localStorage.removeItem(`${LS_PREFIX}${action}`);
  }
}

/** Returns true if a keyboard event key matches the action's bound key. */
export function isAction(action: HotkeyAction, eventKey: string): boolean {
  return eventKey.toLowerCase() === getKey(action);
}

export const ALL_ACTIONS: HotkeyAction[] = Object.keys(DEFAULTS) as HotkeyAction[];
