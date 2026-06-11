/**
 * Game constants — ported from the Unity build (WorldRoot.cs / IsometricCameraRig.cs)
 * so the web version feels identical. Keep names aligned with the C# source.
 *
 * Deliberate divergences from Unity (do NOT "fix" back):
 *  - CameraBounds 95 (Unity 60): web shows the whole island; Unity clamps tighter.
 */
export const Config = {
  // ── Arena island (WorldRoot) ──
  LandRadius: 92,        // circular land disc radius
  CoastInner: 76,        // forest belt starts here
  ForestOuter: 91,       // forest belt ends here
  OceanHalf: 220,        // ocean plane half-extent
  BaseDistance: 84,      // 4 cardinal base pockets sit this far from centre

  // ── Camera (IsometricCameraRig) ──
  PanSpeed: 25,
  ZoomSpeed: 4,
  MinZoom: 6,
  MaxZoom: 30,
  DefaultZoom: 18,       // starting ortho half-height; also pan-speed reference
  CameraYaw: Math.PI / 4,// isometric yaw — pan axes and camera offset share this
  EdgeMargin: 10,        // px from screen edge that triggers edge-scroll
  CameraBounds: 95,      // pannable half-extents (web: full island visible; Unity 60)

  // ── Spawns ──
  BaseSpawnZ: -58,       // player base centre (camera start focus + unit spawn line)

  // ── Sim ──
  FixedStep: 1 / 30,     // N3.fixedstep: 30 Hz simulation tick
} as const;

/** Team palette — TeamPalette.For() equivalent. */
export const TeamColors = [0x3366e6, 0xcc3333, 0x33aa44, 0xddaa22] as const;
