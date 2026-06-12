/**
 * LobbyBrowser — lists open rooms via Supabase Realtime.
 * Requires VITE_SUPABASE_URL + VITE_SUPABASE_ANON_KEY in env.
 */
import { getClient, isSupabaseConfigured } from '../net/Auth';

export interface LobbyEntry {
  room_code: string;
  host_id: string;
  player_count: number;
  map_type: number;
  created_at: string;
}

export class LobbyBrowser {
  private readonly _root: HTMLElement;
  private _entries: LobbyEntry[] = [];
  private _channel: ReturnType<ReturnType<typeof getClient>['channel']> | null = null;

  onJoin: ((roomCode: string) => void) | null = null;

  constructor(container: HTMLElement) {
    this._root = document.createElement('div');
    Object.assign(this._root.style, {
      background: '#112', border: '1px solid #334', borderRadius: '6px',
      padding: '10px', minWidth: '340px', maxHeight: '300px', overflowY: 'auto',
      fontFamily: 'monospace', fontSize: '13px', color: '#ccc',
    });
    container.appendChild(this._root);
  }

  async start(): Promise<void> {
    if (!isSupabaseConfigured) {
      this._root.textContent = 'Supabase not configured';
      return;
    }

    await this._fetch();

    // Subscribe to realtime changes on lobbies table
    const sb = getClient();
    this._channel = sb.channel('lobbies-changes')
      .on('postgres_changes', { event: '*', schema: 'public', table: 'lobbies' }, () => {
        void this._fetch();
      })
      .subscribe();
  }

  stop(): void {
    this._channel?.unsubscribe();
    this._channel = null;
  }

  private async _fetch(): Promise<void> {
    const sb = getClient();
    const { data, error } = await sb
      .from('lobbies')
      .select('*')
      .order('created_at', { ascending: false })
      .limit(20);

    if (error) { console.error('[LobbyBrowser]', error); return; }
    this._entries = (data ?? []) as LobbyEntry[];
    this._render();
  }

  private _render(): void {
    this._root.innerHTML = '';
    if (this._entries.length === 0) {
      const empty = document.createElement('div');
      empty.style.color = '#666';
      empty.textContent = 'No open rooms. Create one!';
      this._root.appendChild(empty);
      return;
    }
    for (const e of this._entries) {
      const row = document.createElement('div');
      Object.assign(row.style, {
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        padding: '4px 0', borderBottom: '1px solid #223',
      });
      const info = document.createElement('span');
      info.textContent = `${e.room_code}  (${e.player_count}/2)  map:${e.map_type}`;
      const btn = document.createElement('button');
      btn.textContent = 'Join';
      Object.assign(btn.style, {
        background: '#224', color: '#eee', border: '1px solid #446',
        borderRadius: '3px', padding: '2px 8px', cursor: 'pointer', fontFamily: 'monospace',
      });
      btn.addEventListener('click', () => this.onJoin?.(e.room_code));
      row.appendChild(info);
      row.appendChild(btn);
      this._root.appendChild(row);
    }
  }
}
