// Exports web-prototype courses as tab-separated fixtures for the Unity parity tests.
const fs = require('fs');
const path = require('path');
global.window = {};
eval(fs.readFileSync(process.argv[2], 'utf8'));
const out = process.argv[3];
const D = window.PSData;
const f6 = (v) => (Math.round(v * 1e6) / 1e6).toString();
function dump(c) {
  const lines = [];
  for (const o of c.objs) lines.push(['O', o.type, f6(o.x), f6(o.z), f6(o.s || 0), f6(o.hw || 0), o.kind || ''].join('\t'));
  for (const r of c.rows) {
    const opts = r.q.options.map((op) => (op.label != null ? op.label : op.shape) + (op.correct ? '*' : '')).join(',');
    lines.push(['R', f6(r.z), r.n, r.q.text, opts].join('\t'));
  }
  for (const f of c.forks || []) lines.push(['F', f6(f.z0), f6(f.z1)].join('\t'));
  for (const d of c.decor) lines.push(['D', d.type, f6(d.x), f6(d.z), f6(d.w || 0), f6(d.h || 0), f6(d.s || 0)].join('\t'));
  for (const z of [0, 777, 4321.5, 9999]) lines.push(['S', f6(z), f6(c.safeX(z))].join('\t'));
  return lines.join('\n') + '\n';
}
for (const L of D.LEVELS) fs.writeFileSync(path.join(out, 'L' + L.id + '.txt'), dump(D.build(L)));
fs.writeFileSync(path.join(out, 'Endless-20261104.txt'), dump(D.buildEndless(20261104)));
// Raw mulberry32 stream: rebuild rng from the module source.
const src = fs.readFileSync(process.argv[2], 'utf8');
const rngSrc = src.slice(src.indexOf('function rng'), src.indexOf('const pick'));
const rng = eval('(' + rngSrc.replace(/^function rng/, 'function') + ')');
const r = rng(12345);
fs.writeFileSync(path.join(out, 'Rng-12345.txt'), Array.from({ length: 20 }, () => r().toString()).join('\n') + '\n');
console.log(fs.readdirSync(out).map((f) => f + ' ' + fs.statSync(path.join(out, f)).size).join('\n'));
