/**
 * Lighting.ts — improved scene lighting for the web RTS.
 * Replaces the inline DirectionalLight+AmbientLight in World.ts:
 *   - HemisphereLight (sky/ground gradient ambient)
 *   - Directional sun with tight shadow frustum (120u)
 *   - Renderer tone-mapping set to ACES Filmic (must call applyToneMapping)
 */
import * as THREE from "three";

export function buildLighting(scene: THREE.Scene, renderer: THREE.WebGLRenderer): void {
  // Sky/ground gradient replaces flat AmbientLight
  const hemi = new THREE.HemisphereLight(
    0xbfd4ff, // sky — pale blue
    0x7d6a3c, // ground — warm brown
    1.2,
  );
  scene.add(hemi);

  // Sun — warm afternoon slant, tight frustum for crisp isometric shadows
  const sun = new THREE.DirectionalLight(0xfff2dd, 2.4);
  sun.position.set(-60, 90, 40);
  sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  sun.shadow.bias = -0.002;
  const sh = 110;
  sun.shadow.camera.left   = -sh;
  sun.shadow.camera.right  =  sh;
  sun.shadow.camera.top    =  sh;
  sun.shadow.camera.bottom = -sh;
  sun.shadow.camera.near   = 1;
  sun.shadow.camera.far    = 400;
  scene.add(sun);

  // ACES Filmic tone mapping — dramatically improves color + contrast vs Linear
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.0;
  renderer.outputColorSpace = THREE.SRGBColorSpace;
}
