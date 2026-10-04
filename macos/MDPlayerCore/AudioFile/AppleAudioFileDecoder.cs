using System.IO;
using System.Runtime.InteropServices;

namespace MDPlayer.AudioFile
{
    // macOS system decoder (AudioToolbox ExtAudioFile) for WAV, AIFF, MP3, AAC/M4A, FLAC
    // and CAF. The Windows build uses NAudio + Media Foundation for the same files.
    // ExtAudioFile converts to the requested client format, including the sample rate,
    // so the output is already at the session's playback rate.
    public sealed class AppleAudioFileDecoder : IPcmDecoder
    {
        private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        private const uint kExtAudioFileProperty_FileDataFormat = 0x66666d74;   // 'ffmt'
        private const uint kExtAudioFileProperty_ClientDataFormat = 0x63666d74; // 'cfmt'
        private const uint kExtAudioFileProperty_FileLengthFrames = 0x2366726d; // '#frm'
        private const uint kAudioFormatLinearPCM = 0x6c70636d;                  // 'lpcm'
        private const uint kAudioFormatFlagIsFloat = 1;
        private const uint kAudioFormatFlagIsPacked = 8;

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioStreamBasicDescription
        {
            public double SampleRate;
            public uint FormatID;
            public uint FormatFlags;
            public uint BytesPerPacket;
            public uint FramesPerPacket;
            public uint BytesPerFrame;
            public uint ChannelsPerFrame;
            public uint BitsPerChannel;
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioBuffer
        {
            public uint NumberChannels;
            public uint DataByteSize;
            public IntPtr Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioBufferList
        {
            public uint NumberBuffers;
            public AudioBuffer Buffer0;
        }

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFURLCreateFromFileSystemRepresentation(IntPtr allocator, byte[] buffer, nint bufLen, [MarshalAs(UnmanagedType.I1)] bool isDirectory);

        [DllImport(CoreFoundation)]
        private static extern void CFRelease(IntPtr cf);

        [DllImport(AudioToolbox)]
        private static extern int ExtAudioFileOpenURL(IntPtr url, out IntPtr extAudioFile);

        [DllImport(AudioToolbox)]
        private static extern int ExtAudioFileDispose(IntPtr extAudioFile);

        [DllImport(AudioToolbox)]
        private static extern int ExtAudioFileGetProperty(IntPtr extAudioFile, uint propertyID, ref uint size, out AudioStreamBasicDescription data);

        [DllImport(AudioToolbox)]
        private static extern int ExtAudioFileGetProperty(IntPtr extAudioFile, uint propertyID, ref uint size, out long data);

        [DllImport(AudioToolbox)]
        private static extern int ExtAudioFileSetProperty(IntPtr extAudioFile, uint propertyID, uint size, ref AudioStreamBasicDescription data);

        [DllImport(AudioToolbox)]
        private static extern int ExtAudioFileRead(IntPtr extAudioFile, ref uint frames, ref AudioBufferList data);

        private IntPtr file;
        private readonly string tempPath;

        public int SampleRate { get; }
        public int Channels { get; }
        public long TotalFrames { get; }

        private AppleAudioFileDecoder(IntPtr file, int sampleRate, int channels, long totalFrames, string tempPath)
        {
            this.file = file;
            SampleRate = sampleRate;
            Channels = channels;
            TotalFrames = totalFrames;
            this.tempPath = tempPath;
        }

        // path should be the original file; when the caller only has bytes they are written
        // to a temporary file first (ExtAudioFile needs a URL).
        public static AppleAudioFileDecoder TryCreate(string path, byte[] buf, int outputRate)
        {
            if (!OperatingSystem.IsMacOS()) return null;

            string tempPath = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                if (buf == null) return null;
                tempPath = Path.Combine(Path.GetTempPath(), "mdplayer4mac-" + Guid.NewGuid().ToString("N") + Path.GetExtension(path ?? ""));
                File.WriteAllBytes(tempPath, buf);
                path = tempPath;
            }

            IntPtr file = IntPtr.Zero;
            try
            {
                byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(path);
                IntPtr url = CFURLCreateFromFileSystemRepresentation(IntPtr.Zero, utf8, utf8.Length, false);
                if (url == IntPtr.Zero) return Fail(tempPath);
                int status = ExtAudioFileOpenURL(url, out file);
                CFRelease(url);
                if (status != 0 || file == IntPtr.Zero)
                {
                    log.Write(LogLevel.Information, "ExtAudioFileOpenURL failed ({0}) for {1}", status, path);
                    return Fail(tempPath);
                }

                uint size = (uint)Marshal.SizeOf<AudioStreamBasicDescription>();
                if (ExtAudioFileGetProperty(file, kExtAudioFileProperty_FileDataFormat, ref size, out AudioStreamBasicDescription fileFormat) != 0
                    || fileFormat.ChannelsPerFrame == 0 || fileFormat.SampleRate <= 0)
                {
                    ExtAudioFileDispose(file);
                    return Fail(tempPath);
                }

                int channels = (int)Math.Min(fileFormat.ChannelsPerFrame, 2u);
                var client = new AudioStreamBasicDescription
                {
                    SampleRate = outputRate,
                    FormatID = kAudioFormatLinearPCM,
                    FormatFlags = kAudioFormatFlagIsFloat | kAudioFormatFlagIsPacked,
                    BitsPerChannel = 32,
                    ChannelsPerFrame = (uint)channels,
                    FramesPerPacket = 1,
                    BytesPerFrame = (uint)(4 * channels),
                    BytesPerPacket = (uint)(4 * channels),
                };
                if (ExtAudioFileSetProperty(file, kExtAudioFileProperty_ClientDataFormat, size, ref client) != 0)
                {
                    ExtAudioFileDispose(file);
                    return Fail(tempPath);
                }

                long totalFrames = -1;
                uint longSize = sizeof(long);
                if (ExtAudioFileGetProperty(file, kExtAudioFileProperty_FileLengthFrames, ref longSize, out long fileFrames) == 0 && fileFrames > 0)
                    totalFrames = (long)(fileFrames * (double)outputRate / fileFormat.SampleRate);

                return new AppleAudioFileDecoder(file, outputRate, channels, totalFrames, tempPath);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                log.ForcedWrite(ex);
                return Fail(tempPath);
            }
        }

        private static AppleAudioFileDecoder Fail(string tempPath)
        {
            DeleteTemp(tempPath);
            return null;
        }

        public unsafe int Read(float[] dest, int frames)
        {
            if (file == IntPtr.Zero || frames <= 0) return 0;
            fixed (float* p = dest)
            {
                var list = new AudioBufferList
                {
                    NumberBuffers = 1,
                    Buffer0 = new AudioBuffer
                    {
                        NumberChannels = (uint)Channels,
                        DataByteSize = (uint)(frames * Channels * sizeof(float)),
                        Data = (IntPtr)p,
                    },
                };
                uint count = (uint)frames;
                int status = ExtAudioFileRead(file, ref count, ref list);
                if (status != 0)
                {
                    log.Write(LogLevel.Information, "ExtAudioFileRead failed ({0})", status);
                    return 0;
                }
                return (int)count;
            }
        }

        public void Dispose()
        {
            if (file != IntPtr.Zero)
            {
                ExtAudioFileDispose(file);
                file = IntPtr.Zero;
            }
            DeleteTemp(tempPath);
        }

        private static void DeleteTemp(string path)
        {
            if (path == null) return;
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
