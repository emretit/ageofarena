/**
 * matchmake Edge Function — pairs players in mm_queue within ELO ±window.
 * Schedule: cron every 10 seconds.
 * Creates room on game server via POST /internal/create-room.
 *
 * Env vars:
 *   SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY (auto-injected in Edge Functions)
 *   GAME_SERVER_URL      — e.g. https://ageofarena.up.railway.app
 *   GAME_SERVER_SECRET   — shared secret header
 */
import { createClient } from 'https://esm.sh/@supabase/supabase-js@2';

const ELO_WINDOW_START = 100;
const ELO_WINDOW_EXPAND = 50;
const WINDOW_EXPAND_SECS = 30;

Deno.serve(async () => {
  const sb = createClient(
    Deno.env.get('SUPABASE_URL')!,
    Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')!,
  );

  const serverUrl = Deno.env.get('GAME_SERVER_URL');
  const serverSecret = Deno.env.get('GAME_SERVER_SECRET') ?? '';

  // Fetch queue sorted by ELO
  const { data: queue, error } = await sb
    .from('mm_queue')
    .select('player_id, elo, queued_at')
    .order('elo', { ascending: true });

  if (error || !queue || queue.length < 2) {
    return new Response(JSON.stringify({ matched: 0 }), { status: 200 });
  }

  const matched: string[][] = [];
  const used = new Set<string>();

  for (let i = 0; i < queue.length - 1; i++) {
    const a = queue[i];
    if (used.has(a.player_id)) continue;

    for (let j = i + 1; j < queue.length; j++) {
      const b = queue[j];
      if (used.has(b.player_id)) continue;

      // Compute expanded ELO window based on wait time
      const waitSecs = (Date.now() - new Date(a.queued_at).getTime()) / 1000;
      const window = ELO_WINDOW_START + Math.floor(waitSecs / WINDOW_EXPAND_SECS) * ELO_WINDOW_EXPAND;

      if (Math.abs(a.elo - b.elo) <= window) {
        matched.push([a.player_id, b.player_id]);
        used.add(a.player_id);
        used.add(b.player_id);
        break;
      }
    }
  }

  let createdRooms = 0;

  for (const pair of matched) {
    // Create room first; only remove from queue on success
    if (serverUrl) {
      try {
        await fetch(`${serverUrl}/internal/create-room`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'X-Secret': serverSecret },
          body: JSON.stringify({ playerIds: pair }),
        });
        await sb.from('mm_queue').delete().in('player_id', pair);
        createdRooms++;
      } catch {
        // Server unreachable — leave players in queue for next cycle
      }
    } else {
      // No game server configured — still remove from queue to avoid stale entries
      await sb.from('mm_queue').delete().in('player_id', pair);
    }
  }

  return new Response(JSON.stringify({ matched: matched.length, rooms: createdRooms }), { status: 200 });
});
