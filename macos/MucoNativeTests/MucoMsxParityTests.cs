using System.Security.Cryptography;
using System.Text;
using MDPlayer.MucoNative;

// MucoMSX (MUC2MUB and MUBPLAY v1.5 on the MSX) is the reference for songs with MSX chip rows.
// The hashes are those of the files MUC2MUB writes for the same text.
internal static class MucoMsxParityTests
{
    internal static void Run(Action<bool,string> check)
    {
        const string user="?IU0={17,49,11,7,231,218,18,7}\n";
        const string voice="  @1\n   0,4\n   31,0,0,15,0,30,0,1,0\n   31,0,0,15,0,0,0,1,0\n   31,0,0,15,0,30,0,1,0\n   31,0,0,15,0,24,0,1,0\n";
        byte[] Compile(string source)=>NativeMucCompiler.Compile(Encoding.ASCII.GetBytes(source),_=>null);
        string Hash(byte[] data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

        // A song loop (L) in an MSX chip row ends the row with the jump alone: 2 bytes more than
        // the same row without L, not 3. No title in the source gives an empty #title.
        string loop=user+voice+"A t184 C240 @1 v12 o4 l4 L [c d e f]2\nMSXA C240 l4 ?IU0 v12 o4 c L [o4 c d e f]2\nMSXD C240 l4 v12 o4 e L [o4 e f g a]2\n";
        byte[] looped=Compile(loop),straight=Compile(loop.Replace(" L "," "));
        check(looped.Length==649&&Hash(looped)=="80003bd953412a44b2760f97fa879ec669adfd2223158d940fd5f913cb06c4d6","A looped MSX chip row differs from MUC2MUB.");
        check(straight.Length==645&&Hash(straight)=="e2adab7aa5b224e15f5c3c8951c333ef5e7982c881eb78679bbc0e7e896cf68f","An MSX chip row without a loop differs from MUC2MUB.");

        // MUCOM88's braced FM voice may end with its name in quotes.
        const string numbers="0,4, 31,0,0,15,0,30,0,1,0, 31,0,0,15,0,0,0,1,0, 31,0,0,15,0,30,0,1,0, 31,0,0,15,0,24,0,1,0";
        string rows="A t184 C240 @1 v12 o4 l4 c d\nMSXA C240 l4 ?IU0 v12 o4 c d\n";
        byte[] named=Compile(user+"@1:{"+numbers+",\"BASS1\"}\n"+rows);
        check(named.SequenceEqual(Compile(user+"@1:{"+numbers+"}\n"+rows)),"A voice name changed the braced FM voice.");
        check(Hash(named)=="4cdc31f3059fc7f3d6f937b71f34106354f9d7ec6029f7ed3c1e4e10fe21491a","A named braced FM voice differs from MUC2MUB.");

        // MUBPLAY runs the OPNA rows and the MSX chip rows in the same tick: notes written on the
        // same clock start on the same sample, from the first note on and across a tempo change.
        string phase=user+voice+"A t184 C240 @1 v12 o4 l4 c d t150 e f t184 g a\nMSXA C240 l4 ?IU0 v12 o4 c d e f g a\n";
        var fm=new List<long>();var opll=new List<long>();NativeMucomSession session=null;
        session=new NativeMucomSession(Compile(phase),44_100,w=>
        {
            long at=session?.Samples??-1;
            if(w.Chip==NativeChip.Opna1&&w.Port==0&&w.Address==0x28&&(w.Value&0xf0)!=0)fm.Add(at);
            if(w.Chip==NativeChip.Opll&&w.Address==0x20&&(w.Value&0x10)!=0)opll.Add(at);
        });
        using(session)while(session.Status>0&&session.Samples<1_000_000)session.AdvanceSample();
        check(session.Error==""&&fm.Count==6,"The phase song did not play its six FM notes.");
        check(fm.SequenceEqual(opll),"MSX-MUSIC notes do not start with the OPNA notes: FM "+string.Join(',',fm)+" MSX-MUSIC "+string.Join(',',opll));
        check(fm[0]==0&&fm[1]==60*914-1&&fm[3]-fm[2]==60*1344,"The tick is not MucoMSX's 2*floor((203N+16)/32) samples.");
        Console.WriteLine("PASS MucoMSX parity: looped MSX chip rows, empty title, named FM voice, OPNA and MSX chip rows in phase");
    }
}
