using System.Buffers.Binary;
using System.Text;
using MDPlayer.MucoNative;

namespace MDPlayer.Driver.MUCOM;

public sealed partial class MsxMubProfile
{
    private static MsxMubProfile ReadPacked(byte[] data,string version)
    {
        uint expected=version switch{"0103"=>21,"0104"=>53,"0105"=>23,"0106"=>55,"0107"=>151,"0108"=>183,"0109"=>407,_=>439};
        uint caps=BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(26));
        if(caps!=expected||data.AsSpan(30,4).ContainsAnyExcept((byte)0))throw new FormatException("Unknown packed msPb capabilities or reserved bytes.");
        int at=8;
        uint Read(int size)
        {
            if(at>data.Length-size)throw new FormatException("Truncated packed msPb directory.");
            uint n=size==1?data[at]:size==2?BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at,2)):BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at,4));at+=size;return n;
        }
        int groups=(int)Read(1),chips=(int)Read(1),parts=(int)Read(2),pages=(int)Read(2),instruments=(int)Read(2),pcms=(int)Read(2);
        uint tag=Read(4),tagSize=Read(4);Read(4);Read(4);
        if(groups!=5||chips!=5||parts!=55||pages is <1 or >551||instruments is <1 or >2||pcms>6)throw new FormatException("Invalid packed msPb counts.");
        for(int c=0;c<5;c++)
        {
            uint index=Read(2),id=Read(4),clock=Read(4),option=Read(4),beat=Read(4),beat2=Read(4);int partCount=(int)Read(1),instCount=(int)Read(1);
            if(index!=c||id!=(c<2?72u:c<4?76u:48u)||clock!=(c<2?7_987_200u:c<4?8_000_000u:3_579_545u)||option!=0||beat!=1||beat2!=0||partCount!=11||instCount>1)throw new FormatException("Invalid packed chip descriptor.");
            for(int i=0;i<instCount;i++)if(Read(2)>=instruments)throw new FormatException("Invalid packed instrument reference.");
            int count=(int)Read(1);if(count>2)throw new FormatException("Invalid packed PCM count.");for(int i=0;i<count;i++)if(Read(2)>=pcms||c==1)throw new FormatException("Invalid packed PCM reference.");
        }
        int countsAt=at;int[] counts=new int[55];for(int p=0;p<55;p++){counts[p]=(int)Read(1);if(counts[p]>(p==0?11:10)||p<11&&counts[p]==0)throw new FormatException("Invalid packed page count.");}
        if(counts.Sum()!=pages)throw new FormatException("Inconsistent packed page count.");
        var definitions=new (int Size,int Loop)[pages];for(int i=0;i<pages;i++)
        {
            uint size=Read(4);int loop=unchecked((int)Read(4));if(size is 0 or >60000||loop< -1||loop>=size)throw new FormatException("Invalid packed page extent.");definitions[i]=((int)size,loop);
        }
        uint[] blockSizes=new uint[instruments+pcms];for(int i=0;i<blockSizes.Length;i++)blockSizes[i]=Read(4);
        var payload=new List<PackedMubContainer.Page>();int pageIndex=0;PackedScoreData packed=null;int timer=198;bool usesOpna=false,hasOpna=false;
        for(int part=0;part<55;part++)for(int p=0;p<counts[part];p++)
        {
            var def=definitions[pageIndex++];if(def.Size>data.Length-at)throw new FormatException("Truncated packed page.");var bytes=data.AsSpan(at,def.Size);at+=def.Size;
            if(part==0&&p==0)
            {
                if(bytes.Length<29||bytes[0]!=0xfa||bytes[1]!=0x26||def.Loop!=-1)throw new FormatException("Missing typed musical page.");timer=bytes[2];packed=new(bytes[3..].ToArray());
                int expectedVersion=version is "0103" or "0104"?1:version is "0105" or "0106"?3:version is "0107" or "0108"?4:5;
                if(packed.Version<expectedVersion||packed.Version>(expectedVersion==1?2:expectedVersion==5?6:expectedVersion))throw new FormatException("Packed object and container profile disagree.");
                continue;
            }
            if(part>=11&&(def.Size!=1||bytes[0]!=0||def.Loop!=-1))throw new FormatException("Packed container has music outside primary OPNA and its typed MSX page.");
            if(part<11){hasOpna|=def.Size>1;usesOpna|=MayProduceOpnaSound(bytes,part,def.Loop);}
            payload.Add(new(bytes.ToArray(),def.Loop));
        }
        if(packed==null)throw new FormatException("Missing packed score.");
        if((caps&32)==0&&hasOpna)throw new FormatException("Standalone packed profile contains an OPNA sequence.");
        var blocks=new List<byte[]>();foreach(uint size in blockSizes){if(size>(uint)(data.Length-at))throw new FormatException("Truncated packed resource block.");blocks.Add(data.AsSpan(at,(int)size).ToArray());at+=(int)size;}
        if(tagSize>0&&(tag<(uint)at||tagSize>data.Length||tag>data.Length-tagSize))throw new FormatException("Invalid packed tags.");
        // Retire the typed A page before the upstream OPNA sequencer sees it.
        // Original page numbers and event bytes then retain their old meaning.
        counts[0]--;if(counts[0]==0){counts[0]=1;payload.Insert(0,new(new byte[]{0}));}
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream,Encoding.ASCII,true);
        byte[] prefix=data.AsSpan(0,countsAt).ToArray();Encoding.ASCII.GetBytes("muPb0100").CopyTo(prefix,0);prefix.AsSpan(26,8).Clear();BinaryPrimitives.WriteUInt16LittleEndian(prefix.AsSpan(12),(ushort)payload.Count);writer.Write(prefix);
        foreach(int n in counts)writer.Write((byte)n);foreach(var page in payload){writer.Write((uint)page.Bytes.Length);writer.Write(page.Loop);}foreach(uint size in blockSizes)writer.Write(size);
        foreach(var page in payload)writer.Write(page.Bytes);foreach(byte[] block in blocks)writer.Write(block);
        int newTag=(int)stream.Position;if(tagSize>0)writer.Write(data.AsSpan((int)tag,(int)tagSize));writer.Flush();byte[] sequencer=stream.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(sequencer.AsSpan(18),tagSize>0?(uint)newTag:0);
        return new(){Capabilities=(byte)caps,ExtendedCapabilities=caps,Packed=packed,PackedTimer=timer,UsesPsg=packed.UsesPsg,UsesOpll=packed.UsesOpll,UsesOpna=usesOpna,HasOpnaSequence=hasOpna,SequencerData=sequencer};
    }
}
