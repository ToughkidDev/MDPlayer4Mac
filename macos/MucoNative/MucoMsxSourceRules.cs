using System.Text;

namespace MDPlayer.MucoNative;

// Source checks of the MucoMSX toolchain (muc2mub, MUCPLAY, MUCEDIT). MucoMSX is the
// reference for MUC output, so a source it refuses is refused here too, with the same
// code and message.
//
//  * code 19: the PCM file of the header's #pcm tag does not exist. The compile does
//    not start. A name with ? or * counts as not found. A #pcm tag without a name
//    selects MUCOMPCM.BIN, which must exist like any named file.
//  * code 20: the K part has a note but the header has no #pcm tag. MucoMSX finds this
//    at the end of the compile, so the message has no line. Without a #pcm tag
//    MUCOMPCM.BIN is never looked for (PcmAsset).
//  * code 16: a macro call on a track row is nested more than eight macros deep
//    (MucoMSX has eight macro slots).
//  * code 9: a score in the MSX grammar (the packed path of NativeMucCompiler) does
//    not fit one input window of the MucoMSX compiler (SourceWindows).
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
    public const int MaxMacroDepth = 8;
    public const int ClassicMacroDepth = 4;
    public const string DefaultPcmName = "MUCOMPCM.BIN";

    /// <summary>
    /// Finds a companion file (#pcm, #voice, ...) the way MSX-DOS would when the exact
    /// name is not in the folder: the same name in any letter case, then the name cut
    /// to 8.3 (MucoMSX opens a long name that way when the folder has no such long
    /// name). Only plain file names are looked up. Returns the full path, or null.
    /// The driver's own bank names for the chips MucoMSX does not have
    /// (mucompcm_2nd.bin, mucompcm_3rd_B.bin ...) are found in any letter case only:
    /// cut to 8.3 they all read MUCOMPCM.BIN, the first OPNA's bank, which would then be
    /// loaded as theirs.
    /// </summary>
    public static string FindCompanion(string folder, string name)
    {
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(name)) return null;
        if (name.IndexOfAny(new[] { '/', '\\', ':', '?', '*' }) >= 0) return null;
        string[] files;
        try { files = Directory.GetFiles(folder); }
        catch { return null; }
        string shortName = IsDriverBankName(name) ? null : ShortName(name);
        foreach (string wanted in new[] { name, shortName })
        {
            if (wanted == null) continue;
            string found = files.Where(f => string.Equals(Path.GetFileName(f), wanted, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).FirstOrDefault();
            if (found != null) return found;
        }
        return null;
    }

    // mucomDotNET's default bank files of OPNA #2 and the YM2610s (driver.cs
    // defaultPCMFileName), which the driver asks for whatever the song names.
    private static readonly string[] DriverBankNames =
    {
        "mucompcm_2nd.bin", "mucompcm_3rd_B.bin", "mucompcm_4th_B.bin", "mucompcm_3rd_A.bin", "mucompcm_4th_A.bin",
    };
    private static bool IsDriverBankName(string name)
        => DriverBankNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

    // The name as MSX-DOS reads it: eight characters, a dot and three; null when the
    // name already has that form.
    private static string ShortName(string name)
    {
        int dot = name.IndexOf('.');
        string stem = dot < 0 ? name : name[..dot], ext = dot < 0 ? string.Empty : name[(dot + 1)..];
        int next = ext.IndexOf('.');
        if (next >= 0) ext = ext[..next];
        if (stem.Length <= 8 && ext.Length <= 3 && next < 0) return null;
        stem = stem.Length > 8 ? stem[..8] : stem;
        ext = ext.Length > 3 ? ext[..3] : ext;
        return ext.Length == 0 ? stem : stem + "." + ext;
    }

    /// <summary>True when the header has a #pcm tag (with or without a name).</summary>
    public static bool HasPcmTag(byte[] source)
    {
        bool found = false;
        if (source != null) ScanPcmTags(source, SplitRows(source), (_, _) => { found = true; return false; });
        return found;
    }

    /// <summary>
    /// The companion-file callback a compile gets: without a #pcm tag the compiler's
    /// default PCM name is never opened; a #pcm tag without a name opens MUCOMPCM.BIN.
    /// </summary>
    public static Func<string, Stream> PcmAsset(byte[] source, Func<string, Stream> asset)
    {
        if (asset == null || HasPcmTag(source)) return asset;
        return name => string.Equals(name, DefaultPcmName, StringComparison.OrdinalIgnoreCase) ? null : asset(name);
    }

    /// <summary>Code 20 text when the K part has a note and there is no #pcm tag, or null.</summary>
    public static string CheckKPart(byte[] source, bool kPartHasNote)
        => kPartHasNote && !HasPcmTag(source) ? "Error, line unknown (code 20): K part needs a #pcm tag" : null;

    /// <summary>
    /// Runs a compile under the #pcm rules: PcmAsset for its files, then the code 20
    /// check. Returns the output, or null with the MucoMSX error text.
    /// </summary>
    public static byte[] CompileWithPcmRules(byte[] source, Func<string, Stream> asset,
        Func<Func<string, Stream>, byte[]> compile, out string refused)
    {
        int[] kNote = { 0 };
        Func<string, Stream> files = PcmAsset(source, asset);
        byte[] output = mucomDotNET.Compiler.Muc88.CapturePcmNote(kNote, () => compile(files));
        refused = output == null ? null : CheckKPart(source, kNote[0] != 0);
        return refused == null ? output : null;
    }

    /// <summary>
    /// The input windows MucoMSX's loader (LOAD_MUC) makes of a source: it reads
    /// 128-byte records (the last one padded) and, once a window holds 15,000 bytes
    /// or more, ends it after the last CR/LF of the record just read.
    /// </summary>
    public static int SourceWindows(byte[] source)
    {
        if (source == null) return 0;
        int windows = 1, held = 0;
        for (int at = 0; at < source.Length; at += 128)
        {
            int length = Math.Min(128, source.Length - at);
            held += 128;
            if (held < 15000) continue;
            int last = -1;
            for (int i = 0; i < length; i++)
                if (source[at + i] == 13 || source[at + i] == 10) last = i;
            if (last < 0) continue;
            windows++;
            held = 128 - (last + 1);
        }
        return windows;
    }

    /// <summary>MucoMSX's message for any error of a score in the MSX grammar.</summary>
    public const string MsxGrammarError = "Error, line unknown (code 9): Command not valid for this channel";

    /// <summary>Code 9 check for a score in the MSX grammar. Returns the MucoMSX error text, or null.</summary>
    public static string CheckSourceWindow(byte[] source)
        => SourceWindows(source) > 1 ? MsxGrammarError : null;

    /// <summary>Code 16 check. Returns the MucoMSX error text, or null.</summary>
    public static string CheckMacroDepth(byte[] source)
    {
        if (source == null) return null;
        List<(int Start, int Length)> rows = SplitRows(source);
        int row = MacroCallDepth(source, rows, MaxMacroDepth, out _);
        return row < 0 ? null : Diagnostic(source, rows[row], row + 1, 16, "Invalid macro definition");
    }

    /// <summary>
    /// The deepest macro nesting a track row calls (0 without macro calls). MUCOM88
    /// nests four deep; MucoMSX writes a score that goes deeper in its extended format.
    /// </summary>
    public static int MacroCallDepth(byte[] source)
    {
        if (source == null) return 0;
        MacroCallDepth(source, SplitRows(source), int.MaxValue, out int deepest);
        return deepest;
    }

    // Returns the first track row whose macro call nests deeper than `limit` (-1 when
    // there is none); `deepest` is the deepest call seen up to there.
    private static int MacroCallDepth(byte[] source, List<(int Start, int Length)> rows, int limit, out int deepest)
    {
        deepest = 0;
        var bodies = new Dictionary<int, List<int>>();
        var calls = new List<(int Row, List<int> Ids)>();
        for (int row = 0; row < rows.Count; row++)
        {
            (int start, int length) = rows[row];
            int end = start + length;
            if (length == 0) continue;
            if (source[start] == '#')
            {
                int p = start + 1;
                while (p < end && (source[p] == ' ' || source[p] == '\t')) p++;
                if (p >= end || source[p] != '*') continue;
                int id = Number(source, ref p, end, 1);
                while (p < end && (source[p] == ' ' || source[p] == '\t')) p++;
                if (id < 0 || p >= end || source[p] != '{') continue;
                bodies[id] = MacroCalls(source, p + 1, end);
                continue;
            }
            if (source[start] < 'A' || source[start] > 'Z') continue;   // column-1 track label
            int body = start;
            while (body < end && source[body] != ' ' && source[body] != '\t') body++;
            if (body >= end) continue;
            List<int> ids = MacroCalls(source, body, end);
            if (ids.Count > 0) calls.Add((row, ids));
        }
        if (bodies.Count == 0) return -1;
        var depth = new Dictionary<int, int>();
        int Depth(int id, int guard)
        {
            if (depth.TryGetValue(id, out int known)) return known;
            if (!bodies.TryGetValue(id, out List<int> inner) || guard > 64) return 1;
            int deepest = 0;
            foreach (int next in inner) deepest = Math.Max(deepest, Depth(next, guard + 1));
            return depth[id] = deepest + 1;
        }
        foreach ((int row, List<int> ids) in calls)
            foreach (int id in ids)
            {
                if (!bodies.ContainsKey(id)) continue;
                deepest = Math.Max(deepest, Depth(id, 0));
                if (deepest > limit) return row;
            }
        return -1;
    }

    // Macro ids called (*n) in source[p..end), up to a ';' comment. A voice name in
    // quotes is skipped.
    private static List<int> MacroCalls(byte[] source, int p, int end)
    {
        var ids = new List<int>();
        bool quoted = false;
        for (; p < end; p++)
        {
            byte c = source[p];
            if (c == '"') { quoted = !quoted; continue; }
            if (quoted) continue;
            if (c == ';') break;
            if (c != '*') continue;
            int q = p;
            int id = Number(source, ref q, end, 1);
            if (id >= 0) { ids.Add(id); p = q - 1; }
        }
        return ids;
    }

    private static int Number(byte[] source, ref int p, int end, int skip)
    {
        int q = p + skip;
        while (q < end && (source[q] == ' ' || source[q] == '\t')) q++;
        if (q >= end || source[q] < '0' || source[q] > '9') return -1;
        int value = 0;
        while (q < end && source[q] >= '0' && source[q] <= '9') { value = Math.Min(value * 10 + (source[q] - '0'), 1 << 20); q++; }
        p = q;
        return value;
    }

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
        string refused = null;
        ScanPcmTags(source, rows, (row, name) =>
        {
            if (name.Length == 0) name = DefaultPcmName;         // #pcm without a name
            if (name.IndexOf('?') < 0 && name.IndexOf('*') < 0 && Exists(asset, name)) return true;
            refused = Diagnostic(source, rows[row], row + 1, 19, "PCM file in #pcm tag not found");
            return false;
        });
        return refused;
    }

    // Calls visit(row, name) for each #pcm tag of the header until it returns false.
    private static void ScanPcmTags(byte[] source, List<(int Start, int Length)> rows, Func<int, string, bool> visit)
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
            if (!visit(row, Encoding.Latin1.GetString(source, after, nameEnd - after))) return;
        }
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
