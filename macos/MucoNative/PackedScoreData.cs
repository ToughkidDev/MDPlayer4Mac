using System.Buffers.Binary;
using System.Text;

namespace MDPlayer.MucoNative;

internal sealed class PackedScoreData
{
    internal byte[] Bytes {get;}
    internal int Version {get;}
    internal int CodeStart {get;}
    internal int CodeEnd {get;}
    internal int[] Routes {get;}
    internal byte[][] Waves {get;}
    internal int[] Rhythms {get;}=new int[10];
    internal byte[][] Patches {get;}=new byte[16][];
    internal byte[][] Drums {get;}=new byte[16][];
    internal Dictionary<int,ushort[]> PitchTables {get;}=new();
    internal Dictionary<int,int[]> InstrumentMaps {get;}=new();
    internal bool UsesPsg=>Routes.Take(3).Any(x=>Bytes[x]!=255);
    internal bool UsesScc=>Routes.Skip(3).Take(5).Any(x=>Bytes[x]!=255);
    internal bool UsesOpll=>Routes.Skip(8).Any(x=>Bytes[x]!=255);

    internal PackedScoreData(byte[] source)
    {
        if(source==null||source.Length is <26 or >4096||source[0]!='P'||source[1]!='N'||source[2] is <(byte)'1' or >(byte)'6'||source[3]!=0)throw new FormatException("Invalid packed musical object header.");
        Bytes=(byte[])source.Clone();Version=source[2]-'0';
        if(U16(4)!=Bytes.Length||Version==1&&Bytes.Length>2560||U16(6)!=26)throw new FormatException("Invalid packed musical extent.");
        int rhythm=U16(8),firstWave=U16(26),count=(firstWave-26)/2;
        if(firstWave<=26||(firstWave-26)%2!=0||count is <1 or >64)throw new FormatException("Invalid SCC wave directory.");
        Waves=new byte[count][];int end=firstWave;
        for(int i=0;i<count;i++)
        {
            int ptr=U16(26+2*i);Check(ptr,32);if(ptr<firstWave||ptr+32>rhythm)throw new FormatException("Wave points outside its resource area.");
            for(int j=0;j<i;j++){int other=U16(26+2*j);if(ptr!=other&&ptr<other+32&&other<ptr+32)throw new FormatException("Overlapping waveform resources.");}
            Waves[i]=Bytes.AsSpan(ptr,32).ToArray();end=Math.Max(end,ptr+32);
        }
        if(end!=rhythm)throw new FormatException("Invalid rhythm directory placement.");Check(rhythm,20);for(int i=0;i<10;i++)Rhythms[i]=U16(rhythm+2*i);
        end=rhythm+20;Routes=new int[Version>=3?15:8];for(int i=0;i<8;i++)Routes[i]=U16(10+2*i);
        if(Version>=3)
        {
            int dir=end;Check(dir,82);if(Encoding.ASCII.GetString(Bytes,dir,4)!="OL1\0")throw new FormatException("Missing OPLL directory.");end+=82;
            for(int i=0;i<7;i++)Routes[8+i]=U16(dir+4+2*i);
            for(int i=0;i<16;i++)
            {
                int patch=U16(dir+18+2*i);if(patch!=0){Check(patch,8);if(patch<dir+82)throw new FormatException("Invalid OPLL patch pointer.");Patches[i]=Bytes.AsSpan(patch,8).ToArray();end=Math.Max(end,patch+8);}
                int drum=U16(dir+50+2*i);if(drum!=0){Check(drum,6);if(drum<dir+82)throw new FormatException("Invalid OPLL drum pointer.");Drums[i]=Bytes.AsSpan(drum,6).ToArray();end=Math.Max(end,drum+6);}
            }
        }
        CodeStart=end;CodeEnd=Version==6?U16(Bytes.Length-2):Bytes.Length;
        if(CodeEnd<=CodeStart||CodeEnd>Bytes.Length-(Version==6?8:0))throw new FormatException("Invalid musical event region.");
        foreach(int ptr in Routes)CodePointer(ptr);foreach(int ptr in Rhythms)if(ptr!=0)CodePointer(ptr);
        if(Version==6)
        {
            int ex=CodeEnd;Check(ex,6);if(Encoding.ASCII.GetString(Bytes,ex,4)!="EX1\0")throw new FormatException("Missing PN6 extension directory.");
            int pitches=Bytes[ex+4],instruments=Bytes[ex+5];if(pitches>16||instruments>16)throw new FormatException("Invalid PN6 table count.");
            int descriptorEnd=ex+6+(pitches+instruments)*4;Check(ex+6,(pitches+instruments)*4);
            for(int i=0;i<pitches+instruments;i++)
            {
                int at=ex+6+4*i,id=Bytes[at],length=Bytes[at+1],ptr=U16(at+2);if(id>15||length is <12 or >128||ptr<descriptorEnd||ptr+2*length>Bytes.Length-2)throw new FormatException("Invalid PN6 table extent.");
                if(i<pitches){var values=new ushort[length];for(int n=0;n<length;n++)values[n]=(ushort)U16(ptr+2*n);if(!PitchTables.TryAdd(id,values))throw new FormatException("Duplicate pitch-table ID.");}
                else{var values=new int[length];for(int n=0;n<length;n++){values[n]=U16(ptr+2*n);CodePointer(values[n]);}if(!InstrumentMaps.TryAdd(id,values))throw new FormatException("Duplicate instrument-map ID.");}
            }
        }
    }
    internal void Check(int at,int size=1) {if(at<0||size<0||at>Bytes.Length-size)throw new FormatException("Truncated musical data.");}
    internal int U16(int at) {Check(at,2);return BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan(at,2));}
    internal void CodePointer(int at) {if(at<CodeStart||at>=CodeEnd)throw new FormatException($"Musical event pointer {at} is outside the event region.");}
    internal int Read(ref int at) {CodePointer(at);return Bytes[at++];}
    internal int ReadWord(ref int at) {int lo=Read(ref at);return lo|(Read(ref at)<<8);}
}
