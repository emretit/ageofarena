/**
 * PostFx.ts — EffectComposer pipeline: RenderPass → UnrealBloom → OutputPass.
 * Quality tiers: High (MSAA4 RT + bloom), Medium (FXAA + bloom), Low (no PP).
 * Auto-selects on first render; can be changed at runtime.
 *
 * Usage:
 *   const postfx = new PostFx(renderer, scene, camera);
 *   // in render loop: postfx.render() instead of renderer.render(scene, camera)
 */
import * as THREE from "three";
import { EffectComposer }    from "three/examples/jsm/postprocessing/EffectComposer.js";
import { RenderPass }        from "three/examples/jsm/postprocessing/RenderPass.js";
import { UnrealBloomPass }   from "three/examples/jsm/postprocessing/UnrealBloomPass.js";
import { OutputPass }        from "three/examples/jsm/postprocessing/OutputPass.js";
import { ShaderPass }        from "three/examples/jsm/postprocessing/ShaderPass.js";
import { FXAAShader }        from "three/examples/jsm/shaders/FXAAShader.js";

export type QualityTier = 'High' | 'Medium' | 'Low';

export class PostFx {
  private _composer: EffectComposer | null = null;
  private _tier: QualityTier = 'High';

  private readonly _renderer: THREE.WebGLRenderer;
  private readonly _scene: THREE.Scene;
  private readonly _camera: THREE.Camera;

  constructor(renderer: THREE.WebGLRenderer, scene: THREE.Scene, camera: THREE.Camera) {
    this._renderer = renderer;
    this._scene    = scene;
    this._camera   = camera;
    this._autoSelectTier();
    this._build();
  }

  get tier(): QualityTier { return this._tier; }

  setTier(t: QualityTier): void {
    this._tier = t;
    this._build();
  }

  render(): void {
    if (this._composer) {
      this._composer.render();
    } else {
      this._renderer.render(this._scene, this._camera);
    }
  }

  onResize(w: number, h: number): void {
    if (this._composer) this._composer.setSize(w, h);
  }

  private _autoSelectTier(): void {
    const gl = this._renderer.getContext();
    const dbExt = gl.getExtension('WEBGL_debug_renderer_info');
    if (!dbExt) { this._tier = 'Medium'; return; }
    const renderer = gl.getParameter(dbExt.UNMASKED_RENDERER_WEBGL) as string;
    // Integrated / mobile GPUs → Medium; unknown → Medium
    if (/intel|mesa|llvm|swiftshader|software/i.test(renderer)) {
      this._tier = 'Medium';
    } else {
      this._tier = 'High';
    }
  }

  private _build(): void {
    if (this._tier === 'Low') {
      this._composer = null;
      return;
    }

    const w = this._renderer.domElement.width;
    const h = this._renderer.domElement.height;

    // High: MSAA4 render target
    let target: THREE.WebGLRenderTarget | undefined;
    if (this._tier === 'High') {
      target = new THREE.WebGLRenderTarget(w, h, {
        samples: 4,
        colorSpace: THREE.SRGBColorSpace,
      });
    }

    const composer = target
      ? new EffectComposer(this._renderer, target)
      : new EffectComposer(this._renderer);
    composer.setSize(w, h);

    // 1. Render pass
    composer.addPass(new RenderPass(this._scene, this._camera));

    // 2. Bloom — subtle glow (strength 0.25 at quarter-res threshold 0.95)
    const bloom = new UnrealBloomPass(
      new THREE.Vector2(w, h),
      0.25,  // strength
      0.4,   // radius
      0.95,  // threshold — only very bright highlights glow
    );
    composer.addPass(bloom);

    // 3. FXAA on Medium (High already has MSAA)
    if (this._tier === 'Medium') {
      const fxaa = new ShaderPass(FXAAShader);
      (fxaa.material.uniforms['resolution'] as THREE.IUniform<THREE.Vector2>).value.set(1 / w, 1 / h);
      composer.addPass(fxaa);
    }

    // 4. OutputPass — handles tone-mapping output + color-space conversion
    composer.addPass(new OutputPass());

    this._composer = composer;
  }
}
