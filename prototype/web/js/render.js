// Pippy The Ski Penguin prototype — draws the world each frame from the shared PS state.
// Camera: low and in front of the skier, looking back up the slope, so the skier faces the
// player near the top third and the course scrolls up toward them.
(function () {
  const ART = window.PSArt;
  const PS = window.PS || (window.PS = {});
  const SKY_H = 150;

  function persp(y, H) { return 0.82 + 0.3 * (y / H); }

  PS.render = function () {
    const ctx = PS.ctx;
    const W = PS.LW;
    const H = PS.LH;
    const g = PS.G;
    const t = PS.clock;
    const world = ART.WORLD[(g && g.L.world) || 'meadow'];
    ctx.setTransform(PS.dpr * PS.scale, 0, 0, PS.dpr * PS.scale, 0, 0);
    ctx.clearRect(0, 0, W, H);
    ctx.save();
    if (g && g.shake > 0) ctx.translate((Math.random() - 0.5) * 8 * g.shake, (Math.random() - 0.5) * 8 * g.shake);

    ART.sky(ctx, W, SKY_H, world, t, g ? g.x : Math.sin(t * 0.3) * 60);
    ctx.fillStyle = world.snow;
    ctx.fillRect(0, SKY_H - 30, W, H);
    ART.crest(ctx, W, SKY_H - 62, world);

    const SY = H * 0.37;
    const pz = g ? g.z : 0;
    const px = g ? g.x : 0;
    const sP = persp(SY, H);
    const toY = (z) => SY + (z - pz);
    const toX = (x, y) => W / 2 + x * (persp(y, H) / sP);
    const fadeTop = (y) => Math.max(0, Math.min(1, (y - (SKY_H - 6)) / 70));
    const course = g ? g.course : PS.titleCourse;

    if (course) {
      for (const d of course.decor) {
        const y = toY(d.z);
        if (y < SKY_H - 40 || y > H + 60) continue;
        ctx.globalAlpha = fadeTop(y);
        const sc = persp(y, H);
        if (d.type === 'shade') ART.E(ctx, toX(d.x, y), y, d.w * sc / 2, d.h * sc / 2, 0, world.shade);
        else ART.mogul(ctx, toX(d.x, y), y, d.s * sc, world);
      }
      ctx.globalAlpha = 1;

      if (g && g.trail.length > 1) {
        ctx.strokeStyle = 'rgba(160,185,225,0.55)';
        ctx.lineWidth = 3;
        ctx.lineCap = 'round';
        for (const off of [-12, 12]) {
          ctx.beginPath();
          let started = false;
          for (const p of g.trail) {
            const y = toY(p.z);
            if (p.air || y < SKY_H + 30) { started = false; continue; }
            const x = toX(p.x, y) + off;
            if (!started) { ctx.moveTo(x, y); started = true; } else ctx.lineTo(x, y);
          }
          ctx.stroke();
        }
      }

      for (const o of course.objs) {
        if ((o.type !== 'ice' && o.type !== 'hole') || o.gone) continue;
        const y = toY(o.z);
        if (y < SKY_H - 40 || y > H + 60) continue;
        ctx.globalAlpha = fadeTop(y);
        const sc = persp(y, H);
        if (o.type === 'hole') ART.hole(ctx, toX(o.x, y), y, o.hw * 2 * sc, o.h * sc, t + o.z);
        else ART.ice(ctx, toX(o.x, y), y, o.w * sc, o.h * sc);
      }
      ctx.globalAlpha = 1;

      const list = [];
      for (const o of course.objs) {
        if (o.type === 'ice' || o.type === 'hole' || o.gone) continue;
        const y = toY(o.z);
        if (y < SKY_H - 60 || y > H + 140) continue;
        list.push({ y, o });
      }
      for (const row of course.rows) {
        const y = toY(row.z);
        if (y < SKY_H - 60 || y > H + 140) continue;
        list.push({ y, row });
      }
      if (!course.endless) {
        const fy = toY(course.len);
        if (fy > SKY_H - 60 && fy < H + 160) list.push({ y: fy, arch: 'FINISH' });
      }
      const sy0 = toY(-40);
      if (g && sy0 > SKY_H - 60) list.push({ y: sy0, arch: 'START' });
      if (g && g.rival) {
        const y = toY(g.rival.z);
        if (y > SKY_H - 60 && y < H + 140) list.push({ y, rival: true });
      }
      list.push({ y: SY + 0.01, player: true });
      list.sort((a, b) => a.y - b.y);

      for (const it of list) {
        const y = it.y;
        const sc = persp(y, H);
        ctx.globalAlpha = it.player ? 1 : fadeTop(y);
        if (it.player) { drawPlayer(ctx, W / 2 + (px), SY, sP, g, t); continue; }
        if (it.arch) { ART.arch(ctx, toX(-150, y), toX(150, y), y, sc, it.arch, it.arch === 'FINISH' ? '#FF5C93' : '#3FA9F5'); continue; }
        if (it.rival) {
          const r = g.rival;
          ART.shadow(ctx, toX(r.x, y), y + 2, 25 * sc, 6 * sc);
          outlined(ctx, toX(r.x, y), y, 130, 190, ctx.globalAlpha, (c) => ART.skier(c, 0, 0, 0.34 * sc, { pal: ART.PAL.benny, t, expr: r.passed ? 'sad' : 'determined', lean: r.lean, carve: r.lean * 0.5, look: r.lean, bob: 3 + Math.sin(t * 5.2 + 1) * 3, pump: Math.sin(t * 7 + 1), blink: blinkAt(t + 1.3), pom: 0 }));
          continue;
        }
        if (it.row) {
          const row = it.row;
          if (row.smashed) continue;
          const xs = [];
          for (let i = 0; i <= row.n; i++) xs.push(toX(-195 + (390 / row.n) * i, y));
          ART.gateRow(ctx, xs, y, sc, row.q.options, { done: row.done, correct: row.correct, picked: row.picked, reveal: row.reveal });
          continue;
        }
        const o = it.o;
        const x = toX(o.x, y);
        switch (o.type) {
          case 'tree': ART.tree(ctx, x, y, o.s * sc, world); break;
          case 'rock': ART.rock(ctx, x, y, o.s * sc, world); break;
          case 'log': ART.log(ctx, x, y, sc, world); break;
          case 'snowman': ART.snowman(ctx, x, y, sc * 1.05); break;
          case 'puff': ART.puff(ctx, x, y, sc * (o.hopT > 0 ? 0.9 : 1), t + o.ph, o.dir || 1); break;
          case 'ball': ART.ball(ctx, x, y, o.rad * sc, o.rot); break;
          case 'bear': ART.bear(ctx, x, y, sc * 0.95, t, o.up || 0, o.warn, o.ph || 0); break;
          case 'coin': ART.coin(ctx, x, y, sc, t, o.ph); break;
          case 'item': ART.item(ctx, x, y, sc, t, o.kind); break;
          case 'sign': ART.sign(ctx, x, y, sc, o.left, o.right); break;
          default: break;
        }
      }
      ctx.globalAlpha = 1;
    }

    for (const p of PS.particles) {
      const y = toY(p.z) - p.h;
      const x = toX(p.x, toY(p.z));
      ctx.globalAlpha = Math.max(0, p.life / p.max);
      if (p.kind === 'confetti') {
        ctx.save(); ctx.translate(x, y); ctx.rotate(p.rot); ctx.fillStyle = p.color; ctx.fillRect(-4, -2, 8, 4); ctx.restore();
      } else if (p.kind === 'spark') {
        ART.shape(ctx, 'star', x, y, p.size, p.color);
      } else {
        ART.C(ctx, x, y, p.size * (1.6 - p.life / p.max * 0.6), p.color);
      }
    }
    ctx.globalAlpha = 1;

    if (g && g.v > 330) {
      ctx.strokeStyle = 'rgba(255,255,255,0.7)';
      ctx.lineWidth = 2;
      const k = Math.min(1, (g.v - 330) / 120);
      for (let i = 0; i < 8; i++) {
        const lx = ((i * 53 + t * 40) % W);
        const ly = (H - ((t * 900 + i * 137) % (H - SKY_H)));
        ctx.globalAlpha = 0.25 + 0.4 * k;
        ctx.beginPath(); ctx.moveTo(lx, ly); ctx.lineTo(lx, ly - 40 - 40 * k); ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    const weather = (g && g.L.weather) || 'snow';
    for (const f of PS.flakes) {
      if (weather === 'sunny' && f.i % 4 !== 0) continue;
      ctx.globalAlpha = f.o;
      ART.C(ctx, f.x, f.y, f.r, '#FFFFFF');
    }
    ctx.globalAlpha = 1;
    if (weather === 'blizzard') {
      const hz = ctx.createLinearGradient(0, 0, 0, H);
      hz.addColorStop(0, 'rgba(255,255,255,0.55)');
      hz.addColorStop(0.5, 'rgba(230,240,255,' + (0.18 + Math.sin(t * 1.7) * 0.06) + ')');
      hz.addColorStop(1, 'rgba(255,255,255,0.5)');
      ctx.fillStyle = hz;
      ctx.fillRect(0, 0, W, H);
    }
    ctx.restore();
  };

  function blinkAt(t) { const k = t % 3.4; return k < 0.12 ? Math.sin(k / 0.12 * Math.PI) : 0; }

  // Draw a character into an offscreen buffer, then stamp a thick ink outline around its
  // silhouette so it pops off the white snow (same sticker look as the UI).
  const buffers = {};
  function outlined(ctx, x, y, w, h, alpha, draw) {
    const k = PS.dpr * PS.scale;
    const key = w + 'x' + h;
    let b = buffers[key];
    if (!b || b.k !== k) {
      b = { c: document.createElement('canvas'), m: document.createElement('canvas'), k };
      b.c.width = b.m.width = Math.ceil(w * k);
      b.c.height = b.m.height = Math.ceil(h * k);
      buffers[key] = b;
    }
    const oc = b.c.getContext('2d');
    const mc = b.m.getContext('2d');
    oc.setTransform(1, 0, 0, 1, 0, 0);
    oc.clearRect(0, 0, b.c.width, b.c.height);
    oc.setTransform(k, 0, 0, k, 0, 0);
    oc.translate(w / 2, h - 70);
    draw(oc);
    mc.setTransform(1, 0, 0, 1, 0, 0);
    mc.globalCompositeOperation = 'source-over';
    mc.clearRect(0, 0, b.m.width, b.m.height);
    mc.drawImage(b.c, 0, 0);
    mc.globalCompositeOperation = 'source-in';
    mc.fillStyle = ART.INK;
    mc.fillRect(0, 0, b.m.width, b.m.height);
    mc.globalCompositeOperation = 'source-over';
    const left = x - w / 2;
    const top = y - (h - 70);
    const o = 2.6;
    const d = o * 0.7;
    ctx.save();
    ctx.globalAlpha = alpha;
    for (const p of [[o, 0], [-o, 0], [0, o], [0, -o], [d, d], [-d, d], [d, -d], [-d, -d]]) ctx.drawImage(b.m, left + p[0], top + p[1], w, h);
    ctx.drawImage(b.c, left, top, w, h);
    ctx.restore();
  }

  function drawPlayer(ctx, x, y, s, g, t) {
    const pal = ART.PAL.pip;
    if (!g) {
      // Idle loop on the title screen: look around, wink, then hop and wave.
      // Sits lower than in play so the two-line logo and tagline stay clear of the hat.
      y += 48;
      const ph = t % 6;
      const waving = ph > 4.4 && ph < 5.8;
      const hop = ph > 4.4 && ph < 4.8 ? Math.sin((ph - 4.4) / 0.4 * Math.PI) * 16 : 0;
      const expr = waving ? 'happy' : ph > 2.4 && ph < 2.8 ? 'wink' : 'normal';
      ART.shadow(ctx, x, y + 16, 33 - hop * 0.5, 11, 0.2);
      outlined(ctx, x, y - hop, 160, 230, 1, (c) => ART.skier(c, 0, 0, 0.46, {
        pal, t, expr, look: waving ? 0 : Math.sin(t * 0.9) * 0.9, blink: blinkAt(t),
        bob: Math.sin(t * 3) * 2.5, squash: hop > 0 ? -0.4 : 0, arms: waving ? 'wave' : 'poles',
        pump: Math.sin(t * 2) * 0.2, pom: Math.sin(t * 3 - 0.6) * 2, headShake: Math.sin(t * 1.3) * 0.05
      }));
      return;
    }
    x += (g.sway || 0) * 8;
    const airH = g.air ? Math.max(0, g.airH) : 0;
    ART.shadow(ctx, x, y + 16, 32 * (1 - airH / 120), 11 * (1 - airH / 120), 0.2);
    if (g.power > 0) {
      // Power crystal glow; flickers in the last 1.5 s as a warning.
      const pw = Math.min(1, g.power / 0.4) * (g.power < 1.5 ? 0.55 + 0.45 * Math.abs(Math.sin(t * 14)) : 1);
      const cy = y - 34 - airH;
      const gr = ctx.createRadialGradient(x, cy, 4, x, cy, 72);
      gr.addColorStop(0, 'rgba(170,247,255,' + (0.85 * pw).toFixed(3) + ')');
      gr.addColorStop(0.5, 'rgba(95,227,255,' + (0.35 * pw).toFixed(3) + ')');
      gr.addColorStop(1, 'rgba(95,227,255,0)');
      ctx.fillStyle = gr;
      ctx.beginPath(); ctx.arc(x, cy, 72, 0, Math.PI * 2); ctx.fill();
    }
    if (g.shield) {
      ctx.save();
      ctx.globalAlpha = 0.35 + Math.sin(t * 6) * 0.1;
      ART.C(ctx, x, y - 32 - airH, 48, '#BFE6FF');
      ctx.globalAlpha = 0.9;
      ctx.lineWidth = 3; ctx.strokeStyle = '#3FA9F5';
      ctx.beginPath(); ctx.arc(x, y - 32 - airH, 48, 0, Math.PI * 2); ctx.stroke();
      ctx.restore();
    }
    if (g.magnet > 0) {
      ctx.save();
      ctx.strokeStyle = 'rgba(255,92,147,0.6)'; ctx.lineWidth = 3; ctx.setLineDash([6, 8]);
      ctx.lineDashOffset = -t * 40;
      ctx.beginPath(); ctx.arc(x, y - 25, 60, 0, Math.PI * 2); ctx.stroke();
      ctx.restore();
    }
    const skiing = g.started && !g.finished && g.tumble <= 0;
    const tumbling = g.tumble > 0;
    let expr = 'normal';
    if (tumbling) expr = 'dizzy';
    else if (g.finished || g.powerT > 0) expr = 'excited';
    else if (g.happyT > 0) expr = 'happy';
    else if (g.sadT > 0) expr = 'sad';
    else if (g.scaredT > 0) expr = 'scared';
    else if (g.air) expr = 'happy';
    else if (g.row || g.icy > 0 || g.v > g.L.v1 * g.mul * 0.95) expr = 'determined';
    const airK = g.air ? Math.min(1, airH / 40) : 0;
    const crouch = Math.max(0, Math.min(1, (g.v - 250) / 150)) * 4;
    const st = {
      pal, t, expr,
      lean: tumbling ? 0 : g.lean + (g.sway || 0) * 0.35,
      carve: tumbling ? 0 : g.lean * 0.55 + (g.sway || 0) * 0.22,
      look: g.lookT > 0 ? g.lookX : g.lean * 0.8,
      mouth: g.air ? 0.6 + airK * 0.4 : undefined,
      bob: skiing && !g.air ? 3 + Math.sin(t * 5.2) * 3 + crouch : Math.sin(t * 3) * 2,
      headBob: skiing && !g.air ? Math.sin(t * 5.2 - 0.8) * 1.5 : 0,
      squash: g.squashT > 0 ? g.squashT / 0.18 : g.air && g.airT < 0.1 ? -0.7 : skiing ? 0.06 + Math.sin(t * 5.2) * 0.05 + crouch * 0.02 : 0,
      tuck: airK,
      arms: g.finished ? 'wave' : g.air || g.happyT > 0 || g.powerT > 0 ? 'up' : 'poles',
      pump: skiing && !g.air ? Math.sin(t * 7) : 0,
      blink: blinkAt(t),
      spin: tumbling ? (1 - g.tumble / PS.TUMBLE) * Math.PI * 2 : g.flipA || 0,
      headShake: g.sadT > 0 ? Math.sin(t * 30) * 0.12 : 0,
      pom: Math.sin(t * 14 - 0.8) * (skiing ? 3.5 : 1.5),
      alpha: g.invuln > 0 && !tumbling && Math.floor(t * 12) % 2 === 0 ? 0.45 : 1
    };
    const fy = g.finished ? -Math.abs(Math.sin(t * 7)) * 18 : 0;
    const a = st.alpha;
    st.alpha = 1;
    outlined(ctx, x, y - airH + fy, 160, 230, a, (c) => ART.skier(c, 0, 0, s * 0.5, st));
    PS.pipScreen = { x: x * PS.scale, y: (y - airH + fy - 78 * s) * PS.scale };
    if (tumbling) {
      for (let i = 0; i < 3; i++) {
        const a = t * 5 + i * 2.1;
        ART.shape(ctx, 'star', x + Math.cos(a) * 24, y - 84 + Math.sin(a) * 7, 6, '#FFC93C');
      }
    }
  }
})();
