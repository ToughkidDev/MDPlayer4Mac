// P/Invoke wrapper over macOS's Audio Queue Services (AudioToolbox.framework) for real-time
// PCM playback. This is the "old but simple" Core Audio streaming API - built for exactly
// this pull/refill-callback shape (as opposed to AUHAL/AudioUnit render callbacks, which are
// lower-latency but a lot more native-side ceremony to set up correctly). Chosen because it
// requires zero extra install (ships with every macOS system) and its buffer-refill model
// maps directly onto MDSound.MDSound.Update's own pull-based signature - see
// macos/LivePlayer/Program.cs for how the two are wired together.
//
// Struct/function signatures below are transcribed from Apple's AudioToolbox/AudioQueue.h
// and CoreAudioTypes.h (a long-stable, unchanged-in-practice C API). Field order and types
// in AudioStreamBasicDescription/AudioQueueBuffer must exactly match the native layout -
// .NET's [StructLayout(LayoutKind.Sequential)] then computes the same platform-native
// padding/alignment the C compiler would, so Marshal.OffsetOf below gives correct offsets
// without this code needing to hand-compute padding itself.
using System.Runtime.InteropServices;

namespace MDPlayer.CoreAudioOutput
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AudioStreamBasicDescription
    {
        public double mSampleRate;
        public uint mFormatID;
        public uint mFormatFlags;
        public uint mBytesPerPacket;
        public uint mFramesPerPacket;
        public uint mBytesPerFrame;
        public uint mChannelsPerFrame;
        public uint mBitsPerChannel;
        public uint mReserved;
    }

    // Mirrors AudioQueueBuffer from AudioQueue.h. mAudioDataBytesCapacity/mAudioData/
    // mPacketDescriptions/mPacketDescriptionCapacity are declared `const` in the C header
    // (Apple owns and sizes the buffer), but they're still just memory we're allowed to
    // read - we only ever write mAudioDataByteSize (and copy PCM into *mAudioData).
    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioQueueBuffer
    {
        public uint mAudioDataBytesCapacity;
        public IntPtr mAudioData;
        public uint mAudioDataByteSize;
        public IntPtr mUserData;
        public uint mPacketDescriptionCapacity;
        public IntPtr mPacketDescriptions;
        public uint mPacketDescriptionCount;
    }

    internal static class AudioToolbox
    {
        private const string Lib = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void AudioQueueOutputCallback(IntPtr inUserData, IntPtr inAQ, IntPtr inBuffer);

        [DllImport(Lib)]
        internal static extern int AudioQueueNewOutput(
            ref AudioStreamBasicDescription inFormat,
            AudioQueueOutputCallback inCallbackProc,
            IntPtr inUserData,
            IntPtr inCallbackRunLoop,
            IntPtr inCallbackRunLoopMode,
            uint inFlags,
            out IntPtr outAQ);

        [DllImport(Lib)]
        internal static extern int AudioQueueAllocateBuffer(IntPtr inAQ, uint inBufferByteSize, out IntPtr outBuffer);

        [DllImport(Lib)]
        internal static extern int AudioQueueEnqueueBuffer(IntPtr inAQ, IntPtr inBuffer, uint inNumPacketDescs, IntPtr inPacketDescs);

        [DllImport(Lib)]
        internal static extern int AudioQueueStart(IntPtr inAQ, IntPtr inStartTime);

        [DllImport(Lib)]
        internal static extern int AudioQueuePause(IntPtr inAQ);

        [DllImport(Lib)]
        internal static extern int AudioQueueStop(IntPtr inAQ, byte inImmediate);

        [DllImport(Lib)]
        internal static extern int AudioQueueDispose(IntPtr inAQ, byte inImmediate);
    }

    /// <summary>
    /// Real-time 16-bit signed, interleaved-stereo PCM output via Audio Queue Services.
    /// Construction primes <paramref name="bufferCount"/> buffers synchronously (calling
    /// <c>fillCallback</c> directly); after that, refills happen on CoreAudio's own internal
    /// callback thread. <c>fillCallback(scratch, sampleCount)</c> should behave like
    /// <c>MDSound.MDSound.Update</c>: write up to <c>sampleCount</c> interleaved shorts into
    /// <c>scratch</c> starting at index 0 and return how many were actually written; return
    /// &lt;= 0 to signal "no more audio" (the queue then drains its remaining buffers and
    /// stops feeding new ones - <see cref="Finished"/> flips true immediately, but audio
    /// already enqueued keeps playing for a bit).
    /// </summary>
    public class CoreAudioQueue : IDisposable
    {
        public const uint FormatLinearPCM = 0x6c70636d; // 'lpcm'
        private const uint FlagIsSignedInteger = 0x4;
        private const uint FlagIsPacked = 0x8;

        private static readonly int OffsetAudioData =
            (int)Marshal.OffsetOf<AudioQueueBuffer>(nameof(AudioQueueBuffer.mAudioData));
        private static readonly int OffsetAudioDataByteSize =
            (int)Marshal.OffsetOf<AudioQueueBuffer>(nameof(AudioQueueBuffer.mAudioDataByteSize));

        private readonly Func<short[], int, int> fillCallback;
        private readonly AudioToolbox.AudioQueueOutputCallback nativeCallback; // keep alive - GC must not collect this
        private readonly int framesPerBuffer;
        private readonly short[] scratch;
        private IntPtr queue = IntPtr.Zero;
        private volatile bool stopped;

        /// <summary>True once fillCallback has signaled "no more audio" - buffers already
        /// enqueued at that point are still draining, so stop playback shortly after this
        /// flips rather than immediately.</summary>
        public bool Finished { get; private set; }

        public CoreAudioQueue(uint sampleRate, int framesPerBuffer, int bufferCount, Func<short[], int, int> fillCallback)
        {
            this.fillCallback = fillCallback;
            this.framesPerBuffer = framesPerBuffer;
            scratch = new short[framesPerBuffer * 2]; // stereo

            var format = new AudioStreamBasicDescription
            {
                mSampleRate = sampleRate,
                mFormatID = FormatLinearPCM,
                mFormatFlags = FlagIsSignedInteger | FlagIsPacked,
                mBytesPerPacket = 4,
                mFramesPerPacket = 1,
                mBytesPerFrame = 4,
                mChannelsPerFrame = 2,
                mBitsPerChannel = 16,
                mReserved = 0,
            };

            nativeCallback = OnBufferNeeded;

            int status = AudioToolbox.AudioQueueNewOutput(ref format, nativeCallback, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out queue);
            if (status != 0)
                throw new InvalidOperationException($"AudioQueueNewOutput failed: OSStatus {status}");

            uint bufferByteSize = (uint)(framesPerBuffer * 4);
            for (int i = 0; i < bufferCount; i++)
            {
                status = AudioToolbox.AudioQueueAllocateBuffer(queue, bufferByteSize, out IntPtr buf);
                if (status != 0)
                    throw new InvalidOperationException($"AudioQueueAllocateBuffer failed: OSStatus {status}");
                FillAndEnqueue(buf);
            }
        }

        private void FillAndEnqueue(IntPtr bufferPtr)
        {
            if (stopped) return;

            int filled = fillCallback(scratch, framesPerBuffer * 2);

            // Stop() may have been called (from another thread) while fillCallback was
            // running above - re-check before touching the buffer/queue so a callback that
            // was mid-flight when Stop() started doesn't race the native teardown.
            if (stopped) return;

            if (filled <= 0)
            {
                Finished = true;
                return; // leave this buffer un-enqueued; the queue drains what's already in flight
            }

            IntPtr audioDataPtr = Marshal.ReadIntPtr(bufferPtr, OffsetAudioData);
            Marshal.Copy(scratch, 0, audioDataPtr, filled);
            Marshal.WriteInt32(bufferPtr, OffsetAudioDataByteSize, filled * sizeof(short));

            int status = AudioToolbox.AudioQueueEnqueueBuffer(queue, bufferPtr, 0, IntPtr.Zero);
            if (status != 0)
                throw new InvalidOperationException($"AudioQueueEnqueueBuffer failed: OSStatus {status}");
        }

        // Invoked directly by CoreAudio's native Audio Queue runtime (a reverse P/Invoke
        // callback on its own internal "AQClient" thread, not a normal .NET thread) every
        // time a buffer drains and needs refilling. An exception escaping a native callback
        // like this is fatal to the whole process - CoreCLR has nowhere safe to unwind it to,
        // so it aborts (SIGABRT) rather than let undefined behavior happen on the native side.
        // This was hit in practice: fillCallback/AudioQueueEnqueueBuffer can race Stop()
        // (called from the UI thread while a refill is mid-flight - see StopPlayback() in
        // MainWindow.axaml.cs, which sets stopRequested/calls Stop() without any lock against
        // this callback), so AudioQueueEnqueueBuffer occasionally fails with a non-zero
        // OSStatus once the queue is concurrently stopping, which used to throw here and take
        // the whole app down. Swallow everything instead - the worst outcome of a failed
        // refill is one dropped/skipped buffer, never a crash.
        private void OnBufferNeeded(IntPtr inUserData, IntPtr inAQ, IntPtr inBuffer)
        {
            try
            {
                FillAndEnqueue(inBuffer);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CoreAudioQueue] buffer refill failed, dropping this buffer: {ex}");
                Finished = true;
            }
        }

        public void Start()
        {
            int status = AudioToolbox.AudioQueueStart(queue, IntPtr.Zero);
            if (status != 0)
                throw new InvalidOperationException($"AudioQueueStart failed: OSStatus {status}");
        }

        public void Pause()
        {
            if (queue == IntPtr.Zero || stopped) return;
            int status = AudioToolbox.AudioQueuePause(queue);
            if (status != 0)
                throw new InvalidOperationException($"AudioQueuePause failed: OSStatus {status}");
        }

        public void Stop(bool immediate = true)
        {
            if (queue == IntPtr.Zero) return;
            stopped = true;
            AudioToolbox.AudioQueueStop(queue, (byte)(immediate ? 1 : 0));
        }

        public void Dispose()
        {
            if (queue != IntPtr.Zero)
            {
                AudioToolbox.AudioQueueDispose(queue, 1);
                queue = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }
    }
}
