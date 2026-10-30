// JavaScript interop for audio output (wwwroot/audio.js and pcm-worklet.js).
using System.Runtime.InteropServices.JavaScript;
using MDPlayer.CoreAudioOutput;

namespace MDPlayer.UI
{
    internal static partial class WebAudio
    {
        [JSImport("open", "mdplayer")]
        internal static partial void Open(int id, int sampleRate, int bufferedFrames);

        [JSImport("start", "mdplayer")]
        internal static partial void Start(int id);

        [JSImport("pause", "mdplayer")]
        internal static partial void Pause(int id);

        [JSImport("stop", "mdplayer")]
        internal static partial void Stop(int id);

        // Called by audio.js whenever the worklet's queue runs low. Returns little-endian
        // 16-bit stereo PCM, or an empty array once the song has ended.
        [JSExport]
        internal static byte[] Pull(int id, int frames) => CoreAudioQueue.Pull(id, frames);
    }
}
