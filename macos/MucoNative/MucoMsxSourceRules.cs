using System.Text;

namespace MDPlayer.MucoNative;

// Source checks that the MucoMSX toolchain (muc2mub, MUCPLAY, MUCEDIT) makes before it
// compiles a MUC. MucoMSX is the reference for MUC output, so a source it refuses is
// refused here too, with the same code and message.
//
//  * code 19: a #pcm tag in the header names a PCM file that does not exist. The
//    compile does not start. A name with ? or * counts as not found. An empty value
//    is only a warning in MucoMSX ("Missing tag value") and is accepted. When there is
//    no #pcm tag, a missing default MUCOMPCM.BIN stays a warning, as before.
//  * code 17: a physical row (prefix, spaces, comments and tags included; CR/LF not
//    counted) is longer than 1023 bytes. muc2mub checks this in the preflight of its
//    legacy grammar; a score its probe hands to the MSX grammar (PSGS/OPLS rows, MSX or
//    SCC rows with ?/@ commands) is not checked. Here that is the packed path of
//    NativeMucCompiler, so callers check rows only for the other paths.
//
// Header rows follow muc2mub's SCAN_MUC_TAGS: rows end at CR, LF or CRLF; a row that
// is empty, blank or a ';' comment (after spaces/tabs) is skipped; a column-1 '#' row
// is a tag; any other row ends the header. The tag name is matched case-insensitively
// as a whole word (#PCMInvert is not #pcm). The value starts after spaces/tabs and
// ends at a space, tab or the end of the row (at most 63 characters).
public static class MucoMsxSourceRules
{
    public const int MaxRowBytes = 1023;

    /// <summary>Code 19 check. Returns the MucoMSX error text, or null.</summary>
    public static string CheckPcm(byte[] source, Func<string, Stream> asset)
        => source == null ? null : CheckPcmTags(source, SplitRows(source), asset);

    /// <summary>Code 17 check. Returns the MucoMSX error text, or null.</summary>
    public static string CheckRows(byte[] source)
    {
        if (source == null) return null;
        List<(int Start, int Length)> rows = SplitRows(source);
        for (int row = 0; row < rows.Count; row++)
            if (rows[row].Length > MaxRowBytes)
                return Diagnostic(source, rows[row], row + 1, 17, "Line/macro exceeds 1023 bytes");
        return null;
    }

    private static string CheckPcmTags(byte[] source, List<(int Start, int Length)> rows, Func<string, Stream> asset)
    {
        for (int row = 0; row < rows.Count; row++)
        {
            (int start, int length) = rows[row];
            int end = start + length;
            int p = start;
            while (p < end && (source[p] == ' ' || source[p] == '\t')) p++;
            if (p == end || source[p] == ';') continue;          // blank row or comment
            if (source[start] != '#') break;                     // first non-header row
            if (!IsTag(source, start + 1, end, "PCM", out int after)) continue;
            while (after < end && (source[after] == ' ' || source[after] == '\t')) after++;
            int nameEnd = after;
            while (nameEnd < end && nameEnd - after < 63 && source[nameEnd] != ' ' && source[nameEnd] != '\t') nameEnd++;
            if (nameEnd == after) continue;                      // empty value: warning only
            string name = Encoding.Latin1.GetString(source, after, nameEnd - after);
            if (name.IndexOf('?') < 0 && name.IndexOf('*') < 0 && Exists(asset, name)) continue;
            return Diagnostic(source, rows[row], row + 1, 19, "PCM file in #pcm tag not found");
        }
        return null;
    }

    private static bool IsTag(byte[] source, int p, int end, string name, out int after)
    {
        after = p;
        for (int i = 0; i < name.Length; i++, p++)
            if (p >= end || char.ToUpperInvariant((char)source[p]) != name[i]) return false;
        if (p < end && source[p] != ' ' && source[p] != '\t') return false;
        after = p;
        return true;
    }

    private static bool Exists(Func<string, Stream> asset, string name)
    {
        if (asset == null) return false;
        try
        {
            using Stream stream = asset(name);
            return stream != null;
        }
        catch { return false; }
    }

    private static List<(int Start, int Length)> SplitRows(byte[] source)
    {
        var rows = new List<(int, int)>();
        int start = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] != 13 && source[i] != 10) continue;
            rows.Add((start, i - start));
            if (source[i] == 13 && i + 1 < source.Length && source[i + 1] == 10) i++;
            start = i + 1;
        }
        if (start < source.Length) rows.Add((start, source.Length - start));
        return rows;
    }

    private static string Diagnostic(byte[] source, (int Start, int Length) row, int line, int code, string reason)
    {
        Encoding sjis;
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); sjis = Encoding.GetEncoding(932); }
        catch { sjis = Encoding.Latin1; }
        string text = sjis.GetString(source, row.Start, Math.Min(row.Length, 72));
        return $"Error line {line} (code {code}): {reason}\n> {text}";
    }
}
