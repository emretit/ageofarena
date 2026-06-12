/**
 * LoopbackTransport — SP loopback that simulates server turn sequencing.
 * All send() calls are processed synchronously (zero network latency).
 * Used for single-player mode so SP and MP share the same LockstepClient code path.
 */
import type { Transport } from './Transport';
import type { ClientMsg, ServerMsg, WireCommand } from '../../../shared/protocol';

export class LoopbackTransport implements Transport {
  onMessage: ((msg: ServerMsg) => void) | null = null;
  readonly connected = true;

  private readonly _playerIds: string[];
  private readonly _turnBuffers = new Map<number, Map<string, WireCommand[]>>();

  /** Pass the player IDs that will participate (for turn completion check). */
  constructor(playerIds: string[]) {
    this._playerIds = playerIds;
  }

  send(msg: ClientMsg): void {
    if (msg.type === 'turn_input') {
      this._handleTurnInput(msg.turn, msg.commands);
    }
    // Other message types (create/join/ready) are no-ops in loopback
  }

  close(): void { /* nothing */ }

  private _handleTurnInput(turn: number, commands: WireCommand[]): void {
    if (!this._turnBuffers.has(turn)) this._turnBuffers.set(turn, new Map());
    const buf = this._turnBuffers.get(turn)!;

    // In SP, only one player. Use first id.
    const pid = this._playerIds[0] ?? 'local';
    buf.set(pid, commands);

    if (buf.size >= this._playerIds.length) {
      const inputs = [...buf.entries()].map(([playerId, cmds]) => ({ playerId, commands: cmds }));
      this._turnBuffers.delete(turn);
      this.onMessage?.({ type: 'turn', turn, inputs });
    }
  }
}
