import * as THREE from 'three';
import { UnitId } from './types';

// Shared materials
const skinMat = new THREE.MeshStandardMaterial({ color: 0xd4a574, roughness: 0.8, metalness: 0.0 });
const clothBrown = new THREE.MeshStandardMaterial({ color: 0x6a4a2a, roughness: 0.85 });
const clothBlue = new THREE.MeshStandardMaterial({ color: 0x3a5a8a, roughness: 0.8 });
const clothGreen = new THREE.MeshStandardMaterial({ color: 0x4a6a3a, roughness: 0.8 });
const metalMat = new THREE.MeshStandardMaterial({ color: 0x7a7a7a, roughness: 0.35, metalness: 0.75 });
const metalDark = new THREE.MeshStandardMaterial({ color: 0x555555, roughness: 0.4, metalness: 0.7 });
const woodMat = new THREE.MeshStandardMaterial({ color: 0x5c3a1e, roughness: 0.85 });
const goldMat = new THREE.MeshStandardMaterial({ color: 0xdaa520, roughness: 0.3, metalness: 0.6 });
const horseBrown = new THREE.MeshStandardMaterial({ color: 0x6a4020, roughness: 0.8 });

export function createUnitMesh(unitId: UnitId, teamColor: number): THREE.Group {
  switch (unitId) {
    case 'villager': return createVillager(teamColor);
    case 'militia': return createMilitia(teamColor);
    case 'spearman': return createSpearman(teamColor);
    case 'archer': return createArcher(teamColor);
    case 'skirmisher': return createSkirmisher(teamColor);
    case 'scoutCavalry': return createScoutCavalry(teamColor);
    case 'knight': return createKnight(teamColor);
    case 'monk': return createMonk();
    case 'tradeCart': return createTradeCart(teamColor);
    default: return createVillager(teamColor);
  }
}

function makeHumanoid(_teamColor: number, bodyMat: THREE.Material): THREE.Group {
  const group = new THREE.Group();

  // Legs
  for (const sx of [-0.06, 0.06]) {
    const leg = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.22, 0.08), clothBrown);
    leg.position.set(sx, 0.11, 0);
    leg.castShadow = true;
    group.add(leg);
  }

  // Body
  const body = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.25, 0.12), bodyMat);
  body.position.set(0, 0.35, 0);
  body.castShadow = true;
  group.add(body);

  // Head
  const head = new THREE.Mesh(new THREE.BoxGeometry(0.12, 0.12, 0.12), skinMat);
  head.position.set(0, 0.54, 0);
  head.castShadow = true;
  group.add(head);

  // Arms
  for (const sx of [-0.14, 0.14]) {
    const arm = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.2, 0.06), skinMat);
    arm.position.set(sx, 0.35, 0);
    group.add(arm);
  }

  return group;
}

function createVillager(teamColor: number): THREE.Group {
  const group = makeHumanoid(teamColor, clothBrown);

  // Straw hat
  const hatMat = new THREE.MeshStandardMaterial({ color: 0xc4a840, roughness: 0.9 });
  const brim = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.12, 0.02, 8), hatMat);
  brim.position.set(0, 0.61, 0);
  group.add(brim);
  const crown = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.08, 0.06, 6), hatMat);
  crown.position.set(0, 0.65, 0);
  group.add(crown);

  // Tool (axe/pick)
  const handle = new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.25, 0.02), woodMat);
  handle.position.set(0.18, 0.35, 0);
  handle.rotation.z = -0.3;
  group.add(handle);
  const axeHead = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.06, 0.02), metalMat);
  axeHead.position.set(0.22, 0.48, 0);
  group.add(axeHead);

  group.scale.set(1.4, 1.4, 1.4);
  return group;
}

function createMilitia(teamColor: number): THREE.Group {
  const teamMat = new THREE.MeshStandardMaterial({ color: teamColor, roughness: 0.75 });
  const group = makeHumanoid(teamColor, teamMat);

  // Helmet (simple cap)
  const helmet = new THREE.Mesh(new THREE.SphereGeometry(0.08, 6, 4, 0, Math.PI * 2, 0, Math.PI / 2), metalMat);
  helmet.position.set(0, 0.58, 0);
  group.add(helmet);

  // Shield (left arm)
  const shieldMat = new THREE.MeshStandardMaterial({ color: teamColor, roughness: 0.6, metalness: 0.2 });
  const shield = new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.18, 0.14), shieldMat);
  shield.position.set(-0.18, 0.38, 0);
  group.add(shield);

  // Sword (right hand)
  const swordBlade = new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.2, 0.03), metalMat);
  swordBlade.position.set(0.18, 0.45, 0);
  group.add(swordBlade);
  const swordHilt = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.02, 0.02), woodMat);
  swordHilt.position.set(0.18, 0.34, 0);
  group.add(swordHilt);

  group.scale.set(1.4, 1.4, 1.4);
  return group;
}

function createSpearman(teamColor: number): THREE.Group {
  const teamMat = new THREE.MeshStandardMaterial({ color: teamColor, roughness: 0.75 });
  const group = makeHumanoid(teamColor, teamMat);

  // Helmet
  const helmet = new THREE.Mesh(new THREE.SphereGeometry(0.08, 6, 4, 0, Math.PI * 2, 0, Math.PI / 2), metalMat);
  helmet.position.set(0, 0.58, 0);
  group.add(helmet);

  // Shield
  const shieldMat = new THREE.MeshStandardMaterial({ color: teamColor, roughness: 0.6, metalness: 0.2 });
  const shield = new THREE.Mesh(new THREE.CircleGeometry(0.1, 6), shieldMat);
  shield.position.set(-0.18, 0.38, 0.01);
  group.add(shield);

  // Spear (long)
  const spearShaft = new THREE.Mesh(new THREE.CylinderGeometry(0.01, 0.012, 0.5, 4), woodMat);
  spearShaft.position.set(0.16, 0.5, 0);
  group.add(spearShaft);
  const spearTip = new THREE.Mesh(new THREE.ConeGeometry(0.025, 0.06, 4), metalMat);
  spearTip.position.set(0.16, 0.78, 0);
  group.add(spearTip);

  group.scale.set(1.4, 1.4, 1.4);
  return group;
}

function createArcher(teamColor: number): THREE.Group {
  const group = makeHumanoid(teamColor, clothGreen);

  // Hood/cap
  const hoodMat = new THREE.MeshStandardMaterial({ color: 0x3a5a2a, roughness: 0.85 });
  const hood = new THREE.Mesh(new THREE.ConeGeometry(0.08, 0.1, 6), hoodMat);
  hood.position.set(0, 0.63, 0);
  group.add(hood);

  // Bow (curved line approximation)
  const bowMat = new THREE.MeshStandardMaterial({ color: 0x6a4a2a, roughness: 0.8 });
  const bowCurve = new THREE.TorusGeometry(0.12, 0.012, 4, 8, Math.PI);
  const bow = new THREE.Mesh(bowCurve, bowMat);
  bow.position.set(-0.2, 0.4, 0);
  bow.rotation.z = Math.PI / 2;
  group.add(bow);

  // Bowstring
  const stringMat = new THREE.MeshStandardMaterial({ color: 0xccccaa, roughness: 0.5 });
  const string = new THREE.Mesh(new THREE.CylinderGeometry(0.003, 0.003, 0.24, 3), stringMat);
  string.position.set(-0.2, 0.4, 0);
  group.add(string);

  // Quiver on back
  const quiver = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.04, 0.18, 5), clothBrown);
  quiver.position.set(0.02, 0.42, -0.08);
  quiver.rotation.x = 0.15;
  group.add(quiver);

  group.scale.set(1.4, 1.4, 1.4);
  return group;
}

function createSkirmisher(teamColor: number): THREE.Group {
  const group = makeHumanoid(teamColor, clothBlue);

  // Leather cap
  const capMat = new THREE.MeshStandardMaterial({ color: 0x5a4a30, roughness: 0.88 });
  const cap = new THREE.Mesh(new THREE.SphereGeometry(0.07, 6, 3, 0, Math.PI * 2, 0, Math.PI / 2), capMat);
  cap.position.set(0, 0.58, 0);
  group.add(cap);

  // Shield
  const shieldMat = new THREE.MeshStandardMaterial({ color: teamColor, roughness: 0.6, metalness: 0.2 });
  const shield = new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.16, 0.12), shieldMat);
  shield.position.set(-0.18, 0.38, 0);
  group.add(shield);

  // Javelins (2 in right hand)
  for (let i = 0; i < 2; i++) {
    const jav = new THREE.Mesh(new THREE.CylinderGeometry(0.008, 0.01, 0.35, 3), woodMat);
    jav.position.set(0.16 + i * 0.03, 0.45, 0);
    group.add(jav);
    const tip = new THREE.Mesh(new THREE.ConeGeometry(0.015, 0.04, 3), metalMat);
    tip.position.set(0.16 + i * 0.03, 0.63, 0);
    group.add(tip);
  }

  group.scale.set(1.4, 1.4, 1.4);
  return group;
}

function createMountedUnit(_teamColor: number, bodyMat: THREE.Material): THREE.Group {
  const group = new THREE.Group();

  // Horse body
  const horseBody = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.2, 0.4), horseBrown);
  horseBody.position.set(0, 0.28, 0);
  horseBody.castShadow = true;
  group.add(horseBody);

  // Horse legs (4)
  for (const sx of [-0.06, 0.06]) {
    for (const sz of [-0.12, 0.12]) {
      const leg = new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.18, 0.04), horseBrown);
      leg.position.set(sx, 0.09, sz);
      group.add(leg);
    }
  }

  // Horse head/neck
  const neck = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.15, 0.08), horseBrown);
  neck.position.set(0, 0.38, -0.18);
  neck.rotation.x = -0.4;
  group.add(neck);
  const horseHead = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.08, 0.1), horseBrown);
  horseHead.position.set(0, 0.44, -0.25);
  group.add(horseHead);

  // Horse tail
  const tailMat = new THREE.MeshStandardMaterial({ color: 0x3a2010, roughness: 0.9 });
  const tail = new THREE.Mesh(new THREE.BoxGeometry(0.03, 0.12, 0.03), tailMat);
  tail.position.set(0, 0.3, 0.22);
  tail.rotation.x = 0.5;
  group.add(tail);

  // Rider body
  const riderBody = new THREE.Mesh(new THREE.BoxGeometry(0.14, 0.18, 0.1), bodyMat);
  riderBody.position.set(0, 0.48, 0);
  riderBody.castShadow = true;
  group.add(riderBody);

  // Rider head
  const riderHead = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.1, 0.1), skinMat);
  riderHead.position.set(0, 0.63, 0);
  riderHead.castShadow = true;
  group.add(riderHead);

  // Rider arms
  for (const sx of [-0.1, 0.1]) {
    const arm = new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.14, 0.05), skinMat);
    arm.position.set(sx, 0.46, 0);
    group.add(arm);
  }

  return group;
}

function createScoutCavalry(teamColor: number): THREE.Group {
  const teamMat = new THREE.MeshStandardMaterial({ color: teamColor, roughness: 0.75 });
  const group = createMountedUnit(teamColor, teamMat);

  // Simple leather cap
  const capMat = new THREE.MeshStandardMaterial({ color: 0x5a4a30, roughness: 0.85 });
  const cap = new THREE.Mesh(new THREE.SphereGeometry(0.06, 6, 3, 0, Math.PI * 2, 0, Math.PI / 2), capMat);
  cap.position.set(0, 0.67, 0);
  group.add(cap);

  // Short sword
  const sword = new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.15, 0.025), metalMat);
  sword.position.set(0.14, 0.48, 0);
  group.add(sword);

  group.scale.set(1.6, 1.6, 1.6);
  return group;
}

function createKnight(teamColor: number): THREE.Group {
  const armorMat = new THREE.MeshStandardMaterial({ color: 0x888888, roughness: 0.3, metalness: 0.8 });
  const group = createMountedUnit(teamColor, armorMat);

  // Full helmet
  const helmet = new THREE.Mesh(new THREE.BoxGeometry(0.11, 0.1, 0.11), metalMat);
  helmet.position.set(0, 0.67, 0);
  group.add(helmet);
  // Helmet visor
  const visor = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.04, 0.02), metalDark);
  visor.position.set(0, 0.65, -0.06);
  group.add(visor);

  // Lance
  const lance = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.015, 0.6, 4), woodMat);
  lance.position.set(0.14, 0.6, -0.05);
  lance.rotation.x = -0.2;
  group.add(lance);
  const lanceTip = new THREE.Mesh(new THREE.ConeGeometry(0.025, 0.06, 4), metalMat);
  lanceTip.position.set(0.14, 0.9, -0.12);
  group.add(lanceTip);

  // Shield with team color
  const shieldMat = new THREE.MeshStandardMaterial({ color: teamColor, roughness: 0.5, metalness: 0.3 });
  const shield = new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.16, 0.12), shieldMat);
  shield.position.set(-0.12, 0.48, -0.02);
  group.add(shield);

  // Horse barding (armor)
  const bardMat = new THREE.MeshStandardMaterial({ color: teamColor, roughness: 0.6 });
  const bard = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.06, 0.42), bardMat);
  bard.position.set(0, 0.38, 0);
  group.add(bard);

  group.scale.set(1.6, 1.6, 1.6);
  return group;
}

function createMonk(): THREE.Group {
  const group = new THREE.Group();
  const robeMat = new THREE.MeshStandardMaterial({ color: 0x8a6a2a, roughness: 0.85 });

  // Robe (long body covering legs)
  const robe = new THREE.Mesh(new THREE.BoxGeometry(0.18, 0.4, 0.14), robeMat);
  robe.position.set(0, 0.2, 0);
  robe.castShadow = true;
  group.add(robe);

  // Head
  const head = new THREE.Mesh(new THREE.BoxGeometry(0.12, 0.12, 0.12), skinMat);
  head.position.set(0, 0.48, 0);
  head.castShadow = true;
  group.add(head);

  // Tonsure (bald top hint)
  const tonsure = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, 0.01, 6), skinMat);
  tonsure.position.set(0, 0.55, 0);
  group.add(tonsure);

  // Hood
  const hoodMat = new THREE.MeshStandardMaterial({ color: 0x7a5a1a, roughness: 0.85 });
  const hood = new THREE.Mesh(new THREE.BoxGeometry(0.14, 0.06, 0.14), hoodMat);
  hood.position.set(0, 0.52, 0.02);
  group.add(hood);

  // Staff
  const staff = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.015, 0.55, 4), woodMat);
  staff.position.set(0.14, 0.35, 0);
  staff.castShadow = true;
  group.add(staff);

  // Gold relic/cross on staff
  const cross1 = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.02, 0.02), goldMat);
  cross1.position.set(0.14, 0.63, 0);
  group.add(cross1);
  const cross2 = new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.06, 0.02), goldMat);
  cross2.position.set(0.14, 0.64, 0);
  group.add(cross2);

  // Arms (in prayer/holding staff)
  for (const sx of [-0.12, 0.12]) {
    const arm = new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.16, 0.05), robeMat);
    arm.position.set(sx, 0.35, 0);
    group.add(arm);
  }

  group.scale.set(1.4, 1.4, 1.4);
  return group;
}

function createTradeCart(teamColor: number): THREE.Group {
  const group = new THREE.Group();
  const cartMat = new THREE.MeshStandardMaterial({ color: 0x6a5030, roughness: 0.85 });

  // Cart body
  const body = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.12, 0.5), cartMat);
  body.position.set(0, 0.22, 0);
  body.castShadow = true;
  group.add(body);

  // Cart sides
  for (const sx of [-0.14, 0.14]) {
    const side = new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.1, 0.5), cartMat);
    side.position.set(sx, 0.3, 0);
    group.add(side);
  }

  // Wheels
  const wheelMat = new THREE.MeshStandardMaterial({ color: 0x4a3020, roughness: 0.8 });
  for (const sz of [-0.2, 0.2]) {
    for (const sx of [-0.16, 0.16]) {
      const wheel = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, 0.02, 8), wheelMat);
      wheel.position.set(sx, 0.06, sz);
      wheel.rotation.z = Math.PI / 2;
      group.add(wheel);
    }
  }

  // Goods (cloth bags with team color)
  const goodsMat = new THREE.MeshStandardMaterial({ color: teamColor, roughness: 0.7 });
  const bag1 = new THREE.Mesh(new THREE.SphereGeometry(0.08, 5, 4), goodsMat);
  bag1.position.set(-0.05, 0.35, -0.05);
  bag1.scale.set(1, 0.7, 1);
  group.add(bag1);
  const bag2 = new THREE.Mesh(new THREE.SphereGeometry(0.06, 5, 4), goodsMat);
  bag2.position.set(0.06, 0.33, 0.08);
  bag2.scale.set(1, 0.7, 1);
  group.add(bag2);

  // Donkey/horse pulling
  const donkeyBody = new THREE.Mesh(new THREE.BoxGeometry(0.12, 0.12, 0.2), horseBrown);
  donkeyBody.position.set(0, 0.2, -0.4);
  group.add(donkeyBody);
  // Donkey legs
  for (const sx of [-0.04, 0.04]) {
    for (const sz of [-0.06, 0.06]) {
      const leg = new THREE.Mesh(new THREE.BoxGeometry(0.03, 0.12, 0.03), horseBrown);
      leg.position.set(sx, 0.06, -0.4 + sz);
      group.add(leg);
    }
  }
  // Donkey head
  const dHead = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.06, 0.06), horseBrown);
  dHead.position.set(0, 0.28, -0.52);
  group.add(dHead);

  // Shaft connecting cart to donkey
  const shaft = new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.02, 0.2), woodMat);
  shaft.position.set(0, 0.2, -0.3);
  group.add(shaft);

  group.scale.set(1.6, 1.6, 1.6);
  return group;
}
