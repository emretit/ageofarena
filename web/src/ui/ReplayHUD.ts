/**
 * ReplayHUD — overlay panel shown during replay playback.
 * Timeline bar (click to seek forward) + play/pause + speed selector.
 */
import type { ReplayDriver } from '../replay/ReplayDriver';

export class ReplayHUD {
  private readonly _root: HTMLElement;
  private readonly _fill: HTMLElement;
  private readonly _seekFill: HTMLElement;
  private readonly _label: HTMLElement;

  constructor(container: HTMLElement, private readonly driver: ReplayDriver) {
    this._root = document.createElement('div');
    Object.assign(this._root.style, {
      position: 'absolute', bottom: '12px', left: '50%', transform: 'translateX(-50%)',
      background: 'rgba(0,0,0,0.75)', borderRadius: '8px', padding: '8px 14px',
      display: 'flex', alignItems: 'center', gap: '10px', zIndex: '50',
      fontFamily: 'monospace', color: '#ddd', fontSize: '13px', userSelect: 'none',
      pointerEvents: 'auto',
    });

    // Play/pause button
    const playBtn = document.createElement('button');
    Object.assign(playBtn.style, {
      background: 'none', border: '1px solid #666', color: '#eee', cursor: 'pointer',
      borderRadius: '4px', padding: '2px 8px', fontSize: '14px',
    });
    playBtn.textContent = '⏸';
    playBtn.addEventListener('click', () => {
      driver.toggle();
      playBtn.textContent = driver.state === 'playing' ? '⏸' : '▶';
    });
    this._root.appendChild(playBtn);

    // Speed selector
    const speeds: Array<1 | 2 | 4 | 8> = [1, 2, 4, 8];
    for (const s of speeds) {
      const btn = document.createElement('button');
      Object.assign(btn.style, {
        background: s === 1 ? '#335' : 'none', border: '1px solid #555',
        color: '#ccc', cursor: 'pointer', borderRadius: '4px', padding: '2px 6px', fontSize: '12px',
      });
      btn.textContent = `×${s}`;
      btn.addEventListener('click', () => {
        driver.speed = s;
        this._root.querySelectorAll<HTMLButtonElement>('[data-speed]').forEach(b => {
          b.style.background = parseInt(b.dataset.speed!) === s ? '#335' : 'none';
        });
      });
      btn.dataset.speed = String(s);
      this._root.appendChild(btn);
    }

    // Timeline bar — click to seek forward to that position
    const barWrap = document.createElement('div');
    Object.assign(barWrap.style, {
      position: 'relative', width: '200px', height: '6px', background: '#333',
      borderRadius: '3px', cursor: 'pointer',
    });

    // Ghost fill — shows seek target while fast-forwarding
    this._seekFill = document.createElement('div');
    Object.assign(this._seekFill.style, {
      position: 'absolute', top: '0', left: '0', height: '100%',
      background: 'rgba(100,180,255,0.25)', borderRadius: '3px', width: '0%',
      pointerEvents: 'none',
    });

    this._fill = document.createElement('div');
    Object.assign(this._fill.style, {
      position: 'absolute', top: '0', left: '0',
      height: '100%', background: '#4af', borderRadius: '3px', width: '0%',
      transition: 'width 0.1s',
    });

    barWrap.appendChild(this._seekFill);
    barWrap.appendChild(this._fill);
    this._root.appendChild(barWrap);

    barWrap.addEventListener('click', (e) => {
      const rect = barWrap.getBoundingClientRect();
      const frac = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
      const targetTick = Math.floor(frac * driver.durationTicks);
      driver.seekForward(targetTick);
    });

    // Time label
    this._label = document.createElement('span');
    this._label.style.minWidth = '80px';
    this._root.appendChild(this._label);

    container.appendChild(this._root);
  }

  tick(): void {
    const pct = Math.min(100, this.driver.progressFraction * 100);
    this._fill.style.width = `${pct}%`;

    // Ghost bar tracks seek target while fast-forwarding
    if (this.driver.seeking && this.driver.seekTarget !== null) {
      const targetPct = Math.min(100, (this.driver.seekTarget / this.driver.durationTicks) * 100);
      this._seekFill.style.width = `${targetPct}%`;
    } else {
      this._seekFill.style.width = `${pct}%`;
    }

    const secs  = Math.floor(this.driver.cursor / 30);
    const total = Math.floor(this.driver.durationTicks / 30);
    if (this.driver.seeking && this.driver.seekTarget !== null) {
      this._label.textContent = `${fmt(secs)} >> ${fmt(Math.floor(this.driver.seekTarget / 30))}`;
    } else if (this.driver.state === 'done') {
      this._label.textContent = `${fmt(total)} [END]`;
    } else {
      this._label.textContent = `${fmt(secs)} / ${fmt(total)}`;
    }
  }

  remove(): void { this._root.remove(); }
}

function fmt(s: number): string {
  const m = Math.floor(s / 60);
  const ss = s % 60;
  return `${m}:${String(ss).padStart(2, '0')}`;
}
