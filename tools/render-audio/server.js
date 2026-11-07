// Renders the web prototype's procedural audio (prototype/web/js/audio.js) to WAV files for Unity.
// Serves a page that drives audio.js through an OfflineAudioContext and POSTs each clip back here;
// clips are written to unity/Assets/Resources/Audio. Usage: node tools/render-audio/server.js
// then open http://localhost:8766/ in a Chromium browser and wait for "done".
const http = require('http');
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '../..');
const OUT = path.join(ROOT, 'unity/Assets/Resources/Audio');
const PORT = Number(process.argv[2] || 8766);
fs.mkdirSync(OUT, { recursive: true });

http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  if (req.method === 'GET' && (url.pathname === '/' || url.pathname === '/index.html')) {
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
    res.end(fs.readFileSync(path.join(__dirname, 'index.html')));
  } else if (req.method === 'GET' && url.pathname === '/audio.js') {
    res.writeHead(200, { 'Content-Type': 'text/javascript; charset=utf-8' });
    res.end(fs.readFileSync(path.join(ROOT, 'prototype/web/js/audio.js')));
  } else if (req.method === 'POST' && url.pathname === '/save') {
    const name = (url.searchParams.get('name') || '').replace(/[^a-z0-9_]/gi, '');
    if (!name) { res.writeHead(400); res.end('bad name'); return; }
    const chunks = [];
    req.on('data', (c) => chunks.push(c));
    req.on('end', () => {
      const buf = Buffer.concat(chunks);
      fs.writeFileSync(path.join(OUT, name + '.wav'), buf);
      console.log('saved', name + '.wav', buf.length, 'bytes');
      res.writeHead(200);
      res.end('ok');
    });
  } else if (req.method === 'POST' && url.pathname === '/log') {
    const chunks = [];
    req.on('data', (c) => chunks.push(c));
    req.on('end', () => { console.log(Buffer.concat(chunks).toString()); res.writeHead(200); res.end('ok'); });
  } else {
    res.writeHead(404);
    res.end();
  }
}).listen(PORT, () => console.log('render-audio on http://localhost:' + PORT + ' → ' + OUT));
