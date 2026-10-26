using MDPlayer;
using MDPlayer.Driver.MUCOM;
using System.Text;
using System.Buffers.Binary;

internal static class Scc5Tests
{
    internal static void Run(Action<bool,string> check,string fixtures)
    {
        string folder=Path.Combine(Path.GetTempPath(),"mdplayer-scc5-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"score.muc");
        string Wave(int id,int value)=>"  @W"+id+":{ "+string.Join(',',Enumerable.Repeat(value,32))+" }\n";
        byte[] Compile(string score)
        {
            byte[] bytes=MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes(score),path,out string error);
            check(bytes!=null,"SCC5 compile: "+error);return bytes;
        }
        void Reject(string score)
        {
            var result=MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes(score),path,out string error);
            check(result==null && !string.IsNullOrEmpty(error),"Accepted invalid SCC5 source: "+score);
        }
        string definitions=string.Concat(Enumerable.Range(0,5).Select(ch=>Wave(ch,ch*20-100)));
        string rows=string.Concat(Enumerable.Range(0,5).Select(ch=>$"SCC{(char)('A'+ch)} C192t240q0 @{ch} E255,0,0,0,0,255 v{ch+2} o4 c%24 r%4\n"));
        byte[] binary=Compile(definitions+rows);var profile=MsxMubProfile.Parse(binary);
        foreach(string form in new[]{Wave(7,-80),Wave(7,-80).TrimStart(),Wave(7,-80).Replace("@W","@w"),
                    Wave(7,-80).Replace(":","="),Wave(7,-80).Replace(":{", "\n").Replace("}","")})
            Compile(form+"SCCA @7 o4 v15 c8\n");
        check(profile.DedicatedScc && profile.Capabilities==5 && profile.UsesSccPlus,"SCC5 profile");
        check(!profile.UsesOpll && !profile.UsesPsg && !profile.UsesOpna,"SCC-only profile registered unrelated chips");
        var session=MusicEngine.Load(binary,Path.ChangeExtension(path,".mub"));check(session!=null,"SCC5 MUB load");
        short[] audio=new short[128];long energy=0;
        for(int tick=0;tick<24;tick++)
        {
            session.RenderSamplesWithMasterVolume(audio,0,audio.Length);energy+=audio.Sum(x=>Math.Abs((int)x));
        }
        var channels=session.ChipRegister.GetK051649Register(0).channel_list;
        for(int ch=0;ch<5;ch++)
        {
            check(channels[ch].volume==ch+2,"SCC5 linear volume: "+ch);
            check(channels[ch].waveram.All(x=>x==ch*20-100),"Independent SCC5 wave: "+ch);
            check(channels[ch].frequency>0 && channels[ch].key!=0,"SCC5 missing pitch/key: "+ch);
        }
        check(energy>0,"SCC5 silent");
        // An unused page 9 remains in this packet because v0 is a command.
        // Its end must not clear page 0's physical SCC or OPNA SSG volume.
        foreach(string channel in new[]{"SCCA","D","MSXD"})
        {
            string score=(channel=="SCCA"?Wave(7,-80):"")+
                $"{channel}0 C192t240q0 @{(channel=="SCCA"?7:0)} E255,0,0,0,0,255 v15 o4 c%48 r%2\n{channel}9 v0\n";
            byte[] packet=channel=="D"?Compile("MSXA r%1\n"+score):Compile(score);
            var active=MusicEngine.Load(packet,Path.ChangeExtension(path,".mub"));
            check(active!=null,"Inactive-page regression load: "+channel);
            short[] first=new short[128];active.RenderSamplesWithMasterVolume(first,0,first.Length);
            check(first.Any(v=>v!=0),"Inactive page muted the first audio block: "+channel);
            if(channel=="SCCA")check(active.ChipRegister.GetK051649Register(0).channel_list[0].volume>0,"Unused SCC page stole volume");
        }
        for(int i=0;i<1000 && !session.Driver.Stopped;i++)session.RenderSamplesWithMasterVolume(audio,0,audio.Length);
        check(session.Driver.Stopped && session.Driver is MucomDotNET d && d.PlaybackError=="","SCC5 finite completion");
        check(channels.All(c=>c.volume==0 && c.key==0),"SCC5 stop did not mute five channels");
        // Source compiles multi-select and page 9 without stealing OPNA or native PSG.
        byte[] pages=Compile(Wave(7,-80)+"#*1{c%3 / d%2 }\nSCCABCDE9 C192t240q0 @7 E255,0,0,0,0,255 v15 o4 [*1]3 r%4\nMSXD C192t240q0 @0 v12 o4 c%12 r%4\n");
        var p=MusicEngine.Load(pages,Path.ChangeExtension(path,".mub"));check(p!=null,"SCC5 page9/multiselect load");
        int nonzero=0;for(int i=0;i<2000 && !p.Driver.Stopped;i++){p.RenderSamplesWithMasterVolume(audio,0,audio.Length);nonzero+=audio.Count(x=>x!=0);}
        check(p.Driver.Stopped && p.Driver is MucomDotNET pd && pd.PlaybackError=="" && nonzero>0,"SCC5 page9/repeat completion");
        byte[] loop=Compile(Wave(0,127)+"SCCE C192t240q0 @0 E255,0,0,0,0,255 v15 o4 L c%4 r%2\n");
        var l=MusicEngine.Load(loop,Path.ChangeExtension(path,".mub"));check(l!=null,"SCC5 loop load");
        for(int i=0;i<2000 && l.Driver.vgmCurLoop<2 && !l.Driver.Stopped;i++)l.RenderSamplesWithMasterVolume(audio,0,audio.Length);
        check(l.Driver.vgmCurLoop>=2 && l.Driver is MucomDotNET ld && ld.PlaybackError=="","SCC5 two loop boundaries");
        foreach(byte cap in new byte[]{0,1,2,3,4,6,7,255}){byte[] bad=(byte[])binary.Clone();bad[26]=cap;try{MsxMubProfile.Parse(bad);check(false,"Accepted SCC5 cap "+cap);}catch(FormatException){check(true,"cap");}}
        for(int i=27;i<34;i++){byte[] bad=(byte[])binary.Clone();bad[i]=1;try{MsxMubProfile.Parse(bad);check(false,"Accepted SCC5 reserved byte");}catch(FormatException){check(true,"reserved");}}
        Reject(Wave(0,0)+Wave(0,1)+"SCCA @0 c\n");
        Reject(Wave(0,0)+"SCCA @1 c\n");
        Reject(Wave(0,0)+"SCCA @W0 c\n");
        Reject("  @W0:{1,2,3}\nSCCA @0 c\n");
        Reject(Wave(0,0)+"#SSGExtend On\nSCCA @0 c\n");
        Reject(Wave(0,0)+"#sccvoice missing.dat\nSCCA @0 c\n");
        Reject(Wave(0,0)+"SCCA @0 c\n#sccvoice missing.dat\n");
        foreach(int duration in new[]{50,127,128,200,255})
        {
        string repeating=Wave(0,70)+$"SCCA C192t240q0 @0 E255,0,0,0,0,255 v15 [o4c%7]4 &c%{duration} r%2\n";
        string expanded=Wave(0,70)+$"SCCA C192t240q0 @0 E255,0,0,0,0,255 v15 o4 c%7 c%7 c%7 c%7 &c%{duration} r%2\n";
        var re=MusicEngine.Load(Compile(repeating),Path.ChangeExtension(path,".mub"));
        var ex=MusicEngine.Load(Compile(expanded),Path.ChangeExtension(path,".mub"));
        check(re!=null && ex!=null,"Repeat/tie regression load");short[] ra=new short[128],ea=new short[128];
        for(int i=0;i<4000 && (!re.Driver.Stopped || !ex.Driver.Stopped);i++)
        {
            re.RenderSamplesWithMasterVolume(ra,0,ra.Length);ex.RenderSamplesWithMasterVolume(ea,0,ea.Length);
            check(ra.SequenceEqual(ea),"Repeat-ending tie changes PCM");
            check(re.Driver.Stopped==ex.Driver.Stopped,"Repeat-ending tie duration differs");
        }
        check(re.Driver.Stopped && re.Driver is MucomDotNET rd && rd.PlaybackError=="","Repeat-ending tie failed to end");
        }
        // Dedicated SCC runs alongside both OPNA FM banks and native PSG.
        File.Copy(Path.Combine(fixtures,"voice.dat"),Path.Combine(folder,"voice.dat"));
        string fm=string.Concat("ABCHIJ".Select(c=>$"{c} C192t240q0 @0 v12 o4 c%24 r%4\n"));
        byte[] mixed=Compile("#voice voice.dat\n"+definitions+rows+fm+"MSXD C192t240q0 @0 v15 o4 c%24 r%4\n");
        var mix=MusicEngine.Load(mixed,Path.ChangeExtension(path,".mub"));check(mix!=null,"Mixed SCC5/OPNA/PSG load");
        HashSet<int> fmKeys=new();
        for(int tick=0;tick<64;tick++)
        {
            mix.RenderSamplesWithMasterVolume(audio,0,audio.Length);
            var keys=mix.ChipRegister.fmKeyOnYM2608[0];
            if(keys!=null)for(int ch=0;ch<Math.Min(6,keys.Length);ch++)if((keys[ch]&0xf0)!=0)fmKeys.Add(ch);
        }
        check(fmKeys.Count==6,"SCC5 stole OPNA FM channels/bank: "+string.Join(',',fmKeys));
        check(mix.ChipRegister.GetK051649Register(0).channel_list.All(c=>c.key!=0 && c.volume>0),"Mixed SCC5 missing channels");
        // A declared external SCWD is validated even when inline data overrides it.
        byte[] bank=new byte[50];Encoding.ASCII.GetBytes("SCWD").CopyTo(bank,0);bank[4]=1;bank[6]=1;bank[8]=34;bank[16]=7;
        Array.Fill(bank,(byte)90,18,32);uint crc=uint.MaxValue;
        foreach(byte b in bank.AsSpan(16)){crc^=b;for(int n=0;n<8;n++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}
        BinaryPrimitives.WriteUInt32LittleEndian(bank.AsSpan(12),crc^uint.MaxValue);File.WriteAllBytes(Path.Combine(folder,"waves.dat"),bank);
        check(MsxMubProfile.Parse(Compile("#sccvoice waves.dat\nSCCA @7 v15 o4 c8\n")).DedicatedScc,"SCWD bank failed");
        Compile("#sccvoice waves.dat\n"+Wave(7,-50)+"SCCE @7 v15 o4 c8\n");
        bank[^1]^=1;File.WriteAllBytes(Path.Combine(folder,"waves.dat"),bank);Reject("#sccvoice waves.dat\n"+Wave(7,-50)+"SCCA @7 c\n");
        // The scoped compiler hook must never leak into ordinary SSG @15 compilation.
        var ordinary=MucomDotNET.CompileMuc(Encoding.ASCII.GetBytes("D @15 o4 v12 c8\n"),path,out string ordinaryError);
        check(ordinary!=null && !MsxMubProfile.HasMagic(ordinary),"SCC compiler mode leaked: "+ordinaryError);
        Directory.Delete(folder,true);
    }
}
