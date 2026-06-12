/**
 * TerrainRenderer.ts — improved terrain: subdivided CircleGeometry with
 * vertex-color splat (grass→dirt→sand transition) + animated ocean ShaderMaterial.
 * Replaces the flat MeshLambertMaterial planes in World.ts.
 */
import * as THREE from "three";
import { Config } from "../core/Config";

const WATER_VERT = /* glsl */`
  uniform float uTime;
  varying vec2 vUv;
  varying float vDepth;

  // Simple 2D value noise for wave height
  float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
  }
  float noise(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i+vec2(1,0)), u.x),
               mix(hash(i+vec2(0,1)), hash(i+vec2(1,1)), u.x), u.y);
  }

  void main() {
    vUv = uv;
    vec3 pos = position;
    // Gentle wave offset
    float wave = noise(pos.xz * 0.12 + uTime * 0.25) * 0.18
               + noise(pos.xz * 0.08 - uTime * 0.15) * 0.12;
    pos.y += wave;
    vDepth = wave;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(pos, 1.0);
  }
`;

const WATER_FRAG = /* glsl */`
  varying vec2 vUv;
  varying float vDepth;
  uniform float uTime;

  void main() {
    // Base water color
    vec3 deep    = vec3(0.10, 0.38, 0.65);
    vec3 shallow = vec3(0.28, 0.62, 0.82);
    vec3 foam    = vec3(0.82, 0.92, 0.98);

    // Edge foam near coast (UV-based, rough approx)
    float distCenter = length(vUv - 0.5) * 2.0;
    float foamEdge   = smoothstep(0.7, 0.95, distCenter);

    // Wave sparkle from vertex depth
    float sparkle = smoothstep(0.12, 0.18, vDepth);

    vec3 col = mix(deep, shallow, sparkle * 0.6 + foamEdge * 0.4);
    col = mix(col, foam, foamEdge * 0.5 + sparkle * 0.3);

    gl_FragColor = vec4(col, 0.88);
  }
`;

function buildVertexColorLand(radius: number, rings: number): THREE.BufferGeometry {
  const geo = new THREE.CircleGeometry(radius, 96, 0, Math.PI * 2);
  geo.computeVertexNormals();

  const pos = geo.attributes.position;
  const count = pos.count;
  const colors = new Float32Array(count * 3);

  const grassColor = new THREE.Color(0x7fac55);
  const dirtColor  = new THREE.Color(0xc8b178);
  const sandColor  = new THREE.Color(0xd9c489);

  for (let i = 0; i < count; i++) {
    const x = pos.getX(i);
    const z = pos.getZ(i);
    const r = Math.sqrt(x * x + z * z);
    const t = Math.min(1, r / radius);

    // 0-0.6: grass, 0.6-0.8: dirt/grass, 0.8-1.0: sand
    let c: THREE.Color;
    if (t < 0.6) {
      c = grassColor;
    } else if (t < 0.8) {
      const u = (t - 0.6) / 0.2;
      c = grassColor.clone().lerp(dirtColor, u);
    } else {
      const u = (t - 0.8) / 0.2;
      c = dirtColor.clone().lerp(sandColor, u);
    }

    colors[i * 3]     = c.r;
    colors[i * 3 + 1] = c.g;
    colors[i * 3 + 2] = c.b;
  }

  geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
  return geo;
}

export interface TerrainObjects {
  land:    THREE.Mesh;
  rim:     THREE.Mesh;
  ocean:   THREE.Mesh;
  /** Call each frame with elapsed time (seconds) to animate ocean. */
  tick(dt: number): void;
}

export function buildTerrain(scene: THREE.Scene): TerrainObjects {
  // ── Ocean ──────────────────────────────────────────────────────────────────
  const oceanGeo = new THREE.PlaneGeometry(Config.OceanHalf * 2, Config.OceanHalf * 2, 32, 32);
  const waterMat = new THREE.ShaderMaterial({
    uniforms: { uTime: { value: 0 } },
    vertexShader: WATER_VERT,
    fragmentShader: WATER_FRAG,
    transparent: true,
    side: THREE.FrontSide,
  });
  const ocean = new THREE.Mesh(oceanGeo, waterMat);
  ocean.rotation.x = -Math.PI / 2;
  ocean.position.y = -0.5;
  scene.add(ocean);

  // ── Rim (sandy shore) ──────────────────────────────────────────────────────
  const rimGeo = new THREE.CircleGeometry(Config.LandRadius + 3, 96);
  const rim = new THREE.Mesh(
    rimGeo,
    new THREE.MeshLambertMaterial({ color: 0xd9c489, vertexColors: false }),
  );
  rim.rotation.x = -Math.PI / 2;
  rim.position.y = -0.15;
  scene.add(rim);

  // ── Land — vertex-color splat ──────────────────────────────────────────────
  const landGeo = buildVertexColorLand(Config.LandRadius, 5);
  const land = new THREE.Mesh(
    landGeo,
    new THREE.MeshLambertMaterial({ vertexColors: true }),
  );
  land.rotation.x = -Math.PI / 2;
  land.receiveShadow = true;
  land.name = "Ground";
  scene.add(land);

  let elapsed = 0;
  return {
    land, rim, ocean,
    tick(dt: number) {
      elapsed += dt;
      (waterMat.uniforms['uTime'] as THREE.IUniform<number>).value = elapsed;
    },
  };
}
