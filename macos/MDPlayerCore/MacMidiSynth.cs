using System;
using System.Runtime.InteropServices;

namespace MDPlayer
{
    // Small managed host for macOS's system DLS Music Device. The unit is a General MIDI
    // synthesizer supplied by macOS; keeping it here lets MID/RCP/RCS reuse their original
    // byte-stream sequencers while rendering into the same PCM queue as every chip format.
    // It intentionally has no UI/AppKit dependency, so EngineSmokeTest can exercise it too.
    internal sealed class MacMidiSynth : IDisposable
    {
        private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
        private const uint AudioUnitTypeMusicDevice = 0x61756d75; // 'aumu'
        private const uint AudioUnitSubTypeDlsSynth = 0x646c7320; // 'dls '
        private const uint AudioUnitManufacturerApple = 0x6170706c; // 'appl'
        private const uint AudioUnitScopeOutput = 2;
        private const uint AudioUnitScopeGlobal = 0;
        private const uint AudioUnitPropertyMaximumFramesPerSlice = 14;
        private const uint FormatLinearPcm = 0x6c70636d; // 'lpcm'
        private const uint FormatFlagIsFloat = 0x1;
        private const uint FormatFlagIsNonInterleaved = 0x20;

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioComponentDescription
        {
            public uint componentType;
            public uint componentSubType;
            public uint componentManufacturer;
            public uint componentFlags;
            public uint componentFlagsMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioStreamBasicDescription
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

        [StructLayout(LayoutKind.Sequential)]
        private struct SMPTETime
        {
            public short mSubframes;
            public short mSubframeDivisor;
            public uint mCounter;
            public uint mType;
            public uint mFlags;
            public short mHours;
            public byte mMinutes;
            public byte mSeconds;
            public byte mFrames;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioTimeStamp
        {
            public double mSampleTime;
            public ulong mHostTime;
            public double mRateScalar;
            public ulong mWordClockTime;
            public SMPTETime mSMPTETime;
            public uint mFlags;
            public uint mReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioBuffer
        {
            public uint mNumberChannels;
            public uint mDataByteSize;
            public IntPtr mData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioBufferListTwo
        {
            public uint mNumberBuffers;
            public AudioBuffer mBuffer1;
            public AudioBuffer mBuffer2;
        }

        [DllImport(AudioToolbox)]
        private static extern IntPtr AudioComponentFindNext(IntPtr component, ref AudioComponentDescription description);

        [DllImport(AudioToolbox)]
        private static extern int AudioComponentInstanceNew(IntPtr component, out IntPtr instance);

        [DllImport(AudioToolbox)]
        private static extern int AudioComponentInstanceDispose(IntPtr instance);

        [DllImport(AudioToolbox)]
        private static extern int AudioUnitGetProperty(IntPtr unit, uint propertyId, uint scope, uint element,
            ref AudioStreamBasicDescription data, ref uint dataSize);

        [DllImport(AudioToolbox)]
        private static extern int AudioUnitSetProperty(IntPtr unit, uint propertyId, uint scope, uint element,
            ref uint data, uint dataSize);

        [DllImport(AudioToolbox)]
        private static extern int AudioUnitInitialize(IntPtr unit);

        [DllImport(AudioToolbox)]
        private static extern int AudioUnitUninitialize(IntPtr unit);

        [DllImport(AudioToolbox)]
        private static extern int AudioUnitRender(IntPtr unit, ref uint actionFlags, ref AudioTimeStamp timeStamp,
            uint outputBusNumber, uint numberFrames, IntPtr outputData);

        [DllImport(AudioToolbox)]
        private static extern int MusicDeviceMIDIEvent(IntPtr unit, uint status, uint data1, uint data2,
            uint offsetSampleFrame);

        [DllImport(AudioToolbox)]
        private static extern int MusicDeviceSysEx(IntPtr unit, byte[] data, uint length);

        private IntPtr unit;
        private readonly bool nonInterleaved;
        private readonly AudioStreamBasicDescription outputFormat;
        private int frameOffset;
        private long renderFramePosition;
        private bool disposed;
        private float[] leftRenderBuffer = Array.Empty<float>();
        private float[] rightRenderBuffer = Array.Empty<float>();

        public MacMidiSynth(uint sampleRate)
        {
            AudioComponentDescription description = new()
            {
                componentType = AudioUnitTypeMusicDevice,
                componentSubType = AudioUnitSubTypeDlsSynth,
                componentManufacturer = AudioUnitManufacturerApple,
            };
            IntPtr component = AudioComponentFindNext(IntPtr.Zero, ref description);
            if (component == IntPtr.Zero)
                throw new InvalidOperationException("macOS DLS General MIDI synthesizer를 찾을 수 없습니다.");
            ThrowIfError(AudioComponentInstanceNew(component, out unit), "AudioComponentInstanceNew");

            // DLS exposes its native output as non-interleaved Float32 PCM.  Recent macOS
            // versions reject a host attempt to replace that stream description, so query
            // and use its canonical form instead of forcing signed 16-bit PCM.
            AudioStreamBasicDescription format = new();
            uint formatSize = (uint)Marshal.SizeOf<AudioStreamBasicDescription>();
            ThrowIfError(AudioUnitGetProperty(unit, 8, AudioUnitScopeOutput, 0, ref format, ref formatSize),
                "AudioUnitGetProperty(StreamFormat)");
            if (format.mFormatID != FormatLinearPcm || format.mBitsPerChannel != 32
                || (format.mFormatFlags & FormatFlagIsFloat) == 0 || format.mChannelsPerFrame != 2)
            {
                throw new InvalidOperationException("macOS DLS synthesizer returned an unsupported output format.");
            }
            outputFormat = format;
            nonInterleaved = (format.mFormatFlags & FormatFlagIsNonInterleaved) != 0;
            // MDSound usually asks us for 2,048 stereo frames at once. DLS's conservative
            // default slice limit is lower, so raise it before initialization.
            uint maximumFrames = 4096;
            ThrowIfError(AudioUnitSetProperty(unit, AudioUnitPropertyMaximumFramesPerSlice, AudioUnitScopeGlobal, 0,
                ref maximumFrames, sizeof(uint)), "AudioUnitSetProperty(MaximumFramesPerSlice)");
            ThrowIfError(AudioUnitInitialize(unit), "AudioUnitInitialize(DLS synth)");
        }

        public void SetFrameOffset(int value) => frameOffset = Math.Max(0, value);

        public void Send(byte[] data)
        {
            if (disposed || data == null || data.Length == 0) return;
            // DLS receives complete system-exclusive byte streams through its dedicated API.
            if (data[0] == 0xf0 || data[0] == 0xf7)
            {
                MusicDeviceSysEx(unit, data, (uint)data.Length);
                return;
            }

            uint status = data[0];
            uint data1 = data.Length > 1 ? data[1] : 0u;
            uint data2 = data.Length > 2 ? data[2] : 0u;
            MusicDeviceMIDIEvent(unit, status, data1, data2, (uint)frameOffset);
        }

        public int Render(short[] buffer, int offset, int sampleCount)
        {
            if (disposed || sampleCount < 2) return 0;
            int frames = sampleCount / 2;
            if (leftRenderBuffer.Length < frames * 2) leftRenderBuffer = new float[frames * 2];
            if (rightRenderBuffer.Length < frames) rightRenderBuffer = new float[frames];
            Array.Clear(leftRenderBuffer, 0, frames * 2);
            Array.Clear(rightRenderBuffer, 0, frames);
            GCHandle leftHandle = GCHandle.Alloc(leftRenderBuffer, GCHandleType.Pinned);
            GCHandle rightHandle = default;
            IntPtr listPtr = IntPtr.Zero;
            try
            {
                IntPtr leftPtr = Marshal.UnsafeAddrOfPinnedArrayElement(leftRenderBuffer, 0);
                AudioBufferListTwo list = new()
                {
                    mNumberBuffers = nonInterleaved ? 2u : 1u,
                    mBuffer1 = new AudioBuffer
                    {
                        mNumberChannels = nonInterleaved ? 1u : 2u,
                        mDataByteSize = (uint)(frames * sizeof(float) * (nonInterleaved ? 1 : 2)),
                        mData = leftPtr,
                    },
                };
                if (nonInterleaved)
                {
                    rightHandle = GCHandle.Alloc(rightRenderBuffer, GCHandleType.Pinned);
                    list.mBuffer2 = new AudioBuffer
                    {
                        mNumberChannels = 1,
                        mDataByteSize = (uint)(frames * sizeof(float)),
                        mData = Marshal.UnsafeAddrOfPinnedArrayElement(rightRenderBuffer, 0),
                    };
                }
                listPtr = Marshal.AllocHGlobal(Marshal.SizeOf<AudioBufferListTwo>());
                Marshal.StructureToPtr(list, listPtr, false);
                AudioTimeStamp timestamp = new()
                {
                    mSampleTime = renderFramePosition,
                    mRateScalar = 1.0,
                    mFlags = 1, // kAudioTimeStampSampleTimeValid
                };
                uint actionFlags = 0;
                int renderStatus = AudioUnitRender(unit, ref actionFlags, ref timestamp, 0, (uint)frames, listPtr);
                if (renderStatus != 0)
                {
                    throw new InvalidOperationException(
                        $"AudioUnitRender(DLS synth) failed: OSStatus {renderStatus} "
                        + $"(format={outputFormat.mFormatID:X8}, flags={outputFormat.mFormatFlags:X8}, "
                        + $"channels={outputFormat.mChannelsPerFrame}, bytes/frame={outputFormat.mBytesPerFrame}, "
                        + $"nonInterleaved={nonInterleaved})");
                }
                renderFramePosition += frames;
                for (int frame = 0; frame < frames; frame++)
                {
                    float left = leftRenderBuffer[nonInterleaved ? frame : frame * 2];
                    float right = nonInterleaved ? rightRenderBuffer[frame] : leftRenderBuffer[frame * 2 + 1];
                    buffer[offset + frame * 2] = FloatToShort(left);
                    buffer[offset + frame * 2 + 1] = FloatToShort(right);
                }
                return frames * 2;
            }
            finally
            {
                if (listPtr != IntPtr.Zero) Marshal.FreeHGlobal(listPtr);
                if (rightHandle.IsAllocated) rightHandle.Free();
                leftHandle.Free();
            }
        }

        private static short FloatToShort(float value)
            => (short)Math.Clamp((int)Math.Round(value * short.MaxValue), short.MinValue, short.MaxValue);

        public void Reset()
        {
            for (uint channel = 0; channel < 16; channel++)
            {
                MusicDeviceMIDIEvent(unit, 0xb0 + channel, 123, 0, 0); // all notes off
                MusicDeviceMIDIEvent(unit, 0xb0 + channel, 120, 0, 0); // all sound off
            }
        }

        private static void ThrowIfError(int status, string operation)
        {
            if (status != 0) throw new InvalidOperationException($"{operation} failed: OSStatus {status}");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (unit != IntPtr.Zero)
            {
                Reset();
                AudioUnitUninitialize(unit);
                AudioComponentInstanceDispose(unit);
                unit = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        ~MacMidiSynth() => Dispose();
    }
}
