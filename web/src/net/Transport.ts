/**
 * Transport.ts — Abstract transport interface for lockstep.
 * Implementations: WsTransport (real WS), LoopbackTransport (SP).
 */
import type { ClientMsg, ServerMsg, TurnInputMsg, WireCommand } from '../../../shared/protocol';

export type { WireCommand };

export interface Transport {
  /** Called by LockstepClient when a message should be sent. */
  send(msg: ClientMsg): void;

  /** Set by LockstepClient to receive incoming server messages. */
  onMessage: ((msg: ServerMsg) => void) | null;

  /** Current connection state. */
  readonly connected: boolean;

  close(): void;
}

export type { ClientMsg, ServerMsg, TurnInputMsg };
