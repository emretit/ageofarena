/**
 * CampaignScreen.ts — Mission list overlay UI.
 * Shows locked/unlocked/completed missions and lets the player start one.
 */
import { MISSIONS, isMissionUnlocked, isMissionComplete, resetProgress } from "../game/CampaignSystem";

export class CampaignScreen {
  private readonly _el: HTMLDivElement;

  /** Fired when the player selects and starts a mission. */
  onStart:  ((missionId: number) => void) | null = null;
  /** Fired when the player closes the screen without starting. */
  onClose: (() => void) | null = null;

  constructor(private readonly _container: HTMLElement) {
    this._el = document.createElement("div");
    this._el.style.cssText = `
      position:absolute; inset:0; background:rgba(4,6,14,0.97);
      display:none; flex-direction:column; align-items:center; justify-content:center;
      font-family:monospace; color:#c8c8e0; z-index:9998; overflow-y:auto; padding:30px 0;
    `;
    this._container.appendChild(this._el);
  }

  show(): void {
    this._el.style.display = "flex";
    this._rebuild();
  }

  hide(): void {
    this._el.style.display = "none";
  }

  private _rebuild(): void {
    this._el.innerHTML = "";

    const title = document.createElement("div");
    title.textContent = "KAMPANYA";
    title.style.cssText = "font-size:28px;font-weight:bold;color:#f5d060;margin-bottom:6px;letter-spacing:3px;";
    this._el.appendChild(title);

    const sub = document.createElement("div");
    sub.textContent = "Üç görevi tamamlayarak imparatorluğunu inşa et.";
    sub.style.cssText = "font-size:12px;color:#888;margin-bottom:28px;";
    this._el.appendChild(sub);

    // Mission cards
    const list = document.createElement("div");
    list.style.cssText = "display:flex;flex-direction:column;gap:14px;width:540px;";

    for (const m of MISSIONS) {
      const unlocked = isMissionUnlocked(m.id);
      const complete  = isMissionComplete(m.id);

      const card = document.createElement("div");
      card.style.cssText = `
        padding:16px 20px; border:2px solid ${complete ? '#3a9a3a' : unlocked ? '#4a5a7a' : '#2a2a3a'};
        border-radius:8px; background:${complete ? '#0a1a0a' : unlocked ? '#0a0e1a' : '#080a14'};
        opacity:${unlocked ? '1' : '0.5'};
      `;

      const row = document.createElement("div");
      row.style.cssText = "display:flex;align-items:center;justify-content:space-between;";

      const nameEl = document.createElement("div");
      nameEl.style.cssText = `font-size:16px;font-weight:bold;color:${complete ? '#5ddc5d' : unlocked ? '#f5d060' : '#666'};`;
      nameEl.textContent = `${m.id + 1}. ${m.name}${complete ? ' ✓' : !unlocked ? ' (Kilitli)' : ''}`;
      row.appendChild(nameEl);

      if (unlocked && !complete) {
        const startBtn = document.createElement("button");
        startBtn.textContent = "Başlat";
        startBtn.style.cssText = `
          padding:6px 20px; font-size:13px; font-weight:bold; font-family:monospace;
          background:#1f5c22; color:#c8f5c0; border:1px solid #3a9a3a; border-radius:5px;
          cursor:pointer;
        `;
        startBtn.addEventListener("click", () => {
          this.hide();
          this.onStart?.(m.id);
        });
        row.appendChild(startBtn);
      } else if (complete) {
        const replayBtn = document.createElement("button");
        replayBtn.textContent = "Tekrar Oyna";
        replayBtn.style.cssText = `
          padding:6px 16px; font-size:12px; font-family:monospace;
          background:#0a1a0a; color:#5ddc5d; border:1px solid #3a7a3a; border-radius:5px;
          cursor:pointer;
        `;
        replayBtn.addEventListener("click", () => {
          this.hide();
          this.onStart?.(m.id);
        });
        row.appendChild(replayBtn);
      }

      card.appendChild(row);

      const brief = document.createElement("div");
      brief.textContent = m.briefing;
      brief.style.cssText = "font-size:12px;color:#8a9aaa;margin-top:8px;line-height:1.5;";
      card.appendChild(brief);

      list.appendChild(card);
    }
    this._el.appendChild(list);

    // Buttons row
    const btnRow = document.createElement("div");
    btnRow.style.cssText = "display:flex;gap:12px;margin-top:28px;";

    const backBtn = document.createElement("button");
    backBtn.textContent = "Geri";
    backBtn.style.cssText = `
      padding:10px 32px; font-size:15px; font-family:monospace;
      background:#1a1e2e; color:#8a9aaa; border:1px solid #3a4a6a; border-radius:6px;
      cursor:pointer;
    `;
    backBtn.addEventListener("click", () => { this.hide(); this.onClose?.(); });
    btnRow.appendChild(backBtn);

    const resetBtn = document.createElement("button");
    resetBtn.textContent = "İlerlemeyi Sıfırla";
    resetBtn.style.cssText = `
      padding:10px 24px; font-size:12px; font-family:monospace;
      background:#1a0a0a; color:#9a5a5a; border:1px solid #5a2a2a; border-radius:6px;
      cursor:pointer;
    `;
    resetBtn.addEventListener("click", () => {
      if (confirm("Tüm kampanya ilerlemesi silinecek. Emin misin?")) {
        resetProgress();
        this._rebuild();
      }
    });
    btnRow.appendChild(resetBtn);

    this._el.appendChild(btnRow);
  }
}
