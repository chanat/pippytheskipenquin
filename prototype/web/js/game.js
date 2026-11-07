// Pippy The Ski Penguin prototype — game state, input, rules and HUD.
(function () {
  const PS = window.PS || (window.PS = {});
  const A = window.PSAudio;
  const DATA = window.PSData;
  const $ = (id) => document.getElementById(id);
  const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
  // Game lettering draws its gradient fill from data-text, so both must change together.
  const setG = (el, text) => { el.textContent = text; el.dataset.text = text; };
  const MSTAR = '<svg class="mstar" viewBox="0 0 24 24" aria-hidden="true"><use href="#starP"></use></svg>';
  const FLAKE = '<svg class="mstar" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9.5" fill="#FFC93C" stroke="#2B2140" stroke-width="2"></circle><path d="M12 7v10M7.7 9.5l8.6 5M7.7 14.5l8.6-5" stroke="#FFFFFF" stroke-width="2" stroke-linecap="round"></path></svg>';
  const reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  PS.LW = 390; PS.LH = 844; PS.scale = 1; PS.dpr = 1;
  // Jump: launch speed and gravity give a 40-unit, 0.62 s hop; a second tap in mid-air flips and relaunches.
  PS.AIR = 0.62; PS.JUMP_V = 258; PS.GRAV = 832; PS.TUMBLE = 1.1;
  PS.POWER = 6; PS.POWER_SPEED = 1.15; // power crystal: seconds of glow, speed multiplier
  PS.clock = 0; PS.G = null; PS.particles = []; PS.flakes = [];
  const canvas = $('game');
  const frameEl = $('frame');
  PS.ctx = canvas.getContext('2d');

  function load(k, d) {
    try { const v = JSON.parse(localStorage.getItem(k) || 'null'); return v ? Object.assign({}, d, v) : d; } catch (e) { return d; }
  }
  function save(k, v) {
    try { localStorage.setItem(k, JSON.stringify(v)); } catch (e) { /* storage blocked: progress stays in memory */ }
  }
  const settings = load('ps-settings', { speed: 1, music: true, sfx: true, gfx: 'high' });
  const progress = load('ps-progress', { stars: {}, best: {}, endlessBest: 0 });
  A.musicOn = settings.music;
  A.sfxOn = settings.sfx;
  PS.gfx = settings.gfx || 'high';

  let mode = 'title';
  PS.titleCourse = DATA.build(DATA.LEVELS[0]);

  function resize() {
    const w = frameEl.clientWidth;
    const h = frameEl.clientHeight;
    PS.dpr = Math.min(window.devicePixelRatio || 1, 2);
    PS.scale = w / PS.LW;
    PS.LH = h / PS.scale;
    canvas.width = Math.round(w * PS.dpr);
    canvas.height = Math.round(h * PS.dpr);
    canvas.style.width = w + 'px';
    canvas.style.height = h + 'px';
  }
  window.addEventListener('resize', resize);
  resize();

  for (let i = 0; i < 90; i++) {
    PS.flakes.push({ i, x: Math.random() * PS.LW, y: Math.random() * PS.LH, r: 1.5 + Math.random() * 3, o: 0.5 + Math.random() * 0.5, vy: 30 + Math.random() * 50, ph: Math.random() * 6 });
  }
  function updateFlakes(dt) {
    const blizz = PS.G && PS.G.L.weather === 'blizzard';
    for (const f of PS.flakes) {
      if (blizz) { f.y += f.vy * 6 * dt; f.x -= 260 * dt; } else { f.y += f.vy * dt; f.x += Math.sin(PS.clock * 1.5 + f.ph) * 20 * dt; }
      if (f.y > PS.LH + 10) { f.y = -10; f.x = Math.random() * (PS.LW + 200); }
      if (f.x < -20) f.x += PS.LW + 40;
      if (f.x > PS.LW + 20) f.x -= PS.LW + 40;
    }
  }

  function burst(x, z, n, color, o) {
    const opt = o || {};
    for (let i = 0; i < n; i++) {
      const life = (opt.life || 0.6) * (0.7 + Math.random() * 0.5);
      PS.particles.push({
        kind: opt.kind || 'puff', x: x + (Math.random() - 0.5) * 20, z: z + (Math.random() - 0.5) * 10, h: opt.h || 4,
        vx: (Math.random() - 0.5) * (opt.spread || 160), vz: (Math.random() - 0.5) * 60 + (opt.vz || 0), vh: (opt.up || 80) + Math.random() * 80,
        life, max: life, size: opt.size || 4 + Math.random() * 4, color: Array.isArray(color) ? color[i % color.length] : color, rot: Math.random() * 6
      });
    }
  }
  function updateParticles(dt) {
    for (const p of PS.particles) {
      p.life -= dt; p.x += p.vx * dt; p.z += p.vz * dt; p.h += p.vh * dt; p.vh -= 260 * dt; p.rot += dt * 6;
      if (p.h < 0) { p.h = 0; p.vh *= -0.3; }
    }
    PS.particles = PS.particles.filter((p) => p.life > 0);
  }

  // ---------- HUD ----------
  let lastHearts = -1;
  let lastCoinSfx = 0;
  function fmt(t) { const s = Math.max(0, t); return Math.floor(s / 60) + ':' + String(Math.floor(s % 60)).padStart(2, '0'); }
  function toast(text, color) {
    const el = $('toast');
    el.textContent = text;
    el.style.color = color || '#FFFFFF';
    el.classList.remove('show');
    void el.offsetWidth;
    el.classList.add('show');
  }
  // Coin streak popup: one gold "+N" above Pip that counts up while pickups keep coming,
  // then floats off once the streak breaks. Renderers publish PS.pipScreen (CSS px, above the head).
  const COIN_STREAK = 0.7;
  const coinPop = { el: null, n: 0, last: -9 };
  function releaseCoinPop() {
    const el = coinPop.el;
    if (!el) return;
    coinPop.el = null;
    el.classList.remove('bump');
    el.classList.add('out');
    setTimeout(() => el.remove(), 600);
  }
  // Sits beside Pip's head (not above it) so it never covers the question card.
  function placeCoinPop() {
    const p = PS.pipScreen;
    if (!coinPop.el || !p) return;
    const left = coinPop.el.classList.contains('left');
    coinPop.el.style.left = (p.x + (left ? -24 : 24)).toFixed(1) + 'px';
    coinPop.el.style.top = (p.y + 14).toFixed(1) + 'px';
  }
  function coinPopup() {
    if (!coinPop.el || PS.clock - coinPop.last > COIN_STREAK) {
      releaseCoinPop();
      const el = document.createElement('div');
      el.className = 'cpop gtext gold';
      if (PS.pipScreen && PS.pipScreen.x > PS.LW * PS.scale - 90) el.classList.add('left');
      $('cpops').appendChild(el);
      coinPop.el = el;
      coinPop.n = 0;
    }
    coinPop.n += 1;
    coinPop.last = PS.clock;
    setG(coinPop.el, '+' + coinPop.n);
    placeCoinPop();
    for (const el of [coinPop.el, $('coins').parentNode]) {
      el.classList.remove('bump');
      void el.offsetWidth;
      el.classList.add('bump');
    }
  }
  function updateCoinPop() {
    if (!coinPop.el) return;
    if (PS.clock - coinPop.last > COIN_STREAK) releaseCoinPop();
    else placeCoinPop();
  }
  function clearCoinPops() {
    coinPop.el = null;
    $('cpops').textContent = '';
  }

  function updateHud(force) {
    const g = PS.G;
    if (!g) return;
    updateCoinPop();
    if (force || g.hearts !== lastHearts) {
      lastHearts = g.hearts;
      document.querySelectorAll('#hearts .heart').forEach((h, i) => h.classList.toggle('empty', i >= g.hearts));
      $('hearts').setAttribute('aria-label', 'หัวใจเหลือ ' + g.hearts + ' จาก 3');
    }
    $('coins').textContent = g.coins;
    $('timer').textContent = g.L.endless ? Math.floor(g.z / 10).toLocaleString() + ' ม.' : fmt(g.time);
    $('prog').hidden = !!g.L.endless;
    if (!g.L.endless) {
      const p = clamp(g.z / g.course.len, 0, 1);
      $('progFill').style.width = (p * 100).toFixed(1) + '%';
      $('progMe').style.left = (p * 100).toFixed(1) + '%';
      const rv = $('progRival');
      rv.hidden = !g.rival;
      if (g.rival) rv.style.left = (clamp(g.rival.z / g.course.len, 0, 1) * 100).toFixed(1) + '%';
    }
    const chips = [];
    if (g.shield) chips.push('ฟองกันชน');
    if (g.magnet > 0) chips.push('แม่เหล็ก ' + Math.ceil(g.magnet));
    if (g.slowmo > 0) chips.push('ช้าลง ' + Math.ceil(g.slowmo));
    if (g.power > 0) chips.push('ผลึกพลัง ' + Math.ceil(g.power));
    const pups = $('pups');
    const txt = chips.join(' · ');
    if (pups.textContent !== txt) pups.textContent = txt;
    pups.hidden = chips.length === 0;
    if (g.row && !g.row.done) {
      const dz = g.row.z - g.z;
      const left = clamp(dz / Math.max(1, g.v * g.readTime), 0, 1);
      $('qring').style.strokeDashoffset = (157 * (1 - left)).toFixed(1);
    }
  }
  function showQuestion(row) {
    const ans = row.q.options[row.correct];
    // With the power crystal the card fills in the answer (number questions) or says it's marked.
    const fillIn = row.reveal && ans.label != null && /\d.*\?$/.test(row.q.text);
    $('qtext').textContent = fillIn ? row.q.text.replace(/\?$/, ans.label) : row.q.text;
    $('qhint').textContent = row.reveal ? 'ผลึกพลังเฉลยให้แล้ว! พุ่งทะลุได้เลย' : row.q.hint;
    const card = $('qcard');
    card.hidden = false;
    card.classList.remove('pop');
    void card.offsetWidth;
    card.classList.add('pop');
  }
  function hideQuestion() { $('qcard').hidden = true; }

  function show(name) {
    ['title', 'map', 'intro', 'pause', 'result'].forEach((s) => { $('screen-' + s).hidden = s !== name; });
    $('hud').hidden = !(name === 'play' || name === 'pause' || name === 'intro');
  }

  // ---------- Screens ----------
  function unlocked(id) { return id === 1 || (progress.stars[id - 1] || 0) >= 1; }
  // Level map: node centres in the map's 390×1100 design space, level 1 at the bottom.
  const NODE_POS = [[110, 980], [270, 860], [130, 730], [260, 600], [120, 470], [220, 330]];
  const LOCK = '<svg width="26" height="29" viewBox="0 0 18 20" aria-hidden="true"><rect x="2" y="8" width="14" height="11" rx="3" fill="#8C84A8" stroke="#2B2140" stroke-width="2"></rect><path d="M5 8V6a4 4 0 0 1 8 0v2" fill="none" stroke="#2B2140" stroke-width="2.4"></path></svg>';
  let backTo = 'title';
  function starIcons(n) {
    let h = '';
    for (let i = 0; i < 3; i++) h += '<svg class="mstar' + (i < n ? '' : ' off') + '" viewBox="0 0 24 24" aria-hidden="true"><use href="#starP"></use></svg>';
    return h;
  }
  function buildGrid() {
    const wrap = $('mapNodes');
    wrap.textContent = '';
    const cur = nextPlayable();
    let total = 0;
    DATA.LEVELS.forEach((L, i) => {
      const ok = unlocked(L.id);
      const stars = progress.stars[L.id] || 0;
      total += stars;
      const [px, py] = NODE_POS[i] || [195, 980 - i * 130];
      const b = document.createElement('button');
      b.type = 'button';
      b.className = 'mnode' + (L.rival ? ' boss' : '') + (ok && L === cur ? ' cur' : '');
      b.style.left = (px / 390 * 100) + '%';
      b.style.top = (py / 1100 * 100) + '%';
      b.disabled = !ok;
      b.setAttribute('aria-label', 'ด่าน ' + L.id + ' ' + L.name + (ok ? ' ได้ ' + stars + ' ดาว' : ' ล็อกอยู่'));
      if (ok) { const n = document.createElement('span'); n.className = 'gtext'; setG(n, String(L.id)); b.appendChild(n); }
      else b.innerHTML = LOCK;
      const st = document.createElement('span'); st.className = 'mstars'; st.innerHTML = starIcons(stars);
      const nm = document.createElement('span'); nm.className = 'mname ' + (px < 195 ? 'r' : 'l'); nm.textContent = L.name;
      b.append(st, nm);
      b.addEventListener('click', () => { A.unlock(); A.sfx('pop'); backTo = 'map'; openLevel(L); });
      wrap.appendChild(b);
      if (ok && L === cur) { $('mapPip').style.left = b.style.left; $('mapPip').style.top = b.style.top; }
    });
    const max = DATA.LEVELS.length * 3;
    $('mapStars').textContent = total + '/' + max;
    $('mapCount').textContent = total + '/' + max;
    $('endlessBest').textContent = progress.endlessBest ? 'สถิติของคุณ ' + progress.endlessBest.toLocaleString() + ' ม.' : 'ยังไม่มีสถิติ';
    document.querySelectorAll('[data-speed]').forEach((b) => b.setAttribute('aria-pressed', String(Number(b.dataset.speed) === settings.speed)));
    $('btnMusic').setAttribute('aria-pressed', String(settings.music));
    $('btnSfx').setAttribute('aria-pressed', String(settings.sfx));
    $('btnGfx').setAttribute('aria-pressed', String(PS.gfx === 'high'));
  }
  function nextPlayable() {
    for (const L of DATA.LEVELS) if (unlocked(L.id) && (progress.stars[L.id] || 0) < 3) return L;
    return DATA.LEVELS[DATA.LEVELS.length - 1];
  }

  function newRun(L, course) {
    const mul = settings.speed;
    const avg = (L.v0 + L.v1) / 2 * mul;
    const par = L.endless ? 0 : Math.round(course.len / avg * 1.04 + 1.5);
    return {
      L, course, mul, par, z: 0, x: 0, tx: 0, vx: 0, v: L.v0 * mul, lean: 0, air: false, airT: 0, airH: 0, airV: 0, flip: null, flipA: 0, squashT: 0,
      hearts: 3, tumble: 0, invuln: 0, slow: 0, coins: 0, correct: 0, graded: course.rows.length, answered: 0, tumbles: 0,
      time: 0, combo: 0, magnet: 0, shield: false, slowmo: 0, icy: 0, started: false, finished: false, finishT: 0,
      failed: false, failT: 0, happyT: 0, sadT: 0, scaredT: 0, powerT: 0, lookX: 0, lookT: 0, sway: 0,
      trail: [], trailT: 0, shake: 0, row: null, oi: 0, lastNear: -9, lastHop: -9, power: 0, lastSmash: -9,
      readTime: L.id >= 5 || L.endless ? 3.1 : 3.5, rivalFirst: false,
      rival: L.rival ? { z: 60, x: 0, lean: 0, passed: false, speed: course.len / (par * 0.96) } : null
    };
  }

  function openLevel(L) {
    const course = L.endless ? DATA.buildEndless(DATA.dailySeed()) : DATA.build(L);
    PS.G = newRun(L, course);
    PS.particles = [];
    clearCoinPops();
    mode = 'intro';
    hideQuestion();
    $('introNum').textContent = L.endless ? 'โหมดรายวัน' : 'ด่าน ' + L.id;
    setG($('introName'), L.name);
    $('introTip').textContent = L.tip;
    const goals = $('introGoals');
    goals.textContent = '';
    const items = L.endless
      ? [[0, 'ไปให้ไกลที่สุดก่อนหัวใจหมด'], [0, 'ตอบถูก +300 · เกล็ดหิมะ +10'], [0, progress.endlessBest ? 'สถิติของคุณ ' + progress.endlessBest.toLocaleString() + ' ม.' : 'ยังไม่มีสถิติ ลองเลย!']]
      : [[1, 'เข้าเส้นชัย'], [2, 'ตอบถูกทุกข้อ (' + course.rows.length + ' ข้อ)'], [3, L.rival ? 'ถึงเส้นชัยก่อนเบนนี่' : 'ไม่เกิน ' + PS.G.par + ' วินาที']];
    items.forEach(([n, t]) => {
      const li = document.createElement('li');
      const st = document.createElement('span');
      st.className = 'gstars';
      st.innerHTML = n ? MSTAR.repeat(n) : FLAKE;
      const tx = document.createElement('span');
      tx.textContent = t;
      li.append(st, tx);
      goals.appendChild(li);
    });
    show('intro');
    lastHearts = -1;
    updateHud(true);
  }

  // One song per block of 5 levels (1–5, 6–10, 11–15, then around again); endless keeps the main theme.
  const LEVEL_SONGS = ['battle', 'rival', 'snow'];
  const songFor = (L) => (L.endless ? 'battle' : LEVEL_SONGS[Math.floor((L.id - 1) / 5) % LEVEL_SONGS.length]);
  function startRun() {
    const g = PS.G;
    if (!g || g.started) return;
    A.unlock();
    g.started = true;
    mode = 'play';
    show('play');
    A.play(songFor(g.L), g.L.endless ? 1.05 : 1);
    toast('ไปเลย!', '#FFE066');
  }

  function goTitle() {
    PS.G = null;
    mode = 'title';
    A.stopMusic();
    hideQuestion();
    show('title');
    buildGrid();
  }

  function openMap() {
    PS.G = null;
    mode = 'map';
    A.stopMusic();
    hideQuestion();
    show('map');
    buildGrid();
    const cur = document.querySelector('.mnode.cur') || document.querySelector('.mnode');
    const sc = $('mapScroll');
    requestAnimationFrame(() => { sc.scrollTop = Math.max(0, cur.offsetTop - sc.clientHeight * 0.55); });
    drawMapPip();
  }
  // Pip waves above the next level to play while the map is open.
  function drawMapPip() {
    if (mode !== 'map') return;
    const t = performance.now() / 1000;
    if (PS.mapPip3d && PS.mapPip3d($('mapPip'), t)) { requestAnimationFrame(drawMapPip); return; }
    const x = $('mapPip').getContext('2d');
    x.setTransform(3, 0, 0, 3, 0, 0);
    x.clearRect(0, 0, 60, 74);
    const hop = Math.max(0, Math.sin(t * 3)) * 2;
    PSArt.skier(x, 30, 47 - hop, 0.24, { pal: PSArt.PAL.pip, t, expr: 'happy', arms: 'wave', blink: t % 3.2 < 0.12 ? 1 : 0, pom: Math.sin(t * 3) * 2 });
    requestAnimationFrame(drawMapPip);
  }

  function togglePause() {
    if (mode === 'play') { mode = 'pause'; show('pause'); if (A.skiStop) A.skiStop(); A.suspend(); }
    else if (mode === 'pause') { mode = 'play'; show('play'); A.resume(); }
  }

  function showResult(win) {
    const g = PS.G;
    const L = g.L;
    mode = 'result';
    hideQuestion();
    const rowsEl = $('resRows');
    rowsEl.textContent = '';
    const addRow = (k, v, cls) => {
      const li = document.createElement('li');
      const a = document.createElement('span'); a.textContent = k;
      const b = document.createElement('b'); b.textContent = v;
      if (cls) b.className = cls;
      li.append(a, b);
      li.style.animationDelay = (0.25 + rowsEl.children.length * 0.08) + 's';
      rowsEl.appendChild(li);
    };
    let total = 0;
    let stars = 0;
    const failed = !win && !L.endless;
    $('resPanel').classList.toggle('fail', failed);
    $('resStars').hidden = !!L.endless || !win;
    $('resFail').hidden = !failed;
    rowsEl.hidden = failed;
    $('resTotalRow').hidden = failed;
    if (L.endless) {
      const dist = Math.floor(g.z / 10);
      total = dist + g.coins * 10 + g.correct * 300;
      const isBest = dist > (progress.endlessBest || 0);
      if (isBest) { progress.endlessBest = dist; save('ps-progress', progress); }
      setG($('resTitle'), isBest ? 'สถิติใหม่!' : 'ไปได้ไกลมาก!');
      addRow('ระยะทาง ' + dist.toLocaleString() + ' ม.', '+' + dist.toLocaleString());
      addRow('เกล็ดหิมะ ' + g.coins, '+' + (g.coins * 10).toLocaleString());
      addRow('ตอบถูก ' + g.correct + ' / ' + g.answered, '+' + (g.correct * 300).toLocaleString());
    } else {
      const timeBonus = Math.max(0, Math.round(g.par - g.time));
      const allRight = g.correct === g.graded;
      const perfect = win && allRight && g.tumbles === 0;
      const s3 = L.rival ? !g.rivalFirst : g.time <= g.par;
      stars = win ? 1 + (allRight ? 1 : 0) + (s3 ? 1 : 0) : 0;
      total = win ? Math.max(0, 1000 + g.correct * 300 + g.coins * 10 + timeBonus * 20 + (perfect ? 500 : 0) - g.tumbles * 100) : 0;
      setG($('resTitle'), win ? 'ผ่านด่าน!' : 'ล้มแล้วลุกได้!');
      if (win) {
        addRow('เข้าเส้นชัย ' + fmt(g.time), '+1,000');
        addRow('ตอบถูก ' + g.correct + ' / ' + g.graded, '+' + (g.correct * 300).toLocaleString());
        addRow('เกล็ดหิมะ ' + g.coins, '+' + (g.coins * 10).toLocaleString());
        addRow('โบนัสเวลา (par ' + g.par + ' วิ)', '+' + (timeBonus * 20).toLocaleString());
        if (perfect) addRow('Perfect! ไม่ล้มเลย ตอบถูกหมด', '+500');
        if (g.tumbles) addRow('ล้ม ' + g.tumbles + ' ครั้ง', '−' + g.tumbles * 100, 'neg');
        if (L.rival) addRow(g.rivalFirst ? 'เบนนี่ถึงก่อน' : 'ชนะเบนนี่!', s3 ? '★' : '–');
      } else {
        const pct = Math.round(clamp(g.z / g.course.len, 0, 1) * 100);
        setG($('failPct'), pct + '%');
        $('failFill').style.width = '0';
        $('failMe').style.left = '0';
        setTimeout(() => { $('failFill').style.width = pct + '%'; $('failMe').style.left = pct + '%'; }, 450);
        const FAIL_TIP = {
          rock: 'ก้อนหินกับขอนไม้ แตะสั้นๆ เพื่อกระโดดข้าม · เจอสองอันติดกัน แตะซ้ำกลางอากาศเพื่อตีลังกา',
          log: 'ก้อนหินกับขอนไม้ แตะสั้นๆ เพื่อกระโดดข้าม · เจอสองอันติดกัน แตะซ้ำกลางอากาศเพื่อตีลังกา',
          puff: 'ปุยหิมะเดินไปมา ดูจังหวะแล้วกระโดดข้าม',
          ball: 'ก้อนหิมะกลิ้งมาจากข้างทาง ดูทิศแล้วเลี้ยวหลบ',
          hole: 'เจอหลุมน้ำแข็ง แตะกระโดดข้าม หรือเลี้ยวอ้อม · ขอนไม้+หลุมติดกัน แตะซ้ำกลางอากาศเพื่อตีลังกา',
          bear: 'กองหิมะสั่นๆ = หมีกำลังจะโผล่ เลี้ยวหลบไว้ก่อนเลย',
        };
        $('failTip').textContent = FAIL_TIP[g.lastHit] || 'เกล็ดหิมะเรียงแถว = ทางปลอดภัย ลองไถลตามไป';
      }
      progress.stars[L.id] = Math.max(progress.stars[L.id] || 0, stars);
      progress.best[L.id] = Math.max(progress.best[L.id] || 0, total);
      save('ps-progress', progress);
    }
    document.querySelectorAll('#resStars .sw').forEach((el, i) => {
      el.classList.remove('on');
      if (i < stars) setTimeout(() => { el.classList.add('on'); A.sfx('star'); }, 550 + i * 420);
    });
    countUp($('resTotal'), total, reduceMotion ? 0 : 650 + stars * 420);
    const next = DATA.LEVELS.find((x) => x.id === L.id + 1);
    const hasNext = !!(win && next && !L.endless);
    $('btnNext').hidden = !hasNext;
    // With a next level, retry shrinks to an icon; otherwise it becomes the main button.
    $('btnRetry').className = hasNext ? 'ice-btn' : 'big-btn outline';
    show('result');
  }

  let countTimer = 0;
  let countRaf = 0;
  function countUp(el, total, delay) {
    clearTimeout(countTimer);
    cancelAnimationFrame(countRaf);
    setG(el, '0');
    if (!total) return;
    if (reduceMotion) { setG(el, total.toLocaleString()); return; }
    countTimer = setTimeout(() => {
      const t0 = performance.now();
      let lastTick = 0;
      const step = (now) => {
        const k = clamp((now - t0) / 900, 0, 1);
        setG(el, Math.round(total * (1 - Math.pow(1 - k, 3))).toLocaleString());
        if (now - lastTick > 90 && k < 1) { lastTick = now; A.sfx('tick'); }
        if (k < 1) countRaf = requestAnimationFrame(step);
        else A.sfx('pop');
      };
      countRaf = requestAnimationFrame(step);
    }, delay);
  }

  // ---------- Actions ----------
  function jump() {
    const g = PS.G;
    if (!g || !g.started || g.tumble > 0 || g.finished) return;
    if (g.air) {
      // Second tap in mid-air: a somersault that relaunches from the current height, so the
      // skier stays up long enough to clear two obstacles in a row. Once per jump.
      if (g.flip || g.airT < 0.06) return;
      g.airV = PS.JUMP_V;
      g.flip = { t0: g.airT, dur: (g.airV + Math.sqrt(g.airV * g.airV + 2 * PS.GRAV * g.airH)) / PS.GRAV };
      A.sfx('flip');
      toast('ตีลังกา!', '#FFE066');
      burst(g.x, g.z, 8, ['#FFE066', '#FFFFFF'], { kind: 'spark', h: g.airH + 30, up: 60, spread: 140, life: 0.5, size: 3 });
      return;
    }
    g.air = true;
    g.airT = 0;
    g.airH = 0;
    g.airV = PS.JUMP_V;
    g.flip = null;
    A.sfx('jump');
    burst(g.x, g.z, 6, '#FFFFFF', { up: 40, life: 0.4 });
  }
  function land() {
    const g = PS.G;
    g.air = false;
    g.airH = 0;
    g.flip = null;
    g.flipA = 0;
    g.squashT = 0.18;
    A.sfx('land');
    burst(g.x, g.z, 8, '#FFFFFF', { up: 50, spread: 120, life: 0.45 });
  }
  function hit(o) {
    const g = PS.G;
    if (g.invuln > 0 || g.tumble > 0 || g.finished) return;
    if (o.type === 'ball') o.gone = true;
    if (g.shield) {
      g.shield = false;
      g.invuln = 1.0;
      A.sfx('shield');
      burst(g.x, g.z, 14, ['#BFE6FF', '#FFFFFF'], { spread: 260, life: 0.7 });
      toast('ฟองกันชนช่วยไว้!', '#8FD3FF');
      return;
    }
    g.hearts -= 1; g.tumbles += 1; g.tumble = PS.TUMBLE; g.invuln = PS.TUMBLE + 1.3; g.combo = 0; g.shake = 1; g.air = false; g.airH = 0; g.flip = null; g.flipA = 0;
    g.lastHit = o.type;
    if (o.type === 'puff') o.hopT = 0.6;
    A.sfx('crash');
    burst(g.x, g.z, 18, ['#FFFFFF', '#DCE7FF'], { spread: 300, up: 140, life: 0.8, size: 6 });
    const ouch = o.type === 'hole' ? 'ตกหลุม!' : o.type === 'bear' ? 'จ๊ะเอ๋! โดนหมี' : 'โอ๊ะ! ชนแล้ว';
    toast(g.hearts > 0 ? ouch : 'หัวใจหมดแล้ว!', '#FF8DB3');
    updateHud(true);
  }
  function collectItem(o) {
    const g = PS.G;
    o.gone = true;
    g.powerT = 0.9;
    if (o.kind === 'magnet') { g.magnet = 8; toast('แม่เหล็ก! ดูดเกล็ดหิมะ', '#FF8DB3'); }
    else if (o.kind === 'shield') { g.shield = true; toast('ฟองกันชน! กันล้ม 1 ครั้ง', '#8FD3FF'); }
    else if (o.kind === 'crystal') { g.power = PS.POWER; toast('ผลึกพลัง! ชนอะไรก็พัง', '#8FF3FF'); }
    else { g.slowmo = 4; toast('นาฬิกาทราย! โลกช้าลง', '#FFE066'); }
    A.sfx('power');
    burst(g.x, g.z, 10, o.kind === 'crystal' ? ['#8FF3FF', '#FFFFFF'] : ['#FFC93C', '#FFFFFF'], { kind: 'spark', spread: 200, up: 120, life: 0.7, size: 5 });
  }
  // Power crystal: anything Pip runs into while glowing shatters instead of knocking Pip down.
  const SMASH_COL = { tree: ['#2FA36B', '#FFFFFF'], rock: ['#9AA3B8', '#FFFFFF'], log: ['#A66A40', '#D9A877'], snowman: ['#FFFFFF', '#E5484D'], bear: ['#8B5A3C', '#FFFFFF'] };
  function smash(o) {
    const g = PS.G;
    o.gone = true;
    g.shake = Math.max(g.shake, 0.35);
    A.sfx('smash');
    burst(o.x, o.z, 14, SMASH_COL[o.type] || ['#FFFFFF', '#DCE7FF'], { spread: 320, up: 170, life: 0.8, size: 6 });
    burst(o.x, o.z, 8, ['#8FF3FF', '#FFE98A'], { kind: 'spark', spread: 260, up: 150, life: 0.6, size: 4 });
    if (PS.clock - g.lastSmash > 1.2) { toast(o.type === 'bear' ? 'หมีกระเด็น!' : 'ตูม! พังเลย', '#8FF3FF'); g.lastSmash = PS.clock; }
  }
  function revealRow(row) {
    row.reveal = true;
    if (PS.G.row === row) showQuestion(row);
    A.sfx('star');
  }
  function resolveRow(row) {
    const g = PS.G;
    // Crashing through a row while powered counts as the right answer and shatters the gates.
    const powered = g.power > 0;
    const idx = powered ? row.correct : clamp(Math.floor((g.x + 195) / (390 / row.n)), 0, row.n - 1);
    row.picked = idx;
    row.done = true;
    g.answered += 1;
    g.row = null;
    hideQuestion();
    A.duck(false);
    if (powered) {
      row.smashed = true;
      A.sfx('smash');
      g.shake = Math.max(g.shake, 0.5);
      row.q.options.forEach((op, i) => burst(-195 + (390 / row.n) * (i + 0.5), row.z, 12, [op.color || '#3FA9F5', '#FFFFFF'], { spread: 240, up: 210, life: 0.9, size: 7 }));
    }
    if (idx === row.correct) {
      g.correct += 1; g.combo += 1; g.happyT = 0.7;
      A.sfx('right');
      burst(g.x, g.z + 10, 16, ['#FF5C93', '#FFC93C', '#7DE0A8', '#3FA9F5'], { kind: 'confetti', spread: 260, up: 160, life: 1.0 });
      if (powered) toast('ทะลุประตู! ถูกต้อง +300', '#8FF3FF');
      else toast(g.combo >= 2 ? 'ถูกต้อง! คอมโบ x' + g.combo : 'ถูกต้อง! +300', '#7DE0A8');
    } else {
      g.combo = 0; g.slow = 1.0; g.sadT = 0.9;
      A.sfx('wrong');
      burst(g.x, g.z, 12, '#FFFFFF', { spread: 200, up: 90, size: 7 });
      toast('อุ๊ย! ไม่ใช่ ช้าลงหน่อย', '#FFE066');
    }
  }

  // ---------- Simulation ----------
  const keys = { left: false, right: false };
  function step(dt) {
    const g = PS.G;
    if (!g || !g.started) return;
    if (g.slowmo > 0) g.slowmo -= dt;
    const gdt = dt * (g.slowmo > 0 ? 0.6 : 1);
    g.time += g.finished ? 0 : gdt;

    if (keys.left) g.tx -= 420 * gdt;
    if (keys.right) g.tx += 420 * gdt;
    g.tx = clamp(g.tx, -160, 160);
    const prev = g.x;
    if (g.tumble <= 0) g.x += (g.tx - g.x) * Math.min(1, (g.icy > 0 ? 2.5 : 12) * gdt);
    g.x = clamp(g.x, -160, 160);
    g.vx = (g.x - prev) / Math.max(gdt, 1e-4);
    g.lean += (clamp(g.vx / 380, -1, 1) - g.lean) * Math.min(1, 10 * dt);

    const L = g.L;
    let v = L.endless ? Math.min(L.v1, L.v0 + g.z * 0.0045) : L.v0 + (L.v1 - L.v0) * clamp(g.z / g.course.len, 0, 1);
    v *= g.mul;
    if (g.power > 0) v *= PS.POWER_SPEED;
    if (g.tumble > 0) v *= 0.3 + 0.7 * (1 - g.tumble / PS.TUMBLE);
    if (g.slow > 0) v *= 0.55;
    if (g.finished) v *= Math.max(0, 1 - g.finishT * 1.2);
    g.v = v;
    g.z += v * gdt;

    if (g.air) {
      g.airT += gdt;
      g.airV -= PS.GRAV * gdt;
      g.airH += g.airV * gdt;
      g.flipA = g.flip ? Math.min(1, (g.airT - g.flip.t0) / g.flip.dur) * Math.PI * 2 : 0;
      if (g.airH <= 0 && g.airV < 0) land();
    }
    g.tumble = Math.max(0, g.tumble - gdt);
    g.invuln -= gdt; g.slow -= gdt; g.magnet -= gdt; g.icy -= gdt;
    if (g.power > 0) {
      g.power -= gdt;
      if (g.power <= 0) toast('พลังหมดแล้ว', '#BFE6FF');
      else if (Math.random() < 0.6) burst(g.x, g.z, 1, ['#8FF3FF', '#FFE98A', '#FFFFFF'], { kind: 'spark', up: 50, spread: 70, vz: -30, life: 0.5, size: 3 });
    }
    g.happyT -= dt; g.sadT -= dt; g.squashT -= dt; g.scaredT -= dt; g.powerT -= dt; g.lookT -= dt;
    g.shake = Math.max(0, g.shake - dt * 3);

    // Gentle S-carve while going straight; fades out when the player steers.
    const onSnow = !g.air && g.tumble <= 0 && !g.finished;
    g.sway = onSnow ? Math.sin(PS.clock * 2.6) * (1 - Math.min(1, Math.abs(g.lean) * 2)) : g.sway * 0.9;
    g.trailT -= dt;
    if (g.trailT <= 0) {
      g.trailT = 0.03;
      g.trail.push({ x: g.x + g.sway * 8, z: g.z, air: g.air || g.tumble > 0 });
      while (g.trail.length && g.trail[0].z < g.z - 420) g.trail.shift();
    }
    if (onSnow && Math.random() < 0.3 + Math.min(0.6, Math.abs(g.vx) / 400)) {
      const side = Math.sign(g.lean + g.sway * 0.5) || 1;
      burst(g.x + g.sway * 8 - side * 20, g.z + 18, 1, '#FFFFFF', { up: 40 + g.v * 0.12, spread: 90, vz: -40, life: 0.38, size: 2.5 + Math.abs(g.lean) * 3 });
    }

    const airH = g.air ? g.airH : 0;
    const objs = g.course.objs;
    while (g.oi < objs.length && objs[g.oi].z < g.z - 320) g.oi++;
    for (let i = g.oi; i < objs.length; i++) {
      const o = objs[i];
      const dz = o.z - g.z;
      if (dz > 900) break;
      if (o.gone) continue;
      if (o.type === 'puff') {
        const nx = o.x0 + Math.sin(PS.clock * o.w + o.ph) * o.amp;
        o.dir = nx > o.x ? 1 : -1;
        o.x = nx;
        if (o.hopT > 0) o.hopT -= dt;
      } else if (o.type === 'bear') {
        // Mound shakes as a warning, then the bear pops up and stays in the way.
        if (dz < 440) o.warn = true;
        if (dz < 300) {
          if (o.up === 0) A.sfx('pop');
          o.up = Math.min(1, o.up + dt * 5);
        }
      } else if (o.type === 'ball') {
        if (!o.active && dz < 560) o.active = true;
        if (o.active) { o.x += o.vx * gdt; o.z += o.vz * gdt; o.rot += o.vx * gdt / o.rad; if (Math.abs(o.x) > 240) o.gone = true; }
      } else if (o.type === 'coin' && g.magnet > 0 && Math.abs(dz) < 170 && Math.abs(o.x - g.x) < 170) {
        o.x += (g.x - o.x) * Math.min(1, 9 * dt);
        o.z += (g.z + 4 - o.z) * Math.min(1, 9 * dt);
      }
      const hazard = o.type === 'puff' || o.type === 'ball' || o.type === 'log' || o.type === 'rock' || o.type === 'tree' || o.type === 'snowman' || o.type === 'hole' || (o.type === 'bear' && o.up > 0.5);
      if (hazard && (o.type === 'puff' || o.type === 'ball' || o.type === 'bear' || o.type === 'hole') && o.z - g.z > 70 && o.z - g.z < 220 && Math.abs(o.x - g.x) < 70) {
        g.lookX = Math.sign(o.x - g.x) || 0;
        g.lookT = 0.15;
      }
      const adz = Math.abs(o.z - g.z);
      if (adz > 40) continue;
      const dx = Math.abs(o.x - g.x);
      if (g.power > 0 && hazard && o.type !== 'hole' && adz < 16 && dx < (o.hw || 20) + 14) { smash(o); continue; }
      if (hazard && !o.passed && adz < 10 && g.tumble <= 0 && g.invuln <= 0) {
        if (o.jump && airH >= 12 && dx < o.hw + 10) {
          o.passed = true;
          g.happyT = 0.5;
          if (PS.clock - g.lastHop > 2) { toast('ข้ามสวย!', '#7DE0A8'); g.lastHop = PS.clock; }
        } else if (dx >= o.hw + 10 && dx < o.hw + 34 && (o.type !== 'tree' || Math.abs(o.x) < 170)) {
          o.passed = true;
          g.scaredT = 0.45;
          g.lookX = Math.sign(o.x - g.x) || 0;
          g.lookT = 0.4;
          if (PS.clock - g.lastNear > 3) { toast('เฉียดฉิว!', '#FFE066'); g.lastNear = PS.clock; }
        }
      }
      switch (o.type) {
        case 'coin':
          if (adz < 16 && dx < 24) {
            o.gone = true; g.coins += 1;
            coinPopup();
            if (PS.clock - lastCoinSfx > 0.05) { A.sfx('coin'); lastCoinSfx = PS.clock; }
            burst(o.x, o.z, 3, '#FFE066', { kind: 'spark', up: 90, spread: 60, life: 0.4, size: 3 });
          }
          break;
        case 'item': if (adz < 18 && dx < 28) collectItem(o); break;
        case 'ice': if (adz < o.h / 2 && dx < o.w / 2) { if (g.icy <= 0) toast('ลื่น!', '#BFE6FF'); g.icy = 0.9; } break;
        case 'tree': case 'snowman': if (adz < 10 && dx < o.hw + 10) hit(o); break;
        case 'rock': case 'log': if (adz < 10 && dx < o.hw + 10 && airH < 12) hit(o); break;
        case 'puff': if (adz < 12 && dx < o.hw + 10 && airH < 14 && !(o.hopT > 0)) hit(o); break;
        case 'ball': if (o.active && adz < 16 && dx < o.hw + 8) hit(o); break;
        case 'bear': if (o.up > 0.5 && adz < 12 && dx < o.hw + 10) hit(o); break;
        case 'hole': if (adz < o.h / 2 - 2 && dx < o.hw - 4 && airH < 8 && g.power <= 0) hit(o); break; // glowing Pip skims over
        default: break;
      }
    }

    for (const row of g.course.rows) {
      if (row.done) continue;
      const dz = row.z - g.z;
      if (dz > g.v * g.readTime + 40) break;
      if (!row.shown) { row.shown = true; g.row = row; showQuestion(row); A.duck(true); A.sfx('tick'); }
      if (g.power > 0 && !row.reveal) revealRow(row);
      if (dz <= 0) resolveRow(row);
    }

    if (g.rival) {
      const r = g.rival;
      if (!g.finished) r.z += r.speed * gdt;
      const tx = g.course.safeX(r.z + 60);
      const nx = r.x + (tx - r.x) * Math.min(1, 3 * dt);
      r.lean = clamp((nx - r.x) / Math.max(dt, 1e-4) / 300, -1, 1);
      r.x = nx;
      if (!r.passed && g.z > r.z + 20) { r.passed = true; toast('แซงเบนนี่แล้ว!', '#FFE066'); }
      else if (r.passed && r.z > g.z + 40) { r.passed = false; toast('เบนนี่แซงกลับ!', '#FF8DB3'); }
      if (r.z >= g.course.len && !g.finished) g.rivalFirst = true;
    }

    if (!L.endless && !g.finished && g.z >= g.course.len) {
      g.finished = true;
      g.finishT = 0;
      A.play('clear');
      toast('เส้นชัย!', '#FFE066');
      burst(g.x, g.z, 30, ['#FF5C93', '#FFC93C', '#7DE0A8', '#3FA9F5'], { kind: 'confetti', spread: 360, up: 220, life: 1.4 });
    }
    if (g.finished) { g.finishT += dt; if (g.finishT > 1.6) showResult(true); }
    if (g.hearts <= 0 && g.tumble <= 0 && !g.failed) { g.failed = true; g.failT = 0; A.play('fail'); }
    if (g.failed) { g.failT += dt; if (g.failT > 0.7) showResult(false); }
  }

  // ---------- Input: hold anywhere and slide to steer; quick tap or flick up to jump ----------
  const touch = { id: null, sx: 0, sy: 0, ay: 0, st: 0, x0: 0, moved: 0, noTap: false };
  const pad = $('pad');
  const padDot = $('padDot');
  function framePos(e) { const r = frameEl.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; }
  canvas.addEventListener('pointerdown', (e) => {
    if (mode !== 'intro' && mode !== 'play') return;
    const startedNow = mode === 'intro';
    if (startedNow) startRun();
    const g = PS.G;
    touch.id = e.pointerId; touch.sx = e.clientX; touch.sy = e.clientY; touch.ay = e.clientY;
    touch.st = performance.now(); touch.x0 = g.tx; touch.moved = 0; touch.noTap = startedNow;
    try { canvas.setPointerCapture(e.pointerId); } catch (err) { /* capture unsupported */ }
    const p = framePos(e);
    pad.style.left = p.x + 'px'; pad.style.top = p.y + 'px';
    padDot.style.transform = 'translate(-50%, -50%)';
    pad.hidden = false;
    e.preventDefault();
  });
  canvas.addEventListener('pointermove', (e) => {
    if (e.pointerId !== touch.id || !PS.G || mode !== 'play') return;
    const dxPx = e.clientX - touch.sx;
    PS.G.tx = clamp(touch.x0 + (dxPx / PS.scale) * 1.25, -160, 160);
    touch.moved = Math.max(touch.moved, Math.hypot(dxPx, e.clientY - touch.sy));
    if (e.clientY > touch.ay) touch.ay = e.clientY;
    if (touch.ay - e.clientY > 44) { jump(); touch.ay = e.clientY; touch.noTap = true; }
    padDot.style.transform = 'translate(calc(-50% + ' + clamp(dxPx, -56, 56).toFixed(0) + 'px), -50%)';
  });
  function endTouch(e) {
    if (e.pointerId !== touch.id) return;
    if (!touch.noTap && performance.now() - touch.st < 220 && touch.moved < 12) jump();
    touch.id = null;
    pad.hidden = true;
  }
  canvas.addEventListener('pointerup', endTouch);
  canvas.addEventListener('pointercancel', endTouch);

  window.addEventListener('keydown', (e) => {
    const k = e.key;
    if (k === 'ArrowLeft' || k === 'a' || k === 'A') keys.left = true;
    if (k === 'ArrowRight' || k === 'd' || k === 'D') keys.right = true;
    if (k === ' ' || k === 'ArrowUp' || k === 'w' || k === 'W') {
      if (mode === 'intro') startRun(); else if (!e.repeat) jump();
      if (mode === 'play' || mode === 'intro') e.preventDefault();
    }
    if (mode === 'intro' && (keys.left || keys.right)) startRun();
    if (k === 'Escape' || k === 'p' || k === 'P') togglePause();
  });
  window.addEventListener('keyup', (e) => {
    const k = e.key;
    if (k === 'ArrowLeft' || k === 'a' || k === 'A') keys.left = false;
    if (k === 'ArrowRight' || k === 'd' || k === 'D') keys.right = false;
  });
  document.addEventListener('visibilitychange', () => { if (document.hidden && mode === 'play') togglePause(); });

  // ---------- Buttons ----------
  $('btnPlay').addEventListener('click', () => { A.unlock(); A.sfx('pop'); backTo = 'title'; openLevel(nextPlayable()); });
  $('btnEndless').addEventListener('click', () => { A.unlock(); A.sfx('pop'); backTo = 'title'; openLevel(DATA.ENDLESS); });
  $('btnMap').addEventListener('click', () => { A.unlock(); A.sfx('pop'); openMap(); });
  $('btnMapBack').addEventListener('click', () => { A.sfx('pop'); goTitle(); });
  $('btnPause').addEventListener('click', () => { A.sfx('pop'); togglePause(); });
  $('btnResume').addEventListener('click', () => togglePause());
  $('btnRestart').addEventListener('click', () => { A.resume(); openLevel(PS.G.L); });
  $('btnHome').addEventListener('click', () => { A.resume(); goTitle(); });
  $('btnIntroBack').addEventListener('click', () => (backTo === 'map' ? openMap() : goTitle()));
  $('btnRetry').addEventListener('click', () => { A.sfx('pop'); openLevel(PS.G.L); });
  $('btnResHome').addEventListener('click', () => { A.sfx('pop'); openMap(); });
  $('btnNext').addEventListener('click', () => {
    A.sfx('pop');
    const next = DATA.LEVELS.find((x) => x.id === PS.G.L.id + 1);
    if (next) openLevel(next);
  });
  document.querySelectorAll('[data-speed]').forEach((b) => b.addEventListener('click', () => {
    settings.speed = Number(b.dataset.speed);
    save('ps-settings', settings);
    A.sfx('pop');
    buildGrid();
  }));
  $('btnMusic').addEventListener('click', () => {
    settings.music = !settings.music; A.musicOn = settings.music;
    if (!settings.music) A.stopMusic();
    save('ps-settings', settings); buildGrid();
  });
  $('btnSfx').addEventListener('click', () => {
    settings.sfx = !settings.sfx; A.sfxOn = settings.sfx;
    save('ps-settings', settings); A.sfx('pop'); buildGrid();
  });
  // High: bloom, MSAA, 2048 shadows, full pixel density. Normal: lighter for older phones.
  $('btnGfx').addEventListener('click', () => {
    settings.gfx = PS.gfx === 'high' ? 'normal' : 'high'; PS.gfx = settings.gfx;
    save('ps-settings', settings); A.sfx('pop'); buildGrid();
  });

  // ---------- Loop ----------
  let last = performance.now();
  let skiOn = false;
  function frame(now) {
    const dt = Math.min(0.05, Math.max(0, (now - last) / 1000));
    last = now;
    if (mode !== 'pause') {
      PS.clock += dt;
      updateFlakes(dt);
      if (mode === 'play') step(dt);
      updateParticles(dt);
    }
    // Continuous ski-on-snow and wind layers follow speed and carve while a run is live.
    if (A.ski) {
      const g = PS.G;
      if (mode === 'play' && g && g.started && !g.finished) {
        const lo = g.L.v0 * g.mul * 0.6;
        A.ski(clamp((g.v - lo) / Math.max(1, g.L.v1 * g.mul - lo), 0, 1), Math.min(1, Math.abs(g.lean || 0)), !g.air && g.tumble <= 0);
        skiOn = true;
      } else if (skiOn) { A.skiStop(); skiOn = false; }
    }
    (PS.render3d || PS.render)();
    if (PS.G && mode !== 'title') updateHud(false);
    requestAnimationFrame(frame);
  }
  // ?debug exposes the simulation so tests can step it without animation frames.
  if (/[?&]debug\b/.test(location.search)) PS.debug = { step, jump, startRun, openLevel };
  show('title');
  buildGrid();
  requestAnimationFrame(frame);
})();
