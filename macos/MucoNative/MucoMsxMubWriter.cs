using System.Buffers.Binary;
using System.Text;

namespace MDPlayer.MucoNative;

// Writes MUB files in the exact layout of the MucoMSX toolchain (muc2mub), the
// reference for MUC output, from the one-OPNA MUB8 that the upstream compiler made.
//
//  * Classic score: MUB8 with jump line 0, the DATA block's final SSG-count byte
//    (00), and MucoMSX's tag block.
//  * Extended score: muPb0100 with five chip descriptors and 55 parts of which the
//    first OPNA's 11 parts carry data. Each part is one page, or ten pages (page 0
//    holds the data) when the classic-layout DATA would pass 60,000 bytes, where
//    MucoMSX retries in its wide format. The first tempo is placed at the head of
//    part A (FA 26 n) unless part A already starts with it. Voices are instrument 0
//    and the ADPCM data (possibly empty) is PCM 0.
//
// The tag block is the '#' rows of the source's whole header, each ending in CRLF
// (blank and ';' comment rows are not tags), for muPb followed by
// "#driver mucomDotNET", then a NUL and MucoMSX's fixed reproducibility block (no
// hash is computed).
public static class MucoMsxMubWriter
{
    private const int LegacyDataLimit = 60000;   // MucoMSX DATA_LIMIT
    private static readonly byte[] Trailer = Encoding.ASCII.GetBytes(
        "\0#mucver 1.7d\r\n#mmlhash 00000000000000000000000000000000\r\n" +
        "#uuid e494355c-68d1-40bb-b836-07b23ae0fb8f\r\n\0\0\0\0");
    private static readonly byte[] DriverTag = Encoding.ASCII.GetBytes("#driver mucomDotNET\r\n");

    // Chip descriptors: OPNA x2, OPNB x2, OPM; only the first refers to instrument 0
    // and PCM 0. Each: index16, identify32, clock32, option32, heartbeat32 x2,
    // part count8, instrument refs (count8 + 16-bit ids), PCM refs (count8 + ids).
    private static readonly byte[] Chips = BuildChips();

    private static byte[] BuildChips()
    {
        var chips = new List<byte>();
        (int identify, int clock)[] defs = { (72, 7_987_200), (72, 7_987_200), (76, 8_000_000), (76, 8_000_000), (48, 3_579_545) };
        for (int i = 0; i < defs.Length; i++)
        {
            Span<byte> head = stackalloc byte[22];
            BinaryPrimitives.WriteUInt16LittleEndian(head, (ushort)i);
            BinaryPrimitives.WriteInt32LittleEndian(head[2..], defs[i].identify);
            BinaryPrimitives.WriteInt32LittleEndian(head[6..], defs[i].clock);
            BinaryPrimitives.WriteInt32LittleEndian(head[10..], 0);
            BinaryPrimitives.WriteInt32LittleEndian(head[14..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(head[18..], 0);
            chips.AddRange(head.ToArray());
            chips.Add(11);
            if (i == 0) chips.AddRange(new byte[] { 1, 0, 0, 1, 0, 0 });
            else chips.AddRange(new byte[] { 0, 0 });
        }
        return chips.ToArray();
    }

    // MucoMSX tag text (TAG_WALK): the initial header is the leading rows that are
    // blank, ';' comments (after spaces/tabs) or start with '#' in column 1, read to
    // its end. Only its '#' rows are tags, each written as the row text and CRLF;
    // blank and comment rows are left out. Rows end at CR, LF or CRLF.
    public static byte[] HeaderText(byte[] source)
    {
        var text = new List<byte>();
        int position = 0;
        while (position < source.Length)
        {
            int end = position;
            while (end < source.Length && source[end] != '\n' && source[end] != '\r') end++;
            int first = position;
            while (first < end && (source[first] == ' ' || source[first] == '\t')) first++;
            if (first < end && source[first] != ';')
            {
                if (source[position] != '#') break;
                text.AddRange(source.AsSpan(position, end - position).ToArray());
                text.Add((byte)'\r');
                text.Add((byte)'\n');
            }
            position = end;
            if (position < source.Length && source[position] == '\r') position++;
            if (position < source.Length && source[position] == '\n') position++;
        }
        return text.ToArray();
    }

    // Tag block of an extended (muPb/msPb) MUB.
    public static byte[] ExtendedTags(byte[] source) => HeaderText(source).Concat(DriverTag).Concat(Trailer).ToArray();

    public static bool IsMub8(byte[] mub) => mub != null && mub.Length >= 0x50
        && mub[0] == 'M' && mub[1] == 'U' && mub[2] == 'B' && mub[3] == '8';

    private static (byte[] data, byte[] pcm, uint pcmOffset) Sections(byte[] mub)
    {
        uint dataOffset = BinaryPrimitives.ReadUInt32LittleEndian(mub.AsSpan(4));
        uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(mub.AsSpan(8));
        uint pcmOffset = BinaryPrimitives.ReadUInt32LittleEndian(mub.AsSpan(20));
        uint pcmSize = BinaryPrimitives.ReadUInt32LittleEndian(mub.AsSpan(24));
        byte[] data = mub.AsSpan((int)dataOffset, (int)dataSize).ToArray();
        byte[] pcm = pcmSize == 0 ? Array.Empty<byte>() : mub.AsSpan((int)pcmOffset, (int)pcmSize).ToArray();
        return (data, pcm, pcmOffset);
    }

    // True when MucoMSX would leave the classic layout (wide retry).
    public static bool ExceedsLegacyData(byte[] mub8) =>
        BinaryPrimitives.ReadUInt32LittleEndian(mub8.AsSpan(8)) + 1 > LegacyDataLimit;

    public static byte[] Classic(byte[] mub8, byte[] source)
    {
        var (data, pcm, pcmOffset) = Sections(mub8);
        byte[] tags = HeaderText(source).Concat(Trailer).ToArray();
        int dataSize = data.Length + 1;                    // final SSG-count byte
        int tagOffset = 0x50 + dataSize;
        byte[] result = new byte[tagOffset + tags.Length + pcm.Length];
        mub8.AsSpan(0, 0x50).CopyTo(result);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), 0x50);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), (uint)dataSize);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), (uint)tagOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), (uint)tags.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(20), pcmOffset == 0 ? 0u : (uint)(tagOffset + tags.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x1e), 0);    // jump line
        data.CopyTo(result, 0x50);
        tags.CopyTo(result, tagOffset);
        pcm.CopyTo(result, tagOffset + tags.Length);
        return result;
    }

    public static byte[] Extended(byte[] mub8, byte[] source)
    {
        var (data, pcm, _) = Sections(mub8);
        // DATA: tempo at +5, then 11 x (start16, loop16), addresses relative to -5.
        // Addresses are 16-bit; a song past 64 KiB wraps, so rebuild them in order.
        long[] start = new long[11];
        long high = 0, previous = -1;
        for (int p = 0; p < 11; p++)
        {
            long a = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6 + p * 4)) + high;
            if (a < previous) { high += 0x10000; a += 0x10000; }
            start[p] = a; previous = a;
        }
        long voices = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(1)) + (start[10] & ~0xffffL);
        if (voices < start[10]) voices += 0x10000;
        var pages = new List<(byte[] bytes, int loop)>();
        for (int p = 0; p < 11; p++)
        {
            long from = start[p] + 5, to = p < 10 ? start[p + 1] + 5 : voices;
            byte[] bytes = data.AsSpan((int)from, (int)(to - from)).ToArray();
            int loopRaw = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8 + p * 4));
            int loop = -1;
            if (loopRaw != 0)
            {
                long l = loopRaw + (start[p] & ~0xffffL);
                if (l < start[p]) l += 0x10000;
                loop = (int)(l + 5 - from);
            }
            pages.Add((bytes, loop));
        }
        byte tempo = data[5];
        var a0 = pages[0];
        if (!(a0.bytes.Length >= 3 && a0.bytes[0] == 0xfa && a0.bytes[1] == 0x26 && a0.bytes[2] == tempo))
            pages[0] = (new byte[] { 0xfa, 0x26, tempo }.Concat(a0.bytes).ToArray(), a0.loop >= 0 ? a0.loop + 3 : -1);
        byte[] instrument = data.AsSpan((int)voices).ToArray();

        int perPart = data.Length + 1 > LegacyDataLimit ? 10 : 1;
        var all = new List<(byte[] bytes, int loop)>();
        foreach (var page in pages)
        {
            all.Add(page);
            for (int i = 1; i < perPart; i++) all.Add((new byte[] { 0 }, -1));
        }
        byte[] tags = ExtendedTags(source);

        var header = new List<byte>();
        header.AddRange(Encoding.ASCII.GetBytes("muPb0100"));
        header.Add(5); header.Add(5);
        void U16(int v) { header.Add((byte)v); header.Add((byte)(v >> 8)); }
        void U32(long v) { for (int i = 0; i < 4; i++) header.Add((byte)(v >> (8 * i))); }
        U16(55); U16(all.Count); U16(1); U16(1);
        int tagFields = header.Count;
        U32(0); U32(0); U32(0); U32(0);                 // tag offset/size, jump count, jump line
        header.AddRange(Chips);
        for (int p = 0; p < 55; p++) header.Add((byte)(p < 11 ? perPart : 0));
        foreach (var page in all) { U32(page.bytes.Length); U32(page.loop); }
        U32(instrument.Length); U32(pcm.Length);
        long tagOffset = header.Count + all.Sum(x => (long)x.bytes.Length) + instrument.Length + pcm.Length;
        byte[] head = header.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(tagFields), (uint)tagOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(tagFields + 4), (uint)tags.Length);

        using MemoryStream output = new();
        output.Write(head);
        foreach (var page in all) output.Write(page.bytes);
        output.Write(instrument);
        output.Write(pcm);
        output.Write(tags);
        return output.ToArray();
    }
}
