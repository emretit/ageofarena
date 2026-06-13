/**
 * Transport.ts — Abstract transport interface for lockstep.
 * Implementations: WsTransport (real WS), LoopbackTransport (SP).
 */
import type { ClientMsg, ServerMsg, TurnInputMsg, WireCommand } from '../../../shared/protocol';

export type { WireCommand };

export interface Transport {
  /** Called by LockstepClient when a message should be sent. */
  send(msg: ClientMsg): void;

  /**
   * Primary, replaceable handler — used for the lobby→game hand-off
   * (RoomScreen sets it, then LockstepClient replaces it on game start).
   */
  onMessage: ((msg: ServerMsg) => void) | null;

  /**
   * Additional persistent listeners that all receive every message alongside
   * onMessage (e.g. DesyncHandler). These are NOT clobbered by setting onMessage.
   */
  addListener(fn: (msg: ServerMsg) => void): void;

  /** Current connection state. */
  readonly connected: boolean;

  close(): void;
}

export type { ClientMsg, ServerMsg, TurnInputMsg };
