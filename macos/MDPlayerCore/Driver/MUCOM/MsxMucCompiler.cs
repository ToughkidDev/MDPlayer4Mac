using System.Text;
using System.Text.RegularExpressions;
using musicDriverInterface;
using mucomDotNET.Driver;
using mucomDotNET.Common;

namespace MDPlayer.Driver.MUCOM;

internal static class MsxMucCompiler
{
    private static readonly Regex NativeLine = new(@"(?m)^[ \t]*MSX([A-K0-9]+)(?=[ \t])",RegexOptions.CultureInvariant);
    private static readonly Regex MusicLine = new(@"(?m)^[ \t]*(?:MSX)?(?:[A-Va-z][0-9]*)+[ \t]",RegexOptions.CultureInvariant);
    private static bool Extended(string source)
    {
        bool result=false;
        Match firstMusic = MusicLine.Match(source);
        foreach(Match tag in Regex.Matches(source,@"(?im)^#(SSGExtend|PCMInvert)[ \t]+([^;\r\n]*)"))
        {
            if (firstMusic.Success && tag.Index > firstMusic.Index)
                throw new FormatException("MSX extension tags must precede music rows.");
            string value=tag.Groups[2].Value.Trim();
            if (!value.Equals("On",StringComparison.OrdinalIgnoreCase) && !value.Equals("Off",StringComparison.OrdinalIgnoreCase))
                throw new FormatException("#"+tag.Groups[1].Value+" requires On or Off.");
            if(tag.Groups[1].Value.Equals("SSGExtend",StringComparison.OrdinalIgnoreCase))result=value.Equals("On",StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
    public static bool IsNativeSource(byte[] source)
    {
        string ascii=Encoding.Latin1.GetString(source);
        return NativeLine.IsMatch(ascii) || Extended(ascii);
    }
    public static byte[] Compile(byte[] source,string path,Func<string,Stream> sibling)
    {
        string ascii=Encoding.Latin1.GetString(source);
        bool native=NativeLine.IsMatch(ascii), extended=Extended(ascii);
        // L..V is upstream OPNA #2's sequencing lane; it becomes MSX output
        // only in this explicit profile. Ordinary dual-OPNA MUCs remain unchanged.
        ascii=NativeLine.Replace(ascii,m=>
        {
            if(m.Groups[1].Value.Contains('K'))throw new FormatException("MSXK has no ADPCM target.");
            return new string(m.Groups[1].Value.Select(c => c is >= 'A' and <= 'K' ? (char)(c + 11) : c).ToArray());
        });
        if(Regex.IsMatch(ascii,@"(?m)^[L-V]+[0-9]*[ \t]" ) && !native)
            throw new FormatException("SCC+ SSG source cannot mix a second OPNA group.");
        // Force extended command encoding even for a single-page native PSG song.
        ascii=Regex.Replace(ascii,@"(?im)^#driver[^\r\n]*","");
        ascii = NormalizeWaves(ascii);
        // Work starts at Timer-B 225, even when a source does not issue t/T.
        // The upstream extended compiler otherwise leaves it at its own default.
        // Prime A before music, after header tags. An authored tempo at tick zero
        // still wins; no event/loop operands are rewritten after compilation.
        Match firstMusic=MusicLine.Match(ascii);
        if(firstMusic.Success)ascii=ascii.Insert(firstMusic.Index,"A t225\r\n");
        byte[] input=Encoding.Latin1.GetBytes("#driver mucomDotNET\r\n"+ascii);
        input=MucomDotNET.NormalizeTrackDeclarationIndentation(input);
        mucomDotNET.Compiler.Compiler compiler=new();compiler.Init();
        using MemoryStream stream=new(input,false);
        MmlDatum[] output=compiler.Compile(stream,sibling);
        var info=compiler.GetCompilerInfo();
        if(output==null || info?.errorList?.Count>0)
            throw new FormatException(info?.errorList?.Count>0?string.Join("\n",info.errorList.Select(x=>x.Item3)):"MSX MUC compilation failed.");
        MUBHeader header=new(output,myEncoding.Default);
        if(header.mupb==null)throw new FormatException("MSX source requires extended compiler output.");
        return Pack(header.mupb,output,(byte)((native?1:0)|(extended?2:0)));
    }
    private static string NormalizeWaves(string source)
    {
        string[] lines=source.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
        List<string> output=new();
        for(int i=0;i<lines.Length;i++)
        {
            Match definition=Regex.Match(lines[i],@"^[ \t]+@W(\d+)[ \t]*(?:;.*)?$",RegexOptions.IgnoreCase);
            if(!definition.Success){output.Add(lines[i]);continue;}
            if(!int.TryParse(definition.Groups[1].Value,out int id) || id>255)throw new FormatException("Invalid SSG waveform ID.");
            List<int> samples=new();
            while(samples.Count<64)
            {
                if(++i>=lines.Length)throw new FormatException("SSG waveform needs 64 samples.");
                string row=lines[i].Split(';')[0].Trim();
                if(row.Length==0)continue;
                foreach(string token in row.Split(',',StringSplitOptions.TrimEntries))
                {
                    int value;
                    if(token.StartsWith('$'))
                    {
                        if(!int.TryParse(token[1..],System.Globalization.NumberStyles.HexNumber,null,out value) || value>255)
                            throw new FormatException("Invalid SSG hex sample.");
                        value=unchecked((sbyte)value);
                    }
                    else if(!int.TryParse(token,out value) || value is <-128 or >127)throw new FormatException("Invalid SSG sample.");
                    samples.Add(value);
                }
                if(samples.Count>64)throw new FormatException("SSG waveform has more than 64 samples.");
            }
            // Upstream requires exactly two spaces before @W and skips column zero of
            // each sample row. Normalize only definitions; retain all song bytes otherwise.
            output.Add("  @W"+id);
            for(int n=0;n<64;n+=16)output.Add(" "+string.Join(",",samples.Skip(n).Take(16)));
        }
        return string.Join("\r\n",output);
    }
    private static byte[] Pack(MupbInfo input,MmlDatum[] output,byte caps)
    {
        // Canonical MUCOMSX five-descriptor directory. Copy streams unchanged so repeat
        // distances/loop offsets remain valid. The driver handles upstream SSG opcodes.
        using MemoryStream stream=new();using BinaryWriter writer=new(stream,Encoding.ASCII,true);
        List<MupbInfo.PageDefine>[] parts=Enumerable.Range(0,55).Select(_=>new List<MupbInfo.PageDefine>()).ToArray();
        List<byte[]> inst=input.instruments.Select(i=>i.data).ToList();int[] instRef={-1,-1};
        if (inst.Count>2) throw new FormatException("Unsupported MSX instrument layout.");
        for(int c=0;c<input.chips.Length;c++)
        {
            var chip=input.chips[c];
            for(int p=0;p<chip.parts.Length;p++)
            {
                var pages=chip.parts[p].pages;
                if(c>=2 || c==1 && ((caps&1)==0 || p==10))
                {
                    if(pages.Any(page=>page.length>1))throw new FormatException("Unsupported chip/part in MSX MUC.");
                    continue;
                }
                parts[c*11+p].AddRange(pages);
            }
            if(c<2 && chip.instrumentNumber.Length>0)
            {
                instRef[c]=(int)chip.instrumentNumber[0];
            }
        }
        if(inst.Count==0)inst.Add(new byte[]{0});
        for(int p=0;p<11*((caps&1)!=0?2:1);p++)
            if(parts[p].Count==0)parts[p].Add(new(){length=1,loopPoint=-1,data=new[]{new MmlDatum(0)}});
        byte[] pcm=Array.Empty<byte>();
        if(input.chips[0].pcmNumber.Length>0)pcm=input.pcms[input.chips[0].pcmNumber[0]].data;
        byte[] tags=output.Skip((int)input.tagDataOffset).Take((int)input.tagDataSize).Select(x=>(byte)x.dat).ToArray();
        writer.Write(Encoding.ASCII.GetBytes((caps&2)!=0?"msPb0101":"msPb0100"));
        writer.Write((byte)5);writer.Write((byte)5);writer.Write((ushort)55);writer.Write((ushort)parts.Sum(p=>p.Count));
        writer.Write((ushort)inst.Count);writer.Write((ushort)1);writer.Write(0u);writer.Write((uint)tags.Length);
        writer.Write((caps&2)!=0?(uint)caps:0u);writer.Write(0u);
        for(int c=0;c<5;c++)
        {
            writer.Write((ushort)c);writer.Write(c<2?72u:c<4?76u:48u);writer.Write(c<2?7_987_200u:c<4?8_000_000u:3_579_545u);
            writer.Write(0u);writer.Write(1u);writer.Write(0u);writer.Write((byte)11);
            writer.Write((byte)(c<2 && instRef[c]>=0?1:0));if(c<2 && instRef[c]>=0)writer.Write((ushort)instRef[c]);
            writer.Write((byte)(c==0?1:0));if(c==0)writer.Write((ushort)0);
        }
        foreach(var part in parts)writer.Write((byte)part.Count);
        foreach(var page in parts.SelectMany(p=>p)){writer.Write(page.length);writer.Write(page.loopPoint);}
        foreach(var instrument in inst)writer.Write((uint)instrument.Length);writer.Write((uint)pcm.Length);
        foreach(var page in parts.SelectMany(p=>p))writer.Write(page.data.Select(x=>(byte)x.dat).ToArray());
        foreach(var instrument in inst)writer.Write(instrument);writer.Write(pcm);
        uint tagOffset=(uint)stream.Position;writer.Write(tags);writer.Flush();
        byte[] result=stream.ToArray();System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(18),tags.Length>0?tagOffset:0u);
        MsxMubProfile.Parse(result);return result;
    }
}
