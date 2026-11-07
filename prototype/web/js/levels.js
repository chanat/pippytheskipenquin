// Pippy The Ski Penguin prototype — World 1 levels, question generator and procedural course builder.
// Units: 1 world unit = 1 logical pixel at the skier's depth. z grows downhill.
(function () {
  function rng(seed) {
    let a = seed >>> 0;
    return function () {
      a = (a + 0x6D2B79F5) >>> 0;
      let t = a;
      t = Math.imul(t ^ (t >>> 15), t | 1);
      t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }
  const pick = (r, arr) => arr[Math.floor(r() * arr.length)];
  const int = (r, a, b) => a + Math.floor(r() * (b - a + 1));
  function shuffle(r, arr) {
    for (let i = arr.length - 1; i > 0; i--) {
      const j = Math.floor(r() * (i + 1));
      const tmp = arr[i]; arr[i] = arr[j]; arr[j] = tmp;
    }
    return arr;
  }

  const GATE_COLORS = ['#3FA9F5', '#FF5C93', '#9B7BF0'];
  const SHAPES = ['circle', 'square', 'tri', 'star', 'diamond'];
  const SHAPE_NAME = { circle: 'วงกลม', square: 'สี่เหลี่ยม', tri: 'สามเหลี่ยม', star: 'ดาว', diamond: 'ข้าวหลามตัด' };

  function numberOptions(r, answer, n) {
    const set = new Set([answer]);
    while (set.size < n) {
      const v = answer + int(r, 1, 4) * (r() < 0.5 ? -1 : 1);
      if (v >= 0) set.add(v);
    }
    return shuffle(r, Array.from(set)).map((v) => ({ label: String(v), correct: v === answer }));
  }

  // Easy arithmetic, one operator per question. − and ÷ are built as the inverse of + and ×,
  // so all four sit at the same small-number level (sums ≤ 18, times tables 2–5).
  const OPS = ['+', '−', '×', '÷'];
  function arith(r, n, op) {
    let a, b, ans;
    if (op === '+') { a = int(r, 2, 9); b = int(r, 1, 9); ans = a + b; }
    else if (op === '−') { ans = int(r, 1, 9); b = int(r, 1, 9); a = ans + b; }
    else if (op === '×') { a = int(r, 2, 5); b = int(r, 2, 5); ans = a * b; }
    else { ans = int(r, 2, 5); b = int(r, 2, 5); a = ans * b; }
    return { text: a + ' ' + op + ' ' + b + ' = ?', hint: 'เลี้ยวผ่านประตูคำตอบ!', options: numberOptions(r, ans, n) };
  }
  // Deals operators from a shuffled bag so a run mixes all four instead of repeating one.
  function opDealer(r) {
    const bag = [];
    return () => {
      if (!bag.length) bag.push.apply(bag, shuffle(r, OPS.slice()));
      return bag.pop();
    };
  }

  const Q = {
    picture(r, n) {
      const kinds = shuffle(r, SHAPES.slice()).slice(0, n);
      const target = kinds[0];
      return { text: 'ประตูไหนเป็นรูป' + SHAPE_NAME[target] + '?', hint: 'เลี้ยวผ่านประตูคำตอบ!', options: shuffle(r, kinds.map((k) => ({ shape: k, correct: k === target }))) };
    },
    math1(r, n, op) {
      return arith(r, n, op || pick(r, OPS));
    },
    odd(r, n) {
      const kinds = shuffle(r, SHAPES.slice());
      const opts = [];
      for (let i = 0; i < n - 1; i++) opts.push({ shape: kinds[0], correct: false });
      opts.push({ shape: kinds[1], correct: true });
      const tints = shuffle(r, ['#FFFFFF', '#FFE066', '#BDF2E9']);
      shuffle(r, opts).forEach((o, i) => { o.shapeColor = tints[i % tints.length]; });
      return { text: 'อันไหนไม่เข้าพวก?', hint: 'ดูที่รูปทรง ไม่ใช่สี', options: opts };
    },
    pattern(r, n) {
      const step = int(r, 2, 5);
      const start = int(r, 1, 9);
      return { text: start + ', ' + (start + step) + ', ' + (start + 2 * step) + ', ?', hint: 'ตัวต่อไปคืออะไร?', options: numberOptions(r, start + 3 * step, n) };
    },
    compare(r, n) {
      const set = new Set();
      while (set.size < n) set.add(int(r, 11, 99));
      const vals = Array.from(set);
      const max = Math.max.apply(null, vals);
      return { text: 'เลขไหนมากที่สุด?', hint: 'เลี้ยวผ่านเลขที่มากที่สุด!', options: vals.map((v) => ({ label: String(v), correct: v === max })) };
    }
  };

  // Difficulty rises level by level: speed, obstacle density, narrower safe corridor,
  // then new hazards (logs → Snow Puffs → rolling snowballs → ice + blizzard → rival race).
  const LEVELS = [
    { id: 1, name: 'แรกไถล', en: 'First Slide', tip: 'แตะค้างแล้วเลื่อนซ้าย-ขวาเพื่อเลี้ยว เก็บเกล็ดหิมะ หลบต้นไม้', len: 9000, v0: 210, v1: 250, weather: 'sunny', world: 'meadow', density: 0.22, corridor: 130, rocks: 0.2, q: ['picture', 'picture'], opts: 2, logs: 0, puffs: 0, balls: 0, ice: 0, forks: 0, items: [], holes: 0, bears: 0, combos: 0, seed: 101 },
    { id: 2, name: 'กระโดดดึ๋ง', en: 'Hop Hop', tip: 'แตะสั้นๆ หรือปัดขึ้น เพื่อกระโดดข้ามขอนไม้ ก้อนหิน และหลุมน้ำแข็ง · แตะซ้ำกลางอากาศ = ตีลังกา ลอยได้นานขึ้น', len: 10500, v0: 225, v1: 270, weather: 'sunny', world: 'meadow', density: 0.3, corridor: 120, rocks: 0.5, q: ['math1', 'math1', 'math1'], opts: 2, logs: 10, puffs: 0, balls: 0, ice: 0, forks: 0, items: [], holes: 3, bears: 0, combos: 0, seed: 202 },
    { id: 3, name: 'ประตูตัวเลข', en: 'Number Gates', tip: 'ระวังปุยหิมะจอมป่วน! มันเดินไปมา กระโดดข้ามได้ · กองหิมะสั่นๆ = หมีกำลังจะโผล่ เลี้ยวหลบ! · เก็บผลึกสีฟ้า = ชนอะไรก็พัง', len: 12000, v0: 235, v1: 285, weather: 'snow', world: 'meadow', density: 0.36, corridor: 110, rocks: 0.5, q: ['math1', 'math1', 'math1', 'math1', 'math1'], opts: 2, logs: 8, puffs: 9, balls: 0, ice: 0, forks: 0, items: ['crystal'], holes: 0, bears: 3, combos: 2, seed: 303 },
    { id: 4, name: 'สองทางเลือก', en: 'Two Paths', tip: 'ทางแยก! ซ้ายเกล็ดหิมะเยอะแต่มีขอนไม้ ขวาปลอดภัยกว่า · ก้อนหิมะกลิ้งมาจากข้างทาง', len: 13000, v0: 245, v1: 300, weather: 'snow', world: 'meadow', density: 0.38, corridor: 100, rocks: 0.5, q: ['math1', 'math1', 'math1'], opts: 2, logs: 8, puffs: 6, balls: 9, ice: 0, forks: 2, items: ['magnet', 'crystal', 'shield'], holes: 3, bears: 3, combos: 2, seed: 404 },
    { id: 5, name: 'ตัวไหนไม่เข้าพวก', en: 'Odd One Out', tip: 'พายุหิมะ มองให้ดี · แผ่นน้ำแข็งทำให้ลื่น · เก็บพลังพิเศษช่วยได้', len: 14000, v0: 250, v1: 310, weather: 'blizzard', world: 'meadow', density: 0.42, corridor: 95, rocks: 0.5, q: ['odd', 'odd', 'odd', 'odd', 'odd'], opts: 3, logs: 8, puffs: 9, balls: 7, ice: 10, forks: 0, items: ['shield', 'slow', 'crystal', 'magnet'], holes: 3, bears: 3, combos: 3, seed: 505 },
    { id: 6, name: 'แข่งกับเบนนี่', en: 'Race vs Benny', tip: 'ด่านบอส! แซงเบนนี่ให้ถึงเส้นชัยก่อน ทุกอย่างรวมกัน', len: 16000, v0: 260, v1: 325, weather: 'snow', world: 'sunset', density: 0.46, corridor: 90, rocks: 0.5, q: ['mixed', 'mixed', 'mixed', 'mixed', 'mixed', 'mixed'], opts: 3, logs: 10, puffs: 11, balls: 9, ice: 7, forks: 2, items: ['shield', 'crystal', 'slow', 'magnet'], rival: true, holes: 4, bears: 4, combos: 4, seed: 606 }
  ];

  function make(type, x, z, r) {
    switch (type) {
      case 'tree': return { type, x, z, s: 0.95 + r() * 0.45, hw: 16, jump: false };
      case 'rock': return { type, x, z, s: 0.6 + r() * 0.25, hw: 16, jump: true };
      case 'snowman': return { type, x, z, s: 1, hw: 16, jump: false };
      case 'log': return { type, x, z, s: 1, hw: 46, jump: true };
      case 'hole': return { type, x, z, hw: 34 + r() * 18, h: 30, jump: true };
      case 'bear': return { type, x, z, hw: 22, jump: false, up: 0, warn: false, ph: r() * 6 };
      default: return { type, x, z };
    }
  }

  // Holes and bears block the safe line, so clear scenery beside them to leave a way around.
  function clearAroundBig(objs) {
    const big = objs.filter((o) => o.type === 'hole' || o.type === 'bear');
    for (let i = objs.length - 1; i >= 0; i--) {
      const o = objs[i];
      if (o.type !== 'tree' && o.type !== 'rock' && o.type !== 'snowman') continue;
      if (Math.abs(o.x) > 175) continue;
      if (big.some((b) => Math.abs(o.z - b.z) < 80 && Math.abs(o.x - b.x) < (b.hw || 22) + 80)) objs.splice(i, 1);
    }
  }

  function build(L) {
    const r = rng(L.seed);
    const len = L.len;
    const objs = [];
    const decor = [];
    const rows = [];
    const forks = [];
    const ph1 = r() * 6;
    const ph2 = r() * 6;
    const amp = 55 + L.id * 9;
    const safeX = (z) => Math.max(-120, Math.min(120, Math.sin(z / 260 + ph1) * amp + Math.sin(z / 110 + ph2) * amp * 0.35));

    // The course stays seeded per level, but questions are re-rolled every run so players
    // solve them rather than memorise the gate positions.
    const qr = Math.random;
    const nextOp = opDealer(qr);
    const nq = L.q.length;
    for (let i = 0; i < nq; i++) {
      const z = Math.round(1500 + (len - 2050) * (nq === 1 ? 0.5 : i / (nq - 1)));
      const kind = L.q[i] === 'mixed' ? pick(qr, ['math1', 'pattern', 'odd', 'compare']) : L.q[i];
      const n = kind === 'odd' ? Math.max(3, L.opts) : L.opts;
      const q = Q[kind](qr, n, kind === 'math1' ? nextOp() : undefined);
      q.options.forEach((o, k) => { o.color = GATE_COLORS[k % GATE_COLORS.length]; });
      rows.push({ z, q, n, done: false, shown: false, correct: q.options.findIndex((o) => o.correct), picked: -1 });
    }
    const nearRow = (z) => rows.some((row) => z > row.z - 280 && z < row.z + 150);
    for (let i = 0; i < L.forks; i++) {
      let z0 = Math.round(950 + (len - 1700) * ((i + 0.5) / L.forks));
      while (nearRow(z0) || nearRow(z0 + 560)) z0 += 90;
      if (z0 + 560 < len - 200) forks.push({ z0, z1: z0 + 560 });
    }
    const inFork = (z) => forks.some((f) => z > f.z0 - 90 && z < f.z1 + 90);
    const busy = (z) => nearRow(z) || inFork(z) || z < 380 || z > len - 220;

    for (let z = -260; z < len + 700; z += 44 + r() * 30) {
      objs.push(make('tree', -188 - r() * 22, z, r));
      objs.push(make('tree', 188 + r() * 22, z + r() * 30, r));
    }
    for (let z = 420; z < len - 250; z += 70) {
      if (busy(z) || r() > L.density + 0.35) continue;
      const sx = safeX(z);
      const tries = 1 + (r() < L.density ? 1 : 0);
      for (let k = 0; k < tries; k++) {
        const x = -160 + r() * 320;
        if (Math.abs(x - sx) < L.corridor * 0.5 + 22) continue;
        const roll = r();
        const type = roll < L.rocks * 0.5 ? 'rock' : roll > 0.93 ? 'snowman' : 'tree';
        objs.push(make(type, x, z + r() * 40, r));
      }
    }
    const taken = [];
    const free = (z) => !busy(z) && !taken.some((t) => Math.abs(t - z) < 130);
    const spots = (count, salt) => {
      const out = [];
      for (let i = 0; i < count; i++) {
        let z = Math.round(520 + (len - 900) * ((i + 0.3 + r() * 0.4) / count) + salt);
        let guard = 0;
        while (!free(z) && guard++ < 20) z += 60;
        if (free(z)) { out.push(z); taken.push(z); }
      }
      return out;
    };
    spots(L.logs, 0).forEach((z) => objs.push(make('log', safeX(z), z, r)));
    spots(L.puffs, 35).forEach((z) => objs.push({ type: 'puff', x0: safeX(z), x: safeX(z), z, amp: 70 + r() * 40, w: 1.3 + r() * 0.8, ph: r() * 6, hw: 18, jump: true }));
    spots(L.balls, 70).forEach((z) => {
      const side = r() < 0.5 ? -1 : 1;
      objs.push({ type: 'ball', x: side * 205, z, vx: -side * (100 + r() * 50), vz: 70, rad: 20, active: false, rot: 0, hw: 26, jump: false });
    });
    spots(L.ice, 105).forEach((z) => objs.push({ type: 'ice', x: safeX(z) + (r() - 0.5) * 30, z, w: 120, h: 46 }));
    spots(L.holes || 0, 140).forEach((z) => objs.push(make('hole', safeX(z) + (r() - 0.5) * 30, z, r)));
    spots(L.bears || 0, 175).forEach((z) => objs.push(make('bear', safeX(z) + (r() - 0.5) * 24, z, r)));
    // Combo: a log then an ice hole close behind it, too long for one hop: flip over both, or go round.
    spots(L.combos || 0, 210).forEach((z) => {
      if (busy(z + 150)) return;
      taken.push(z + 120);
      objs.push(make('log', safeX(z), z, r));
      objs.push(make('hole', safeX(z + 120), z + 120, r));
    });
    clearAroundBig(objs);
    L.items.forEach((kind, i) => {
      let z = Math.round(len * (0.28 + i * (L.items.length > 3 ? 0.18 : 0.24)));
      // A power crystal sits ~3 s before the nearest question row so its auto-answer gets used.
      if (kind === 'crystal' && rows.length) z = rows.reduce((a, b) => (Math.abs(b.z - 800 - z) < Math.abs(a.z - 800 - z) ? b : a)).z - 800;
      while (busy(z)) z += 60;
      objs.push({ type: 'item', kind, x: safeX(z) + (r() < 0.5 ? -50 : 50), z });
    });
    for (let z = 460; z < len - 200; z += 240) {
      if (busy(z)) continue;
      for (let k = 0; k < 5; k++) objs.push({ type: 'coin', x: safeX(z + k * 34), z: z + k * 34, ph: k * 0.6 });
    }
    forks.forEach((f) => {
      objs.push({ type: 'sign', x: 0, z: f.z0 - 70, left: 'ทางลัด', right: 'ปลอดภัย' });
      for (let z = f.z0; z <= f.z1; z += 48) objs.push(make('tree', (r() - 0.5) * 12, z, r));
      for (let z = f.z0 + 60; z < f.z1 - 40; z += 34) objs.push({ type: 'coin', x: -100 + Math.sin(z / 50) * 20, z, ph: z * 0.01 });
      objs.push(make('log', -100, f.z0 + 200, r));
      objs.push(make('log', -96, f.z0 + 390, r));
      objs.push(make('rock', 96, f.z0 + 300, r));
      objs.push({ type: 'coin', x: 100, z: f.z0 + 140, ph: 0 });
      objs.push({ type: 'coin', x: 100, z: f.z0 + 450, ph: 1 });
    });
    for (let z = 0; z < len + 600; z += 150 + r() * 120) {
      decor.push({ type: 'shade', x: -160 + r() * 320, z, w: 160 + r() * 120, h: 34 + r() * 20 });
      if (r() < 0.6) decor.push({ type: 'mogul', x: -150 + r() * 300, z: z + 60, s: 0.6 + r() * 0.5 });
    }
    objs.sort((a, b) => a.z - b.z);
    return { len, objs, decor, rows, forks, safeX };
  }

  // Daily endless run: everyone gets the same seeded course for the UTC day, so a global
  // leaderboard compares like with like. Difficulty and speed climb with distance.
  const ENDLESS = { id: 0, endless: true, name: 'ไถลไม่รู้จบ', en: 'Daily Endless', tip: 'เส้นทางเดียวกันทั้งโลกในวันนี้ ไปให้ไกลที่สุด ยิ่งไกลยิ่งเร็วและยากขึ้น', len: 90000, v0: 230, v1: 440, weather: 'snow', world: 'meadow', q: [] };

  function dailySeed(d) {
    const dt = d || new Date();
    return dt.getUTCFullYear() * 10000 + (dt.getUTCMonth() + 1) * 100 + dt.getUTCDate();
  }

  function buildEndless(seed) {
    const r = rng(seed);
    const len = ENDLESS.len;
    const objs = [];
    const decor = [];
    const rows = [];
    const ph1 = r() * 6;
    const ph2 = r() * 6;
    const diff = (z) => Math.min(1, z / 30000);
    const safeX = (z) => {
      const amp = 60 + diff(z) * 70;
      return Math.max(-125, Math.min(125, Math.sin(z / 260 + ph1) * amp + Math.sin(z / 110 + ph2) * amp * 0.35));
    };
    const nextOp = opDealer(r);
    for (let z = 1800; z < len; z += 1000 + Math.round(r() * 350)) {
      const d = diff(z);
      const pool = d < 0.12 ? ['picture', 'math1'] : d < 0.4 ? ['math1', 'odd', 'compare'] : ['math1', 'odd', 'pattern', 'compare'];
      const kind = pick(r, pool);
      const n = kind === 'odd' || d > 0.3 ? 3 : 2;
      const q = Q[kind](r, n, kind === 'math1' ? nextOp() : undefined);
      q.options.forEach((o, k) => { o.color = GATE_COLORS[k % GATE_COLORS.length]; });
      rows.push({ z, q, n, done: false, shown: false, correct: q.options.findIndex((o) => o.correct), picked: -1 });
    }
    const nearRow = (z) => rows.some((row) => z > row.z - 280 && z < row.z + 150);
    for (let z = -260; z < len; z += 44 + r() * 30) {
      objs.push(make('tree', -188 - r() * 22, z, r));
      objs.push(make('tree', 188 + r() * 22, z + r() * 30, r));
    }
    let itemIx = 0;
    const kinds = ['shield', 'magnet', 'crystal', 'slow'];
    for (let z = 420; z < len - 200; z += 70) {
      if (nearRow(z)) continue;
      const d = diff(z);
      const sx = safeX(z);
      if (z > 9000 && r() < 0.012 + d * 0.025 && !nearRow(z + 150)) { objs.push(make('log', sx, z, r)); objs.push(make('hole', safeX(z + 120), z + 120, r)); z += 120; continue; }
      const roll = r();
      if (roll < 0.03 + d * 0.05) { objs.push(make('log', sx, z, r)); continue; }
      if (z > 2500 && roll < 0.05 + d * 0.08) { objs.push({ type: 'puff', x0: sx, x: sx, z, amp: 70 + r() * 40, w: 1.3 + r() * 0.9, ph: r() * 6, hw: 18, jump: true }); continue; }
      if (z > 5000 && roll < 0.065 + d * 0.1) { const side = r() < 0.5 ? -1 : 1; objs.push({ type: 'ball', x: side * 205, z, vx: -side * (100 + r() * 60), vz: 70, rad: 20, active: false, rot: 0, hw: 26, jump: false }); continue; }
      if (z > 8000 && roll < 0.08 + d * 0.1) { objs.push({ type: 'ice', x: sx, z, w: 120, h: 46 }); continue; }
      if (z > 3500 && roll < 0.105 + d * 0.12) { objs.push(make('hole', sx + (r() - 0.5) * 30, z, r)); continue; }
      if (z > 6000 && roll < 0.13 + d * 0.15) { objs.push(make('bear', sx + (r() - 0.5) * 24, z, r)); continue; }
      if (r() > 0.55 + d * 0.3) continue;
      const corridor = 130 - d * 45;
      for (let k = 0; k < 1 + (r() < 0.2 + d * 0.4 ? 1 : 0); k++) {
        const x = -160 + r() * 320;
        if (Math.abs(x - sx) < corridor * 0.5 + 22) continue;
        objs.push(make(r() < 0.25 ? 'rock' : 'tree', x, z + r() * 40, r));
      }
    }
    for (let z = 460; z < len - 200; z += 240) {
      if (nearRow(z)) continue;
      for (let k = 0; k < 5; k++) objs.push({ type: 'coin', x: safeX(z + k * 34), z: z + k * 34, ph: k * 0.6 });
    }
    for (let z = 3000; z < len; z += 3200 + Math.round(r() * 800)) {
      let zz = z;
      while (nearRow(zz)) zz += 60;
      objs.push({ type: 'item', kind: kinds[itemIx++ % kinds.length], x: safeX(zz) + (r() < 0.5 ? -50 : 50), z: zz });
    }
    for (let z = 0; z < len; z += 150 + r() * 120) {
      decor.push({ type: 'shade', x: -160 + r() * 320, z, w: 160 + r() * 120, h: 34 + r() * 20 });
      if (r() < 0.6) decor.push({ type: 'mogul', x: -150 + r() * 300, z: z + 60, s: 0.6 + r() * 0.5 });
    }
    clearAroundBig(objs);
    objs.sort((a, b) => a.z - b.z);
    return { len, objs, decor, rows, forks: [], safeX, endless: true };
  }

  window.PSData = { LEVELS, ENDLESS, build, buildEndless, dailySeed, SHAPE_NAME };
})();
