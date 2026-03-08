import * as THREE from 'three';

// Kameranın bakış noktasından olan offset'i (izometrik açı)
const CAMERA_DISTANCE = 100;
const ELEVATION_ANGLE = Math.PI / 6; // 30° yukarıdan
const ROTATION_ANGLE = Math.PI / 4;  // 45° sağdan (AoE2 klasik açı)

// Offset hesapla: kamera hedeften bu kadar uzakta duracak
export const CAMERA_OFFSET = new THREE.Vector3(
  CAMERA_DISTANCE * Math.cos(ELEVATION_ANGLE) * Math.sin(ROTATION_ANGLE),
  CAMERA_DISTANCE * Math.sin(ELEVATION_ANGLE),
  CAMERA_DISTANCE * Math.cos(ELEVATION_ANGLE) * Math.cos(ROTATION_ANGLE),
);

export function setupCamera(mapW: number, mapH: number): THREE.OrthographicCamera {
  const aspect = window.innerWidth / window.innerHeight;
  const viewSize = 30;

  const camera = new THREE.OrthographicCamera(
    -viewSize * aspect,
    viewSize * aspect,
    viewSize,
    -viewSize,
    0.1,
    500
  );

  // Başlangıç: oyuncunun üssüne bak
  const targetX = mapW / 2;
  const targetZ = mapH * 0.75;

  camera.position.set(
    targetX + CAMERA_OFFSET.x,
    CAMERA_OFFSET.y,
    targetZ + CAMERA_OFFSET.z,
  );
  camera.lookAt(targetX, 0, targetZ);
  camera.zoom = 3;
  camera.updateProjectionMatrix();

  return camera;
}
