// MDPlayer4Web front end: playlist, transport and audio graph.
//
// Chip-music files are played by the MDPlayerCore engine running in engine-worker.js;
// its PCM reaches the speakers through the pcm-player AudioWorklet. Ordinary audio files
// (WAV, MP3, OGG, FLAC, M4A, ...) are decoded by the browser itself.

const ENGINE_EXTENSIONS = new Set([
  '.vgm', '.vgz', '.xgm', '.xgz', '.sid', '.mnd', '.mgs', '.zms', '.zmd', '.mdx',
  '.mdr', '.mdl', '.mub', '.muc', '.mml', '.m', '.m2', '.mz', '.mus', '.o', '.ox', '.oy',
  '.msd', '.bgm', '.nrd', '.ndp', '.opi', '.ovi', '.ozi', '.mpi', '.mvi', '.mzi',
  '.nsf', '.gbs', '.hes', '.s98', '.ay', '.zgm',
]);
// MIDI/RCP use the macOS system synthesizer and are not available in the browser.
const MIDI_EXTENSIONS = new Set(['.mid', '.rcp', '.rcs']);
const NATIVE_EXTENSIONS = new Set(['.wav', '.aif', '.aiff', '.mp3', '.m4a', '.aac', '.flac', '.ogg']);
const RHYTHM_FILES = ['2608_BD.WAV', '2608_HH.WAV', '2608_RIM.WAV', '2608_SD.WAV', '2608_TOM.WAV', '2608_TOP.WAV'];
const SAMPLE_RATE = 44100;

const $ = (id) => document.getElementById(id);
const ui = {
  engineState: $('engineState'), title: $('title'), subline: $('subline'), chips: $('chips'),
  time: $('time'), total: $('total'), progress: $('progress'), status: $('status'),
  play: $('play'), stop: $('stop'), prev: $('prev'), next: $('next'), loop: $('loop'),
  volume: $('volume'), playlist: $('playlist'), scope: $('scope'), driverList: $('driverList'),
  dropOverlay: $('dropOverlay'),
};

const state = {
  playlist: [],          // {name, ext, path, kind:'engine'|'native', file}
  current: -1,
  mode: 'off',           // 'off' | 'repeat' | 'random'
  playing: false,
  paused: false,
  gen: 0,                // engine song generation; stale PCM is dropped by the worklet
  batch: 0,
  driversFolder: '/drivers',
  engineReady: false,
  native: null,          // {source, startedAt, offset, duration}
  position: 0,
  total: 0,
};

// ---- engine worker ---------------------------------------------------------------------

const worker = new Worker('engine-worker.js', { type: 'module' });
const pending = new Map();
let requestId = 0;
function call(message, transfer = []) {
  const id = ++requestId;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    worker.postMessage({ ...message, id }, transfer);
  });
}
let engineReady;
const engineReadyPromise = new Promise((resolve) => { engineReady = resolve; });

worker.onmessage = (e) => {
  const msg = e.data;
  switch (msg.type) {
    case 'ready':
      state.driversFolder = msg.driversFolder;
      state.appFolder = msg.appFolder;
      engineReady();
      break;
    case 'loaded':
    case 'filesWritten':
      pending.get(msg.id)?.resolve(msg);
      pending.delete(msg.id);
      break;
    case 'error':
      if (msg.id && pending.has(msg.id)) { pending.get(msg.id).reject(new Error(msg.message)); pending.delete(msg.id); }
      else setStatus(msg.message, true);
      break;
    case 'status':
      if (msg.loaded && state.native === null) {
        state.position = msg.position;
        if (msg.total > 0) state.total = msg.total;
      }
      break;
    case 'fatal':
      ui.engineState.textContent = '엔진을 시작하지 못했습니다';
      setStatus('엔진 오류: ' + msg.message, true);
      break;
  }
};

async function writeFiles(items) {
  if (items.length === 0) return;
  await call({ type: 'files', items }, items.map((i) => i.data));
}

// ---- audio graph -----------------------------------------------------------------------

let audio = null;   // {ctx, node, gain, analyser}
async function ensureAudio() {
  if (audio) {
    if (audio.ctx.state === 'suspended' && !state.paused) await audio.ctx.resume();
    return audio;
  }
  const ctx = new AudioContext({ sampleRate: SAMPLE_RATE, latencyHint: 'playback' });
  await ctx.audioWorklet.addModule('pcm-worklet.js');
  const node = new AudioWorkletNode(ctx, 'pcm-player', { numberOfInputs: 0, outputChannelCount: [2] });
  const gain = ctx.createGain();
  const analyser = ctx.createAnalyser();
  analyser.fftSize = 2048;
  node.connect(gain);
  gain.connect(analyser);
  analyser.connect(ctx.destination);
  const channel = new MessageChannel();
  worker.postMessage({ type: 'port' }, [channel.port1]);
  node.port.postMessage({ workerPort: channel.port2 }, [channel.port2]);
  node.port.onmessage = (e) => { if (e.data.drained && e.data.drained === state.gen) onTrackEnded(); };
  audio = { ctx, node, gain, analyser };
  applyVolume();
  drawScope();
  return audio;
}

function applyVolume() {
  if (!audio) return;
  const v = Number(ui.volume.value) / 100;
  audio.gain.gain.value = v * v;
}

// ---- playlist --------------------------------------------------------------------------

function extOf(name) {
  const dot = name.lastIndexOf('.');
  return dot < 0 ? '' : name.slice(dot).toLowerCase();
}

// files: [{file: File, rel: 'folder/name.ext'}]
async function addFiles(files) {
  if (files.length === 0) return;
  await engineReadyPromise;
  const batch = ++state.batch;
  const root = `/music/${batch}`;
  const writes = [];
  const added = [];
  const byPath = new Map();
  let skippedMidi = 0;
  for (const { file, rel } of files) {
    const path = `${root}/${rel}`;
    const ext = extOf(file.name);
    byPath.set(path.toLowerCase(), { file, path, ext });
    if (NATIVE_EXTENSIONS.has(ext)) {
      added.push({ name: rel, ext, path, kind: 'native', file });
      continue;
    }
    // Everything else goes to the engine's file system: songs and the companion files
    // (PCM, PDX, voice data, rhythm WAVs ...) their drivers look up next to them.
    writes.push({ path, data: await file.arrayBuffer() });
    if (ENGINE_EXTENSIONS.has(ext)) added.push({ name: rel, ext, path, kind: 'engine' });
    else if (MIDI_EXTENSIONS.has(ext)) skippedMidi++;
  }
  await writeFiles(writes);

  // .m3u playlists: add their entries that were opened in the same batch.
  for (const { file, rel } of files) {
    if (extOf(file.name) !== '.m3u') continue;
    const base = rel.includes('/') ? rel.slice(0, rel.lastIndexOf('/') + 1) : '';
    for (const line of (await file.text()).split(/\r?\n/)) {
      const entry = line.trim();
      if (!entry || entry.startsWith('#')) continue;
      const target = byPath.get(`${root}/${base}${entry.replace(/\\/g, '/')}`.toLowerCase());
      if (!target) continue;
      const kind = NATIVE_EXTENSIONS.has(target.ext) ? 'native' : ENGINE_EXTENSIONS.has(target.ext) ? 'engine' : null;
      if (kind) added.push({ name: entry, ext: target.ext, path: target.path, kind, file: target.file });
    }
  }

  added.sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }));
  const wasEmpty = state.playlist.length === 0;
  state.playlist.push(...added);
  renderPlaylist();
  const notes = [`${added.length}곡 추가`];
  if (skippedMidi) notes.push(`MIDI/RCP ${skippedMidi}개는 웹판에서 재생할 수 없어 제외`);
  setStatus(notes.join(' · '));
  if (wasEmpty && added.length > 0) playIndex(state.playlist.length - added.length);
}

function renderPlaylist() {
  ui.playlist.replaceChildren(...state.playlist.map((entry, index) => {
    const li = document.createElement('li');
    li.textContent = entry.name;
    const ext = document.createElement('span');
    ext.className = 'ext';
    ext.textContent = entry.kind === 'native' ? '오디오' : entry.ext.slice(1).toUpperCase();
    li.append(ext);
    if (index === state.current) li.className = 'current';
    li.onclick = () => playIndex(index);
    return li;
  }));
}

// ---- playback --------------------------------------------------------------------------

async function playIndex(index) {
  if (index < 0 || index >= state.playlist.length) return;
  const entry = state.playlist[index];
  await ensureAudio();
  stopEverything();
  state.current = index;
  state.paused = false;
  state.position = 0;
  state.total = 0;
  renderPlaylist();
  showInfo({ title: entry.name.split('/').pop() }, entry);
  if (audio.ctx.state === 'suspended') await audio.ctx.resume();
  if (entry.kind === 'native') return playNative(entry);

  const gen = ++state.gen;
  audio.node.port.postMessage({ reset: 0 });
  setStatus('불러오는 중…');
  let info;
  try {
    ({ info } = await call({ type: 'load', path: entry.path, loop: state.mode === 'repeat', gen }));
  } catch (err) {
    info = { error: err.message };
  }
  if (gen !== state.gen) return;
  if (info.error) {
    state.playing = false;
    updateTransport();
    setStatus('재생할 수 없습니다: ' + info.error, true);
    return;
  }
  showInfo(info, entry);
  state.total = info.total;
  state.playing = true;
  audio.node.port.postMessage({ reset: gen });
  updateTransport();
  setStatus(`재생 중 · ${info.chips}`);
}

async function playNative(entry) {
  try {
    const buffer = await audio.ctx.decodeAudioData(await entry.file.arrayBuffer());
    const source = audio.ctx.createBufferSource();
    source.buffer = buffer;
    source.connect(audio.gain);
    const token = {};
    source.onended = () => { if (state.native?.token === token) { state.native = null; onTrackEnded(); } };
    state.native = { source, token, startedAt: audio.ctx.currentTime, duration: buffer.duration };
    source.start();
    state.total = buffer.duration;
    state.playing = true;
    showInfo({ title: entry.name.split('/').pop(), chips: `PCM ${buffer.numberOfChannels}ch ${buffer.sampleRate}Hz`, format: entry.ext.slice(1).toUpperCase() }, entry);
    setStatus('재생 중 · 브라우저 디코더');
  } catch (err) {
    state.playing = false;
    setStatus('이 브라우저에서 디코딩할 수 없는 오디오 파일입니다: ' + (err?.message ?? err), true);
  }
  updateTransport();
}

function stopEverything() {
  if (state.native) {
    const { source } = state.native;
    state.native = null;
    try { source.stop(); } catch { /* already stopped */ }
  }
  state.gen++;
  audio?.node.port.postMessage({ reset: 0 });
  worker.postMessage({ type: 'unload' });
  state.playing = false;
}

function stop() {
  stopEverything();
  state.paused = false;
  state.position = 0;
  if (audio?.ctx.state === 'suspended') audio.ctx.resume();
  updateTransport();
  setStatus('정지');
}

async function togglePlay() {
  if (!state.playing) {
    if (state.playlist.length > 0) playIndex(state.current >= 0 ? state.current : 0);
    return;
  }
  if (state.paused) {
    await audio.ctx.resume();
    state.paused = false;
    setStatus('재생 중');
  } else {
    await audio.ctx.suspend();
    state.paused = true;
    setStatus('일시정지');
  }
  updateTransport();
}

function onTrackEnded() {
  state.playing = false;
  updateTransport();
  if (state.playlist.length === 0) return;
  if (state.mode === 'repeat') return playIndex(state.current);
  if (state.mode === 'random') return playIndex(Math.floor(Math.random() * state.playlist.length));
  if (state.current + 1 < state.playlist.length) return playIndex(state.current + 1);
  setStatus('재생 목록 끝');
}

function step(delta) {
  if (state.playlist.length === 0) return;
  const n = state.playlist.length;
  const index = state.mode === 'random' ? Math.floor(Math.random() * n) : ((state.current < 0 ? 0 : state.current) + delta + n) % n;
  playIndex(index);
}

function cycleMode() {
  state.mode = state.mode === 'off' ? 'repeat' : state.mode === 'repeat' ? 'random' : 'off';
  ui.loop.textContent = { off: '반복 끔', repeat: '반복 재생', random: '랜덤 재생' }[state.mode];
  worker.postMessage({ type: 'loop', on: state.mode === 'repeat' });
}

// ---- display ---------------------------------------------------------------------------

function showInfo(info, entry) {
  ui.title.textContent = info.title || entry.name.split('/').pop();
  const parts = [info.game, info.system, info.composer].filter((v) => v);
  ui.subline.textContent = parts.join(' · ');
  ui.chips.textContent = [info.format, info.chips].filter((v) => v).join(' · ');
  document.title = `${ui.title.textContent} - MDPlayer4Web`;
}

function setStatus(text, error = false) {
  ui.status.textContent = text;
  ui.status.classList.toggle('error', error);
}

function updateTransport() {
  ui.play.textContent = state.playing && !state.paused ? '❚❚' : '▶';
}

function formatTime(seconds) {
  if (!isFinite(seconds) || seconds < 0) return '--:--';
  const s = Math.floor(seconds);
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
}

function tick() {
  let position = state.position;
  if (state.native && audio) position = audio.ctx.currentTime - state.native.startedAt;
  ui.time.textContent = formatTime(state.playing ? position : 0);
  ui.total.textContent = state.total > 0 ? formatTime(state.total) : '--:--';
  ui.progress.style.width = state.total > 0 && state.playing ? `${Math.min(100, (position / state.total) * 100)}%` : '0';
  requestAnimationFrame(tick);
}

function drawScope() {
  const canvas = ui.scope;
  const g = canvas.getContext('2d');
  const data = new Float32Array(audio.analyser.fftSize);
  const color = () => getComputedStyle(document.documentElement).getPropertyValue('--scope').trim();
  const frame = () => {
    const width = canvas.width = canvas.clientWidth * devicePixelRatio;
    const height = canvas.height = canvas.clientHeight * devicePixelRatio;
    audio.analyser.getFloatTimeDomainData(data);
    g.clearRect(0, 0, width, height);
    g.strokeStyle = color();
    g.lineWidth = 1.5 * devicePixelRatio;
    g.beginPath();
    for (let i = 0; i < data.length; i++) {
      const x = (i / (data.length - 1)) * width;
      const y = height / 2 - data[i] * height * 0.45;
      if (i === 0) g.moveTo(x, y); else g.lineTo(x, y);
    }
    g.stroke();
    requestAnimationFrame(frame);
  };
  frame();
}

// ---- driver files (kept in IndexedDB) ----------------------------------------------------

function openDb() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open('mdplayer4web', 1);
    request.onupgradeneeded = () => request.result.createObjectStore('drivers');
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}
async function dbTransaction(mode, action) {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction('drivers', mode);
    const result = action(tx.objectStore('drivers'));
    tx.oncomplete = () => resolve(result?.result ?? result);
    tx.onerror = () => reject(tx.error);
  });
}
async function savedDrivers() {
  try {
    const db = await openDb();
    return await new Promise((resolve, reject) => {
      const out = [];
      const cursor = db.transaction('drivers').objectStore('drivers').openCursor();
      cursor.onsuccess = () => {
        const c = cursor.result;
        if (c) { out.push({ name: c.key, data: c.value }); c.continue(); } else resolve(out);
      };
      cursor.onerror = () => reject(cursor.error);
    });
  } catch { return []; }
}
async function installDrivers(list) {
  await writeFiles(list.map(({ name, data }) => ({ path: `${state.driversFolder}/${name}`, data: data.slice(0) })));
}
async function addDrivers(files) {
  await engineReadyPromise;
  const list = [];
  for (const file of files) list.push({ name: file.name, data: await file.arrayBuffer() });
  await dbTransaction('readwrite', (store) => { for (const d of list) store.put(d.data, d.name); });
  await installDrivers(list);
  await renderDrivers();
  setStatus(`드라이버 파일 ${list.length}개 추가`);
}
async function renderDrivers() {
  const list = await savedDrivers();
  ui.driverList.replaceChildren(...list.map(({ name, data }) => {
    const li = document.createElement('li');
    li.textContent = `${name} (${data.byteLength.toLocaleString()} B)`;
    return li;
  }));
}

// ---- wiring ----------------------------------------------------------------------------

function filesFromInput(input) {
  return [...input.files].map((file) => ({ file, rel: file.webkitRelativePath || file.name }));
}

async function filesFromDrop(dataTransfer) {
  const out = [];
  const walk = async (entry, prefix) => {
    if (entry.isFile) {
      const file = await new Promise((resolve, reject) => entry.file(resolve, reject));
      out.push({ file, rel: prefix + file.name });
    } else if (entry.isDirectory) {
      const reader = entry.createReader();
      for (;;) {
        const batch = await new Promise((resolve, reject) => reader.readEntries(resolve, reject));
        if (batch.length === 0) break;
        for (const child of batch) await walk(child, `${prefix}${entry.name}/`);
      }
    }
  };
  const entries = [...dataTransfer.items].map((item) => item.webkitGetAsEntry?.()).filter(Boolean);
  if (entries.length === 0) return [...dataTransfer.files].map((file) => ({ file, rel: file.name }));
  for (const entry of entries) await walk(entry, '');
  return out;
}

$('pickFiles').onchange = (e) => { addFiles(filesFromInput(e.target)); e.target.value = ''; };
$('pickFolder').onchange = (e) => { addFiles(filesFromInput(e.target)); e.target.value = ''; };
$('pickDrivers').onchange = (e) => { addDrivers([...e.target.files]); e.target.value = ''; };
$('clear').onclick = () => { stop(); state.playlist = []; state.current = -1; renderPlaylist(); setStatus('재생 목록을 비웠습니다'); };
$('clearDrivers').onclick = async () => {
  await dbTransaction('readwrite', (store) => store.clear());
  await renderDrivers();
  setStatus('저장한 드라이버 파일을 지웠습니다. 새로 고치면 엔진에서도 사라집니다.');
};
ui.play.onclick = togglePlay;
ui.stop.onclick = stop;
ui.prev.onclick = () => step(-1);
ui.next.onclick = () => step(1);
ui.loop.onclick = cycleMode;
ui.volume.oninput = applyVolume;

let dragDepth = 0;
window.addEventListener('dragenter', (e) => { e.preventDefault(); dragDepth++; ui.dropOverlay.hidden = false; });
window.addEventListener('dragleave', () => { if (--dragDepth <= 0) { dragDepth = 0; ui.dropOverlay.hidden = true; } });
window.addEventListener('dragover', (e) => e.preventDefault());
window.addEventListener('drop', async (e) => {
  e.preventDefault();
  dragDepth = 0;
  ui.dropOverlay.hidden = true;
  addFiles(await filesFromDrop(e.dataTransfer));
});
window.addEventListener('keydown', (e) => {
  if (e.code === 'Space' && e.target === document.body) { e.preventDefault(); togglePlay(); }
});

// Start-up: shared rhythm samples and saved driver files go into the engine first.
(async () => {
  await engineReadyPromise;
  const rhythm = [];
  for (const name of RHYTHM_FILES) {
    try {
      const response = await fetch(`rhythm/${name}`);
      if (response.ok) rhythm.push({ path: `${state.appFolder}${state.appFolder.endsWith('/') ? '' : '/'}${name}`, data: await response.arrayBuffer() });
    } catch { /* optional */ }
  }
  await writeFiles(rhythm);
  await installDrivers(await savedDrivers());
  await renderDrivers();
  state.engineReady = true;
  ui.engineState.textContent = '엔진 준비됨';
  ui.engineState.classList.add('ok');
})();

requestAnimationFrame(tick);
window.mdplayer4web = { state, addFiles, playIndex, stop, get audio() { return audio; } };
