// Real-time VGM playback through the Mac's speakers. Loads a VGM file the same way
// EngineSmokeTest does (via MDPlayerCore's VgmEngine.Load), but instead of writing the
// rendered samples to a WAV file, streams them live through CoreAudioOutput's
// Audio Queue Services wrapper (CoreAudioQueue). This is the first milestone in the port
// that can only be verified on a real Mac with real speakers - the Linux sandbox this was
// written in has no AudioToolbox.framework at all, so only `dotnet build` could be checked
// here; actual sound has to be confirmed by running this on the target machine.
using MDPlayer;
using MDPlayer.CoreAudioOutput;

namespace MDPlayer.LivePlayer
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("usage: LivePlayer <input.vgm>");
                return 1;
            }

            string vgmPath = args[0];
            if (!File.Exists(vgmPath))
            {
                Console.Error.WriteLine($"error: file not found: {vgmPath}");
                return 1;
            }

            byte[] vgmBuf = File.ReadAllBytes(vgmPath);
            Console.WriteLine($"Loaded {vgmPath} ({vgmBuf.Length} bytes)");

            VgmEngineSession session = VgmEngine.Load(vgmBuf);
            if (session == null)
            {
                Console.Error.WriteLine("error: Vgm.init() failed, or the file uses none of the chips VgmEngine.Load wires up (see VgmEngine.cs)");
                return 1;
            }

            Vgm vgm = session.Vgm;
            MDSound.MDSound mds = session.Mds;
            uint sampleRate = session.SampleRate;

            Console.WriteLine($"VGM version {vgm.Version}, chips: {session.DescribeActiveChips()}");
            Console.WriteLine($"Playing live @ {sampleRate}Hz. Press Ctrl+C to stop.");

            // framesPerBuffer * bufferCount is roughly how much audio is buffered ahead of
            // the speaker at any moment - small enough to keep latency low, large enough
            // that a slow fillCallback call doesn't starve the queue and click/glitch.
            const int framesPerBuffer = 2048;
            const int bufferCount = 4;

            bool stopRequested = false;
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true; // let us shut the queue down cleanly instead of the process dying mid-callback
                stopRequested = true;
            };

            int FillCallback(short[] buf, int sampleCount)
            {
                if (stopRequested || vgm.Stopped)
                    return 0;
                return mds.Update(buf, 0, sampleCount, vgm.oneFrameProc);
            }

            using CoreAudioQueue queue = new(sampleRate, framesPerBuffer, bufferCount, FillCallback);
            queue.Start();

            while (!stopRequested && !queue.Finished)
            {
                Thread.Sleep(50);
            }

            // The last few buffers enqueued before Finished flipped are still draining -
            // give them a moment to actually finish playing before tearing the queue down,
            // otherwise the tail of the track gets cut off.
            if (!stopRequested)
            {
                Thread.Sleep((int)(1000.0 * framesPerBuffer * bufferCount / sampleRate) + 200);
            }

            queue.Stop();
            Console.WriteLine(stopRequested ? "Stopped." : "Playback finished.");

            return 0;
        }
    }
}
