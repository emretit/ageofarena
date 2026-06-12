/**
 * AudioManager.ts — Port of AudioManager.cs (N7.sfx subset).
 * Procedural Web Audio API synthesis; no asset files needed.
 * Matches Unity's 14 SoundId set with pitch-vary.
 */

export const enum SoundId {
  UnitAttack   = "UnitAttack",
  UnitDie      = "UnitDie",
  BuildingDie  = "BuildingDie",
  GatherHit    = "GatherHit",
  TrainStart   = "TrainStart",
  ResearchDone = "ResearchDone",
  AgeUp        = "AgeUp",
  ButtonClick  = "ButtonClick",
  Victory      = "Victory",
  Defeat       = "Defeat",
  Conversion   = "Conversion",
  HornAttack   = "HornAttack",
}

/** Master volume (0–1). Persisted in localStorage. */
let _masterVol = parseFloat(localStorage.getItem("masterVol") ?? "0.4");
let _sfxVol    = parseFloat(localStorage.getItem("sfxVol")    ?? "0.7");

export function setMasterVol(v: number) { _masterVol = v; localStorage.setItem("masterVol", String(v)); }
export function setSfxVol(v: number)    { _sfxVol    = v; localStorage.setItem("sfxVol",    String(v)); }
export function getMasterVol()          { return _masterVol; }
export function getSfxVol()             { return _sfxVol; }

let _ctx: AudioContext | null = null;

// Ambient loop state
let _ambientGain: GainNode | null = null;
let _ambientStarted = false;

function ctx(): AudioContext {
  if (!_ctx) _ctx = new AudioContext();
  // Resume if suspended (browser autoplay policy)
  if (_ctx.state === "suspended") _ctx.resume();
  return _ctx;
}

/** Start the ambient wind/nature loop (call once on first interaction). */
function _startAmbient() {
  if (_ambientStarted) return;
  _ambientStarted = true;
  try {
    const c = ctx();
    const bufLen = c.sampleRate * 2;
    const buf = c.createBuffer(1, bufLen, c.sampleRate);
    const data = buf.getChannelData(0);
    for (let i = 0; i < bufLen; i++) data[i] = (Math.random() * 2 - 1);

    const src = c.createBufferSource();
    src.buffer = buf;
    src.loop = true;

    const lp = c.createBiquadFilter();
    lp.type = "lowpass";
    lp.frequency.value = 300;

    _ambientGain = c.createGain();
    _ambientGain.gain.value = 0.08 * _masterVol;

    src.connect(lp).connect(_ambientGain).connect(c.destination);
    src.start();
  } catch { /* no ambient if ctx fails */ }
}

/**
 * Duck ambient by intensity 0..1.
 * At full intensity (lots of combat), ambient drops to ~20% of normal.
 */
export function setAmbientDuck(intensity: number): void {
  if (!_ambientGain) return;
  const target = 0.08 * _masterVol * (1 - intensity * 0.8);
  _ambientGain.gain.setTargetAtTime(target, ctx().currentTime, 0.5);
}

/** Pitch-vary helper: ±fraction random offset. */
function pitchVary(base: number, variance = 0.08): number {
  return base * (1 + (Math.random() * 2 - 1) * variance);
}

/** Short oscillator burst. */
function playTone(
  freq: number,
  type: OscillatorType,
  duration: number,
  gainPeak: number,
  attack = 0.01,
  release?: number,
) {
  const c = ctx();
  const vol = _masterVol * _sfxVol;
  if (vol <= 0) return;

  const osc  = c.createOscillator();
  const gain = c.createGain();
  osc.connect(gain);
  gain.connect(c.destination);

  osc.type      = type;
  osc.frequency.value = freq;
  gain.gain.setValueAtTime(0, c.currentTime);
  gain.gain.linearRampToValueAtTime(gainPeak * vol, c.currentTime + attack);
  const rel = release ?? duration - attack;
  gain.gain.setValueAtTime(gainPeak * vol, c.currentTime + attack);
  gain.gain.linearRampToValueAtTime(0, c.currentTime + attack + rel);

  osc.start(c.currentTime);
  osc.stop(c.currentTime + duration);
}

/** White noise burst (for impacts / gather). */
function playNoise(duration: number, gainPeak: number, hpFreq = 800) {
  const c = ctx();
  const vol = _masterVol * _sfxVol;
  if (vol <= 0) return;

  const bufSize = Math.ceil(c.sampleRate * duration);
  const buf  = c.createBuffer(1, bufSize, c.sampleRate);
  const data = buf.getChannelData(0);
  for (let i = 0; i < bufSize; i++) data[i] = Math.random() * 2 - 1;

  const src    = c.createBufferSource();
  const filter = c.createBiquadFilter();
  const gain   = c.createGain();
  src.buffer   = buf;
  filter.type  = "highpass";
  filter.frequency.value = hpFreq;
  src.connect(filter);
  filter.connect(gain);
  gain.connect(c.destination);
  gain.gain.setValueAtTime(gainPeak * vol, c.currentTime);
  gain.gain.linearRampToValueAtTime(0, c.currentTime + duration);
  src.start(c.currentTime);
}

export function play(id: SoundId) {
  try {
    switch (id) {
      case SoundId.UnitAttack:
        playNoise(0.06, 0.25, pitchVary(600, 0.15));
        break;
      case SoundId.UnitDie:
        playTone(pitchVary(180, 0.2), "sawtooth", 0.3, 0.3, 0.01, 0.28);
        break;
      case SoundId.BuildingDie:
        playNoise(0.5, 0.6, 80);
        playTone(pitchVary(90, 0.1), "sawtooth", 0.5, 0.4, 0.01, 0.45);
        break;
      case SoundId.GatherHit:
        playNoise(0.05, 0.15, pitchVary(1200, 0.2));
        break;
      case SoundId.TrainStart:
        playTone(pitchVary(440, 0.05), "sine", 0.12, 0.3);
        break;
      case SoundId.ResearchDone:
        playTone(pitchVary(523, 0.03), "sine", 0.1, 0.3);
        setTimeout(() => playTone(pitchVary(659, 0.03), "sine", 0.1, 0.35), 120);
        setTimeout(() => playTone(pitchVary(784, 0.03), "sine", 0.15, 0.4), 240);
        break;
      case SoundId.AgeUp:
        playTone(523, "sine", 0.15, 0.4);
        setTimeout(() => playTone(659, "sine", 0.15, 0.4), 150);
        setTimeout(() => playTone(784, "sine", 0.15, 0.4), 300);
        setTimeout(() => playTone(1047, "sine", 0.25, 0.5), 450);
        break;
      case SoundId.ButtonClick:
        playTone(pitchVary(880, 0.05), "square", 0.07, 0.15);
        break;
      case SoundId.Victory:
        [523, 659, 784, 1047, 1319].forEach((f, i) =>
          setTimeout(() => playTone(f, "sine", 0.2, 0.45), i * 160)
        );
        break;
      case SoundId.Defeat:
        [392, 349, 311, 262].forEach((f, i) =>
          setTimeout(() => playTone(f, "sawtooth", 0.25, 0.3, 0.02, 0.2), i * 200)
        );
        break;
      case SoundId.Conversion:
        // Gregorian chant-like: rising thirds
        [330, 392, 440, 523].forEach((f, i) =>
          setTimeout(() => playTone(pitchVary(f, 0.02), "sine", 0.18, 0.5), i * 220)
        );
        break;
      case SoundId.HornAttack:
        // Military horn blast
        [220, 330, 440].forEach((f, i) =>
          setTimeout(() => playTone(pitchVary(f, 0.03), "sawtooth", 0.3, 0.35), i * 100)
        );
        break;
    }
    _startAmbient();
  } catch {
    // Silently ignore if AudioContext unavailable
  }
}
