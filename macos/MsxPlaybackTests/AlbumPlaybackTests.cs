using MDPlayer;
using MDPlayer.Driver.MUCOM;
using System.Text.Json;
using System.Security.Cryptography;

internal static class AlbumPlaybackTests
{
    internal static int Run(string manifest,string output)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(manifest));
        var results=new List<object>();int failures=0,index=0;
        foreach(var row in document.RootElement.EnumerateArray())
        {
            string path=row.GetProperty("muc").GetString()!;
            bool loop=row.GetProperty("loop").GetBoolean();
            long expected=row.GetProperty("input_samples").GetInt64()+(loop?2L*row.GetProperty("loop_samples").GetInt64():0);
            long frames=0,energy=0,clipped=0;double squares=0;int peak=0;
            int verifiedWrites=0;
            string error="",chips="",compiledHash="";uint loops=0;bool stopped=false;byte? raw=null;
            try
            {
                byte[] source=File.ReadAllBytes(path);
                byte[] binary=MucomDotNET.CompileMuc(source,path,out string compileError);
                if(binary==null)throw new Exception("Compile: "+compileError);
                compiledHash=Convert.ToHexString(SHA256.HashData(binary));
                var profile=MsxMubProfile.Parse(binary);raw=profile.RawFlags;
                var session=MusicEngine.Load(source,path)??throw new Exception("MusicEngine.Load failed");
                chips=session.DescribeActiveChips();
                if(session.ChipClocks.ContainsKey(MDSound.MDSound.enmInstrumentType.K051649)!=profile.UsesScc)
                    throw new Exception("SCC initialization differs from raw declaration");
                if(session.UsesK052539SccPlus!=profile.UsesSccPlus)throw new Exception("SCC+ variant mismatch");
                var writes=new List<int[]>();int sccPort=0,sccAddress=0;
                if(row.TryGetProperty("source_events",out _))
                {
                    // Observe the existing raw sink without changing the product API.
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    var driverField=typeof(MucomDotNET).GetField("driver",flags)!;
                    object core=driverField.GetValue(session.Driver)!;
                    var sinkField=core.GetType().GetField("WriteOPNAS",flags)!;
                    var sink=(Action<musicDriverInterface.ChipDatum>)sinkField.GetValue(core)!;
                    sinkField.SetValue(core,(Action<musicDriverInterface.ChipDatum>)(write=>
                    {
                        if(write.port==0)
                        {
                            int r=write.address,v=write.data;
                            if(r>=64&&r<128)writes.Add(new[]{0,r-64,v});
                            else if(r>=192&&r<=255)writes.Add(new[]{0,r-192,v});
                            else if(r>=128&&r<=141)writes.Add(new[]{1,r-128,v});
                            else if(r>=142&&r<=147){sccPort=r-142;sccAddress=v;}
                            else if(r==148){if(profile.UsesScc)writes.Add(new[]{2,(sccPort<<8)|sccAddress,v});sccAddress=(sccAddress+1)&255;}
                            else if(r>=149&&r<=162)writes.Add(new[]{3,r-149,v});
                        }
                        sink(write);
                    }));
                }
                short[] samples=new short[4096];
                long limit=Math.Max(44100,expected)+44100*2;
                while(frames<limit && !session.Driver.Stopped && (!loop||session.Driver.vgmCurLoop<3))
                {
                    int n=session.RenderSamplesWithMasterVolume(samples,0,samples.Length);
                    if(n<=0)throw new Exception("Renderer returned no samples");
                    frames+=n/2;
                    for(int i=0;i<n;i++){int value=Math.Abs((int)samples[i]);energy+=value;squares+=(double)value*value;peak=Math.Max(peak,value);if(value>=32767)clipped++;}
                }
                stopped=session.Driver.Stopped;loops=session.Driver.vgmCurLoop;
                if(session.Driver is MucomDotNET driver && !string.IsNullOrEmpty(driver.PlaybackError))
                    throw new Exception("Playback: "+driver.PlaybackError);
                if(row.TryGetProperty("source_events",out var tracePath))
                {
                    using var trace=JsonDocument.Parse(File.ReadAllText(tracePath.GetString()!));
                    int start=trace.RootElement.GetProperty("initial_writes").GetInt32();
                    var expectedWrites=trace.RootElement.GetProperty("events").EnumerateArray();
                    foreach(var write in expectedWrites)
                    {
                        int[] target=write.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                        int at=start+verifiedWrites;
                        if(at>=writes.Count||!writes[at].SequenceEqual(target))
                            throw new Exception($"Source register order/value differs at {verifiedWrites}: expected [{string.Join(',',target)}], actual "+(at<writes.Count?$"[{string.Join(',',writes[at])}]":"missing"));
                        verifiedWrites++;
                    }
                }
                if(energy==0)throw new Exception("Silent");
                if(loop?loops<3:!stopped)throw new Exception("Song/three loops did not complete within expected duration");
                if(Math.Abs(frames-expected)>4410)throw new Exception("Duration differs by more than 100ms");
                if(stopped&&profile.UsesScc && session.ChipRegister.GetK051649Register(0).channel_list.Any(c=>c.key!=0||c.volume!=0))
                    throw new Exception("SCC remains keyed after completion");
            }
            catch(Exception exception){error=exception.Message;failures++;}
            results.Add(new{muc=path,pass=error=="",error,raw_flags=raw,chips,loop,loops,stopped,frames,expected_frames=expected,duration_error_samples=frames-expected,peak,rms=frames==0?0:Math.Sqrt(squares/(2*frames)),clipped_samples=clipped,verified_source_writes=verifiedWrites,compiled_sha256=compiledHash});
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output,JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"{(++index)}/{document.RootElement.GetArrayLength()} {(error==""?"PASS":"FAIL")} {Path.GetFileName(Path.GetDirectoryName(path))}/{Path.GetFileName(path)} frames={frames} delta={frames-expected} loops={loops} peak={peak} {error}");
        }
        Console.WriteLine($"ALBUM TOTAL files={index} failures={failures}");
        return failures==0?0:1;
    }
}
