/**
 * Checksum — FNV-1a 32-bit deterministic hash for sim state snapshots.
 * Feed unit/building state in canonical order (sorted by entity ID).
 * Used for cross-browser determinism verification.
 */

const FNV_PRIME  = 0x01000193;
const FNV_OFFSET = 0x811c9dc5;

export class Checksum {
  private _hash = FNV_OFFSET;

  /** Feed an integer value (up to 32 bits). */
  feedInt(n: number): this {
    const u = n >>> 0; // to unsigned 32-bit
    this._hash = (Math.imul(this._hash ^ ((u >> 24) & 0xff), FNV_PRIME)) >>> 0;
    this._hash = (Math.imul(this._hash ^ ((u >> 16) & 0xff), FNV_PRIME)) >>> 0;
    this._hash = (Math.imul(this._hash ^ ((u >>  8) & 0xff), FNV_PRIME)) >>> 0;
    this._hash = (Math.imul(this._hash ^ ( u        & 0xff), FNV_PRIME)) >>> 0;
    return this;
  }

  /** Feed a float as q*256 fixed-point integer. */
  feedQ(v: number): this {
    return this.feedInt(Math.round(v * 256));
  }

  get value(): number { return this._hash; }

  /** Compute checksum of a command log (for replay identity verification). */
  static ofCommandLog(log: ReadonlyArray<{ tick: number; seq: number; teamId: number; kind: string }>): number {
    const cs = new Checksum();
    for (const c of log) {
      cs.feedInt(c.tick).feedInt(c.seq).feedInt(c.teamId);
      for (let i = 0; i < c.kind.length; i++) cs.feedInt(c.kind.charCodeAt(i));
    }
    return cs.value;
  }
}
