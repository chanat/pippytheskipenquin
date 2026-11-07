// Pippy The Ski Penguin prototype — procedural "cinematic" audio, pure Web Audio (no samples, no libs).
// Music re-voices the original note data below: brass lead doubled by horns, staccato string
// ostinato (or FM harp), string pad + choir built from the chords, sub+saw bass and an orchestral
// kit (kick, snare, hats, timpani on downbeats, cymbal swells into sections). Bars are scheduled
// ~1.5 s ahead instead of whole loops. Chain: music + sfx buses -> generated stereo reverb send ->
// compressor -> soft clip -> out. A.ski() drives a looping carve-hiss + wind layer (param ramps
// only). All melodies are original compositions.
(function () {
  const A = { musicOn: true, sfxOn: true };
  const MASTER = 0.17; // level into the compressor
  const LOOK = 1.5; // music look-ahead (s), scheduled in whole bars
  let ac = null;
  let mix, musicBus, sfxBus, sfxKit, noiseBuf, waves;
  let song = null;
  let ski = null;
  const COMPILED = {};

  function init() {
    if (ac) {
      if (ac.state === 'suspended') wake();
      return true;
    }
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return false;
    ac = new AC();
    waves = {
      brass: harm(32, (k) => (k < 6 ? 1.2 : 1) / Math.pow(k, 0.95)),
      flute: harm(4, (k) => [0, 1, 0.3, 0.1, 0.04][k]),
      timp: harm(5, (k) => [0, 0, 1, 0.45, 0.25, 0.12][k]), // played an octave down: modes 1, 1.5, 2, 2.5
    };
    const len = ac.sampleRate * 2;
    noiseBuf = ac.createBuffer(1, len, ac.sampleRate);
    const d = noiseBuf.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
    // mix -> compressor -> soft clip -> out. The reverb send is highpassed so kick/bass stay dry.
    const comp = ac.createDynamicsCompressor();
    comp.threshold.value = -16;
    comp.knee.value = 12;
    comp.ratio.value = 3;
    comp.attack.value = 0.005;
    comp.release.value = 0.25;
    const clip = ac.createWaveShaper();
    clip.curve = softClip();
    mix = gn(MASTER);
    link(mix, comp, clip, ac.destination);
    const verbIn = bq('highpass', 280, 0.5);
    const verb = ac.createConvolver();
    verb.buffer = impulse(2.1);
    link(verbIn, verb, mix);
    musicBus = link(gn(0.8), mix);
    link(musicBus, gn(0.8), verbIn);
    sfxBus = link(gn(2), mix);
    link(sfxBus, gn(0.45), verbIn);
    sfxKit = mkKit(sfxBus);
    if (ac.state === 'suspended') wake();
    return true;
  }

  function wake() {
    const p = ac.resume();
    if (p && p.catch) p.catch(() => { /* needs a user gesture */ });
  }

  // ---- building blocks ----

  function link(...ns) {
    for (let i = 0; i < ns.length - 1; i++) ns[i].connect(ns[i + 1]);
    return ns[0];
  }

  function gn(v) {
    const g = ac.createGain();
    g.gain.value = v;
    return g;
  }

  function bq(type, f, q) {
    const x = ac.createBiquadFilter();
    x.type = type;
    x.frequency.value = f;
    x.Q.value = q;
    return x;
  }

  function osc(type, f, t, det) {
    const o = ac.createOscillator();
    if (waves[type]) o.setPeriodicWave(waves[type]);
    else o.type = type;
    o.frequency.setValueAtTime(f, t);
    if (det) o.detune.setValueAtTime(det, t);
    return o;
  }

  function nz(rate) {
    const s = ac.createBufferSource();
    s.buffer = noiseBuf;
    s.loop = true;
    s.off = Math.random() * 1.9;
    if (rate) s.playbackRate.value = rate;
    return s;
  }

  function harm(n, fn) {
    const re = new Float32Array(n + 1);
    const im = new Float32Array(n + 1);
    for (let k = 1; k <= n; k++) im[k] = fn(k);
    return ac.createPeriodicWave(re, im);
  }

  // Start/stop sources; free the note's chain once the first source ends.
  function run(srcs, t, end, last) {
    srcs.forEach((s) => (s.buffer ? s.start(t, s.off) : s.start(t)));
    srcs.forEach((s) => s.stop(end));
    srcs[0].onended = () => last.disconnect();
  }

  // Attack, decay to sustain, release (time constants); returns when the tail is silent.
  function env(p, t, dur, peak, a, d, sus, r) {
    a = Math.min(a, dur * 0.5);
    p.setValueAtTime(0, t);
    p.linearRampToValueAtTime(peak, t + a);
    p.setTargetAtTime(peak * sus, t + a, d);
    p.setTargetAtTime(0, t + dur, r);
    return t + dur + r * 6;
  }

  // Unity below 0.7, tanh knee above: the output can never reach 1.0.
  function softClip() {
    const n = 2048;
    const c = new Float32Array(n);
    for (let i = 0; i < n; i++) {
      const x = (i / (n - 1)) * 2 - 1;
      const a = Math.abs(x);
      c[i] = a < 0.7 ? x : Math.sign(x) * (0.7 + 0.28 * Math.tanh((a - 0.7) / 0.28));
    }
    return c;
  }

  // Generated stereo hall: a few early reflections, then decaying noise that darkens over time.
  function impulse(sec) {
    const sr = ac.sampleRate;
    const n = Math.floor(sr * sec);
    const pre = Math.floor(sr * 0.015);
    const buf = ac.createBuffer(2, n, sr);
    for (let ch = 0; ch < 2; ch++) {
      const d = buf.getChannelData(ch);
      let y = 0;
      for (let i = pre; i < n; i++) {
        const x = (i - pre) / (n - pre);
        y += (0.85 - 0.7 * x) * (Math.random() * 2 - 1 - y);
        d[i] = y * Math.exp(-6 * x) * Math.min(1, (i - pre) / (sr * 0.02));
      }
      for (let k = 0; k < 6; k++) d[pre + Math.floor(Math.random() * sr * 0.07)] += (Math.random() - 0.5) * 0.8;
    }
    return buf;
  }

  function pan(v, out) {
    if (!ac.createStereoPanner) return out;
    const p = ac.createStereoPanner();
    p.pan.value = v;
    p.connect(out);
    return p;
  }

  // ---- instruments ----

  // Brass: two detuned brassy waves; the lowpass "blats" open then settles. Vibrato on long notes.
  function brass(dest, f, t, dur, vol, dark, atk) {
    atk = atk || 0.025;
    const g = gn(0);
    const lp = bq('lowpass', f, 1.1);
    const a = osc('brass', f, t, -6);
    const b = osc('brass', f, t, 6);
    lp.frequency.setValueAtTime(f * 0.9, t);
    lp.frequency.linearRampToValueAtTime(Math.min(f * (dark ? 3 : 6), 9000), t + atk + 0.02);
    lp.frequency.setTargetAtTime(Math.min(f * (dark ? 1.8 : 3.2), 7000), t + atk + 0.02, 0.12);
    const end = env(g.gain, t, dur, vol, atk, 0.15, 0.75, 0.05);
    a.connect(lp);
    b.connect(lp);
    link(lp, g, dest);
    const srcs = [a, b];
    if (dur > 0.3) srcs.push(vib(t, [a, b], 9));
    run(srcs, t, end, g);
  }

  // Soft flute-like lead: rounded wave, gentle attack, breath chiff, delayed vibrato.
  function flute(dest, f, t, dur, vol) {
    const g = gn(0);
    const a = osc('flute', f, t);
    const end = env(g.gain, t, dur, vol, 0.03, 0.2, 0.85, 0.05);
    link(a, g, dest);
    const srcs = [a];
    if (dur > 0.3) srcs.push(vib(t, [a], 14));
    run(srcs, t, end, g);
    hiss(dest, t, 0.08, 'bandpass', Math.min(f * 2, 8000), 0, 2, vol * 0.25, 0.01, 0.025);
  }

  // Delayed vibrato LFO on the oscillators' detune (cents).
  function vib(t, targets, cents) {
    const l = osc('sine', 5.2, t);
    const g = gn(0);
    g.gain.setValueAtTime(0, t + 0.15);
    g.gain.linearRampToValueAtTime(cents, t + 0.45);
    l.connect(g);
    targets.forEach((o) => g.connect(o.detune));
    return l;
  }

  // Sustained ensemble voice (strings / choir): two detuned saws, slow swell, long release.
  function pad(dest, f, t, dur, vol, atk, det) {
    det = det || 9;
    const g = gn(0);
    const a = osc('sawtooth', f, t, -det);
    const b = osc('sawtooth', f, t, det);
    const end = env(g.gain, t, dur, vol, Math.min(atk || 0.3, dur * 0.4), 0.4, 0.85, 0.15);
    a.connect(g);
    b.connect(g);
    g.connect(dest);
    run([a, b], t, end, g);
  }

  // Staccato string ostinato note: two detuned saws, fast decay (group lowpass does the tone).
  function ost(dest, f, t, dur, vol) {
    const g = gn(0);
    const a = osc('sawtooth', f, t, -7);
    const b = osc('sawtooth', f, t, 7);
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(vol, t + 0.006);
    g.gain.setTargetAtTime(vol * 0.25, t + 0.006, dur * 0.45);
    g.gain.setTargetAtTime(0, t + dur, 0.02);
    a.connect(g);
    b.connect(g);
    g.connect(dest);
    run([a, b], t, t + dur + 0.12, g);
  }

  // FM strike: sine carrier + modulator at `ratio` with a fast-falling index (harp, bells, pings).
  function fm(dest, f, t, len, vol, ratio, idx) {
    const c = osc('sine', f, t);
    const m = osc('sine', f * ratio, t);
    const mg = gn(0);
    const g = gn(0);
    mg.gain.setValueAtTime(f * idx, t);
    mg.gain.setTargetAtTime(f * idx * 0.08, t, len * 0.18);
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(vol, t + 0.003);
    g.gain.setTargetAtTime(0, t + 0.003, len / 4.5);
    link(m, mg, c.frequency);
    link(c, g, dest);
    run([c, m], t, t + len * 1.35, g);
  }

  // Bass: sine sub + saw through a lowpass that closes after the attack.
  function bass(dest, f, t, dur, vol) {
    const g = gn(0);
    const lp = bq('lowpass', f * 8, 1.4);
    const a = osc('sine', f, t);
    const b = osc('sawtooth', f, t, 5);
    lp.frequency.setValueAtTime(Math.min(f * 8, 2400), t);
    lp.frequency.setTargetAtTime(f * 2.5, t + 0.01, 0.08);
    const end = env(g.gain, t, dur, vol, 0.006, 0.12, 0.7, 0.03);
    a.connect(g);
    link(b, lp, g, dest);
    run([a, b], t, end, g);
  }

  // Pitched blip gliding f0 -> f1 with exponential decay (kick body, thumps, pops).
  function blip(dest, type, f0, f1, t, glide, vol, tau) {
    const o = osc(type, f0, t);
    const g = gn(0);
    o.frequency.exponentialRampToValueAtTime(f1, t + glide);
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(vol, t + 0.003);
    g.gain.setTargetAtTime(0, t + 0.01, tau);
    link(o, g, dest);
    run([o], t, t + 0.01 + tau * 6, g);
  }

  // Noise hit into a shared (kit) filter: instant attack, exponential decay.
  function burst(dest, t, vol, tau) {
    const s = nz();
    const g = gn(0);
    g.gain.setValueAtTime(vol, t);
    g.gain.setTargetAtTime(0, t + 0.001, tau);
    link(s, g, dest);
    run([s], t, t + 0.01 + tau * 6, g);
  }

  // Noise through its own filter, optionally sweeping f0 -> f1 over len (whooshes, sprays).
  function hiss(dest, t, len, type, f0, f1, q, vol, atk, tau) {
    const s = nz();
    const f = bq(type, f0, q);
    const g = gn(0);
    f.frequency.setValueAtTime(f0, t);
    if (f1) f.frequency.exponentialRampToValueAtTime(f1, t + len);
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(vol, t + atk);
    g.gain.setTargetAtTime(0, t + atk, tau);
    link(s, f, g, dest);
    run([s], t, Math.max(t + len, t + atk + tau * 6), g);
  }

  // Snow crunch: band-limited noise chopped into random grains that thin out.
  function crunch(dest, t, len, vol, f) {
    const s = nz();
    const bp = bq('bandpass', f, 0.8);
    const g = gn(0);
    for (let x = t; x < t + len; x += 0.007 + Math.random() * 0.014) {
      g.gain.setValueAtTime(vol * (1 - (x - t) / len) * (0.45 + Math.random() * 0.55), x);
      g.gain.setTargetAtTime(0, x + 0.002, 0.005 + Math.random() * 0.007);
    }
    link(s, bp, g, dest);
    run([s], t, t + len + 0.06, g);
  }

  // Soft muted "bonk": triangle + sub sine dropping through a dark lowpass.
  function bonk(dest, f, t, vol) {
    const g = gn(0);
    const lp = bq('lowpass', 900, 0.8);
    const a = osc('triangle', f, t);
    const c = osc('sine', f / 2, t);
    a.frequency.exponentialRampToValueAtTime(f * 0.7, t + 0.2);
    c.frequency.exponentialRampToValueAtTime(f * 0.35, t + 0.2);
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(vol, t + 0.006);
    g.gain.setTargetAtTime(0, t + 0.02, 0.08);
    a.connect(lp);
    c.connect(lp);
    link(lp, g, dest);
    run([a, c], t, t + 0.55, g);
  }

  // Resonant detuned-saw sweep ("force field" / power-up whoom).
  function zap(dest, t, f0, f1, len, vol) {
    const g = gn(0);
    const lp = bq('lowpass', 300, 7);
    const a = osc('sawtooth', f0, t, -14);
    const c = osc('sawtooth', f0, t, 14);
    a.frequency.exponentialRampToValueAtTime(f1, t + len);
    c.frequency.exponentialRampToValueAtTime(f1, t + len);
    lp.frequency.setValueAtTime(300, t);
    lp.frequency.exponentialRampToValueAtTime(3600, t + len * 0.6);
    lp.frequency.exponentialRampToValueAtTime(900, t + len);
    const end = env(g.gain, t, len, vol, len * 0.3, 0.1, 0.7, 0.06);
    a.connect(lp);
    c.connect(lp);
    link(lp, g, dest);
    run([a, c], t, end, g);
  }

  // Orchestral kit: shared filters per kit so each hit is only a source + gain.
  function mkKit(out) {
    return {
      out,
      click: link(bq('bandpass', 3200, 0.8), out),
      hat: link(bq('highpass', 7500, 0.7), out),
      snr: link(bq('bandpass', 1900, 0.6), out),
      cym: link(bq('highpass', 4500, 0.4), out),
      thump: link(bq('lowpass', 600, 0.7), out),
    };
  }

  function kick(K, t, v) {
    blip(K.out, 'sine', 150, 46, t, 0.12, v, 0.08);
    burst(K.click, t, v * 0.35, 0.004);
  }

  function snare(K, t, v) {
    burst(K.snr, t, v, 0.05);
    blip(K.out, 'triangle', 210, 165, t, 0.06, v * 0.6, 0.035);
  }

  // Timpani: modal wave with a small pitch settle + a soft mallet thump.
  function timp(K, f, t, v) {
    const o = osc('timp', f * 0.52, t);
    const g = gn(0);
    o.frequency.setTargetAtTime(f * 0.5, t, 0.04);
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(v, t + 0.005);
    g.gain.setTargetAtTime(0, t + 0.005, 0.3);
    link(o, g, K.out);
    run([o], t, t + 1.9, g);
    burst(K.thump, t, v * 0.7, 0.025);
  }

  // Reverse-cymbal swell that peaks right at t + dur (lands on the next section's crash).
  function swell(K, t, dur, v) {
    const s = nz();
    const f = bq('bandpass', 1200, 0.7);
    const g = gn(0);
    f.frequency.setValueAtTime(1200, t);
    f.frequency.exponentialRampToValueAtTime(9000, t + dur);
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(v, t + dur);
    g.gain.setTargetAtTime(0, t + dur, 0.04);
    link(s, f, g, K.out);
    run([s], t, t + dur + 0.3, g);
  }

  // Choir "ah": parallel formant bandpasses (+ a little low body) after the voices.
  function formant(out) {
    const inp = gn(1);
    [['lowpass', 450, 0.7, 0.5], ['bandpass', 700, 5, 2.2], ['bandpass', 1150, 7, 1.6], ['bandpass', 2700, 9, 0.8]]
      .forEach(([ty, f, q, v]) => link(inp, bq(ty, f, q), gn(v), out));
    return inp;
  }

  function hz(name) {
    const m = /^([A-G])(#?)(\d)$/.exec(name);
    const semis = { C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 }[m[1]] + (m[2] ? 1 : 0);
    return 440 * Math.pow(2, (12 * (Number(m[3]) + 1) + semis - 69) / 12);
  }

  // Timpani pitch: a note folded into a drum-like register.
  function low(n) {
    let f = hz(n);
    while (f < 85) f *= 2;
    return f;
  }

  const CHORD = {
    C: ['C4', 'E4', 'G4', 'C5'], Am: ['A3', 'C4', 'E4', 'A4'], F: ['F3', 'A3', 'C4', 'F4'], G: ['G3', 'B3', 'D4', 'G4'], E: ['E3', 'G#3', 'B3', 'E4'],
    Dm: ['D4', 'F4', 'A4', 'D5'], Bb: ['A#3', 'D4', 'F4', 'A#4'], Gm: ['G3', 'A#3', 'D4', 'G4'], A: ['A3', 'C#4', 'E4', 'A4'],
  };
  const ROOT = { C: ['C3', 'C4'], Am: ['A2', 'A3'], F: ['F2', 'F3'], G: ['G2', 'G3'], E: ['E2', 'E3'], Dm: ['D2', 'D3'], Bb: ['A#1', 'A#2'], Gm: ['G2', 'G3'], A: ['A2', 'A3'] };

  const TRACKS = {
    snow: {
      bpm: 150, bars: 8, loop: true, bassPat: 'rorororo', drum: 'k.h.s.h.k.hks.hh',
      chords: ['C', 'Am', 'F', 'G', 'C', 'Am', 'F|G', 'C'],
      lead: 'E5:2 G5:2 C6:2 G5:2 A5:2 G5:2 E5:4 A5:2 C6:2 A5:2 E5:2 G5:4 E5:4 F5:2 A5:2 C6:2 A5:2 G5:2 F5:2 E5:2 D5:2 D5:2 E5:2 F5:2 G5:2 D5:6 -:2 E5:2 G5:2 C6:2 G5:2 E6:4 D6:4 C6:2 A5:2 E5:2 A5:2 C6:4 B5:4 A5:2 F5:2 A5:2 C6:2 B5:2 G5:2 D6:4 C6:6 G5:2 E5:2 G5:2 C6:4'
    },
    // "Slope Showdown": original battle-style theme for runs. D minor, driving 8th-note bass,
    // 16th arpeggios, syncopated stabs, drum fill every 4 bars, 2-bar intro before the loop.
    battle: {
      bpm: 176, bars: 16, loop: true, bassPat: 'rrorrror', drum: 'k.hks.hkk.hks.hk', fill: 'k.hks.hks.s.ssss', stab: '..x..x....x..x.x',
      chords: ['Dm', 'Bb', 'C', 'A', 'Dm', 'Bb', 'Gm|A', 'Dm', 'Gm', 'Dm', 'Bb', 'F', 'Gm', 'A', 'Bb|C', 'A'],
      lead: 'D5:6 A4:2 D5:2 F5:2 E5:2 D5:2 F5:6 D5:2 F5:2 A#5:2 A5:2 F5:2 G5:6 E5:2 C5:2 E5:2 G5:2 C6:2 A5:8 G5:2 F5:2 E5:2 C#5:2 '
        + 'D5:6 A4:2 D5:2 F5:2 A5:2 D6:2 D6:4 C6:2 A#5:2 A5:4 F5:4 G5:4 A#5:4 A5:4 C#6:4 D6:12 -:4 '
        + 'A#5:8 A5:4 G5:4 F5:8 E5:4 D5:4 D5:4 F5:4 A#5:4 D6:4 C6:12 A5:4 '
        + 'A#5:4 A5:2 G5:2 F5:4 G5:4 A5:4 G5:2 F5:2 E5:4 C#5:4 D5:2 F5:2 A#5:2 D6:2 E6:2 C6:2 G5:2 E5:2 A5:4 E5:2 A5:2 C#6:4 E6:4',
      intro: {
        bars: 2,
        lead: 'D5:1 -:1 D5:1 -:1 D5:1 -:3 A#4:2 -:2 C5:2 -:2 C#5:1 D5:1 E5:1 F5:1 G5:1 A5:1 A#5:1 C6:1 C#6:4 E6:4',
        harmony: 'A4:1 -:1 A4:1 -:1 A4:1 -:3 F4:2 -:2 G4:2 -:2 A4:16',
        bassLine: 'D2:1 -:1 D2:1 -:1 D2:1 -:3 A#1:2 -:2 C2:2 -:2 A1:4 A1:4 A1:2 A1:2 A1:2 A1:2',
        drum2: ['k.k.k...s...s...', 's.s.s.s.ssssssss'],
      },
    },
    rival: {
      bpm: 170, bars: 8, loop: true, bassPat: 'rrorroro', drum: 'k.h.s.hkk.h.s.hh',
      chords: ['Am', 'F', 'G', 'E', 'Am', 'F', 'G', 'E'],
      lead: 'A4:2 C5:2 E5:2 A5:2 G5:2 E5:2 C5:2 E5:2 F5:4 E5:2 D5:2 C5:4 D5:4 D5:2 G5:2 B5:2 D6:2 C6:2 B5:2 G5:2 B5:2 E5:4 G#5:4 B5:4 E6:4 A5:2 -:2 A5:2 C6:2 B5:2 A5:2 G5:2 E5:2 F5:2 A5:2 C6:2 F6:2 E6:4 C6:4 D6:2 C6:2 B5:2 G5:2 A5:2 B5:2 C6:2 D6:2 E6:6 D6:2 B5:4 G#5:4'
    },
    clear: {
      bpm: 160, bars: 2, loop: false, drum2: ['k...s...k.k.s.s.', 'c...............'],
      lead: 'G5:2 C6:2 E6:2 G6:4 E6:2 F6:2 G6:2 A6:2 G6:2 E6:2 C7:10',
      harmony: 'E5:2 G5:2 C6:2 E6:4 C6:2 D6:2 E6:2 F6:2 E6:2 C6:2 G6:10',
      bassLine: 'C3:4 G3:4 C4:4 G3:4 F3:4 G3:4 C3:8'
    },
    fail: {
      bpm: 140, bars: 2, loop: false, drum2: ['k.......s.......', 'k...............'],
      lead: 'E5:2 D5:2 C5:4 G4:4 A4:2 B4:2 C5:4 E5:4 G5:8',
      bassLine: 'C3:8 G2:8 C3:16'
    }
  };

  // How each track is voiced (the note data above is untouched). dbl = horns an octave under the
  // lead, lv = lead level, choir = first bar with choir, timp 2 = extra fill-bar timpani,
  // cym = crash bars (swell in the bar before), cv = cymbal level, kit = drum level.
  const ARR = {
    battle: { lead: 'brass', dbl: 1, arp: 'ost', pad: 1, choir: 8, timp: 2, cym: [0, 8] },
    rival: { lead: 'brass', arp: 'ost', pad: 0.9, timp: 1, cym: [0], kit: 0.9 },
    snow: { lead: 'flute', lv: 1.25, arp: 'harp', pad: 0.8, cym: [0], cv: 0.6, kit: 0.75 },
    clear: { lead: 'brass', bell: 1 },
    fail: { lead: 'horn', lv: 2.3, kit: 0.9 },
  };

  // Event voices: (song, event, time, duration in s).
  const VOICE = {
    brass: (S, e, t, d) => brass(S.bus, e.f, t, d, 0.28 * e.v),
    horn: (S, e, t, d) => brass(S.bus, e.f, t, d, 0.15 * e.v, true, 0.045),
    stab: (S, e, t, d) => brass(S.bus, e.f, t, d, 0.12 * e.v, false, 0.006),
    flute: (S, e, t, d) => flute(S.bus, e.f, t, d, 0.24 * e.v),
    bell: (S, e, t) => fm(S.bus, e.f, t, 0.9, 0.06 * e.v, 3.5, 0.7),
    ost: (S, e, t, d) => ost(S.ost, e.f, t, d, 0.21 * e.v),
    harp: (S, e, t) => fm(S.ost, e.f, t, 0.7, 0.13 * e.v, 2, 1.1),
    pad: (S, e, t, d) => pad(e.x ? S.padR : S.padL, e.f, t, d, 0.06 * e.v),
    choir: (S, e, t, d) => pad(S.choir, e.f, t, d, 0.1 * e.v, 0.5, 12),
    bass: (S, e, t, d) => bass(S.bus, e.f, t, d, 0.17 * e.v),
    timp: (S, e, t) => timp(S.kit, e.f, t, 0.45 * e.v),
    k: (S, e, t) => kick(S.kit, t, 0.52 * e.v),
    s: (S, e, t) => snare(S.kit, t, 0.4 * e.v),
    h: (S, e, t) => burst(S.kit.hat, t, 0.14 * e.v, 0.012),
    c: (S, e, t) => burst(S.kit.cym, t, 0.3 * e.v, 0.45),
    sw: (S, e, t, d) => swell(S.kit, t, d, 0.2 * e.v),
  };

  // Parse "N:len" tokens (len in 16th steps): fn(name, startStep, len) for each non-rest.
  function notes(str, fn) {
    let p = 0;
    str.trim().split(/\s+/).forEach((tok) => {
      const parts = tok.split(':');
      const len = Number(parts[1]);
      if (parts[0] !== '-') fn(parts[0], p, len);
      p += len;
    });
  }

  // Flatten a section into per-bar event lists {s: step in bar, k: voice, f: Hz, d: steps, v, x}.
  function compile(sec, arr, intro) {
    const bars = [];
    for (let b = 0; b < sec.bars; b++) bars.push([]);
    const put = (p, k, f, d, v, x) => {
      const b = Math.min(sec.bars - 1, Math.floor(p / 16));
      bars[b].push({ s: p - b * 16, k, f, d, v, x });
    };
    const at = (halves, i) => halves[Math.min(halves.length - 1, Math.floor(i / (16 / halves.length)))];
    notes(sec.lead, (n, p, l) => {
      put(p, arr.lead, hz(n), l * 0.92, arr.lv || 1);
      if (arr.dbl) put(p, 'horn', hz(n) / 2, l * 0.92, 0.6);
      if (arr.bell) put(p, 'bell', hz(n), l, 1);
    });
    if (sec.harmony) notes(sec.harmony, (n, p, l) => put(p, 'horn', hz(n), l * 0.9, 1));
    const bl = [];
    if (sec.bassLine) notes(sec.bassLine, (n, p, l) => { put(p, 'bass', hz(n), l * 0.85, 1); bl[p] = n; });
    if (sec.chords) {
      sec.chords.forEach((bar, b) => {
        const halves = bar.split('|');
        const w = 16 / halves.length;
        halves.forEach((name, h) => {
          const p0 = b * 16 + h * w;
          for (let i = 0; i < w; i++) put(p0 + i, arr.arp, hz(CHORD[name][i % 4]), 0.9, 1);
          for (let i = 0; i < w / 2; i++) {
            const n = sec.bassPat[i % sec.bassPat.length] === 'o' ? ROOT[name][1] : ROOT[name][0];
            put(p0 + i * 2, 'bass', hz(n), 1.6, 1);
          }
          if (arr.pad) CHORD[name].forEach((n, j) => put(p0, 'pad', hz(n), w, arr.pad, j % 2));
          if (arr.choir != null && b >= arr.choir) CHORD[name].slice(1).forEach((n) => put(p0, 'choir', hz(n), w, 1));
          if (arr.timp) put(p0, 'timp', low(ROOT[name][0]), 0, h ? 0.7 : 1);
        });
        if (arr.timp > 1 && b % 4 === 3) [8, 12, 14].forEach((i, j) => put(b * 16 + i, 'timp', low(ROOT[at(halves, i)][0]), 0, 0.55 + j * 0.15));
        if (sec.stab) {
          for (let i = 0; i < 16; i++) {
            if (sec.stab[i] !== 'x') continue;
            const name = at(halves, i);
            put(b * 16 + i, 'stab', hz(CHORD[name][1]), 0.8, 1);
            put(b * 16 + i, 'stab', hz(CHORD[name][2]), 0.8, 0.8);
          }
        }
      });
    }
    const kit = arr.kit || 1;
    const hits = (b, pat) => {
      for (let i = 0; i < 16; i++) {
        const k = pat[i];
        const p = b * 16 + i;
        if (k === '.') continue;
        put(p, k, 0, 0, k === 'c' ? 1 : kit);
        if (intro && arr.timp && k === 'k' && bl[p]) put(p, 'timp', low(bl[p]), 0, 0.9);
      }
    };
    if (sec.drum) for (let b = 0; b < sec.bars; b++) hits(b, sec.fill && b % 4 === 3 ? sec.fill : sec.drum);
    if (sec.drum2) sec.drum2.forEach((p, b) => hits(b, p));
    if (arr.cym && intro) put((sec.bars - 1) * 16, 'sw', 0, 16, 1);
    if (arr.cym && !intro) {
      arr.cym.forEach((b) => {
        put(b * 16, 'c', 0, 0, 0.7 * (arr.cv || 1));
        put(((b - 1 + sec.bars) % sec.bars) * 16, 'sw', 0, 16, arr.cv || 1);
      });
    }
    return bars;
  }

  // Per-song buses: dry lead/bass/kit, panned string groups, choir formants. Faded as one.
  function mkSong(arr) {
    const bus = link(gn(1), musicBus);
    return {
      bus,
      kit: mkKit(bus),
      ost: link(bq('lowpass', 3400, 0.5), pan(-0.2, bus)),
      padL: link(bq('lowpass', 1700, 0.5), pan(-0.5, bus)),
      padR: link(bq('lowpass', 1700, 0.5), pan(0.5, bus)),
      choir: arr.choir != null ? formant(pan(0.15, bus)) : null,
    };
  }

  // Schedule one bar of events from t0 (anything already in the past is skipped).
  function bar(S, evs, t0) {
    const now = ac.currentTime - 0.01;
    evs.forEach((e) => {
      const t = t0 + e.s * S.step;
      if (t >= now) VOICE[e.k](S, e, t, e.d * S.step);
    });
  }

  // Keep LOOK seconds of music scheduled, one whole bar at a time.
  function pump(S) {
    if (song !== S) return;
    while (S.t < ac.currentTime + LOOK) {
      const sec = S.secs[S.si];
      bar(S, sec[S.bi], S.t);
      S.t += 16 * S.step;
      if (++S.bi < sec.length) continue;
      S.bi = 0;
      if (S.si + 1 < S.secs.length) S.si++;
      else if (!S.loop) return;
    }
    S.timer = setTimeout(() => pump(S), 250);
  }

  A.unlock = init;

  A.play = function (id, tempoMul) {
    if (!init()) return;
    A.stopMusic();
    const tr = TRACKS[id];
    if (!A.musicOn || !tr) return;
    const arr = ARR[id];
    if (!COMPILED[id]) COMPILED[id] = tr.intro ? [compile(tr.intro, arr, true), compile(tr, arr)] : [compile(tr, arr)];
    const S = mkSong(arr);
    S.secs = COMPILED[id];
    S.step = 60 / (tr.bpm * (tempoMul || 1)) / 4;
    S.si = 0;
    S.bi = 0;
    S.loop = tr.loop;
    S.t = ac.currentTime + 0.06;
    song = S;
    pump(S);
  };

  A.stopMusic = function () {
    if (!song) return;
    const S = song;
    song = null;
    clearTimeout(S.timer);
    const g = S.bus.gain;
    g.setValueAtTime(1, ac.currentTime);
    g.linearRampToValueAtTime(0.0001, ac.currentTime + 0.12);
    setTimeout(() => { try { S.bus.disconnect(); } catch (e) { /* already gone */ } }, 250);
  };

  A.duck = function (on) {
    if (!ac) return;
    musicBus.gain.setTargetAtTime(on ? 0.45 : 0.8, ac.currentTime, 0.08);
  };

  A.suspend = function () { if (ac && ac.state === 'running') ac.suspend(); };
  A.resume = function () { if (ac && ac.state === 'suspended') wake(); };

  A.sfx = function (id) {
    if (!A.sfxOn || !init()) return;
    const b = sfxBus;
    const K = sfxKit;
    const t = ac.currentTime + 0.01;
    switch (id) {
      case 'coin': { // glassy FM ping (root + fifth struck together) + airy sparkle
        const f = 1760 * (0.99 + Math.random() * 0.02);
        fm(b, f, t, 0.35, 0.17, 3.5, 0.8);
        fm(b, f * 1.5, t, 0.25, 0.06, 2, 0.5);
        burst(K.hat, t, 0.05, 0.025);
        break;
      }
      case 'right': // rolled brass major chord + bell sparkle on top
        ['C5', 'E5', 'G5', 'C6'].forEach((n, i) => brass(b, hz(n), t + i * 0.02, 0.4, 0.055, false, 0.012));
        ['G6', 'C7', 'E7'].forEach((n, i) => fm(b, hz(n), t + 0.08 + i * 0.05, 0.6, 0.04, 3.5, 0.5));
        break;
      case 'wrong': // two soft low bonks, falling
        bonk(b, 196, t, 0.23);
        bonk(b, 147, t + 0.13, 0.26);
        break;
      case 'jump': // airy whoosh up + soft rising body
        hiss(b, t, 0.3, 'bandpass', 450, 2800, 1.3, 0.5, 0.09, 0.07);
        blip(b, 'sine', 280, 640, t, 0.16, 0.12, 0.06);
        break;
      case 'flip': // double swish + shimmer
        hiss(b, t, 0.2, 'bandpass', 700, 3400, 1.6, 0.4, 0.07, 0.05);
        hiss(b, t + 0.13, 0.24, 'bandpass', 2800, 900, 1.4, 0.3, 0.06, 0.06);
        ['E6', 'G#6', 'B6'].forEach((n, i) => fm(b, hz(n), t + 0.1 + i * 0.045, 0.4, 0.045, 3.5, 0.6));
        break;
      case 'land': // snow crunch + soft thump
        crunch(b, t, 0.12, 0.32, 1700);
        blip(b, 'sine', 120, 50, t, 0.1, 0.35, 0.05);
        break;
      case 'crash': // heavy thud + snow burst settling
        blip(b, 'sine', 110, 36, t, 0.25, 0.85, 0.14);
        burst(K.thump, t, 0.5, 0.05);
        crunch(b, t, 0.35, 0.5, 1300);
        hiss(b, t, 0.9, 'lowpass', 5000, 350, 0.7, 0.36, 0.01, 0.22);
        break;
      case 'power': // magical rising sweep + harp-like arpeggio
        hiss(b, t, 0.5, 'bandpass', 400, 6500, 3, 0.24, 0.3, 0.1);
        zap(b, t, 131, 262, 0.45, 0.05);
        ['C5', 'G5', 'C6', 'E6', 'G6'].forEach((n, i) => fm(b, hz(n), t + i * 0.045, 0.5, 0.09, 2, 0.9));
        break;
      case 'shield': // impact deflected by a resonant force-field whoom + bell
        burst(K.snr, t, 0.6, 0.05);
        zap(b, t, 220, 440, 0.32, 0.16);
        fm(b, hz('E7'), t + 0.04, 0.5, 0.08, 3.5, 0.5);
        break;
      case 'star': // bell arpeggio + warm horn note + sparkle air
        ['E6', 'G6', 'C7', 'E7', 'G7'].forEach((n, i) => fm(b, hz(n), t + i * 0.04, 0.7, 0.1 - i * 0.009, 3.5, 0.6));
        brass(b, hz('C5'), t, 0.35, 0.13, true, 0.03);
        hiss(b, t, 0.5, 'highpass', 6000, 9000, 0.7, 0.07, 0.15, 0.1);
        break;
      case 'smash': // power-crystal impact: icy shatter + bright shards + short thump
        crunch(b, t, 0.2, 0.42, 3200);
        burst(K.thump, t, 0.35, 0.04);
        ['A6', 'E7', 'C#7'].forEach((n, i) => fm(b, hz(n), t + i * 0.03, 0.35, 0.05, 3.5, 0.6));
        break;
      case 'tick': fm(b, 1320, t, 0.06, 0.18, 2.4, 0.5); break;
      case 'pop': blip(b, 'sine', 480, 1300, t, 0.05, 0.19, 0.025); break;
      default: break;
    }
  };

  // ---- continuous ski layer: one looping noise for carve hiss + spray, another for wind ----

  function skiBuild() {
    const S = { on: false, last: {}, srcs: null };
    const out = link(gn(1), mix);
    S.hissF = bq('bandpass', 1200, 0.7);
    S.hissG = gn(0);
    S.sprayF = bq('highpass', 5500, 0.6);
    S.sprayG = gn(0);
    S.windF = bq('bandpass', 400, 1.8);
    S.windG = gn(0);
    link(S.hissF, S.hissG, out);
    link(S.sprayF, S.sprayG, out);
    link(S.windF, S.windG, out);
    // Random "LFOs" (noise played very slowly): grainy scrape on the hiss, gusts on the wind.
    S.flut = link(gn(0), S.hissG.gain);
    S.gustF = link(gn(120), S.windF.frequency);
    S.gustG = link(gn(0), S.windG.gain);
    return S;
  }

  function skiOn() {
    if (!ski) ski = skiBuild();
    const S = ski;
    clearTimeout(S.kill);
    S.on = true;
    if (S.srcs) return;
    const t = ac.currentTime;
    const n1 = nz();
    const n2 = nz(0.6);
    const g1 = nz(0.00002);
    const g2 = nz(0.000013);
    S.flutO = nz(0.0003);
    n1.connect(S.hissF);
    n1.connect(S.sprayF);
    n2.connect(S.windF);
    S.flutO.connect(S.flut);
    g1.connect(S.gustF);
    g2.connect(S.gustG);
    S.srcs = [n1, n2, S.flutO, g1, g2];
    S.srcs.forEach((s) => (s.buffer ? s.start(t, s.off) : s.start(t)));
  }

  // Ramp a param toward v unless it is already (nearly) there; keeps per-frame cost tiny.
  function aim(S, k, p, v, tau) {
    const o = S.last[k];
    if (o !== undefined && Math.abs(o - v) <= Math.abs(v) * 0.02 + 0.0005) return;
    S.last[k] = v;
    p.setTargetAtTime(v, ac.currentTime, tau);
  }

  // Per frame during a run: speed01 / carve01 in 0..1, onSnow false while airborne or tumbling.
  A.ski = function (speed01, carve01, onSnow) {
    if (!ac || ac.state !== 'running') return;
    if (!A.sfxOn) { A.skiStop(); return; }
    if (!ski || !ski.on) skiOn();
    const S = ski;
    const sp = Math.min(1, Math.max(0, speed01 || 0));
    const cv = Math.min(1, Math.max(0, carve01 || 0));
    const snow = onSnow ? 1 : 0;
    const h = snow * Math.min(1, sp * 4) * (0.13 + 0.1 * sp) * (1 + 0.35 * cv);
    aim(S, 'h', S.hissG.gain, h, snow ? 0.07 : 0.03);
    aim(S, 'fl', S.flut.gain, h * (0.15 + 0.25 * cv), 0.1);
    aim(S, 'hf', S.hissF.frequency, 500 + 1500 * sp + 1500 * cv, 0.1);
    aim(S, 'sp', S.sprayG.gain, snow * cv * cv * (0.25 + 0.75 * sp) * 0.06, 0.06);
    aim(S, 'ff', S.flutO.playbackRate, 0.0002 + 0.0006 * sp, 0.2);
    const w = (0.03 + 0.27 * sp * sp) * (snow ? 1 : 1.2);
    aim(S, 'w', S.windG.gain, w, 0.3);
    aim(S, 'gw', S.gustG.gain, w * 0.4, 0.3);
    aim(S, 'wf', S.windF.frequency, 260 + 1000 * sp, 0.35);
  };

  // Fade the ski + wind layers out; their sources are stopped once silent.
  A.skiStop = function () {
    if (!ski || !ski.on) return;
    const S = ski;
    S.on = false;
    S.last = {};
    [S.hissG, S.flut, S.sprayG, S.windG, S.gustG].forEach((g) => g.gain.setTargetAtTime(0, ac.currentTime, 0.12));
    clearTimeout(S.kill);
    S.kill = setTimeout(() => {
      if (S.on || !S.srcs) return;
      S.srcs.forEach((s) => { try { s.stop(); } catch (e) { /* not started */ } s.disconnect(); });
      S.srcs = null;
    }, 1500);
  };

  window.PSAudio = A;
})();
