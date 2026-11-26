using System.Text;
using MDPlayer.MucoNative;

// MUC output follows the MucoMSX toolchain (muc2mub), the reference compiler:
// output mode and MUB layout, the column-1 track rule, rest merging and ties.
internal static class MucoMsxCompileTests
{
    internal static void Run(Action<bool,string> check)
    {
        byte[] Compile(string score)=>NativeMucCompiler.Compile(Encoding.ASCII.GetBytes(score),_=>null);
        bool Same(string a,string b)=>Compile(a).SequenceEqual(Compile(b));
        bool HasText(byte[] data,string text)=>Encoding.ASCII.GetString(data).Contains(text,StringComparison.Ordinal);

        // A classic score is a MUCOM88 v1.7 MUB8 played in classic mode: no #driver tag.
        byte[] classic=Compile("#title classic\nA t200 o4 l8 cdef\nD t200 o4 c4\n");
        check(Encoding.ASCII.GetString(classic,0,4)=="MUB8","Classic score is not MUB8.");
        check(!HasText(classic,"#driver"),"Classic score was written in mucomDotNET mode.");
        // An explicit driver tag, or a MucoMSX extension, keeps mucomDotNET mode.
        check(HasText(Compile("#driver mucomDotNET\nA t200 o4 c4\n"),"#driver mucomDotNET"),"Explicit #driver tag lost.");
        check(HasText(Compile("A t200 o4 {cg}4\n"),"#driver mucomDotNET"),"Pitched portamento is not mucomDotNET mode.");
        check(HasText(Compile("A0 t200 o4 c4\n"),"#driver mucomDotNET"),"Page digit is not mucomDotNET mode.");

        // Column-1 rule: an indented track label is not a track row and is ignored;
        // an indented header comment does not become music.
        check(Same("A t200 o4 c4\n G o4 c4\n","A t200 o4 c4\n"),"Indented track row was compiled.");
        check(Same("#title x\n   note text\nA t200 o4 c4\n","#title x\nA t200 o4 c4\n"),"Indented text line was compiled.");

        // Rests merge only when they follow each other directly.
        check(Same("A t200 l8 r r\n","A t200 l8 r4\n"),"Adjacent rests were not merged.");
        check(!Same("A t200 l8 r %16 r\n","A t200 l8 r4\n"),"Rests separated by a command were merged.");
        check(!Same("A t200 l8 r\nA r\n","A t200 l8 r4\n"),"Rests on separate rows were merged.");

        // A tie continues into the next row; a command between tie and note ends the
        // merge (the notes stay joined by FD), except the direct count %.
        check(Same("A t200 o4 c4&\nA c4\n","A t200 o4 c4&c4\n"),"Tie across rows was not merged.");
        check(!Same("A t200 o4 c4&l4c\n","A t200 o4 c4&c4\n"),"Tie through a command was merged.");
        check(Same("A t200 o4 c4&%32c\n","A t200 o4 c4&c4\n"),"Direct count % ended the tie.");
        // The note a slide plays is not one a tied note is joined to: "{ff+}%3&f%9" keeps
        // the slide, the FD and the tied note, as it does with a command in between.
        check(Same("A t200 o4 {ff+}%3&f%9\n","A t200 o4 {ff+}%3&o4f%9\n"),"A note tied on to a slide's first note was joined to the slide.");
        check(Compile("A t200 o4 {ff+}%3&f%9\n").AsSpan().IndexOf(new byte[]{0xff,0xf9,0x35,0x36,3,0,3,0x35,0xfd,9,0x35})>=0,"A slide tied on to its first note is not written as it reads.");
        check(Compile("A t200 o4 {ff+}%3&f+%9\n").AsSpan().IndexOf(new byte[]{0xff,0xf9,0x35,0x36,3,0,3,0x35,0xfd,9,0x36})>=0,"A slide tied on to its last note changed.");
        // MucoMSX layout: classic MUB8 keeps jump line 0, the final SSG-count byte and
        // MucoMSX's tag block; an extended score is muPb0100 with the tempo at A's head.
        byte[] tagged=Compile("#title layout\n; note\nA t200 o4 c4\n");
        check(BitConverter.ToUInt16(tagged,0x1e)==0,"Classic jump line is not 0.");
        int dataSize=(int)BitConverter.ToUInt32(tagged,8),tagOffset=(int)BitConverter.ToUInt32(tagged,12);
        check(tagged[0x50+dataSize-1]==0 && tagOffset==0x50+dataSize,"Classic DATA lacks its final byte.");
        string tagText=Encoding.ASCII.GetString(tagged,tagOffset,(int)BitConverter.ToUInt32(tagged,16));
        check(tagText.StartsWith("#title layout\r\n\0#mucver 1.7d\r\n",StringComparison.Ordinal),"Classic tag block differs from MucoMSX.");
        byte[] mupb=Compile("#title x\n\nA t200 o4 {cg}4\n");
        check(Encoding.ASCII.GetString(mupb,0,8)=="muPb0100" && BitConverter.ToUInt16(mupb,12)==11,"Extended score is not an 11-page muPb0100.");
        check(Encoding.ASCII.GetString(mupb).Contains("#title x\r\n#driver mucomDotNET\r\n\0#mucver 1.7d",StringComparison.Ordinal),"muPb tag block differs from MucoMSX.");
        // Tag text is the '#' rows of the whole header (any length), without blank or
        // comment rows; an indented '#' row ends the header.
        string Header(string source)=>Encoding.ASCII.GetString(MucoMsxMubWriter.HeaderText(Encoding.ASCII.GetBytes(source)));
        check(Header("; c\r\n\r\n#title T\n  ;x\n\t\n#*1{c}\r\nA c\n#memo late\n")=="#title T\r\n#*1{c}\r\n","Blank or comment rows are in the tag text.");
        check(Header("#title T\n #memo X\n#memo Y\n")=="#title T\r\n","Indented # row did not end the header.");
        check(Header("#title end")=="#title end\r\n","Header at EOF lacks its CRLF.");
        string longHeader=string.Concat(Enumerable.Range(0,1100).Select(i=>$"#memo {i:00000} {new string('x',40)}\n"));
        check(Header(longHeader+"A c\n")==longHeader.Replace("\n","\r\n"),"Long header was cut.");
        // A tie still pending at the end of a macro body is dropped (no FD), as in MucoMSX.
        byte[] Data(byte[] mub)=>mub.AsSpan(BitConverter.ToInt32(mub,4),BitConverter.ToInt32(mub,8)).ToArray();
        check(Data(Compile("#*1{c4&}\nA t200 o4 *1 d4\n")).SequenceEqual(Data(Compile("#*1{c4}\nA t200 o4 *1 d4\n"))),"Tie at a macro end was kept.");
        check(!Data(Compile("#*1{c4& d4}\nA t200 o4 *1\n")).SequenceEqual(Data(Compile("#*1{c4 d4}\nA t200 o4 *1\n"))),"Tie inside a macro body was dropped.");
        // MucoMSX refuses a #pcm tag whose file is missing (code 19) and a row over 1023
        // bytes (code 17) before compiling.
        Stream Pcm(string name)=>name=="PCM.BIN"?new MemoryStream(new byte[]{0}):null;
        string Refused(string score,Func<string,Stream> asset=null)
        {
            try{ NativeMucCompiler.Compile(Encoding.ASCII.GetBytes(score),asset??(_=>null)); return null; }
            catch(FormatException e){ return e.Message; }
        }
        check(Refused("#title t\n#pcm PCM.BIN\nA t200 o4 c4\n",Pcm)==null,"Existing #pcm file was refused.");
        string missing=Refused("#title t\n\n#pcm NONE.BIN\nA t200 o4 c4\n",Pcm);
        check(missing=="Error line 3 (code 19): PCM file in #pcm tag not found\n> #pcm NONE.BIN","Missing #pcm file was not code 19 on its row.");
        check(Refused("#PCM  none.bin ; x\nA t200 o4 c4\n")?.Contains("(code 19)")==true,"#PCM tag name is not case-insensitive.");
        check(Refused("#pcm P*.BIN\nA t200 o4 c4\n",_=>new MemoryStream(new byte[]{0}))?.Contains("(code 19)")==true,"Wildcard #pcm name was accepted.");
        // A voice number the #voice file does not reach is refused by name, not as an index
        // error: a file of 68 voices holds @0 to @67.
        Stream Voices(string name)=>name.Equals("V.DAT",StringComparison.OrdinalIgnoreCase)?new MemoryStream(Enumerable.Range(0,68*32).Select(i=>(byte)(i%32==9||i%32==10||i%32==11||i%32==12?31:0)).ToArray()):null;
        check(Refused("#voice V.DAT\nA t200 @67 o4 c4\n",Voices)==null,"The last voice of the voice file was refused.");
        string past=Refused("#voice V.DAT\nA t200 @68 o4 c4\n",Voices);
        check(past!=null && past.Contains("@68") && past.Contains("@0 to @67") && !past.Contains("outside the bounds"),"A voice past the end of the voice file was not refused by name: "+past);
        // A #pcm tag without a name selects MUCOMPCM.BIN, which must exist (MucoMSX 2026-11-17).
        Stream Default(string name)=>name.Equals("MUCOMPCM.BIN",StringComparison.OrdinalIgnoreCase)?new MemoryStream(new byte[0x400]):null;
        check(Refused("#title t\n#pcm\nA t200 o4 c4\n")=="Error line 2 (code 19): PCM file in #pcm tag not found\n> #pcm"
            && Refused("#pcm   \nA t200 o4 c4\n")?.Contains("(code 19)")==true,"#pcm without a name was accepted without MUCOMPCM.BIN.");
        check(Refused("#pcm\nA t200 o4 c4\n",Default)==null && Refused("#pcm \t\nA t200 o4 c4\nK @1 c4\n",Default)==null,"#pcm without a name did not take MUCOMPCM.BIN.");
        check(Refused("#PCMInvert On\nA t200 o4 c4\n")==null,"#PCMInvert was read as #pcm.");
        check(Refused("A t200 o4 c4\n#pcm NONE.BIN\n")==null && Refused("#title t\n #pcm NONE.BIN\nA t200 o4 c4\n")==null,"#pcm outside the header was checked.");
        check(Refused("; c\n#pcm NONE.BIN\nA t200 o4 c4\n")?.StartsWith("Error line 2 (code 19)")==true,"Comment row hid the #pcm tag.");
        string row1023="A t200 o4 c4"+new string(' ',1023-12), row1024=row1023+" ";
        check(Refused(row1023+"\r\n")==null,"A 1023-byte row was refused.");
        check(Refused("#title t\r\n"+row1024+"\r\n")?.StartsWith("Error line 2 (code 17): Line/macro exceeds 1023 bytes")==true,"A 1024-byte row was not code 17.");
        check(Refused("; "+new string('x',1100)+"\nA t200 o4 c4\n")?.Contains("(code 17)")==true,"A long comment row was not code 17.");
        // muc2mub's MSX grammar (here the packed path) has no such row check.
        string notes=string.Concat(Enumerable.Repeat("c8",560));
        check(Refused("MSXA t200 o4 ?DB1 "+notes+"\n")==null,"A long packed MSX row was refused.");
        check(Refused("MSXA t200 o4 "+notes+"\n")?.Contains("(code 17)")==true,"A long plain MSX row was not code 17.");
        MucoMsx20261118(check,Refused);
        Console.WriteLine("PASS MucoMSX MUC output: classic/extended mode, column-1 rows, rest merging, ties, header tags, source refusals");
    }

    // Rules of the MucoMSX toolchain of 2026-11-18. The expected errors and hashes were
    // taken from MUC2MUB itself (MSX-DOS 2.20, and the toolchain's host harness).
    private static void MucoMsx20261118(Action<bool,string> check,Func<string,Func<string,Stream>,string> Refused)
    {
        byte[] Bytes(string score)=>Encoding.ASCII.GetBytes(score);
        string Sha(byte[] data)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();
        var opened=new List<string>();
        Stream Bank(string name){ opened.Add(name); return name.Equals("PCM.BIN",StringComparison.OrdinalIgnoreCase)?new MemoryStream(new byte[0x440]):null; }
        byte[] Compile(string score)=>NativeMucCompiler.Compile(Bytes(score),Bank);
        uint PcmSize(byte[] mub8)=>BitConverter.ToUInt32(mub8,24);
        const string K20="Error, line unknown (code 20): K part needs a #pcm tag";

        // #pcm rules: a K part with a note needs the tag (code 20); without the tag the
        // default PCM file is never opened; the file is attached only for a K note.
        check(Refused("A t200 o4 c4\nK @1 c4\n",Bank)==K20,"A K note without #pcm was not code 20.");
        check(Refused("A t200 o4 c4\nK {cd}4\n",Bank)==K20,"A K portamento without #pcm was not code 20.");
        check(Refused("#PCMInvert on\nA t200 o4 c4\nK @1 c4\n",Bank)==K20,"#PCMInvert counted as a #pcm tag.");
        check(Refused("A t200 o4 c4\nMSXD o4 c4\nK @1 c4\n",Bank)==K20,"A K note in a mixed MSX score without #pcm was not code 20.");
        opened.Clear();
        check(Refused("A t200 o4 c4\nK r4\n",Bank)==null && Refused("A t200 o4 c4\nK @1 r4\n",Bank)==null,"A K part without notes was refused.");
        check(!opened.Any(name=>name.Equals("MUCOMPCM.BIN",StringComparison.OrdinalIgnoreCase)),"A score without #pcm opened MUCOMPCM.BIN.");
        check(PcmSize(Compile("#pcm PCM.BIN\nA t200 o4 c4\nK @1 c4\n"))==0x440 && PcmSize(Compile("#pcm PCM.BIN\nA t200 o4 c4\nK c4\n"))==0x440,"The PCM file was not attached for a K note.");
        check(PcmSize(Compile("#pcm PCM.BIN\nA t200 o4 c4\nK @1 r4\n"))==0 && PcmSize(Compile("#pcm PCM.BIN\nA t200 o4 c4\n"))==0,"The PCM file was attached without a K note.");

        // A score without t/T has Timer-B 225; t0 stays 0.
        check(Sha(Compile("A o4 c4 d4\r\n"))=="32d9d59191e7fc0fe45567264edde316b892cde8100ac4ca89967007c4e7e023","Classic score without tempo differs from MucoMSX.");
        check(Sha(Compile("A o4 c4 {cd}4\r\n"))=="fa46027ae9b597664d447d06add8e8b880f4117e3eb1e0c6db96cbb9f34370e6","muPb score without tempo differs from MucoMSX.");
        byte[] zero=Compile("A t0 o4 c4\r\n");
        check(zero[BitConverter.ToInt32(zero,4)+5]==0,"t0 was replaced by the default tempo.");

        // Tempo in the MSX group of a mixed score: FA 26 n on FM-type tracks, FF F7 00 00 26 n on SSG tracks.
        check(Sha(Compile("A o4 c4\r\nMSXA t150 o4 c4 d4 t140 e4\r\n"))=="912dce37ee03c5cc453715d4d213a8dc4d816dd21fb8e761531f8716706639a4","MSXA tempo differs from MucoMSX.");
        check(Sha(Compile("A o4 c4\r\nMSXD t150 o4 c4 d4 t140 e4\r\n"))=="9578183bd5c679a62a44930f910785d9c0e7fff03e534d24a67050165c5c623a","MSXD tempo differs from MucoMSX.");

        // Macro nesting: four deep is classic, five to eight deep is muPb, nine is code 16.
        string macros=string.Concat(Enumerable.Range(1,10).Select(i=>"#*"+i+"{ "+(i==1?"c":"*"+(i-1))+" }\r\n"));
        check(Sha(Compile(macros+"A o4 *4\r\n"))=="d318888b09edbea893fcf2f47cd537676c98cb71d3ba8cadd80c09d7408710d9","Four nested macros differ from MucoMSX.");
        check(Sha(Compile(macros+"A o4 *5\r\n"))=="4427f65f1ba71e9c30d07871bf932365214bc8064cdc1bba688cfa9d504f8bfd","Five nested macros differ from MucoMSX.");
        check(Sha(Compile(macros+"A o4 *8\r\n"))=="4427f65f1ba71e9c30d07871bf932365214bc8064cdc1bba688cfa9d504f8bfd","Eight nested macros differ from MucoMSX.");
        check(Refused(macros+"A o4 *9\r\n",null)=="Error line 11 (code 16): Invalid macro definition\n> A o4 *9","Nine nested macros were not code 16.");
        check(Refused(macros+"A o4 c4\r\nMSXD o4 *9\r\n",null)?.StartsWith("Error line 12 (code 16)")==true,"Nine nested macros on an MSX row were not code 16.");
        check(Refused(macros+"A o4 c4 ; *9\r\nB @\"*9\" c4\r\n",null)?.Contains("(code 16)")!=true,"A macro id in a comment or a voice name was counted.");

        // An MSX-grammar score must fit one input window of the MucoMSX loader.
        string packed="MSXA t200 o4 ?DB1 c8c8\r\n";
        string Sized(int size)=>";"+new string('x',size-packed.Length-3)+"\r\n"+packed;
        check(MucoMsxSourceRules.SourceWindows(Bytes(Sized(14976)))==1 && MucoMsxSourceRules.SourceWindows(Bytes(Sized(14978)))==2
            && MucoMsxSourceRules.SourceWindows(Bytes(Sized(14976)+";xx"))==1,"Input windows differ from the MucoMSX loader.");
        check(Refused(Sized(14976),null)==null,"An MSX-grammar score of one window was refused.");
        check(Refused(Sized(14978),null)=="Error, line unknown (code 9): Command not valid for this channel","An MSX-grammar score of two windows was not code 9.");
        check(Refused("A t200 o4 c4\r\n;"+new string('x',900)+string.Concat(Enumerable.Repeat("\r\n;"+new string('x',900),20))+"\r\n",null)==null,"A long score in the ordinary grammar was refused.");

        // Companion files are found like MSX-DOS finds them: any letter case, then the name cut to 8.3.
        string folder=Path.Combine(Path.GetTempPath(),"mdp4mac-companion-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder,"MUCOMPCM.BIN"),new byte[1]);
            File.WriteAllBytes(Path.Combine(folder,"BOSCO_PC.BIN"),new byte[1]);
            string Found(string name)=>Path.GetFileName(MucoMsxSourceRules.FindCompanion(folder,name));
            check(Found("mucompcm.bin")=="MUCOMPCM.BIN","A companion file in another letter case was not found.");
            check(Found("bosco_pcm.bin")=="BOSCO_PC.BIN","A companion file under its 8.3 name was not found.");
            check(Found("none.bin")==null && Found("../MUCOMPCM.BIN")==null && Found("MUCO*.BIN")==null,"A companion file was found for a wrong name.");
            // The driver's default banks of the other chips are not MUCOMPCM.BIN under another name.
            check(new[]{"mucompcm_2nd.bin","mucompcm_3rd_B.bin","mucompcm_4th_B.bin","mucompcm_3rd_A.bin","mucompcm_4th_A.bin"}.All(n=>Found(n)==null),
                "A driver default bank name was found as MUCOMPCM.BIN.");
        }
        finally { Directory.Delete(folder,true); }
    }
}
