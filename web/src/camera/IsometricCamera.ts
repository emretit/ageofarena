import * as THREE from 'three';
import { GAME_CONFIG } from '../config';

export function setupCamera(mapW: number, mapH: number): THREE.OrthographicCamera {
  const aspect = window.innerWidth / window.innerHeight;
  const zoom = GAME_CONFIG.cameraZoom;

  const camera = new THREE.OrthographicCamera(
    -zoom * aspect,
    zoom * aspect,
    zoom,
    -zoom,
    0.1,
    200
  );

  // İzometrik açı: AoE2 tarzı yukarıdan bakış
  // Kamerayı oyuncunun üssüne odakla (haritanın alt kısmı)
  const centerX = mapW / 2;
  const centerZ = mapH * 0.75; // Oyuncunun üssüne yakın

  const distance = 50;
  const angle = GAME_CONFIG.cameraAngle; // 30 derece

  camera.position.set(
    centerX + distance * Math.sin(Math.PI / 4),
    distance * Math.sin(angle),
    centerZ + distance * Math.cos(Math.PI / 4)
  );

  camera.lookAt(centerX, 0, centerZ);
  camera.zoom = zoom;
  camera.updateProjectionMatrix();

  return camera;
}
