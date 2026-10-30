// Starts the .NET runtime and the Avalonia player (see MDPlayer4Web.csproj), wiring up
// audio output, saved settings/driver files and file drag-and-drop.
import { dotnet } from './_framework/dotnet.js';
import * as audio from './audio.js';
import * as storage from './storage.js';

// A home folder gives the player the same per-user folders as on a desktop
// (application data for its settings, the Drivers folder for driver files).
const runtime = await dotnet.withEnvironmentVariable('HOME', '/home/web').create();
const config = runtime.getConfig();
runtime.setModuleImports('mdplayer', { ...audio.imports, ...storage.imports, pickFiles });
const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
const web = exports.MDPlayer.UI;
audio.connect(web.WebAudio);

// Settings and driver files from earlier visits go back into the file system first.
for (const { path, data } of await storage.loadAll()) web.WebPersistence.Restore(path, data);
addEventListener('pagehide', () => web.WebPersistence.Flush());

installDragAndDrop(web.WebDrop);
// Lets automated tests feed files the same way a drop does.
globalThis.mdplayer4web = { drop: web.WebDrop, persistence: web.WebPersistence };
document.getElementById('splash')?.remove();
await runtime.runMain();

// Browsers do not hand dropped files to Avalonia with paths, so take the drop here,
// copy everything (folders included) into the runtime's file system and pass it on.
function installDragAndDrop(drop) {
  let depth = 0;
  const hasFiles = (e) => [...(e.dataTransfer?.types ?? [])].includes('Files');
  addEventListener('dragenter', (e) => {
    if (!hasFiles(e)) return;
    e.preventDefault(); e.stopPropagation();
    depth++;
    drop.Over(e.clientX, e.clientY);
  }, true);
  addEventListener('dragover', (e) => {
    if (!hasFiles(e)) return;
    e.preventDefault(); e.stopPropagation();
    e.dataTransfer.dropEffect = 'copy';
    drop.Over(e.clientX, e.clientY);
  }, true);
  addEventListener('dragleave', (e) => {
    if (!hasFiles(e)) return;
    e.preventDefault(); e.stopPropagation();
    if (--depth <= 0) { depth = 0; drop.Leave(); }
  }, true);
  addEventListener('drop', async (e) => {
    if (!hasFiles(e)) return;
    e.preventDefault(); e.stopPropagation();
    depth = 0;
    const x = e.clientX, y = e.clientY;
    const entries = [...e.dataTransfer.items].map((item) => item.webkitGetAsEntry?.()).filter(Boolean);
    const files = [...e.dataTransfer.files];
    const folder = drop.Begin();
    const names = [];
    if (entries.length > 0) {
      for (const entry of entries) {
        names.push(entry.name);
        await copyEntry(drop, folder, entry, '');
      }
    } else {
      for (const file of files) {
        names.push(file.name);
        drop.AddFile(folder, file.name, await readFile(file));
      }
    }
    drop.Complete(folder, JSON.stringify(names), x, y);
  }, true);
}

// The page's file chooser for the Open button: copies the chosen files into one folder
// of the runtime's file system (so a song and its companion files stay together).
function pickFiles() {
  const drop = web.WebDrop;
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.multiple = true;
    let settled = false;
    const finish = async () => {
      if (settled) return;
      settled = true;
      const files = [...(input.files ?? [])];
      const folder = files.length ? drop.Begin() : '';
      for (const file of files) drop.AddFile(folder, file.name, await readFile(file));
      resolve(JSON.stringify({ folder, names: files.map((f) => f.name) }));
    };
    input.addEventListener('change', finish);
    input.addEventListener('cancel', finish);
    input.click();
  });
}

async function copyEntry(drop, folder, entry, prefix) {
  if (entry.isFile) {
    const file = await new Promise((resolve, reject) => entry.file(resolve, reject));
    drop.AddFile(folder, prefix + file.name, await readFile(file));
  } else if (entry.isDirectory) {
    const reader = entry.createReader();
    for (;;) {
      const batch = await new Promise((resolve, reject) => reader.readEntries(resolve, reject));
      if (batch.length === 0) break;
      for (const child of batch) await copyEntry(drop, folder, child, `${prefix}${entry.name}/`);
    }
  }
}

// On macOS, MP3/M4A/AAC/FLAC/AIFF files are decoded by Apple's AudioToolbox, which the
// browser build does not have. The browser's own decoder turns them into WAV data here;
// the player still sees the original file name and decodes the content as WAV.
const BROWSER_DECODED = /\.(mp3|m4a|aac|flac|aif|aiff)$/i;

async function readFile(file) {
  const buffer = await file.arrayBuffer();
  if (!BROWSER_DECODED.test(file.name)) return new Uint8Array(buffer);
  try {
    const decoder = new OfflineAudioContext(2, 1, 44100);
    return encodeWav(await decoder.decodeAudioData(buffer));
  } catch {
    return new Uint8Array(buffer);
  }
}

function encodeWav(audio) {
  const channels = Math.min(2, audio.numberOfChannels);
  const frames = audio.length;
  const out = new DataView(new ArrayBuffer(44 + frames * channels * 2));
  const text = (offset, value) => { for (let i = 0; i < value.length; i++) out.setUint8(offset + i, value.charCodeAt(i)); };
  text(0, 'RIFF'); out.setUint32(4, 36 + frames * channels * 2, true); text(8, 'WAVE');
  text(12, 'fmt '); out.setUint32(16, 16, true); out.setUint16(20, 1, true); out.setUint16(22, channels, true);
  out.setUint32(24, audio.sampleRate, true); out.setUint32(28, audio.sampleRate * channels * 2, true);
  out.setUint16(32, channels * 2, true); out.setUint16(34, 16, true);
  text(36, 'data'); out.setUint32(40, frames * channels * 2, true);
  const data = [...Array(channels).keys()].map((c) => audio.getChannelData(c));
  let offset = 44;
  for (let i = 0; i < frames; i++) {
    for (let c = 0; c < channels; c++) {
      const v = Math.max(-1, Math.min(1, data[c][i]));
      out.setInt16(offset, v < 0 ? v * 0x8000 : v * 0x7fff, true);
      offset += 2;
    }
  }
  return new Uint8Array(out.buffer);
}
