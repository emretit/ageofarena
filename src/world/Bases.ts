import * as THREE from 'three';
import { GAME_CONFIG } from '../config';
import { addContactShadow } from '../utils/shadows';

export function createBases(scene: THREE.Scene): void {
  for (const base of GAME_CONFIG.bases) {
    addTownCenter(scene, base.center.x, base.center.z, base.teamColor);
  }
}

function addTownCenter(scene: THREE.Scene, x: number, z: number, teamColor: number): void {
  const group = new THREE.Group();

  // Taş platform
  const platMat = new THREE.MeshStandardMaterial({
    color: 0x7a7060, roughness: 0.85, metalness: 0.08,
  });
  const platGeo = new THREE.BoxGeometry(3.6, 0.4, 3.6);
  const plat = new THREE.Mesh(platGeo, platMat);
  plat.position.set(0, 0.2, 0);
  plat.castShadow = true;
  plat.receiveShadow = true;
  group.add(plat);

  // Basamak
  const stepGeo = new THREE.BoxGeometry(4.0, 0.15, 4.0);
  const step = new THREE.Mesh(stepGeo, platMat);
  step.position.set(0, 0.07, 0);
  step.receiveShadow = true;
  group.add(step);

  // Ana yapı gövdesi
  const wallMat = new THREE.MeshStandardMaterial({
    color: 0x9b8b6b, roughness: 0.8, metalness: 0.05,
  });
  const tcGeo = new THREE.BoxGeometry(3, 2, 3);
  const tc = new THREE.Mesh(tcGeo, wallMat);
  tc.position.set(0, 1.4, 0);
  tc.castShadow = true;
  tc.receiveShadow = true;
  group.add(tc);

  // Taş alt bant
  const stoneMat = new THREE.MeshStandardMaterial({
    color: 0x6a6055, roughness: 0.9, metalness: 0.06,
  });
  const stoneGeo = new THREE.BoxGeometry(3.1, 0.5, 3.1);
  const stone = new THREE.Mesh(stoneGeo, stoneMat);
  stone.position.set(0, 0.65, 0);
  group.add(stone);

  // Çatı
  const roofMat = new THREE.MeshStandardMaterial({
    color: teamColor, roughness: 0.65, metalness: 0.08,
  });
  const roofGeo = new THREE.ConeGeometry(2.6, 1.5, 4);
  const roof = new THREE.Mesh(roofGeo, roofMat);
  roof.position.set(0, 3.15, 0);
  roof.rotation.y = Math.PI / 4;
  roof.castShadow = true;
  group.add(roof);

  // Çatı kenar bordürü
  const bordurMat = new THREE.MeshStandardMaterial({
    color: 0x4a3a2a, roughness: 0.8, metalness: 0.05,
  });
  const bordurGeo = new THREE.BoxGeometry(3.2, 0.12, 3.2);
  const bordur = new THREE.Mesh(bordurGeo, bordurMat);
  bordur.position.set(0, 2.44, 0);
  group.add(bordur);

  // Pencereler (4 yüzde)
  const windowMat = new THREE.MeshStandardMaterial({
    color: 0x8ab4d8, roughness: 0.3, metalness: 0.1,
    emissive: 0x1a2a3a, emissiveIntensity: 0.1,
  });
  const frameMat = new THREE.MeshStandardMaterial({
    color: 0x4a3020, roughness: 0.85, metalness: 0.03,
  });

  const windowPositions = [
    { pos: [0, 1.6, -1.52], rot: 0 },
    { pos: [0, 1.6, 1.52], rot: 0 },
    { pos: [-1.52, 1.6, 0], rot: Math.PI / 2 },
    { pos: [1.52, 1.6, 0], rot: Math.PI / 2 },
  ];

  for (const wp of windowPositions) {
    const wGeo = new THREE.BoxGeometry(0.5, 0.6, 0.02);
    const w = new THREE.Mesh(wGeo, windowMat);
    w.position.set(wp.pos[0], wp.pos[1], wp.pos[2]);
    w.rotation.y = wp.rot;
    group.add(w);

    const fGeo = new THREE.BoxGeometry(0.58, 0.68, 0.04);
    const f = new THREE.Mesh(fGeo, frameMat);
    f.position.set(wp.pos[0], wp.pos[1], wp.pos[2]);
    f.rotation.y = wp.rot;
    group.add(f);
  }

  // Ana kapı
  const doorMat = new THREE.MeshStandardMaterial({
    color: 0x5c3a1e, roughness: 0.75, metalness: 0.08,
  });
  const doorGeo = new THREE.BoxGeometry(0.7, 1.2, 0.08);
  const door = new THREE.Mesh(doorGeo, doorMat);
  door.position.set(0, 1.0, -1.52);
  group.add(door);

  // Kapı kemeri
  const archGeo = new THREE.BoxGeometry(0.85, 0.15, 0.12);
  const arch = new THREE.Mesh(archGeo, stoneMat);
  arch.position.set(0, 1.65, -1.52);
  group.add(arch);

  // Köşe sütunları
  for (const sx of [-1.4, 1.4]) {
    for (const sz of [-1.4, 1.4]) {
      const colGeo = new THREE.BoxGeometry(0.25, 2.1, 0.25);
      const col = new THREE.Mesh(colGeo, stoneMat);
      col.position.set(sx, 1.45, sz);
      col.castShadow = true;
      group.add(col);
    }
  }

  // Bayrak direği (çatıda)
  const poleMat = new THREE.MeshStandardMaterial({
    color: 0x4a3a2a, roughness: 0.7, metalness: 0.15,
  });
  const poleGeo = new THREE.CylinderGeometry(0.04, 0.05, 2.0, 4);
  const pole = new THREE.Mesh(poleGeo, poleMat);
  pole.position.set(0, 4.9, 0);
  pole.castShadow = true;
  group.add(pole);

  // Bayrak
  const flagMat = new THREE.MeshStandardMaterial({
    color: teamColor, roughness: 0.5, metalness: 0.05,
    side: THREE.DoubleSide,
  });
  const flagGeo = new THREE.PlaneGeometry(0.9, 0.6);
  const flag = new THREE.Mesh(flagGeo, flagMat);
  flag.position.set(0.45, 5.5, 0);
  group.add(flag);

  // Contact shadow
  addContactShadow(group, 0, 0, 2.5, 2.5, 0.2);

  group.position.set(x, 0, z);
  scene.add(group);
}
