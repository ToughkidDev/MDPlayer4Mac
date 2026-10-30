// Plays interleaved stereo PCM that the engine worker renders on request. Keeps about
// TARGET seconds queued and asks for more whenever the queue runs low.

const TARGET = 0.35;   // seconds kept queued
const CHUNK = 4096;    // frames per request

class PcmPlayer extends AudioWorkletProcessor {
  constructor() {
    super();
    this.chunks = [];
    this.offset = 0;       // frame offset into chunks[0]
    this.queued = 0;       // frames queued
    this.pending = 0;      // frames requested but not received
    this.gen = 0;
    this.ending = false;
    this.workerPort = null;
    this.port.onmessage = (e) => {
      const msg = e.data;
      if (msg.workerPort) {
        this.workerPort = msg.workerPort;
        this.workerPort.onmessage = (w) => this.onWorker(w.data);
      } else if (msg.reset !== undefined) {
        // New song (or stop): drop queued audio from the previous generation.
        this.chunks = []; this.offset = 0; this.queued = 0; this.pending = 0;
        this.gen = msg.reset; this.ending = false;
      }
    };
  }

  onWorker(msg) {
    if (msg.gen !== this.gen) return;
    if (msg.pcm) {
      const frames = msg.pcm.length / 2;
      this.chunks.push(msg.pcm);
      this.queued += frames;
      this.pending = Math.max(0, this.pending - CHUNK);
    } else if (msg.end) {
      this.pending = 0;
      this.ending = true;
    }
  }

  request() {
    if (!this.workerPort || this.ending || this.gen === 0) return;
    const target = TARGET * sampleRate;
    while (this.queued + this.pending < target) {
      this.workerPort.postMessage({ need: CHUNK, gen: this.gen });
      this.pending += CHUNK;
    }
  }

  process(_inputs, outputs) {
    const left = outputs[0][0];
    const right = outputs[0][1] ?? left;
    let i = 0;
    while (i < left.length && this.chunks.length > 0) {
      const chunk = this.chunks[0];
      const frames = chunk.length / 2;
      const n = Math.min(left.length - i, frames - this.offset);
      for (let k = 0; k < n; k++) {
        left[i + k] = chunk[(this.offset + k) * 2];
        right[i + k] = chunk[(this.offset + k) * 2 + 1];
      }
      i += n; this.offset += n; this.queued -= n;
      if (this.offset >= frames) { this.chunks.shift(); this.offset = 0; }
    }
    for (; i < left.length; i++) { left[i] = 0; right[i] = 0; }
    if (this.ending && this.queued <= 0 && this.chunks.length === 0) {
      this.ending = false;
      this.port.postMessage({ drained: this.gen });
      this.gen = 0;
    }
    this.request();
    return true;
  }
}

registerProcessor('pcm-player', PcmPlayer);
