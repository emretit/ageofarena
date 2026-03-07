import * as THREE from 'three';
import { GAME_CONFIG } from './config';
import { createGround } from './world/Ground';
import { createBases } from './world/Bases';
import { setupCamera } from './camera/IsometricCamera';
import { setupControls } from './camera/CameraControls';

// Renderer
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
document.body.appendChild(renderer.domElement);

// Sahne
const scene = new THREE.Scene();
scene.background = new THREE.Color(0x87ceeb); // Gökyüzü mavi

// Işıklandırma
const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
scene.add(ambientLight);

const sunLight = new THREE.DirectionalLight(0xfff4e0, 1.0);
sunLight.position.set(30, 50, 20);
sunLight.castShadow = true;
sunLight.shadow.mapSize.set(2048, 2048);
sunLight.shadow.camera.left = -60;
sunLight.shadow.camera.right = 60;
sunLight.shadow.camera.top = 60;
sunLight.shadow.camera.bottom = -60;
scene.add(sunLight);

// Harita boyutu (world units)
const mapW = GAME_CONFIG.mapTilesW * GAME_CONFIG.tileSize;
const mapH = GAME_CONFIG.mapTilesH * GAME_CONFIG.tileSize;

// Zemin ve grid
createGround(scene, mapW, mapH);

// Üsler (oyuncu alt, düşman üst)
createBases(scene, mapW, mapH);

// Kamera (izometrik)
const camera = setupCamera(mapW, mapH);

// Kontroller (pan + zoom)
const controls = setupControls(camera, renderer.domElement, mapW, mapH);

// Pencere yeniden boyutlandırma
window.addEventListener('resize', () => {
  const w = window.innerWidth;
  const h = window.innerHeight;
  renderer.setSize(w, h);

  const aspect = w / h;
  const zoom = camera.zoom;
  camera.left = -zoom * aspect;
  camera.right = zoom * aspect;
  camera.top = zoom;
  camera.bottom = -zoom;
  camera.updateProjectionMatrix();
});

// Render döngüsü
function animate() {
  requestAnimationFrame(animate);
  controls.update();
  renderer.render(scene, camera);
}

animate();
