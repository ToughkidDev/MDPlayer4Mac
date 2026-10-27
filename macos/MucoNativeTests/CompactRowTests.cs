using MDPlayer.MucoNative;

// Compact-row structure (| [ ] L), OPLL repeats and rests on effect/drum tracks, ?ID kit
// application, ?IR/?IU on compact OPLL rows, PSG-only songs without waves and morph reads.
internal static class CompactRowTests
{
    internal static void Run(Action<bool,string> check)
    {
        const string wave="?WD0={0,25,49,71,90,106,117,125,127,125,117,106,90,71,49,25,0,-25,-49,-71,-90,-106,-117,-125,-128,-125,-117,-106,-90,-71,-49,-25}\n";
        (PackedMusicPlayer P,List<NativeChipWrite> E) Make(string s)
        {
            var o=MsxMusicalCompiler.Parse(s,_=>null);var e=new List<NativeChipWrite>();
            return(new(new(o.Object),e.Add,_=>{}),e);
        }
        byte[] Bytes(string s)=>MsxMusicalCompiler.Parse(s,_=>null).Object;
        bool Rejected(string s,string text=null)
        {
            try{Bytes(s);return false;}catch(FormatException e){return text==null||e.Message.Contains(text);}
        }
        // Key-on ticks of an OPLL melody channel, or rhythm hits (register $0E) with their masks.
        List<int> OpllKeys(List<NativeChipWrite> e,List<int> ticks,int channel)
        {
            var on=new List<int>();bool keyed=false;
            for(int i=0;i<e.Count;i++)if(e[i].Chip==NativeChip.Opll&&e[i].Address==0x20+channel){bool k=(e[i].Value&16)!=0;if(k&&!keyed)on.Add(ticks[i]);keyed=k;}
            return on;
        }
        (List<NativeChipWrite> E,List<int> T) Play(string s,int frames)
        {
            var m=Make(s);var ticks=new List<int>();
            for(int t=0;t<frames&&!m.P.Ended;t++){m.P.Tick();while(ticks.Count<m.E.Count)ticks.Add(t);}
            return(m.E,ticks);
        }
        List<(int Tick,int Mask)> Hits((List<NativeChipWrite> E,List<int> T) r)
        {
            var hits=new List<(int,int)>();
            for(int i=0;i<r.E.Count;i++)if(r.E[i].Chip==NativeChip.Opll&&r.E[i].Address==14&&(r.E[i].Value&31)!=0)hits.Add((r.T[i],r.E[i].Value&31));
            return hits;
        }

        // ?FB0 ... ?FBn on OPLL melody and MSXG play the marked part n times in all.
        var fb=Play("MSXA ?XU1 ?XT1 @3 v12 o4 ?FB0 c%2 d%2 ?FB3 e%2 !\n",40);
        check(OpllKeys(fb.E,fb.T,0).Count==7,"OPLL ?FB0/?FB3 did not play the marked notes three times.");
        var fbDrum=Hits(Play("MSXG ?XU1 ?XT64 v14 ?FB0 ?ID16,2 ?ID8,2 ?FB2 !\n",40));
        check(fbDrum.Count==4&&fbDrum[0].Mask==16&&fbDrum[1].Mask==8,"MSXG ?FB0/?FB2 did not repeat the drum hits.");

        // A rest on MSXG hits nothing; on a ?XT4 track it starts no effect and does not stop playback.
        var rest=Hits(Play("MSXG ?XU1 ?XT64 v14 r%2 c+%2 r%2 g+%2 !\n",20));
        check(rest.Count==2&&rest[0]==(2,1)&&rest[1]==(6,8),"An MSXG rest hit drums or moved the following hits.");
        var fx=Make(wave+"PSGS ?XD0={ :90 ?XM1,1 ?NP0 ?XS$07 ! }\nMSXD ?XU1 ?XT4 v12 o4 r%2 c%2 r%2 c%2 !\n");
        for(int t=0;t<12;t++)fx.P.Tick();
        check(fx.E.Count(x=>x.Chip==NativeChip.Psg&&x.Address==6)>=2,"A ?XT4 rest stopped the effect track.");

        // ?ID hits apply the pending kit and volume, as note hits do, and a pending ?IB.
        string kit="?IT0={224,4,190,3,5,1}\n";
        var id=Play(kit+"MSXG ?XU1 ?XT64 ?IT0 v10 ?ID1,2 ?IB ?ID8,2 !\n",8);
        int firstHit=id.E.FindIndex(x=>x.Chip==NativeChip.Opll&&x.Address==14&&(x.Value&31)!=0);
        check(firstHit>0&&id.E.Take(firstHit).Any(x=>x.Chip==NativeChip.Opll&&x.Address==0x16&&x.Value==224)&&id.E.Take(firstHit).Any(x=>x.Chip==NativeChip.Opll&&x.Address==0x36&&x.Value!=0),
            "The first ?ID hit did not apply the pending kit and volume.");
        check(Hits(id).Any(h=>h.Mask==(16|8)),"?IB before ?ID did not add the bass drum.");

        // | is a bar line; [ ]n repeats (two levels, across lines); L loops back from !.
        check(Bytes(wave+"SCCA ?XU1 ?XT1 @0 v12 o4 c%2 | d%2 !\n").SequenceEqual(Bytes(wave+"SCCA ?XU1 ?XT1 @0 v12 o4 c%2 d%2 !\n")),"| changed a compact row.");
        var nested=Play("MSXA ?XU1 ?XT1 @3 v12 o4 [c%2 [d%2]2]3 e%2 !\n",60);
        check(OpllKeys(nested.E,nested.T,0).Count==10,"Nested compact repeats did not play 3 x (1 + 2) + 1 notes.");
        var lines=Play("MSXA ?XU1 ?XT1 @3 v12 o4 [c%1\nMSXA d%1]3 !\n",20);
        check(OpllKeys(lines.E,lines.T,0).Count==6,"A compact repeat across two lines did not play three times.");
        var loop=Make("MSXA ?XU1 ?XT1 @3 v12 o4 c%1 L d%1 e%1 !\n");for(int t=0;t<40;t++)loop.P.Tick();
        check(!loop.P.Ended,"Compact L did not loop.");
        check(Rejected(wave+"SCCA ?XU1 ?XT1 @0 v12 o4 [c%1 d%1 !\n","Unclosed"),"An unclosed compact repeat was accepted.");
        check(Rejected(wave+"SCCA ?XU1 ?XT1 @0 v12 o4 [c%1 / d%1]2 !\n","/"),"/ on a compact row was not explained.");
        check(Rejected(wave+"SCCS ?FD20={ [c%1 }\nSCCA ?XU1 ?XT1 @0 v12 o4 ?FC20 d%1] !\n","cross"),"A repeat crossing a definition was accepted.");

        // PSG-only compact songs need no wave; SCC rows still name what they miss.
        Bytes("MSXD ?XU1 ?XT1 ?EP0,$81,$38 v12 o4 c%4 !\n");
        check(Rejected("SCCA ?XU1 ?XT1 v12 o4 c%4 !\n","?WD"),"A compact SCC row without waves was accepted or not explained.");

        // ?IR / ?IU select the same instruments on compact OPLL rows as @n / @0.
        string patch="?IU0={33,97,29,7,130,143,16,7}\n";
        check(Bytes("MSXA ?XU1 ?XT1 ?IR3 v12 o4 c%4 !\n").SequenceEqual(Bytes("MSXA ?XU1 ?XT1 @3 v12 o4 c%4 !\n")),"Compact ?IR differs from @n.");
        check(Bytes(patch+"MSXA ?XU1 ?XT1 ?IU0 v12 o4 c%4 !\n").SequenceEqual(Bytes(patch+"MSXA ?XU1 ?XT1 @0 v12 o4 c%4 !\n")),"Compact ?IU differs from @0.");
        check(Rejected("MSXA ?XU1 ?XT1 ?WM0,1,1 v12 o4 c%4 !\n","?WM"),"An OPLL error did not name the two-letter command.");

        // A morph that steps past the last wave keeps playing.
        var morph=Make(wave+"SCCA ?XU1 ?XT1 @0 ?WM0,63,1 v12 o4 c%16 c%16 !\n");
        for(int t=0;t<40&&!morph.P.Ended;t++)morph.P.Tick();
        check(morph.P.Ended,"A long morph over one wave did not play to the end.");
        Console.WriteLine("PASS compact rows: | [ ] L, OPLL ?FB0, rests on drum/effect tracks, ?ID kit, ?IR/?IU, PSG without waves, morph wrap");
    }
}
