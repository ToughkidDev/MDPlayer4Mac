using System.Text;
using System.Text.RegularExpressions;
using static MDPlayer.MucoNative.MsxMmlSyntax;

namespace MDPlayer.MucoNative;

internal static class MsxMusicalCompiler
{
    internal sealed record Compilation(byte[] Object,string Title,int Timer,string LegacySource);
    private static readonly Regex Row=new(@"^(MSX[A-K]|SCC[A-E])\s+(.*)",RegexOptions.CultureInvariant|RegexOptions.Singleline);
    internal static bool HasExtensions(string source)
    {
        bool channels=Regex.IsMatch(source,@"(?m)^\s*(?:MSX[A-K]|SCC[A-E])\s+");
        return channels&&(Regex.IsMatch(source,@"(?m)^\s*(?:MSX[A-K]|SCC[A-E])\s+[^;\r\n]*(?:\?|@[A-Za-z]|:\d)")
            ||Regex.IsMatch(source,@"(?m)^\s*(?:SCCS|PSGS|OPLS)\s+")
            ||Regex.IsMatch(source,@"(?m)^#\s*\*\d+\s*\{[^;\r\n]*(?:\?|@[A-Za-z])"));
    }
    private static string ExpandMacros(string text,Dictionary<int,string> definitions,string channel,HashSet<int> stack=null)
    {
        stack??=new();return Regex.Replace(text,@"\*\*?\s*([+-]?\d+)",m=>
        {
            if(m.Value.StartsWith("**",StringComparison.Ordinal))throw new FormatException("MSX channels do not support macro shift **.");
            int id=int.Parse(m.Groups[1].Value);if(id is <0 or >65535||!definitions.TryGetValue(id,out string body))throw new FormatException("Undefined MSX macro "+id);
            if(stack.Count>=8||!stack.Add(id))throw new FormatException("Recursive macro or more than eight nested macros.");
            body=FunctionalText(body,channel);if(Regex.IsMatch(body,@"[@?]u(?=\b|[+-]?\d)"))throw new FormatException("Write ?XU on the channel, not inside a macro.");
            string result=ExpandMacros(body,definitions,channel,stack);stack.Remove(id);return result;
        });
    }
    private sealed class Build
    {
        internal readonly List<string> Body=new(),Waves=new(),Resources=new(),Legacy=new();
        internal readonly Dictionary<string,string> Routes=new();internal readonly SortedDictionary<int,string> Rhythms=new();
        internal readonly HashSet<int> ProgramLabels=new(),TablePrograms=new();
        internal readonly HashSet<string> Standard=new();
        internal readonly Dictionary<string,(int Default,int Octave,int Clock)> Defaults=new();
        internal byte[] Voice=new byte[8192];internal string Title="Packed note score";
        internal bool InBlock,Stage1,Pn6,HasWave,Carrier,Pn6Source;internal int Timer=198,Serial;
        internal (bool Normal,int Default,int Octave,int Clock,int Volume,bool Direct)? ProgramContext;
    }
    internal static Compilation Parse(string source,Func<string,Stream> asset,byte[] voiceBank=null)
    {
        source=ResolveBankImports(source,asset);source=LowerShared(source);
        var b=new Build();var lines=new List<string>();
        foreach(string raw in source.Split('\n'))
        {
            int semicolon=raw.IndexOf(';');string content=semicolon<0?raw:raw[..semicolon],comment=semicolon<0?"":raw[semicolon..];
            Match row=Regex.Match(content,@"^(\s*(?:MSX[A-K]|SCC[A-E])\s+)(.*)$");
            if(row.Success)content=row.Groups[1].Value+FunctionalText(row.Groups[2].Value,row.Groups[1].Value.Trim());
            else if(Regex.IsMatch(content,@"^\s*[@?](?:WD|WS|IU|IT|KT|IM|[WwOT])(?=[^A-Za-z]|$)"))content=NormalizeHex(content,true);
            lines.Add(content+comment);
        }
        var scoped=lines.Select(x=>x.TrimStart()).ToList();
        foreach(string line in scoped)
        {
            if(line.Length==0||line.StartsWith(';'))continue;if(!line.StartsWith('#'))break;
            Match carrier=Regex.Match(line,@"^#CarrierCorrection[ \t]+(.+)",RegexOptions.IgnoreCase);
            if(carrier.Success)b.Carrier=new[]{"YES","Y","TRUE","T","1"}.Contains(carrier.Groups[1].Value.Trim().ToUpperInvariant());
        }
        var macros=new Dictionary<int,string>();
        foreach(string line in scoped)
        {
            Match m=Regex.Match(line.Split(';')[0],@"^#\s*\*(\d+)\s*\{(.*)\}\s*$");if(m.Success)macros.TryAdd(int.Parse(m.Groups[1].Value),m.Groups[2].Value);
            Match map=Regex.Match(line.Split(';')[0],@"^\?IM\d+\s*=\s*\{([^}]*)\}\s*$");
            if(map.Success)foreach(string n in map.Groups[1].Value.Split(','))b.ProgramLabels.Add(int.Parse(n.Trim()));
            foreach(Match program in Regex.Matches(line.Split(';')[0],@"\?(?:EP\s*8|MV\s*2)\s*,\s*(\d+)"))b.TablePrograms.Add(int.Parse(program.Groups[1].Value));
        }
        b.ProgramLabels.UnionWith(b.TablePrograms);
        var phraseBodies=new Dictionary<int,string>();
        foreach(string line in scoped)
        {
            Match row=Row.Match(line.Split(';')[0]);if(!row.Success)continue;
            string expanded=ExpandMacros(row.Groups[2].Value,macros,row.Groups[1].Value);
            foreach(Match def in Regex.Matches(expanded,@"[@?]P\s*(\d+)\s*\{([^}]*)\}"))phraseBodies[int.Parse(def.Groups[1].Value)]=def.Groups[2].Value;
        }
        var pending=new Queue<int>(b.ProgramLabels);
        while(pending.TryDequeue(out int next))if(phraseBodies.TryGetValue(next,out string body))foreach(Match call in Regex.Matches(body,@"[@?]p\s*(\d+)"))if(b.ProgramLabels.Add(int.Parse(call.Groups[1].Value)))pending.Enqueue(int.Parse(call.Groups[1].Value));
        var rows=Channels.ToDictionary(ch=>ch,_=>new List<string>());var extended=new HashSet<string>();
        foreach(string line in scoped)
        {
            Match row=Row.Match(line.Split(';')[0]);if(!row.Success)continue;string ch=row.Groups[1].Value,text=row.Groups[2].Value;
            if(rows.ContainsKey(ch))rows[ch].Add(text);
            if(Regex.IsMatch(text,@"@[a-zA-Z]|\?|:\d")){if(!Channels.Contains(ch))throw new FormatException("Unsupported extended channel "+ch);extended.Add(ch);}
        }
        foreach(var (ch,texts) in rows)if(texts.Count>0&&string.Join(' ',texts).Contains('*')&&Regex.IsMatch(ExpandMacros(string.Join(' ',texts),macros,ch),@"@[a-zA-Z]|\?|:\d"))extended.Add(ch);
        var compact=rows.Where(x=>Regex.IsMatch(string.Join(' ',x.Value),@"[@?]u\s*[+-]?\d")).Select(x=>x.Key).ToHashSet();
        b.Pn6Source=scoped.Any(line=>Regex.IsMatch(line,@"\?MV\s*2\s*,|\?EP\s*8\s*,|\?(?:GS|NP|XT)\s*(?:\d+|-)\s*,\s*[0123](?!\d)|(?:\?DS|[@?]g|\?WM)\s*-?\d+\s*,\s*\d+\s*,\s*[12](?!\d)|^\?(?:KT|IM)\d+\s*="))
            ||rows.Any(x=>Regex.IsMatch(string.Join(' ',x.Value),@"\?(?:KT|IM|XH|XC|XR|VF)(?![A-Za-z])"));
        if(extended.Count>0)b.Standard.UnionWith(rows.Where(x=>x.Value.Count>0&&!compact.Contains(x.Key)).Select(x=>x.Key));
        if(b.Standard.Any(OpllChannels[..^1].Contains))b.Voice=LoadFmVoices(source,asset,voiceBank);
        if(b.Standard.Count>0)
        {
            extended.UnionWith(rows.Where(x=>x.Value.Count>0).Select(x=>x.Key));
            lines=lines.Where(x=>!Regex.IsMatch(x,@"^\s*(MSX[A-K]|SCC[A-E])\s+")).ToList();
            foreach(string ch in Channels)if(rows[ch].Count>0)lines.Add(ch+" "+(compact.Contains(ch)?string.Join(' ',rows[ch]):ExpandMacros(string.Join('\n',rows[ch]),macros,ch)));
        }
        bool stageSyntax=scoped.Any(line=>Regex.IsMatch(line,@"^(MSX[A-J]|SCC[A-E])\s+")&&Regex.IsMatch(line,@"[@?](?:[adm]\s*[+-]?\d+\s*,\s*[12](?!\d)|e\s*[+-]?\d+\s*,\s*[+-]?\d+\s*,\s*(?:7(?!\d)|[+-]?\d+\s*,\s*3(?!\d)))"));
        if(extended.Overlaps(OpllChannels)||stageSyntax)extended.UnionWith(rows.Where(x=>OpllChannels.Contains(x.Key)&&x.Value.Count>0).Select(x=>x.Key));
        if(extended.Count==0)return null;
        bool opll=b.Standard.Count>0||extended.Overlaps(OpllChannels);
        b.Timer=b.Standard.Count>0?225:198;
        foreach(string ch in Channels)b.Defaults[ch]=(b.Standard.Contains(ch)?24:1,b.Standard.Contains(ch)?6:4,128);
        foreach(string raw in lines)
        {
            string line=Global(raw.Split(';')[0].Trim());if(line.Length==0){b.Legacy.Add(raw);continue;}
            if(line.StartsWith("#title ",StringComparison.Ordinal)){b.Title=line[7..].Trim();b.Legacy.Add(raw);continue;}
            if(Regex.IsMatch(line,@"^#msx\s+compact\b",RegexOptions.IgnoreCase))throw new FormatException("A document-wide compact mode is not supported.");
            if(Resource(b,line)){b.Legacy.Add(raw);continue;}
            Match row=Row.Match(line);if(!row.Success||!extended.Contains(row.Groups[1].Value)){b.Legacy.Add(raw);continue;}
            new Parser(b,row.Groups[1].Value,row.Groups[2].Value).Run();
        }
        if(b.InBlock)throw new FormatException("Unclosed MSX definition.");
        string[] channels=b.Stage1||b.Standard.Count>0||b.Pn6?Channels:opll?Channels:BaseChannels;
        foreach(string ch in channels)if(!b.Routes.ContainsKey(ch))b.Routes[ch]="EMPTY";
        if(b.Routes.Values.Contains("EMPTY"))b.Body.AddRange(new[]{"EMPTY:","end"});
        var ir=new List<string>{"title "+b.Title,"timer "+b.Timer};ir.AddRange(b.Waves);ir.AddRange(b.Resources);
        for(int i=0;i<channels.Length;i++)ir.Add($"part {i} {b.Routes[channels[i]]}");
        foreach(var (id,label) in b.Rhythms)ir.Add($"rhythm {id} {label}");
        ir.AddRange(b.Standard.Count>0?ShortNotes(b.Body):b.Body);
        var result=PackedScoreAssembler.Assemble(ir);
        return new(result.Object,result.Title,result.Timer,string.Join('\n',b.Legacy)+"\n");
    }
    private static bool Resource(Build b,string line)
    {
        Match table=Regex.Match(line,@"^\?(KT|IM)(\d+)\s*=\s*\{([^}]*)\}$");
        if(table.Success)
        {
            int id=int.Parse(table.Groups[2].Value);int[] values=table.Groups[3].Value.Split(',').Select(x=>int.Parse(x.Trim())).ToArray();
            if(id is <0 or >15||values.Length is <12 or >128||values.Any(v=>v<0||v>(table.Groups[1].Value=="KT"?65535:255)))throw new FormatException("Invalid MSX table definition.");
            b.Resources.Add((table.Groups[1].Value=="KT"?"pitch-table ":"instrument-map ")+id+" "+string.Join(',',values.Select(v=>table.Groups[1].Value=="KT"?v.ToString():"L"+v)));b.Pn6=true;return true;
        }
        if(Regex.IsMatch(line,@"^\?(KT|IM)"))throw new FormatException("Table definition requires ?KT/?IMid={...}.");
        Match patch=Regex.Match(line,@"^[@?]([OT])(\d+)\s*=?\s*\{([^}]*)\}$");
        if(patch.Success)
        {
            int id=int.Parse(patch.Groups[2].Value);int[] values=patch.Groups[3].Value.Split(',').Select(x=>int.Parse(x.Trim())).ToArray();
            if(id is <0 or >15||values.Length!=(patch.Groups[1].Value=="O"?8:6)||values.Any(v=>v is <0 or >255))throw new FormatException("Invalid OPLL resource definition.");
            b.Waves.Add((patch.Groups[1].Value=="O"?"opll-patch ":"opll-drums ")+id+" "+string.Join(',',values));return true;
        }
        if(Regex.IsMatch(line,@"^[@?]W\d",RegexOptions.IgnoreCase))
        {
            Match alias=Regex.Match(line,@"^[@?]W(\d+)\s*=\s*[@?]W(\d+)$",RegexOptions.IgnoreCase);
            if(alias.Success)b.Waves.Add("wave-alias "+alias.Groups[1].Value+" "+alias.Groups[2].Value);
            else
            {
                Match wave=Regex.Match(line,@"^[@?]W(\d+)\s*(?::|=)?\s*\{([^}]*)\}$",RegexOptions.IgnoreCase);if(!wave.Success)throw new FormatException("Use a complete 32-sample waveform definition.");
                int[] samples=wave.Groups[2].Value.Split(',').Select(x=>int.Parse(x.Trim())).ToArray();if(samples.Length!=32||samples.Any(x=>x is < -128 or >127))throw new FormatException("SCC waves require 32 signed samples.");b.Waves.Add("wave "+wave.Groups[1].Value+" "+string.Join(',',samples));
            }
            b.HasWave=true;return true;
        }
        return false;
    }
    private static IEnumerable<string> ShortNotes(IEnumerable<string> operations)
    {
        var result=new List<string>();int?[] units={1,1},octaves={null,null};
        foreach(string operation in operations)
        {
            bool ol=operation.StartsWith("opll-",StringComparison.Ordinal);int key=ol?1:0;string name=ol?operation[5..]:operation,prefix=ol?"opll-":"";
            if(name.EndsWith(':')||Regex.IsMatch(name,@"^(jump|call|repeat|repeat2) ")){units=new int?[]{null,null};octaves=new int?[]{null,null};}
            if(name=="standard-init"){units[key]=1;octaves[key]=null;}
            if(name.StartsWith("standard-note ",StringComparison.Ordinal))
            {
                int[] a=name[14..].Split(',').Select(int.Parse).ToArray();int duration=a[0],pitch=a[1],flags=a[2];
                if((flags&3)==0&&(ol||(flags&4)!=0||pitch<0x60))
                {
                    int? unit=units[key];if(!unit.HasValue||duration%unit.Value!=0||duration/unit.Value is <1 or >16)
                    {
                        unit=null;for(int u=Math.Max(1,(duration+15)/16);u<(ol?16:256);u++)if(duration%u==0&&duration/u<=16){unit=u;break;}
                    }
                    if(unit.HasValue)
                    {
                        if(unit!=units[key]){result.Add(prefix+"unit "+unit.Value);units[key]=unit;}
                        if((flags&4)==0&&(pitch>>4)!=octaves[key]){result.Add(prefix+"octave "+(pitch>>4));octaves[key]=pitch>>4;}
                        result.Add(prefix+((flags&4)!=0?"r":PackedScoreAssembler.NoteNames[pitch&15])+"%"+(duration/unit.Value));continue;
                    }
                }
            }
            result.Add(operation);
        }
        return result;
    }
    private static string ResolveBankImports(string source,Func<string,Stream> asset)
    {
        var imports=new Regex(@"^\?WS\s*(\d+)\s*,\s*(\d+)\s*,\s*1\s*$");
        var lines=source.Replace("\r\n","\n").Replace('\r','\n').Split('\n');bool wanted=false;
        foreach(string raw in lines){string code=raw.Split(';')[0].Trim();if(code.StartsWith("?WS",StringComparison.Ordinal)&&imports.IsMatch(NormalizeHex(code,true)))wanted=true;}
        if(!wanted)return source;
        var headers=new List<string>();foreach(string raw in lines){string code=raw.Split(';')[0].Trim();if(code.Length==0)continue;if(!code.StartsWith('#'))break;headers.Add(code);}
        var tags=Regex.Matches(string.Join('\n',headers),@"(?im)^#sccvoice[ \t]+([^;\r\n]+)");if(tags.Count!=1)throw new FormatException("External ?WS imports require exactly one #sccvoice tag.");
        string filename=tags[0].Groups[1].Value.Trim();using Stream input=asset?.Invoke(filename)??throw new FormatException("Missing SCC voice bank: "+filename);
        using var copy=new MemoryStream();input.CopyTo(copy);byte[] data=copy.ToArray();
        if(data.Length<16||data.Length>8720||Encoding.ASCII.GetString(data,0,4)!="SCWD"||data[4]!=1||data[5]!=0||BitConverter.ToUInt16(data,8)!=34||BitConverter.ToUInt16(data,10)!=0)throw new FormatException("Invalid SCWD header.");
        int count=BitConverter.ToUInt16(data,6);if(count is <1 or >256||data.Length!=16+count*34)throw new FormatException("Invalid SCWD size.");uint crc=uint.MaxValue;
        foreach(byte value in data.AsSpan(16)){crc^=value;for(int n=0;n<8;n++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}if((crc^uint.MaxValue)!=BitConverter.ToUInt32(data,12))throw new FormatException("Invalid SCWD CRC.");
        var bank=new Dictionary<int,sbyte[]>();int previous=-1;for(int i=0;i<count;i++){int at=16+i*34,id=BitConverter.ToUInt16(data,at);if(id>255||id<=previous)throw new FormatException("Invalid SCWD IDs.");bank[id]=data.AsSpan(at+2,32).ToArray().Select(x=>(sbyte)x).ToArray();previous=id;}
        var output=new List<string>();foreach(string raw in lines)
        {
            int semicolon=raw.IndexOf(';');string code=semicolon<0?raw:raw[..semicolon],comment=semicolon<0?"":raw[semicolon..];if(code.TrimStart().StartsWith("?WS",StringComparison.Ordinal))code=NormalizeHex(code,true);
            Match import=imports.Match(code.Trim());if(import.Success)
            {
                int local=int.Parse(import.Groups[1].Value),id=int.Parse(import.Groups[2].Value);if(local is <0 or >63||!bank.TryGetValue(id,out var samples))throw new FormatException("Undefined or invalid external SCC wave.");code="?WD"+local+"={"+string.Join(',',samples)+"}";
            }
            output.Add(code+comment);
        }
        return string.Join('\n',output)+"\n";
    }
    private static byte[] LoadFmVoices(string source,Func<string,Stream> asset,byte[] supplied)
    {
        var bank=new byte[8192];Match file=Regex.Match(source,@"(?im)^#voice[ \t]+([^;\r\n]+)");
        if(supplied!=null){if(supplied.Length>8192)throw new FormatException("FM voice bank exceeds 8192 bytes.");supplied.CopyTo(bank,0);}
        else if(file.Success)
        {
            using var stream=asset?.Invoke(file.Groups[1].Value.Trim());if(stream!=null){using var ms=new MemoryStream();stream.CopyTo(ms);if(ms.Length>8192)throw new FormatException("FM voice bank exceeds 8192 bytes.");ms.ToArray().CopyTo(bank,0);}
        }
        string clean=string.Join('\n',source.Split('\n').Select(x=>x.Split(';')[0]));
        foreach(Match header in Regex.Matches(clean,@"(?m)^\s*@(%?)(\d+)(?=\s|:|$)"))
        {
            bool raw=header.Groups[1].Value.Length>0;int id=int.Parse(header.Groups[2].Value);if(id>255)throw new FormatException("FM voice number outside 0..255.");int pos=header.Index+header.Length;
            if(raw&&!Regex.IsMatch(clean[pos..],@"^[ \t]*:?[ \t]*\{")){pos=clean.IndexOf('\n',pos);if(pos<0)throw new FormatException("Incomplete FM voice.");}
            int[] values=new int[raw?25:38];for(int i=0;i<values.Length;i++)
            {
                while(pos<clean.Length&&" \t\r\n,:{}".Contains(clean[pos]))pos++;Match n=Regex.Match(clean[pos..],@"^(\$[0-9a-fA-F]+|[+-]?\d+)");if(!n.Success)throw new FormatException("Incomplete FM voice "+id);values[i]=(n.Value[0]=='$'?Convert.ToInt32(n.Value[1..],16):int.Parse(n.Value))&255;pos+=n.Length;
            }
            byte[] packed=PackFmVoice(values);
            packed.CopyTo(bank,id*32+1);
        }
        return bank;
    }

    internal static byte[] PackFmVoice(int[] values)
    {
        if(values.Length==25)return values.Select(v=>(byte)v).ToArray();
        if(values.Length!=38)throw new FormatException("An FM voice requires 38 parameters or 25 raw bytes.");
        var packed=new byte[25];packed[24]=(byte)(((values[0]&7)<<3)|(values[1]&7));int[] columns={0,2,1,3};
        for(int row=0;row<4;row++){int at=2+9*row,column=columns[row];packed[column]=(byte)(((values[at+8]&15)<<4)|(values[at+7]&15));packed[4+column]=(byte)(values[at+5]&127);packed[8+column]=(byte)(((values[at+6]&3)<<6)|(values[at]&31));packed[12+column]=(byte)(values[at+1]&31);packed[16+column]=(byte)(values[at+2]&31);packed[20+column]=(byte)(((values[at+4]&15)<<4)|(values[at+3]&15));}
        return packed;
    }

    private sealed class Parser
    {
        private readonly Build b;private readonly string ch,text;private int pos,def,octave,clock,volume,rhythmMask;private int[] rhythmLevels=new int[6];
        private bool normal,direct,closed,pendingTie;private int? lastNote,programId;private string loopPoint;
        private readonly List<(string Target,int? Escape)> loops=new();
        private bool Ol=>OpllChannels.Contains(ch);private bool Base=>BaseChannels.Contains(ch);
        internal Parser(Build build,string channel,string source){b=build;ch=channel;text=source;(def,octave,clock)=b.Defaults[ch];normal=b.Standard.Contains(ch);}
        private void Error(string msg)=>throw new FormatException(ch+": "+msg+" (column "+(pos+1)+")");
        private void Spaces(){while(pos<text.Length&&char.IsWhiteSpace(text[pos]))pos++;}
        private int Num(int min=0,int max=255)
        {
            Spaces();Match m=Regex.Match(text[pos..],@"^[+-]?\d+");if(!m.Success)Error("Number required");pos+=m.Length;int n=int.Parse(m.Value);if(n<min||n>max)Error($"Number outside {min}..{max}");return n;
        }
        private void Comma(){Spaces();if(pos==text.Length||text[pos]!=',')Error("Comma required");pos++;}
        private bool CommaAhead(){Spaces();return pos<text.Length&&text[pos]==',';}
        private bool Off=>pos<text.Length&&text[pos]=='-'&&(pos+1==text.Length||!char.IsAsciiDigit(text[pos+1]));
        private int Duration()
        {
            int n=def;if(pos<text.Length&&text[pos]=='%'){pos++;n=Num(1,255);}else if(pos<text.Length&&char.IsAsciiDigit(text[pos]))n=clock/Num(1,255);
            int half=n;while(pos<text.Length&&text[pos]=='.'){pos++;half/=2;n=(n+half)&255;}return n==0?def:n;
        }
        private void Emit(string op)
        {
            string name=op.Split(' ')[0];if(PackedScoreAssembler.Stage.ContainsKey(name)){if(ch=="MSXG")Error("Envelope/detune requires a melody channel");b.Stage1=true;}
            bool nativeOl=Ol&&(name=="octave"||name=="voice"||name=="pitch-select"||name=="drum-bias"||name=="end-policy"||name=="gate-mode"||PackedScoreAssembler.Standard.ContainsKey(name)||PackedScoreAssembler.Stage.ContainsKey(name)||Regex.IsMatch(name,@"^(?:[a-g]\+?|r)%\d+$")||new[]{"drum","gate","unit","base-level","sustain","sustain-off","bass","drum-preset","detune","detune-off","vibrato-off","vibrato","vibrato-delay","vibrato-immediate","call","return","repeat","repeat2","jump","type","end"}.Contains(name));
            b.Body.Add(nativeOl?"opll-"+op:op);
        }
        private void Flags(int add=0,int extra=0)
        {
            if(lastNote==null)Error("Tie requires a preceding note");string instruction=b.Body[lastNote.Value];int space=instruction.IndexOf(' ');int[] a=instruction[(space+1)..].Split(',').Select(int.Parse).ToArray();
            if((a[2]&4)!=0&&add!=0)Error("A rest cannot be tied as a pitched note");if(a[0]+extra>65535)Error("Tied duration exceeds 65535 clocks");a[0]+=extra;a[2]|=add;b.Body[lastNote.Value]=instruction[..(space+1)]+string.Join(',',a);
        }
        private void EmitVolume()
        {
            if(normal&&ch=="MSXG")Emit("standard-rhythm-volume "+string.Join(',',new[]{volume}.Concat(rhythmLevels)));
            else if(normal){int kind=Ol&&!direct?1:0;Emit($"standard-volume {kind},{(kind==1?(volume+4)&255:volume)}");}
            else Emit("base-level "+volume);
        }
        private void Route()
        {
            if(b.Routes.ContainsKey(ch))return;b.Routes[ch]="C"+ch;Emit("C"+ch+":");if(normal)Emit("standard-init");
        }
        internal void Run()
        {
            if(!normal&&!b.HasWave&&!Ol&&!(b.Pn6Source&&Array.IndexOf(BaseChannels,ch)<3))Error("Define SCC wave data before compact PSG/SCC rows");
            bool definition=b.InBlock||Regex.IsMatch(text,@"^[@?][RP]");if(!definition)Route();
            while(pos<text.Length)
            {
                if(normal&&closed&&!b.InBlock){Match next=Regex.Match(text[pos..],@"(?m)^\s*[@?](?:P|R)\s*\d+\s*\{");if(!next.Success)break;pos+=next.Index;}
                Spaces();if(pos==text.Length)break;
                if(!b.InBlock&&!b.Routes.ContainsKey(ch)&&!Regex.IsMatch(text[pos..],@"^[@?][RP]"))Route();
                char c=text[pos++];
                if(c=='}') {if(!b.InBlock)Error("Unmatched }");b.InBlock=false;programId=null;if(b.ProgramContext is {} saved){(normal,def,octave,clock,volume,direct)=saved;b.ProgramContext=null;}continue;}
                if(c==':'){Emit("L"+Num()+":");continue;}
                if(c=='!') {if(programId.HasValue&&!b.TablePrograms.Contains(programId.Value))Error("Use ?FR to end an instrument-map program");if(normal&&b.InBlock)Error("Use ?FR to end a runtime phrase");if(normal&&loopPoint!=null)Emit("jump "+loopPoint);Emit("end");if(normal)closed=true;continue;}
                if("abcdefgr".Contains(c)){Note(c);continue;}
                if(c=='o'){octave=Num(1,normal||Ol?8:6);if(!normal)Emit("octave "+((Ol?8:6)-octave));continue;}
                if(c is '<' or '>'){octave+=c=='>'?1:-1;if(octave<1||octave>(normal||Ol?8:6))Error("Octave outside channel range");if(!normal)Emit("octave "+((Ol?8:6)-octave));continue;}
                if(c=='l')
                {
                    if(normal){if(pos==text.Length||!"%0123456789".Contains(text[pos]))Error("Default length requires a number");def=Duration();}
                    else if(pos<text.Length&&text[pos]=='%'){pos++;def=Num(1,16);}else{int d=Num(1,192);if(192%d!=0||192/d is <1 or >16)Error("Invalid compact duration");def=192/d;}continue;
                }
                if(c=='v')
                {
                    volume=Num(0,normal&&ch=="MSXG"?63:(normal&&Ol&&!direct)||(!normal&&!Ol)?255:15);
                    if(normal&&ch=="MSXG")for(int i=0;i<6;i++){Comma();rhythmLevels[i]=Num(0,31);}
                    if(!normal&&!Ol&&CommaAhead()){pos++;int target=Num(0,15);if(volume>15)Error("Combined level must be 0..15");Emit("combined-level "+(volume+16*target));continue;}
                    EmitVolume();continue;
                }
                if(c is 't' or 'T'&&(normal||c=='t')){int value=Num(c=='T'?1:0);if(c=='T')value=(-(14483/value))&255;if(normal)Emit("standard-tempo "+value);else b.Timer=value;continue;}
                if(c=='C'){if(normal){int value=Num();if(value!=0)clock=value;}else if(Num(1,192)!=192)Error("Compact duration base is C192");continue;}
                if(normal&&Common(c))continue;
                if(c is not('@' or '?'))Error("Unsupported extended-channel command "+c);
                if(c=='?'&&(pos==text.Length||!char.IsAsciiLetter(text[pos])))Error("? requires an MSX extension letter; numeric voice selection uses @N");
                if(pos<text.Length&&char.IsAsciiDigit(text[pos])){Voice();continue;}
                if(pos==text.Length)Error("Incomplete extension command");
                if(c=='?'&&Functional())continue;
                char command=text[pos++];Extension(command);
            }
            if(normal)
            {
                if(loops.Count!=0)Error("Unclosed repeat");if(pendingTie)Error("Tie has no following note");
                if(!closed&&b.Body.Count>0&&!Regex.IsMatch(b.Body[^1],@"(?:end|return)$")){if(loopPoint!=null)Emit("jump "+loopPoint);Emit("end");}
            }
            b.Defaults[ch]=(def,octave,clock);
        }
        private void Note(char c)
        {
            string note=c.ToString();int noteOctave=octave;
            if(pos<text.Length&&"+#-".Contains(text[pos]))
            {
                char accidental=text[pos++];if(c=='r')Error("Accidental on rest");int pitch=Array.IndexOf(PackedScoreAssembler.NoteNames,note)+(accidental=='-'?-1:1);
                noteOctave+=(int)Math.Floor(pitch/12.0);pitch=(pitch%12+12)%12;if(noteOctave<1||noteOctave>(normal||Ol?8:6))Error("Accidental outside octave range");note=PackedScoreAssembler.NoteNames[pitch];
            }
            if(normal)
            {
                int length=Duration(),pitch=note=="r"?0:(noteOctave-1)*16+Array.IndexOf(PackedScoreAssembler.NoteNames,note);if(pendingTie&&note=="r")Error("A rest cannot be a tie target");int flags=(note=="r"?4:0)|(pendingTie?1:0);
                Emit(ch=="MSXG"?$"standard-rhythm {length},{rhythmMask},{flags}":$"standard-note {length},{pitch},{flags}");lastNote=b.Body.Count-1;pendingTie=false;return;
            }
            int multiplier=def;if(pos<text.Length&&text[pos]=='%'){pos++;multiplier=Num(1,16);}else if(pos<text.Length&&char.IsAsciiDigit(text[pos])){int denominator=Num(1,192);if(192%denominator!=0)Error("Length must divide C192");multiplier=192/denominator;}
            if(multiplier is <1 or >16)Error("Compact multiplier must be 1..16; set ?XU for duration units");
            if(noteOctave!=octave)Emit("octave "+((Ol?8:6)-noteOctave));Emit(note+"%"+multiplier);if(noteOctave!=octave)Emit("octave "+((Ol?8:6)-octave));
        }
        private bool Common(char c)
        {
            switch(c)
            {
                case '|':return true;
                case ')':case '(':
                    if(ch=="MSXG")Error("Relative rhythm volume is unsupported");int amount=pos<text.Length&&char.IsAsciiDigit(text[pos])?Num():1;Emit("standard-volume-relative "+((c==')'?amount:-amount)&255));return true;
                case '%':def=Num(1,255);return true;
                case '&':Flags(2);pendingTie=true;return true;
                case '^':
                    int extra=Duration();if(lastNote==null)Error("^ requires a preceding note or rest");var between=b.Body.Skip(lastNote.Value+1).ToArray();
                    if(between.Any(x=>x.EndsWith(':')||Regex.IsMatch(x,@"^(?:opll-)?(?:call|jump|repeat|repeat2|return|end)\b")))Error("^ cannot cross runtime control flow");
                    if(between.Length==0)Flags(extra:extra);else{string old=b.Body[lastNote.Value];int space=old.IndexOf(' ');int[] a=old[(space+1)..].Split(',').Select(int.Parse).ToArray();int flags;if((a[2]&4)!=0)flags=4;else{Flags(2);flags=1|(pendingTie?2:0);}b.Body.Add(old[..(space+1)]+$"{extra},{a[1]},{flags}");lastNote=b.Body.Count-1;}return true;
                case 'q':Emit("standard-gate "+Num());return true;
                case 'P':if(Ol)Error("PSG mixer requires PSG or SCC");int enable=Num(0,3)^3;Emit("standard-mixer "+(((enable<<2)|enable)&9));return true;
                case 'w':if(Ol)Error("PSG noise requires PSG or SCC");Emit("standard-noise "+Num(0,31));return true;
                case 'D':
                    if(ch=="MSXG")Error("Detune requires a melody channel");int value=Num(-32768,32767);if(!Ol)value=(short)-value;bool add=pos<text.Length&&text[pos]=='+';if(add)pos++;Emit($"standard-detune {value},{(add?1:0)}");return true;
                case 'E':
                    int[] env=new int[6];for(int i=0;i<6;i++){if(i>0)Comma();env[i]=Num();}if(ch=="MSXG")Error("Software envelope requires a melody channel");Emit("standard-envelope "+string.Join(',',env));return true;
                case 'M':
                    if(ch=="MSXG")Error("LFO requires a melody channel");var args=new List<int>();
                    if(pos<text.Length&&char.IsAsciiLetter(text[pos]))
                    {
                        char mode=text[pos++];if(mode=='F')args.Add(1+Num(0,1));else if(mode is 'W' or 'C' or 'D'){args.Add(mode=='W'?3:mode=='C'?4:6);args.Add(Num());}
                        else if(mode=='L'){int n=Num(-32768,32767)&65535;args.AddRange(new[]{5,n&255,n>>8});}else Error("Unsupported LFO subcommand M"+mode);
                    }
                    else {int delay=Num();Comma();int rate=Num();Comma();int vector=Num(-32768,32767);Comma();int peak=Num();if(!Ol)vector=-vector;vector&=65535;args.AddRange(new[]{0,delay,rate,vector&255,vector>>8,peak});}
                    Emit("standard-lfo "+string.Join(',',args));return true;
                case '[':
                    if(loops.Count>=2)Error("At most two nested repeats");string target=$"S_{ch}_{++b.Serial}";Emit(target+":");loops.Add((target,null));return true;
                case ']':
                    if(loops.Count==0)Error("Unmatched ]");int count=Num(1,255);var loop=loops[^1];loops.RemoveAt(loops.Count-1);string after=null;
                    if(loop.Escape.HasValue){after=$"S_{ch}_{++b.Serial}";string name=b.Body[loop.Escape.Value].Split(' ')[0];b.Body[loop.Escape.Value]=name+$" {loops.Count},{count},{after}";}
                    if(count>1)Emit((loops.Count>0?"repeat2 ":"repeat ")+$"{count},{loop.Target}");if(after!=null)Emit(after+":");return true;
                case '/':
                    if(loops.Count==0||loops[^1].Escape.HasValue)Error("/ requires an unescaped repeat");var active=loops[^1];loops[^1]=(active.Target,b.Body.Count);Emit("standard-escape 0,1,EMPTY");return true;
                case 'L':if(loopPoint!=null)Error("Duplicate song loop");loopPoint=$"S_{ch}_{++b.Serial}";Emit(loopPoint+":");return true;
                default:return false;
            }
        }
        private void Voice()
        {
            if(normal&&ch=="MSXG"){rhythmMask=Num(0,31);return;}
            if(normal&&Array.IndexOf(BaseChannels,ch) is >=0 and <3)
            {
                var preset=NativeChipPresets.Psg[Num(0,15)];Emit("standard-envelope "+string.Join(',',preset.Skip(2).Take(6)));Emit("standard-mixer "+preset[0]);if(preset[8]==0)Emit("standard-lfo "+string.Join(',',preset.Skip(9).Take(6)));return;
            }
            if(normal&&Ol)
            {
                int id=Num();direct=false;var voice=b.Voice.AsSpan(id*32+1,25);Emit("voice "+(NativeChipPresets.ProjectOpll(voice)*16));
                if(b.Carrier){int mask=new[]{8,8,8,8,12,14,14,15}[voice[24]&7],tl=127;for(int i=0;i<4;i++)if((mask&(1<<i))!=0)tl=Math.Min(tl,voice[4+i]&127);Emit("standard-carrier "+tl);}return;
            }
            int number=Num(0,Ol?31:255);Emit("voice "+(Ol?(number<16?number*16:number-16):number));
        }
        // Functional compound forms retain their canonical token until this point.
        private bool Functional()
        {
            string command=pos+2<=text.Length?text.Substring(pos,2):"";
            if(!new[]{"WI","WF","WE","MV","EP","KT","IM","XH","XC","XR","VF","GS","NP","XT","WM","WP","QE","WG"}.Contains(command))return false;
            pos+=2;
            if(command=="WG")
            {
                if(normal||programId!=null||!ch.StartsWith("SCC",StringComparison.Ordinal))Error("?WG requires a compact SCC track");
                Spaces();int offset;
                if(Off){pos++;offset=255;}else offset=32*(Array.IndexOf(MsxMmlSyntax.BaseChannels,ch)-3)+Num(0,31);
                Emit("wave-gate "+offset);b.Pn6=true;return true;
            }
            if(command is "WI" or "WF" or "WE")
            {
                if(normal||!Base||programId.HasValue)Error("Wave program requires compact PSG/SCC outside an IM program");Spaces();bool off=Off;
                if(command!="WF"&&!(command=="WI"&&off)&&!ch.StartsWith("SCC",StringComparison.Ordinal))Error("Wave program requires SCC");
                if(off){if(command=="WF")Error("?WF requires mask,value");pos++;Emit(command=="WI"?"wave-interpolate-off":"wave-envelope-off");}
                else {int[] limits=command switch{"WI"=>new[]{63,63,15,255,7},"WF"=>new[]{7,7},_=>new[]{63,63,63,63}};int[] values=new int[limits.Length];for(int i=0;i<values.Length;i++){if(i>0)Comma();values[i]=Num(0,limits[i]);}Emit((command=="WI"?"wave-interpolate ":command=="WF"?"wave-flags ":"wave-envelope ")+string.Join(',',values));}b.Pn6=true;return true;
            }
            if(command is "MV" or "EP")
            {
                int mode=Num();if(mode!=(command=="MV"?2:8)||!Base||normal||programId.HasValue)Error("?"+command+" program requires compact PSG/SCC");Comma();int target=Num();Comma();int first=Num(0,command=="MV"?255:15);Comma();int second=Num(0,command=="MV"?1:15);
                if(command=="MV")Emit($"pitch-program L{target},{first},{second}");else{Comma();Emit($"table-envelope L{target},{first},{second},{Num()}");}b.Pn6=true;return true;
            }
            if(command=="QE"){if(!Ol)Error("?QE requires OPLL");int mode=Num(0,2);if(ch=="MSXG"&&mode==1)Error("?QE1 requires OPLL melody");Emit("end-policy "+mode);b.Pn6=true;return true;}
            if(command=="WP"){if(!ch.StartsWith("SCC",StringComparison.Ordinal))Error("?WP requires SCC");Emit("scc-wave-policy "+Num(0,2));return true;}
            b.Pn6=true;
            if(command=="WM") {if(!ch.StartsWith("SCC",StringComparison.Ordinal))Error("?WM requires SCC");int voice=Num(0,63);Comma();int rate=Num(0,63);Comma();Num(1,1);Emit($"morph {voice},{rate}");Emit("morph-refresh 1");}
            else if(command=="XT") {if(!Base)Error("?XT direct mode requires PSG/SCC");int value=Num(0,4);Comma();int mode=Num(1,2);if(mode==2&&normal)Error("?XT type,2 requires compact PSG/SCC");Emit((mode==1?"track-direct ":"track-literal ")+value);}
            else if(command=="NP") {if(!Base)Error("?NP priority requires PSG/SCC");Spaces();int value;if(Off){pos++;value=255;}else value=Num(0,31);Comma();Emit($"noise-priority {value},{Num(0,3)}");}
            else if(command=="GS")
            {
                Spaces();if(Off){pos++;Comma();Num(1,1);if(!Base||normal)Error("?GS-,1 requires compact PSG/SCC");Emit("shared-envelope-off");}
                else {if(!Base)Error("?GS deferred mode requires PSG/SCC");int value=Num(0,15);Comma();Num(1,1);Emit("deferred-shape "+value);}
            }
            else if(command is "KT" or "IM")
            {
                if(Ol&&(normal||command!="KT"||ch=="MSXG"))Error("?KT requires compact OPLL melody");
                Spaces();int id,start=0;if(Off){pos++;id=255;}else{id=Num(0,15);if(CommaAhead()){pos++;start=Num(0,116);}}
                Emit((command=="KT"?"pitch-select ":"instrument-select ")+$"{id},{start}");
            }
            else if(command=="XH")Emit("program-hold "+Num(0,1));
            else if(command=="XC"&&Regex.IsMatch(text[pos..],@"^\s*\d"))
            {
                if(!Base||normal)Error("?XC group end requires compact PSG/SCC");int mask=Num(1,255);Comma();Num(1,1);Spaces();if(pos==text.Length||text[pos]!='!')Error("?XC mask,1 must be followed by !");Emit("group-end "+mask);
            }
            else if(command is "XC" or "XR") {Spaces();if(pos<text.Length&&(char.IsAsciiDigit(text[pos])||text[pos] is '+' or '-'))Error("?"+command+" takes no numeric operand");Emit(command=="XC"?"program-cancel":"program-restart");}
            else if(command=="VF")
            {
                Spaces();if(Off){pos++;Emit("master-fade 255,0,0,0");}
                else{int profile=Num(0,1);Comma();int interval=Num();Comma();int step=Num(1,15);Comma();Emit($"master-fade {profile},{interval},{step},{Num(0,15)}");}
            }
            return true;
        }
        private void Extension(char cmd)
        {
            if(normal&&"fzix".Contains(cmd))Error("?"+cmd+" requires an explicit ?XU compact channel");
            if(Ol)
            {
                if(normal&&cmd is 'I' or 'O'&&ch!="MSXG"){int id=Num(cmd=='I'?1:0,15);direct=true;Emit("voice "+(cmd=='I'?id*16:id));return;}
                if(cmd=='q'){int q=Num();if(CommaAhead()){if(normal)Error("?QP mode requires compact OPLL");pos++;Emit($"gate-mode {q},{Num(0,1)}");}else Emit("gate "+q);return;}if(cmd=='s'){Emit(Num(0,1)==1?"sustain":"sustain-off");return;}
                if(cmd=='T'){Emit("drum-preset "+Num(0,15));if(CommaAhead()){if(ch!="MSXG")Error("?IT bias requires MSXG");pos++;Emit("drum-bias "+Num(0,15));}return;}if(cmd=='B'){Emit("bass");return;}
                if(cmd=='D'){if(ch!="MSXG")Error("?ID requires MSXG");Spaces();int mask;if(Off){pos++;mask=32;}else mask=Num(0,31);Comma();Emit($"drum {mask},{Num(1,16)-1}");return;}
                if(!"Ppjbrudmhkea".Contains(cmd))Error("Unsupported OPLL extension "+cmd);
            }
            if(cmd is 'R' or 'P')
            {
                int id=Num(0,cmd=='R'?9:255);Spaces();if(pos==text.Length||text[pos]!='{'||b.InBlock)Error("Definition requires { ... }");pos++;b.InBlock=true;
                if(cmd=='R'){if(!b.Rhythms.TryAdd(id,"R"+id))Error("Duplicate rhythm definition");Emit("R"+id+":");}
                else{Emit("L"+id+":");if(b.ProgramLabels.Contains(id))programId=id;if(normal&&b.ProgramLabels.Contains(id)){b.ProgramContext=(normal,def,octave,clock,volume,direct);normal=false;}}return;
            }
            if("edmha".Contains(cmd)&&Off)
            {
                pos++;if(Ol&&cmd=='a')Error("Hardware envelope disable is PSG-specific");Emit(cmd switch{'e'=>Ol?"software-envelope-off":"envelope-off",'d'=>"detune-off",'m'=>"vibrato-off",'h'=>"vibrato-immediate",_=>"env-off"});return;
            }
            if("wgva".Contains(cmd))
            {
                if(cmd=='w'&&!ch.StartsWith("SCC",StringComparison.Ordinal))Error("Wave morph requires SCC");
                if(Off){pos++;Emit(cmd switch{'w'=>"morph-off",'g'=>"sweep-off",'v'=>"floor-off",_=>"env-off"});return;}
                if(cmd=='w'){int wave=Num(0,63);Comma();Emit($"morph {wave},{Num(0,63)}");}
                else if(cmd=='g')
                {
                    int step=Num(-255,255);Comma();int count=Num();if(step==0)Error("Sweep step cannot be zero");
                    if(CommaAhead()){pos++;int mode=Num(1,2);if(mode==1){if(Math.Abs(step)>15||count>7)Error("Byte sweep requires step 1..15/count 0..7");Emit($"byte-sweep {Math.Abs(step)},{count+(step<0?8:0)}");}else Emit($"word-sweep {Math.Abs(step)},{count},{(step<0?1:0)}");b.Pn6=true;}
                    else{if(count==0)Error("Legacy sweep count must be nonzero");Emit((step>0?"sweep-down ":"sweep-up ")+$"{Math.Abs(step)},{count}");}
                }
                else if(cmd=='v')Emit("floor "+Num(0,15));
                else
                {
                    int value=Num(),mode=0;if(CommaAhead()){pos++;mode=Num(0,3);}if(mode==3&&Ol)Error("Start attenuation requires PSG/SCC");if(mode==2&&value>15)Error("Initial target must be 0..15");
                    Emit((mode switch{0=>Ol?"software-attack ":"attack ",1=>"quarth-attack ",2=>"initial-target ",_=>"start-attenuation "})+value);
                }
                return;
            }
            if(cmd=='e')
            {
                int a=Num();Comma();int second=Num();string kind="envelope";
                if(CommaAhead())
                {
                    pos++;int third=Num();if(CommaAhead()){pos++;if(Num()!=3)Error("Four-operand envelope requires profile 3");Emit($"quarth-envelope {a},{second},{third}");return;}
                    if(third is >=4 and <=6){if(Ol)Error("OPLL supports envelope profiles 2 and 3");Emit($"classic-envelope {third-4},{a},{second}");return;}
                    if(third==7){if(Ol)Error("Absolute envelope requires PSG/SCC");Emit($"absolute-envelope {a},{second}");return;}
                    if(third>2)Error("Invalid envelope profile");if(third!=0)kind=third==1?"fine-envelope":"manbow-envelope";
                }
                if(Ol){if(kind!="manbow-envelope")Error("OPLL supports envelope profiles 2 and 3");kind="software-envelope";}Emit($"{kind} {a},{second}");return;
            }
            if(cmd=='x'){int mode=Num(0,3);if(mode==0)Emit("mode-off");else{Comma();Emit((mode==1?"mode-note ":mode==2?"mode-pitch ":"mode-level ")+Num());}return;}
            if(cmd=='i'){Emit(new[]{"pitch-byte","pitch-word","pitch-high"}[Num(0,2)]);return;}
            if(cmd=='b')
            {
                int count=Num();if(!CommaAhead()){Emit(count==0?"loop-mark":"loop-count "+count);return;}if(count==0)Error("Labelled repeat count must be 1..255");pos++;int target=Num();string kind=Ol?"repeat":"repeat-short";
                if(CommaAhead()){pos++;kind=Num(1,2)==1?"repeat":"repeat2";}Emit($"{kind} {count},L{target}");return;
            }
            if(cmd=='d')
            {
                int value=Num(-32768,32767);if(CommaAhead()){pos++;if(Num()!=1)Error("Signed detune requires mode 1");Emit("signed-detune "+value);}else{if(value is < -128 or >255)Error("Legacy detune must be -128..255");Emit("detune "+(value&255));}return;
            }
            if(cmd=='m') {int value=Num();if(CommaAhead()){pos++;if(Num()!=1||Ol)Error("Phase vibrato requires PSG/SCC");Emit("phase-vibrato "+value);}else Emit("vibrato "+value);return;}
            if(cmd=='f'&&programId.HasValue)
            {
                int period=Num(0,4095);Comma();int level=Num(0,15);bool full=false;if(CommaAhead()){pos++;if(Num()!=1)Error("Full program step flag must be 1");full=true;}Emit((full||level>12?"program-step ":"pitch ")+$"{period},{level}");return;
            }
            string name;int argc;
            switch(cmd)
            {
                case 'u':name="unit";argc=1;break;case 'h':name="vibrato-delay";argc=1;break;case 'k':name="type";argc=1;break;
                case 'n':name="noise";argc=1;break;case 's':name="shape";argc=1;break;case 'H':name="env-high";argc=1;break;case 'L':name="env-low";argc=1;break;
                case 'z':name="step";argc=1;break;case 'f':name="pitch";argc=2;break;case 'p':name="call";argc=1;break;case 'j':name="jump";argc=1;break;case 'r':name="return";argc=0;break;case 'q':name="gate-normal";argc=0;break;
                default:Error("Unknown extension "+cmd);return;
            }
            int[] values=new int[argc];for(int i=0;i<argc;i++){if(i>0)Comma();values[i]=Num(cmd=='u'?1:0,cmd=='f'&&i==0?4095:255);}
            if(cmd=='k')
            {
                if(Ol&&(values[0]!=0&&values[0]!=1&&(values[0]!=64||ch!="MSXG")))Error("OPLL type must be 1, or 64 on MSXG");if(normal&&values[0]!=(ch=="MSXG"?64:1))Error("This track type requires compact syntax");
                if(CommaAhead()){if(!ch.StartsWith("SCC",StringComparison.Ordinal))Error("Wave policy requires SCC");pos++;int policy=Num(0,2);Emit("type "+values[0]);Emit("scc-wave-policy "+policy);return;}
            }
            Emit(name+(argc>0?" "+(cmd is 'p' or 'j'?"L"+values[0]:string.Join(',',values)):""));
        }
    }
}
