/**
 * Telemetry.ts — optional Sentry error + desync reporting (server).
 * Offline-safe: if SENTRY_DSN is unset, every call is a no-op.
 * Desync rate is the primary health metric (tagged `kind=desync`).
 */
import * as Sentry from '@sentry/node';

const DSN = process.env.SENTRY_DSN ?? '';

let _enabled = false;

export function initTelemetry(): void {
  if (!DSN || _enabled) return;
  Sentry.init({ dsn: DSN, tracesSampleRate: 0 });
  _enabled = true;
  console.log('[Telemetry] Sentry initialised');
}

/** Report a desync detected by the turn sequencer — the primary reliability metric. */
export function reportDesync(roomCode: string, turn: number, hashes: number[]): void {
  if (!_enabled) return;
  Sentry.captureMessage(`desync room=${roomCode} turn=${turn} hashes=${hashes.join(',')}`, {
    level: 'error',
    tags: { kind: 'desync' },
  });
}

export function reportError(err: unknown): void {
  if (!_enabled) return;
  Sentry.captureException(err);
}
