// Pippy The Ski Penguin prototype — 3D renderer (three.js). Soft, toy-like realism: PBR materials,
// sun shadows, image-based fill light and procedural textures (knit, snow grain). Everything is
// modelled from primitives in code. Game state comes from the shared PS object; the painted sky
// band is still drawn by ART.sky on the 2D canvas underneath this one.
import * as THREE from 'three';
import { mergeGeometries, mergeVertices } from 'three/addons/utils/BufferGeometryUtils.js';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { Pass } from 'three/addons/postprocessing/Pass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

const PS = window.PS;
const ART = window.PSArt;
const INK = '#2B2140';
const canvas3d = document.getElementById('game3d');
const frameEl = document.getElementById('frame');
const want3d = !/[?&]r=2d\b/.test(location.search);

// ---------- Procedural textures (shared by every scene) ----------
function hash2(x, y) { const s = Math.sin(x * 127.1 + y * 311.7) * 43758.5453; return s - Math.floor(s); }
// Tileable value noise on a period-p grid.
function vnoise(u, v, p) {
  const x = u * p; const y = v * p;
  const xi = Math.floor(x); const yi = Math.floor(y);
  const xf = x - xi; const yf = y - yi;
  const w = (i) => ((i % p) + p) % p;
  const a = hash2(w(xi), w(yi)); const b = hash2(w(xi + 1), w(yi));
  const c = hash2(w(xi), w(yi + 1)); const d = hash2(w(xi + 1), w(yi + 1));
  const sx = xf * xf * (3 - 2 * xf); const sy = yf * yf * (3 - 2 * yf);
  return a + (b - a) * sx + (c - a) * sy + (a - b - c + d) * sx * sy;
}
function normalTex(size, hf, strength) {
  const c = document.createElement('canvas');
  c.width = c.height = size;
  const x = c.getContext('2d');
  const img = x.createImageData(size, size);
  const H = new Float32Array(size * size);
  for (let j = 0; j < size; j++) for (let i = 0; i < size; i++) H[j * size + i] = hf(i / size, j / size);
  const at = (i, j) => H[((j + size) % size) * size + ((i + size) % size)];
  for (let j = 0; j < size; j++) {
    for (let i = 0; i < size; i++) {
      const dx = (at(i + 1, j) - at(i - 1, j)) * strength;
      const dy = (at(i, j + 1) - at(i, j - 1)) * strength;
      const l = Math.hypot(dx, dy, 1);
      const k = (j * size + i) * 4;
      img.data[k] = (-dx / l * 0.5 + 0.5) * 255;
      img.data[k + 1] = (dy / l * 0.5 + 0.5) * 255;
      img.data[k + 2] = (1 / l * 0.5 + 0.5) * 255;
      img.data[k + 3] = 255;
    }
  }
  x.putImageData(img, 0, 0);
  const t = new THREE.CanvasTexture(c);
  t.wrapS = t.wrapT = THREE.RepeatWrapping;
  t.colorSpace = THREE.NoColorSpace;
  return t;
}
function canvasTex(w, h, draw, opts) {
  const c = document.createElement('canvas');
  c.width = w; c.height = h;
  draw(c.getContext('2d'), w, h);
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.anisotropy = 4;
  if (opts && opts.repeat) tex.wrapS = tex.wrapT = THREE.RepeatWrapping;
  return tex;
}
// Knit: vertical ribs with a V-stitch running down each rib.
const KNIT_N = normalTex(128, (u, v) => {
  const rib = 0.5 + 0.5 * Math.cos(u * Math.PI * 2 * 8);
  const f = (u * 8) % 1;
  const st = Math.abs(Math.sin((v * 10 + Math.abs(f - 0.5) * 1.4) * Math.PI * 2));
  return rib * 0.7 + st * 0.3;
}, 5);
const SNOW_N = normalTex(256, (u, v) => vnoise(u, v, 8) * 0.6 + vnoise(u, v, 32) * 0.3 + vnoise(u, v, 64) * 0.1, 3);
const FLUFF_N = normalTex(64, (u, v) => vnoise(u, v, 16), 6);

let renderer = null;
if (want3d && canvas3d) {
  try {
    renderer = new THREE.WebGLRenderer({ canvas: canvas3d, antialias: true, alpha: true, powerPreference: 'high-performance' });
  } catch (err) {
    renderer = null;
  }
}

function setup() {
  renderer.setClearColor(0x000000, 0);
  renderer.autoClear = false;
  renderer.toneMapping = THREE.NeutralToneMapping;
  renderer.toneMappingExposure = 0.95;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  const scene = new THREE.Scene();
  const pmrem = new THREE.PMREMGenerator(renderer);
  scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
  scene.environmentIntensity = 0.35;
  const camera = new THREE.PerspectiveCamera(30, 0.46, 20, 6000);
  // Camera sits in front of the skier, high, looking back up the slope at 52°: steep enough
  // that the near edge of the screen still shows the full width of an answer-gate row.
  const CAM = { h: 1184, d: 1108, pitch: THREE.MathUtils.degToRad(52), follow: 0.12 };
  // Where the slope tips over behind the skier: closer = more sky and mountains on screen.
  const CREST = -130;
  const bendY = (rel) => { const d = CREST - rel; return d > 0 ? -0.006 * d * d : 0; };

  scene.fog = new THREE.Fog('#DCEBFF', 2300, 4300);
  const hemi = new THREE.HemisphereLight('#FFFFFF', '#8FA9E0', 0.65);
  scene.add(hemi);
  const sun = new THREE.DirectionalLight('#FFF4E2', 2.4);
  sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  Object.assign(sun.shadow.camera, { left: -760, right: 760, top: 900, bottom: -900, near: 100, far: 3200 });
  sun.shadow.camera.updateProjectionMatrix();
  sun.shadow.bias = -0.0004;
  sun.shadow.normalBias = 1.2;
  sun.shadow.radius = 3;
  scene.add(sun);
  scene.add(sun.target);
  // Cool rim light from behind so characters separate from the white snow.
  const rim = new THREE.DirectionalLight('#CFE2FF', 1.1);
  scene.add(rim);
  scene.add(rim.target);

  // Backdrop (sky dome, distant mountains, clouds) is a second scene with its own camera,
  // drawn first; the main scene's slope covers everything below the crest.
  const bd = makeBackdrop();

  // ---------- Post-processing (high quality): MSAA, bloom on hot highlights, grade + vignette ----------
  class DualRenderPass extends Pass {
    constructor() { super(); this.needsSwap = false; }
    render(r, writeBuffer, readBuffer) {
      r.setRenderTarget(this.renderToScreen ? null : readBuffer);
      r.clear();
      r.render(bd.scene, bd.cam);
      r.clearDepth();
      r.render(scene, camera);
    }
  }
  const GradeShader = {
    uniforms: { tDiffuse: { value: null } },
    vertexShader: 'varying vec2 vUv; void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
    fragmentShader: `uniform sampler2D tDiffuse; varying vec2 vUv;
      void main() {
        vec4 c = texture2D(tDiffuse, vUv);
        vec3 col = c.rgb;
        float l = dot(col, vec3(0.299, 0.587, 0.114));
        col = mix(vec3(l), col, 1.1);
        col = mix(col, col * vec3(1.03, 1.0, 0.96), smoothstep(0.55, 1.0, l));
        vec2 d = (vUv - 0.5) * vec2(1.0, 0.8);
        col *= mix(0.84, 1.0, smoothstep(0.75, 0.3, length(d)));
        gl_FragColor = vec4(col, c.a);
      }`,
  };
  let composer = null;
  let quality = null;
  function applyQuality() {
    const q = PS.gfx === 'normal' ? 'normal' : 'high';
    if (q === quality) return;
    quality = q;
    const size = q === 'high' ? 2048 : 1024;
    if (sun.shadow.mapSize.x !== size) {
      sun.shadow.mapSize.set(size, size);
      if (sun.shadow.map) { sun.shadow.map.dispose(); sun.shadow.map = null; }
    }
    if (q === 'high' && !composer) {
      composer = new EffectComposer(renderer, new THREE.WebGLRenderTarget(2, 2, { type: THREE.HalfFloatType, samples: 4 }));
      composer.addPass(new DualRenderPass());
      composer.addPass(new UnrealBloomPass(new THREE.Vector2(256, 256), 0.38, 0.45, 2.4));
      composer.addPass(new OutputPass());
      composer.addPass(new ShaderPass(GradeShader));
    }
    lastW = 0;
  }

  const lib = makeLibrary();
  const { mat, part, group, SPH, PLANE, FLAT, capsule, rbox } = lib;

  const PLAYER_SCALE = 0.62;
  const player = makePenguin(lib);
  scene.add(player.root);
  const rival = makeBenny(lib);
  scene.add(rival.root);

  const shield = new THREE.Mesh(SPH, new THREE.MeshPhysicalMaterial({ color: '#BFE6FF', roughness: 0.05, transparent: true, opacity: 0.3, clearcoat: 1, depthWrite: false }));
  shield.scale.setScalar(48);
  scene.add(shield);
  const magnet = new THREE.Mesh(new THREE.TorusGeometry(52, 2, 6, 48), new THREE.MeshBasicMaterial({ color: '#FF5C93', transparent: true, opacity: 0.6 }));
  magnet.rotation.x = -Math.PI / 2;
  scene.add(magnet);
  // Power-crystal glow: an HDR additive halo (blooms in high quality) plus a pool of light on the snow.
  const haloTex = canvasTex(128, 128, (x) => {
    const gr = x.createRadialGradient(64, 64, 0, 64, 64, 64);
    gr.addColorStop(0, 'rgba(255,255,255,1)');
    gr.addColorStop(0.3, 'rgba(255,255,255,0.5)');
    gr.addColorStop(1, 'rgba(255,255,255,0)');
    x.fillStyle = gr; x.fillRect(0, 0, 128, 128);
  });
  const glowMat = (r, g, b) => new THREE.SpriteMaterial({ map: haloTex, color: new THREE.Color(r, g, b), transparent: true, blending: THREE.AdditiveBlending, depthWrite: false });
  // Additive light vanishes on white snow, so the visible halo and pool are a normal-blended
  // cyan tint; the additive core on top is what bloom picks up.
  const aura = new THREE.Sprite(new THREE.SpriteMaterial({ map: haloTex, color: '#3FD8FF', transparent: true, depthWrite: false }));
  aura.renderOrder = 5;
  scene.add(aura);
  const auraCore = new THREE.Sprite(glowMat(0, 0, 0));
  auraCore.renderOrder = 6;
  scene.add(auraCore);
  const auraPool = new THREE.Mesh(PLANE, new THREE.MeshBasicMaterial({ map: haloTex, color: '#36C9F5', transparent: true, depthWrite: false }));
  auraPool.rotation.x = -Math.PI / 2;
  scene.add(auraPool);

  // ---------- Ground ----------
  const snowTex = canvasTex(256, 256, (x, w, h) => {
    x.fillStyle = '#FFFFFF'; x.fillRect(0, 0, w, h);
    for (let i = 0; i < 16; i++) {
      const gx = (i * 97) % w; const gy = (i * 61) % h;
      const g = x.createRadialGradient(gx, gy, 2, gx, gy, 40 + (i % 3) * 16);
      g.addColorStop(0, 'rgba(214,228,255,0.6)'); g.addColorStop(1, 'rgba(214,228,255,0)');
      x.fillStyle = g; x.fillRect(0, 0, w, h);
    }
  }, { repeat: true });
  const TILE = 420;
  const groundGeo = new THREE.PlaneGeometry(1800, 1700, 36, 68);
  groundGeo.rotateX(-Math.PI / 2);
  const GROUND_OFF = 250;
  {
    const pos = groundGeo.attributes.position;
    for (let i = 0; i < pos.count; i++) pos.setY(i, bendY(pos.getZ(i) + GROUND_OFF));
    groundGeo.computeVertexNormals();
  }
  snowTex.repeat.set(1800 / TILE, 1700 / TILE);
  const snowN = SNOW_N.clone();
  snowN.needsUpdate = true;
  snowN.repeat.set(1800 / 160, 1700 / 160);
  // Snow glints: mostly matte, with tiny glossy specks that catch the sun and twinkle (and bloom).
  const glintTex = canvasTex(256, 256, (x, w, h) => {
    x.fillStyle = 'rgb(214,214,214)'; x.fillRect(0, 0, w, h);
    x.fillStyle = 'rgb(16,16,16)';
    for (let i = 0; i < 700; i++) x.fillRect(Math.floor(hash2(i, 1.7) * w), Math.floor(hash2(i, 5.3) * h), 2, 2);
  }, { repeat: true });
  glintTex.colorSpace = THREE.NoColorSpace;
  glintTex.minFilter = glintTex.magFilter = THREE.NearestFilter;
  glintTex.generateMipmaps = false;
  glintTex.repeat.copy(snowN.repeat);
  const ground = new THREE.Mesh(groundGeo, new THREE.MeshStandardMaterial({ color: '#F7FAFF', map: snowTex, normalMap: snowN, normalScale: new THREE.Vector2(0.55, 0.55), roughness: 1, roughnessMap: glintTex }));
  ground.receiveShadow = true;
  scene.add(ground);
  const lip = new THREE.Mesh(new THREE.CylinderGeometry(26, 26, 1800, 18, 1), new THREE.MeshStandardMaterial({ color: '#F7FAFF', map: snowTex, roughness: 0.85 }));
  lip.rotation.z = Math.PI / 2;
  lip.receiveShadow = true;
  scene.add(lip);

  // ---------- Instanced scenery ----------
  const vcMat = new THREE.MeshStandardMaterial({ color: '#FFFFFF', vertexColors: true, roughness: 0.85 });
  const geoByWorld = new Map();
  function sceneryGeos(w) {
    if (geoByWorld.has(w)) return geoByWorld.get(w);
    const out = { trees: [0, 1, 2].map((variant) => makeTreeGeo(w, variant)), rock: makeRockGeo(w) };
    geoByWorld.set(w, out);
    return out;
  }
  function instanced(geo, material, max, shadow) {
    const im = new THREE.InstancedMesh(geo, material, max);
    im.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    im.frustumCulled = false;
    im.castShadow = !!shadow;
    scene.add(im);
    return { im, n: 0, max };
  }
  const w0 = sceneryGeos(ART.WORLD.meadow);
  const treeTime = { value: 0 };
  const treeMat = new THREE.MeshStandardMaterial({ color: '#FFFFFF', vertexColors: true, roughness: 0.85 });
  treeMat.onBeforeCompile = (sh) => {
    sh.uniforms.uTime = treeTime;
    sh.vertexShader = 'uniform float uTime;\n' + sh.vertexShader.replace('#include <begin_vertex>', `#include <begin_vertex>
      #ifdef USE_INSTANCING
        float ph = instanceMatrix[3].x * 0.013 + instanceMatrix[3].z * 0.007;
      #else
        float ph = 0.0;
      #endif
      float kk = max(0.0, position.y - 25.0) / 110.0;
      transformed.x += sin(uTime * 1.7 + ph) * kk * kk * 4.0;
      transformed.z += cos(uTime * 1.3 + ph) * kk * kk * 2.0;`);
  };
  const trees = w0.trees.map((g) => instanced(g, treeMat, 120, true));
  const rocks = instanced(w0.rock, vcMat, 80, true);
  const coinTex = canvasTex(128, 128, (x) => {
    const g = x.createRadialGradient(54, 50, 6, 64, 64, 64);
    g.addColorStop(0, '#FFE58A'); g.addColorStop(1, '#F2A81E');
    x.fillStyle = g; x.fillRect(0, 0, 128, 128);
    x.strokeStyle = '#FFFFFF'; x.lineWidth = 11; x.lineCap = 'round';
    x.beginPath(); x.moveTo(64, 30); x.lineTo(64, 98); x.moveTo(34, 47); x.lineTo(94, 81); x.moveTo(34, 81); x.lineTo(94, 47); x.stroke();
  });
  const coinGeo = new THREE.CylinderGeometry(11, 11, 3.4, 24).rotateX(Math.PI / 2);
  const coins = instanced(coinGeo, new THREE.MeshStandardMaterial({ color: '#FFFFFF', map: coinTex, metalness: 0.3, roughness: 0.32, emissive: '#7A5200', emissiveIntensity: 0.35 }), 320, true);
  const blobTex = canvasTex(64, 64, (x) => {
    const g = x.createRadialGradient(32, 32, 2, 32, 32, 31);
    g.addColorStop(0, 'rgba(31,42,77,0.22)'); g.addColorStop(0.6, 'rgba(31,42,77,0.1)'); g.addColorStop(1, 'rgba(31,42,77,0)');
    x.fillStyle = g; x.fillRect(0, 0, 64, 64);
  });
  const blobs = instanced(FLAT, new THREE.MeshBasicMaterial({ map: blobTex, transparent: true, depthWrite: false }), 400, false);
  // Soft round point sprites, sized in world units: one normal layer (snow), one additive (sparkles).
  const pScale = { value: 800 };
  const PMAX = 900;
  function pointsLayer(additive) {
    const geo = new THREE.BufferGeometry();
    const pos = new Float32Array(PMAX * 3);
    const col = new Float32Array(PMAX * 4);
    const size = new Float32Array(PMAX);
    geo.setAttribute('position', new THREE.BufferAttribute(pos, 3).setUsage(THREE.DynamicDrawUsage));
    geo.setAttribute('pcolor', new THREE.BufferAttribute(col, 4).setUsage(THREE.DynamicDrawUsage));
    geo.setAttribute('psize', new THREE.BufferAttribute(size, 1).setUsage(THREE.DynamicDrawUsage));
    const m = new THREE.ShaderMaterial({
      uniforms: { uScale: pScale },
      transparent: true,
      depthWrite: false,
      blending: additive ? THREE.AdditiveBlending : THREE.NormalBlending,
      vertexShader: 'uniform float uScale; attribute vec4 pcolor; attribute float psize; varying vec4 vC; void main() { vC = pcolor; vec4 mv = modelViewMatrix * vec4(position, 1.0); gl_PointSize = psize * uScale / -mv.z; gl_Position = projectionMatrix * mv; }',
      fragmentShader: `varying vec4 vC;
        void main() {
          float a = smoothstep(0.5, 0.12, length(gl_PointCoord - 0.5));
          gl_FragColor = vec4(vC.rgb, vC.a * a);
          #include <colorspace_fragment>
        }`,
    });
    const pts = new THREE.Points(geo, m);
    pts.frustumCulled = false;
    scene.add(pts);
    const L = { n: 0 };
    L.add = (x, y, z, s, c, a, boost) => {
      if (L.n >= PMAX) return;
      const i = L.n++;
      pos[i * 3] = x; pos[i * 3 + 1] = y; pos[i * 3 + 2] = z;
      col[i * 4] = c.r * boost; col[i * 4 + 1] = c.g * boost; col[i * 4 + 2] = c.b * boost; col[i * 4 + 3] = a;
      size[i] = s;
    };
    L.flush = () => {
      geo.setDrawRange(0, L.n);
      geo.attributes.position.needsUpdate = true;
      geo.attributes.pcolor.needsUpdate = true;
      geo.attributes.psize.needsUpdate = true;
      L.n = 0;
    };
    return L;
  }
  const softP = pointsLayer(false);
  const glowP = pointsLayer(true);
  // Ski spray lives only in the renderer: thrown off the outside ski, more with speed and carve.
  const spray = [];
  let sprayAcc = 0;
  const sprayCol = new THREE.Color('#F1F6FF');
  const dummy = new THREE.Object3D();
  const tmpColor = new THREE.Color();
  function put(inst, x, y, z, sx, sy, sz, ry, rz) {
    if (inst.n >= inst.max) return -1;
    dummy.position.set(x, y, z);
    dummy.rotation.set(0, ry || 0, rz || 0);
    dummy.scale.set(sx, sy, sz);
    dummy.updateMatrix();
    inst.im.setMatrixAt(inst.n, dummy.matrix);
    return inst.n++;
  }
  function flush(inst) {
    inst.im.count = inst.n;
    inst.im.instanceMatrix.needsUpdate = true;
    if (inst.im.instanceColor) inst.im.instanceColor.needsUpdate = true;
    inst.n = 0;
  }
  const blob = (x, z, y, rx, rz) => put(blobs, x, y + 0.8, z, rx * 2, 1, rz * 2);

  // ---------- Per-object visuals (built when they come into view) ----------
  const decalCache = new Map();
  function decal(key, w, h, draw) {
    if (decalCache.has(key)) return decalCache.get(key);
    const m = new THREE.MeshStandardMaterial({ map: canvasTex(w, h, draw), transparent: true, depthWrite: false, roughness: 0.4, polygonOffset: true, polygonOffsetFactor: -2 });
    decalCache.set(key, m);
    return m;
  }
  function flatDecal(m, w, h) { const d = new THREE.Mesh(FLAT, m); d.scale.set(w, 1, h); d.receiveShadow = true; return d; }
  const fur = mat('#B5774A', { roughness: 0.95, sheen: 0.8, sheenColor: '#E8B98A', sheenRoughness: 0.6 });
  const furDark = mat('#8E5733', { roughness: 0.95 });
  const cream = mat('#F6DDB8', { roughness: 0.9 });
  const snowWhite = mat('#FFFFFF', { roughness: 0.75, normalMap: FLUFF_N, normalScale: new THREE.Vector2(0.4, 0.4) });

  const builders = {
    snowman() {
      const g = new THREE.Group();
      part(g, SPH, snowWhite, [0, 17, 0], [18, 17, 17]);
      part(g, SPH, snowWhite, [0, 42, 0], [13, 13, 13]);
      part(g, SPH, snowWhite, [0, 61, 0], [10, 10, 10]);
      part(g, new THREE.TorusGeometry(11, 3, 8, 20), mat('#FF6B5A', { roughness: 0.95, normalMap: KNIT_N }), [0, 52, 0], null, [Math.PI / 2, 0, 0]);
      part(g, SPH, mat('#FF4F6D', { roughness: 0.95, normalMap: KNIT_N }), [0, 66, -1], [10.6, 8, 10.6]);
      part(g, SPH, snowWhite, [0, 75, -1], [4, 4, 4]);
      part(g, new THREE.ConeGeometry(2.4, 11, 10), mat('#FF9A3D', { roughness: 0.6 }), [0, 60, 14], null, [Math.PI / 2, 0, 0]);
      for (const s of [-1, 1]) {
        part(g, SPH, lib.eyeMat, [s * 3.6, 64, 8.5], [1.5, 1.5, 1]);
        part(g, new THREE.CylinderGeometry(1.2, 1.2, 26, 6), mat('#7A4B2E', { roughness: 0.9 }), [s * 20, 48, 0], null, [0, 0, s * 1.0]);
      }
      return { root: g };
    },
    log() {
      const g = new THREE.Group();
      const bark = canvasTex(128, 32, (x) => {
        x.fillStyle = '#8E5E3A'; x.fillRect(0, 0, 128, 32);
        x.strokeStyle = 'rgba(60,35,20,0.5)'; x.lineWidth = 2;
        for (let i = 0; i < 18; i++) { const y = (i * 7) % 32; x.beginPath(); x.moveTo((i * 23) % 128, y); x.lineTo((i * 23) % 128 + 30, y + 1); x.stroke(); }
      }, { repeat: true });
      part(g, new THREE.CylinderGeometry(11, 11.5, 92, 16), mat('#FFFFFF', { map: bark, roughness: 0.95 }), [0, 11, 0], null, [0, 0, Math.PI / 2]);
      const ringMat = decal('logend', 64, 64, (x) => {
        x.fillStyle = '#E7C59A'; x.beginPath(); x.arc(32, 32, 31, 0, Math.PI * 2); x.fill();
        x.strokeStyle = '#B8875C'; x.lineWidth = 3;
        for (const r of [8, 16, 24]) { x.beginPath(); x.arc(32, 32, r, 0, Math.PI * 2); x.stroke(); }
      });
      for (const s of [-1, 1]) {
        const e = new THREE.Mesh(PLANE, ringMat);
        e.scale.set(22, 22, 1); e.position.set(s * 46.3, 11, 0); e.rotation.y = s * Math.PI / 2;
        g.add(e);
      }
      part(g, capsule(5, 70), snowWhite, [0, 21.5, -1], [1, 1, 1.4], [0, 0, Math.PI / 2]);
      return { root: g, tex: bark };
    },
    puff() {
      const g = new THREE.Group();
      const b = group(g);
      for (const s of [-1, 1]) part(g, SPH, mat('#8C7AE6', { roughness: 0.7 }), [s * 9, 3, 2], [7, 4, 8]);
      [[0, 22, 18], [-14, 17, 11], [14, 17, 11], [-10, 34, 11], [10, 34, 11], [0, 39, 10]].forEach(([x, y, r]) => part(b, lib.FLUFF, snowWhite, [x, y, -2], [r, r, r * 0.9]));
      const face = group(b);
      for (const s of [-1, 1]) {
        part(face, SPH, lib.eyeMat, [s * 6, 25, 14.5], [3, 4, 1.6]);
        part(face, capsule(1.4, 7), mat(INK, { roughness: 0.5 }), [s * 7, 31.5, 15], null, [0, 0, Math.PI / 2 + s * 0.45]);
        part(face, SPH, lib.blushMat, [s * 12.5, 20, 12], [3, 2, 1]);
      }
      part(face, new THREE.TorusGeometry(3.6, 1.1, 6, 12, Math.PI), mat(INK), [0, 14, 16]);
      return { root: g, b, face };
    },
    ball() {
      const g = new THREE.Group();
      const s = part(g, lib.FLUFF, snowWhite, [0, 0, 0], [1, 1, 1]);
      return { root: g, s };
    },
    bear() {
      const g = new THREE.Group();
      const mound = part(g, lib.FLUFF, snowWhite, [0, 0, 0], [38, 18, 30]);
      mound.receiveShadow = true;
      const bear = group(g);
      part(bear, SPH, fur, [0, 22, 0], [21, 19, 17]);
      part(bear, SPH, cream, [0, 20, 12], [13, 12, 8]);
      part(bear, new THREE.TorusGeometry(17, 4.5, 8, 24), mat('#4CC096', { roughness: 0.95, normalMap: KNIT_N }), [0, 37, 0], null, [Math.PI / 2, 0, 0]);
      const arms = [];
      for (const s of [-1, 1]) {
        const pv = group(bear, [s * 17, 32, 2]);
        part(pv, capsule(6, 18), fur, [0, 14, 0]);
        part(pv, SPH, furDark, [0, 27, 0], [7.5, 7.5, 7.5]);
        arms.push({ pv, s });
        part(bear, SPH, fur, [s * 16, 76, 0], [8, 8, 5]);
        part(bear, SPH, cream, [s * 16, 76, 3.5], [4.2, 4.2, 2]);
        part(bear, SPH, lib.eyeMat, [s * 8, 63, 18], [3.2, 3.8, 2]);
        part(bear, SPH, lib.blushMat, [s * 14, 54, 16], [3.6, 2.2, 1]);
      }
      part(bear, SPH, fur, [0, 58, 0], [22, 21, 20]);
      part(bear, SPH, cream, [0, 51, 15], [11, 8.5, 8]);
      part(bear, SPH, lib.eyeMat, [0, 55, 23], [4.5, 3.2, 3]);
      const mouthIn = part(bear, SPH, mat('#7A2E3B', { roughness: 0.5 }), [0, 45.5, 21], [3.6, 3.2, 2]);
      return { root: g, bear, arms, mouthIn };
    },
    hole(o) {
      const w = o.hw * 2;
      const m = decal('hole' + Math.round(w), 256, 96, (x) => ART.hole(x, 128, 44, 220, 70, 0.3));
      const g = new THREE.Group(); g.add(flatDecal(m, w * 1.16, o.h * 1.5));
      return { root: g };
    },
    ice(o) {
      const m = decal('ice', 256, 110, (x) => ART.ice(x, 128, 55, 240, 96));
      const g = new THREE.Group(); g.add(flatDecal(m, o.w * 1.07, o.h * 1.15));
      return { root: g };
    },
    item(o) {
      const tex = canvasTex(96, 96, (x) => { x.translate(48, 84); ART.item(x, 0, 0, 1.8, 0, o.kind); });
      const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, transparent: true }));
      sp.scale.set(64, 64, 1);
      sp.center.set(0.5, 0.125);
      const g = new THREE.Group(); g.add(sp);
      if (o.kind === 'crystal') {
        const glow = new THREE.Sprite(glowMat(1.1, 2.5, 3));
        glow.scale.set(84, 84, 1);
        glow.position.y = 35;
        g.add(glow);
      }
      return { root: g, tex };
    },
    sign(o) {
      const g = new THREE.Group();
      part(g, rbox(8, 76, 8, 3), mat('#7A4B2E', { roughness: 0.9 }), [0, 38, 0]);
      const tex = canvasTex(320, 80, (x) => { x.scale(2, 2); x.translate(80, 98); ART.sign(x, 0, 0, 1, o.left, o.right); });
      const board = new THREE.Mesh(PLANE, new THREE.MeshBasicMaterial({ map: tex, transparent: true }));
      board.scale.set(160, 40, 1); board.position.set(0, 78, 5); board.rotation.x = -0.35;
      g.add(board);
      return { root: g, tex };
    },
    mogul(d) {
      const g = new THREE.Group();
      const m = part(g, lib.FLUFF, snowWhite, [0, 0, 0], [30 * d.s, 9 * d.s, 16 * d.s]);
      m.castShadow = false; m.receiveShadow = true;
      return { root: g };
    },
    shade(d) {
      const m = decal('shade', 64, 32, (x) => { const gr = x.createRadialGradient(32, 16, 1, 32, 16, 30); gr.addColorStop(0, 'rgba(200,216,250,0.55)'); gr.addColorStop(1, 'rgba(200,216,250,0)'); x.fillStyle = gr; x.save(); x.scale(1, 0.5); x.fillRect(0, 0, 64, 64); x.restore(); });
      const g = new THREE.Group(); g.add(flatDecal(m, d.w * 1.2, d.h * 1.6));
      return { root: g };
    },
  };

  const stripeTex = canvasTex(16, 64, (x) => { x.fillStyle = '#FFFFFF'; x.fillRect(0, 0, 16, 64); x.fillStyle = '#FF5C93'; for (let y = 0; y < 64; y += 22) x.fillRect(0, y, 16, 11); }, { repeat: true });
  stripeTex.repeat.set(1, 1.6);
  const poleMat = mat('#FFFFFF', { map: stripeTex, roughness: 0.35, clearcoat: 0.6 });
  const poleGeo = new THREE.CylinderGeometry(5, 5, 104, 14);
  function buildRow(row) {
    const g = new THREE.Group();
    const n = row.n;
    const xs = [];
    for (let i = 0; i <= n; i++) xs.push(-195 + (390 / n) * i);
    xs.forEach((x, i) => {
      const px = i === 0 ? x + 14 : i === n ? x - 14 : x;
      part(g, poleGeo, poleMat, [px, 52, 0]);
      part(g, SPH, mat('#FFFFFF', { roughness: 0.4 }), [px, 106, 0], [6, 6, 6]);
    });
    const banners = [];
    for (let i = 0; i < n; i++) {
      const cx = (xs[i] + xs[i + 1]) / 2;
      const w = Math.min(xs[i + 1] - xs[i] - 26, 124);
      const c = document.createElement('canvas');
      c.width = 256; c.height = 136;
      const tex = new THREE.CanvasTexture(c);
      tex.colorSpace = THREE.SRGBColorSpace;
      const plane = new THREE.Mesh(PLANE, new THREE.MeshBasicMaterial({ map: tex, transparent: true }));
      plane.scale.set(w + 8, (w + 8) * 136 / 256, 1);
      plane.position.set(cx, 74, 6);
      plane.rotation.x = -0.4;
      g.add(plane);
      banners.push({ c, tex, w, i });
    }
    return { root: g, banners, key: '' };
  }
  function paintRow(v, row) {
    const key = (row.done ? 1 : 0) + ':' + row.picked + ':' + (row.reveal ? 1 : 0);
    if (v.key === key) return;
    v.key = key;
    v.banners.forEach((b) => {
      const x = b.c.getContext('2d');
      x.setTransform(1, 0, 0, 1, 0, 0);
      x.clearRect(0, 0, 256, 136);
      const s = 256 / (b.w + 8);
      x.setTransform(s, 0, 0, s, 0, 0);
      const o = row.q.options[b.i];
      let fill = o.color || '#3FA9F5';
      if (row.done && b.i === row.correct) fill = '#2FB37A';
      else if (row.done && b.i === row.picked) fill = '#9AA3B8';
      else if (row.reveal) fill = b.i === row.correct ? '#FFB81F' : '#9AA3B8';
      const W = b.w; const H = 58; const ox = 4; const oy = 4;
      ART.box(x, ox, oy + 5, W, H, 16, INK);
      ART.box(x, ox, oy, W, H, 16, fill, INK, 3);
      ART.box(x, ox + 8, oy + 4, W - 16, 10, 6, 'rgba(255,255,255,0.35)');
      if (o.shape) ART.shape(x, o.shape, ox + W / 2, oy + H / 2, 15, o.shapeColor || '#FFFFFF');
      else ART.text(x, o.label, ox + W / 2, oy + H / 2 + 1, String(o.label).length > 3 ? 20 : 30, '#FFFFFF');
      if ((row.done || row.reveal) && b.i === row.correct) ART.text(x, '✓', ox + W - 10, oy + 10, 20, '#7DE0A8');
      b.tex.needsUpdate = true;
    });
  }
  function buildArch(label, color) {
    const g = new THREE.Group();
    for (const s of [-1, 1]) part(g, rbox(14, 124, 14, 5), mat('#A66A40', { roughness: 0.85 }), [s * 150, 62, 0]);
    const tex = canvasTex(640, 80, (x) => {
      x.scale(2, 2);
      ART.box(x, 4, 4, 312, 32, 12, '#FFF4E0', INK, 3);
      if (label === 'FINISH') {
        x.save(); x.beginPath(); x.rect(6, 6, 308, 28); x.clip();
        for (let cx = 6, i = 0; cx < 314; cx += 8, i++) for (let r = 0; r < 4; r++) if ((i + r) % 2 === 0) { x.fillStyle = 'rgba(43,33,64,0.18)'; x.fillRect(cx, 6 + r * 7, 8, 7); }
        x.restore();
      }
      ART.text(x, label, 160, 21, 22, color);
    });
    const board = new THREE.Mesh(PLANE, new THREE.MeshBasicMaterial({ map: tex, transparent: true }));
    board.scale.set(330, 41, 1); board.position.set(0, 138, 4); board.rotation.x = -0.3;
    g.add(board);
    part(g, rbox(330, 40, 8, 6), mat('#FFF4E0', { roughness: 0.6 }), [0, 138, -1], null, [-0.3, 0, 0]);
    return { root: g, tex };
  }

  const live = new Map();
  let seen = new Set();
  function vis(key, build) {
    let v = live.get(key);
    if (!v) { v = build(); scene.add(v.root); live.set(key, v); }
    v.root.visible = true;
    seen.add(key);
    return v;
  }
  function sweep() {
    for (const [k, v] of live) {
      if (seen.has(k)) continue;
      scene.remove(v.root);
      if (v.tex) v.tex.dispose();
      if (v.banners) v.banners.forEach((b) => { b.tex.dispose(); b.c.width = 0; });
      live.delete(k);
    }
    seen = new Set();
  }

  // ---------- Trail, flakes ----------
  const TRAIL_MAX = 240;
  const trailGeo = new THREE.BufferGeometry();
  const trailPos = new Float32Array(TRAIL_MAX * 2 * 2 * 3);
  trailGeo.setAttribute('position', new THREE.BufferAttribute(trailPos, 3));
  const trailIdx = [];
  for (let k = 0; k < 2; k++) {
    const base = k * TRAIL_MAX * 2;
    for (let i = 0; i < TRAIL_MAX - 1; i++) { const a = base + i * 2; trailIdx.push(a, a + 1, a + 2, a + 1, a + 3, a + 2); }
  }
  trailGeo.setIndex(trailIdx);
  const trailUv = new Float32Array(TRAIL_MAX * 2 * 2 * 2);
  for (let k = 0; k < 2; k++) for (let i = 0; i < TRAIL_MAX; i++) { const a = (k * TRAIL_MAX * 2 + i * 2) * 2; trailUv[a] = 0; trailUv[a + 1] = i / TRAIL_MAX; trailUv[a + 2] = 1; trailUv[a + 3] = i / TRAIL_MAX; }
  trailGeo.setAttribute('uv', new THREE.BufferAttribute(trailUv, 2));
  const grooveTex = canvasTex(32, 4, (x) => {
    const gr = x.createLinearGradient(0, 0, 32, 0);
    gr.addColorStop(0, 'rgba(150,175,220,0)'); gr.addColorStop(0.22, 'rgba(112,142,200,0.78)'); gr.addColorStop(0.5, 'rgba(190,208,240,0.5)');
    gr.addColorStop(0.78, 'rgba(112,142,200,0.78)'); gr.addColorStop(1, 'rgba(150,175,220,0)');
    x.fillStyle = gr; x.fillRect(0, 0, 32, 4);
  });
  const trail = new THREE.Mesh(trailGeo, new THREE.MeshBasicMaterial({ map: grooveTex, transparent: true, depthWrite: false, side: THREE.DoubleSide }));
  trail.frustumCulled = false;
  scene.add(trail);

  const flakeTex = canvasTex(32, 32, (x) => { const g = x.createRadialGradient(16, 16, 1, 16, 16, 15); g.addColorStop(0, 'rgba(255,255,255,1)'); g.addColorStop(0.6, 'rgba(255,255,255,0.8)'); g.addColorStop(1, 'rgba(255,255,255,0)'); x.fillStyle = g; x.fillRect(0, 0, 32, 32); });
  const flakeGeo = new THREE.BufferGeometry();
  const flakePos = new Float32Array(200 * 3);
  flakeGeo.setAttribute('position', new THREE.BufferAttribute(flakePos, 3));
  const flakes = new THREE.Points(flakeGeo, new THREE.PointsMaterial({ map: flakeTex, size: 7, sizeAttenuation: false, transparent: true, depthWrite: false, fog: false }));
  flakes.frustumCulled = false;
  camera.add(flakes);
  scene.add(camera);

  // ---------- Frame ----------
  let lastW = 0;
  let lastH = 0;
  let worldKey = null;
  const v3 = new THREE.Vector3();
  const pipScreen = { x: 0, y: 0 };
  const camS = { x: 0, fov: 30, bump: 0, bumpV: 0, wasAir: false, flipped: false, lastT: 0 };

  function render() {
    const w = frameEl.clientWidth;
    const h = frameEl.clientHeight;
    applyQuality();
    if (w !== lastW || h !== lastH) {
      lastW = w; lastH = h;
      const pr = Math.min(window.devicePixelRatio || 1, quality === 'high' ? 2 : 1.5);
      renderer.setPixelRatio(pr);
      renderer.setSize(w, h, false);
      if (composer) { composer.setPixelRatio(pr); composer.setSize(w, h); }
      canvas3d.style.width = w + 'px';
      canvas3d.style.height = h + 'px';
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      pScale.value = h * pr / (2 * Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)));
      PS.ctx.setTransform(1, 0, 0, 1, 0, 0);
      PS.ctx.clearRect(0, 0, PS.ctx.canvas.width, PS.ctx.canvas.height);
    }
    const g = PS.G;
    const t = PS.clock;
    const L = g ? g.L : null;
    const wname = (L && L.world) || 'meadow';
    const world = ART.WORLD[wname];
    if (worldKey !== wname) {
      worldKey = wname;
      const geos = sceneryGeos(world);
      trees.forEach((tr, i) => { tr.im.geometry = geos.trees[i]; });
      rocks.im.geometry = geos.rock;
      scene.fog.color.set(world.shade);
      hemi.groundColor.set(wname === 'sunset' ? '#E8C2C0' : '#B7CBF2');
      sun.color.set(wname === 'sunset' ? '#FFD2A6' : '#FFF6E8');
      bd.setWorld(wname);
    }
    const blizz = L && L.weather === 'blizzard';
    scene.fog.near = blizz ? 1300 : 2300;
    scene.fog.far = blizz ? 2700 : 4300;
    sun.intensity = blizz ? 1.5 : 2.4;
    const pz = g ? g.z : 0;
    const px = g ? g.x : 0;
    const course = g ? g.course : PS.titleCourse;
    const shake = g && g.shake > 0 ? g.shake : 0;

    // Camera feel: slight lag on steering, a little zoom-out with speed, a springy dip on landing.
    const dt = Math.min(0.05, Math.max(0, t - camS.lastT));
    camS.lastT = t;
    const vMin = g ? g.L.v0 * g.mul * 0.85 : 1;
    const vMax = g ? g.L.v1 * g.mul : 2;
    const spd = g && g.started ? Math.max(0, Math.min(1, (g.v - vMin) / Math.max(1, vMax - vMin))) : 0;
    if (g) {
      if (camS.wasAir && !g.air && g.tumble <= 0) camS.bumpV -= camS.flipped ? 150 : 90;
      camS.flipped = !!g.flip || (camS.flipped && g.air);
      camS.wasAir = g.air;
    }
    camS.bumpV += (-camS.bump * 150 - camS.bumpV * 13) * dt;
    camS.bump += camS.bumpV * dt;
    camS.fov += (30 + spd * 3 + (g && g.flip ? 1.2 : 0) - camS.fov) * Math.min(1, dt * 3);
    camS.x += (px * CAM.follow - camS.x) * Math.min(1, dt * 6);
    if (Math.abs(camera.fov - camS.fov) > 0.01) {
      camera.fov = camS.fov;
      camera.updateProjectionMatrix();
      pScale.value = lastH * renderer.getPixelRatio() / (2 * Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)));
    }
    camera.position.set(camS.x + (Math.random() - 0.5) * 10 * shake, CAM.h + camS.bump + (Math.random() - 0.5) * 10 * shake, pz + CAM.d);
    camera.rotation.set(-CAM.pitch, 0, 0);
    camera.updateMatrixWorld();
    // Sun from the front-left, high; the shadow box follows the view, snapped to texels to avoid shimmer.
    const tz = Math.round((pz + 160) / 4) * 4;
    const tx = Math.round(camS.x / 4) * 4;
    sun.target.position.set(tx, 0, tz);
    sun.position.set(tx - 520, 1100, tz + 620);
    rim.target.position.set(tx, 40, tz - 160);
    rim.position.set(tx + 380, 520, tz - 1100);
    treeTime.value = t;

    v3.set(camS.x, -6, pz + CREST).project(camera);
    bd.update(t, g ? g.x : Math.sin(t * 0.3) * 60, v3.y, camera.fov, camera.aspect);

    ground.position.set(0, 0, pz + GROUND_OFF);
    const off = -(pz + GROUND_OFF + 850);
    snowTex.offset.set(0, (off / TILE) % 1);
    snowN.offset.set(0, (off / 160) % 1);
    glintTex.offset.copy(snowN.offset);
    lip.position.set(0, -22, pz + CREST - 18);

    const zMin = pz + CREST - 260;
    const zMax = pz + 820;
    const Y = (z) => bendY(z - pz);
    const inView = (z) => z > zMin && z < zMax;

    if (course) {
      for (const d of course.decor) {
        if (!inView(d.z)) continue;
        const v = vis(d, () => builders[d.type === 'shade' ? 'shade' : 'mogul'](d));
        v.root.position.set(d.x, Y(d.z) + (d.type === 'shade' ? 0.3 : 0), d.z);
      }
      for (const o of course.objs) {
        if (o.z > zMax) break;
        if (o.gone || o.z < zMin) continue;
        const y = Y(o.z);
        switch (o.type) {
          case 'tree': {
            const k = Math.floor(Math.abs(o.x * 7 + o.z * 3)) % 3;
            const i = put(trees[k], o.x, y, o.z, o.s, o.s * (0.95 + k * 0.05), o.s, o.x * 0.1 + o.z * 0.01);
            if (i >= 0) { tmpColor.setHSL(0, 0, 0.9 + (Math.abs(o.z * 13 + o.x) % 10) / 100); trees[k].im.setColorAt(i, tmpColor); }
            blob(o.x, o.z, y, 26 * o.s, 12 * o.s);
            break;
          }
          case 'rock': put(rocks, o.x, y, o.z, o.s * 1.3, o.s * 1.3, o.s * 1.3, o.z * 0.01); blob(o.x, o.z, y, 28 * o.s, 12 * o.s); break;
          case 'coin': put(coins, o.x, y + 16 + Math.sin(t * 3 + (o.ph || 0)) * 2, o.z, 1, 1, 1, t * 4 + (o.ph || 0)); break;
          case 'snowman': { const v = vis(o, builders.snowman); v.root.position.set(o.x, y, o.z); break; }
          case 'log': { const v = vis(o, builders.log); v.root.position.set(o.x, y, o.z); break; }
          case 'puff': {
            const v = vis(o, builders.puff);
            const bb = Math.abs(Math.sin((t + o.ph) * 7));
            const hop = o.hopT > 0 ? 0.9 : 1;
            v.root.position.set(o.x, y, o.z);
            v.b.position.y = bb * 7;
            v.b.scale.set((1 + (1 - bb) * 0.08) * hop, (1 - (1 - bb) * 0.08) * hop, hop);
            v.face.position.x = (o.dir || 1) * 2;
            v.root.rotation.y = (o.dir || 1) * 0.25;
            break;
          }
          case 'ball': {
            const v = vis(o, builders.ball);
            if (!o.active) { v.root.visible = false; break; }
            v.root.position.set(o.x, y + o.rad, o.z);
            v.s.scale.setScalar(o.rad);
            v.s.rotation.z = -o.rot;
            break;
          }
          case 'bear': {
            const v = vis(o, builders.bear);
            const up = Math.max(0, Math.min(1, o.up || 0));
            const warn = o.warn && up < 0.05;
            const shk = warn ? Math.sin(t * 46 + (o.ph || 0)) * 2.4 : 0;
            v.root.position.set(o.x + shk, y, o.z);
            v.bear.position.y = warn ? -58 : -80 + up * 80;
            v.bear.visible = warn || up > 0.01;
            v.bear.scale.setScalar(0.6 + 0.4 * Math.max(up, warn ? 0.4 : 0));
            const wave = Math.sin(t * 9 + (o.ph || 0)) * 0.25;
            v.arms.forEach(({ pv, s }) => pv.rotation.set(0, 0, s * (-0.35 - wave * s)));
            v.mouthIn.scale.y = 3 + Math.abs(Math.sin(t * 5 + (o.ph || 0))) * 1.5;
            break;
          }
          case 'hole': { const v = vis(o, () => builders.hole(o)); v.root.position.set(o.x, y + 0.5, o.z); break; }
          case 'ice': { const v = vis(o, () => builders.ice(o)); v.root.position.set(o.x, y + 0.4, o.z); break; }
          case 'item': { const v = vis(o, () => builders.item(o)); v.root.position.set(o.x, y + Math.sin(t * 3) * 4, o.z); blob(o.x, o.z, y, 15, 6); break; }
          case 'sign': { const v = vis(o, () => builders.sign(o)); v.root.position.set(o.x, y, o.z); break; }
          default: break;
        }
      }
      for (const row of course.rows) {
        if (!inView(row.z) || row.smashed) continue;
        const v = vis(row, () => buildRow(row));
        paintRow(v, row);
        v.root.position.set(0, Y(row.z), row.z);
      }
      if (g) {
        const sv = vis('START', () => buildArch('START', '#3FA9F5'));
        sv.root.position.set(0, Y(-40), -40);
        sv.root.visible = inView(-40);
        if (!course.endless) {
          const fv = vis('FINISH', () => buildArch('FINISH', '#FF5C93'));
          fv.root.position.set(0, Y(course.len), course.len);
          fv.root.visible = inView(course.len);
        }
      }
    }
    sweep();

    drawPlayer(g, t);
    if (g && g.rival && inView(g.rival.z)) {
      const r = g.rival;
      rival.root.visible = true;
      rival.root.position.set(r.x, Y(r.z), r.z);
      rival.update({ t, lean: r.lean, carve: r.lean * 0.5, look: r.lean, bob: 3 + Math.sin(t * 5.2 + 1) * 3, pump: Math.sin(t * 7 + 1) });
      rival.root.scale.multiplyScalar(PLAYER_SCALE * 0.9);
    } else rival.root.visible = false;

    let tn = 0;
    if (g && g.trail.length > 1) {
      const pts = g.trail.slice(-TRAIL_MAX);
      tn = pts.length;
      for (let k = 0; k < 2; k++) {
        const offx = k === 0 ? -14 : 14;
        for (let i = 0; i < TRAIL_MAX; i++) {
          const p = pts[Math.min(i, tn - 1)];
          const wdt = p.air || p.z < pz + CREST ? 0 : 4.2;
          const y = Y(p.z) + 0.6;
          const a = (k * TRAIL_MAX * 2 + i * 2) * 3;
          trailPos[a] = p.x + offx - wdt; trailPos[a + 1] = y; trailPos[a + 2] = p.z;
          trailPos[a + 3] = p.x + offx + wdt; trailPos[a + 4] = y; trailPos[a + 5] = p.z;
        }
      }
      trailGeo.attributes.position.needsUpdate = true;
    }
    trail.visible = tn > 1;

    for (const p of PS.particles) {
      const k = Math.max(0, p.life / p.max);
      tmpColor.set(p.color);
      if (p.kind === 'spark') glowP.add(p.x, Y(p.z) + p.h, p.z, p.size * 2.6, tmpColor, k, 4);
      else softP.add(p.x, Y(p.z) + p.h, p.z, p.size * (2.2 + (1 - k) * 1.4), tmpColor, 0.9 * k, 1);
    }
    if (g && g.started && !g.air && g.tumble <= 0 && !g.finished && dt > 0) {
      const lean = g.lean || 0;
      sprayAcc += (18 + spd * 70) * (0.35 + Math.min(1, Math.abs(lean)) * 1.8) * dt;
      const side = Math.sign(lean) || (Math.random() < 0.5 ? -1 : 1);
      while (sprayAcc >= 1 && spray.length < 260) {
        sprayAcc -= 1;
        spray.push({
          x: g.x + (Math.random() - 0.5) * 26, y: 3, z: g.z - 6 + Math.random() * 12,
          vx: -side * (40 + Math.random() * 90) * (0.35 + Math.abs(lean)), vy: 45 + Math.random() * 85, vz: g.v * (0.12 + Math.random() * 0.25),
          life: 0.45 + Math.random() * 0.35, max: 0.8, s: 6 + Math.random() * 7,
        });
      }
    }
    for (let i = spray.length - 1; i >= 0; i--) {
      const q = spray[i];
      q.life -= dt;
      if (q.life <= 0) { spray.splice(i, 1); continue; }
      q.vy -= 300 * dt; q.x += q.vx * dt; q.y = Math.max(1, q.y + q.vy * dt); q.z += q.vz * dt;
      const k = q.life / q.max;
      softP.add(q.x, Y(q.z) + q.y, q.z, q.s * (1.9 - k), sprayCol, 0.85 * k, 1);
    }

    const fd = 120;
    const hh = Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)) * fd;
    const hw = hh * camera.aspect;
    let fi = 0;
    for (const f of PS.flakes) {
      if (fi >= 200) break;
      flakePos[fi * 3] = (f.x / PS.LW * 2 - 1) * hw;
      flakePos[fi * 3 + 1] = (1 - f.y / PS.LH * 2) * hh;
      flakePos[fi * 3 + 2] = -fd;
      fi++;
    }
    flakeGeo.setDrawRange(0, fi);
    flakeGeo.attributes.position.needsUpdate = true;
    flakes.visible = !!L && (L.weather === 'snow' || L.weather === 'blizzard');
    flakes.material.size = blizz ? 9 : 7;

    trees.forEach(flush); flush(rocks); flush(coins); flush(blobs);
    softP.flush(); glowP.flush();
    if (quality === 'high' && composer) composer.render(dt);
    else {
      renderer.setRenderTarget(null);
      renderer.clear();
      renderer.render(bd.scene, bd.cam);
      renderer.clearDepth();
      renderer.render(scene, camera);
    }
  }

  function drawPlayer(g, t) {
    const pr = player.root;
    if (!g) {
      const ph = t % 6;
      const waving = ph > 4.4 && ph < 5.8;
      const hop = ph > 4.4 && ph < 4.8 ? Math.sin((ph - 4.4) / 0.4 * Math.PI) * 16 : 0;
      const expr = waving ? 'happy' : ph > 2.4 && ph < 2.8 ? 'wink' : 'normal';
      pr.visible = true;
      pr.position.set(0, hop, 150);
      pr.rotation.y = Math.sin(t * 0.5) * 0.25;
      player.update({ t, expr, look: waving ? 0 : Math.sin(t * 0.9) * 0.9, blink: blinkAt(t), bob: Math.sin(t * 3) * 2.5, squash: hop > 0 ? -0.4 : 0, arms: waving ? 'wave' : 'poles', pump: Math.sin(t * 2) * 0.2, pom: Math.sin(t * 3 - 0.6) * 2, headShake: Math.sin(t * 1.3) * 0.05 });
      pr.scale.multiplyScalar(0.95);
      blob(0, 150, 0, 34 - hop * 0.6, 15);
      shield.visible = false; magnet.visible = false; aura.visible = false; auraCore.visible = false; auraPool.visible = false;
      return;
    }
    const skiing = g.started && !g.finished && g.tumble <= 0;
    const tumbling = g.tumble > 0;
    let expr = 'normal';
    if (tumbling) expr = 'dizzy';
    else if (g.finished || g.powerT > 0) expr = 'excited';
    else if (g.happyT > 0) expr = 'happy';
    else if (g.sadT > 0) expr = 'sad';
    else if (g.scaredT > 0) expr = 'scared';
    else if (g.flip) expr = 'excited';
    else if (g.air) expr = 'happy';
    else if (g.row || g.icy > 0 || g.v > g.L.v1 * g.mul * 0.95) expr = 'determined';
    const airH = g.air ? Math.max(0, g.airH) : 0;
    const airK = g.air ? Math.min(1, airH / 40) : 0;
    const crouch = Math.max(0, Math.min(1, (g.v - 250) / 150)) * 4;
    const x = g.x + (g.sway || 0) * 8;
    const fy = g.finished ? Math.abs(Math.sin(t * 7)) * 18 : 0;
    const blinkOff = g.invuln > 0 && !tumbling && Math.floor(t * 12) % 2 === 0;
    pr.visible = !blinkOff;
    pr.position.set(x, airH + fy, g.z);
    v3.set(x, airH + fy + 64, g.z).project(camera);
    pipScreen.x = (v3.x + 1) * 0.5 * lastW;
    pipScreen.y = (1 - v3.y) * 0.5 * lastH;
    PS.pipScreen = pipScreen;
    player.update({
      t, expr,
      lean: tumbling ? 0 : g.lean + (g.sway || 0) * 0.35,
      carve: tumbling ? 0 : g.lean * 0.55 + (g.sway || 0) * 0.22,
      look: g.lookT > 0 ? g.lookX : g.lean * 0.8,
      mouth: g.air ? 0.6 + airK * 0.4 : undefined,
      bob: skiing && !g.air ? 3 + Math.sin(t * 5.2) * 3 + crouch : Math.sin(t * 3) * 2,
      headBob: skiing && !g.air ? Math.sin(t * 5.2 - 0.8) * 1.5 : 0,
      squash: g.squashT > 0 ? g.squashT / 0.18 : g.air && g.airT < 0.1 ? -0.7 : skiing ? 0.06 + Math.sin(t * 5.2) * 0.05 + crouch * 0.02 : 0,
      tuck: g.flip ? 1 : airK,
      flip: g.flipA || 0,
      arms: g.finished ? 'wave' : g.air || g.happyT > 0 || g.powerT > 0 ? 'up' : 'poles',
      pump: skiing && !g.air ? Math.sin(t * 7) : 0,
      blink: blinkAt(t),
      spin: tumbling ? (1 - g.tumble / PS.TUMBLE) * Math.PI * 2 : 0,
      headShake: g.sadT > 0 ? Math.sin(t * 30) * 0.12 : 0,
      pom: Math.sin(t * 14 - 0.8) * (skiing ? 3.5 : 1.5),
    });
    pr.rotation.y = tumbling ? 0 : -(g.lean || 0) * 0.18;
    pr.scale.multiplyScalar(PLAYER_SCALE);
    blob(x, g.z, 0, 21 * (1 - airH / 120), 9 * (1 - airH / 120));
    shield.visible = !!g.shield;
    if (shield.visible) { shield.position.set(x, airH + 38, g.z); shield.material.opacity = 0.25 + Math.sin(t * 6) * 0.06; }
    magnet.visible = g.magnet > 0;
    if (magnet.visible) { magnet.position.set(x, 1.5, g.z); magnet.rotation.z = t * 2; }
    // Glow fades in on pickup and flickers through the last 1.5 s as a warning.
    const pw = g.power > 0 ? Math.min(1, g.power / 0.4) * (g.power < 1.5 ? 0.55 + 0.45 * Math.abs(Math.sin(t * 14)) : 1) : 0;
    aura.visible = auraCore.visible = auraPool.visible = pw > 0.01;
    if (aura.visible) {
      const s = 150 + Math.sin(t * 9) * 12;
      aura.position.set(x, airH + fy + 30, g.z);
      aura.scale.set(s, s, 1);
      aura.material.opacity = 0.75 * pw;
      auraCore.position.copy(aura.position);
      auraCore.scale.set(s * 0.6, s * 0.6, 1);
      auraCore.material.color.setRGB(1.4 * pw, 2.8 * pw, 3.2 * pw);
      auraPool.position.set(x, 0.8, g.z);
      auraPool.scale.set(200, 200, 1);
      auraPool.material.opacity = 0.62 * pw;
    }
  }

  PS.render3d = render;
  if (/[?&]debug\b/.test(location.search)) PS.debug3d = { THREE, renderer, scene, camera, bd, sun, hemi };

  // Small second renderer for Pip on the level map (its own WebGL context, its own Pip).
  let mini = null;
  PS.mapPip3d = function (canvas, t) {
    if (!mini) {
      try {
        const r = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
        r.setClearColor(0x000000, 0);
        r.toneMapping = THREE.NeutralToneMapping;
        r.setPixelRatio(1);
        r.setSize(canvas.width, canvas.height, false);
        const sc = new THREE.Scene();
        sc.add(new THREE.HemisphereLight('#FFFFFF', '#B7CBF2', 1.6));
        const d = new THREE.DirectionalLight('#FFF6E8', 2.4);
        d.position.set(-160, 260, 260);
        sc.add(d);
        const cam = new THREE.PerspectiveCamera(26, canvas.width / canvas.height, 10, 2000);
        cam.position.set(0, 150, 360);
        cam.lookAt(0, 66, 0);
        const pen = makePenguin(lib);
        sc.add(pen.root);
        mini = { r, sc, cam, pen };
      } catch (err) {
        PS.mapPip3d = null;
        return false;
      }
    }
    const hop = Math.max(0, Math.sin(t * 3)) * 5;
    mini.pen.update({ t, expr: 'happy', arms: 'wave', blink: blinkAt(t), bob: Math.sin(t * 3) * 2, pom: Math.sin(t * 3) * 2 });
    mini.pen.root.position.y = hop;
    mini.pen.root.rotation.y = Math.sin(t * 0.8) * 0.3;
    mini.r.render(mini.sc, mini.cam);
    return true;
  };
  document.documentElement.classList.add('r3d');
}

// ---------- Backdrop: gradient sky with sun glow, two hazy mountain ranges, drifting clouds ----------
function noise2(x, y) {
  const xi = Math.floor(x); const yi = Math.floor(y);
  const xf = x - xi; const yf = y - yi;
  const a = hash2(xi, yi); const b = hash2(xi + 1, yi); const c = hash2(xi, yi + 1); const d = hash2(xi + 1, yi + 1);
  const sx = xf * xf * (3 - 2 * xf); const sy = yf * yf * (3 - 2 * yf);
  return a + (b - a) * sx + (c - a) * sy + (a - b - c + d) * sx * sy;
}
function ridge(x, y) {
  let v = 0; let amp = 1; let f = 1;
  for (let o = 0; o < 5; o++) { const n = 1 - Math.abs(noise2(x * f, y * f) * 2 - 1); v += n * n * amp; amp *= 0.5; f *= 2.03; }
  return v / 1.94;
}
const SKY = {
  meadow: { top: '#2F7FE0', mid: '#8CC8FF', hor: '#E2F0FF', sun: '#FFF1D2', rock: '#5F70A0', forest: '#2F6B58', snow: '#F4F8FF', fog: '#D6E8FF' },
  sunset: { top: '#5468C8', mid: '#FFB48C', hor: '#FFE3C6', sun: '#FFC98A', rock: '#8D7695', forest: '#5A6B6A', snow: '#FFF0E8', fog: '#FFD9C4' },
};
function makeBackdrop() {
  const scene = new THREE.Scene();
  const cam = new THREE.PerspectiveCamera(30, 0.46, 50, 30000);
  cam.position.set(0, 400, 0);
  const u = { top: { value: new THREE.Color() }, mid: { value: new THREE.Color() }, hor: { value: new THREE.Color() }, sunCol: { value: new THREE.Color() }, sunDir: { value: new THREE.Vector3(0.3, 0.03, -1).normalize() } };
  scene.add(new THREE.Mesh(new THREE.SphereGeometry(20000, 32, 24), new THREE.ShaderMaterial({
    uniforms: u, side: THREE.BackSide, depthWrite: false,
    vertexShader: 'varying vec3 vDir; void main() { vDir = normalize(position); gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
    fragmentShader: `uniform vec3 top; uniform vec3 mid; uniform vec3 hor; uniform vec3 sunCol; uniform vec3 sunDir; varying vec3 vDir;
      void main() {
        float h = vDir.y;
        vec3 c = mix(hor, mid, smoothstep(0.0, 0.05, h));
        c = mix(c, top, smoothstep(0.05, 0.35, h));
        float s = max(dot(normalize(vDir), sunDir), 0.0);
        c += sunCol * (pow(s, 1200.0) * 9.0 + pow(s, 70.0) * 0.7 + pow(s, 8.0) * 0.18);
        gl_FragColor = vec4(c, 1.0);
        #include <tonemapping_fragment>
        #include <colorspace_fragment>
      }`,
  })));
  const mtnMat = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.95 });
  function range(w, d, zc, hScale, seed, segX, segZ, lowCol, snowCol) {
    const geo = new THREE.PlaneGeometry(w, d, segX, segZ).rotateX(-Math.PI / 2);
    const p = geo.attributes.position;
    for (let i = 0; i < p.count; i++) {
      const x = p.getX(i); const z = p.getZ(i);
      const k = (d / 2 - z) / d;
      const rise = Math.min(1, Math.max(0, k / 0.35));
      p.setY(i, ridge(x / 2400 + seed, z / 2400) * hScale * rise * rise * (3 - 2 * rise) - 160);
    }
    geo.translate(0, 0, zc);
    geo.computeVertexNormals();
    const n = geo.attributes.normal;
    const lo = new THREE.Color(lowCol); const hi = new THREE.Color(snowCol); const c = new THREE.Color();
    const col = new Float32Array(p.count * 3);
    for (let i = 0; i < p.count; i++) {
      const hK = Math.min(1, Math.max(0, (p.getY(i) - hScale * 0.3) / (hScale * 0.22)));
      const flat = Math.min(1, Math.max(0, (n.getY(i) - 0.42) / 0.3));
      c.copy(lo).lerp(hi, Math.min(1, hK * (0.35 + flat * 0.9)));
      col[i * 3] = c.r; col[i * 3 + 1] = c.g; col[i * 3 + 2] = c.b;
    }
    geo.setAttribute('color', new THREE.BufferAttribute(col, 3));
    return new THREE.Mesh(geo, mtnMat);
  }
  let far = null;
  let near = null;
  const hemi = new THREE.HemisphereLight('#FFFFFF', '#9DB3E0', 1.0);
  const sunL = new THREE.DirectionalLight('#FFF1D8', 2.4);
  sunL.position.set(3000, 2600, -6000);
  scene.add(hemi, sunL);
  scene.fog = new THREE.Fog('#DCEBFF', 7000, 26000);
  const cloudTex = canvasTex(256, 128, (x) => {
    for (let i = 0; i < 16; i++) {
      const cx = 40 + hash2(i, 3) * 176; const cy = 70 + (hash2(i, 9) - 0.5) * 34; const r = 22 + hash2(i, 4) * 26;
      const g = x.createRadialGradient(cx, cy - r * 0.2, r * 0.1, cx, cy, r);
      g.addColorStop(0, 'rgba(255,255,255,0.95)'); g.addColorStop(0.7, 'rgba(240,246,255,0.75)'); g.addColorStop(1, 'rgba(225,235,252,0)');
      x.fillStyle = g; x.beginPath(); x.arc(cx, cy, r, 0, Math.PI * 2); x.fill();
    }
  });
  const clouds = [];
  for (let i = 0; i < 9; i++) {
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: cloudTex, transparent: true, depthWrite: false, opacity: 0.92 }));
    const s = 2000 + hash2(i, 11) * 1600;
    sp.scale.set(s, s * 0.42, 1);
    sp.position.set((hash2(i, 12) - 0.5) * 20000, 820 + hash2(i, 13) * 380, -9000 - hash2(i, 14) * 4000);
    scene.add(sp);
    clouds.push({ sp, x0: sp.position.x, v: 18 + hash2(i, 15) * 22 });
  }
  let worldName = null;
  function setWorld(name) {
    if (name === worldName) return;
    worldName = name;
    const k = SKY[name] || SKY.meadow;
    u.top.value.set(k.top); u.mid.value.set(k.mid); u.hor.value.set(k.hor); u.sunCol.value.set(k.sun);
    scene.fog.color.set(k.fog);
    sunL.color.set(k.sun);
    if (far) { scene.remove(far, near); far.geometry.dispose(); near.geometry.dispose(); }
    far = range(32000, 7000, -11500, 1500, name === 'sunset' ? 5.3 : 3.1, 240, 60, k.rock, k.snow);
    near = range(26000, 3000, -5400, 640, name === 'sunset' ? 9.1 : 7.7, 200, 30, k.forest, k.snow);
    scene.add(far, near);
  }
  setWorld('meadow');
  // Pitch the backdrop camera so its horizon sits just under the slope crest on screen.
  function update(t, px, crestNdcY, fov, aspect) {
    cam.fov = fov;
    cam.aspect = aspect;
    cam.updateProjectionMatrix();
    const p = Math.atan(Math.max(0, crestNdcY - 0.02) * Math.tan(THREE.MathUtils.degToRad(fov / 2)));
    cam.rotation.set(-p, 0, 0);
    cam.position.x = px * 3;
    clouds.forEach((c) => { c.sp.position.x = ((c.x0 + t * c.v + 10000) % 20000 + 20000) % 20000 - 10000; });
  }
  return { scene, cam, setWorld, update };
}

function blinkAt(t) { const p = t % 3.4; return p < 0.12 ? 1 - Math.abs(p - 0.06) / 0.06 : 0; }

// ---------- Shared materials, geometry and helpers ----------
let LIB = null;
function makeLibrary() {
  if (LIB) return LIB;
  const cache = new Map();
  // Physical-ish materials: roughness, optional sheen (fabric/feathers) and clearcoat (gloss).
  function mat(hex, o) {
    const opt = o || {};
    const key = hex + JSON.stringify(Object.keys(opt).map((k) => [k, opt[k] && opt[k].uuid ? opt[k].uuid : opt[k]]));
    let m = cache.get(key);
    if (!m) {
      const physical = opt.sheen != null || opt.clearcoat != null;
      const base = Object.assign({ color: hex, roughness: 0.7, metalness: 0 }, opt);
      m = physical ? new THREE.MeshPhysicalMaterial(base) : new THREE.MeshStandardMaterial(base);
      cache.set(key, m);
    }
    return m;
  }
  const SPH = new THREE.SphereGeometry(1, 28, 20);
  const PLANE = new THREE.PlaneGeometry(1, 1);
  const FLAT = PLANE.clone().rotateX(-Math.PI / 2);
  const capCache = new Map();
  const capsule = (r, l) => { const k = r + ':' + l; if (!capCache.has(k)) capCache.set(k, new THREE.CapsuleGeometry(r, l, 6, 14)); return capCache.get(k); };
  const rbox = (w, h, d, r) => new RoundedBoxGeometry(w, h, d, 3, r);
  // Lumpy sphere for snow, fluff and pom-poms.
  function lumpy(detail, amp, freq) {
    let g = new THREE.IcosahedronGeometry(1, detail);
    g.deleteAttribute('uv');
    g = mergeVertices(g);
    const p = g.attributes.position;
    for (let i = 0; i < p.count; i++) {
      const x = p.getX(i); const y = p.getY(i); const z = p.getZ(i);
      const n = Math.sin(x * freq + 1.3) * Math.sin(y * freq * 1.1 + 0.4) * Math.sin(z * freq * 0.9 + 2.1);
      const k = 1 + amp * n;
      p.setXYZ(i, x * k, y * k, z * k);
    }
    g.computeVertexNormals();
    const uv = new Float32Array(p.count * 2);
    for (let i = 0; i < p.count; i++) { uv[i * 2] = Math.atan2(p.getZ(i), p.getX(i)) / (Math.PI * 2) + 0.5; uv[i * 2 + 1] = p.getY(i) * 0.5 + 0.5; }
    g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
    return g;
  }
  const FLUFF = lumpy(3, 0.06, 6);
  const POM = lumpy(4, 0.12, 9);
  function part(parent, geo, material, pos, scale, rot) {
    const m = new THREE.Mesh(geo, typeof material === 'string' ? mat(material) : material);
    if (pos) m.position.set(pos[0], pos[1], pos[2]);
    if (scale) m.scale.set(scale[0], scale[1], scale[2]);
    if (rot) m.rotation.set(rot[0], rot[1], rot[2]);
    m.castShadow = true;
    parent.add(m);
    return m;
  }
  const group = (parent, pos) => { const g = new THREE.Group(); if (pos) g.position.set(pos[0], pos[1], pos[2]); parent.add(g); return g; };
  const eyeMat = mat('#1A1426', { roughness: 0.12, clearcoat: 1, clearcoatRoughness: 0.05 });
  const blushMat = new THREE.MeshBasicMaterial({ color: '#FF8FA8', transparent: true, opacity: 0.55, depthWrite: false });
  LIB = { mat, part, group, SPH, PLANE, FLAT, capsule, rbox, FLUFF, POM, eyeMat, blushMat };
  return LIB;
}

function starGeo(r) {
  const s = new THREE.Shape();
  for (let i = 0; i < 10; i++) {
    const a = Math.PI / 2 + i * Math.PI / 5;
    const rr = i % 2 === 0 ? r : r * 0.45;
    if (i === 0) s.moveTo(Math.cos(a) * rr, Math.sin(a) * rr); else s.lineTo(Math.cos(a) * rr, Math.sin(a) * rr);
  }
  s.closePath();
  const g = new THREE.ExtrudeGeometry(s, { depth: r * 0.35, bevelEnabled: true, bevelThickness: r * 0.12, bevelSize: r * 0.12, bevelSegments: 2 });
  g.center();
  return g;
}

function colored(geo, hex) {
  const c = new THREE.Color(hex);
  const n = geo.attributes.position.count;
  const arr = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) { arr[i * 3] = c.r; arr[i * 3 + 1] = c.g; arr[i * 3 + 2] = c.b; }
  geo.setAttribute('color', new THREE.BufferAttribute(arr, 3));
  return geo;
}
// Pine: five jagged tiers that get lighter toward the top, each with a snow mantle and clumps.
function makeTreeGeo(w, variant) {
  const trunk = new THREE.CylinderGeometry(5, 7, 24, 10).translate(0, 12, 0);
  trunk.deleteAttribute('uv');
  const parts = [colored(trunk, w.trunk)];
  const green = new THREE.Color(w.tree);
  const dark = new THREE.Color(w.treeDark);
  const tiers = 5;
  for (let i = 0; i < tiers; i++) {
    const k = i / (tiers - 1);
    const r = 38 - k * 26 + (variant - 1) * 2;
    const h = 34 - k * 6;
    const y = 28 + i * 18 + h / 2;
    const cone = new THREE.ConeGeometry(r, h, 14, 2, true);
    cone.deleteAttribute('uv');
    const pos = cone.attributes.position;
    for (let j = 0; j < pos.count; j++) {
      if (pos.getY(j) > -h / 2 + 0.01) continue;
      const a = Math.atan2(pos.getZ(j), pos.getX(j));
      const jag = 1 + 0.12 * Math.sin(a * 7 + i * 1.7 + variant) + 0.06 * Math.sin(a * 13 + variant * 2);
      pos.setX(j, pos.getX(j) * jag); pos.setZ(j, pos.getZ(j) * jag);
      pos.setY(j, pos.getY(j) - 2.5 * Math.abs(Math.sin(a * 3.5 + i)));
    }
    cone.computeVertexNormals();
    cone.rotateY(i * 0.7 + variant);
    cone.translate(0, y, 0);
    parts.push(colored(cone, '#' + dark.clone().lerp(green, 0.35 + k * 0.65).getHexString()));
    const cap = new THREE.ConeGeometry(r * 0.56, h * 0.52, 14, 1, true);
    cap.deleteAttribute('uv');
    cap.rotateY(i * 0.7 + variant + 0.2);
    cap.translate(0, y + h * 0.25 + 0.8, 0);
    parts.push(colored(cap, w.cap));
    for (let c = 0; c < 3; c++) {
      const a = c * 2.1 + i + variant;
      const clump = new THREE.IcosahedronGeometry(4.2 - k * 1.2, 1);
      clump.deleteAttribute('uv');
      clump.scale(1.3, 0.6, 1.3);
      clump.translate(Math.cos(a) * r * 0.72, y - h * 0.32, Math.sin(a) * r * 0.72);
      parts.push(colored(clump, w.cap));
    }
  }
  const tip = new THREE.ConeGeometry(4, 12, 10, 1, true);
  tip.deleteAttribute('uv');
  tip.translate(0, 28 + 4 * 18 + 34, 0);
  parts.push(colored(tip, w.cap));
  return mergeGeometries(parts.map((p) => (p.index ? p.toNonIndexed() : p)));
}
function makeRockGeo(w) {
  const lump = (detail, amp, seed) => {
    let g = new THREE.IcosahedronGeometry(1, detail);
    g.deleteAttribute('uv');
    g = mergeVertices(g);
    const p = g.attributes.position;
    for (let i = 0; i < p.count; i++) {
      const x = p.getX(i); const y = p.getY(i); const z = p.getZ(i);
      const n = Math.sin(x * 3.1 + seed) * Math.cos(y * 2.7 + seed * 2) + Math.sin(z * 4.3 + seed) * 0.5;
      const k = 1 + amp * n;
      p.setXYZ(i, x * k, y * k, z * k);
    }
    g.computeVertexNormals();
    return g.toNonIndexed();
  };
  const body = lump(1, 0.16, 1).scale(24, 15, 20).translate(0, 8, 0);
  const cap = lump(1, 0.1, 3).scale(19, 6, 15).translate(-2, 21, 1);
  return mergeGeometries([colored(body, w.rock), colored(cap, w.cap)]);
}

// ---------- Pip ----------
// A round, egg-shaped penguin with a heart-shaped white face, teal knit beanie with a cream
// pom-pom, ski goggles resting on the hat, coral scarf and mittens, sunny-yellow skis.
const PIP_COLORS = { body: '#25406F', face: '#FFFFFF', beak: '#FFA53D', hat: '#2FB0A8', cuff: '#F3E9DA', pom: '#F7F0E4', scarf: '#FF6B5A', scarf2: '#F7F0E4', mitt: '#FF6B5A', ski: '#FFC531', strap: '#FF6B5A' };
const BROW = { normal: [0, 0], happy: [-2, 0.15], excited: [-3, 0.2], scared: [-4, -0.4], sad: [1, -0.5], determined: [1.5, 0.5], wink: [-1, 0.1], dizzy: [-1, -0.2] };
let lensTex = null;
function makePenguin(lib) {
  const { mat, part, group, SPH, capsule, rbox, POM, eyeMat, blushMat } = lib;
  const C = PIP_COLORS;
  if (!lensTex) {
    lensTex = canvasTex(128, 48, (x, w, h) => {
      const g = x.createLinearGradient(0, 0, w, h);
      g.addColorStop(0, '#FFC23D'); g.addColorStop(0.45, '#FF5E9A'); g.addColorStop(1, '#7B5CFF');
      x.fillStyle = g; x.fillRect(0, 0, w, h);
      x.fillStyle = 'rgba(255,255,255,0.7)';
      x.beginPath(); x.moveTo(18, 0); x.lineTo(34, 0); x.lineTo(16, h); x.lineTo(0, h); x.closePath(); x.fill();
    });
  }
  const feather = mat(C.body, { roughness: 0.62, sheen: 0.7, sheenColor: '#7E9BE0', sheenRoughness: 0.45 });
  const white = mat(C.face, { roughness: 0.55, sheen: 0.4, sheenColor: '#FFFFFF' });
  const knit = (hex) => mat(hex, { roughness: 0.95, normalMap: KNIT_N, normalScale: new THREE.Vector2(0.9, 0.9), sheen: 0.6, sheenColor: '#FFFFFF', sheenRoughness: 0.8 });
  const orange = mat(C.beak, { roughness: 0.4, clearcoat: 0.3 });
  const root = new THREE.Group();
  const spin = group(root, [0, 60, 0]);
  const inner = group(spin, [0, -60, 0]);

  const skis = [];
  const skiMat = mat(C.ski, { roughness: 0.3, clearcoat: 0.8, clearcoatRoughness: 0.15 });
  for (const side of [-1, 1]) {
    const sp = group(inner, [side * 17, 0, 4]);
    part(sp, rbox(13, 4, 104, 1.8), skiMat, [0, 2, 16]);
    part(sp, rbox(13, 4, 16, 1.8), skiMat, [0, 6.5, 71], null, [-0.6, 0, 0]);
    part(sp, rbox(3, 0.8, 84, 0.4), mat('#FFFFFF', { roughness: 0.3 }), [0, 4.3, 18]);
    part(sp, rbox(15, 5, 14, 2), mat('#3A3550', { roughness: 0.5 }), [0, 5.5, 0]);
    const foot = group(sp, [0, 9, 4]);
    part(foot, SPH, orange, [0, 0, 0], [9.5, 4.5, 13]);
    for (const tx of [-5, 0, 5]) part(foot, SPH, orange, [tx, -0.5, 11], [3.4, 3, 4]);
    skis.push(sp);
  }

  const body = group(inner);
  // One smooth egg for head + body, so the silhouette reads as a single chubby shape.
  const profile = [[0, 0], [22, 1], [33, 8], [39, 22], [40.5, 38], [39, 54], [36, 66], [33.5, 76], [33, 87], [32.5, 98], [30, 108], [24, 116], [13, 121], [0, 123]];
  const curve = new THREE.SplineCurve(profile.map(([r, y]) => new THREE.Vector2(r, y)));
  part(body, new THREE.LatheGeometry(curve.getPoints(40), 40), feather, [0, 4, 0]);
  part(body, SPH, white, [0, 44, 23], [29, 33, 19]);

  const arms = [];
  for (const side of [-1, 1]) {
    const pv = group(body, [side * 36, 64, 2]);
    part(pv, capsule(7.5, 20), feather, [0, -16, 0]);
    const mitt = group(pv, [0, -32, 0]);
    part(mitt, SPH, knit(C.mitt), [0, 0, 0], [9, 9.5, 8.5]);
    part(mitt, SPH, knit(C.mitt), [-side * 6, 3, 4], [3.6, 4.4, 3.6]);
    part(mitt, new THREE.CylinderGeometry(9.2, 9.2, 4, 18), knit(C.cuff), [0, 7, 0]);
    const pole = group(mitt);
    part(pole, new THREE.CylinderGeometry(1.5, 1.5, 70, 8), mat('#C9D1DD', { metalness: 0.8, roughness: 0.3 }), [side * 3, -34, 5], null, [0, 0, side * -0.1]);
    part(pole, new THREE.TorusGeometry(4.5, 1.1, 6, 14), mat('#3A3550', { roughness: 0.5 }), [side * 6.5, -62, 5], null, [Math.PI / 2, 0, 0]);
    arms.push({ pv, pole, side });
  }
  const scarfMat = knit(C.scarf);
  part(body, new THREE.TorusGeometry(34.2, 5.8, 14, 44), scarfMat, [0, 75.5, 0], [1, 1, 0.98], [Math.PI / 2, 0, 0]);
  const tail = group(body, [22, 74, 23]);
  part(tail, capsule(5.6, 18), scarfMat, [0, -12, 0], [1, 1, 0.6]);
  part(tail, new THREE.CylinderGeometry(5.8, 5.8, 3, 12), knit(C.scarf2), [0, -20, 0], [1, 1, 0.6]);

  // Head features pivot at the neck so a head turn stays on the round body.
  const neck = group(body, [0, 86, 0]);
  const head = group(neck, [0, -86, 0]);
  // Heart-shaped face: two upper lobes and a rounded chin, sitting proud of the feathers.
  part(head, SPH, white, [-11.5, 101, 22.5], [16, 16.5, 10.2]);
  part(head, SPH, white, [11.5, 101, 22.5], [16, 16.5, 10.2]);
  part(head, SPH, white, [0, 89.5, 24], [22.5, 16, 11.4]);
  const eyes = [];
  for (const side of [-1, 1]) {
    const eg = group(head, [side * 10.5, 100, 31.8]);
    eg.rotation.y = side * 0.32;
    const open = group(eg);
    part(open, SPH, eyeMat, [0, 0, 0], [4.8, 5.9, 2.6]);
    part(open, SPH, mat('#FFFFFF', { roughness: 0.2 }), [1.5, 2.1, 2.1], [1.6, 1.6, 0.7]);
    const lid = part(eg, SPH, white, [0, 4.2, 0.7], [5.6, 2.8, 2.6]);
    const happy = part(eg, new THREE.TorusGeometry(3.8, 1.2, 8, 16, Math.PI), eyeMat, [0, -1.2, 1]);
    const scared = group(eg);
    part(scared, SPH, mat('#FFFFFF', { roughness: 0.3 }), [0, 0, 0], [5.4, 6, 2.6]);
    part(scared, SPH, eyeMat, [0, 0, 2.1], [2, 2, 0.9]);
    const star = part(eg, starGeo(1), mat('#FFD43B', { roughness: 0.3, clearcoat: 0.5 }), [0, 0, 1.2], [5.4, 5.4, 5.4]);
    const dizzy = part(eg, new THREE.TorusGeometry(3.4, 0.9, 6, 16, Math.PI * 1.6), eyeMat, [0, 0, 1]);
    const brow = part(head, capsule(1.2, 6), mat(C.body, { roughness: 0.6 }), [side * 10.5, 107.5, 31], null, [0, side * 0.3, Math.PI / 2]);
    eyes.push({ side, open, lid, happy, scared, star, dizzy, brow });
  }
  const mouth = {};
  mouth.upper = part(head, SPH, orange, [0, 94, 34.5], [6.8, 3.8, 6.5]);
  mouth.inside = part(head, SPH, mat('#B8333A', { roughness: 0.5 }), [0, 91.5, 33.5], [4.6, 2, 2.5]);
  mouth.lower = part(head, SPH, orange, [0, 91.5, 33.3], [5, 2.3, 5]);
  for (const side of [-1, 1]) {
    const b = new THREE.Mesh(SPH, blushMat);
    b.position.set(side * 18, 92.5, 29.2); b.scale.set(5.2, 3.2, 1.4); b.rotation.y = side * 0.55;
    head.add(b);
  }
  // Teal knit beanie with a fat cream rib cuff and a fluffy pom-pom.
  const hatG = group(head, [0, 112, 0]);
  part(hatG, new THREE.SphereGeometry(1, 36, 18, 0, Math.PI * 2, 0, Math.PI / 2), knit(C.hat), [0, 0, 0], [31, 24, 31]);
  part(hatG, new THREE.TorusGeometry(30, 5.8, 14, 48), knit(C.cuff), [0, 1.4, 0], null, [Math.PI / 2, 0, 0]);
  const pomM = mat(C.pom, { roughness: 1, sheen: 1, sheenColor: '#FFFFFF', sheenRoughness: 0.9, normalMap: FLUFF_N });
  const pom = part(hatG, POM, pomM, [0, 27, 0], [11.5, 11.5, 11.5]);
  // Goggles pushed up on the beanie: Pip's signature lens.
  const gog = group(hatG, [0, 11, 0]);
  gog.rotation.x = -0.42;
  part(gog, new THREE.TorusGeometry(28.6, 2.2, 8, 48), mat(C.strap, { roughness: 0.7 }), [0, 0, 0], [1, 1, 0.98], [Math.PI / 2, 0, 0]);
  part(gog, rbox(36, 14.5, 7, 3.6), mat('#FFFFFF', { roughness: 0.35, clearcoat: 0.6 }), [0, 0, 26]);
  const lens = new THREE.Mesh(rbox(31, 10.2, 2.6, 2.2), new THREE.MeshPhysicalMaterial({ map: lensTex, roughness: 0.08, clearcoat: 1, metalness: 0.1 }));
  lens.position.set(0, 0, 29.6);
  gog.add(lens);

  const stars = [];
  for (let i = 0; i < 3; i++) { const s = part(root, starGeo(1), mat('#FFC93C', { roughness: 0.3 }), [0, 0, 0], [6, 6, 6]); s.visible = false; stars.push(s); }

  function update(st) {
    const t = st.t || 0;
    const lean = st.lean || 0;
    const tuck = st.tuck || 0;
    const q = st.squash || 0;
    root.scale.set(1 + q * 0.14, 1 - q * 0.14, 1 + q * 0.14);
    spin.rotation.set(st.flip || 0, 0, -(st.spin || 0));
    const carve = st.carve || 0;
    skis.forEach((sp, i) => {
      const side = i === 0 ? -1 : 1;
      sp.position.set(side * 17 + lean * 5, tuck * 10, 4);
      sp.rotation.set(tuck * 0.25, -carve * 0.7 + side * (0.05 + tuck * 0.12), lean * 0.1);
    });
    body.position.set(0, (st.bob || 0) * 0.7 - tuck * 6, 0);
    body.rotation.set(-0.3 + tuck * 0.15, 0, -lean * 0.24);
    const armMode = st.arms || 'poles';
    const pump = st.pump || 0;
    arms.forEach(({ pv, pole, side }) => {
      const raised = armMode === 'up' || (armMode === 'wave' && side === 1);
      if (raised) {
        const wob = armMode === 'wave' ? Math.sin(t * 14) * 0.35 : Math.sin(t * 9 + side) * 0.15;
        pv.rotation.set(0, 0, side * (2.5 + wob));
      } else {
        pv.rotation.set(-0.35 - pump * 0.22, 0, side * (0.5 + lean * side * 0.18));
      }
      pole.visible = !raised;
    });
    tail.rotation.set(-0.35, 0, 0.35 + Math.sin(t * 13) * 0.3 + lean * 0.4);
    neck.position.y = 86 + (st.headBob || 0);
    neck.rotation.set(0, (st.look || 0) * 0.28, -lean * 0.06 + (st.headShake || 0));
    const expr = st.expr || 'normal';
    const blink = 1 - (st.blink || 0) * 0.9;
    const brow = BROW[expr] || BROW.normal;
    eyes.forEach((e) => {
      const kind = expr === 'happy' || (expr === 'wink' && e.side > 0) ? 'happy' : expr === 'excited' ? 'star' : expr === 'dizzy' ? 'dizzy' : expr === 'scared' ? 'scared' : 'open';
      e.open.visible = kind === 'open';
      e.open.scale.y = blink;
      e.open.position.x = (st.look || 0) * 1.3;
      e.lid.visible = kind === 'open' && expr === 'determined';
      e.happy.visible = kind === 'happy';
      e.scared.visible = kind === 'scared';
      e.star.visible = kind === 'star';
      e.star.rotation.z = Math.sin(t * 6) * 0.3;
      e.dizzy.visible = kind === 'dizzy';
      e.dizzy.rotation.z = t * 9 * e.side;
      e.brow.visible = expr !== 'normal' && expr !== 'happy';
      e.brow.position.y = 107.5 - brow[0] * 0.6;
      e.brow.rotation.z = Math.PI / 2 + e.side * brow[1] * 0.5;
    });
    const mo = st.mouth != null ? st.mouth : expr === 'happy' || expr === 'excited' ? 1 : expr === 'scared' ? 0.7 : 0;
    mouth.lower.position.y = 91.5 - mo * 4.5;
    mouth.inside.visible = mo > 0.05;
    mouth.inside.scale.y = 1.6 + mo * 3;
    mouth.inside.position.y = 91.5 - mo * 2;
    pom.position.y = 27 + Math.abs(st.pom || 0) * 0.5;
    pom.position.x = (st.pom || 0) * 0.6;
    stars.forEach((s, i) => {
      s.visible = expr === 'dizzy';
      if (!s.visible) return;
      const a = t * 5 + i * 2.1;
      s.position.set(Math.cos(a) * 30, 156 + Math.sin(a) * 4, Math.sin(a) * 18);
      s.rotation.z = t * 4;
    });
  }
  return { root, update };
}

// ---------- Benny (rival): a brown beaver-ish racer with a yellow scarf ----------
function makeBenny(lib) {
  const { mat, part, group, SPH, capsule, rbox, eyeMat } = lib;
  const p = ART.PAL.benny;
  const furM = mat(p.body, { roughness: 0.9, sheen: 0.6, sheenColor: '#D9A877' });
  const root = new THREE.Group();
  const skis = [];
  const skiMat = mat(p.ski, { roughness: 0.3, clearcoat: 0.8 });
  for (const side of [-1, 1]) {
    const sp = group(root, [side * 17, 0, 4]);
    part(sp, rbox(13, 4, 100, 1.8), skiMat, [0, 2, 16]);
    part(sp, SPH, mat(p.boot), [0, 9, 3], [9, 7, 12]);
    skis.push(sp);
  }
  const body = group(root);
  part(body, SPH, furM, [0, 50, 0], [34, 40, 30]);
  part(body, SPH, mat(p.belly, { roughness: 0.9 }), [0, 44, 17], [24, 28, 14]);
  part(body, new THREE.TorusGeometry(27, 6, 12, 36), mat(p.scarf, { roughness: 0.95, normalMap: KNIT_N }), [0, 80, 0], null, [Math.PI / 2, 0, 0]);
  const arms = [];
  for (const side of [-1, 1]) {
    const pv = group(body, [side * 32, 66, 2]);
    part(pv, capsule(7, 20), furM, [0, -16, 0]);
    part(pv, SPH, mat(p.boot), [0, -31, 0], [8, 8, 8]);
    arms.push({ pv, side });
  }
  const head = group(body, [0, 82, 0]);
  part(head, SPH, furM, [0, 28, 0], [33, 30, 30]);
  part(head, SPH, mat(p.face, { roughness: 0.9 }), [0, 22, 19], [22, 17, 12]);
  for (const side of [-1, 1]) {
    part(head, SPH, furM, [side * 25, 52, 0], [10, 10, 6]);
    part(head, SPH, mat(p.earIn), [side * 25, 52, 4], [5.5, 5.5, 3]);
    part(head, SPH, eyeMat, [side * 10, 31, 27], [4.6, 5.6, 2.6]);
    part(head, capsule(1.6, 7), mat('#3B2416'), [side * 10, 40, 28], null, [0, 0, Math.PI / 2 + side * 0.4]);
  }
  part(head, SPH, mat(p.nose, { roughness: 0.3, clearcoat: 0.6 }), [0, 24, 31], [6, 4.5, 4]);
  part(head, rbox(4.5, 7, 2, 1), mat('#FFFFFF', { roughness: 0.3 }), [-2.6, 14, 30]);
  part(head, rbox(4.5, 7, 2, 1), mat('#FFFFFF', { roughness: 0.3 }), [2.6, 14, 30]);
  function update(st) {
    const t = st.t || 0;
    const lean = st.lean || 0;
    root.scale.set(1, 1, 1);
    skis.forEach((sp, i) => { const side = i === 0 ? -1 : 1; sp.rotation.set(0, -(st.carve || 0) * 0.7 + side * 0.05, 0); });
    body.position.y = (st.bob || 0) * 0.7;
    body.rotation.set(0.06, 0, -lean * 0.24);
    arms.forEach(({ pv, side }) => pv.rotation.set(-0.35 - (st.pump || 0) * 0.22, 0, side * 0.5));
    head.rotation.set(0, (st.look || 0) * 0.28, Math.sin(t * 2) * 0.03);
  }
  return { root, update };
}

// Start last, so every helper and constant above is initialised.
if (!renderer) {
  if (canvas3d) canvas3d.remove();
} else {
  setup();
}
