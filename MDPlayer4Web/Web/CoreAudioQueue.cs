// Web stand-in for macos/CoreAudioOutput/CoreAudioQueue.cs with the same public surface,
// so MainWindow's playback code runs unchanged. Instead of an Audio Queue it feeds the
// pcm-player AudioWorklet (wwwroot/pcm-worklet.js): the worklet asks for PCM as its buffer
// drains and WebAudio.Pull calls this queue's fill callback on the page's main thread.
using MDPlayer.UI;

namespace MDPlayer.CoreAudioOutput
{
    public class CoreAudioQueue : IDisposable
    {
        private static int nextId;
        private static readonly Dictionary<int, CoreAudioQueue> queues = new();

        private readonly int id;
        private readonly Func<short[], int, int> fillCallback;
        private short[] scratch = Array.Empty<short>();
        private byte[] bytes = Array.Empty<byte>();
        private volatile bool stopped;

        /// <summary>True once fillCallback has signaled "no more audio".</summary>
        public bool Finished { get; private set; }

        public CoreAudioQueue(uint sampleRate, int framesPerBuffer, int bufferCount, Func<short[], int, int> fillCallback)
        {
            this.fillCallback = fillCallback;
            id = ++nextId;
            queues[id] = this;
            // The page's main thread also runs the UI, so keep at least half a second queued
            // regardless of the (much lower) native latency setting.
            int bufferedFrames = Math.Max(framesPerBuffer * bufferCount, (int)(sampleRate / 2));
            WebAudio.Open(id, (int)sampleRate, bufferedFrames);
        }

        internal static byte[] Pull(int id, int frames)
        {
            if (!queues.TryGetValue(id, out CoreAudioQueue? queue)) return Array.Empty<byte>();
            return queue.Fill(frames);
        }

        private byte[] Fill(int frames)
        {
            if (stopped || Finished || frames <= 0) return Array.Empty<byte>();
            int count = frames * 2;
            if (scratch.Length != count)
            {
                scratch = new short[count];
                bytes = new byte[count * 2];
            }

            int filled;
            try
            {
                filled = fillCallback(scratch, count);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CoreAudioQueue] buffer refill failed: {ex}");
                filled = 0;
            }
            if (stopped) return Array.Empty<byte>();
            if (filled <= 0)
            {
                Finished = true;
                return Array.Empty<byte>();
            }

            Buffer.BlockCopy(scratch, 0, bytes, 0, filled * sizeof(short));
            return filled == count ? bytes : bytes.AsSpan(0, filled * sizeof(short)).ToArray();
        }

        public void Start() => WebAudio.Start(id);

        public void Pause()
        {
            if (stopped) return;
            WebAudio.Pause(id);
        }

        public void Stop(bool immediate = true)
        {
            if (stopped) return;
            stopped = true;
            WebAudio.Stop(id);
        }

        public void Dispose()
        {
            Stop();
            queues.Remove(id);
            GC.SuppressFinalize(this);
        }
    }
}
