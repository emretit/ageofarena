/**
 * DesyncHandler — detects checksum mismatches during MP playback.
 * Sends periodic checksum reports to server; shows desync banner on mismatch.
 * In SP mode, no-op (no transport).
 */
import type { Transport } from './Transport';
import type { CommandBus } from '../sim/CommandBus';
import { Checksum } from '../sim/Checksum';
import { reportDesync } from './Telemetry';

const SAMPLE_EVERY_TICKS = 30;

export class DesyncHandler {
  private _bannerEl: HTMLElement | null = null;
  private _lastSampleTick = 0;

  constructor(
    private readonly _bus: CommandBus | null,
    private readonly _transport: Transport | null,
  ) {
    if (_transport) {
      // addListener (not onMessage=) so we don't clobber LockstepClient's turn handler.
      _transport.addListener((msg) => {
        if (msg.type === 'desync') {
          this._showBanner(msg.turn);
          reportDesync(msg.turn);
        }
      });
    }
  }

  /** Call each sim tick. Sends checksum sample every SAMPLE_EVERY_TICKS ticks. */
  tick(tick: number, container: HTMLElement): void {
    if (!this._transport || !this._bus) return;
    if (tick - this._lastSampleTick < SAMPLE_EVERY_TICKS) return;
    this._lastSampleTick = tick;

    const log = this._bus.getLog();
    const hash = Checksum.ofCommandLog([...log]);
    const turn = Math.floor(tick / SAMPLE_EVERY_TICKS);
    this._transport.send({ type: 'checksum', turn, hash });

    this._ensureBannerEl(container);
  }

  private _showBanner(turn: number): void {
    if (!this._bannerEl) return;
    this._bannerEl.textContent = `DESYNC at turn ${turn} — game state diverged`;
    this._bannerEl.style.display = 'block';
    // Also dump replay for debugging
    console.error(`[DesyncHandler] mismatch at turn ${turn}`);
  }

  private _ensureBannerEl(container: HTMLElement): void {
    if (this._bannerEl) return;
    this._bannerEl = document.createElement('div');
    Object.assign(this._bannerEl.style, {
      display: 'none',
      position: 'absolute', top: '50%', left: '50%',
      transform: 'translate(-50%,-50%)',
      background: '#c00', color: '#fff', padding: '16px 32px',
      borderRadius: '8px', fontFamily: 'monospace', fontSize: '18px',
      zIndex: '100', pointerEvents: 'none',
    });
    container.appendChild(this._bannerEl);
  }
}
