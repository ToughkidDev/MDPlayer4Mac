using System.Text;
using System.Text.RegularExpressions;

namespace MDPlayer.MucoNative;

// The managed MUCOM compiler reads its traditional fixed-column FM format.
// Adapt complete braced definitions at the mixed-container boundary; do not
// provide an empty voice bank that could silently replace a missing voice.
internal static class InlineFmDefinitions
{
    internal static string NormalizeBraces(string source)
    {
        string clean=Regex.Replace(source,@";[^\r\n]*",m=>new string(' ',m.Length));
        var output=new StringBuilder();int copied=0;
        foreach(Match header in Regex.Matches(clean,@"(?m)^[ \t]*@(%?)(\d+)[ \t]*:?[ \t]*\{"))
        {
            int id=int.Parse(header.Groups[2].Value);
            if(id>255)throw new FormatException("FM voice number outside 0..255.");
            int start=header.Index+header.Length,end=clean.IndexOf('}',start);
            if(end<0)throw new FormatException("Incomplete braced FM voice "+id);
            string body=clean[start..end];var numbers=Regex.Matches(body,@"\$[0-9a-fA-F]+|[+-]?\d+");
            if(Regex.Replace(body,@"\$[0-9a-fA-F]+|[+-]?\d+|[\s,]","").Length!=0||numbers.Count!=(header.Groups[1].Value=="%"?25:38))
                throw new FormatException("Invalid parameter list for braced FM voice "+id);
            int[] values=numbers.Select(m=>(m.Value[0]=='$'?Convert.ToInt32(m.Value[1..],16):int.Parse(m.Value))&255).ToArray();
            byte[] packed=MsxMusicalCompiler.PackFmVoice(values);
            output.Append(source,copied,header.Index-copied).Append("  @%").Append(id).Append('\n');
            for(int row=0;row<6;row++)output.Append(' ').AppendJoin(',',packed.Skip(row*4).Take(4)).Append('\n');
            output.Append("  ").Append(packed[24]).Append('\n');copied=end+1;
        }
        return copied==0?source:output.Append(source,copied,source.Length-copied).ToString();
    }
}
