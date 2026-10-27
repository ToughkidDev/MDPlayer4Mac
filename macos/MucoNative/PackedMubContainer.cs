using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using musicDriverInterface;
using mucomDotNET.Common;
using mucomDotNET.Driver;

namespace MDPlayer.MucoNative;

internal static class PackedMubContainer
{
    internal sealed record Page(byte[] Bytes,int Loop=-1);
    internal static byte[] Compile(MsxMusicalCompiler.Compilation score,Func<string,Stream> asset,string path)
    {
        string legacy=score.LegacySource;
        // Resource declarations belong to MSX only. Ordinary OPNA text and FM
        // definitions are compiled by the existing managed MUCOM compiler.
        legacy=string.Join('\n',legacy.Split('\n').Where(line=>!Regex.IsMatch(line.TrimStart(),@"^[@?](?:WD|WS|IU|IT|KT|IM|[WwOT])(?=[^A-Za-z]|$)|^#(?:sccvoice|driver)\b",RegexOptions.IgnoreCase)));
        if(Regex.IsMatch(legacy,@"(?im)^#SSGExtend\s+On\b"))throw new FormatException("Dedicated packed SCC channels cannot be combined with #SSGExtend On.");
        bool mixed=Regex.IsMatch(legacy,@"(?m)^\s*[A-K][A-K0-9]*[ \t]+");
        if(!mixed)return Write(score.Object,score.Title,score.Timer);
        legacy=InlineFmDefinitions.NormalizeBraces(legacy);
        var compiler=new mucomDotNET.Compiler.Compiler();compiler.Init();
        Match first=Regex.Match(legacy,@"(?m)^\s*[A-K][A-K0-9]*[ \t]+");
        string primed=first.Success?legacy.Insert(first.Index,"A t"+score.Timer+"\r\n"):legacy+"\r\nA t"+score.Timer+"\r\n";
        string text="#driver mucomDotNET\r\n"+primed+"\r\nL ; preserve five-group directory\r\n";
        using var input=new MemoryStream(MucSourceNormalization.NormalizeTrackDeclarationIndentation(Encoding.Latin1.GetBytes(text)),false);
        MmlDatum[] output=compiler.Compile(input,name=>asset?.Invoke(name));var info=compiler.GetCompilerInfo();
        if(output==null||info?.errorList?.Count>0)throw new FormatException(info?.errorList?.Count>0?string.Join('\n',info.errorList.Select(x=>x.Item3)):"OPNA compilation failed.");
        var header=new MUBHeader(output,myEncoding.Default);header.GetTags();if(header.mupb==null)throw new FormatException("Mixed packed score requires an OPNA directory.");
        var parts=Enumerable.Range(0,55).Select(_=>new List<Page>()).ToArray();
        for(int chip=0;chip<header.mupb.chips.Length;chip++)for(int p=0;p<header.mupb.chips[chip].parts.Length;p++)
        {
            var pages=header.mupb.chips[chip].parts[p].pages;
            if(chip>0&&pages.Any(x=>x.length>1))throw new FormatException("Packed MSX mixing currently reserves the additional chip groups; use primary OPNA A..K.");
            if(chip==0)
            {
                foreach(var page in pages)parts[p].Add(new(page.data.Select(x=>(byte)x.dat).ToArray(),page.loopPoint));
                while(parts[p].Count>1&&parts[p][^1].Bytes.SequenceEqual(new byte[]{0})&&parts[p][^1].Loop==-1)parts[p].RemoveAt(parts[p].Count-1);
            }
        }
        var instruments=header.mupb.instruments.Select(x=>x.data).ToList();if(instruments.Count==0)instruments.Add(new byte[]{0});
        int inst=header.mupb.chips[0].instrumentNumber.Length>0?(int)header.mupb.chips[0].instrumentNumber[0]:0;
        byte[] pcm=header.mupb.chips[0].pcmNumber.Length>0?header.mupb.pcms[header.mupb.chips[0].pcmNumber[0]].data:Array.Empty<byte>();
        string tags="";if(header.CarrierCorrection)tags+="#CarrierCorrection Yes\r\n";
        if(header.RhythmMute[0]!=63)tags+="#OPNA1RhythmMute "+new string("BSCHTR".Where((_,i)=>(header.RhythmMute[0]&(1<<i))==0).ToArray())+"\r\n";
        return Write(score.Object,score.Title,score.Timer,parts,instruments,pcm,inst,tags);
    }
    internal static byte[] Write(byte[] obj,string title,int timer,List<Page>[] parts=null,List<byte[]> instruments=null,byte[] pcm=null,int instrument=0,string extraTags="")
    {
        _=new PackedScoreData(obj);if(timer is <0 or >255)throw new FormatException("Invalid packed tempo.");
        int version=obj[2]-'0';bool mixed=parts!=null;
        parts=parts?.Select(x=>new List<Page>(x)).ToArray()??Enumerable.Range(0,55).Select(_=>new List<Page>()).ToArray();
        if(!mixed)for(int p=0;p<27;p++)parts[p].Add(new(new byte[]{0}));
        byte[] typed=new byte[obj.Length+3];typed[0]=0xfa;typed[1]=0x26;typed[2]=(byte)timer;obj.CopyTo(typed,3);
        if(!mixed)parts[0][0]=new(typed);else{for(int p=0;p<11;p++)if(parts[p].Count==0)parts[p].Add(new(new byte[]{0}));parts[0].Insert(0,new(typed));}
        instruments??=new(){new byte[]{0}};pcm??=Array.Empty<byte>();
        if(instruments.Count is <1 or >2||parts.Any(x=>x.Count>11))throw new FormatException("Invalid packed container layout.");
        uint caps=(uint)(version>=3?23:21)|(version>=4?128u:0)|(version>=5?256u:0)|(mixed?32u:0);
        string magic=version>=5?mixed?"msPb010A":"msPb0109":version>=4?mixed?"msPb0108":"msPb0107":version>=3?mixed?"msPb0106":"msPb0105":mixed?"msPb0104":"msPb0103";
        byte[] tags=Encoding.UTF8.GetBytes("#title "+title+"\r\n#driver MUCOMSX Packed Notes\r\n"+extraTags);
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream,Encoding.ASCII,true);
        writer.Write(Encoding.ASCII.GetBytes(magic));writer.Write((byte)5);writer.Write((byte)5);writer.Write((ushort)55);writer.Write((ushort)parts.Sum(x=>x.Count));writer.Write((ushort)instruments.Count);writer.Write((ushort)1);writer.Write(0u);writer.Write((uint)tags.Length);writer.Write(caps);writer.Write(0u);
        for(int c=0;c<5;c++)
        {
            writer.Write((ushort)c);writer.Write(c<2?72u:c<4?76u:48u);writer.Write(c<2?7_987_200u:c<4?8_000_000u:3_579_545u);writer.Write(0u);writer.Write(1u);writer.Write(0u);writer.Write((byte)11);
            writer.Write((byte)(c==0?1:0));if(c==0)writer.Write((ushort)instrument);writer.Write((byte)(c==0?1:0));if(c==0)writer.Write((ushort)0);
        }
        foreach(var part in parts)writer.Write((byte)part.Count);
        foreach(var page in parts.SelectMany(x=>x)){writer.Write((uint)page.Bytes.Length);writer.Write(page.Loop);}
        foreach(byte[] inst in instruments)writer.Write((uint)inst.Length);writer.Write((uint)pcm.Length);
        foreach(var page in parts.SelectMany(x=>x))writer.Write(page.Bytes);foreach(byte[] inst in instruments)writer.Write(inst);writer.Write(pcm);
        int tag=(int)stream.Position;writer.Write(tags);writer.Flush();byte[] result=stream.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(18),(uint)tag);return result;
    }
}
