/**
 * BottomBar — AoE2-style docked command bar pinned to the bottom of the screen.
 *
 * Four zones, left → right (matches Unity HUD.cs BuildCommandBar order):
 *   1. commandSlot  — action buttons (5×3 grid: build / train / research / garrison / age-up)
 *   2. infoSlot     — selected entity portrait + name + HP + progress
 *   3. centerSlot   — decorative "AGE OF ARENA" emblem (stretches with screen)
 *   4. minimapSlot  — minimap canvas (diamond, rotated 45°)
 *
 * Chrome only — other UI components (HUD, Minimap) mount their content into
 * the exposed slot elements. Always visible during play.
 */
export class BottomBar {
  readonly root: HTMLElement;
  /** Zone 1 (far left): command grid — build/train/research buttons. */
  readonly commandSlot: HTMLElement;
  /** Zone 2: selected entity info — name, HP, progress, queue. */
  readonly infoSlot: HTMLElement;
  /** Zone 3 (flex): decorative centre emblem. */
  readonly centerSlot: HTMLElement;
  /** Zone 4 (far right): minimap canvas. */
  readonly minimapSlot: HTMLElement;

  /** Bar height in CSS pixels — used by callers that need to offset other bottom-anchored UI. */
  static readonly HEIGHT = 160;

  /** Zone widths matching Unity's HUD.cs constants (CmdZoneW=352, LeftW=240, MinW=230). */
  static readonly CMD_W  = 352;
  static readonly INFO_W = 240;
  static readonly MAP_W  = 230;

  constructor(container: HTMLElement) {
    this.root = document.createElement("div");
    Object.assign(this.root.style, {
      position: "absolute", bottom: "0", left: "0",
      width: "100%", height: `${BottomBar.HEIGHT}px`,
      display: "flex", alignItems: "stretch",
      background: "linear-gradient(180deg,#2c2618 0%,#171309 100%)",
      borderTop: "3px solid #6b5a2e",
      boxShadow: "0 -4px 14px rgba(0,0,0,0.55)",
      fontFamily: "monospace", color: "#e8e0c8",
      pointerEvents: "auto", boxSizing: "border-box",
      zIndex: "20",
    });

    // ── Zone 1 (far left): command card ───────────────────────────────────────
    this.commandSlot = document.createElement("div");
    Object.assign(this.commandSlot.style, {
      width: `${BottomBar.CMD_W}px`, flexShrink: "0",
      borderRight: "2px solid #4a3f22",
      padding: "10px 14px", overflowY: "auto",
      fontSize: "13px", color: "#eee",
    });

    // ── Zone 2: info / portrait ───────────────────────────────────────────────
    this.infoSlot = document.createElement("div");
    Object.assign(this.infoSlot.style, {
      width: `${BottomBar.INFO_W}px`, flexShrink: "0",
      borderRight: "2px solid #4a3f22",
      padding: "10px 14px", overflowY: "auto",
      fontSize: "13px", color: "#eee",
      boxShadow: "inset 0 0 16px rgba(0,0,0,0.4)",
    });

    // ── Zone 3 (flex): decorative centre emblem ───────────────────────────────
    this.centerSlot = document.createElement("div");
    Object.assign(this.centerSlot.style, {
      flex: "1", minWidth: "0",
      display: "flex", alignItems: "center", justifyContent: "center",
      pointerEvents: "none",
    });
    const emblem = document.createElement("div");
    Object.assign(emblem.style, {
      fontSize: "22px", fontWeight: "bold",
      color: "#e8d4a0", letterSpacing: "3px",
      textShadow: "0 1px 4px rgba(0,0,0,0.8)",
      opacity: "0.85",
    });
    emblem.textContent = "AGE OF ARENA";
    this.centerSlot.appendChild(emblem);

    // ── Zone 4 (far right): minimap ──────────────────────────────────────────
    this.minimapSlot = document.createElement("div");
    Object.assign(this.minimapSlot.style, {
      width: `${BottomBar.MAP_W}px`, flexShrink: "0",
      borderLeft: "2px solid #4a3f22",
      display: "flex", alignItems: "center", justifyContent: "center",
      boxShadow: "inset 0 0 16px rgba(0,0,0,0.4)",
    });

    this.root.append(this.commandSlot, this.infoSlot, this.centerSlot, this.minimapSlot);
    container.appendChild(this.root);
  }
}
