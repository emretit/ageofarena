/**
 * ReplayDriver — feeds a command log to the CommandBus for deterministic playback.
 * In SP mode, the game restarts with the same seed, then driver feeds commands tick-by-tick.
 *
 * Seek: seekForward(targetTick) — fast-forward from cursor to target.
 * The frame loop checks `seeking` and injects extra sim ticks per render frame.
 * Backward seek is not supported without a full game restart.
 */
import type { AoaRep } from './ReplayFile';
import type { CommandBus } from '../sim/CommandBus';
import type { Command } from '../sim/Command';

export type PlaybackState = 'playing' | 'paused' | 'done';

export const SEEK_BURST = 60; // extra sim ticks injected per render frame while seeking

export class ReplayDriver {
  private readonly _byTick: Map<number, Command[]>;
  private _state: PlaybackState = 'playing';
  private _cursor = 0;  // next tick to deliver
  private _seeking = false;
  private _seekTarget: number | null = null;
  speed: 1 | 2 | 4 | 8 = 1;

  constructor(
    private readonly rep: AoaRep,
    private readonly bus: CommandBus,
  ) {
    // Index commands by tick for O(1) lookup
    this._byTick = new Map();
    for (const cmd of rep.commands) {
      if (!this._byTick.has(cmd.tick)) this._byTick.set(cmd.tick, []);
      this._byTick.get(cmd.tick)!.push(cmd);
    }
  }

  get state(): PlaybackState { return this._state; }
  get durationTicks(): number { return this.rep.durationTicks; }
  get cursor(): number { return this._cursor; }
  get progressFraction(): number {
    return this.rep.durationTicks > 0 ? this._cursor / this.rep.durationTicks : 0;
  }
  get seeking(): boolean { return this._seeking; }
  get seekTarget(): number | null { return this._seekTarget; }

  pause(): void  { if (this._state === 'playing') this._state = 'paused'; }
  resume(): void { if (this._state === 'paused')  this._state = 'playing'; }
  toggle(): void { this._state === 'playing' ? this.pause() : this.resume(); }

  /**
   * Seek forward to targetTick. The frame loop must inject SEEK_BURST extra sim ticks
   * per render frame (check `seeking`). Backward seek is unsupported — cursor must be < target.
   */
  seekForward(targetTick: number): void {
    if (targetTick <= this._cursor) return;
    this._seekTarget = Math.min(targetTick, this.rep.durationTicks);
    this._seeking = true;
    if (this._state !== 'playing') this._state = 'playing';
  }

  /**
   * Call once per sim tick. Feeds commands for the current tick into the bus.
   * Returns true if replay still has content, false when done.
   */
  tick(): boolean {
    if (this._state !== 'playing') return true;
    const cmds = this._byTick.get(this._cursor) ?? [];
    for (const cmd of cmds) {
      this.bus.issue({ ...cmd } as Parameters<typeof this.bus.issue>[0]);
    }
    this._cursor++;

    // Auto-clear seek when target is reached
    if (this._seeking && this._cursor >= (this._seekTarget ?? 0)) {
      this._seeking = false;
      this._seekTarget = null;
    }

    if (this._cursor > this.rep.durationTicks) {
      this._state = 'done';
      return false;
    }
    return true;
  }
}
