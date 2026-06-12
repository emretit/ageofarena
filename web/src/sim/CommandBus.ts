/**
 * CommandBus.ts — collects commands this tick, drains in (teamId, seq) order.
 * Lockstep-ready: in SP mode, player commands are delayed 1 tick (GameClock).
 * getLog() returns the full command history for replay/save.
 */
import type { Command } from "./Command";

/** Distributive Omit — preserves discriminated union variants. */
type DistOmit<T, K extends keyof T> = T extends unknown ? Omit<T, K> : never;
export type CommandInput = DistOmit<Command, 'tick' | 'seq'>;

export class CommandBus {
  private _currentTick = 0;
  private readonly _pending: Command[] = [];
  private readonly _log: Command[] = [];
  private readonly _seqs = new Map<number, number>(); // teamId → next seq

  /** Advance the tick counter. Call once per sim tick before draining. */
  advanceTick(): void {
    this._currentTick++;
  }

  /** Issue a command. Stamps tick and seq, pushes to pending. */
  issue(cmd: CommandInput): void {
    const seq = this._seqs.get(cmd.teamId) ?? 0;
    this._seqs.set(cmd.teamId, seq + 1);
    const stamped = { ...cmd, tick: this._currentTick, seq } as Command;
    this._pending.push(stamped);
  }

  /**
   * Drain all pending commands for the current tick, sorted by (teamId, seq).
   * Returns the sorted list and clears _pending.
   */
  drain(): Command[] {
    const cmds = this._pending.splice(0);
    cmds.sort((a, b) => a.teamId !== b.teamId ? a.teamId - b.teamId : a.seq - b.seq);
    this._log.push(...cmds);
    return cmds;
  }

  /** Full command log — suitable for JSON round-trip (replay/save). */
  getLog(): readonly Command[] {
    return this._log;
  }

  get currentTick(): number { return this._currentTick; }
}
