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
        check(Refused("#pcm\nA t200 o4 c4\n")==null && Refused("#pcm   \nA t200 o4 c4\n")==null,"Empty #pcm value was refused.");
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
        Console.WriteLine("PASS MucoMSX MUC output: classic/extended mode, column-1 rows, rest merging, ties, header tags, source refusals");
    }
}
