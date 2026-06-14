/**
 * BottomBar — AoE2-style docked command bar pinned to the bottom of the screen.
 *
 * Three zones, left → right:
 *   • infoSlot    — selected entity portrait + stats
 *   • commandSlot — action buttons (build / train / research / market / garrison / age-up)
 *   • minimapSlot — minimap canvas
 *
 * The bar is just chrome: other UI components (HUD, Minimap) mount their content into
 * the exposed slot elements. Always visible during play; slots empty when nothing is selected.
 */
export class BottomBar {
  readonly root: HTMLElement;
  readonly infoSlot: HTMLElement;
  readonly commandSlot: HTMLElement;
  readonly minimapSlot: HTMLElement;

  /** Bar height in CSS pixels — used by callers that need to offset other bottom-anchored UI. */
  static readonly HEIGHT = 156;

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

    // ── Left: info / portrait ──────────────────────────────────────────────
    this.infoSlot = document.createElement("div");
    Object.assign(this.infoSlot.style, {
      width: "320px", flexShrink: "0",
      borderRight: "2px solid #4a3f22",
      padding: "10px 14px", overflowY: "auto",
      fontSize: "13px", color: "#eee",
      boxShadow: "inset 0 0 16px rgba(0,0,0,0.4)",
    });

    // ── Center: command card ───────────────────────────────────────────────
    this.commandSlot = document.createElement("div");
    Object.assign(this.commandSlot.style, {
      flex: "1", minWidth: "0",
      padding: "10px 14px", overflowY: "auto",
      fontSize: "13px", color: "#eee",
    });

    // ── Right: minimap ─────────────────────────────────────────────────────
    this.minimapSlot = document.createElement("div");
    Object.assign(this.minimapSlot.style, {
      width: "168px", flexShrink: "0",
      borderLeft: "2px solid #4a3f22",
      display: "flex", alignItems: "center", justifyContent: "center",
      boxShadow: "inset 0 0 16px rgba(0,0,0,0.4)",
    });

    this.root.append(this.infoSlot, this.commandSlot, this.minimapSlot);
    container.appendChild(this.root);
  }
}
