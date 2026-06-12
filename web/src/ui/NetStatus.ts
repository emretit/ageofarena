/**
 * NetStatus — "Waiting for opponent..." overlay shown when sim is stalling.
 * Also shows ping in the corner during MP.
 */
export class NetStatus {
  private readonly _overlay: HTMLElement;
  private readonly _ping: HTMLElement;
  private _stalling = false;

  constructor(container: HTMLElement) {
    // Stall overlay
    this._overlay = document.createElement('div');
    Object.assign(this._overlay.style, {
      display: 'none', position: 'absolute', top: '50%', left: '50%',
      transform: 'translate(-50%,-50%)',
      background: 'rgba(0,0,0,0.8)', color: '#fff', padding: '14px 28px',
      borderRadius: '8px', fontFamily: 'monospace', fontSize: '15px',
      zIndex: '60', pointerEvents: 'none',
    });
    this._overlay.textContent = 'Waiting for opponent...';
    container.appendChild(this._overlay);

    // Ping indicator (top-right corner)
    this._ping = document.createElement('div');
    Object.assign(this._ping.style, {
      display: 'none', position: 'absolute', top: '8px', right: '8px',
      background: 'rgba(0,0,0,0.5)', color: '#0f0', padding: '2px 8px',
      borderRadius: '4px', fontFamily: 'monospace', fontSize: '11px',
      zIndex: '60', pointerEvents: 'none',
    });
    container.appendChild(this._ping);
  }

  setStalling(stalling: boolean): void {
    if (this._stalling === stalling) return;
    this._stalling = stalling;
    this._overlay.style.display = stalling ? 'block' : 'none';
  }

  setPing(ms: number): void {
    this._ping.textContent = `${ms}ms`;
    this._ping.style.display = 'block';
    this._ping.style.color = ms < 60 ? '#0f0' : ms < 150 ? '#ff0' : '#f44';
  }

  hide(): void {
    this._overlay.style.display = 'none';
    this._ping.style.display = 'none';
  }
}
