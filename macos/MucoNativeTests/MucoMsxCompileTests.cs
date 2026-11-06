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
        check(tagText.StartsWith("#title layout\r\n; note\r\n\0#mucver 1.7d\r\n",StringComparison.Ordinal),"Classic tag block differs from MucoMSX.");
        byte[] mupb=Compile("A t200 o4 {cg}4\n");
        check(Encoding.ASCII.GetString(mupb,0,8)=="muPb0100" && BitConverter.ToUInt16(mupb,12)==11,"Extended score is not an 11-page muPb0100.");
        check(Encoding.ASCII.GetString(mupb).Contains("#driver mucomDotNET\r\n\0#mucver 1.7d",StringComparison.Ordinal),"muPb tag block differs from MucoMSX.");
        // A tie still pending at the end of a macro body is dropped (no FD), as in MucoMSX.
        byte[] Data(byte[] mub)=>mub.AsSpan(BitConverter.ToInt32(mub,4),BitConverter.ToInt32(mub,8)).ToArray();
        check(Data(Compile("#*1{c4&}\nA t200 o4 *1 d4\n")).SequenceEqual(Data(Compile("#*1{c4}\nA t200 o4 *1 d4\n"))),"Tie at a macro end was kept.");
        check(!Data(Compile("#*1{c4& d4}\nA t200 o4 *1\n")).SequenceEqual(Data(Compile("#*1{c4 d4}\nA t200 o4 *1\n"))),"Tie inside a macro body was dropped.");
        Console.WriteLine("PASS MucoMSX MUC output: classic/extended mode, column-1 rows, rest merging, ties");
    }
}
