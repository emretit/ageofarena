import * as THREE from 'three';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { ShaderPass } from 'three/examples/jsm/postprocessing/ShaderPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';
import { GAME_CONFIG } from './config';
import { createGround } from './world/Ground';
import { setupCamera } from './camera/IsometricCamera';
import { setupControls } from './camera/CameraControls';
import { createWallEnclosures } from './world/Walls';
import { createForest } from './world/Forest';
import { createProps } from './world/Props';
import { Minimap } from './ui/Minimap';
import { ZoomLOD } from './utils/LOD';
import { GameWorld } from './entities/GameWorld';
import { SelectionSystem } from './systems/Selection';
import { HUD } from './ui/HUD';
import { ResourceManager } from './systems/ResourceManager';
import { ResourceBarUI } from './ui/ResourceBarUI';
import { TrainingQueue } from './systems/TrainingQueue';
import { IdleVillagerTracker } from './ui/IdleVillagerTracker';
import { HotkeySystem } from './systems/HotkeySystem';
import { CommandPanel } from './ui/CommandPanel';

// Renderer - PBR + tonemapping + daha iyi gölgeler
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.1;
document.body.appendChild(renderer.domElement);

// Sahne
const scene = new THREE.Scene();

// Gökyüzü gradient (üstte açık mavi, altta beyazımsı)
const skyCanvas = document.createElement('canvas');
skyCanvas.width = 2;
skyCanvas.height = 256;
const skyCtx = skyCanvas.getContext('2d')!;
const skyGrad = skyCtx.createLinearGradient(0, 0, 0, 256);
skyGrad.addColorStop(0, '#4a90d9');    // üst - koyu mavi
skyGrad.addColorStop(0.4, '#87ceeb');  // orta - açık mavi
skyGrad.addColorStop(0.8, '#c9e4f2');  // alt - çok açık
skyGrad.addColorStop(1.0, '#e8d5b0');  // ufuk - sıcak ton
skyCtx.fillStyle = skyGrad;
skyCtx.fillRect(0, 0, 2, 256);
const skyTexture = new THREE.CanvasTexture(skyCanvas);
skyTexture.magFilter = THREE.LinearFilter;
scene.background = skyTexture;

// Atmosferik sis - daha yoğun derinlik hissi
scene.fog = new THREE.FogExp2(0xc9dce8, 0.001);

// === IŞIKLANDIRMA - geliştirilmiş çok katmanlı ===

// 1. Hemisphere light: gökyüzü mavisi + zemin yeşili arası doğal geçiş
const hemiLight = new THREE.HemisphereLight(0x8ad0f0, 0x4a7c3f, 0.5);
scene.add(hemiLight);

// 2. Ambient - düşük seviye base fill (sıcak ton)
const ambientLight = new THREE.AmbientLight(0xfff8f0, 0.25);
scene.add(ambientLight);

// 3. Ana güneş ışığı - sıcak altın saat tonu, yüksek kalite gölge
const sunLight = new THREE.DirectionalLight(0xfff0d0, 1.2);
sunLight.position.set(50, 80, 30);
sunLight.castShadow = true;
sunLight.shadow.mapSize.set(2048, 2048);
sunLight.shadow.camera.left = -170;
sunLight.shadow.camera.right = 170;
sunLight.shadow.camera.top = 170;
sunLight.shadow.camera.bottom = -170;
sunLight.shadow.camera.near = 1;
sunLight.shadow.camera.far = 250;
sunLight.shadow.bias = -0.001;
sunLight.shadow.normalBias = 0.04;
scene.add(sunLight);

// 4. Fill light - gölge tarafını yumuşak aydınlat (gölge yok)
const fillLight = new THREE.DirectionalLight(0xb0c4de, 0.3);
fillLight.position.set(-30, 30, -20);
scene.add(fillLight);

// 5. Rim/back light - kenar tanımlama
const rimLight = new THREE.DirectionalLight(0x88aacc, 0.15);
rimLight.position.set(-40, 40, -25);
scene.add(rimLight);

// Harita boyutu (world units)
const mapW = GAME_CONFIG.mapTilesW * GAME_CONFIG.tileSize;
const mapH = GAME_CONFIG.mapTilesH * GAME_CONFIG.tileSize;

// Zemin ve grid
const terrainProps = createGround(scene, mapW, mapH);

// Taş surlar ve kapılar
const enclosures = createWallEnclosures(scene);

// Orman (üslerin arkasında yoğun)
createForest(scene, mapW, mapH, enclosures);

// Çevre prop'ları (taşlar, variller, kütükler, meşaleler)
createProps(scene, mapW, mapH, enclosures);

// Kamera (izometrik)
const camera = setupCamera(mapW, mapH);

// Kontroller (pan + zoom)
const controls = setupControls(camera, renderer.domElement, mapW, mapH);

// Minimap
const minimap = new Minimap(camera, controls, mapW, mapH);

// === ENTITY SYSTEM ===
const gameWorld = new GameWorld(scene);
gameWorld.placeInitialBuildings();
gameWorld.spawnInitialUnits();

// Resource system
const resourceManager = new ResourceManager(4);
const resourceBarUI = new ResourceBarUI();

// Recalc pop after initial spawn
for (let i = 0; i < 4; i++) {
  resourceManager.recalcPop(i, gameWorld.units);
  resourceManager.recalcPopCap(i, gameWorld.buildings);
}

// Training Queue
const trainingQueue = new TrainingQueue(resourceManager, gameWorld);

// HUD & Selection
const hud = new HUD();
hud.onCancelQueueClick = (b, i) => trainingQueue.cancelAt(b, i);

// Command Panel (alt HUD içinde)
const commandPanel = new CommandPanel();
commandPanel.onTrainClick = (b, uid) => trainingQueue.enqueue(b, uid);
commandPanel.onBuildClick = (id) => console.log('Build:', id);
commandPanel.onStanceClick = (units, stance) => {
  for (const u of units) u.stance = stance;
};

const selection = new SelectionSystem(camera, scene, renderer.domElement, gameWorld, hud, commandPanel);

// Delete selected entities
const deleteSelected = () => {
  const sel = selection.getSelected();
  for (const e of sel) {
    if (e.playerIndex !== 0) continue;
    gameWorld.removeEntity(e);
  }
  selection.select(null);
  resourceManager.recalcPop(0, gameWorld.units);
  resourceManager.recalcPopCap(0, gameWorld.buildings);
};
commandPanel.onDeleteClick = deleteSelected;

// Idle villager tracker
const idleTracker = new IdleVillagerTracker();

// Hotkey system
new HotkeySystem({
  selection,
  trainingQueue,
  idleTracker,
  gameWorld,
  moveTo: controls.moveTo,
  onBuildClick: (id) => console.log('Build:', id),
  onStanceClick: (units, stance) => {
    for (const u of units) u.stance = stance;
  },
  onDeleteClick: deleteSelected,
});

// Idle villager button click
document.getElementById('idle-villager-btn')!.addEventListener('click', () => {
  const unit = idleTracker.cycleNext(gameWorld.units, 0);
  if (unit) {
    selection.select(unit);
    controls.moveTo(unit.position.x, unit.position.z);
  }
});

// Zoom-based LOD
const lod = new ZoomLOD(camera);
lod.register('high', [terrainProps.tufts, terrainProps.rocks]);
lod.register('medium', [terrainProps.bushes]);

// === POST-PROCESSING PIPELINE ===
const composer = new EffectComposer(renderer);

// 1. Base render pass
const renderPass = new RenderPass(scene, camera);
composer.addPass(renderPass);

// 2. Bloom - çok hafif, sadece parlak yüzeyler (pencere cam, meşale)
const bloomPass = new UnrealBloomPass(
  new THREE.Vector2(window.innerWidth, window.innerHeight),
  0.15,  // strength - çok hafif
  0.4,   // radius
  0.85   // threshold - sadece parlaklar
);
composer.addPass(bloomPass);

// 4. Color grading - sıcak RTS tonu
const colorGradingShader = {
  uniforms: {
    tDiffuse: { value: null },
    warmth: { value: 0.06 },
    contrast: { value: 1.08 },
    saturation: { value: 1.1 },
  },
  vertexShader: `
    varying vec2 vUv;
    void main() {
      vUv = uv;
      gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
    }
  `,
  fragmentShader: `
    uniform sampler2D tDiffuse;
    uniform float warmth;
    uniform float contrast;
    uniform float saturation;
    varying vec2 vUv;
    void main() {
      vec4 color = texture2D(tDiffuse, vUv);
      // Warmth - kırmızı/sarı kaydırma
      color.r += warmth;
      color.g += warmth * 0.5;
      // Contrast
      color.rgb = (color.rgb - 0.5) * contrast + 0.5;
      // Saturation
      float gray = dot(color.rgb, vec3(0.299, 0.587, 0.114));
      color.rgb = mix(vec3(gray), color.rgb, saturation);
      gl_FragColor = color;
    }
  `,
};
const colorPass = new ShaderPass(colorGradingShader);
composer.addPass(colorPass);

// 5. Output pass
const outputPass = new OutputPass();
composer.addPass(outputPass);

// Pencere yeniden boyutlandırma
window.addEventListener('resize', () => {
  const w = window.innerWidth;
  const h = window.innerHeight;
  renderer.setSize(w, h);
  composer.setSize(w, h);

  const aspect = w / h;
  const viewSize = 30;
  camera.left = -viewSize * aspect;
  camera.right = viewSize * aspect;
  camera.top = viewSize;
  camera.bottom = -viewSize;
  camera.updateProjectionMatrix();

});

// Render döngüsü
const clock = new THREE.Clock();
let frameCount = 0;
function animate() {
  requestAnimationFrame(animate);
  const dt = clock.getDelta();
  controls.update();
  gameWorld.update(dt);
  trainingQueue.update(dt);
  selection.update(dt);
  lod.update();
  resourceBarUI.update(resourceManager.getResources(0));

  // Update idle/military counts (throttled)
  if (frameCount % 12 === 0) {
    const idleCount = idleTracker.getCount(gameWorld.units, 0);
    const militaryCount = gameWorld.units.filter(
      u => u.playerIndex === 0 && u.def.id !== 'villager'
    ).length;
    resourceBarUI.updateCounts(idleCount, militaryCount);
  }

  // Update HUD display (throttled: every 6 frames)
  if (frameCount % 6 === 0) {
    const selected = selection.getSelected();
    if (selected.length === 1) {
      hud.showEntity(selected[0]);
    } else if (selected.length > 1) {
      hud.showEntities(selected);
    }
    const currentBuilding = hud.getCurrentBuilding();
    if (currentBuilding) {
      hud.updateQueueDisplay(trainingQueue.getQueue(currentBuilding));
    }
    commandPanel.updateButtonStates(resourceManager.getResources(0));
  }

  minimap.update();
  composer.render();
  frameCount++;
}

animate();
