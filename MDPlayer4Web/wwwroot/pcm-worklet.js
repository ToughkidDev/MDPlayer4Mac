// Plays the interleaved stereo PCM that audio.js supplies, keeping `buffered` frames
// queued and requesting more in CHUNK-sized pieces as it drains.
const CHUNK = 2048;

class PcmPlayer extends AudioWorkletProcessor {
  constructor() {
    super();
    this.reset(0, 0, true);
    this.port.onmessage = (e) => {
      const m = e.data;
      if (m.play !== undefined) this.reset(m.play, m.buffered ?? 0, m.paused ?? true);
      else if (m.pause !== undefined && m.pause === this.id) this.paused = true;
      else if (m.resume !== undefined && m.resume === this.id) this.paused = false;
      else if (m.pcm && m.id === this.id) {
        this.chunks.push(m.pcm);
        this.queued += m.pcm.length / 2;
        this.pending = Math.max(0, this.pending - CHUNK);
      } else if (m.end !== undefined && m.end === this.id) {
        this.ended = true;
        this.pending = 0;
      }
    };
  }

  reset(id, buffered, paused) {
    this.id = id;
    this.target = Math.max(buffered, CHUNK * 2);
    this.paused = paused;
    this.chunks = [];
    this.offset = 0;
    this.queued = 0;
    this.pending = 0;
    this.ended = false;
    this.played = 0;
    this.starved = 0;
    this.sinceReport = 0;
  }

  process(_inputs, outputs) {
    const left = outputs[0][0];
    const right = outputs[0][1] ?? left;
    let i = 0;
    if (!this.paused) {
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
    }
    const playing = this.id !== 0 && !this.paused;
    if (playing) {
      this.played += i;
      if (!this.ended) this.starved += left.length - i;
    }
    for (; i < left.length; i++) { left[i] = 0; right[i] = 0; }
    // Playback statistics (frames played / frames of silence while waiting for data).
    if (playing && (this.sinceReport += left.length) >= sampleRate) {
      this.sinceReport = 0;
      this.port.postMessage({ stats: { played: this.played, starved: this.starved } });
    }
    if (this.id !== 0 && !this.ended) {
      while (this.queued + this.pending < this.target) {
        this.port.postMessage({ need: CHUNK, id: this.id });
        this.pending += CHUNK;
      }
    }
    return true;
  }
}

registerProcessor('pcm-player', PcmPlayer);
