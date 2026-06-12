/**
 * WsTransport — WebSocket transport for multiplayer.
 * Connects to the Age of Arena game server.
 */
import type { Transport } from './Transport';
import type { ClientMsg, ServerMsg } from '../../../shared/protocol';

export class WsTransport implements Transport {
  onMessage: ((msg: ServerMsg) => void) | null = null;
  private _ws: WebSocket | null = null;
  private _connected = false;

  get connected(): boolean { return this._connected; }

  connect(url: string): Promise<void> {
    return new Promise((resolve, reject) => {
      const ws = new WebSocket(url);
      this._ws = ws;

      ws.addEventListener('open', () => {
        this._connected = true;
        resolve();
      });
      ws.addEventListener('error', (e) => {
        reject(new Error(`WS connect failed: ${url}`));
      });
      ws.addEventListener('close', () => {
        this._connected = false;
      });
      ws.addEventListener('message', (e) => {
        let msg: ServerMsg;
        try { msg = JSON.parse(e.data as string); } catch { return; }
        this.onMessage?.(msg);
      });
    });
  }

  send(msg: ClientMsg): void {
    if (this._ws && this._connected) {
      this._ws.send(JSON.stringify(msg));
    }
  }

  close(): void {
    this._ws?.close();
    this._connected = false;
  }
}
