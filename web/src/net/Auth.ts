/**
 * Auth.ts — Supabase anonymous auth + email upgrade.
 * Uses env vars: VITE_SUPABASE_URL, VITE_SUPABASE_ANON_KEY.
 * Offline-safe: if env vars are missing, returns a no-op stub.
 */
import { createClient, type SupabaseClient, type Session, type User } from '@supabase/supabase-js';

const SUPABASE_URL = (import.meta as any).env?.VITE_SUPABASE_URL ?? '';
const SUPABASE_KEY = (import.meta as any).env?.VITE_SUPABASE_ANON_KEY ?? '';

export const isSupabaseConfigured = SUPABASE_URL !== '' && SUPABASE_KEY !== '';

let _client: SupabaseClient | null = null;

function client(): SupabaseClient {
  if (!_client) {
    if (!isSupabaseConfigured) throw new Error('Supabase not configured (missing VITE_SUPABASE_URL/VITE_SUPABASE_ANON_KEY)');
    _client = createClient(SUPABASE_URL, SUPABASE_KEY);
  }
  return _client;
}

export interface AuthState {
  user: User | null;
  session: Session | null;
  isAnon: boolean;
}

// ── Anonymous sign-in ────────────────────────────────────────────────────────

/** Sign in anonymously (no email needed). uid is stable across sessions via localStorage. */
export async function signInAnon(): Promise<AuthState> {
  const sb = client();
  const { data: existing } = await sb.auth.getSession();
  if (existing.session) {
    return { user: existing.session.user, session: existing.session, isAnon: isAnonUser(existing.session.user) };
  }
  const { data, error } = await sb.auth.signInAnonymously();
  if (error) throw error;
  return { user: data.user, session: data.session, isAnon: true };
}

/** Upgrade anonymous account to email. uid is preserved. */
export async function upgradeToEmail(email: string, password: string): Promise<AuthState> {
  const sb = client();
  const { data, error } = await sb.auth.updateUser({ email, password });
  if (error) throw error;
  const { data: s } = await sb.auth.getSession();
  return { user: data.user, session: s.session, isAnon: false };
}

export async function getSession(): Promise<AuthState> {
  const sb = client();
  const { data } = await sb.auth.getSession();
  return { user: data.session?.user ?? null, session: data.session ?? null, isAnon: isAnonUser(data.session?.user ?? null) };
}

export async function signOut(): Promise<void> {
  await client().auth.signOut();
}

function isAnonUser(user: User | null): boolean {
  return (user?.app_metadata?.['provider'] ?? '') === 'anonymous';
}

export function getClient(): SupabaseClient {
  return client();
}
