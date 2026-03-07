import * as THREE from 'three';
import { GAME_CONFIG } from '../config';

export function createGround(scene: THREE.Scene, mapW: number, mapH: number): void {
  const { colors, tileSize, mapTilesW, mapTilesH } = GAME_CONFIG;

  // Ana zemin
  const groundGeo = new THREE.PlaneGeometry(mapW, mapH);
  const groundMat = new THREE.MeshLambertMaterial({ color: colors.grass });
  const ground = new THREE.Mesh(groundGeo, groundMat);
  ground.rotation.x = -Math.PI / 2;
  ground.position.set(mapW / 2, 0, mapH / 2);
  ground.receiveShadow = true;
  scene.add(ground);

  // Dama tahtası deseni (AoE2 tarzı zemin varyasyonu)
  for (let x = 0; x < mapTilesW; x++) {
    for (let z = 0; z < mapTilesH; z++) {
      if ((x + z) % 2 === 0) continue;
      const tileGeo = new THREE.PlaneGeometry(tileSize, tileSize);
      const tileMat = new THREE.MeshLambertMaterial({ color: colors.grassDark });
      const tile = new THREE.Mesh(tileGeo, tileMat);
      tile.rotation.x = -Math.PI / 2;
      tile.position.set(
        x * tileSize + tileSize / 2,
        0.01,
        z * tileSize + tileSize / 2
      );
      tile.receiveShadow = true;
      scene.add(tile);
    }
  }

  // Grid çizgileri
  const gridMat = new THREE.LineBasicMaterial({ color: colors.gridLine, transparent: true, opacity: 0.3 });

  const gridPoints: THREE.Vector3[] = [];
  for (let x = 0; x <= mapTilesW; x++) {
    gridPoints.push(new THREE.Vector3(x * tileSize, 0.02, 0));
    gridPoints.push(new THREE.Vector3(x * tileSize, 0.02, mapH));
  }
  for (let z = 0; z <= mapTilesH; z++) {
    gridPoints.push(new THREE.Vector3(0, 0.02, z * tileSize));
    gridPoints.push(new THREE.Vector3(mapW, 0.02, z * tileSize));
  }

  const gridGeo = new THREE.BufferGeometry().setFromPoints(gridPoints);
  const grid = new THREE.LineSegments(gridGeo, gridMat);
  scene.add(grid);
}
