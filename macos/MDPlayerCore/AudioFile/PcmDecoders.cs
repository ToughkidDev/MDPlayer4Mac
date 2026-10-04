using System.IO;

namespace MDPlayer.AudioFile
{
    // Decoded audio stream used by AudioFileDriver. Samples are interleaved floats in
    // [-1, 1]; Channels is 1 or 2.
    public interface IPcmDecoder : IDisposable
    {
        int SampleRate { get; }
        int Channels { get; }
        // Length in frames at SampleRate, or -1 when the container does not say.
        long TotalFrames { get; }
        // Reads up to frames frames into dest (frames * Channels floats). Returns the
        // number of frames read; 0 means end of stream.
        int Read(float[] dest, int frames);
    }

    // Uncompressed RIFF/WAVE (8/16/24/32-bit integer PCM and 32/64-bit float, incl.
    // WAVE_FORMAT_EXTENSIBLE). Used on platforms without AudioToolbox; on macOS the
    // system decoder handles .wav as well.
    public sealed class WaveFileDecoder : IPcmDecoder
    {
        private readonly byte[] data;
        private readonly int dataStart;
        private readonly int dataLength;
        private readonly int sourceChannels;
        private readonly int bitsPerSample;
        private readonly bool isFloat;
        private readonly int blockAlign;
        private int position;

        public int SampleRate { get; }
        public int Channels { get; }
        public long TotalFrames => dataLength / blockAlign;

        private WaveFileDecoder(byte[] data, int dataStart, int dataLength, int channels, int sampleRate, int bits, bool isFloat)
        {
            this.data = data;
            this.dataStart = dataStart;
            this.dataLength = dataLength;
            sourceChannels = channels;
            bitsPerSample = bits;
            this.isFloat = isFloat;
            blockAlign = channels * (bits / 8);
            SampleRate = sampleRate;
            Channels = Math.Min(channels, 2);
        }

        public static WaveFileDecoder TryCreate(byte[] buf)
        {
            if (buf == null || buf.Length < 12) return null;
            if (buf[0] != 'R' || buf[1] != 'I' || buf[2] != 'F' || buf[3] != 'F') return null;
            if (buf[8] != 'W' || buf[9] != 'A' || buf[10] != 'V' || buf[11] != 'E') return null;

            int format = 0, channels = 0, rate = 0, bits = 0;
            int pos = 12;
            while (pos + 8 <= buf.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(buf, pos, 4);
                int size = BitConverter.ToInt32(buf, pos + 4);
                int body = pos + 8;
                if (size < 0) return null;
                if (id == "fmt " && size >= 16 && body + 16 <= buf.Length)
                {
                    format = BitConverter.ToUInt16(buf, body);
                    channels = BitConverter.ToUInt16(buf, body + 2);
                    rate = BitConverter.ToInt32(buf, body + 4);
                    bits = BitConverter.ToUInt16(buf, body + 14);
                    // WAVE_FORMAT_EXTENSIBLE: the real format is the first two bytes of
                    // the sub-format GUID.
                    if (format == 0xFFFE && size >= 40 && body + 26 <= buf.Length)
                        format = BitConverter.ToUInt16(buf, body + 24);
                }
                else if (id == "data")
                {
                    if (channels <= 0 || rate <= 0) return null;
                    bool isFloat = format == 3;
                    if (format != 1 && !isFloat) return null;
                    if (isFloat ? bits != 32 && bits != 64 : bits != 8 && bits != 16 && bits != 24 && bits != 32) return null;
                    int length = Math.Min(size, buf.Length - body);
                    return new WaveFileDecoder(buf, body, length, channels, rate, bits, isFloat);
                }
                pos = body + size + (size & 1);
            }
            return null;
        }

        public int Read(float[] dest, int frames)
        {
            int available = (dataLength - position) / blockAlign;
            int count = Math.Min(frames, available);
            for (int f = 0; f < count; f++)
            {
                int frameStart = dataStart + position + f * blockAlign;
                for (int c = 0; c < Channels; c++)
                    dest[f * Channels + c] = ReadSample(frameStart + c * (bitsPerSample / 8));
            }
            position += count * blockAlign;
            return count;
        }

        private float ReadSample(int i)
        {
            if (isFloat)
                return bitsPerSample == 32 ? BitConverter.ToSingle(data, i) : (float)BitConverter.ToDouble(data, i);
            return bitsPerSample switch
            {
                8 => (data[i] - 128) / 128f,
                16 => BitConverter.ToInt16(data, i) / 32768f,
                24 => ((data[i] | data[i + 1] << 8 | (sbyte)data[i + 2] << 16)) / 8388608f,
                _ => BitConverter.ToInt32(data, i) / 2147483648f,
            };
        }

        public void Dispose() { }
    }

    // Ogg Vorbis through NVorbis (MIT). macOS's AudioToolbox does not decode Vorbis in an
    // Ogg container, and the Windows build relies on NAudio.Vorbis for the same reason.
    public sealed class VorbisDecoder : IPcmDecoder
    {
        private readonly NVorbis.VorbisReader reader;
        private readonly int sourceChannels;
        private float[] scratch = Array.Empty<float>();

        public int SampleRate => reader.SampleRate;
        public int Channels { get; }
        public long TotalFrames => reader.TotalSamples > 0 ? reader.TotalSamples : -1;
        public string Title => reader.Tags?.Title;
        public string Artist => reader.Tags?.Artist;

        private VorbisDecoder(NVorbis.VorbisReader reader)
        {
            this.reader = reader;
            sourceChannels = reader.Channels;
            Channels = Math.Min(sourceChannels, 2);
        }

        public static VorbisDecoder TryCreate(byte[] buf)
        {
            if (buf == null || buf.Length < 4 || buf[0] != 'O' || buf[1] != 'g' || buf[2] != 'g' || buf[3] != 'S') return null;
            try
            {
                return new VorbisDecoder(new NVorbis.VorbisReader(new MemoryStream(buf, writable: false), closeOnDispose: true));
            }
            catch (Exception ex)
            {
                log.Write(LogLevel.Information, "Vorbis decode failed: {0}", ex.Message);
                return null;
            }
        }

        public int Read(float[] dest, int frames)
        {
            if (sourceChannels == Channels) return reader.ReadSamples(dest, 0, frames * Channels) / Channels;

            // More than two channels: keep the front left/right pair.
            int needed = frames * sourceChannels;
            if (scratch.Length < needed) scratch = new float[needed];
            int got = reader.ReadSamples(scratch, 0, needed) / sourceChannels;
            for (int f = 0; f < got; f++)
            {
                dest[f * 2] = scratch[f * sourceChannels];
                dest[f * 2 + 1] = scratch[f * sourceChannels + 1];
            }
            return got;
        }

        public void Dispose() => reader.Dispose();
    }

    // Linear-interpolation sample-rate converter for the managed decoders above (the
    // AudioToolbox decoder converts to the output rate itself).
    public sealed class ResamplingDecoder : IPcmDecoder
    {
        private readonly IPcmDecoder source;
        private readonly double step;
        private float[] input = Array.Empty<float>();
        private int inputFrames;
        private double phase;
        private bool sourceEnded;

        public int SampleRate { get; }
        public int Channels => source.Channels;
        public long TotalFrames => source.TotalFrames < 0 ? -1 : (long)(source.TotalFrames * (double)SampleRate / source.SampleRate);

        public ResamplingDecoder(IPcmDecoder source, int outputRate)
        {
            this.source = source;
            SampleRate = outputRate;
            step = (double)source.SampleRate / outputRate;
        }

        public int Read(float[] dest, int frames)
        {
            int ch = Channels;
            int written = 0;
            while (written < frames)
            {
                // Need input frames idx and idx+1 for interpolation.
                int idx = (int)phase;
                if (idx + 1 >= inputFrames)
                {
                    if (!Refill()) break;
                    continue;
                }
                float t = (float)(phase - idx);
                for (int c = 0; c < ch; c++)
                {
                    float a = input[idx * ch + c];
                    float b = input[(idx + 1) * ch + c];
                    dest[written * ch + c] = a + (b - a) * t;
                }
                written++;
                phase += step;
            }
            return written;
        }

        // Keeps the last frame of the previous block so interpolation spans block edges.
        private bool Refill()
        {
            if (sourceEnded) return false;
            int ch = Channels;
            int keep = inputFrames > 0 ? 1 : 0;
            const int block = 4096;
            float[] next = new float[(keep + block) * ch];
            if (keep == 1) Array.Copy(input, (inputFrames - 1) * ch, next, 0, ch);
            float[] tmp = new float[block * ch];
            int got = source.Read(tmp, block);
            if (got <= 0)
            {
                sourceEnded = true;
                return false;
            }
            Array.Copy(tmp, 0, next, keep * ch, got * ch);
            phase -= inputFrames > 0 ? inputFrames - 1 : 0;
            input = next;
            inputFrames = keep + got;
            return true;
        }

        public void Dispose() => source.Dispose();
    }
}
