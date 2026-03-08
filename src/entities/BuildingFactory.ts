import * as THREE from 'three';
import { BuildingId } from './types';
import { addContactShadow } from '../utils/shadows';

// Shared materials
const stoneMat = new THREE.MeshStandardMaterial({ color: 0x7a7060, roughness: 0.9, metalness: 0.05 });
const wallMat = new THREE.MeshStandardMaterial({ color: 0xc8b898, roughness: 0.8, metalness: 0.02 });
const darkWallMat = new THREE.MeshStandardMaterial({ color: 0x8a7a6a, roughness: 0.85, metalness: 0.05 });
const timberMat = new THREE.MeshStandardMaterial({ color: 0x4a3020, roughness: 0.85, metalness: 0.03 });
const doorMat = new THREE.MeshStandardMaterial({ color: 0x5c3a1e, roughness: 0.8, metalness: 0.05 });
const windowMat = new THREE.MeshStandardMaterial({ color: 0x8ab4d8, roughness: 0.3, metalness: 0.1, emissive: 0x1a2a3a, emissiveIntensity: 0.25 });
const metalMat = new THREE.MeshStandardMaterial({ color: 0x555555, roughness: 0.4, metalness: 0.7 });
const hayMat = new THREE.MeshStandardMaterial({ color: 0xc4a840, roughness: 0.95, metalness: 0.0 });


export function createBuildingMesh(buildingId: BuildingId, roofColor: number, teamColor: number): THREE.Group {
  switch (buildingId) {
    case 'townCenter': return createTownCenter(roofColor, teamColor);
    case 'house': return createHouse(roofColor);
    case 'barracks': return createBarracks(roofColor);
    case 'archeryRange': return createArcheryRange(roofColor);
    case 'stable': return createStable(roofColor);
    case 'blacksmith': return createBlacksmith(roofColor);
    case 'market': return createMarket(roofColor);
    case 'castle': return createCastle(roofColor, teamColor);
    default: return new THREE.Group();
  }
}

function createArcheryRange(roofColor: number): THREE.Group {
  const group = new THREE.Group();
  const roofMat = new THREE.MeshStandardMaterial({ color: roofColor, roughness: 0.65, metalness: 0.08 });

  // Foundation
  const found = new THREE.Mesh(new THREE.BoxGeometry(2.8, 0.25, 2.2), stoneMat);
  found.position.set(0, 0.125, 0);
  found.castShadow = true; found.receiveShadow = true;
  group.add(found);

  // Open-sided structure (3 walls, front open)
  // Back wall
  const backWall = new THREE.Mesh(new THREE.BoxGeometry(2.6, 1.8, 0.15), wallMat);
  backWall.position.set(0, 1.15, 1.0);
  backWall.castShadow = true; backWall.receiveShadow = true;
  group.add(backWall);

  // Side walls (shorter, half-height)
  for (const sx of [-1.25, 1.25]) {
    const sideWall = new THREE.Mesh(new THREE.BoxGeometry(0.15, 1.8, 2.0), wallMat);
    sideWall.position.set(sx, 1.15, 0);
    sideWall.castShadow = true;
    group.add(sideWall);
  }

  // Timber pillars at front
  for (const sx of [-1.2, 1.2]) {
    const pillar = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.08, 1.8, 6), timberMat);
    pillar.position.set(sx, 1.15, -0.95);
    pillar.castShadow = true;
    group.add(pillar);
  }

  // Slanted roof
  const roofGeo = new THREE.BoxGeometry(2.9, 0.12, 2.5);
  const roof = new THREE.Mesh(roofGeo, roofMat);
  roof.position.set(0, 2.15, 0.05);
  roof.rotation.x = 0.12;
  roof.castShadow = true;
  group.add(roof);

  // Targets (archery targets at back wall)
  const targetMat = new THREE.MeshStandardMaterial({ color: 0xcc3333, roughness: 0.8 });
  const targetWhiteMat = new THREE.MeshStandardMaterial({ color: 0xeeeecc, roughness: 0.8 });
  for (const sx of [-0.6, 0.6]) {
    // Target board
    const board = new THREE.Mesh(new THREE.CylinderGeometry(0.3, 0.3, 0.06, 12), targetWhiteMat);
    board.rotation.x = Math.PI / 2;
    board.position.set(sx, 1.2, 0.9);
    group.add(board);
    // Red center
    const center = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.12, 0.07, 8), targetMat);
    center.rotation.x = Math.PI / 2;
    center.position.set(sx, 1.2, 0.89);
    group.add(center);
  }

  // Quiver with arrows
  const quiverMat = new THREE.MeshStandardMaterial({ color: 0x6a4a2a, roughness: 0.85 });
  const quiver = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.08, 0.5, 6), quiverMat);
  quiver.position.set(-0.9, 0.5, -0.7);
  group.add(quiver);
  // Arrow shafts
  for (let i = 0; i < 4; i++) {
    const arrow = new THREE.Mesh(new THREE.CylinderGeometry(0.008, 0.008, 0.7, 3), timberMat);
    arrow.position.set(-0.9 + (i - 1.5) * 0.02, 0.6, -0.7);
    group.add(arrow);
  }

  addContactShadow(group, 0, 0, 1.8, 1.4, 0.16);
  return group;
}

function createStable(roofColor: number): THREE.Group {
  const group = new THREE.Group();
  const roofMat = new THREE.MeshStandardMaterial({ color: roofColor, roughness: 0.65, metalness: 0.08 });

  // Foundation
  const found = new THREE.Mesh(new THREE.BoxGeometry(3.0, 0.25, 2.4), stoneMat);
  found.position.set(0, 0.125, 0);
  found.castShadow = true; found.receiveShadow = true;
  group.add(found);

  // Main barn structure
  const barn = new THREE.Mesh(new THREE.BoxGeometry(2.8, 1.8, 2.2), wallMat);
  barn.position.set(0, 1.15, 0);
  barn.castShadow = true; barn.receiveShadow = true;
  group.add(barn);

  // Timber frame
  for (const sx of [-1.35, 1.35]) {
    for (const sz of [-1.05, 1.05]) {
      const beam = new THREE.Mesh(new THREE.BoxGeometry(0.1, 1.8, 0.1), timberMat);
      beam.position.set(sx, 1.15, sz);
      group.add(beam);
    }
  }

  // Triangular roof (A-frame)
  const roofGeo = new THREE.BoxGeometry(3.0, 0.12, 1.5);
  for (const side of [-1, 1]) {
    const roofPanel = new THREE.Mesh(roofGeo, roofMat);
    roofPanel.position.set(0, 2.3, side * 0.5);
    roofPanel.rotation.x = side * 0.45;
    roofPanel.castShadow = true;
    group.add(roofPanel);
  }

  // Ridge beam
  const ridge = new THREE.Mesh(new THREE.BoxGeometry(3.1, 0.08, 0.08), timberMat);
  ridge.position.set(0, 2.65, 0);
  group.add(ridge);

  // Large barn door
  const barnDoor = new THREE.Mesh(new THREE.BoxGeometry(1.0, 1.4, 0.08), doorMat);
  barnDoor.position.set(0, 0.95, -1.12);
  group.add(barnDoor);

  // Door arch
  const arch = new THREE.Mesh(new THREE.BoxGeometry(1.15, 0.15, 0.12), stoneMat);
  arch.position.set(0, 1.7, -1.12);
  group.add(arch);

  // Hay bales inside (visible through door)
  for (let i = 0; i < 2; i++) {
    const hay = new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.3, 0.4), hayMat);
    hay.position.set(0.8, 0.4 + i * 0.3, 0.5);
    group.add(hay);
  }

  // Horse hitching post
  const post = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.05, 1.0, 4), timberMat);
  post.position.set(-1.6, 0.5, -0.5);
  post.castShadow = true;
  group.add(post);
  const rail = new THREE.Mesh(new THREE.BoxGeometry(1.0, 0.05, 0.05), timberMat);
  rail.position.set(-1.6, 0.9, 0);
  rail.rotation.y = Math.PI / 2;
  group.add(rail);

  // Water trough
  const troughMat = new THREE.MeshStandardMaterial({ color: 0x5a4a3a, roughness: 0.9 });
  const trough = new THREE.Mesh(new THREE.BoxGeometry(0.7, 0.25, 0.35), troughMat);
  trough.position.set(-1.6, 0.2, 0.5);
  group.add(trough);
  const waterMat = new THREE.MeshStandardMaterial({ color: 0x4a7acc, roughness: 0.2, metalness: 0.3 });
  const water = new THREE.Mesh(new THREE.BoxGeometry(0.6, 0.08, 0.25), waterMat);
  water.position.set(-1.6, 0.3, 0.5);
  group.add(water);

  addContactShadow(group, 0, 0, 2.0, 1.6, 0.17);
  return group;
}

function createBlacksmith(roofColor: number): THREE.Group {
  const group = new THREE.Group();
  const roofMat = new THREE.MeshStandardMaterial({ color: roofColor, roughness: 0.65, metalness: 0.08 });

  // Foundation
  const found = new THREE.Mesh(new THREE.BoxGeometry(2.4, 0.25, 2.2), stoneMat);
  found.position.set(0, 0.125, 0);
  found.castShadow = true; found.receiveShadow = true;
  group.add(found);

  // Main structure (stone, darker)
  const body = new THREE.Mesh(new THREE.BoxGeometry(2.2, 1.6, 2.0), darkWallMat);
  body.position.set(0, 1.05, 0);
  body.castShadow = true; body.receiveShadow = true;
  group.add(body);

  // Chimney (large, prominent)
  const chimney = new THREE.Mesh(new THREE.BoxGeometry(0.6, 1.8, 0.6), stoneMat);
  chimney.position.set(0.6, 2.0, 0.5);
  chimney.castShadow = true;
  group.add(chimney);

  const chimTop = new THREE.Mesh(new THREE.BoxGeometry(0.7, 0.1, 0.7), stoneMat);
  chimTop.position.set(0.6, 2.95, 0.5);
  group.add(chimTop);

  // Smoke particles hint (emissive spot)
  const smokeMat = new THREE.MeshStandardMaterial({
    color: 0xff4400, emissive: 0xff2200, emissiveIntensity: 0.5,
    roughness: 0.9,
  });
  const ember = new THREE.Mesh(new THREE.BoxGeometry(0.35, 0.08, 0.35), smokeMat);
  ember.position.set(0.6, 1.1, 0.5);
  group.add(ember);

  // Roof (flat, one side)
  const roof = new THREE.Mesh(new THREE.BoxGeometry(2.5, 0.15, 2.3), roofMat);
  roof.position.set(0, 1.95, 0);
  roof.rotation.x = 0.08;
  roof.castShadow = true;
  group.add(roof);

  // Anvil
  const anvilBase = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.25, 0.2), metalMat);
  anvilBase.position.set(-0.7, 0.37, -1.1);
  anvilBase.castShadow = true;
  group.add(anvilBase);
  const anvilTop = new THREE.Mesh(new THREE.BoxGeometry(0.4, 0.08, 0.25), metalMat);
  anvilTop.position.set(-0.7, 0.53, -1.1);
  group.add(anvilTop);

  // Weapon rack outside
  const rack = new THREE.Mesh(new THREE.BoxGeometry(0.8, 0.06, 0.1), timberMat);
  rack.position.set(0.3, 1.0, -1.05);
  group.add(rack);
  // Swords on rack
  for (let i = 0; i < 3; i++) {
    const sword = new THREE.Mesh(new THREE.BoxGeometry(0.03, 0.5, 0.02), metalMat);
    sword.position.set(0.1 + i * 0.15, 1.25, -1.04);
    group.add(sword);
  }

  // Door
  const door = new THREE.Mesh(new THREE.BoxGeometry(0.6, 1.2, 0.06), doorMat);
  door.position.set(-0.4, 0.85, -1.02);
  group.add(door);

  // Window with glow
  const win = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.3, 0.06), windowMat);
  win.position.set(0.5, 1.3, -1.02);
  group.add(win);

  addContactShadow(group, 0, 0, 1.6, 1.4, 0.18);
  return group;
}

function createMarket(roofColor: number): THREE.Group {
  const group = new THREE.Group();

  // Foundation (stone platform)
  const found = new THREE.Mesh(new THREE.BoxGeometry(3.0, 0.2, 2.4), stoneMat);
  found.position.set(0, 0.1, 0);
  found.castShadow = true; found.receiveShadow = true;
  group.add(found);

  // Open stall - 4 wooden pillars
  for (const sx of [-1.2, 1.2]) {
    for (const sz of [-0.9, 0.9]) {
      const pillar = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.08, 2.0, 6), timberMat);
      pillar.position.set(sx, 1.2, sz);
      pillar.castShadow = true;
      group.add(pillar);
    }
  }

  // Cloth awning roof (colorful)
  const awning1Mat = new THREE.MeshStandardMaterial({ color: roofColor, roughness: 0.7, side: THREE.DoubleSide });
  const awning = new THREE.Mesh(new THREE.BoxGeometry(2.8, 0.06, 2.2), awning1Mat);
  awning.position.set(0, 2.2, 0);
  awning.castShadow = true;
  group.add(awning);

  // Striped cloth banner
  const stripeMat = new THREE.MeshStandardMaterial({ color: 0xcc8833, roughness: 0.7, side: THREE.DoubleSide });
  for (let i = 0; i < 3; i++) {
    const stripe = new THREE.Mesh(new THREE.PlaneGeometry(0.3, 2.2), stripeMat);
    stripe.position.set(-0.8 + i * 0.8, 2.22, 0);
    stripe.rotation.x = -Math.PI / 2;
    group.add(stripe);
  }

  // Counter/table
  const counterMat = new THREE.MeshStandardMaterial({ color: 0x6a5030, roughness: 0.85 });
  const counter = new THREE.Mesh(new THREE.BoxGeometry(2.0, 0.1, 0.6), counterMat);
  counter.position.set(0, 0.85, -0.5);
  group.add(counter);
  // Counter legs
  for (const sx of [-0.8, 0.8]) {
    const leg = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.65, 0.08), timberMat);
    leg.position.set(sx, 0.52, -0.5);
    group.add(leg);
  }

  // Goods on counter (boxes, sacks)
  const sackMat = new THREE.MeshStandardMaterial({ color: 0x9a8a5a, roughness: 0.95 });
  const sack1 = new THREE.Mesh(new THREE.SphereGeometry(0.15, 6, 4), sackMat);
  sack1.position.set(-0.3, 1.05, -0.5);
  sack1.scale.set(1, 0.7, 1);
  group.add(sack1);

  const boxMat = new THREE.MeshStandardMaterial({ color: 0x7a5a30, roughness: 0.88 });
  const box1 = new THREE.Mesh(new THREE.BoxGeometry(0.25, 0.2, 0.2), boxMat);
  box1.position.set(0.3, 1.0, -0.5);
  group.add(box1);

  const box2 = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.15, 0.2), boxMat);
  box2.position.set(0.6, 0.97, -0.45);
  group.add(box2);

  // Back shelf with goods
  const shelf = new THREE.Mesh(new THREE.BoxGeometry(1.5, 0.06, 0.3), counterMat);
  shelf.position.set(0, 1.2, 0.7);
  group.add(shelf);

  // Gold/coin pile hint
  const goldMat = new THREE.MeshStandardMaterial({ color: 0xdaa520, roughness: 0.3, metalness: 0.6 });
  const coins = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.12, 0.05, 8), goldMat);
  coins.position.set(0, 1.25, 0.7);
  group.add(coins);

  addContactShadow(group, 0, 0, 2.0, 1.6, 0.15);
  return group;
}

function createCastle(roofColor: number, teamColor: number): THREE.Group {
  const group = new THREE.Group();
  const castleStoneMat = new THREE.MeshStandardMaterial({ color: 0x8a8378, roughness: 0.88, metalness: 0.06 });
  const darkStoneMat = new THREE.MeshStandardMaterial({ color: 0x6b6560, roughness: 0.9, metalness: 0.08 });
  const roofMat = new THREE.MeshStandardMaterial({ color: roofColor, roughness: 0.65, metalness: 0.08 });

  // Large foundation
  const found = new THREE.Mesh(new THREE.BoxGeometry(4.5, 0.4, 4.5), darkStoneMat);
  found.position.set(0, 0.2, 0);
  found.castShadow = true; found.receiveShadow = true;
  group.add(found);

  // Main keep (center tower)
  const keep = new THREE.Mesh(new THREE.BoxGeometry(2.5, 3.5, 2.5), castleStoneMat);
  keep.position.set(0, 2.15, 0);
  keep.castShadow = true; keep.receiveShadow = true;
  group.add(keep);

  // Keep roof
  const keepRoof = new THREE.Mesh(new THREE.ConeGeometry(2.0, 1.2, 4), roofMat);
  keepRoof.position.set(0, 4.5, 0);
  keepRoof.rotation.y = Math.PI / 4;
  keepRoof.castShadow = true;
  group.add(keepRoof);

  // 4 corner towers
  for (const sx of [-1.8, 1.8]) {
    for (const sz of [-1.8, 1.8]) {
      // Tower body (cylinder)
      const tower = new THREE.Mesh(new THREE.CylinderGeometry(0.5, 0.55, 3.0, 8), castleStoneMat);
      tower.position.set(sx, 1.9, sz);
      tower.castShadow = true;
      group.add(tower);

      // Tower roof (cone)
      const towerRoof = new THREE.Mesh(new THREE.ConeGeometry(0.6, 0.8, 8), roofMat);
      towerRoof.position.set(sx, 3.8, sz);
      towerRoof.castShadow = true;
      group.add(towerRoof);

      // Battlements on tower
      for (let a = 0; a < 4; a++) {
        const angle = (a / 4) * Math.PI * 2;
        const bx = sx + Math.cos(angle) * 0.45;
        const bz = sz + Math.sin(angle) * 0.45;
        const merlon = new THREE.Mesh(new THREE.BoxGeometry(0.15, 0.25, 0.15), castleStoneMat);
        merlon.position.set(bx, 3.55, bz);
        group.add(merlon);
      }
    }
  }

  // Connecting walls between towers
  for (const pos of [
    { x: 0, z: -1.8, sx: 3.0, sz: 0.2 },
    { x: 0, z: 1.8, sx: 3.0, sz: 0.2 },
    { x: -1.8, z: 0, sx: 0.2, sz: 3.0 },
    { x: 1.8, z: 0, sx: 0.2, sz: 3.0 },
  ]) {
    const wall = new THREE.Mesh(new THREE.BoxGeometry(pos.sx, 2.2, pos.sz), castleStoneMat);
    wall.position.set(pos.x, 1.5, pos.z);
    wall.castShadow = true;
    group.add(wall);

    // Battlements on walls
    const isXWall = pos.sz < 0.5;
    const count = isXWall ? 5 : 5;
    for (let m = 0; m < count; m++) {
      const merlon = new THREE.Mesh(new THREE.BoxGeometry(0.12, 0.2, 0.12), castleStoneMat);
      if (isXWall) {
        merlon.position.set(-1.2 + m * 0.6, 2.7, pos.z);
      } else {
        merlon.position.set(pos.x, 2.7, -1.2 + m * 0.6);
      }
      group.add(merlon);
    }
  }

  // Gate (front)
  const gate = new THREE.Mesh(new THREE.BoxGeometry(0.8, 1.5, 0.25), doorMat);
  gate.position.set(0, 1.15, -1.8);
  group.add(gate);
  // Gate arch
  const gateArch = new THREE.Mesh(new THREE.BoxGeometry(1.0, 0.2, 0.3), darkStoneMat);
  gateArch.position.set(0, 1.95, -1.8);
  group.add(gateArch);

  // Flag on keep
  const poleMat = new THREE.MeshStandardMaterial({ color: 0x4a3a2a, roughness: 0.7, metalness: 0.15 });
  const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.04, 1.5, 4), poleMat);
  pole.position.set(0, 5.8, 0);
  pole.castShadow = true;
  group.add(pole);

  const flagMat = new THREE.MeshStandardMaterial({ color: teamColor, roughness: 0.5, side: THREE.DoubleSide });
  const flag = new THREE.Mesh(new THREE.PlaneGeometry(0.8, 0.5), flagMat);
  flag.position.set(0.4, 6.3, 0);
  group.add(flag);

  addContactShadow(group, 0, 0, 3.0, 3.0, 0.2);
  return group;
}

function createTownCenter(_roofColor: number, teamColor: number): THREE.Group {
  const group = new THREE.Group();

  // Stone platform
  const platGeo = new THREE.BoxGeometry(3.6, 0.4, 3.6);
  const plat = new THREE.Mesh(platGeo, stoneMat);
  plat.position.set(0, 0.2, 0);
  plat.castShadow = true; plat.receiveShadow = true;
  group.add(plat);

  // Step
  const stepGeo = new THREE.BoxGeometry(4.0, 0.15, 4.0);
  const step = new THREE.Mesh(stepGeo, stoneMat);
  step.position.set(0, 0.07, 0);
  step.receiveShadow = true;
  group.add(step);

  // Main body
  const tcGeo = new THREE.BoxGeometry(3, 2, 3);
  const tc = new THREE.Mesh(tcGeo, wallMat);
  tc.position.set(0, 1.4, 0);
  tc.castShadow = true; tc.receiveShadow = true;
  group.add(tc);

  // Stone base band
  const stoneGeo = new THREE.BoxGeometry(3.1, 0.5, 3.1);
  const stone = new THREE.Mesh(stoneGeo, stoneMat);
  stone.position.set(0, 0.65, 0);
  group.add(stone);

  // Roof
  const roofMat = new THREE.MeshStandardMaterial({ color: teamColor, roughness: 0.65, metalness: 0.08 });
  const roofGeo = new THREE.ConeGeometry(2.6, 1.5, 4);
  const roof = new THREE.Mesh(roofGeo, roofMat);
  roof.position.set(0, 3.15, 0);
  roof.rotation.y = Math.PI / 4;
  roof.castShadow = true;
  group.add(roof);

  // Roof edge
  const bordurGeo = new THREE.BoxGeometry(3.2, 0.12, 3.2);
  const bordur = new THREE.Mesh(bordurGeo, timberMat);
  bordur.position.set(0, 2.44, 0);
  group.add(bordur);

  // Windows (4 sides)
  const windowPositions = [
    { pos: [0, 1.6, -1.52] as const, rot: 0 },
    { pos: [0, 1.6, 1.52] as const, rot: 0 },
    { pos: [-1.52, 1.6, 0] as const, rot: Math.PI / 2 },
    { pos: [1.52, 1.6, 0] as const, rot: Math.PI / 2 },
  ];
  for (const wp of windowPositions) {
    const w = new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.6, 0.02), windowMat);
    w.position.set(wp.pos[0], wp.pos[1], wp.pos[2]);
    w.rotation.y = wp.rot;
    group.add(w);
    const f = new THREE.Mesh(new THREE.BoxGeometry(0.58, 0.68, 0.04), timberMat);
    f.position.set(wp.pos[0], wp.pos[1], wp.pos[2]);
    f.rotation.y = wp.rot;
    group.add(f);
  }

  // Main door
  const door = new THREE.Mesh(new THREE.BoxGeometry(0.7, 1.2, 0.08), doorMat);
  door.position.set(0, 1.0, -1.52);
  group.add(door);

  // Door arch
  const arch = new THREE.Mesh(new THREE.BoxGeometry(0.85, 0.15, 0.12), stoneMat);
  arch.position.set(0, 1.65, -1.52);
  group.add(arch);

  // Corner pillars
  for (const sx of [-1.4, 1.4]) {
    for (const sz of [-1.4, 1.4]) {
      const col = new THREE.Mesh(new THREE.BoxGeometry(0.25, 2.1, 0.25), stoneMat);
      col.position.set(sx, 1.45, sz);
      col.castShadow = true;
      group.add(col);
    }
  }

  // Flag pole
  const poleMat = new THREE.MeshStandardMaterial({ color: 0x4a3a2a, roughness: 0.7, metalness: 0.15 });
  const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.05, 2.0, 4), poleMat);
  pole.position.set(0, 4.9, 0);
  pole.castShadow = true;
  group.add(pole);

  // Flag
  const flagMat = new THREE.MeshStandardMaterial({ color: teamColor, roughness: 0.5, side: THREE.DoubleSide });
  const flag = new THREE.Mesh(new THREE.PlaneGeometry(0.9, 0.6), flagMat);
  flag.position.set(0.45, 5.5, 0);
  group.add(flag);

  addContactShadow(group, 0, 0, 2.5, 2.5, 0.2);
  return group;
}

function createHouse(roofColor: number): THREE.Group {
  const group = new THREE.Group();

  // Foundation
  const found = new THREE.Mesh(new THREE.BoxGeometry(1.7, 0.3, 1.7), stoneMat);
  found.position.set(0, 0.15, 0);
  found.castShadow = true; found.receiveShadow = true;
  group.add(found);

  // Walls
  const base = new THREE.Mesh(new THREE.BoxGeometry(1.5, 1.5, 1.5), wallMat);
  base.position.set(0, 1.05, 0);
  base.castShadow = true; base.receiveShadow = true;
  group.add(base);

  // Timber frame
  for (const sx of [-0.7, 0.7]) {
    for (const sz of [-0.7, 0.7]) {
      const t = new THREE.Mesh(new THREE.BoxGeometry(0.08, 1.5, 0.08), timberMat);
      t.position.set(sx, 1.05, sz);
      group.add(t);
    }
  }

  // Horizontal beams
  for (const sz of [-0.72, 0.72]) {
    const h = new THREE.Mesh(new THREE.BoxGeometry(1.55, 0.06, 0.06), timberMat);
    h.position.set(0, 1.2, sz);
    group.add(h);
  }

  // Windows
  for (const sz of [-0.76, 0.76]) {
    const w = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.35, 0.02), windowMat);
    w.position.set(0, 1.2, sz);
    group.add(w);
    const f = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.41, 0.03), timberMat);
    f.position.set(0, 1.2, sz * 0.99);
    group.add(f);
    const inner = new THREE.Mesh(new THREE.BoxGeometry(0.28, 0.33, 0.035), windowMat);
    inner.position.set(0, 1.2, sz * 0.98);
    group.add(inner);
  }

  // Door
  const door = new THREE.Mesh(new THREE.BoxGeometry(0.4, 0.7, 0.06), doorMat);
  door.position.set(0.3, 0.65, -0.76);
  group.add(door);

  // Roof
  const roofMat = new THREE.MeshStandardMaterial({ color: roofColor, roughness: 0.7, metalness: 0.05 });
  const roof = new THREE.Mesh(new THREE.ConeGeometry(1.25, 1.0, 4), roofMat);
  roof.position.set(0, 2.3, 0);
  roof.rotation.y = Math.PI / 4;
  roof.castShadow = true;
  group.add(roof);

  // Chimney
  const chimMat = new THREE.MeshStandardMaterial({ color: 0x6a5a4a, roughness: 0.9, metalness: 0.05 });
  const chim = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.6, 0.2), chimMat);
  chim.position.set(0.4, 2.5, 0.3);
  chim.castShadow = true;
  group.add(chim);
  const chimTop = new THREE.Mesh(new THREE.BoxGeometry(0.28, 0.08, 0.28), chimMat);
  chimTop.position.set(0.4, 2.84, 0.3);
  group.add(chimTop);

  // Roof ridge
  const ridge = new THREE.Mesh(new THREE.BoxGeometry(1.8, 0.06, 0.06), timberMat);
  ridge.position.set(0, 2.8, 0);
  ridge.rotation.y = Math.PI / 4;
  group.add(ridge);

  addContactShadow(group, 0, 0, 1.3, 1.3, 0.15);
  return group;
}

function createBarracks(roofColor: number): THREE.Group {
  const group = new THREE.Group();

  // Foundation
  const found = new THREE.Mesh(new THREE.BoxGeometry(2.7, 0.3, 2.2), stoneMat);
  found.position.set(0, 0.15, 0);
  found.castShadow = true; found.receiveShadow = true;
  group.add(found);

  // Main body
  const body = new THREE.Mesh(new THREE.BoxGeometry(2.5, 2.0, 2.0), darkWallMat);
  body.position.set(0, 1.3, 0);
  body.castShadow = true; body.receiveShadow = true;
  group.add(body);

  // Stone band
  const bandGeo = new THREE.BoxGeometry(2.55, 0.4, 2.05);
  const band = new THREE.Mesh(bandGeo, stoneMat);
  band.position.set(0, 0.5, 0);
  group.add(band);

  // Roof
  const roofMat = new THREE.MeshStandardMaterial({ color: roofColor, roughness: 0.65, metalness: 0.08 });
  const roof = new THREE.Mesh(new THREE.BoxGeometry(2.7, 0.3, 2.2), roofMat);
  roof.position.set(0, 2.45, 0);
  roof.castShadow = true;
  group.add(roof);

  // Roof edge
  const edge = new THREE.Mesh(new THREE.BoxGeometry(2.8, 0.1, 2.3), timberMat);
  edge.position.set(0, 2.35, 0);
  group.add(edge);

  // Door
  const door = new THREE.Mesh(new THREE.BoxGeometry(0.8, 1.4, 0.06), doorMat);
  door.position.set(0, 1.0, -1.03);
  group.add(door);

  // Door arch
  const arch = new THREE.Mesh(new THREE.BoxGeometry(0.9, 0.15, 0.1), stoneMat);
  arch.position.set(0, 1.75, -1.03);
  group.add(arch);

  // Windows
  const darkWindowMat = new THREE.MeshStandardMaterial({ color: 0x3a3a3a, roughness: 0.5, metalness: 0.2 });
  for (const sx of [-0.7, 0.7]) {
    const w = new THREE.Mesh(new THREE.BoxGeometry(0.25, 0.5, 0.06), darkWindowMat);
    w.position.set(sx, 1.5, -1.03);
    group.add(w);
  }

  // Flag pole
  const poleMat = new THREE.MeshStandardMaterial({ color: 0x4a3a2a, roughness: 0.7, metalness: 0.1 });
  const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.05, 1.8, 4), poleMat);
  pole.position.set(1.1, 3.25, 0);
  pole.castShadow = true;
  group.add(pole);

  // Flag
  const flagMat = new THREE.MeshStandardMaterial({ color: roofColor, roughness: 0.6, side: THREE.DoubleSide });
  const flag = new THREE.Mesh(new THREE.PlaneGeometry(0.7, 0.45), flagMat);
  flag.position.set(1.45, 3.9, 0);
  group.add(flag);

  // Weapon rack
  const rackMat = new THREE.MeshStandardMaterial({ color: 0x3a2a1a, roughness: 0.8, metalness: 0.1 });
  const rack = new THREE.Mesh(new THREE.BoxGeometry(0.8, 0.06, 0.1), rackMat);
  rack.position.set(-0.8, 1.3, 1.03);
  group.add(rack);
  for (let i = 0; i < 3; i++) {
    const spear = new THREE.Mesh(new THREE.CylinderGeometry(0.015, 0.015, 1.2, 3), rackMat);
    spear.position.set(-0.95 + i * 0.15, 1.9, 1.0);
    group.add(spear);
  }

  addContactShadow(group, 0, 0, 1.8, 1.5, 0.18);
  return group;
}
