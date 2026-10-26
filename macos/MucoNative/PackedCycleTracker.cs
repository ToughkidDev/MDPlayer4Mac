using System.Security.Cryptography;
using System.Text;

namespace MDPlayer.MucoNative;

internal sealed partial class PackedMusicPlayer
{
    // A backward phrase jump also occurs during introductions. Count a song
    // loop only after the complete deterministic musical state repeats.
    private readonly Dictionary<string,long> cycleStates=new();
    private readonly MemoryStream cycleBuffer=new();
    private long cycleStart=-1,cycleLength;
    private long MusicalLoops=>cycleLength==0?0:Math.Max(0,(Ticks-cycleStart)/cycleLength);
    private void TrackCycle()
    {
        if(Ended||cycleLength!=0||cycleStates.Count>=65536)return;
        cycleBuffer.SetLength(0);using var w=new BinaryWriter(cycleBuffer,Encoding.ASCII,true);
        void Voice(Channel c)
        {
            w.Write(c.Pc);w.Write(c.Remaining);w.Write(c.PatternPc);w.Write(c.PatternRemaining);w.Write(c.Mark);w.Write(c.LastReturn);w.Write(c.Standard);w.Write(c.W);w.Write(c.M);w.Write(c.U);
            w.Write(c.Returns.Count);foreach(int value in c.Returns)w.Write(value);
        }
        foreach(var c in channels)
        {
            Voice(c);
            if(programs.TryGetValue(c,out var s))
            {
                w.Write(true);w.Write(s.Kt);w.Write(s.KtStart);w.Write(s.Latched);w.Write(s.LatchedStart);w.Write(s.Im);w.Write(s.ImStart);w.Write(s.LastPitch);w.Write(s.Root);w.Write(s.Pattern);w.Write(s.Wait);w.Write(s.Held);w.Write(s.Active);w.Write(s.Cancelled);w.Write(s.Valid);w.Write(s.Sound);if(s.Aux!=null){w.Write(true);Voice(s.Aux);}else w.Write(false);
            }
            else w.Write(false);
            foreach(var dictionary in new[]{levels,pitches})
            {
                if(dictionary.TryGetValue(c,out var t))
                {
                    w.Write(true);w.Write(t.Root);w.Write(t.Pc);w.Write(t.Wait);w.Write(t.Interval);w.Write(t.RepeatA);w.Write(t.RepeatB);w.Write(t.Extra);w.Write(t.Last);w.Write(t.Multiplier);w.Write(t.Delay);w.Write(t.Policy);w.Write(t.Active);w.Write(t.Calls.Count);foreach(int value in t.Calls)w.Write(value);
                }
                else w.Write(false);
            }
            w.Write(wavePhases.GetValueOrDefault(c));
            if(phaseWaves.TryGetValue(c,out var phases)){w.Write(true);foreach(int value in phases)w.Write(value);}else w.Write(false);
            if(olEnvelopes.TryGetValue(c.Index,out var envelope)){w.Write(true);Voice(envelope);}else w.Write(false);
        }
        foreach(int value in new[]{mixer,noisePrimary,noiseSecondary,noisePolicy,envelopeShape,envelopeLow,envelopeHigh,psgPending,wavePolicy,waveGateSample,waveFlags,waveShift,waveRate,waveCount,wavePhase,waveOwner,waveA,waveB,fadeProfile,fadeInterval,fadeStep,fadeTarget,fadeCurrent,fadeCounter})w.Write(value);
        w.Write(generated);w.Write(psg);foreach(int value in sccCache.Concat(fadeOpllRaw).Concat(fadeOpllPhysical))w.Write(value);foreach(bool value in fadeOpllWritten)w.Write(value);w.Write(bassPending);w.Write(pendingFade!=null);if(pendingFade!=null)foreach(int value in pendingFade)w.Write(value);
        foreach(int value in olLow.Concat(olHigh).Concat(olVolume).Concat(olDrumVolumes))w.Write(value);
        foreach(var entry in olPatches.OrderBy(x=>x.Key)){w.Write(entry.Key);w.Write(entry.Value);}w.Flush();
        string key=Convert.ToHexString(SHA256.HashData(cycleBuffer.GetBuffer().AsSpan(0,(int)cycleBuffer.Length)));
        if(cycleStates.TryGetValue(key,out long previous)){cycleStart=previous;cycleLength=Ticks-previous;cycleStates.Clear();}
        else cycleStates[key]=Ticks;
    }
}
