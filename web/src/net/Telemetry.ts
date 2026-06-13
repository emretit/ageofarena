/**
 * Telemetry.ts — optional Sentry error + desync reporting (client).
 * Offline-safe: if VITE_SENTRY_DSN is unset, every call is a no-op.
 * Desync rate is the primary health metric (tagged `kind=desync`).
 */
import * as Sentry from '@sentry/browser';
import { versionString } from '../../../shared/Versions';

const DSN = (import.meta as unknown as { env?: Record<string, string | undefined> }).env?.VITE_SENTRY_DSN ?? '';

let _enabled = false;

export function initTelemetry(): void {
  if (!DSN || _enabled) return;
  Sentry.init({ dsn: DSN, release: versionString(), tracesSampleRate: 0 });
  _enabled = true;
}

/** Report a desync — the primary reliability metric. */
export function reportDesync(turn: number, detail?: string): void {
  if (!_enabled) return;
  Sentry.captureMessage(`desync turn=${turn}${detail ? ' ' + detail : ''}`, {
    level: 'error',
    tags: { kind: 'desync' },
  });
}

export function reportError(err: unknown): void {
  if (!_enabled) return;
  Sentry.captureException(err);
}
