using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace MDPlayer.MucoNative;

// A musical object assembler, not a CPU assembler. All addresses below are
// bounded offsets into score data; the object contains no executable code.
internal static class PackedScoreAssembler
{
    internal sealed record Result(byte[] Object,string Title,int Timer);
    private static readonly Dictionary<string,(int Op,int Args)> Base = new()
    {
        ["mode-off"]=(0xe0,0),["mode-note"]=(0xe1,1),["mode-pitch"]=(0xe2,1),["mode-level"]=(0xe3,1),
        ["noise"]=(0xe4,1),["shape"]=(0xe5,1),["env-high"]=(0xe6,1),["env-low"]=(0xe7,1),["env-off"]=(0xe8,0),
        ["unit"]=(0xe9,1),["base-level"]=(0xea,1),["repeat-short"]=(0xeb,2),["gate-normal"]=(0xec,0),
        ["envelope"]=(0xed,2),["envelope-off"]=(0xee,0),["detune"]=(0xef,1),["detune-off"]=(0xf0,0),
        ["vibrato"]=(0xf1,1),["vibrato-off"]=(0xf2,0),["vibrato-delay"]=(0xf3,1),["vibrato-immediate"]=(0xf4,0),
        ["pitch-byte"]=(0xf5,0),["pitch-word"]=(0xf6,0),["pitch-high"]=(0xf7,0),["voice"]=(0xf8,1),
        ["call"]=(0xf9,2),["return"]=(0xfa,0),["repeat"]=(0xfb,3),["repeat2"]=(0xfc,3),["jump"]=(0xfd,2),["type"]=(0xfe,1),["end"]=(0xff,0)
    };
    private static readonly Dictionary<string,(int Op,int Args)> Opll = new(Base)
    {
        ["drum"]=(0xd8,2),["gate"]=(0xe8,1),["sustain"]=(0xeb,0),["sustain-off"]=(0xec,0),["bass"]=(0xed,0),
        ["drum-preset"]=(0xe7,1),["detune"]=(0xee,1),["detune-off"]=(0xef,0),["vibrato-off"]=(0xf0,0),
        ["vibrato"]=(0xf1,1),["vibrato-delay"]=(0xf2,1),["vibrato-immediate"]=(0xf3,0)
    };
    internal static readonly Dictionary<string,(int Sub,int Args)> Stage = new()
    {
        ["quarth-envelope"]=(0,3),["quarth-attack"]=(1,1),["initial-target"]=(2,1),["signed-detune"]=(3,2),
        ["software-envelope-off"]=(5,0),["software-envelope"]=(6,2),["software-attack"]=(7,1),["phase-vibrato"]=(25,1),["absolute-envelope"]=(26,2)
    };
    internal static readonly Dictionary<string,(int Sub,int Args)> Standard = new()
    {
        ["standard-note"]=(8,4),["standard-envelope"]=(9,6),["standard-detune"]=(10,3),["standard-gate"]=(11,1),
        ["standard-lfo"]=(12,-1),["standard-tempo"]=(13,1),["standard-init"]=(14,0),["standard-escape"]=(15,4),
        ["standard-mixer"]=(16,1),["standard-noise"]=(17,1),["standard-rhythm"]=(18,4),["standard-rhythm-volume"]=(19,7),
        ["standard-volume-relative"]=(20,1),["standard-volume"]=(21,2),["standard-carrier"]=(22,1)
    };
    private static readonly Dictionary<string,(int Sub,int Args)> Extended = new()
    {
        ["drum-bias"]=(50,1),["end-policy"]=(51,1),["gate-mode"]=(52,2),["pitch-select"]=(27,2),["instrument-select"]=(28,2),["program-hold"]=(29,1),["program-cancel"]=(30,0),["program-restart"]=(31,0),
        ["master-fade"]=(32,4),["program-step"]=(33,3),["byte-sweep"]=(34,2),["deferred-shape"]=(35,1),["noise-priority"]=(36,2),
        ["track-direct"]=(37,1),["group-end"]=(38,1),["morph-refresh"]=(39,1),["table-envelope"]=(40,5),["pitch-program"]=(41,4),
        ["word-sweep"]=(42,3),["shared-envelope-off"]=(43,0),["track-literal"]=(44,1),["wave-interpolate"]=(45,5),["wave-flags"]=(46,2),
        ["wave-gate"]=(53,1),["wave-interpolate-off"]=(47,0),["wave-envelope"]=(48,4),["wave-envelope-off"]=(49,0)
    };
    private static readonly Dictionary<string,(int Op,int Args)> Manbow = new()
    {
        ["start-attenuation"]=(0xf8,1),["combined-level"]=(0xf8,1),["scc-wave-policy"]=(0xf8,1),
        ["sweep-down"]=(0xd6,2),["sweep-up"]=(0xd7,2),["sweep-off"]=(0xd8,0),["attack"]=(0xd9,1),["morph-off"]=(0xda,0),
        ["floor"]=(0xdb,1),["floor-off"]=(0xdc,0),["fine-envelope"]=(0xdd,2),["loop-mark"]=(0xde,0),["loop-count"]=(0xdf,1),
        ["morph"]=(0xf8,2),["manbow-envelope"]=(0xf8,2),["classic-envelope"]=(0xf8,3)
    };
    internal static readonly string[] NoteNames={"c","c+","d","d+","e","f","f+","g","g+","a","a+","b","r"};
    private sealed record Fixup(int At,string Label,bool Short=false);
    private static int Num(string s,int min=0,int max=255)
    {
        int n=int.Parse(s,System.Globalization.CultureInfo.InvariantCulture);
        if(n<min||n>max)throw new FormatException($"Value {s} is outside {min}..{max}.");return n;
    }
    internal static Result Assemble(IEnumerable<string> lines)
    {
        var waves=new SortedDictionary<int,byte[]>();var aliases=new Dictionary<int,int>();
        var patches=new SortedDictionary<int,byte[]>();var drums=new SortedDictionary<int,byte[]>();
        var parts=new Dictionary<int,string>();var rhythms=new SortedDictionary<int,string>();
        var pitchTables=new SortedDictionary<int,int[]>();var maps=new SortedDictionary<int,string[]>();
        var labels=new Dictionary<string,int>();var code=new List<byte>();var fixes=new List<Fixup>();
        int version=1,timer=198;string title="Packed note score";
        void Put(params int[] values) {foreach(int n in values) {if(n is <0 or >255)throw new FormatException("Invalid musical byte.");code.Add((byte)n);}}
        void Word(int n) {Put(n&255,(n>>8)&255);}
        void Target(string label,bool small=false) {fixes.Add(new(code.Count,label,small));Put(0);if(!small)Put(0);}
        foreach(string raw in lines)
        {
            string line=raw.Split(';')[0].Trim();if(line.Length==0)continue;
            if(line.EndsWith(':')) {string label=line[..^1];if(!Regex.IsMatch(label,@"^[A-Za-z_]\w*$")||!labels.TryAdd(label,code.Count))throw new FormatException("Invalid or duplicate label: "+label);continue;}
            int space=line.IndexOf(' ');string name=space<0?line:line[..space];string rest=space<0?"":line[(space+1)..].Trim();
            string[] args=Regex.Split(rest,@"[\s,]+").Where(x=>x.Length>0).ToArray();
            if(name=="title") {title=rest;continue;}if(name=="timer") {timer=Num(rest);continue;}
            if(name is "wave" or "wave-alias")
            {
                int i=Num(args[0],0,63);if(waves.ContainsKey(i))throw new FormatException("Duplicate wave.");
                if(name=="wave-alias") {int j=Num(args[1],0,63);if(j>=i||!waves.ContainsKey(j)||args.Length!=2)throw new FormatException("Wave alias requires an earlier wave.");waves[i]=waves[j];aliases[i]=j;version=Math.Max(version,2);}
                else {if(args.Length!=33)throw new FormatException("Wave requires 32 samples.");waves[i]=args.Skip(1).Select(x=>(byte)(Num(x,-128,255)&255)).ToArray();}continue;
            }
            if(name is "opll-patch" or "opll-drums")
            {
                var table=name=="opll-patch"?patches:drums;int count=name=="opll-patch"?8:6,id=Num(args[0],0,15);
                if(args.Length!=count+1||table.ContainsKey(id))throw new FormatException("Invalid OPLL resource.");table[id]=args.Skip(1).Select(x=>(byte)Num(x)).ToArray();continue;
            }
            if(name is "pitch-table" or "instrument-map")
            {
                int id=Num(args[0],0,15);if(args.Length is <13 or >129)throw new FormatException("Table requires 12..128 entries.");
                if(name=="pitch-table") {if(!pitchTables.TryAdd(id,args.Skip(1).Select(x=>Num(x,0,65535)).ToArray()))throw new FormatException("Duplicate pitch table.");}
                else if(!maps.TryAdd(id,args.Skip(1).ToArray()))throw new FormatException("Duplicate instrument map.");version=6;continue;
            }
            if(name is "part" or "rhythm")
            {
                int id=Num(args[0],0,name=="part"?14:9);if(args.Length!=2)throw new FormatException("Invalid route.");
                if(name=="part") {if(!parts.TryAdd(id,args[1]))throw new FormatException("Duplicate part.");}else if(!rhythms.TryAdd(id,args[1]))throw new FormatException("Duplicate rhythm.");continue;
            }
            bool ol=name.StartsWith("opll-",StringComparison.Ordinal);if(ol){name=name[5..];version=Math.Max(version,3);}
            int opcodeAt=code.Count;
            if(Standard.TryGetValue(name,out var st))
            {
                version=Math.Max(version,5);Put(0xf8,65,st.Sub);
                if(name is "standard-note" or "standard-rhythm") {Word(Num(args[0],1,65535));int pitch=Num(args[1],0,name=="standard-note"?127:31);if(name=="standard-note"&&(pitch&15)>11)throw new FormatException("Invalid pitch.");Put(pitch,Num(args[2],0,7));}
                else if(name=="standard-detune") {Word(Num(args[0],-32768,32767)&65535);Put(Num(args[1],0,1));}
                else if(name=="standard-escape") {Put(Num(args[0],0,1),Num(args[1],1));Target(args[2]);}
                else if(name=="standard-lfo") {int mode=Num(args[0],0,6),n=new[]{5,0,0,1,1,2,1}[mode];if(args.Length!=n+1)throw new FormatException("Invalid LFO operands.");Put(mode);foreach(string a in args.Skip(1))Put(Num(a));}
                else {if(args.Length!=st.Args)throw new FormatException("Invalid "+name+" operands.");foreach(string a in args)Put(Num(a));}
            }
            else if(Extended.TryGetValue(name,out var ext))
            {
                if(name=="wave-gate"&&Num(args[0]) is >=160 and <255)throw new FormatException("Invalid wave gate target.");
                version=6;Put(0xf8,65,ext.Sub);
                if(name is "table-envelope" or "pitch-program") {Target(args[0]);foreach(string a in args.Skip(1))Put(Num(a));}
                else if(name=="program-step") {Word(Num(args[0],0,4095));Put(Num(args[1],0,15));}
                else {if(args.Length!=ext.Args)throw new FormatException("Invalid "+name+" operands.");foreach(string a in args)Put(Num(a));}
            }
            else if(Stage.TryGetValue(name,out var stage))
            {
                version=Math.Max(version,4);Put(0xf8,65,stage.Sub);
                if(name=="signed-detune")Word(Num(args[0],-32768,32767)&65535);
                else {if(args.Length!=stage.Args)throw new FormatException("Invalid "+name+" operands.");foreach(string a in args)Put(Num(a));}
            }
            else if(name=="octave")Put(0xd0+Num(args[0],0,ol?7:5));
            else if(name=="pitch") {int period=Num(args[0],0,4095);Put(Num(args[1],0,12)*16+(period>>8),period&255);}
            else if(name=="step")Put(Num(args[0],0,207));
            else if(Regex.IsMatch(name,@"^(?:[a-g]\+?|r)%\d+$")) {string[] n=name.Split('%');int pitch=Array.IndexOf(NoteNames,n[0]);if(pitch<0)throw new FormatException("Invalid note.");Put(pitch*16+Num(n[1],1,16)-1);}
            else if(Manbow.TryGetValue(name,out var mb))
            {
                version=Math.Max(version,2);if(args.Length!=mb.Args)throw new FormatException("Invalid "+name+" operands.");Put(mb.Op);
                if(name=="manbow-envelope")Put(64);else if(name=="classic-envelope")Put(67);else if(name=="combined-level")Put(66);
                else if(name is "scc-wave-policy" or "start-attenuation")Put(65,name=="scc-wave-policy"?23:24);
                int i=0;foreach(string a in args){int n=Num(a);Put(name=="morph"&&i++==0?n|128:n);}
            }
            else if((ol?Opll:Base).TryGetValue(name,out var op))
            {
                Put(op.Op);
                if(name is "call" or "jump")Target(args[0]);
                else if(name is "repeat" or "repeat2") {Put(Num(args[0],1));Target(args[1]);}
                else if(name=="repeat-short") {Put(Num(args[0],1));Target(args[1],true);}
                else {if(args.Length!=op.Args)throw new FormatException("Invalid "+name+" operands.");foreach(string a in args)Put(Num(a));}
                if(ol&&name=="voice"&&code[opcodeAt+1]<16&&!patches.ContainsKey(code[opcodeAt+1]))throw new FormatException("Undefined OPLL custom patch.");
                if(ol&&name=="drum-preset"&&!drums.ContainsKey(code[opcodeAt+1]))throw new FormatException("Undefined OPLL drum preset.");
            }
            else throw new FormatException("Unknown musical operation: "+name);
        }
        if(parts.Keys.Any(i=>i>=8))version=Math.Max(version,3);
        if(!parts.Keys.Order().SequenceEqual(Enumerable.Range(0,version>=3?15:8)))throw new FormatException("Missing musical routes.");
        if(version>=3&&waves.Count==0)waves[0]=new byte[32];
        if(waves.Count==0||!waves.Keys.SequenceEqual(Enumerable.Range(0,waves.Count)))throw new FormatException("Wave IDs must be dense from zero.");
        var data=new List<byte>(new byte[26]);int waveoff=data.Count;data.AddRange(new byte[waves.Count*2]);
        void Store(int at,int value) {data[at]=(byte)value;data[at+1]=(byte)(value>>8);}
        int Read(int at)=>data[at]|(data[at+1]<<8);
        foreach(var (id,wave) in waves) {Store(waveoff+id*2,aliases.TryGetValue(id,out int alias)?Read(waveoff+alias*2):data.Count);if(!aliases.ContainsKey(id))data.AddRange(wave);}
        int rhythmoff=data.Count;data.AddRange(new byte[20]);int oldir=data.Count;
        if(version>=3)
        {
            data.AddRange(Encoding.ASCII.GetBytes("OL1\0"));data.AddRange(new byte[78]);
            foreach(var (table,at) in new[]{(patches,oldir+18),(drums,oldir+50)})foreach(var (id,bytes) in table){Store(at+2*id,data.Count);data.AddRange(bytes);}
        }
        int seqoff=data.Count;
        int Resolve(string label) {if(!labels.TryGetValue(label,out int pos)||pos>=code.Count)throw new FormatException("Undefined or non-event label: "+label);return seqoff+pos;}
        foreach(var (id,label) in parts)Store(id<8?10+id*2:oldir+4+(id-8)*2,Resolve(label));
        foreach(var (id,label) in rhythms)Store(rhythmoff+id*2,Resolve(label));
        foreach(var fix in fixes) {int target=Resolve(fix.Label);if(fix.Short){int offset=seqoff+fix.At-target;if(offset is <1 or >255)throw new FormatException("Short repeat exceeds 255 bytes.");code[fix.At]=(byte)offset;}else{code[fix.At]=(byte)target;code[fix.At+1]=(byte)(target>>8);}}
        data.AddRange(code);
        if(version>=6)
        {
            int ex=data.Count;data.AddRange(Encoding.ASCII.GetBytes("EX1\0"));data.Add((byte)pitchTables.Count);data.Add((byte)maps.Count);
            int desc=data.Count;data.AddRange(new byte[4*(pitchTables.Count+maps.Count)]);int i=0;
            foreach(var (id,values) in pitchTables) {int at=desc+4*i++;data[at]=(byte)id;data[at+1]=(byte)values.Length;Store(at+2,data.Count);foreach(int n in values){data.Add((byte)n);data.Add((byte)(n>>8));}}
            foreach(var (id,values) in maps) {int at=desc+4*i++;data[at]=(byte)id;data[at+1]=(byte)values.Length;Store(at+2,data.Count);foreach(string label in values){int n=Resolve(label);data.Add((byte)n);data.Add((byte)(n>>8));}}
            data.Add((byte)ex);data.Add((byte)(ex>>8));
        }
        if(data.Count>4096)throw new FormatException($"Musical object is {data.Count} bytes; maximum is 4096.");
        if(data.Count>2560)version=Math.Max(version,2);
        data[0]=(byte)'P';data[1]=(byte)'N';data[2]=(byte)('0'+version);data[3]=0;
        Store(4,data.Count);Store(6,waveoff);Store(8,rhythmoff);
        return new(data.ToArray(),title,timer);
    }
}
