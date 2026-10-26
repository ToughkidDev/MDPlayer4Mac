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
        string text=Encoding.Latin1.GetString(source);
        if(MsxMusicalCompiler.HasExtensions(text))
        {
            var score=MsxMusicalCompiler.Parse(text,asset);
            if(score!=null)return PackedMubContainer.Compile(score,asset,sourcePath);
        }
        if (MsxMucCompiler.IsNativeSource(source)) return MsxMucCompiler.Compile(source, sourcePath, asset);
        var compiler = new mucomDotNET.Compiler.Compiler();
        compiler.Init();
        using var input = new MemoryStream(MucSourceNormalization.NormalizeTrackDeclarationIndentation(source), false);
        MmlDatum[] output = compiler.Compile(input, name => asset(name));
        var info = compiler.GetCompilerInfo();
        if (output == null || info?.errorList?.Count > 0)
            throw new FormatException(info?.errorList?.Count > 0
                ? string.Join(Environment.NewLine, info.errorList.Select(x => x.Item3))
                : "MUCOM88 compilation failed.");
        return output.Select(x => x == null ? (byte)0 : (byte)x.dat).ToArray();
    }
}
