using MDPlayer;
using MDPlayer.Driver.MUCOM;
using MDPlayer.MucoNative;
using System.Text;

internal static class ModernPlaybackTests
{
    internal static void Run(Action<bool,string> check,string fixtures)
    {
        string wave="?WD0={"+string.Join(',',Enumerable.Range(0,32).Select(i=>i<16?-100:100))+"}\n";
        string native=wave+"?IU0={33,97,29,7,130,143,16,7}\n"+
            "MSXD ?EP2,129,115 ?DW-2 v12 o4 c%12 r%4\n"+
            "SCCE ?WP0 @0 ?EP2,129,115 ?DS3,5,2 v12 o4 e%12 r%4\n"+
            "MSXA ?IU0 ?EP2,129,115 ?DW-2 v12 o4 g%12 r%4\n";
        string mixed=native+"A C192t225q0 @0 o4 v12 c%32 r%4\n";
        byte[] Compile(string source)
        {
            byte[] bytes=MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes(source),Path.Combine(fixtures,"modern.muc"),out string error);
            check(bytes!=null,"Modern compile: "+error);return bytes;
        }
        long Render(byte[] data)
        {
            var session=MusicEngine.Load(data,Path.Combine(fixtures,"modern.mub"));check(session!=null,"Modern engine load rejected.");
            check(session.UsesK052539SccPlus,"Modern SCC fifth wave lost independent SCC+ mode.");
            check(session.ChipClocks.ContainsKey(MDSound.MDSound.enmInstrumentType.AY8910),"Modern PSG not acquired.");
            check(session.ChipClocks.ContainsKey(MDSound.MDSound.enmInstrumentType.YM2413emu),"Modern OPLL not acquired.");
            short[] buffer=new short[2048];long energy=0;
            for(int i=0;i<1000&&!session.Driver.Stopped;i++)
            {
                int n=session.RenderSamplesWithMasterVolume(buffer,0,buffer.Length);for(int j=0;j<n;j++)energy+=Math.Abs((int)buffer[j]);
            }
            check(session.Driver.Stopped,"Modern finite playback did not finish.");check(string.IsNullOrEmpty(((MucomDotNET)session.Driver).PlaybackError),"Modern playback error: "+((MucomDotNET)session.Driver).PlaybackError);
            check(energy>0,"Modern score was silent.");
            check(session.ChipRegister.GetK051649Register(0).channel_list.All(c=>c.key==0&&c.volume==0),"Modern SCC stop left sound active.");
            var fifth=session.ChipRegister.GetK051649Register(0).channel_list[4].waveram;check(fifth.Take(16).All(x=>x==-100)&&fifth.Skip(16).Take(16).All(x=>x==100),"Modern fifth SCC+ waveform was not uploaded independently.");
            return energy;
        }
        var data=Compile(native);check(MsxMubProfile.Parse(data).IsPacked,"Modern source fell back to old chip mapping.");Render(data);
        mixed+="D @15 o4 v12 c%20 r%4\n";
        var mix=Compile(mixed);check(MsxMubProfile.Parse(mix).UsesOpna,"Modern mixed score discarded OPNA.");Render(mix);
        var mixedWrites=new List<NativeChipWrite>();using(var clocked=new NativeMucomSession(mix,44100,mixedWrites.Add))
        {
            while(clocked.Status>0&&clocked.Samples<100000)clocked.AdvanceSample();
            check(clocked.Status==0&&clocked.Samples>=36*394,"Packed finish retired the longer OPNA phrase early.");
            check(mixedWrites.Any(x=>x.Chip==NativeChip.Opna1&&x.Address==0x28&&(x.Value&240)!=0),"New mixed score emitted no OPNA key-on.");
            check(mixedWrites.Any(x=>x.Chip==NativeChip.Opna1&&x.Port==0&&x.Address==8&&(x.Value&15)!=0),"OPNA SSG was redirected by MSX capability bits.");
            check(mixedWrites.Any(x=>x.Chip==NativeChip.Opll&&x.Address is >=32 and <=37&&(x.Value&16)!=0),"New mixed score emitted no OPLL key-on.");
        }
        Render(Compile(mixed.Replace("c%12 r%4","c%8 t198 d%8 t225 e%8 r%4")));
        // The canonical names and both historical prefixes compile to one object.
        string canonical=wave+"SCCA ?XU2 ?XT1 @0 ?EP2,129,115 ?DB2 ?DS3,5 v12 o4 c%4 !\n";
        string question=canonical.Replace("?XU2","?u2").Replace("?XT1","?k1").Replace("?EP2,129,115","?e129,115,2").Replace("?DB2","?d2").Replace("?DS3,5","?g3,5");
        string historical=question.Replace("?u","@u").Replace("?k","@k").Replace("?e","@e").Replace("?d","@d").Replace("?g","@g");
        check(Compile(canonical).SequenceEqual(Compile(question))&&Compile(question).SequenceEqual(Compile(historical)),"Canonical, ? alias and @ alias differ.");
        // Malformed execution terminates the current session; a fresh one recovers.
        byte[] corrupt=(byte[])data.Clone();int typed=Find(corrupt,Encoding.ASCII.GetBytes("PN"));check(typed>=0,"Missing typed score.");
        int route=BitConverter.ToUInt16(corrupt,typed+10);corrupt[typed+route]=0xf8;corrupt[typed+route+1]=65;corrupt[typed+route+2]=255;
        int writes=0;using(var failed=new NativeMucomSession(corrupt,44100,_=>writes++))
        {
            try{for(int i=0;i<1000;i++)failed.AdvanceSample();check(false,"Unknown execution opcode accepted.");}
            catch(FormatException){check(failed.Status==0&&failed.Error.Length>0&&writes>0,"Runtime failure did not stop and mute.");}
        }
        check(Render(data)>0,"Modern restart after failure did not recover.");
        byte[] loop=Compile("MSXD ?EP- v12 o4 L c%2 d%2\n");
        using(var looping=new NativeMucomSession(loop,44100,_=>{}))
        {
            for(int i=0;i<394;i++)looping.AdvanceSample();check(looping.LoopCounter==0,"Initial route jump counted as a song loop.");
            for(int i=0;i<394*24;i++)looping.AdvanceSample();check(looping.LoopCounter>=2,"Repeated musical cycle was not reported.");
        }
        Console.WriteLine("PASS modern MSX native compiler, aliases, mixed audio and recovery");
    }
    private static int Find(byte[] data,byte[] needle){for(int i=0;i<=data.Length-needle.Length;i++)if(data.AsSpan(i,needle.Length).SequenceEqual(needle))return i;return -1;}
}
