using System.Buffers.Binary;
using System.Text;

namespace MDPlayer.Driver.MUCOM;

// A bounded reader for MUCOMSX's directory. Never let malformed counts reach the
// upstream MUBHeader allocator. In particular 0101 capability bytes are not JCLOCK.
public sealed class MsxMubProfile
{
    public byte Capabilities { get; private init; }
    public bool NativeGroup => (Capabilities & 1) != 0;
    public bool SccSsg => (Capabilities & 2) != 0;
    public bool DedicatedScc => (Capabilities & 4) != 0;
    public byte? RawFlags { get; private set; }
    public bool UsesScc => SccSsg || DedicatedScc || (RawFlags.GetValueOrDefault() & 4) != 0;
    public bool UsesSccPlus => SccSsg || DedicatedScc || (RawFlags.GetValueOrDefault() & 8) != 0;
    public bool UsesOpna { get; private set; }
    internal bool HasOpnaSequence { get; private set; }
    public bool UsesPsg { get; private set; }
    public bool UsesOpll { get; private set; }
    public byte[] SequencerData { get; private init; }
    public static bool HasMagic(byte[] data) => data?.Length >= 4 && data[0]=='m' && data[1]=='s' && data[2]=='P' && data[3]=='b';
    // Timer-B writes sequence every target, including native-only PSG/SCC scores.
    // Keep them in SequencerData, but do not add a silent OPNA to MDSound: its
    // automatic gain depends on the registered chip set. Only recognize this
    // narrow control-only grammar; non-timer writes, unknown instructions,
    // truncated commands and trailing bytes conservatively retain the chip.
    private static bool MayProduceOpnaSound(ReadOnlySpan<byte> page, int part, int loop)
    {
        // FA is a software-envelope command on SSG D/E/F, not a raw write.
        int at = 0;
        bool loopAtInstruction = loop == -1;
        while (at < page.Length)
        {
            // A header loop can re-enter at an operand and interpret it as a note.
            // Exclude OPNA only when every execution entry has been classified.
            if (at == loop) loopAtInstruction = true;
            byte code = page[at++];
            if (code == 0) return at != page.Length || !loopAtInstruction;
            if (code is >= 0x80 and < 0xf0) continue; // rest, encoded in one byte
            if (code == 0xfa && part is not (>= 3 and <= 5)
                && at + 2 <= page.Length && page[at] == 0x26)
            {
                at += 2; // port-0 Timer-B value; zero is a valid operand
                continue;
            }
            if (code == 0xff && at + 5 <= page.Length && page[at] == 0xf7
                && page[at + 1] == 0 && page[at + 2] == 0 && page[at + 3] == 0x26)
            {
                at += 5; // extended write: primary OPNA, port 0, Timer B
                continue;
            }
            return true;
        }
        return true; // no terminating opcode: leave validation to the driver
    }

    public static MsxMubProfile Parse(byte[] data)
    {
        if (!HasMagic(data) || data.Length < 34) throw new FormatException("Truncated msPb header.");
        string version = Encoding.ASCII.GetString(data, 4, 4);
        if (version != "0100" && version != "0101" && version != "0102") throw new FormatException("Unsupported msPb version.");
        byte caps = version == "0100" ? (byte)1 : data[26];
        if (version != "0100" && ((version == "0101" ? caps is not (2 or 3) : caps != 5) || data.AsSpan(27, 7).ContainsAnyExcept((byte)0)))
            throw new FormatException("Unknown msPb capabilities or reserved bytes.");
        int at = 8;
        uint Read(int size)
        {
            if (at > data.Length-size) throw new FormatException("Truncated msPb directory.");
            uint result = size == 1 ? data[at] : size == 2
                ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at,2))
                : BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at,4));
            at += size; return result;
        }
        int groups=(int)Read(1), chips=(int)Read(1), parts=(int)Read(2), pages=(int)Read(2), instruments=(int)Read(2), pcms=(int)Read(2);
        uint tag=Read(4), tagSize=Read(4); Read(4); Read(4);
        if (groups!=5 || chips!=5 || parts!=55 || pages>550 || instruments is <1 or >2 || pcms>6)
            throw new FormatException("Unsupported msPb directory counts.");
        for (int c=0;c<5;c++)
        {
            uint index=Read(2), id=Read(4), clock=Read(4), option=Read(4), beat=Read(4), beat2=Read(4);
            int partCount=(int)Read(1), instCount=(int)Read(1);
            if (index!=c || id!=(c<2?72u:c<4?76u:48u) || clock!=(c<2?7_987_200u:c<4?8_000_000u:3_579_545u)
                || option!=0 || beat!=1 || beat2!=0 || partCount!=11 || instCount>1)
                throw new FormatException("Unsupported msPb chip descriptor.");
            for(int i=0;i<instCount;i++) if(Read(2)>=instruments) throw new FormatException("Invalid instrument reference.");
            int pcmCount=(int)Read(1);
            if(pcmCount>2) throw new FormatException("Invalid PCM count.");
            for(int i=0;i<pcmCount;i++) if(Read(2)>=pcms || c==1) throw new FormatException("Invalid native PCM reference.");
        }
        int[] counts=new int[55];
        for(int i=0;i<counts.Length;i++)
        {
            counts[i]=(int)Read(1);
            if(counts[i]>10 || (i<11 || (caps&1)!=0 && i<22) && counts[i]==0)
                throw new FormatException("Invalid msPb page count.");
        }
        if(counts.Sum()!=pages) throw new FormatException("Inconsistent msPb page count.");
        (uint Size,int Loop)[] definitions=new (uint,int)[pages];
        for(int i=0;i<pages;i++)
        {
            uint size=Read(4);int loop=unchecked((int)Read(4));
            if(size is 0 or >60_000 || loop < -1 || loop>=size) throw new FormatException("Invalid msPb page extent.");
            definitions[i]=(size,loop);
        }
        uint[] blocks=new uint[instruments+pcms];
        for(int i=0;i<blocks.Length;i++) blocks[i]=Read(4);
        MsxMubProfile profile=new(){Capabilities=caps, SequencerData=(byte[])data.Clone()};
        int page=0; long musicSize=0;
        for(int part=0;part<55;part++) for(int p=0;p<counts[part];p++)
        {
            var definition=definitions[page++];
            if(definition.Size>(uint)(data.Length-at)) throw new FormatException("Truncated msPb page.");
            bool supported=part<11 || profile.NativeGroup && part<21 || profile.DedicatedScc && part is >=22 and <=26;
            if(!supported && (definition.Size!=1 || data[at]!=0 || definition.Loop!=-1))
                throw new FormatException("Music in an unsupported msPb part (including MSXK).");
            if(part<11)
            {
                profile.HasOpnaSequence |= definition.Size>1;
                profile.UsesOpna |= MayProduceOpnaSound(data.AsSpan(at,(int)definition.Size),part,definition.Loop);
            }
            else if(part<21 && definition.Size>1)
            {
                if(part is >=14 and <=16) profile.UsesPsg=true;
                else profile.UsesOpll=true;
            }
            if(part is >=11 and <21 && definition.Size>1)
            {
                byte? raw=MsxRawTransport.ReadFlags(data.AsSpan(at,(int)definition.Size),part-11);
                if(raw.HasValue)
                {
                    byte combined=profile.RawFlags.GetValueOrDefault();
                    // v1 owns OPLL unconditionally; v2 bit 1 declares its use.
                    if(combined==1 && raw.Value!=1)combined=19;
                    byte incoming=raw.Value==1 && combined>=16?(byte)19:raw.Value;
                    profile.RawFlags=(byte)(combined|incoming);
                }
            }
            musicSize+=definition.Size; at+=(int)definition.Size;
        }
        if(musicSize>122_880) throw new FormatException($"msPb music exceeds 120 KiB ({musicSize} bytes).");
        foreach(uint size in blocks)
        {
            if(size>(uint)(data.Length-at)) throw new FormatException("Truncated msPb instrument/PCM block.");
            at+=(int)size;
        }
        if(tagSize>0 && (tag<(uint)at || tagSize>data.Length || tag>data.Length-tagSize))
            throw new FormatException("Invalid msPb tags.");
        // Raw MSXA streams can drive PSG and all nine OPLL channels. Both are registered
        // whenever the native group carries music; chip metadata cannot describe raw writes.
        if(!profile.DedicatedScc && (profile.UsesPsg || profile.UsesOpll)) profile.UsesPsg=profile.UsesOpll=true;
        if(profile.RawFlags is byte rawFlags)
        {
            profile.UsesPsg=true;
            profile.UsesOpll=rawFlags==1 || (rawFlags&2)!=0;
            if((profile.SccSsg || profile.DedicatedScc) && (rawFlags&4)!=0)
                throw new FormatException("Raw SCC conflicts with SCC+ SSG.");
        }
        profile.SequencerData[1]=(byte)'u'; profile.SequencerData[7]=(byte)'0';
        if(version!="0100") profile.SequencerData.AsSpan(26,8).Clear();
        return profile;
    }
}
