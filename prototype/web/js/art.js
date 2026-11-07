// Pippy The Ski Penguin prototype — procedural "toy diorama" art.
// Every character, prop and effect is drawn in code from simple shapes; no external images.
(function () {
  const INK = '#2B2140';
  const ART = { INK };

  ART.PAL = {
    pip: { body: '#2B3A67', bodyDark: '#1C2748', belly: '#FFFFFF', head: '#2B3A67', headDark: '#1C2748', face: '#FFFFFF', faceRx: 44, faceRy: 36, faceY: -100, ears: null, mouth: 'beak', beak: '#FF9A1F', hat: null, tuft: true, goggles: true, strap: '#FF3E86', hatBand: '#178FE0', pom: '#FFFFFF', scarf: '#FF3E86', ski: '#FF2E78', skiDark: '#2B2140', boot: '#FF9A1F' },
    benny: { body: '#8B5A3C', bodyDark: '#6E4329', belly: '#C99A6E', head: '#8B5A3C', headDark: '#6E4329', face: '#C99A6E', faceRx: 32, faceRy: 24, ears: '#6E4329', earIn: '#5A3420', mouth: 'teeth', nose: '#3B2416', hat: null, scarf: '#FFC21A', ski: '#1E9BFF', skiDark: '#2B2140', boot: '#5A3420' }
  };

  ART.WORLD = {
    meadow: { skyTop: '#4DB6FF', skyMid: '#A6E1FF', skyLow: '#FFD9EC', mtFar: '#9FB2F5', mtNear: '#7E98EC', cap: '#FFFFFF', snow: '#FBFDFF', shade: '#DAE6FF', tree: '#28C17F', tree2: '#1AA56A', treeDark: '#137A4F', trunk: '#9A5B2E', rock: '#8D9DC4', rockDark: '#6B7AA3', hill: '#C4D8FF', sun: '#FFF6C9', sunGlow: 'rgba(255,240,180,0.7)', sunX: 300 },
    sunset: { skyTop: '#FF9C73', skyMid: '#FFC79A', skyLow: '#FFF0D4', mtFar: '#C99BBE', mtNear: '#A8789F', cap: '#FFF4EC', snow: '#FFF9F2', shade: '#F3DACB', tree: '#1FAE73', tree2: '#16925E', treeDark: '#0F6A43', hill: '#E9C6CF', trunk: '#7A4B2E', rock: '#A08A9E', rockDark: '#806C80', sun: '#FFE9A8', sunGlow: 'rgba(255,190,120,0.8)', sunX: 70 }
  };

  const PATH = {};
  function P(d) { return PATH[d] || (PATH[d] = new Path2D(d)); }
  function fillP(ctx, d, fill) { ctx.fillStyle = fill; ctx.fill(P(d)); }
  function E(ctx, x, y, rx, ry, rot, fill) {
    ctx.beginPath();
    ctx.ellipse(x, y, Math.max(0.1, rx), Math.max(0.1, ry), rot || 0, 0, Math.PI * 2);
    ctx.fillStyle = fill;
    ctx.fill();
  }
  function C(ctx, x, y, r, fill) { E(ctx, x, y, r, r, 0, fill); }
  function RR(ctx, x, y, w, h, r) {
    r = Math.min(r, w / 2, h / 2);
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
  }
  function box(ctx, x, y, w, h, r, fill, stroke, lw) {
    RR(ctx, x, y, w, h, r);
    if (fill) { ctx.fillStyle = fill; ctx.fill(); }
    if (stroke) { ctx.lineWidth = lw || 3; ctx.strokeStyle = stroke; ctx.stroke(); }
  }
  ART.E = E; ART.C = C; ART.box = box;

  ART.text = function (ctx, str, x, y, px, fill, opts) {
    const o = opts || {};
    ctx.font = (o.weight || 700) + ' ' + px + 'px Fredoka, Mitr, sans-serif';
    ctx.textAlign = o.align || 'center';
    ctx.textBaseline = 'middle';
    ctx.lineJoin = 'round';
    if (o.stroke !== false) {
      ctx.lineWidth = Math.max(3, px * 0.22);
      ctx.strokeStyle = INK;
      ctx.strokeText(str, x, y);
    }
    ctx.fillStyle = fill;
    ctx.fillText(str, x, y);
  };

  ART.shadow = function (ctx, x, y, rx, ry, a) { E(ctx, x, y, rx, ry, 0, 'rgba(31,42,77,' + (a == null ? 0.16 : a) + ')'); };

  // Front-facing chibi skier. Feet at (0,0); drawn in the 200-unit rig space and scaled by s.
  // st: pal, t, lean, look, expr, mouth, blink, bob, squash, tuck, pump, arms ('poles'|'up'|'wave'),
  //     spin, headShake, pom, sweat, alpha.
  const BROW = { normal: [0, 0], happy: [-3, 0.15], excited: [-5, 0.2], scared: [-6, -0.4], sad: [1, -0.5], determined: [2, 0.5], wink: [-2, 0.1], dizzy: [-2, -0.2] };
  function spiral(ctx, cx, cy) {
    ctx.beginPath();
    for (let a = 0; a < Math.PI * 4.2; a += 0.35) {
      const r = 1 + a * 0.7;
      const px = cx + Math.cos(a) * r;
      const py = cy + Math.sin(a) * r;
      if (a === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
    }
    ctx.stroke();
  }
  ART.skier = function (ctx, x, y, s, st) {
    const p = st.pal;
    const lean = st.lean || 0;
    const t = st.t || 0;
    const expr = st.expr || 'normal';
    const look = Math.max(-1, Math.min(1, st.look || 0));
    const tuck = st.tuck || 0;
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(s, s);
    if (st.alpha != null) ctx.globalAlpha = st.alpha;
    if (st.spin) { ctx.translate(0, -80); ctx.rotate(st.spin); ctx.translate(0, 80); }
    const q = st.squash || 0;
    ctx.scale(1 + q * 0.16, 1 - q * 0.16);

    // Skis point toward the camera (tips at the bottom) and angle together into the carve.
    const carve = st.carve || 0;
    for (const side of [-1, 1]) {
      ctx.save();
      ctx.translate(side * 27 + lean * 5, -8 - tuck * 14);
      ctx.rotate(-carve + side * (0.1 + tuck * 0.22));
      ctx.beginPath();
      ctx.moveTo(-6, -20);
      ctx.lineTo(6, -20);
      ctx.lineTo(12, 84);
      ctx.quadraticCurveTo(0, 100, -12, 84);
      ctx.closePath();
      ctx.fillStyle = p.ski;
      ctx.fill();
      ctx.lineWidth = 3;
      ctx.lineJoin = 'round';
      ctx.strokeStyle = INK;
      ctx.stroke();
      ctx.fillStyle = 'rgba(255,255,255,0.75)';
      ctx.beginPath(); ctx.moveTo(-1.2, -14); ctx.lineTo(1.2, -14); ctx.lineTo(2.6, 78); ctx.lineTo(-2.6, 78); ctx.closePath(); ctx.fill();
      E(ctx, 0, 88, 8, 4, 0, 'rgba(255,255,255,0.75)');
      E(ctx, 0, 0, 14, 11, 0, p.boot);
      box(ctx, -13, 5, 26, 6, 3, p.skiDark);
      ctx.restore();
    }

    ctx.save();
    ctx.rotate(lean * 0.26);
    ctx.translate(0, (st.bob || 0) - tuck * 10 - 14);
    const arms = st.arms || 'poles';
    const pump = st.pump || 0;
    for (const side of [-1, 1]) {
      const raised = arms === 'up' || (arms === 'wave' && side === 1);
      let a;
      if (raised) a = -Math.PI / 2 + side * 0.55 + (arms === 'wave' ? Math.sin(t * 14) * 0.4 : Math.sin(t * 9 + side) * 0.18);
      else a = Math.PI / 2 - side * (0.85 + lean * side * 0.25) + side * pump * 0.22;
      const sx = side * 44;
      const sy = -68;
      const hx = sx + Math.cos(a) * 32;
      const hy = sy + Math.sin(a) * 32;
      if (!raised) {
        ctx.strokeStyle = '#7C8AA5';
        ctx.lineWidth = 4;
        ctx.lineCap = 'round';
        const gx = hx + side * 16 - pump * side * 8;
        ctx.beginPath(); ctx.moveTo(hx, hy); ctx.lineTo(gx, 10); ctx.stroke();
        C(ctx, gx, 8, 5, '#7C8AA5');
      }
      E(ctx, sx + Math.cos(a) * 16, sy + Math.sin(a) * 16, 13.5, 19, a - Math.PI / 2, p.bodyDark);
      C(ctx, hx, hy, 9.5, p.boot);
    }
    E(ctx, 0, -44, 55, 47, 0, p.bodyDark);
    E(ctx, -4, -48, 51, 43, 0, p.body);
    E(ctx, 0, -36, 35, 31, 0, p.belly);
    E(ctx, -28, -62, 12, 7, -0.5, 'rgba(255,255,255,0.3)');

    ctx.save();
    ctx.translate(27, -81);
    ctx.rotate(-0.2 + Math.sin(t * 13) * 0.3 + lean * 0.6);
    box(ctx, -8, 0, 16, 32, 7, p.scarf);
    box(ctx, -8, 24, 16, 8, 4, 'rgba(255,255,255,0.35)');
    ctx.restore();
    box(ctx, -49, -89, 98, 19, 9.5, p.scarf);
    box(ctx, -49, -89, 98, 6, 3, 'rgba(255,255,255,0.25)');

    ctx.save();
    ctx.translate(0, -80);
    ctx.rotate(lean * 0.12 + (st.headShake || 0));
    ctx.translate(0, 80 + (st.headBob || 0));
    if (p.ears) {
      C(ctx, -50, -157, 18, p.ears); C(ctx, -50, -157, 9.5, p.earIn);
      C(ctx, 50, -157, 18, p.ears); C(ctx, 50, -157, 9.5, p.earIn);
    }
    if (p.tuft) {
      // Signature messy hair: spiky tuft bursting up over the goggles.
      for (const [a, len] of [[-0.85, 30], [-0.42, 40], [0, 46], [0.44, 39], [0.88, 29]]) {
        ctx.save();
        ctx.translate(0, -160);
        ctx.rotate(a + Math.sin(t * 14 + a * 5) * 0.05);
        ctx.beginPath(); ctx.moveTo(-9, 6); ctx.quadraticCurveTo(-4, -len * 0.6, 2, -len); ctx.quadraticCurveTo(6, -len * 0.5, 9, 6); ctx.closePath();
        ctx.fillStyle = p.headDark; ctx.fill();
        ctx.restore();
      }
    }
    E(ctx, 0, -117, 61, 56, 0, p.headDark);
    E(ctx, -4, -121, 57, 52, 0, p.head);
    E(ctx, 0, p.faceY || -103, p.faceRx, p.faceRy, 0, p.face);
    E(ctx, -28, -151, 16, 9, -0.56, 'rgba(255,255,255,0.35)');

    const ey = -108;
    const lx = look * 3.5;
    ctx.strokeStyle = INK;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    const openEye = (ex) => {
      const k = 1 - (st.blink || 0) * 0.9;
      E(ctx, ex + lx, ey, 9.8, 12 * k, 0, INK);
      if (k > 0.5) { C(ctx, ex + lx + 3.4, ey - 4.6, 3.9, '#FFFFFF'); C(ctx, ex + lx - 2.4, ey + 4.6, 1.8, '#FFFFFF'); }
      if (expr === 'determined') E(ctx, ex + lx, ey - 10, 12, 6, 0, p.face);
    };
    const happyEye = (ex) => { ctx.lineWidth = 4.5; ctx.beginPath(); ctx.arc(ex, ey + 4, 8, Math.PI * 1.15, Math.PI * 1.85); ctx.stroke(); };
    for (const ex of [-21, 21]) {
      if (expr === 'happy') happyEye(ex);
      else if (expr === 'excited') ART.shape(ctx, 'star', ex, ey, 9.5, '#FFE066');
      else if (expr === 'dizzy') { ctx.lineWidth = 2.6; spiral(ctx, ex, ey); }
      else if (expr === 'scared') {
        C(ctx, ex, ey, 10.5, '#FFFFFF');
        ctx.lineWidth = 2.5; ctx.beginPath(); ctx.arc(ex, ey, 10.5, 0, Math.PI * 2); ctx.stroke();
        C(ctx, ex + lx, ey + 1, 3.6, INK);
      } else if (expr === 'wink' && ex > 0) happyEye(ex);
      else openEye(ex);
    }
    const brow = BROW[expr] || BROW.normal;
    ctx.lineWidth = 4.2;
    for (const side of [-1, 1]) {
      const bx = side * 21;
      const by = -125 + brow[0];
      ctx.beginPath();
      ctx.moveTo(bx + side * 8, by - brow[1] * 4);
      ctx.quadraticCurveTo(bx, by - 3.5, bx - side * 8, by + brow[1] * 9);
      ctx.stroke();
    }
    const blushA = expr === 'happy' || expr === 'excited' ? 0.85 : expr === 'scared' ? 0.15 : 0.55;
    E(ctx, -37, -92, 9.5, 5.5, 0, 'rgba(255,127,168,' + blushA + ')');
    E(ctx, 37, -92, 9.5, 5.5, 0, 'rgba(255,127,168,' + blushA + ')');
    if (expr === 'sad') fillP(ctx, 'M-30 -97 Q-25 -88 -30 -85 Q-35 -88 -30 -97 Z', '#8FD3FF');

    const mo = st.mouth != null ? st.mouth : expr === 'happy' || expr === 'excited' ? 1 : expr === 'scared' ? 0.7 : 0;
    if (p.mouth === 'beak') {
      if (mo > 0.05) {
        E(ctx, 0, -93 + mo * 2, 8.5, 3 + mo * 5, 0, '#B8333A');
        E(ctx, 0, -89 + mo * 4, 4.5, 2 + mo * 1.5, 0, '#FF8DA8');
        ctx.save(); ctx.translate(0, mo * 7); fillP(ctx, 'M-9 -95 Q0 -92 9 -95 Q0 -86 -9 -95 Z', p.beak); ctx.restore();
        fillP(ctx, 'M-12 -99 Q0 -108 12 -99 Q0 -94 -12 -99 Z', p.beak);
        E(ctx, -4, -101, 3.5, 1.6, 0, 'rgba(255,255,255,0.55)');
      } else {
        ctx.save();
        if (expr === 'sad') { ctx.translate(0, -96); ctx.rotate(0.06); ctx.translate(0, 96); }
        fillP(ctx, 'M-11 -98 Q0 -107 11 -98 Q0 -85 -11 -98 Z', p.beak);
        E(ctx, -3, -100, 4, 2, 0, 'rgba(255,255,255,0.5)');
        ctx.restore();
      }
    } else {
      E(ctx, 0, -98, 7, 5, 0, p.nose);
      if (mo > 0.05) {
        E(ctx, 0, -86 + mo * 2, 6.5, 2 + mo * 5, 0, INK);
        E(ctx, 0, -83 + mo * 3, 3.5, 1.5 + mo * 1.5, 0, '#FF8DA8');
      } else if (expr === 'sad') {
        ctx.lineWidth = 3; ctx.beginPath(); ctx.arc(0, -82, 6, Math.PI * 1.2, Math.PI * 1.8); ctx.stroke();
      } else {
        fillP(ctx, 'M-7 -91 Q0 -84 7 -91 Q0 -87.5 -7 -91 Z', INK);
      }
      if (p.mouth === 'teeth') { box(ctx, -6, -90, 5.5, 8, 1.6, '#FFFFFF'); box(ctx, 0.5, -90, 5.5, 8, 1.6, '#FFFFFF'); }
    }
    if (p.hat) {
      fillP(ctx, 'M-62 -135 Q-60 -189 0 -189 Q60 -189 62 -135 Z', p.hat);
      box(ctx, -64, -145, 128, 18, 9, p.hatBand);
      C(ctx, 0, -189 + (st.pom || 0), 13, p.pom);
    }
    if (p.goggles) {
      box(ctx, -60, -152, 120, 10, 5, p.strap);
      box(ctx, -42, -166, 84, 30, 12, '#FFFFFF', INK, 3);
      const lg = ctx.createLinearGradient(-36, -160, 36, -142);
      lg.addColorStop(0, '#FFC23D'); lg.addColorStop(0.45, '#FF5E9A'); lg.addColorStop(1, '#7B5CFF');
      box(ctx, -36, -160, 72, 18, 8, lg);
      ctx.fillStyle = 'rgba(255,255,255,0.7)';
      ctx.beginPath(); ctx.moveTo(-22, -160); ctx.lineTo(-11, -160); ctx.lineTo(-20, -142); ctx.lineTo(-31, -142); ctx.closePath(); ctx.fill();
    }
    if (st.sweat || expr === 'scared') {
      fillP(ctx, 'M48 -160 Q54 -148 48 -144 Q42 -148 48 -160 Z', '#8FD3FF');
      if (expr === 'scared') fillP(ctx, 'M-50 -150 Q-45 -140 -50 -137 Q-55 -140 -50 -150 Z', '#8FD3FF');
    }
    ctx.restore();
    ctx.restore();
    ctx.restore();
  };

  ART.tree = function (ctx, x, y, s, w) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(s, s);
    ctx.translate(-30, -86);
    E(ctx, 30, 86, 24, 5, 0, 'rgba(31,42,77,0.14)');
    box(ctx, 26, 72, 8, 14, 3, w.trunk, INK, 2.2);
    ctx.lineWidth = 2.4; ctx.lineJoin = 'round'; ctx.strokeStyle = INK;
    for (const [d, f] of [['M30 38 L58 76 Q30 84 2 76 Z', w.tree2], ['M30 20 L54 56 Q30 63 6 56 Z', w.tree], ['M30 3 L50 36 Q30 42 10 36 Z', w.tree]]) {
      fillP(ctx, d, f);
      ctx.stroke(P(d));
    }
    ctx.globalAlpha *= 0.4;
    fillP(ctx, 'M30 3 L50 36 Q40 39 30 40 Z M30 20 L54 56 Q42 60 30 61 Z M30 38 L58 76 Q44 80 30 81 Z', w.treeDark);
    ctx.globalAlpha /= 0.4;
    fillP(ctx, 'M30 3 L39 17 Q35 15 33 19 Q30 15 27 19 Q25 15 21 17 Z', w.cap);
    fillP(ctx, 'M12 54 Q16 49 21 53 Q25 48 30 54 Q35 48 39 53 Q44 49 48 54 L52 56 Q30 63 8 56 Z', w.cap);
    fillP(ctx, 'M6 74 Q11 69 17 73 Q23 67 30 74 Q37 67 43 73 Q49 69 54 74 L57 76 Q30 84 3 76 Z', w.cap);
    ctx.restore();
  };

  ART.rock = function (ctx, x, y, s, w) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(s, s);
    ctx.translate(-30, -84);
    E(ctx, 30, 84, 26, 5, 0, 'rgba(31,42,77,0.14)');
    fillP(ctx, 'M6 82 Q8 58 26 54 Q46 50 54 82 Z', w.rock);
    fillP(ctx, 'M34 54 Q46 56 54 82 L38 82 Q42 66 34 54 Z', w.rockDark);
    fillP(ctx, 'M12 64 Q20 52 30 54 Q42 50 50 62 Q40 58 32 62 Q22 58 12 64 Z', w.cap);
    ctx.lineWidth = 2.4; ctx.lineJoin = 'round'; ctx.strokeStyle = INK;
    ctx.stroke(P('M6 82 Q8 58 26 54 Q46 50 54 82 Z'));
    ctx.restore();
  };

  ART.log = function (ctx, x, y, s, w) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(s, s);
    E(ctx, 0, 2, 52, 6, 0, 'rgba(31,42,77,0.16)');
    box(ctx, -46, -20, 92, 22, 11, '#9C6B45', INK, 2.5);
    box(ctx, -40, -14, 70, 3, 1.5, 'rgba(0,0,0,0.12)');
    E(ctx, 40, -9, 9, 11, 0, '#E7C59A');
    ctx.strokeStyle = '#B8875C'; ctx.lineWidth = 1.6;
    ctx.beginPath(); ctx.ellipse(40, -9, 5, 6.5, 0, 0, Math.PI * 2); ctx.stroke();
    fillP(ctx, 'M-42 -18 Q-30 -28 -14 -21 Q0 -29 14 -21 Q26 -27 34 -19 Z', w.cap);
    ctx.restore();
  };

  ART.snowman = function (ctx, x, y, s) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(s, s);
    ctx.translate(-30, -86);
    E(ctx, 30, 86, 20, 4, 0, 'rgba(31,42,77,0.14)');
    ctx.strokeStyle = '#8E5E39'; ctx.lineWidth = 2.5; ctx.lineCap = 'round';
    ctx.beginPath(); ctx.moveTo(14, 46); ctx.lineTo(4, 36); ctx.moveTo(46, 46); ctx.lineTo(56, 38); ctx.stroke();
    C(ctx, 30, 70, 17.4, INK); C(ctx, 30, 46, 13.4, INK); C(ctx, 30, 27, 10.9, INK);
    C(ctx, 30, 70, 16, '#FFFFFF');
    C(ctx, 30, 46, 12, '#FFFFFF');
    fillP(ctx, 'M38 62 A16 16 0 0 1 30 86 A16 16 0 0 0 38 62 Z', '#DCE8FF');
    C(ctx, 30, 27, 9.5, '#FFFFFF');
    box(ctx, 21, 34, 18, 5, 2.5, '#FF6FA3');
    box(ctx, 34, 36, 5, 10, 2, '#FF6FA3');
    box(ctx, 22, 13, 16, 8, 2, INK);
    box(ctx, 19, 20, 22, 3, 1.5, INK);
    C(ctx, 27, 26, 1.4, INK); C(ctx, 33, 26, 1.4, INK);
    fillP(ctx, 'M30 28.5 L38 30 L30 31 Z', '#FF9A3D');
    ctx.restore();
  };

  // "Snow Puff": an original, grumpy fluffball that patrols side to side.
  ART.puff = function (ctx, x, y, s, t, dir) {
    const b = Math.abs(Math.sin(t * 7));
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(s, s);
    E(ctx, 0, 2, 20 - b * 4, 5, 0, 'rgba(31,42,77,0.16)');
    ctx.translate(0, -b * 7);
    ctx.scale(1 + (1 - b) * 0.08, 1 - (1 - b) * 0.08);
    E(ctx, -9, -2, 7, 4, 0, '#8C7AE6');
    E(ctx, 9, -2, 7, 4, 0, '#8C7AE6');
    const fluff = [[0, -22, 18], [-14, -17, 11], [14, -17, 11], [-10, -34, 11], [10, -34, 11], [0, -39, 10]];
    fluff.forEach((f) => C(ctx, f[0], f[1], f[2] + 2.6, INK));
    fluff.forEach((f) => C(ctx, f[0] + 1.5, f[1] + 1.5, f[2], '#C9C0F5'));
    fluff.forEach((f) => C(ctx, f[0], f[1], f[2], '#FFFFFF'));
    E(ctx, -6 + dir * 2, -24, 3.2, 4.2, 0, INK);
    E(ctx, 6 + dir * 2, -24, 3.2, 4.2, 0, INK);
    ctx.strokeStyle = INK; ctx.lineWidth = 2.6; ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.moveTo(-12, -32); ctx.lineTo(-3, -29);
    ctx.moveTo(12, -32); ctx.lineTo(3, -29);
    ctx.stroke();
    ctx.beginPath(); ctx.arc(dir * 2, -13, 4, Math.PI * 1.15, Math.PI * 1.85); ctx.stroke();
    E(ctx, -13, -20, 3, 2, 0, 'rgba(255,127,168,0.5)');
    E(ctx, 13, -20, 3, 2, 0, 'rgba(255,127,168,0.5)');
    ctx.restore();
  };

  ART.ball = function (ctx, x, y, r, rot) {
    ART.shadow(ctx, x, y + 2, r * 1.05, r * 0.28, 0.18);
    C(ctx, x, y - r, r, '#D3E1F7');
    C(ctx, x - r * 0.12, y - r * 1.08, r * 0.9, '#FFFFFF');
    ctx.save();
    ctx.translate(x, y - r);
    ctx.rotate(rot);
    ctx.strokeStyle = '#C3D4EE'; ctx.lineWidth = 2.5; ctx.lineCap = 'round';
    for (let i = 0; i < 3; i++) {
      ctx.rotate(Math.PI * 2 / 3);
      ctx.beginPath(); ctx.arc(r * 0.35, 0, r * 0.35, -0.8, 0.8); ctx.stroke();
    }
    ctx.restore();
    ctx.lineWidth = 2.5; ctx.strokeStyle = INK;
    ctx.beginPath(); ctx.arc(x, y - r, r, 0, Math.PI * 2); ctx.stroke();
  };

  ART.coin = function (ctx, x, y, s, t, ph) {
    const k = 0.25 + 0.75 * Math.abs(Math.cos(t * 4 + ph));
    ctx.save();
    ctx.translate(x, y - 14 * s);
    ctx.scale(s * k, s);
    C(ctx, 0, 0, 11, '#FFC93C');
    ctx.lineWidth = 2; ctx.strokeStyle = INK;
    ctx.beginPath(); ctx.arc(0, 0, 11, 0, Math.PI * 2); ctx.stroke();
    ctx.strokeStyle = '#FFFFFF'; ctx.lineWidth = 2; ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.moveTo(0, -6); ctx.lineTo(0, 6);
    ctx.moveTo(-5.2, -3); ctx.lineTo(5.2, 3);
    ctx.moveTo(-5.2, 3); ctx.lineTo(5.2, -3);
    ctx.stroke();
    ctx.restore();
  };

  ART.item = function (ctx, x, y, s, t, type) {
    const by = y - 22 * s + Math.sin(t * 3) * 4 * s;
    ART.shadow(ctx, x, y, 14 * s, 4 * s, 0.14);
    ctx.save();
    ctx.translate(x, by);
    ctx.scale(s, s);
    if (type === 'crystal') {
      const gl = ctx.createRadialGradient(0, 0, 10, 0, 0, 30);
      gl.addColorStop(0, 'rgba(143,243,255,0.75)');
      gl.addColorStop(1, 'rgba(143,243,255,0)');
      C(ctx, 0, 0, 30, gl);
    }
    C(ctx, 0, 0, 17, type === 'shield' ? '#BFE6FF' : type === 'magnet' ? '#FFD6E2' : type === 'crystal' ? '#E3FBFF' : '#FFF0C2');
    ctx.lineWidth = 2.5; ctx.strokeStyle = INK;
    ctx.beginPath(); ctx.arc(0, 0, 17, 0, Math.PI * 2); ctx.stroke();
    E(ctx, -6, -8, 6, 3.5, -0.5, 'rgba(255,255,255,0.8)');
    ctx.lineCap = 'round';
    if (type === 'magnet') {
      ctx.strokeStyle = '#E5484D'; ctx.lineWidth = 6;
      ctx.beginPath(); ctx.arc(0, 1, 6, 0, Math.PI); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(-6, 1); ctx.lineTo(-6, -6); ctx.moveTo(6, 1); ctx.lineTo(6, -6); ctx.stroke();
      ctx.strokeStyle = '#DCE6F2';
      ctx.beginPath(); ctx.moveTo(-6, -6); ctx.lineTo(-6, -9); ctx.moveTo(6, -6); ctx.lineTo(6, -9); ctx.stroke();
    } else if (type === 'shield') {
      ctx.strokeStyle = '#2A6FC4'; ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(0, 0, 9, 0, Math.PI * 2); ctx.stroke();
      E(ctx, -3, -4, 3, 2, -0.5, '#FFFFFF');
    } else if (type === 'crystal') {
      // Faceted ice crystal with a gold glint.
      fillP(ctx, 'M0 -12 L8 -3 L0 12 L-8 -3 Z', '#3FD0F5');
      fillP(ctx, 'M0 -12 L2.5 -3 L0 12 L-8 -3 Z', '#A8F1FF');
      fillP(ctx, 'M0 -12 L8 -3 L-8 -3 Z', 'rgba(255,255,255,0.55)');
      ctx.strokeStyle = INK; ctx.lineWidth = 2; ctx.lineJoin = 'round';
      ctx.beginPath(); ctx.moveTo(0, -12); ctx.lineTo(8, -3); ctx.lineTo(0, 12); ctx.lineTo(-8, -3); ctx.closePath(); ctx.moveTo(-8, -3); ctx.lineTo(8, -3); ctx.stroke();
      fillP(ctx, 'M10 -13 L11.2 -10 L14 -9 L11.2 -8 L10 -5 L8.8 -8 L6 -9 L8.8 -10 Z', '#FFD84A');
    } else {
      fillP(ctx, 'M-7 -9 H7 L0 0 L7 9 H-7 L0 0 Z', '#E0A21B');
      ctx.strokeStyle = INK; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.moveTo(-8, -10); ctx.lineTo(8, -10); ctx.moveTo(-8, 10); ctx.lineTo(8, 10); ctx.stroke();
    }
    ctx.restore();
  };

  ART.ice = function (ctx, x, y, w, h) {
    E(ctx, x, y, w / 2 + 3, h / 2 + 3, 0, 'rgba(120,180,240,0.35)');
    E(ctx, x, y, w / 2, h / 2, 0, '#C6ECFF');
    ctx.strokeStyle = 'rgba(255,255,255,0.9)'; ctx.lineWidth = 2.5; ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.moveTo(x - w * 0.28, y - h * 0.1); ctx.lineTo(x - w * 0.12, y + h * 0.12);
    ctx.moveTo(x + w * 0.05, y - h * 0.18); ctx.lineTo(x + w * 0.24, y + h * 0.08);
    ctx.stroke();
  };

  // Ice hole: a dark crack in the snow with a frozen rim; flat on the ground, jump it or go round.
  ART.hole = function (ctx, x, y, w, h, t) {
    const rx = w / 2;
    const ry = h / 2;
    E(ctx, x, y + ry * 0.25, rx + 8, ry + 6, 0, 'rgba(120,160,220,0.35)');
    E(ctx, x, y, rx + 5, ry + 4, 0, '#FFFFFF');
    ctx.save();
    ctx.beginPath();
    const n = 14;
    for (let i = 0; i <= n; i++) {
      const a = (i / n) * Math.PI * 2;
      const k = i % 2 ? 0.86 : 1;
      const px = x + Math.cos(a) * rx * k;
      const py = y + Math.sin(a) * ry * (i % 2 ? 0.8 : 1);
      if (i === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
    }
    ctx.closePath();
    const gr = ctx.createLinearGradient(0, y - ry, 0, y + ry);
    gr.addColorStop(0, '#7CC4F2');
    gr.addColorStop(0.45, '#2C5C9E');
    gr.addColorStop(1, '#152C55');
    ctx.fillStyle = gr;
    ctx.fill();
    ctx.lineJoin = 'round'; ctx.lineWidth = 2.6; ctx.strokeStyle = INK;
    ctx.stroke();
    ctx.clip();
    E(ctx, x, y - ry * 0.55, rx * 0.8, ry * 0.32, 0, 'rgba(210,240,255,0.55)');
    ctx.restore();
    ctx.fillStyle = '#FFFFFF';
    for (let i = 0; i < 4; i++) {
      const ix = x - rx * 0.6 + (rx * 1.2 / 3) * i;
      ctx.beginPath(); ctx.moveTo(ix - 4, y - ry * 0.82); ctx.lineTo(ix + 4, y - ry * 0.82); ctx.lineTo(ix, y - ry * 0.82 + 7 + (i % 2) * 3); ctx.closePath(); ctx.fill();
    }
    const tw = 0.5 + 0.5 * Math.sin(t * 5);
    ctx.globalAlpha *= tw;
    ART.shape(ctx, 'star', x + rx * 0.55, y - ry * 0.5, 4, '#FFFFFF');
    ctx.globalAlpha /= tw || 1;
  };

  // "Peekaboo bear": an original bear cub that pops out of a snow mound, paws up. Mound shakes first.
  ART.bear = function (ctx, x, y, s, t, up, warn, ph) {
    const shake = warn && up < 0.05 ? Math.sin(t * 46 + ph) * 2.2 : 0;
    const k = Math.max(0, Math.min(1, up));
    const pop = k < 1 ? k : 1 + Math.sin(t * 6 + ph) * 0.02;
    ctx.save();
    ctx.translate(x + shake * s, y);
    ctx.scale(s, s);
    E(ctx, 0, 2, 36, 8, 0, 'rgba(31,42,77,0.16)');
    E(ctx, 0, -6, 34, 16, 0, '#DCE8FF');
    E(ctx, -2, -9, 31, 14, 0, '#FFFFFF');
    if (k > 0.01) {
      ctx.save();
      ctx.beginPath(); ctx.rect(-60, -140, 120, 134); ctx.clip();
      ctx.translate(0, (1 - k) * 70);
      ctx.scale(pop, pop);
      const fur = '#B5774A';
      const furDark = '#8E5733';
      const cream = '#F6DDB8';
      ctx.lineWidth = 3; ctx.strokeStyle = INK; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
      const wave = Math.sin(t * 9 + ph) * 0.25;
      for (const side of [-1, 1]) {
        ctx.save();
        ctx.translate(side * 17, -30);
        ctx.rotate(-side * (2.45 + wave));
        box(ctx, -6, -4, 12, 28, 6, fur, INK, 3);
        C(ctx, 0, 24, 7.5, furDark);
        ctx.beginPath(); ctx.arc(0, 24, 7.5, 0, Math.PI * 2); ctx.stroke();
        ctx.restore();
      }
      E(ctx, 0, -22, 22, 20, 0, fur);
      ctx.beginPath(); ctx.ellipse(0, -22, 22, 20, 0, 0, Math.PI * 2); ctx.stroke();
      E(ctx, 0, -18, 13, 12, 0, cream);
      box(ctx, -18, -40, 36, 8, 4, '#4CC096', INK, 2.5);
      for (const side of [-1, 1]) {
        C(ctx, side * 16, -74, 8, fur);
        ctx.beginPath(); ctx.arc(side * 16, -74, 8, 0, Math.PI * 2); ctx.stroke();
        C(ctx, side * 16, -74, 4, cream);
      }
      C(ctx, 0, -58, 22, fur);
      ctx.beginPath(); ctx.arc(0, -58, 22, 0, Math.PI * 2); ctx.stroke();
      E(ctx, 0, -50, 11, 8.5, 0, cream);
      E(ctx, 0, -55, 4.5, 3.2, 0, INK);
      C(ctx, -8, -63, 3.4, INK); C(ctx, 8, -63, 3.4, INK);
      C(ctx, -7, -64.4, 1.2, '#FFFFFF'); C(ctx, 9, -64.4, 1.2, '#FFFFFF');
      E(ctx, 0, -46, 3.6, 3 + Math.abs(Math.sin(t * 5 + ph)) * 1.5, 0, '#7A2E3B');
      E(ctx, -14, -53, 3.5, 2.2, 0, 'rgba(255,127,168,0.55)');
      E(ctx, 14, -53, 3.5, 2.2, 0, 'rgba(255,127,168,0.55)');
      ctx.restore();
    }
    // Front lip of the mound hides the bear's lower half.
    E(ctx, 0, -4, 33, 9, 0, '#FFFFFF');
    E(ctx, 8, -1, 22, 5, 0, '#E7F0FF');
    if (warn && k < 0.05) {
      for (const side of [-1, 1]) {
        C(ctx, side * 11 + shake, -14, 6, '#B5774A');
        ctx.lineWidth = 2.5; ctx.strokeStyle = INK;
        ctx.beginPath(); ctx.arc(side * 11 + shake, -14, 6, Math.PI, 0); ctx.stroke();
      }
      E(ctx, 0, -4, 33, 9, 0, '#FFFFFF');
      C(ctx, -14 + shake * 2, -22, 2.5, '#FFFFFF');
      C(ctx, 12 - shake * 2, -25, 2, '#FFFFFF');
    }
    ctx.restore();
  };

  ART.mogul = function (ctx, x, y, s, w) {
    E(ctx, x, y, 30 * s, 9 * s, 0, w.shade);
    E(ctx, x - 3 * s, y - 3 * s, 26 * s, 7 * s, 0, w.snow);
    E(ctx, x - 10 * s, y - 5 * s, 8 * s, 2 * s, 0, '#FFFFFF');
  };

  ART.shape = function (ctx, kind, x, y, r, fill) {
    ctx.save();
    ctx.translate(x, y);
    ctx.beginPath();
    if (kind === 'circle') ctx.arc(0, 0, r, 0, Math.PI * 2);
    else if (kind === 'square') RR(ctx, -r * 0.9, -r * 0.9, r * 1.8, r * 1.8, r * 0.3);
    else if (kind === 'tri') { ctx.moveTo(0, -r * 1.05); ctx.lineTo(r * 1.05, r * 0.85); ctx.lineTo(-r * 1.05, r * 0.85); ctx.closePath(); }
    else if (kind === 'diamond') { ctx.moveTo(0, -r * 1.1); ctx.lineTo(r * 0.9, 0); ctx.lineTo(0, r * 1.1); ctx.lineTo(-r * 0.9, 0); ctx.closePath(); }
    else {
      for (let i = 0; i < 10; i++) {
        const a = -Math.PI / 2 + i * Math.PI / 5;
        const rr = i % 2 === 0 ? r * 1.1 : r * 0.48;
        if (i === 0) ctx.moveTo(Math.cos(a) * rr, Math.sin(a) * rr); else ctx.lineTo(Math.cos(a) * rr, Math.sin(a) * rr);
      }
      ctx.closePath();
    }
    ctx.fillStyle = fill;
    ctx.fill();
    ctx.lineWidth = 2.5; ctx.strokeStyle = INK; ctx.lineJoin = 'round';
    ctx.stroke();
    ctx.restore();
  };

  // A row of answer gates. xs: screen x of segment edges; y: snow line; opts: answer options.
  ART.gateRow = function (ctx, xs, y, sc, opts, state) {
    const n = opts.length;
    const poleH = 104 * sc;
    for (let i = 0; i <= n; i++) {
      const px = i === 0 ? xs[0] + 14 * sc : i === n ? xs[n] - 14 * sc : xs[i];
      ctx.save();
      ctx.translate(px, y);
      E(ctx, 0, 0, 12 * sc, 4 * sc, 0, 'rgba(31,42,77,0.16)');
      box(ctx, -5 * sc, -poleH, 10 * sc, poleH, 5 * sc, '#FFFFFF', INK, 2);
      ctx.save();
      RR(ctx, -5 * sc, -poleH, 10 * sc, poleH, 5 * sc);
      ctx.clip();
      ctx.fillStyle = '#FF5C93';
      for (let k = 0; k < poleH; k += 22 * sc) ctx.fillRect(-6 * sc, -poleH + k, 12 * sc, 11 * sc);
      ctx.restore();
      ctx.lineWidth = 2; ctx.strokeStyle = INK;
      RR(ctx, -5 * sc, -poleH, 10 * sc, poleH, 5 * sc); ctx.stroke();
      ctx.restore();
    }
    for (let i = 0; i < n; i++) {
      const cx = (xs[i] + xs[i + 1]) / 2;
      const w = Math.min(xs[i + 1] - xs[i] - 26 * sc, 124 * sc);
      const h = 58 * sc;
      const top = y - poleH + 4 * sc;
      const o = opts[i];
      let fill = o.color || '#3FA9F5';
      if (state.done && i === state.correct) fill = '#2FB37A';
      else if (state.done && i === state.picked) fill = '#9AA3B8';
      else if (state.reveal) fill = i === state.correct ? '#FFB81F' : '#9AA3B8';
      ctx.save();
      box(ctx, cx - w / 2, top + 5 * sc, w, h, 16 * sc, INK);
      box(ctx, cx - w / 2, top, w, h, 16 * sc, fill, INK, 3);
      box(ctx, cx - w / 2 + 8 * sc, top + 4 * sc, w - 16 * sc, 10 * sc, 6 * sc, 'rgba(255,255,255,0.35)');
      ctx.restore();
      if (o.shape) ART.shape(ctx, o.shape, cx, top + h / 2, 15 * sc, o.shapeColor || '#FFFFFF');
      else ART.text(ctx, o.label, cx, top + h / 2 + 1, (String(o.label).length > 3 ? 20 : 30) * sc, '#FFFFFF');
      if ((state.done || state.reveal) && i === state.correct) ART.text(ctx, '✓', cx + w / 2 - 6 * sc, top, 22 * sc, '#7DE0A8');
    }
  };

  ART.sign = function (ctx, x, y, s, left, right) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(s, s);
    E(ctx, 0, 0, 16, 4, 0, 'rgba(31,42,77,0.16)');
    box(ctx, -4, -70, 8, 70, 3, '#8E5E39', INK, 2);
    box(ctx, -78, -96, 156, 34, 10, '#C98A5A', INK, 3);
    box(ctx, -72, -92, 144, 6, 3, 'rgba(255,255,255,0.25)');
    ART.text(ctx, '← ' + left, -38, -79, 15, '#FFFFFF');
    ART.text(ctx, right + ' →', 40, -79, 15, '#FFFFFF');
    ctx.restore();
  };

  ART.arch = function (ctx, xL, xR, y, sc, label, color) {
    const h = 120 * sc;
    for (const px of [xL, xR]) {
      E(ctx, px, y, 14 * sc, 4 * sc, 0, 'rgba(31,42,77,0.16)');
      box(ctx, px - 7 * sc, y - h, 14 * sc, h, 5 * sc, '#B9774A', INK, 2.5);
    }
    box(ctx, xL - 10 * sc, y - h - 30 * sc, xR - xL + 20 * sc, 40 * sc, 12 * sc, '#FFF4E0', INK, 3);
    if (label === 'FINISH') {
      ctx.save();
      RR(ctx, xL - 10 * sc, y - h - 30 * sc, xR - xL + 20 * sc, 40 * sc, 12 * sc);
      ctx.clip();
      const cell = 10 * sc;
      for (let cx = xL - 10 * sc, i = 0; cx < xR + 10 * sc; cx += cell, i++) {
        for (let r = 0; r < 4; r++) if ((i + r) % 2 === 0) { ctx.fillStyle = 'rgba(43,33,64,0.18)'; ctx.fillRect(cx, y - h - 30 * sc + r * cell, cell, cell); }
      }
      ctx.restore();
    }
    ART.text(ctx, label, (xL + xR) / 2, y - h - 10 * sc, 24 * sc, color);
  };

  // Sky, sun and mountains in the band above the slope crest.
  // Layered backdrop: each layer slides a little as the skier steers (px), farther layers less.
  function cloud(ctx, x, y, s) {
    E(ctx, x + 2 * s, y + 9 * s, 30 * s, 6 * s, 0, 'rgba(150,185,235,0.35)');
    C(ctx, x - 16 * s, y + 2 * s, 11 * s, '#FFFFFF');
    C(ctx, x, y - 4 * s, 15 * s, '#FFFFFF');
    C(ctx, x + 17 * s, y + 1 * s, 12 * s, '#FFFFFF');
    box(ctx, x - 24 * s, y, 50 * s, 12 * s, 6 * s, '#FFFFFF');
    E(ctx, x - 5 * s, y - 10 * s, 7 * s, 3 * s, -0.3, 'rgba(255,255,255,0.9)');
  }
  const ridgeY = (x) => 100 + Math.sin(x / 64) * 6 + Math.sin(x / 27 + 1.3) * 3;
  ART.sky = function (ctx, W, H, w, t, px) {
    const par = (k) => -(px || 0) * k;
    const g = ctx.createLinearGradient(0, 0, 0, H);
    g.addColorStop(0, w.skyTop);
    g.addColorStop(0.55, w.skyMid);
    g.addColorStop(1, w.skyLow);
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, W, H);
    const sunY = H * 0.32;
    ctx.save();
    ctx.translate(w.sunX + par(0.02), sunY);
    ctx.rotate(t * 0.05);
    ctx.fillStyle = 'rgba(255,255,255,0.13)';
    for (let i = 0; i < 9; i++) {
      ctx.rotate(Math.PI * 2 / 9);
      ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(150, -16); ctx.lineTo(150, 16); ctx.closePath(); ctx.fill();
    }
    ctx.restore();
    ctx.save();
    ctx.shadowColor = w.sunGlow;
    ctx.shadowBlur = 40;
    C(ctx, w.sunX + par(0.02), sunY, 26, w.sun);
    ctx.restore();
    for (let i = 0; i < 3; i++) {
      const cx = ((t * (5 + i * 3) + i * 170) % (W + 180)) - 90 + par(0.03);
      cloud(ctx, cx, 22 + i * 19, 0.75 + (i % 2) * 0.25);
    }
    ctx.save();
    ctx.translate(par(0.05), H - 150);
    fillP(ctx, 'M-60 200 L-20 118 L40 90 L80 118 L140 40 L200 110 L250 70 L300 118 L350 60 L410 128 L450 100 L450 200 Z', w.mtFar);
    ctx.fillStyle = 'rgba(255,255,255,0.22)';
    ctx.fill(P('M140 40 L200 110 L156 110 Z M250 70 L300 118 L262 118 Z M350 60 L410 128 L366 128 Z M40 90 L80 118 L50 118 Z'));
    fillP(ctx, 'M140 40 L158 64 Q151 60 147 67 Q141 60 135 67 Q129 61 122 64 Z M250 70 L264 88 Q258 85 254 90 Q249 85 244 90 Q240 86 236 88 Z M350 60 L366 80 Q360 77 356 83 Q350 77 344 83 Q339 78 334 80 Z', w.cap);
    ctx.restore();
    ctx.save();
    ctx.translate(par(0.1), H - 150);
    fillP(ctx, 'M-60 200 L-10 150 L60 122 L120 168 L190 100 L260 160 L330 110 L410 170 L450 140 L450 200 Z', w.mtNear);
    ctx.fillStyle = 'rgba(255,255,255,0.2)';
    ctx.fill(P('M60 122 L120 168 L72 168 Z M190 100 L260 160 L204 160 Z M330 110 L410 170 L344 170 Z'));
    fillP(ctx, 'M60 122 L76 140 Q70 137 66 143 Q60 137 54 143 Q49 138 44 140 Z M190 100 L210 124 Q203 120 198 127 Q191 120 184 127 Q178 121 170 124 Z M330 110 L346 130 Q340 126 335 132 Q329 126 323 132 Q318 127 314 130 Z', w.cap);
    const gx = ((t * 18) % 520) - 60;
    ctx.strokeStyle = '#4A4560'; ctx.lineWidth = 1.4;
    ctx.beginPath(); ctx.moveTo(-20, 190); ctx.lineTo(410, 120); ctx.stroke();
    const gy = 190 - (gx + 20) * (70 / 430);
    box(ctx, gx - 11, gy + 7, 22, 16, 5, '#FF6FA3', INK, 2);
    box(ctx, gx - 7, gy + 11, 14, 6, 2, '#FFE9A8');
    ctx.restore();
    // Near ridge with a row of little pines: the fastest backdrop layer.
    ctx.save();
    ctx.translate(par(0.18), H - 150);
    ctx.beginPath();
    ctx.moveTo(-60, 200);
    for (let x = -60; x <= 450; x += 10) ctx.lineTo(x, ridgeY(x));
    ctx.lineTo(450, 200);
    ctx.closePath();
    ctx.fillStyle = w.hill;
    ctx.fill();
    for (let i = 0; i < 22; i++) {
      const x = -50 + i * 23 + Math.sin(i * 7.3) * 6;
      const h = 15 + (Math.sin(i * 12.9898) * 0.5 + 0.5) * 10;
      const y = ridgeY(x) + 3;
      ctx.fillStyle = w.tree2;
      ctx.beginPath(); ctx.moveTo(x, y - h); ctx.lineTo(x + h * 0.42, y); ctx.lineTo(x - h * 0.42, y); ctx.closePath(); ctx.fill();
      ctx.fillStyle = w.cap;
      ctx.beginPath(); ctx.moveTo(x, y - h); ctx.lineTo(x + h * 0.16, y - h * 0.62); ctx.lineTo(x - h * 0.16, y - h * 0.62); ctx.closePath(); ctx.fill();
    }
    ctx.restore();
  };

  ART.crest = function (ctx, W, y, w) {
    ctx.save();
    ctx.translate(0, y);
    fillP(ctx, 'M0 46 Q90 6 200 32 Q300 56 390 20 L390 90 L0 90 Z', w.snow);
    ctx.globalAlpha = 0.45;
    fillP(ctx, 'M0 58 Q90 20 200 44 Q300 66 390 32 L390 90 L0 90 Z', w.shade);
    ctx.globalAlpha = 1;
    fillP(ctx, 'M0 64 Q90 28 200 50 Q300 72 390 38 L390 90 L0 90 Z', w.snow);
    ctx.restore();
  };

  window.PSArt = ART;
})();
