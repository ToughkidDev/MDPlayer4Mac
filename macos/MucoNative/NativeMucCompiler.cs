using System.Text;
using MDPlayer.Driver.MUCOM;
using musicDriverInterface;

namespace MDPlayer.MucoNative;

/// <summary>
/// MDPlayer4Mac's in-process compiler. No emulator, child process, game image,
/// COM/overlay file, or toolchain installation is used by this assembly.
/// Companion assets are supplied by the host rather than opened implicitly.
/// </summary>
public static class NativeMucCompiler
{
    public static byte[] Compile(byte[] source, Func<string, Stream> asset = null, string sourcePath = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        int start = source.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
        int eof = Array.IndexOf(source, (byte)0x1a, start);
        source = source.AsSpan(start, (eof < 0 ? source.Length : eof) - start).ToArray();
        if (source.Length == 0) throw new FormatException("MUC source is empty.");
        // Shift-JIS is also used by compiler diagnostics and FM-bank tags.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        asset ??= _ => null;
        string refused = MucoMsxSourceRules.CheckPcm(source, asset);
        if (refused != null) throw new FormatException(refused);
        string text=Encoding.Latin1.GetString(source);
        if(MsxMusicalCompiler.HasExtensions(text))
        {
            // MucoMSX reports every error of the MSX grammar as code 9 without a line;
            // the reason found here follows on the next row.
            MsxMusicalCompiler.Compilation score;
            try { score=MsxMusicalCompiler.Parse(text,asset); }
            catch(FormatException e) { throw new FormatException(MucoMsxSourceRules.MsxGrammarError+"\n"+e.Message,e); }
            if(score!=null)
            {
                // MucoMSX reads an MSX-grammar score into one input window only, and
                // its OPNA rows keep the macro nesting limit.
                refused = MucoMsxSourceRules.CheckSourceWindow(source) ?? MucoMsxSourceRules.CheckMacroDepth(source);
                if (refused != null) throw new FormatException(refused);
                return WithPcmRules(source, asset, files => PackedMubContainer.Compile(score,files,sourcePath));
            }
        }
        // Like muc2mub's legacy-grammar preflight: not for packed (MSX grammar) scores.
        refused = MucoMsxSourceRules.CheckRows(source) ?? MucoMsxSourceRules.CheckMacroDepth(source);
        if (refused != null) throw new FormatException(refused);
        if (MsxMucCompiler.IsNativeSource(source))
            return WithPcmRules(source, asset, files => MsxMucCompiler.Compile(source, sourcePath, files));
        byte[] output = MucomCompileMode.Compile(source, name => asset(name), out string error);
        if (output == null)
            throw new FormatException(string.IsNullOrEmpty(error) ? "MUCOM88 compilation failed." : error);
        return output;
    }

    private static byte[] WithPcmRules(byte[] source, Func<string, Stream> asset, Func<Func<string, Stream>, byte[]> compile)
    {
        byte[] output = MucoMsxSourceRules.CompileWithPcmRules(source, asset, compile, out string refused);
        if (refused != null) throw new FormatException(refused);
        return output;
    }
}
