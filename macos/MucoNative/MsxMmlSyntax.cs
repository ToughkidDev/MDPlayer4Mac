using System.Text;
using System.Text.RegularExpressions;

namespace MDPlayer.MucoNative;

internal static class MsxMmlSyntax
{
    internal static readonly string[] BaseChannels={"MSXD","MSXE","MSXF","SCCA","SCCB","SCCC","SCCD","SCCE"};
    internal static readonly string[] OpllChannels={"MSXA","MSXB","MSXC","MSXH","MSXI","MSXJ","MSXG"};
    internal static readonly string[] Channels=BaseChannels.Concat(OpllChannels).ToArray();
    private static readonly HashSet<string> Functional=new(("EP EA ET EN EL DB DW DR DS MV MW QR QP NP GS GH GL WD WS WM WP IR IU IT IS ID IB FD FC FR FJ FB XD XM XI XS XP XT XU KT IM XH XC XR VF WI WF WE QE WG").Split(' '));
    private static readonly Dictionary<string,string> Simple=new()
    {
        ["DB"]="d",["DS"]="g",["MW"]="h",["QP"]="q",["NP"]="n",["GH"]="H",["GL"]="L",["WM"]="w",["IR"]="I",["IU"]="O",["IT"]="T",["IS"]="s",["ID"]="D",["IB"]="B",["FC"]="p",["FR"]="r",["FJ"]="j",["FB"]="b",["XM"]="x",["XI"]="i",["XS"]="z",["XP"]="f",["XT"]="k",["XU"]="u"
    };
    internal static string NormalizeHex(string text,bool resources=false)
    {
        var replacements=new SortedDictionary<int,(int End,string Value)>();
        void HexAt(ref int at)
        {
            int start=at;var match=Regex.Match(text[at..],@"^[+-]?\$[0-9a-fA-F]+");
            if(!match.Success)throw new FormatException("Hexadecimal digits required after $.");
            at+=match.Length;
            if(at<text.Length&&(char.IsAsciiLetterOrDigit(text[at])||text[at] is '_' or '.' or '$'))throw new FormatException("Invalid hexadecimal literal: "+match.Value);
            string token=match.Value;int sign=token.StartsWith('-')?-1:1;int dollar=token.IndexOf('$');
            long value=Convert.ToInt64(token[(dollar+1)..],16);if(value>65535)throw new FormatException("MSX hexadecimal literal exceeds 16 bits.");
            replacements[start]=(at,(sign*value).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if(resources)foreach(Match m in Regex.Matches(text,@"[+-]?\$")){int at=m.Index;HexAt(ref at);}
        else foreach(Match m in Regex.Matches(text,@"[?@](?:[A-Z]{2}(?![A-Za-z])|[A-Za-z])|:"))
        {
            int at=m.Index+m.Length;
            while(true)
            {
                while(at<text.Length&&char.IsWhiteSpace(text[at]))at++;
                int start=at;if(at<text.Length&&text[at] is '+' or '-')at++;
                if(at<text.Length&&text[at]=='$'){at=start;HexAt(ref at);}
                else {var n=Regex.Match(text[start..],@"^[+-]?\d+");if(n.Success)at=start+n.Length;else if(text[start..at]!="-")break;}
                while(at<text.Length&&char.IsWhiteSpace(text[at]))at++;
                if(at==text.Length||text[at]!=',')break;at++;
            }
        }
        var output=new StringBuilder();int end=0;
        foreach(var (start,r) in replacements){if(start<end)continue;output.Append(text,end,start-end);output.Append(r.Value);end=r.End;}
        output.Append(text,end,text.Length-end);return output.ToString();
    }
    internal static string FunctionalText(string text,string channel=null)
    {
        text=NormalizeHex(text);var output=new StringBuilder();int end=0;
        foreach(Match match in Regex.Matches(text,@"\?([A-Z]{2})(?![A-Za-z])"))
        {
            if(match.Index<end)continue;string name=match.Groups[1].Value;if(!Functional.Contains(name))throw new FormatException("Unknown MSX functional command ?"+name);
            int at=match.Index+match.Length;
            bool Probe(string pattern)=>Regex.IsMatch(text[at..],"^"+pattern);
            string[] allowed=name switch
            {
                "NP" or "GS" or "GH" or "GL"=>BaseChannels[..3],
                "WM" or "WP" or "WI" or "WE" or "WG"=>BaseChannels[3..],
                "IR" or "IU" or "IS" or "QP"=>OpllChannels[..^1],
                "QE"=>OpllChannels,
                "IT" or "ID" or "IB"=>new[]{"MSXG"},
                "EP" or "EA" or "ET" or "DB" or "DW" or "DR" or "MV" or "MW" or "KT"=>Channels[..^1],
                "WF" or "QR" or "XD" or "XM" or "XI" or "XS" or "XP" or "EN" or "EL" or "DS" or "IM" or "XH" or "XC" or "XR"=>BaseChannels,
                _=>Channels
            };
            if(name=="WI"&&Probe(@"\s*-(?!\d)"))allowed=BaseChannels;
            if(name is "GS" or "NP"&&Probe(@"\s*(?:\d+|-)\s*,"))allowed=BaseChannels;
            if(channel!=null&&!allowed.Contains(channel))throw new FormatException("?"+name+" is not supported on "+channel);
            int[] Args(int count)
            {
                var values=new int[count];for(int i=0;i<count;i++)
                {
                    Match m=Regex.Match(text[at..],(i>0?@"^\s*,\s*":@"^\s*")+@"([+-]?\d+)");
                    if(!m.Success)throw new FormatException($"?{name} requires {count} numeric operands.");values[i]=int.Parse(m.Groups[1].Value);at+=m.Length;
                }return values;
            }
            bool Off()=>Probe(@"\s*-(?!\d)");
            void SkipOff(){at+=Regex.Match(text[at..],@"^\s*-(?!\d)").Length;}
            string replacement;
            if(name=="GS"&&Probe(@"\s*-\s*,\s*1(?!\d)")){at+=Regex.Match(text[at..],@"^\s*-\s*,\s*1(?!\d)").Length;replacement="?GS-,1";}
            else if(name is "EP" or "MV" or "GS"&&Off()){SkipOff();replacement=name switch{"EP"=>"?e-","MV"=>"?m-",_=>"?a-"};}
            else if(name=="EP")
            {
                int[] a=Args(3);if(a[0] is <0 or >8)throw new FormatException("?EP profile must be 0..8.");
                if(a[0]==8){int[] extra=ArgsAfterComma(2);if(channel!=null&&!BaseChannels.Contains(channel))throw new FormatException("?EP8 requires PSG/SCC.");replacement=$"?EP8,{a[1]},{a[2]},{extra[0]},{extra[1]}";}
                else if(a[0]==3){int[] extra=ArgsAfterComma(1);replacement=$"?e{a[1]},{a[2]},{extra[0]},3";}
                else replacement=$"?e{a[1]},{a[2]},{a[0]}";
            }
            else if(name=="MV"&&Probe(@"\s*2\s*,")){int[] a=Args(4);if(channel!=null&&!BaseChannels.Contains(channel))throw new FormatException("?MV2 requires PSG/SCC.");replacement="?MV"+string.Join(',',a);}
            else if(name is "EA" or "MV") {int[] a=Args(2);if(a[0] is <0 or >1)throw new FormatException("Invalid ?"+name+" mode.");replacement=name=="EA"?$"?a{a[1]},{a[0]}":$"?m{a[1]}"+(a[0]==1?",1":"");}
            else if(name is "ET" or "EN" or "DW" or "EL" or "DB") {int v=Args(1)[0];replacement=name switch{"ET"=>$"?a{v},2","EN"=>$"?a{v},3","DW"=>$"?d{v},1","DB"=>$"?d{v}",_=>v==0?"?v-":$"?v{v}"};}
            else if(name is "DR" or "QR" or "IB" or "FR") {if(Probe(@"\s*[+-]?\d"))throw new FormatException("?"+name+" takes no numeric operand.");replacement=name switch{"DR"=>"?d-","QR"=>"?q","IB"=>"?B",_=>"?r"};}
            else if(name=="WM")
            {
                if(Off()){SkipOff();replacement="?w-";}else {int[] a=Args(2);if(Probe(@"\s*,")){if(ArgsAfterComma(1)[0]!=1)throw new FormatException("?WM mode must be 1.");replacement=$"?WM{a[0]},{a[1]},1";}else replacement=$"?w{a[0]},{a[1]}";}
            }
            else if(name=="XT")
            {
                int v=Args(1)[0];if(Probe(@"\s*,")){int mode=ArgsAfterComma(1)[0];if(mode is not(1 or 2)||channel!=null&&!BaseChannels.Contains(channel))throw new FormatException("Invalid ?XT direct mode.");replacement=$"?XT{v},{mode}";}else replacement="?k"+v;
            }
            else if(name=="NP")
            {
                bool off=Off();string value;if(off){SkipOff();value="-";}else value=Args(1)[0].ToString();
                if(Probe(@"\s*,")){int mode=ArgsAfterComma(1)[0];if(mode is <0 or >3)throw new FormatException("?NP priority must be 0..3.");replacement=$"?NP{value},{mode}";}else{if(off)throw new FormatException("?NP- requires a priority.");replacement="?n"+value;}
            }
            else if(name=="GS") {int v=Args(1)[0];if(Probe(@"\s*,")){if(ArgsAfterComma(1)[0]!=1)throw new FormatException("?GS mode must be 1.");replacement=$"?GS{v},1";}else replacement="?s"+v;}
            else if(name is "FD" or "XD")
            {
                int id=Args(1)[0];Match opening=Regex.Match(text[at..],@"^\s*=\s*\{");if(!opening.Success)throw new FormatException("?"+name+" definition requires ={ ... }.");at+=opening.Length;replacement=(name=="FD"?"?P":"?R")+id+"{";
            }
            else if(name is "WD" or "WS")throw new FormatException("?"+name+" is a global waveform definition.");
            else if(name=="XC"&&Probe(@"\s*\d")) {int[] a=Args(2);if(a[0] is <1 or >255||a[1]!=1||channel!=null&&!BaseChannels.Contains(channel))throw new FormatException("Invalid ?XC group end.");replacement=$"?XC{a[0]},1";}
            else if(name is "WP" or "KT" or "IM" or "XH" or "XC" or "XR" or "VF" or "WI" or "WF" or "WE" or "QE" or "WG")
            {if(name is "XC" or "XR"&&Probe(@"\s*[+-]?\d"))throw new FormatException("?"+name+" takes no numeric operand.");replacement="?"+name;}
            else replacement="?"+Simple[name];
            if(name is "EP" or "EA" or "ET" or "EN" or "EL" or "DW" or "DB" or "MV" or "XT"&&Probe(@"\s*,"))throw new FormatException("Too many operands for ?"+name);
            output.Append(text,end,match.Index-end);output.Append(replacement);end=at;
            int[] ArgsAfterComma(int count)
            {
                var list=new int[count];for(int i=0;i<count;i++){Match m=Regex.Match(text[at..],@"^\s*,\s*([+-]?\d+)");if(!m.Success)throw new FormatException("Incomplete ?"+name+" operand list.");list[i]=int.Parse(m.Groups[1].Value);at+=m.Length;}return list;
            }
        }
        output.Append(text,end,text.Length-end);string result=output.ToString();
        if(Regex.IsMatch(result,@"\?[A-Z]{2}[A-Za-z]"))throw new FormatException("MSX functional names contain exactly two letters.");return result;
    }
    internal static string Global(string line)
    {
        if(Regex.IsMatch(line,@"^[@?](?:WD|WS|IU|IT|KT|IM|[WwOT])(?=[^A-Za-z]|$)"))line=NormalizeHex(line,true);
        Match share=Regex.Match(line,@"^\?WS\s*(\d+)\s*,\s*(\d+)\s*$");if(share.Success)return "?W"+share.Groups[1].Value+"=?W"+share.Groups[2].Value;
        Match head=Regex.Match(line,@"^\?(WD|IU|IT)\s*(\d+)\s*=\s*\{");
        if(head.Success)return "?"+(head.Groups[1].Value switch{"WD"=>"W","IU"=>"O",_=>"T"})+head.Groups[2].Value+"={"+line[head.Length..];
        if(Regex.IsMatch(line,@"^\?(?:WD|WS|IU|IT)(?![A-Z])"))throw new FormatException("Invalid global MSX resource definition.");return line;
    }
    internal static string LowerShared(string source)
    {
        string opened=null;var output=new List<string>();
        foreach(string raw in source.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))
        {
            string code=raw.Split(';')[0];Match row=Regex.Match(code,@"^(\s*)(SCCS|PSGS|OPLS)([ \t]+)(.*)$");
            if(!row.Success){if(opened!=null&&code.Trim().Length>0)throw new FormatException("Continue shared definition on "+opened);if(Regex.IsMatch(code,@"^\s*(?:SCCS|PSGS|OPLS)\b"))throw new FormatException("Shared row requires whitespace.");output.Add(raw);continue;}
            string prefix=row.Groups[2].Value;if(opened!=null&&opened!=prefix)throw new FormatException("Shared definition changes chip context.");string body=row.Groups[4].Value;
            if(opened==null&&body.Trim().Length==0)throw new FormatException(prefix+" requires a definition.");
            for(int pos=0;pos<body.Length;)
            {
                if(opened!=null){if(body[pos]=='{')throw new FormatException("Nested shared definition.");if(body[pos]=='}')opened=null;pos++;continue;}
                if(char.IsWhiteSpace(body[pos])){pos++;continue;}
                Match def=Regex.Match(body[pos..],@"^(?:\?(?:FD|XD)|[@?][PR])\s*[+]?(?:\$[0-9a-fA-F]+|\d+)\s*=?\s*\{");
                if(!def.Success)throw new FormatException(prefix+" accepts definitions only.");opened=prefix;pos+=def.Length;
            }
            int start=row.Groups[2].Index;output.Add(raw[..start]+(prefix switch{"SCCS"=>"SCCA","PSGS"=>"MSXD",_=>"MSXA"})+raw[(start+4)..]);
        }
        if(opened!=null)throw new FormatException("Unclosed shared definition on "+opened);return string.Join('\n',output);
    }
}
