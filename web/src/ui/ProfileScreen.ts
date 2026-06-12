/**
 * ProfileScreen — player profile, match history (last 20), leaderboard.
 * Renders as a full-screen DOM overlay.
 */
import { getClient, isSupabaseConfigured, signOut, upgradeToEmail, type AuthState } from '../net/Auth';

export class ProfileScreen {
  private readonly _root: HTMLElement;
  private _authState: AuthState | null = null;

  onClose: (() => void) | null = null;

  constructor(container: HTMLElement) {
    this._root = document.createElement('div');
    Object.assign(this._root.style, {
      position: 'absolute', inset: '0', background: 'rgba(0,0,20,0.97)',
      display: 'none', flexDirection: 'column', alignItems: 'center',
      overflowY: 'auto', padding: '30px 20px', color: '#ccc',
      fontFamily: 'monospace', fontSize: '14px', zIndex: '90',
    });
    container.appendChild(this._root);
  }

  async show(auth: AuthState): Promise<void> {
    this._authState = auth;
    this._root.style.display = 'flex';
    this._root.innerHTML = '';
    await this._render();
  }

  hide(): void { this._root.style.display = 'none'; }

  private async _render(): Promise<void> {
    const auth = this._authState!;

    // Close button
    const closeBtn = document.createElement('button');
    closeBtn.textContent = 'Close';
    Object.assign(closeBtn.style, { alignSelf: 'flex-end', marginBottom: '16px', background: '#334', color: '#eee', border: '1px solid #446', borderRadius: '4px', padding: '6px 14px', cursor: 'pointer' });
    closeBtn.addEventListener('click', () => { this.hide(); this.onClose?.(); });
    this._root.appendChild(closeBtn);

    const title = document.createElement('h2');
    title.textContent = auth.user?.email ?? (auth.isAnon ? 'Anonymous Player' : 'Player');
    this._root.appendChild(title);

    if (!isSupabaseConfigured) {
      const note = document.createElement('p');
      note.textContent = 'Supabase not configured — profile unavailable offline.';
      note.style.color = '#888';
      this._root.appendChild(note);
      return;
    }

    // Anon upgrade form
    if (auth.isAnon) {
      this._root.appendChild(this._buildUpgradeForm());
    }

    // Profile stats
    if (auth.user) {
      await this._renderStats(auth.user.id);
      await this._renderHistory(auth.user.id);
    }

    await this._renderLeaderboard();
  }

  private _buildUpgradeForm(): HTMLElement {
    const section = document.createElement('div');
    section.style.cssText = 'margin:12px 0; padding:12px; background:#112; border:1px solid #334; border-radius:6px; min-width:300px;';

    const label = document.createElement('p');
    label.textContent = 'Save your ranking — link an email:';
    label.style.marginBottom = '8px';

    const emailIn = document.createElement('input');
    emailIn.placeholder = 'email';
    emailIn.type = 'email';
    Object.assign(emailIn.style, { padding: '4px 8px', background: '#223', color: '#ddd', border: '1px solid #446', borderRadius: '4px', marginRight: '6px' });

    const passIn = document.createElement('input');
    passIn.placeholder = 'password';
    passIn.type = 'password';
    Object.assign(passIn.style, { padding: '4px 8px', background: '#223', color: '#ddd', border: '1px solid #446', borderRadius: '4px', marginRight: '6px' });

    const btn = document.createElement('button');
    btn.textContent = 'Link';
    Object.assign(btn.style, { background: '#224', color: '#eee', border: '1px solid #446', borderRadius: '4px', padding: '4px 12px', cursor: 'pointer' });
    const status = document.createElement('span');
    status.style.marginLeft = '8px';

    btn.addEventListener('click', async () => {
      try {
        await upgradeToEmail(emailIn.value.trim(), passIn.value);
        status.textContent = 'Linked!'; status.style.color = '#0f0';
      } catch (e) {
        status.textContent = String(e); status.style.color = '#f44';
      }
    });

    section.appendChild(label);
    section.appendChild(emailIn);
    section.appendChild(passIn);
    section.appendChild(btn);
    section.appendChild(status);
    return section;
  }

  private async _renderStats(uid: string): Promise<void> {
    const sb = getClient();
    const { data } = await sb.from('ratings').select('elo, wins, losses').eq('player_id', uid).single();
    if (!data) return;
    const stats = document.createElement('p');
    stats.textContent = `ELO: ${data.elo}  Wins: ${data.wins}  Losses: ${data.losses}`;
    this._root.appendChild(stats);
  }

  private async _renderHistory(uid: string): Promise<void> {
    const sb = getClient();
    const { data } = await sb
      .from('match_players')
      .select('result, elo_before, elo_after, matches(room_code, ended_at)')
      .eq('player_id', uid)
      .order('id', { ascending: false })
      .limit(20);

    if (!data || data.length === 0) return;

    const h3 = document.createElement('h3');
    h3.textContent = 'Recent matches';
    this._root.appendChild(h3);

    const table = document.createElement('table');
    table.style.cssText = 'border-collapse:collapse; width:100%; max-width:500px;';
    const header = document.createElement('tr');
    ['Room', 'Result', 'ELO', 'Date'].forEach(t => {
      const th = document.createElement('th');
      th.textContent = t;
      th.style.cssText = 'padding:4px 8px; border-bottom:1px solid #334; text-align:left;';
      header.appendChild(th);
    });
    table.appendChild(header);

    for (const row of data as any[]) {
      const tr = document.createElement('tr');
      const match = row.matches ?? {};
      const delta = row.elo_after - row.elo_before;
      [match.room_code ?? '?', row.result, `${row.elo_after} (${delta >= 0 ? '+' : ''}${delta})`, match.ended_at?.slice(0, 10) ?? '?'].forEach(v => {
        const td = document.createElement('td');
        td.textContent = String(v);
        td.style.cssText = 'padding:3px 8px;';
        if (String(v) === 'win') td.style.color = '#4f4';
        if (String(v) === 'loss') td.style.color = '#f44';
        tr.appendChild(td);
      });
      table.appendChild(tr);
    }
    this._root.appendChild(table);
  }

  private async _renderLeaderboard(): Promise<void> {
    const sb = getClient();
    const { data } = await sb.from('leaderboard').select('*');
    if (!data || data.length === 0) return;

    const h3 = document.createElement('h3');
    h3.textContent = 'Leaderboard';
    h3.style.marginTop = '24px';
    this._root.appendChild(h3);

    const table = document.createElement('table');
    table.style.cssText = 'border-collapse:collapse; max-width:500px; width:100%;';
    for (const row of data as any[]) {
      const tr = document.createElement('tr');
      [`#${row.rank}`, row.username, `${row.elo} ELO`, `${row.wins}W/${row.losses}L`].forEach(v => {
        const td = document.createElement('td');
        td.textContent = v;
        td.style.cssText = 'padding:3px 8px;';
        tr.appendChild(td);
      });
      table.appendChild(tr);
    }
    this._root.appendChild(table);
  }
}
