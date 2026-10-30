// Web Audio output for CoreAudioQueue.cs. One AudioContext per sample rate; the
// pcm-player worklet keeps a short queue and asks for more over a MessagePort, and each
// request is answered by calling the .NET fill callback (WebAudio.Pull).
let bridge = null;
let context = null;
let contextRate = 0;
let node = null;
let ready = null;
let current = 0;          // id of the queue the worklet is playing
const queues = new Map(); // id -> { rate, buffered, paused }

export function connect(webAudio) { bridge = webAudio; }

// Browsers only allow audio after a user gesture; create/resume on the first one.
for (const type of ['pointerdown', 'keydown', 'touchend']) {
  addEventListener(type, () => { ensure(contextRate || 44100); context?.resume(); }, { capture: true, passive: true });
}

function ensure(rate) {
  if (context && contextRate === rate) return ready;
  const old = context;
  context = new AudioContext({ sampleRate: rate, latencyHint: 'playback' });
  contextRate = rate;
  node = null;
  old?.close();
  const ctx = context;
  ready = ctx.audioWorklet.addModule('pcm-worklet.js').then(() => {
    if (ctx !== context) return;
    node = new AudioWorkletNode(ctx, 'pcm-player', { numberOfInputs: 0, outputChannelCount: [2] });
    node.connect(ctx.destination);
    node.port.onmessage = (e) => onWorklet(e.data);
    const q = queues.get(current);
    if (q) node.port.postMessage({ play: current, buffered: q.buffered, paused: q.paused });
  });
  return ready;
}

// Last playback statistics from the worklet, for diagnostics and automated tests.
export const stats = { played: 0, starved: 0 };
globalThis.mdplayer4webAudio = stats;

function onWorklet(msg) {
  if (msg.stats) { Object.assign(stats, msg.stats); return; }
  if (msg.need && msg.id === current && bridge) {
    const bytes = bridge.Pull(msg.id, msg.need);
    if (bytes.length === 0) {
      node?.port.postMessage({ end: msg.id });
      return;
    }
    const pcm16 = new Int16Array(bytes.buffer, bytes.byteOffset, bytes.length >> 1);
    const pcm = new Float32Array(pcm16.length);
    for (let i = 0; i < pcm16.length; i++) pcm[i] = pcm16[i] / 32768;
    node?.port.postMessage({ pcm, id: msg.id }, [pcm.buffer]);
  }
}

export const imports = {
  open(id, rate, buffered) {
    queues.set(id, { rate, buffered, paused: true });
    current = id;
    node?.port.postMessage({ play: 0 });
    ensure(rate);
    node?.port.postMessage({ play: id, buffered, paused: true });
  },
  start(id) {
    const q = queues.get(id);
    if (!q) return;
    q.paused = false;
    context?.resume();
    if (id === current) node?.port.postMessage({ resume: id });
  },
  pause(id) {
    const q = queues.get(id);
    if (!q) return;
    q.paused = true;
    if (id === current) node?.port.postMessage({ pause: id });
  },
  stop(id) {
    queues.delete(id);
    if (id === current) {
      current = 0;
      node?.port.postMessage({ play: 0 });
    }
  },
};
