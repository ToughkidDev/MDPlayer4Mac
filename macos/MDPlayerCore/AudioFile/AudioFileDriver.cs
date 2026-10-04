using System.IO;

namespace MDPlayer.AudioFile
{
    // Plays an ordinary audio file (WAV/AIFF/MP3/M4A/AAC/FLAC/OGG) through the same
    // MusicEngineSession surface as the chip drivers. There is no chip and no MDSound
    // graph: Render() writes the decoded PCM directly, like the SID/NSF renderers.
    public sealed class AudioFileDriver : baseDriver, IDisposable
    {
        private readonly IPcmDecoder decoder;
        private readonly int outputRate;
        private float[] scratch = Array.Empty<float>();
        private double counterRemainder;

        public AudioFileDriver(Setting setting, IPcmDecoder decoder, int outputRate, string title, string artist)
        {
            this.setting = setting;
            this.decoder = decoder;
            this.outputRate = outputRate;
            if (decoder.TotalFrames > 0)
                TotalCounter = (long)(decoder.TotalFrames * (double)Common.VGMProcSampleRate / decoder.SampleRate);
            GD3.TrackName = GD3.TrackNameJ = title ?? string.Empty;
            GD3.Composer = GD3.ComposerJ = artist ?? string.Empty;
            UsedChips = "PCM";
        }

        // Fills count shorts (interleaved stereo) and returns count. After the decoder runs
        // out, the rest of the buffer is silence and Stopped is set, so the caller's next
        // request ends playback without cutting off the samples delivered here.
        public int Render(short[] buffer, int offset, int count)
        {
            int frames = count / 2;
            int ch = decoder.Channels;
            if (scratch.Length < frames * ch) scratch = new float[frames * ch];

            int got = 0;
            while (got < frames)
            {
                int n = ReadInto(got, frames - got);
                if (n <= 0) break;
                got += n;
            }

            for (int f = 0; f < got; f++)
            {
                float l = scratch[f * ch];
                float r = ch > 1 ? scratch[f * ch + 1] : l;
                buffer[offset + f * 2] = ToShort(l);
                buffer[offset + f * 2 + 1] = ToShort(r);
            }
            Array.Clear(buffer, offset + got * 2, count - got * 2);

            double advance = got * (double)Common.VGMProcSampleRate / outputRate + counterRemainder;
            long whole = (long)advance;
            counterRemainder = advance - whole;
            Counter += whole;
            vgmFrameCounter = Counter;

            if (got < frames) Stopped = true;
            return count;
        }

        // Decoders write from index 0, so read into a temporary block for later chunks.
        private float[] tail = Array.Empty<float>();
        private int ReadInto(int frameOffset, int frames)
        {
            int ch = decoder.Channels;
            if (frameOffset == 0) return decoder.Read(scratch, frames);
            if (tail.Length < frames * ch) tail = new float[frames * ch];
            int n = decoder.Read(tail, frames);
            if (n > 0) Array.Copy(tail, 0, scratch, frameOffset * ch, n * ch);
            return n;
        }

        private static short ToShort(float v)
        {
            int s = (int)MathF.Round(v * 32767f);
            return (short)Math.Clamp(s, short.MinValue, short.MaxValue);
        }

        public override bool init(byte[] vgmBuf, ChipRegister chipRegister, EnmModel model, EnmChip[] useChip, uint latency, uint waitTime) => true;
        public override bool init(byte[] vgmBuf, int fileType, ChipRegister chipRegister, EnmModel model, EnmChip[] useChip, uint latency, uint waitTime) => true;
        public override void oneFrameProc() { }
        public override GD3 getGD3Info(byte[] buf, uint vgmGd3) => GD3;

        public void Dispose() => decoder.Dispose();

        // Opens buf (the file contents; sourcePath is the original file when known).
        public static IPcmDecoder OpenDecoder(byte[] buf, string sourcePath, int outputRate, out string title, out string artist)
        {
            title = string.IsNullOrEmpty(sourcePath) ? null : Path.GetFileNameWithoutExtension(sourcePath);
            artist = null;

            VorbisDecoder vorbis = VorbisDecoder.TryCreate(buf);
            if (vorbis != null)
            {
                if (!string.IsNullOrWhiteSpace(vorbis.Title)) title = vorbis.Title;
                artist = vorbis.Artist;
                return Resample(vorbis, outputRate);
            }

            IPcmDecoder apple = AppleAudioFileDecoder.TryCreate(sourcePath, buf, outputRate);
            if (apple != null) return apple;

            WaveFileDecoder wave = WaveFileDecoder.TryCreate(buf);
            return wave != null ? Resample(wave, outputRate) : null;
        }

        private static IPcmDecoder Resample(IPcmDecoder decoder, int outputRate)
            => decoder.SampleRate == outputRate ? decoder : new ResamplingDecoder(decoder, outputRate);
    }
}
