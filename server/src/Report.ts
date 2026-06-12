/**
 * Report.ts — posts match results to Supabase after a game ends.
 * Called by the game server (not the client) — uses service role key.
 *
 * Env vars: SUPABASE_URL, SUPABASE_SERVICE_KEY, MATCH_SECRET
 */

const SUPABASE_URL = process.env.SUPABASE_URL ?? '';
const SUPABASE_KEY = process.env.SUPABASE_SERVICE_KEY ?? '';
const MATCH_SECRET = process.env.MATCH_SECRET ?? '';

export interface PlayerResult {
  player_id: string;
  team: number;
  result: 'win' | 'loss' | 'draw' | 'void';
}

export interface MatchReport {
  roomCode: string;
  mapType: number;
  durationSeconds: number;
  replayUrl?: string;
  results: PlayerResult[];
}

export async function reportMatch(report: MatchReport): Promise<string | null> {
  if (!SUPABASE_URL || !SUPABASE_KEY) {
    console.warn('[Report] Supabase not configured — skipping match report');
    return null;
  }

  const body = {
    p_room_code:  report.roomCode,
    p_map_type:   report.mapType,
    p_duration_s: report.durationSeconds,
    p_replay_url: report.replayUrl ?? null,
    p_results:    report.results,
    p_secret:     MATCH_SECRET,
  };

  const res = await fetch(`${SUPABASE_URL}/rest/v1/rpc/apply_match_result`, {
    method: 'POST',
    headers: {
      'Content-Type':  'application/json',
      'apikey':        SUPABASE_KEY,
      'Authorization': `Bearer ${SUPABASE_KEY}`,
    },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const text = await res.text();
    console.error(`[Report] apply_match_result failed: ${res.status} ${text}`);
    return null;
  }

  const matchId = String(await res.json());
  console.log(`[Report] match recorded: ${matchId}`);
  return matchId;
}
