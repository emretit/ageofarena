import * as THREE from 'three';
import { GAME_CONFIG } from '../config';

export function createBases(scene: THREE.Scene, mapW: number, mapH: number): void {
  const { colors, tileSize } = GAME_CONFIG;
  const baseDepth = 5 * tileSize; // 5 tile derinliğinde üs alanı

  // Oyuncu üssü (alt kısım - güneyde)
  const playerBaseGeo = new THREE.PlaneGeometry(mapW, baseDepth);
  const playerBaseMat = new THREE.MeshLambertMaterial({ color: colors.playerBase, transparent: true, opacity: 0.5 });
  const playerBase = new THREE.Mesh(playerBaseGeo, playerBaseMat);
  playerBase.rotation.x = -Math.PI / 2;
  playerBase.position.set(mapW / 2, 0.03, mapH - baseDepth / 2);
  scene.add(playerBase);

  // Oyuncu Şehir Merkezi (basit kutu)
  const tcGeo = new THREE.BoxGeometry(3, 2, 3);
  const tcMat = new THREE.MeshLambertMaterial({ color: 0x8b7355 });
  const townCenter = new THREE.Mesh(tcGeo, tcMat);
  townCenter.position.set(mapW / 2, 1, mapH - baseDepth / 2);
  townCenter.castShadow = true;
  townCenter.receiveShadow = true;
  scene.add(townCenter);

  // Oyuncu TC çatı
  const roofGeo = new THREE.ConeGeometry(2.5, 1.5, 4);
  const roofMat = new THREE.MeshLambertMaterial({ color: 0xcc4444 });
  const roof = new THREE.Mesh(roofGeo, roofMat);
  roof.position.set(mapW / 2, 2.75, mapH - baseDepth / 2);
  roof.rotation.y = Math.PI / 4;
  roof.castShadow = true;
  scene.add(roof);

  // Düşman üssü (üst kısım - kuzeyde)
  const enemyBaseGeo = new THREE.PlaneGeometry(mapW, baseDepth);
  const enemyBaseMat = new THREE.MeshLambertMaterial({ color: colors.enemyBase, transparent: true, opacity: 0.5 });
  const enemyBase = new THREE.Mesh(enemyBaseGeo, enemyBaseMat);
  enemyBase.rotation.x = -Math.PI / 2;
  enemyBase.position.set(mapW / 2, 0.03, baseDepth / 2);
  scene.add(enemyBase);

  // Düşman Şehir Merkezi
  const etcGeo = new THREE.BoxGeometry(3, 2, 3);
  const etcMat = new THREE.MeshLambertMaterial({ color: 0x5a4a3a });
  const enemyTC = new THREE.Mesh(etcGeo, etcMat);
  enemyTC.position.set(mapW / 2, 1, baseDepth / 2);
  enemyTC.castShadow = true;
  enemyTC.receiveShadow = true;
  scene.add(enemyTC);

  // Düşman TC çatı
  const eRoofGeo = new THREE.ConeGeometry(2.5, 1.5, 4);
  const eRoofMat = new THREE.MeshLambertMaterial({ color: 0x4444cc });
  const eRoof = new THREE.Mesh(eRoofGeo, eRoofMat);
  eRoof.position.set(mapW / 2, 2.75, baseDepth / 2);
  eRoof.rotation.y = Math.PI / 4;
  eRoof.castShadow = true;
  scene.add(eRoof);
}
