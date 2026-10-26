using System.Text;
using System.Text.Json;
using MDPlayer.MucoNative;

internal static class ToolchainSyncTests
{
    internal static void Run(Action<bool,string> check)
    {
        (PackedMusicPlayer P,List<NativeChipWrite> E) Make(string text)
        {
            text="?WD0={"+string.Join(',',Enumerable.Repeat(0,32))+"}\n"+text;
            var score=MsxMusicalCompiler.Parse(text,_=>null);var events=new List<NativeChipWrite>();
            return(new(new(score.Object),events.Add,_=>{}),events);
        }
        byte[] Work(PackedMusicPlayer p,int channel)
        {
            using var json=JsonDocument.Parse(JsonSerializer.Serialize(p.Snapshot()));
            return json.RootElement[channel].GetProperty("w").GetBytesFromBase64();
        }
        // Reusing a sweep across note boundaries must rearm its byte product,
        // including negative phase/residual arithmetic. No waveform is needed.
        foreach(int count in new[]{0,1,4,7})foreach(int step in new[]{-3,3})
        {
            var repeated=Make($"SCCA ?XU1 ?XT1 ?DS{step},{count},1 v12 o4 c%16 c%16 !\n");
            var first=new List<int>();var second=new List<int>();
            for(int t=0;t<32;t++)
            {
                repeated.P.Tick();byte[] w=Work(repeated.P,3);
                (t<16?first:second).Add(w[10]|w[11]<<8);
            }
            check(first.SequenceEqual(second),"Byte sweep did not rearm identically on the second note: "+count);
        }
        foreach(string channel in new[]{"MSXD","MSXE","MSXF"})
        {
            string owner=channel=="MSXD"?"":"MSXD ?XU1 ?XT1 ?NP5 v12 o4 c%8 !\n";
            var noise=Make(owner+$"{channel} q0 ?NP5 w0 v12 o4 c%4 w7 c%4 !\n");
            noise.P.Tick();check(noise.E.Last(x=>x.Chip==NativeChip.Psg&&x.Address==6).Value==0,"w0 did not replace the shared noise latch on "+channel);
            for(int t=1;t<=4;t++)noise.P.Tick();
            check(noise.E.Last(x=>x.Chip==NativeChip.Psg&&x.Address==6).Value==6,"w7 did not update the shared noise latch on "+channel);
        }
        string map="?IM0={"+string.Join(',',Enumerable.Repeat(10,12))+"}\nPSGS ?FD10={?XM2,1 ?XP384,12 ?FR}\n";
        var cancelled=Make(map+"MSXD ?XU1 ?XT1 ?MV0,$11 ?IM0 v12 o4 c%2 ?IM- ?XC r%4 !\n");
        var restPeriods=new List<int>();
        for(int t=0;t<6;t++){cancelled.P.Tick();byte[] w=Work(cancelled.P,0);if(t>=2){check(w[13]==0,"Cancelled program left a rest gated.");restPeriods.Add(w[10]|w[11]<<8);}}
        check(restPeriods.Distinct().Count()>1,"A rest retained cancelled IM ownership and suppressed its pitch phase.");
        var definitionFirst=Make("SCCB ?FD20={?EN2 ?FR}\nMSXA ?IR3 v12 o4 c%4 !\nSCCB ?XU1 ?XT1 @0 ?EP- v12 o4 c%4 !\n");
        definitionFirst.P.Tick();byte[] routed=Work(definitionFirst.P,4);
        check(routed[0]!=0&&routed[12]>0&&routed[13]!=0,"A compact main route following its phrase definition was replaced with the empty route in a mixed score.");
        const string parameters="7,2,31,8,3,5,2,41,1,1,3,31,5,4,5,2,39,0,5,2,31,4,2,5,1,31,0,3,7,28,30,6,6,0,0,2,1,4";
        byte[] raw=Convert.FromHexString("31732541291f27005f1f1f9c0804051e03020406251525063a");
        string music="\n?WD0={"+string.Join(',',Enumerable.Repeat(0,32))+"}\nA C192t240q0 @1 v10 o4 c%4 r%2\nH C192q0 @1 v10 o3 e%4 r%2\nMSXD ?XU1 ?XT1 v12 o4 c%4 !\nMSXA @1 v12 o4 c%4 !\n";
        byte[] Compile(string definition)=>NativeMucCompiler.Compile(Encoding.ASCII.GetBytes(definition+music),_=>null);
        byte[] normal=Compile("@1:{"+parameters+"}");
        byte[] bracedRaw=Compile("@%1:{"+string.Join(',',raw)+"}");
        string canonical="  @%1\n"+string.Concat(Enumerable.Range(0,6).Select(row=>" "+string.Join(',',raw.Skip(row*4).Take(4))+"\n"))+"  "+raw[24]+"\n";
        check(normal.SequenceEqual(bracedRaw)&&normal.SequenceEqual(Compile(canonical)),"Braced normal/raw FM voices differ from the existing fixed-column syntax.");
        byte[] expected=raw.ToArray();for(int i=12;i<16;i++)expected[i]|=128;
        check(normal.AsSpan().IndexOf(expected)>=0,"Mixed compilation lost inline FM operator parameters.");
        var writes=new List<NativeChipWrite>();using(var session=new NativeMucomSession(normal,44100,writes.Add,_=>null))
        {
            while(session.Status>0&&session.Samples<1_000_000)session.AdvanceSample();
            check(session.Status==0&&session.Error=="","Inline FM mixed playback failed.");
        }
        foreach(int key in new[]{0,4})check(writes.Any(x=>x.Chip==NativeChip.Opna1&&x.Address==0x28&&(x.Value&7)==key&&(x.Value&0xf0)!=0),"Mixed FM bank did not key on: "+key);
        bool missing=false;try{Compile("@2:{"+parameters+"}");}catch(FormatException){missing=true;}
        check(missing,"Undefined FM voice silently fell back to an empty bank.");
        foreach(string invalid in new[]{"@1:{1,2}","@256:{"+parameters+"}","@1:{"+parameters})
        {
            bool rejected=false;try{Compile(invalid);}catch(FormatException){rejected=true;}
            check(rejected,"Malformed braced FM voice accepted.");
        }
        Console.WriteLine("PASS toolchain sync: repeated byte sweep, shared w noise, inline FM and both OPNA banks");
    }
}
