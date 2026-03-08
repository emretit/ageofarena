import * as THREE from 'three';
import { GAME_CONFIG, BaseConfig } from '../config';
import { addContactShadow } from '../utils/shadows';

interface BuildingPlacement {
  x: number;
  z: number;
  type: 'house' | 'barracks';
}

function getBuildingsForBase(base: BaseConfig): BuildingPlacement[] {
  const cx = base.center.x;
  const cz = base.center.z;

  return [
    { x: cx - 5, z: cz - 4, type: 'house' },
    { x: cx + 5, z: cz - 4, type: 'house' },
    { x: cx - 5, z: cz + 4, type: 'house' },
    { x: cx + 5, z: cz + 4, type: 'house' },
    { x: cx, z: cz - 6, type: 'barracks' },
  ];
}

export function createBaseBuildings(scene: THREE.Scene): void {
  for (const base of GAME_CONFIG.bases) {
    const buildings = getBuildingsForBase(base);
    for (const b of buildings) {
      if (b.type === 'house') addHouse(scene, b.x, b.z, base.roofColor);
      else addBarracks(scene, b.x, b.z, base.roofColor);
    }
  }
}

function addHouse(scene: THREE.Scene, x: number, z: number, roofColor: number): void {
  const group = new THREE.Group();

  const foundationMat = new THREE.MeshStandardMaterial({
    color: 0x7a7060, roughness: 0.9, metalness: 0.05,
  });
  const foundGeo = new THREE.BoxGeometry(1.7, 0.3, 1.7);
  const found = new THREE.Mesh(foundGeo, foundationMat);
  found.position.set(0, 0.15, 0);
  found.castShadow = true;
  found.receiveShadow = true;
  group.add(found);

  const wallMat = new THREE.MeshStandardMaterial({
    color: 0xc8b898, roughness: 0.8, metalness: 0.02,
  });
  const baseGeo = new THREE.BoxGeometry(1.5, 1.5, 1.5);
  const base = new THREE.Mesh(baseGeo, wallMat);
  base.position.set(0, 1.05, 0);
  base.castShadow = true;
  base.receiveShadow = true;
  group.add(base);

  const timberMat = new THREE.MeshStandardMaterial({
    color: 0x4a3020, roughness: 0.85, metalness: 0.03,
  });

  for (const sx of [-0.7, 0.7]) {
    for (const sz of [-0.7, 0.7]) {
      const tGeo = new THREE.BoxGeometry(0.08, 1.5, 0.08);
      const t = new THREE.Mesh(tGeo, timberMat);
      t.position.set(sx, 1.05, sz);
      group.add(t);
    }
  }

  const hGeo = new THREE.BoxGeometry(1.55, 0.06, 0.06);
  for (const sz of [-0.72, 0.72]) {
    const h = new THREE.Mesh(hGeo, timberMat);
    h.position.set(0, 1.2, sz);
    group.add(h);
  }

  const windowMat = new THREE.MeshStandardMaterial({
    color: 0x8ab4d8, roughness: 0.3, metalness: 0.1,
    emissive: 0x1a2a3a, emissiveIntensity: 0.25,
  });
  for (const sz of [-0.76, 0.76]) {
    const wGeo = new THREE.BoxGeometry(0.3, 0.35, 0.02);
    const w = new THREE.Mesh(wGeo, windowMat);
    w.position.set(0, 1.2, sz);
    group.add(w);

    const fGeo = new THREE.BoxGeometry(0.36, 0.41, 0.03);
    const f = new THREE.Mesh(fGeo, timberMat);
    f.position.set(0, 1.2, sz * 0.99);
    group.add(f);

    const innerGeo = new THREE.BoxGeometry(0.28, 0.33, 0.035);
    const inner = new THREE.Mesh(innerGeo, windowMat);
    inner.position.set(0, 1.2, sz * 0.98);
    group.add(inner);
  }

  const doorMat = new THREE.MeshStandardMaterial({
    color: 0x5c3a1e, roughness: 0.8, metalness: 0.05,
  });
  const doorGeo = new THREE.BoxGeometry(0.4, 0.7, 0.06);
  const door = new THREE.Mesh(doorGeo, doorMat);
  door.position.set(0.3, 0.65, -0.76);
  group.add(door);

  const roofMat = new THREE.MeshStandardMaterial({
    color: roofColor, roughness: 0.7, metalness: 0.05,
  });
  const roofGeo = new THREE.ConeGeometry(1.25, 1.0, 4);
  const roof = new THREE.Mesh(roofGeo, roofMat);
  roof.position.set(0, 2.3, 0);
  roof.rotation.y = Math.PI / 4;
  roof.castShadow = true;
  group.add(roof);

  const chimGeo = new THREE.BoxGeometry(0.2, 0.6, 0.2);
  const chimMat = new THREE.MeshStandardMaterial({
    color: 0x6a5a4a, roughness: 0.9, metalness: 0.05,
  });
  const chim = new THREE.Mesh(chimGeo, chimMat);
  chim.position.set(0.4, 2.5, 0.3);
  chim.castShadow = true;
  group.add(chim);

  const chimTopGeo = new THREE.BoxGeometry(0.28, 0.08, 0.28);
  const chimTop = new THREE.Mesh(chimTopGeo, chimMat);
  chimTop.position.set(0.4, 2.84, 0.3);
  group.add(chimTop);

  // Roof ridge beam
  const ridgeMat = new THREE.MeshStandardMaterial({ color: 0x4a3020, roughness: 0.85 });
  const ridge = new THREE.Mesh(new THREE.BoxGeometry(1.8, 0.06, 0.06), ridgeMat);
  ridge.position.set(0, 2.8, 0);
  ridge.rotation.y = Math.PI / 4;
  group.add(ridge);

  // Wood pile next to house
  const woodMat = new THREE.MeshStandardMaterial({ color: 0x5c3a1e, roughness: 0.88 });
  for (let row = 0; row < 2; row++) {
    for (let li = 0; li < 3 - row; li++) {
      const logGeo = new THREE.CylinderGeometry(0.06, 0.06, 0.6, 4);
      const log = new THREE.Mesh(logGeo, woodMat);
      log.rotation.x = Math.PI / 2;
      log.position.set(-1.1 + li * 0.14, 0.06 + row * 0.12, 0.5);
      log.castShadow = true;
      group.add(log);
    }
  }

  // Doorstep stone
  const stepMat = new THREE.MeshStandardMaterial({ color: 0x7a7060, roughness: 0.92 });
  const doorstep = new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.08, 0.2), stepMat);
  doorstep.position.set(0.3, 0.04, -0.86);
  group.add(doorstep);

  // Contact shadow
  addContactShadow(group, 0, 0, 1.3, 1.3, 0.15);

  group.position.set(x, 0, z);
  scene.add(group);
}

function addBarracks(scene: THREE.Scene, x: number, z: number, roofColor: number): void {
  const group = new THREE.Group();

  const foundMat = new THREE.MeshStandardMaterial({
    color: 0x6a6050, roughness: 0.9, metalness: 0.08,
  });
  const foundGeo = new THREE.BoxGeometry(2.7, 0.3, 2.2);
  const found = new THREE.Mesh(foundGeo, foundMat);
  found.position.set(0, 0.15, 0);
  found.castShadow = true;
  found.receiveShadow = true;
  group.add(found);

  const wallMat = new THREE.MeshStandardMaterial({
    color: 0x8a7a6a, roughness: 0.85, metalness: 0.05,
  });
  const baseGeo = new THREE.BoxGeometry(2.5, 2.0, 2.0);
  const base = new THREE.Mesh(baseGeo, wallMat);
  base.position.set(0, 1.3, 0);
  base.castShadow = true;
  base.receiveShadow = true;
  group.add(base);

  const stoneBandMat = new THREE.MeshStandardMaterial({
    color: 0x6a6055, roughness: 0.9, metalness: 0.06,
  });
  const bandGeo = new THREE.BoxGeometry(2.55, 0.4, 2.05);
  const band = new THREE.Mesh(bandGeo, stoneBandMat);
  band.position.set(0, 0.5, 0);
  group.add(band);

  const roofMat = new THREE.MeshStandardMaterial({
    color: roofColor, roughness: 0.65, metalness: 0.08,
  });
  const roofGeo = new THREE.BoxGeometry(2.7, 0.3, 2.2);
  const roof = new THREE.Mesh(roofGeo, roofMat);
  roof.position.set(0, 2.45, 0);
  roof.castShadow = true;
  group.add(roof);

  const edgeMat = new THREE.MeshStandardMaterial({
    color: 0x4a3a2a, roughness: 0.8, metalness: 0.05,
  });
  const edgeGeo = new THREE.BoxGeometry(2.8, 0.1, 2.3);
  const edge = new THREE.Mesh(edgeGeo, edgeMat);
  edge.position.set(0, 2.35, 0);
  group.add(edge);

  const doorMat = new THREE.MeshStandardMaterial({
    color: 0x5c3a1e, roughness: 0.8, metalness: 0.05,
  });
  const doorGeo = new THREE.BoxGeometry(0.8, 1.4, 0.06);
  const door = new THREE.Mesh(doorGeo, doorMat);
  door.position.set(0, 1.0, -1.03);
  group.add(door);

  const archGeo = new THREE.BoxGeometry(0.9, 0.15, 0.1);
  const arch = new THREE.Mesh(archGeo, stoneBandMat);
  arch.position.set(0, 1.75, -1.03);
  group.add(arch);

  const windowMat = new THREE.MeshStandardMaterial({
    color: 0x3a3a3a, roughness: 0.5, metalness: 0.2,
  });
  for (const sx of [-0.7, 0.7]) {
    const wGeo = new THREE.BoxGeometry(0.25, 0.5, 0.06);
    const w = new THREE.Mesh(wGeo, windowMat);
    w.position.set(sx, 1.5, -1.03);
    group.add(w);
  }

  const poleMat = new THREE.MeshStandardMaterial({
    color: 0x4a3a2a, roughness: 0.7, metalness: 0.1,
  });
  const poleGeo = new THREE.CylinderGeometry(0.04, 0.05, 1.8, 4);
  const pole = new THREE.Mesh(poleGeo, poleMat);
  pole.position.set(1.1, 3.25, 0);
  pole.castShadow = true;
  group.add(pole);

  const flagMat = new THREE.MeshStandardMaterial({
    color: roofColor, roughness: 0.6, metalness: 0.05,
    side: THREE.DoubleSide,
  });
  const flagGeo = new THREE.PlaneGeometry(0.7, 0.45);
  const flag = new THREE.Mesh(flagGeo, flagMat);
  flag.position.set(1.45, 3.9, 0);
  group.add(flag);

  const rackMat = new THREE.MeshStandardMaterial({
    color: 0x3a2a1a, roughness: 0.8, metalness: 0.1,
  });
  const rackGeo = new THREE.BoxGeometry(0.8, 0.06, 0.1);
  const rack = new THREE.Mesh(rackGeo, rackMat);
  rack.position.set(-0.8, 1.3, 1.03);
  group.add(rack);

  for (let i = 0; i < 3; i++) {
    const spearGeo = new THREE.CylinderGeometry(0.015, 0.015, 1.2, 3);
    const spear = new THREE.Mesh(spearGeo, rackMat);
    spear.position.set(-0.95 + i * 0.15, 1.9, 1.0);
    group.add(spear);
  }

  // Contact shadow
  addContactShadow(group, 0, 0, 1.8, 1.5, 0.18);

  group.position.set(x, 0, z);
  scene.add(group);
}
