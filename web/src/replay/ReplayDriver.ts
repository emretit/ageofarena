/**
 * ReplayDriver — feeds a command log to the CommandBus for deterministic playback.
 * In SP mode, the game restarts with the same seed, then driver feeds commands tick-by-tick.
 * Seek: re-run from tick 0 (or nearest keyframe) up to target.
 *
 * Usage:
 *   const driver = new ReplayDriver(rep, bus);
 *   driver.speed = 4;       // ×4 fast-forward
 *   // In the sim tick: driver.tick(tickNumber) → feeds commands for that tick
 */
import type { AoaRep } from './ReplayFile';
import type { CommandBus } from '../sim/CommandBus';
import type { Command } from '../sim/Command';

export type PlaybackState = 'playing' | 'paused' | 'done';

export class ReplayDriver {
  private readonly _byTick: Map<number, Command[]>;
  private _state: PlaybackState = 'playing';
  private _cursor = 0;  // next tick to deliver
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

  pause(): void  { if (this._state === 'playing') this._state = 'paused'; }
  resume(): void { if (this._state === 'paused')  this._state = 'playing'; }
  toggle(): void { this._state === 'playing' ? this.pause() : this.resume(); }

  /**
   * Call once per sim tick. Feeds commands for the current tick into the bus.
   * Returns true if replay still has content, false if done.
   */
  tick(): boolean {
    if (this._state !== 'playing') return true;
    const cmds = this._byTick.get(this._cursor) ?? [];
    for (const cmd of cmds) {
      // Re-issue without tick/seq stamp (they're already stamped in the log)
      this.bus.issue({ ...cmd } as Parameters<typeof this.bus.issue>[0]);
    }
    this._cursor++;
    if (this._cursor > this.rep.durationTicks) {
      this._state = 'done';
      return false;
    }
    return true;
  }
}
