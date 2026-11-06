using System.Text;
using musicDriverInterface;

namespace MDPlayer.MucoNative;

// Output-mode rule for MUC sources compiled by the upstream mucomDotNET compiler,
// matching the MucoMSX toolchain (muc2mub), which is the reference for MUC output.
//
// MucoMSX writes a classic MUCOM88 v1.7 MUB8 unless the score uses a mucomDotNET
// extension; only then does it switch to the extended format. The upstream compiler
// defaults to its DotNet driver mode for every source, which changes playback of
// classic scores: the driver runs in DotNet mode, SSG/FM tempo becomes FF F7
// register events, FM3 slot detune takes 16-bit operands, and so on. A classic score
// is therefore compiled as "#driver mucom88" here, exactly as if the source said so.
//
// A source stays in DotNet mode when it
//  * names a driver itself (#driver ...), or uses a DotNet-only header tag;
//  * has a track label beyond MUCOM88's A-K, or a page digit (A0, B1, ...);
//  * uses portamento braces on a pitched track (MucoMSX: PTM_EXTENDED); or
//  * does not compile in classic mode at all (the upstream compiler rejects
//    DotNet-only commands there).
public static class MucomCompileMode
{
    private static readonly byte[] ClassicTag = Encoding.ASCII.GetBytes("#driver mucom88\r\n");

    private static readonly string[] DotNetTags =
    {
        "driver", "carriercorrection", "ssgextend",
        "pcm_2nd", "pcm_3rd_b", "pcm_4th_b", "pcm_3rd_a", "pcm_4th_a",
        "@pcm_2nd", "@pcm_3rd_b", "@pcm_4th_b", "@pcm_3rd_a", "@pcm_4th_a",
    };

    public static bool PrefersClassic(byte[] source)
    {
        string text = Encoding.Latin1.GetString(source);
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            if (line[0] == '#')
            {
                int end = 1;
                while (end < line.Length && line[end] != ' ' && line[end] != '\t') end++;
                string name = line.Substring(1, end - 1).ToLowerInvariant();
                if (Array.IndexOf(DotNetTags, name) >= 0) return false;
                continue;
            }
            // Only a column-1 upper-case label is a track row (original MUCOM88 rule).
            if (line[0] < 'A' || line[0] > 'Z') continue;
            int labelEnd = 0;
            while (labelEnd < line.Length && line[labelEnd] != ' ' && line[labelEnd] != '\t') labelEnd++;
            if (labelEnd >= line.Length) continue;       // a label needs a following blank
            string label = line.Substring(0, labelEnd);
            foreach (char c in label)
                if (c < 'A' || c > 'K') return false;  // L-Z parts, MSX/SCC rows, page digits
            string body = line.Substring(labelEnd);
            int comment = body.IndexOf(';');
            if (comment >= 0) body = body.Substring(0, comment);
            if (body.IndexOf('{') >= 0 && label.Replace("G", "").Length > 0) return false;
        }
        return true;
    }

    // Compile with the upstream compiler using the MucoMSX mode rule above.
    // Returns null and the compiler's messages when the source does not compile.
    public static byte[] Compile(byte[] source, Func<string, Stream> asset, out string error)
    {
        if (PrefersClassic(source))
        {
            byte[] classic = CompileOnce(ClassicTag.Concat(source).ToArray(), asset, out _);
            if (classic != null)
            {
                error = string.Empty;
                return RemoveClassicTag(classic);
            }
        }
        return CompileOnce(source, asset, out error);
    }

    private static byte[] CompileOnce(byte[] source, Func<string, Stream> asset, out string error)
    {
        error = string.Empty;
        try
        {
            var compiler = new mucomDotNET.Compiler.Compiler();
            compiler.Init();
            using MemoryStream input = new(source, writable: false);
            MmlDatum[] output = compiler.Compile(input, name => asset?.Invoke(name));
            var info = compiler.GetCompilerInfo();
            if (output == null || info?.errorList?.Count > 0)
            {
                error = info?.errorList?.Count > 0
                    ? string.Join(Environment.NewLine, info.errorList.Select(x => x.Item3))
                    : "MUCOM88 compilation failed.";
                return null;
            }
            return output.Select(x => x == null ? (byte)0 : (byte)x.dat).ToArray();
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return null;
        }
    }

    // The mode tag added above is not part of the score: drop it from the MUB8 tag
    // block so the written tags are the source's own (the driver then defaults to
    // classic MUCOM88 playback, as for a MUB8 without a #driver tag).
    private static byte[] RemoveClassicTag(byte[] mub)
    {
        if (mub.Length < 0x20 || mub[0] != 'M' || mub[1] != 'U' || mub[2] != 'B' || mub[3] != '8') return mub;
        uint tagOffset = BitConverter.ToUInt32(mub, 12), tagSize = BitConverter.ToUInt32(mub, 16);
        uint pcmOffset = BitConverter.ToUInt32(mub, 20);
        if (tagOffset == 0 || tagSize < ClassicTag.Length || tagOffset + tagSize > mub.Length) return mub;
        int at = mub.AsSpan((int)tagOffset, (int)tagSize).IndexOf(ClassicTag);
        if (at < 0) return mub;
        at += (int)tagOffset;
        byte[] result = new byte[mub.Length - ClassicTag.Length];
        mub.AsSpan(0, at).CopyTo(result);
        mub.AsSpan(at + ClassicTag.Length).CopyTo(result.AsSpan(at));
        BitConverter.GetBytes(tagSize - (uint)ClassicTag.Length).CopyTo(result, 16);
        if (pcmOffset > tagOffset) BitConverter.GetBytes(pcmOffset - (uint)ClassicTag.Length).CopyTo(result, 20);
        return result;
    }
}
