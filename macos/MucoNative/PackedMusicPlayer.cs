namespace MDPlayer.MucoNative;

// Direct interpretation of musical PN1..PN6 data. There is no CPU, machine
// memory, mapper, ROM, executable loader, or register-timeline expansion here.
internal sealed partial class PackedMusicPlayer
{
    private sealed class Channel
    {
        internal readonly int Index;
        // The compact format's byte arithmetic is deliberately retained. These
        // are per-voice musical parameters, not emulated CPU registers/memory.
        internal readonly byte[] W=new byte[44],U=new byte[32];
        internal byte[] M=new byte[32];
        internal int Pc,Remaining=1,PatternPc,PatternRemaining,WaveId,Mark=-1,LastReturn=-1;
        internal readonly Stack<int> Returns=new();
        internal bool Standard,Auxiliary,Yield;
        internal byte PreEnvelopeFlags;
        internal long Loops;
        internal Channel(int index,int pc){Index=index;Pc=pc;W[0]=1;W[4]=1;}
        internal int Word(int at)=>W[at]|(W[at+1]<<8);
        internal void Word(int at,int value){W[at]=(byte)value;W[at+1]=(byte)(value>>8);}
        internal int Period {get=>Word(10);set=>Word(10,value);}
        internal int BasePeriod {get=>Word(16);set=>Word(16,value);}
        internal bool Active=>W[0]!=0;
    }
    private readonly PackedScoreData score;
    private readonly Action<NativeChipWrite> output;
    private readonly Action<int> tempo;
    private readonly Channel[] channels;
    private readonly int[] sccCache=Enumerable.Repeat(255,16).ToArray();
    private readonly byte[] psg=new byte[14];
    private int mixer,nextMixer,noisePrimary,noiseSecondary,envelopeShape,envelopeLow,envelopeHigh,psgPending;
    private int wavePolicy,commandBudget;
    private bool baseEnded;
    internal bool Ended=>channels.All(c=>!c.Active);
    internal long Ticks {get;private set;}
    internal object Snapshot()=>channels.Select(c=>new{c.Pc,c.Remaining,w=c.W,m=c.M,u=c.U,c.Standard}).ToArray();
    internal long LoopCounter=>MusicalLoops;
    internal PackedMusicPlayer(PackedScoreData score,Action<NativeChipWrite> output,Action<int> tempo)
    {
        this.score=score;this.output=output;this.tempo=tempo;
        channels=score.Routes.Select((pc,index)=>new Channel(index,pc)).ToArray();
        if(score.UsesScc){Scc(5,0,0);Scc(3,0,0);for(int i=0;i<5;i++)for(int n=0;n<32;n++)Scc(4,i*32+n,n<16?128:127);}
        InitializeOpll();
    }
    private static byte B(int n)=>unchecked((byte)n);
    private static bool Bit(byte value,int bit)=>(value&(1<<bit))!=0;
    private static int Rol4(int n)=>((n<<4)|(n>>4))&255;
    private void Psg(int register,int value)
    {
        if(register==7)value=(value&63)|128;psg[register]=B(value);output(new(NativeChip.Psg,0,register,value&255));
    }
    private void Scc(int port,int register,int value)=>output(new(NativeChip.Scc,port,register,value&255));
    private void Opll(int register,int value)=>output(new(NativeChip.Opll,0,register,value&255));
    internal void Tick()
    {
        if(Ended)return;
        TickFade();
        if(!baseEnded)
        {
            Psg(7,mixer);
            for(int i=0;i<8;i++)
            {
                var c=channels[i];if(!c.Active)continue;
                TickOwnedProgram(c);
                if(c.Standard)TickStandard(c);else TickCompact(c);
            }
            CommitPsg();CommitScc();baseEnded=channels.Take(8).All(c=>!c.Active);
        }
        TickOpll();Ticks++;TrackCycle();
    }
    private void Duration(Channel c,int value){c.Remaining=value&65535;c.W[4]=B(value);}
    private void TickCompact(Channel c)
    {
        Duration(c,c.Remaining-1);
        if(c.Remaining==0)
        {
            // Compact byte sweeps rearm at the duration boundary, before
            // parsing controls. A mode command can still cancel this onset.
            if(score.Version>=6&&Bit(c.M[25],3)&&Bit(c.M[28],4))c.M[28]|=64;
            int p=c.Pc;Parse(c,ref p,false);
        }
        else if(!Owned(c))Held(c);
    }
    private void Parse(Channel c,ref int p,bool pattern)
    {
        commandBudget=4096;
        while(true)
        {
            if(--commandBudget<=0)throw new FormatException("MSX musical command loop has no duration boundary.");
            int start=p,op=score.Read(ref p);
            if(op==255){End(c);return;}
            if(op>=0xd0)
            {
                Control(c,op,ref p,start);
                if(!c.Active)return;if(c.Yield){c.Yield=false;return;}
                if(c.Standard&&!pattern){ParseStandard(c,ref p);return;}
                continue;
            }
            int type=c.W[9];
            if((type&1)!=0)
            {
                Duration(c,(op%16+1)*c.W[20]);OnDuration(c);
                int pitch=op>>4;if((type&8)==0)
                {
                    if(NoteProgram(c,((5-c.W[22])<<4)|pitch,pitch>=12?4:0)){c.Pc=p;return;}
                    if(pitch>=12)c.W[13]=0;
                    else
                    {
                        c.BasePeriod=CompactPitches[pitch]<<c.W[22];ApplyPitchTable(c,pitch,false);
                        c.W[18]=c.W[21];c.W[13]=2;Start(c);
                    }
                }
                c.Pc=p;
            }
            if((type&2)!=0)
            {
                int gate=c.W[13]&3;
                if(gate!=0)
                {
                    int token=(type&1)!=0?score.Read(ref p):op;
                    if(gate==1)c.W[18]=B((token&15)+2);
                    else if(Bit(c.W[14],7))c.BasePeriod=(c.BasePeriod&0xff00)|token;
                    else
                    {
                        int level=token>>4;
                        if(Bit(c.W[14],5))c.BasePeriod=(c.BasePeriod&255)|((token&15)<<8);
                        else c.BasePeriod=((token&15)<<8)|score.Read(ref p);
                        if(c.Index<3&&Bit(c.W[13],2))level=16;
                        c.W[18]=B(level+2);
                    }
                    StartLiteral(c);
                }
                if((type&8)==0){c.Pc=p;Duration(c,c.W[19]);}
            }
            if((type&4)!=0)
            {
                // A score note selects a shared effect phrase; its clock stays
                // in the main voice while literal steps advance independently.
                c.W[9]=9;Duration(c,(op%16+1)*c.W[20]);OnDuration(c);c.Pc=p;
                int id=op>>4;if(id>=10||score.Rhythms[id]==0)throw new FormatException("Undefined shared rhythm/effect phrase.");
                var ps=Program(c);if(ps.Held&&Owned(c)){Publish(c,ps);c.W[9]=4;return;}ps.Pattern=score.Rhythms[id];
                c.W[14]|=1;c.W[9]=10;int effect=score.Rhythms[id];Parse(c,ref effect,true);
                c.PatternPc=effect;c.PatternRemaining=c.W[19];c.W[9]=4;
            }
            return;
        }
    }
    private void Control(Channel c,int op,ref int p,int start)
    {
        if(op<0xd6){c.W[22]=B(op-0xd0);return;}
        if(op<0xe0)
        {
            if(score.Version<2)throw new FormatException("PN1 octave outside 0..5.");
            switch(op)
            {
                case 0xd6:case 0xd7:
                    c.M[5]&=0xdf;c.M[25]&=0xf7;c.M[2]=B(score.Read(ref p));c.M[3]=c.M[4]=B(score.Read(ref p));
                    if(c.M[2]==0||c.M[3]==0)throw new FormatException("Zero compact sweep parameter.");c.M[28]=B((c.M[28]&127)|(op==0xd7?128:0)|0x50);return;
                case 0xd8:c.M[5]&=0xdf;c.M[25]&=0xf7;c.M[28]&=0xaf;return;
                case 0xd9:
                    c.M[5]|=2;int attack=score.Read(ref p);c.M[9]=B(attack);c.M[21]=B(attack&15);c.M[19]=B(attack>>4);c.M[18]=B((c.M[18]&254)|(c.M[19]!=0?1:0));return;
                case 0xda:c.M[5]&=0xb7;c.W[15]|=128;return;
                case 0xdb:c.M[0]=B(score.Read(ref p));if(c.M[0]>15)throw new FormatException("Envelope floor outside 0..15.");return;
                case 0xdc:c.M[0]=0;return;
                case 0xdd:SetEnvelope(c,score.Read(ref p),score.Read(ref p),1);return;
                case 0xde:c.Mark=p;return;
                case 0xdf:
                    int count=score.Read(ref p);if(count==0||c.Mark<0)throw new FormatException("Compact repeat has no loop mark.");
                    int rep=B(c.W[5]+1);c.W[5]=rep==count?(byte)0:B(rep);if(rep!=count)p=c.Mark;return;
            }
        }
        switch(op)
        {
            case >=0xe0 and <=0xe3:
                if(Bit(c.M[25],3))c.M[28]&=0xaf;c.W[14]&=127;c.W[13]=B(op&3);
                c.M[5]&=0xb7;if((op&1)!=0){if(c.Index==(noisePolicy==3?-1:noisePolicy==0?0:2))psgPending=(psgPending|1)&~2;else psgPending=(psgPending|4)&~8;}
                if(op!=0xe0)c.W[19]=B(score.Read(ref p));else c.W[19]=B(score.Bytes[p]);return;
            case 0xe4:SetNoise(c,score.Read(ref p));return;
            case 0xe5:
                c.W[13]=B((c.W[13]&247)|4);envelopeShape=score.Read(ref p);psgPending|=32;
                if(score.Version==1){envelopeHigh=score.Read(ref p);envelopeLow=score.Read(ref p);psgPending|=192;}
                else FlushEnvelope();return;
            case 0xe6:
                envelopeHigh=score.Read(ref p);psgPending|=64;c.W[13]|=4;
                if(score.Version==1){envelopeLow=score.Read(ref p);psgPending|=128;}return;
            case 0xe7:envelopeLow=score.Read(ref p);psgPending|=128;c.W[13]|=4;return;
            case 0xe8:c.W[13]&=0xf3;if(score.Version==1)psgPending=0;return;
            case 0xe9:c.W[20]=B(score.Read(ref p));if(c.W[20]==0)throw new FormatException("Duration unit is zero.");return;
            case 0xea:c.W[21]=B(score.Read(ref p));return;
            case 0xeb:
                int times=score.Read(ref p),offset=score.Read(ref p),repeat=B(c.W[5]+1);c.W[5]=repeat==times?(byte)0:B(repeat);
                if(repeat!=times){p=p-1-offset;score.CodePointer(p);}return;
            case 0xec:c.W[14]&=0xbf;return;
            case 0xed:SetEnvelope(c,score.Read(ref p),score.Read(ref p),0);return;
            case 0xee:
                c.M[29]=0;c.W[15]&=0xbf;if(Bit(c.M[5],1))c.W[15]&=128;c.W[15]&=254;return;
            case 0xef:c.M[5]&=254;c.W[37]=B(score.Read(ref p));c.W[14]|=2;return;
            case 0xf0:c.M[5]&=254;c.W[14]&=253;return;
            case 0xf1:
                c.M[25]&=253;c.M[5]&=0xef;int vibrato=score.Read(ref p);c.W[14]|=0x14;c.W[28]=B(vibrato&15);c.W[27]=B(vibrato>>4);return;
            case 0xf2:c.M[5]&=0xef;c.W[14]&=0xe2;return;
            case 0xf3:c.W[26]=B(score.Read(ref p));c.W[14]|=12;return;
            case 0xf4:c.W[14]&=247;return;
            case 0xf5:c.W[14]|=128;return;
            case 0xf6:c.W[14]&=0x5f;return;
            case 0xf7:c.W[14]|=32;return;
            case 0xf8:
                int voice=score.Read(ref p);
                if(score.Version>=2&&voice==64){SetEnvelope(c,score.Read(ref p),score.Read(ref p),2);return;}
                if(score.Version>=2&&voice==65){Extended(c,score.Read(ref p),ref p);return;}
                if(score.Version>=2&&voice==66){int level=score.Read(ref p);c.W[21]=B(level&15);SetTarget(c,level>>4);return;}
                if(score.Version>=2&&voice==67){int variant=score.Read(ref p);if(variant>2)throw new FormatException("Unknown classic envelope profile.");SetEnvelope(c,score.Read(ref p),score.Read(ref p),(128>>variant)|130);return;}
                SetWave(c,voice&127,(voice&128)!=0,ref p);return;
            case 0xf9:
                int call=score.ReadWord(ref p);score.CodePointer(call);if(score.Version==1)c.LastReturn=p;else{if(c.Returns.Count>=32)throw new FormatException("MSX phrase call depth exceeds 32.");c.Returns.Push(p);}p=call;return;
            case 0xfa:
                if(c.Returns.Count>0)c.LastReturn=c.Returns.Pop();if(c.LastReturn<0)throw new FormatException("Return without a phrase call.");p=c.LastReturn;score.CodePointer(p);return;
            case 0xfb:case 0xfc:
                int total=score.Read(ref p),target=score.ReadWord(ref p),counter=op==0xfb?5:6;int value=B(c.W[counter]+1);c.W[counter]=value==total?(byte)0:B(value);if(value!=total){score.CodePointer(target);p=target;}return;
            case 0xfd:
                int jump=score.ReadWord(ref p);score.CodePointer(jump);if(!c.Auxiliary&&jump<start)c.Loops++;p=jump;return;
            case 0xfe:SetType(c,score.Read(ref p));return;
            default:throw new FormatException($"Unknown compact musical opcode {op:X2}.");
        }
    }
    private static readonly int[] CompactPitches={106,100,94,89,84,79,74,70,66,63,59,56,53};
    private void SetType(Channel c,int type)
    {
        c.M[5]&=251;int old=c.W[9];c.W[9]=B(type);
        if(score.Version==1){if(type==4){c.W[13]=c.W[14]=c.W[15]=0;}return;}
        if(type==1){if(old!=1){c.W[14]=c.M[26];c.W[15]=c.M[27];}}
        else if(old==1){c.M[26]=c.W[14];c.M[27]=c.W[15];c.W[14]=c.W[15]=0;}
    }
    private void SetTarget(Channel c,int value){if(value>15)throw new FormatException("Initial target outside 0..15.");c.M[5]|=2;c.M[20]=B(value);c.M[18]=B((c.M[18]&254)|(value!=0?1:0));}
    private void SetWave(Channel c,int id,bool morph,ref int p)
    {
        if(id>=score.Waves.Length)throw new FormatException("Undefined SCC wave.");c.WaveId=id;c.Word(38,score.U16(26+2*id));
        c.M[28]&=253;c.M[5]&=247;c.M[25]&=0xef;
        if(morph)
        {
            if(c.Index<3)throw new FormatException("Wave morph requires SCC.");int rate=score.Read(ref p);if(rate>63)throw new FormatException("Wave morph rate exceeds 63.");c.M[7]=B(rate);c.M[5]|=0x48;c.M[8]=c.M[10]=0;c.M[25]|=1;c.M[18]&=191;
        }
        c.W[15]|=128;
    }
    private void ApplyDetune(Channel c)
    {
        int pitch=c.BasePeriod;if(Bit(c.W[14],1))pitch+=c.W[37];if(Bit(c.M[5],0))pitch+=(short)(c.M[6]|(c.M[14]<<8));c.Period=pitch;
    }
    private void Start(Channel c)
    {
        ApplyDetune(c);OnNoteStart(c);c.W[14]&=0xef;
        c.W[15]&=B(score.Version>=2&&(c.M[29]!=0||Bit(c.M[5],1))?0xd7:0xe7);
        c.W[29]=c.W[30]=c.W[31]=c.W[32]=0;c.W[15]|=4;
        int level=c.W[18];if(Bit(c.W[14],6))level=Math.Max(0,level-c.W[21]);c.W[12]=B(level);
        if(!(c.Index<3&&Bit(c.W[13],2))&&Bit(c.W[15],1)){c.W[12]=8;c.W[15]&=251;}
        StartSelectedEnvelope(c);
    }
    private void Held(Channel c)
    {
        if(score.Version>=2&&!Bit(c.W[14],0))HeldExtensions(c);
        if(Bit(c.W[14],0)&&--c.PatternRemaining<=0)
        {
            int p=c.PatternPc;score.CodePointer(p);
            if(score.Bytes[p]==255){c.W[14]&=254;c.W[13]=0;if(score.Version>=2)c.W[12]=0;}
            else{c.W[9]=10;Parse(c,ref p,true);c.PatternPc=p;c.PatternRemaining=c.W[19];c.W[9]=4;}
        }
        if(Bit(c.W[14],2))Vibrato(c);
        if(Bit(c.W[15],0)&&(score.Version<2||c.M[29]==0))LegacyEnvelopeTick(c);
    }
    private void Vibrato(Channel c)
    {
        if(Bit(c.M[25],1)){PhaseVibratoTick(c);return;}
        c.W[30]++;int ticks=c.W[30];
        if(!Bit(c.W[14],4)&&Bit(c.W[14],3)){if(ticks!=c.W[26])return;c.W[30]=0;c.W[14]|=16;}
        else if(ticks!=c.W[27])return;
        c.W[29]^=255;c.Period=c.Period+(c.W[29]==0?c.W[28]:-c.W[28]);c.W[30]=0;
    }
    private void End(Channel c)
    {
        EndPrograms(c);c.M[5]&=191;c.W[0]=c.W[1]=c.W[5]=c.W[6]=0;for(int i=10;i<=15;i++)c.W[i]=0;
        if(c.Index<3)EndNoise(c);
    }
    private void SetNoise(Channel c,int value)
    {
        if(score.Version>=6){SetPriorityNoise(c,value,0,false);return;}
        value&=31;if(c.Index==0){noisePrimary=value;psgPending=(psgPending|1)&~2;}else{noiseSecondary=value;psgPending=(psgPending|4)&~8;}
    }
    private void FlushEnvelope()
    {
        if((psgPending&128)!=0){psgPending&=127;Psg(11,envelopeLow);}
        else if(score.Version==1)return;
        if((psgPending&64)!=0){psgPending&=191;Psg(12,envelopeHigh);}
        else if(score.Version==1)return;
        if((psgPending&32)!=0){psgPending&=223;if(ShouldWriteShape())Psg(13,envelopeShape);}
    }
    private void CommitPsg()
    {
        for(int i=0;i<3;i++)
        {
            var c=channels[i];Psg(i*2,c.W[10]);Psg(i*2+1,c.W[11]);
            if(!Bit(c.W[13],3)){Psg(8+i,FadeLevel(c.W[12],true));if(Bit(c.W[13],2))c.W[13]|=8;}
        }
        CommitNoise();FlushEnvelope();
        int[,] mixes={{137,129,136,128},{146,130,144,128},{164,132,160,128}};
        nextMixer=mixes[0,channels[0].W[13]&3]|mixes[1,channels[1].W[13]&3]|mixes[2,channels[2].W[13]&3];mixer=nextMixer;Psg(7,mixer);
    }
    private void CommitScc()
    {
        if(score.Version>=3&&!score.UsesScc)return;
        for(int i=0;i<5;i++)
        {
            var c=channels[i+3];for(int byteIndex=0;byteIndex<2;byteIndex++){int value=c.W[10+byteIndex];if(sccCache[3*i+byteIndex]!=value){sccCache[3*i+byteIndex]=value;Scc(1,i*2+byteIndex,value);}}
        }
        for(int i=0;i<5;i++) {int raw=channels[i+3].W[12],value=FadeLevel(raw,false);if(sccCache[3*i+2]!=raw){sccCache[3*i+2]=raw;Scc(2,i,value);}}
        int gates=0;for(int i=0;i<5;i++)if(GateFlags(channels[i+3])!=0)gates|=1<<i;
        if(sccCache[15]!=gates){sccCache[15]=gates;Scc(3,0,gates);}
        for(int i=0;i<5;i++)
        {
            var c=channels[i+3];if(!Bit(c.W[15],7))continue;c.W[15]&=127;
            int physical=i;
            if(i==4){if(score.Version==1)continue;if(wavePolicy==1)continue;if(wavePolicy==2)physical=3;}
            byte[] wave=WaveSamples(c);if(wave==null)continue;
            bool muted=!Bit(c.M[28],0)&&!Bit(c.M[28],2)&&!Bit(c.M[5],3);
            if(muted){if(waveGateSample>=0)Scc(4,waveGateSample,0);else Scc(3,0,0);}
            for(int n=0;n<32;n++)Scc(4,physical*32+n,wave[n]);
            if(physical==3&&(score.Version==1||wavePolicy!=0))for(int n=0;n<32;n++)Scc(4,128+n,wave[n]);
            if(muted){if(waveGateSample>=0)Scc(4,waveGateSample,sccCache[15]);else Scc(3,0,sccCache[15]);}
        }
    }
    private byte[] WaveSamples(Channel c)
    {
        if(Bit(c.M[28],0)||Bit(c.M[28],2)){if(Bit(c.M[28],0)&&c.Index==waveOwner&&(waveFlags&8)!=0&&c.Index<7)channels[c.Index+1].W[15]|=128;return GeneratedWave(c);}
        int ptr=c.Word(38),stride=Bit(c.M[5],3)?c.M[10]:1;
        if(ptr<score.U16(26)||ptr+31*stride>=score.U16(8))throw new FormatException("Wave morph reads beyond shared waveform data.");
        var wave=new byte[32];for(int n=0;n<32;n++)wave[n]=score.Bytes[ptr+n*stride];return wave;
    }
    private int GateFlags(Channel c)
    {
        int flags=c.W[13];if(Bit(c.M[25],3)&&(c.M[28]&80)!=0)return flags|64;
        // Morph release ownership belongs to the voice, independently of the
        // physical SCC/SCC+ wave-sharing policy. Static @N keeps that tail.
        if(score.Version!=5&&Bit(c.M[5],6))flags|=8;return flags;
    }
    internal void Stop()
    {
        foreach(var c in channels)c.W[0]=0;Psg(7,0x3f);for(int i=0;i<3;i++)Psg(8+i,0);
        Scc(3,0,0);for(int i=0;i<5;i++)Scc(2,i,0);for(int i=0;i<9;i++){Opll(0x20+i,0);Opll(0x30+i,15);}Opll(14,32);
    }
}
