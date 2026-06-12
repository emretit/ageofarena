/**
 * RoomScreen — multiplayer lobby: create/join room, ready up, wait for game_start.
 * Rendered as a DOM overlay on top of the canvas.
 */
import { WsTransport } from '../net/WsTransport';
import { PROTOCOL_VERSION } from '../../../shared/protocol';
import type { ServerMsg, PlayerInfo } from '../../../shared/protocol';
import type { MapType } from '../world/MapGenerator';

export interface MPGameConfig {
  seed: number;
  myTeam: number;
  myPlayerId: string;
  players: PlayerInfo[];
  mapType: MapType;
  transport: WsTransport;
}

export class RoomScreen {
  private readonly _root: HTMLElement;
  private _transport: WsTransport | null = null;
  private _myTeam = 0;
  private _myPlayerId = '';
  private _roomCode = '';

  onGameStart: ((cfg: MPGameConfig) => void) | null = null;

  constructor(private readonly container: HTMLElement) {
    this._root = document.createElement('div');
    Object.assign(this._root.style, {
      position: 'absolute', inset: '0', background: 'rgba(0,0,20,0.97)',
      display: 'none', flexDirection: 'column', alignItems: 'center',
      justifyContent: 'center', color: '#ddd', fontFamily: 'monospace',
      fontSize: '15px', gap: '12px', zIndex: '80',
    });
    this._buildUI();
    container.appendChild(this._root);
  }

  show(): void { this._root.style.display = 'flex'; }
  hide(): void { this._root.style.display = 'none'; }

  private _buildUI(): void {
    const title = document.createElement('h2');
    title.textContent = 'Age of Arena — Multiplayer';
    title.style.marginBottom = '20px';
    this._root.appendChild(title);

    // --- Name row ---
    const nameRow = this._row();
    nameRow.appendChild(this._label('Name:'));
    const nameInput = document.createElement('input');
    Object.assign(nameInput.style, { padding: '4px 8px', background: '#223', color: '#ddd', border: '1px solid #446', borderRadius: '4px', width: '160px' });
    nameInput.value = `Player${Math.floor(Math.random() * 999) + 1}`;
    nameRow.appendChild(nameInput);
    this._root.appendChild(nameRow);

    // --- Server URL ---
    const urlRow = this._row();
    urlRow.appendChild(this._label('Server:'));
    const urlInput = document.createElement('input');
    Object.assign(urlInput.style, { padding: '4px 8px', background: '#223', color: '#ddd', border: '1px solid #446', borderRadius: '4px', width: '220px' });
    urlInput.value = (import.meta as any).env?.VITE_WS_URL ?? 'ws://localhost:2567';
    urlRow.appendChild(urlInput);
    this._root.appendChild(urlRow);

    // --- Create / Join buttons ---
    const btnRow = this._row();

    const createBtn = this._btn('Create Room');
    createBtn.addEventListener('click', async () => {
      const t = new WsTransport();
      try { await t.connect(urlInput.value.trim()); } catch {
        this._setStatus('Connection failed.', true); return;
      }
      this._transport = t;
      this._setCallbacks(t, statusEl, playerList);
      t.send({ type: 'create', playerName: nameInput.value.trim() || 'Player', version: PROTOCOL_VERSION });
    });

    const joinBtn = this._btn('Join Room');
    const codeInput = document.createElement('input');
    Object.assign(codeInput.style, { padding: '4px 8px', background: '#223', color: '#ddd', border: '1px solid #446', borderRadius: '4px', width: '90px', textTransform: 'uppercase', textAlign: 'center' });
    codeInput.placeholder = 'XXXXX';
    codeInput.maxLength = 5;
    joinBtn.addEventListener('click', async () => {
      const code = codeInput.value.trim().toUpperCase();
      if (code.length !== 5) { this._setStatus('Enter 5-char room code.', true); return; }
      const t = new WsTransport();
      try { await t.connect(urlInput.value.trim()); } catch {
        this._setStatus('Connection failed.', true); return;
      }
      this._transport = t;
      this._setCallbacks(t, statusEl, playerList);
      t.send({ type: 'join', roomCode: code, playerName: nameInput.value.trim() || 'Player', version: PROTOCOL_VERSION });
    });

    btnRow.appendChild(createBtn);
    btnRow.appendChild(this._label(' or '));
    btnRow.appendChild(codeInput);
    btnRow.appendChild(joinBtn);
    this._root.appendChild(btnRow);

    // --- Status line ---
    const statusEl = document.createElement('div');
    statusEl.style.cssText = 'color:#aaa; min-height:24px; margin:4px 0;';
    this._root.appendChild(statusEl);

    // --- Player list ---
    const playerList = document.createElement('div');
    Object.assign(playerList.style, { background: '#112', border: '1px solid #334', borderRadius: '6px', padding: '10px 20px', minWidth: '280px', minHeight: '40px' });
    this._root.appendChild(playerList);

    // --- Ready button ---
    const readyBtn = this._btn('Ready', '#181');
    readyBtn.style.display = 'none';
    readyBtn.addEventListener('click', () => {
      this._transport?.send({ type: 'ready' });
      readyBtn.disabled = true;
      readyBtn.textContent = 'Waiting...';
    });
    this._root.appendChild(readyBtn);

    // --- Chat ---
    const chatBox = document.createElement('div');
    Object.assign(chatBox.style, { background: '#112', border: '1px solid #334', borderRadius: '6px', padding: '6px 10px', minWidth: '280px', maxHeight: '80px', overflowY: 'auto', fontSize: '12px' });
    chatBox.style.display = 'none';
    this._root.appendChild(chatBox);

    const chatRow = this._row();
    const chatInput = document.createElement('input');
    Object.assign(chatInput.style, { padding: '4px 8px', background: '#223', color: '#ddd', border: '1px solid #446', borderRadius: '4px', flex: '1' });
    chatInput.placeholder = 'Chat...';
    const chatSend = this._btn('Send');
    const sendChat = () => {
      const m = chatInput.value.trim();
      if (m) { this._transport?.send({ type: 'chat', message: m }); chatInput.value = ''; }
    };
    chatSend.addEventListener('click', sendChat);
    chatInput.addEventListener('keydown', e => { if (e.key === 'Enter') sendChat(); });
    chatRow.appendChild(chatInput);
    chatRow.appendChild(chatSend);
    chatRow.style.display = 'none';
    this._root.appendChild(chatRow);

    // store refs for callbacks
    (this as any)._statusEl = statusEl;
    (this as any)._playerListEl = playerList;
    (this as any)._readyBtn = readyBtn;
    (this as any)._chatBox = chatBox;
    (this as any)._chatRow = chatRow;
  }

  private _setCallbacks(t: WsTransport, statusEl: HTMLElement, playerList: HTMLElement): void {
    t.onMessage = (msg: ServerMsg) => {
      if (msg.type === 'room_created') {
        this._myTeam = msg.team;
        this._myPlayerId = msg.playerId;
        this._roomCode = msg.roomCode;
        statusEl.textContent = `Room created: ${msg.roomCode} — share this code!`;
        (this as any)._readyBtn.style.display = 'inline-block';
        (this as any)._chatBox.style.display = 'block';
        (this as any)._chatRow.style.display = 'flex';
      } else if (msg.type === 'room_joined') {
        this._myTeam = msg.team;
        this._myPlayerId = msg.playerId;
        this._roomCode = msg.roomCode;
        statusEl.textContent = `Joined room: ${msg.roomCode} (team ${msg.team})`;
        this._renderPlayers(msg.players);
        (this as any)._readyBtn.style.display = 'inline-block';
        (this as any)._chatBox.style.display = 'block';
        (this as any)._chatRow.style.display = 'flex';
      } else if (msg.type === 'player_joined' || msg.type === 'player_ready') {
        this._renderPlayers(msg.players);
      } else if (msg.type === 'player_left') {
        statusEl.textContent = `${msg.name} left.`;
      } else if (msg.type === 'game_start') {
        this.hide();
        this.onGameStart?.({
          seed: msg.seed, myTeam: this._myTeam, myPlayerId: this._myPlayerId,
          players: msg.players, mapType: msg.mapType as MapType, transport: t,
        });
      } else if (msg.type === 'error') {
        this._setStatus(`Error: ${msg.message}`, true);
      } else if (msg.type === 'chat') {
        const line = document.createElement('div');
        line.textContent = `${msg.name}: ${msg.message}`;
        (this as any)._chatBox.appendChild(line);
        (this as any)._chatBox.scrollTop = (this as any)._chatBox.scrollHeight;
      }
    };
  }

  private _renderPlayers(players: PlayerInfo[]): void {
    const el = (this as any)._playerListEl as HTMLElement;
    el.innerHTML = '';
    players.forEach(p => {
      const row = document.createElement('div');
      row.textContent = `Team ${p.team}: ${p.name} ${p.ready ? '[READY]' : ''}`;
      row.style.padding = '2px 0';
      el.appendChild(row);
    });
  }

  private _setStatus(msg: string, isError = false): void {
    const el = (this as any)._statusEl as HTMLElement;
    el.textContent = msg;
    el.style.color = isError ? '#f66' : '#aaa';
  }

  private _row(): HTMLElement {
    const d = document.createElement('div');
    d.style.cssText = 'display:flex; align-items:center; gap:8px;';
    return d;
  }

  private _label(text: string): HTMLElement {
    const s = document.createElement('span');
    s.textContent = text;
    s.style.color = '#888';
    return s;
  }

  private _btn(text: string, bg = '#224'): HTMLButtonElement {
    const b = document.createElement('button');
    b.textContent = text;
    Object.assign(b.style, {
      background: bg, color: '#eee', border: '1px solid #446',
      borderRadius: '4px', padding: '6px 14px', cursor: 'pointer', fontFamily: 'monospace',
    });
    return b;
  }
}
