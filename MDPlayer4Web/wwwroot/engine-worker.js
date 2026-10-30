// Runs the .NET playback engine (MDPlayerCore) off the main thread.
//
// Main thread -> worker:
//   {type:'files', items:[{path, data:ArrayBuffer}]}   write into the in-memory file system
//   {type:'load', id, path, loop, gen}                 load a song (replies 'loaded')
//   {type:'loop', on}  {type:'unload'}
//   {type:'port'} with a transferred MessagePort        connection to the PCM worklet
// Worker -> main thread:
//   {type:'ready', driversFolder, appFolder} {type:'loaded', id, info} {type:'status', ...}
//   {type:'filesWritten', id} {type:'error', message, id} {type:'fatal', message}
// Worklet <-> worker (over the port): worklet sends {need: frames, gen}; worker answers with
// {pcm: Float32Array (interleaved stereo), gen} or {end: true, gen} once the song has finished.

import { dotnet } from './_framework/dotnet.js';

let bridge = null;
let port = null;
let playing = false;
let ended = false;
let generation = 0;

async function start() {
  const runtime = await dotnet.create();
  await runtime.runMain();
  const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
  bridge = exports.MDPlayer.Web.EngineBridge;
  postMessage({ type: 'ready', driversFolder: bridge.DriversFolder(), appFolder: bridge.AppFolder() });
}

function render(frames) {
  if (!bridge || !playing) return null;
  const bytes = bridge.Render(frames);
  if (bytes.length === 0) return null;
  const pcm16 = new Int16Array(bytes.buffer, bytes.byteOffset, bytes.length >> 1);
  const out = new Float32Array(pcm16.length);
  for (let i = 0; i < pcm16.length; i++) out[i] = pcm16[i] / 32768;
  return out;
}

function onPortMessage(e) {
  const { need, gen } = e.data;
  if (!need || gen !== generation) return;
  const pcm = render(need);
  if (pcm) {
    port.postMessage({ pcm, gen }, [pcm.buffer]);
  } else if (playing && !ended) {
    ended = true;
    port.postMessage({ end: true, gen });
  }
  sendStatus();
}

function sendStatus() {
  if (!bridge) return;
  const status = JSON.parse(bridge.Status());
  postMessage({ type: 'status', ...status });
  if (status.loaded && status.stopped && !ended) {
    ended = true;
    port?.postMessage({ end: true, gen: generation });
  }
}

onmessage = (e) => {
  const msg = e.data;
  try {
    switch (msg.type) {
      case 'port':
        port = e.ports[0];
        port.onmessage = onPortMessage;
        break;
      case 'files':
        for (const item of msg.items) bridge.WriteFile(item.path, new Uint8Array(item.data));
        postMessage({ type: 'filesWritten', id: msg.id });
        break;
      case 'load': {
        playing = false;
        generation = msg.gen;
        const info = JSON.parse(bridge.Load(msg.path, !!msg.loop));
        ended = false;
        playing = !info.error;
        postMessage({ type: 'loaded', id: msg.id, info });
        break;
      }
      case 'loop':
        bridge?.SetLoop(!!msg.on);
        break;
      case 'unload':
        playing = false;
        bridge?.Unload();
        break;
    }
  } catch (err) {
    postMessage({ type: 'error', message: String(err?.message ?? err), id: msg.id });
  }
};

start().catch((err) => postMessage({ type: 'fatal', message: String(err?.message ?? err) }));
