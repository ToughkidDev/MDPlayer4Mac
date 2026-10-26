namespace MDPlayer.MucoNative;

internal sealed partial class PackedMusicPlayer
{
    private sealed class ProgramState
    {
        internal int Kt=-1,KtStart,Latched=-1,LatchedStart,Im=-1,ImStart,LastPitch=-1,Root=-1,Pattern=-1,Wait=1;
        internal bool Held,Active,Cancelled,Valid;
        internal Channel Aux;
        internal readonly byte[] Sound=new byte[6];
    }
    private sealed class TableProgram
    {
        internal int Root=-1,Pc,Wait=1,Interval=1,RepeatA,RepeatB,Extra,Last,Multiplier,Delay,Policy;
        internal bool Active;
        internal readonly Stack<int> Calls=new();
    }
    private readonly Dictionary<Channel,ProgramState> programs=new();
    private readonly Dictionary<Channel,TableProgram> levels=new(),pitches=new();
    private ProgramState Program(Channel c){if(!programs.TryGetValue(c,out var s))programs[c]=s=new();return s;}
    private TableProgram Table(Channel c,bool pitch){var d=pitch?pitches:levels;if(!d.TryGetValue(c,out var s))d[c]=s=new(){Last=c.W[12]};return s;}
    private static bool HeldOwns(Channel c,ProgramState s)=>s.Active||s.Im>=0||Bit(c.W[14],0);
    private bool Owned(Channel c)=>programs.TryGetValue(c,out var s)&&(s.Active||s.Cancelled||s.Held&&HeldOwns(c,s));
    private void SaveSound(Channel c,ProgramState s){s.Sound[0]=c.W[10];s.Sound[1]=c.W[11];s.Sound[2]=c.W[12];s.Sound[3]=c.W[13];s.Sound[4]=c.W[38];s.Sound[5]=c.W[39];s.Valid=true;}
    private void Publish(Channel c,ProgramState s){if(!s.Valid)return;Array.Copy(s.Sound,0,c.W,10,4);c.W[38]=s.Sound[4];c.W[39]=s.Sound[5];}
    private bool NoteProgram(Channel c,int pitch,int flags)
    {
        if(score.Version<6)return false;var s=Program(c);
        if(s.Held&&HeldOwns(c,s)){Publish(c,s);return true;}
        if((flags&4)==0)
        {
            if((flags&1)==0||s.LastPitch!=pitch)
            {
                s.LastPitch=pitch;s.Latched=s.Kt;s.LatchedStart=s.KtStart;
                if(s.Im>=0)
                {
                    int entry=s.ImStart+(pitch&15);var map=score.InstrumentMaps[s.Im];if(entry>=map.Length)throw new FormatException("Instrument map window exceeds table.");
                    s.Root=map[entry];s.Active=s.Cancelled=true;s.Wait=1;
                    s.Aux??=new Channel(c.Index,s.Root){Auxiliary=true};var a=s.Aux;
                    if(a.W[9]==0){a.W[9]=2;a.W[13]=2;a.W[19]=a.W[20]=1;a.Word(38,c.Word(38));}
                    a.Pc=s.Root;a.W[0]=1;a.Returns.Clear();a.W[5]=a.W[6]=0;
                }
                else if(!s.Active)s.Cancelled=false;
            }
        }
        else if(s.Im>=0){c.W[12]=0;if(s.Aux!=null)s.Aux.W[12]=0;}
        // A note/rest without an active program or selected map releases the
        // cancellation latch. Otherwise silent rests suppress native pitch
        // phases until a later pitched note happens to clear that latch.
        bool owns=s.Active||s.Im>=0;if(!owns)s.Cancelled=false;return owns;
    }
    private void TickOwnedProgram(Channel c)
    {
        if(!programs.TryGetValue(c,out var s)||s.Held||!s.Active||c.Remaining<=1)return;
        var a=s.Aux;
        if(--s.Wait==0)
        {
            int p=a.Pc;bool step=false;
            for(int budget=128;!step;)
            {
                if(--budget==0)throw new FormatException("Effect program has no duration boundary.");int start=p,op=score.Read(ref p);
                if(op==255||op==250&&a.Returns.Count==0){s.Active=false;a.W[12]=a.W[13]=0;break;}
                if(op<208)
                {
                    int gate=a.W[13]&3;
                    if(gate!=0)
                    {
                        if(Bit(a.W[14],7)){a.BasePeriod=(a.BasePeriod&65280)|op;ApplyDetune(a);}
                        else
                        {
                            if(gate==1)a.W[18]=B((op&15)+2);
                            else{a.BasePeriod=((op&15)<<8)|(Bit(a.W[14],5)?a.BasePeriod&255:score.Read(ref p));a.W[18]=B((op>>4)+2);}
                            Duration(a,a.W[19]==0?1:a.W[19]);OnDuration(a);Start(a);ApplyProgramFade(a);
                        }
                    }
                    step=true;
                }
                else if(op==229){envelopeShape=score.Read(ref p);psgPending|=32;a.W[13]|=4;}
                else if(op==228){noisePrimary=score.Read(ref p)&31;psgPending=(psgPending|1)&~2;}
                else if(op==248&&p+1<score.CodeEnd&&score.Bytes[p]==65&&score.Bytes[p+1]==33)
                {
                    p+=2;a.BasePeriod=score.ReadWord(ref p);a.W[18]=B(score.Read(ref p)+2);Duration(a,a.W[19]==0?1:a.W[19]);OnDuration(a);Start(a);ApplyProgramFade(a);step=true;
                }
                else Control(a,op,ref p,start);
            }
            a.Pc=p;s.Wait=a.W[19]==0?1:a.W[19];
        }
        else{Duration(a,a.Remaining-1);Held(a);}
        for(int i=10;i<=13;i++)c.W[i]=a.W[i];c.W[15]=B((c.W[15]&127)|(a.W[15]&128));c.Word(38,a.Word(38));SaveSound(c,s);
    }
    private void EndPrograms(Channel c){if(programs.TryGetValue(c,out var s)){s.Active=s.Held=s.Valid=s.Cancelled=false;}if(levels.TryGetValue(c,out var l))l.Active=false;if(pitches.TryGetValue(c,out var t))t.Active=false;}
    private void SelectTable(Channel c,bool instrument,ref int p)
    {
        int id=score.Read(ref p),start=score.Read(ref p);var s=Program(c);
        if(id!=255){int length=instrument?(score.InstrumentMaps.TryGetValue(id,out var im)?im.Length:0):(score.PitchTables.TryGetValue(id,out var kt)?kt.Length:0);if(start+12>length)throw new FormatException("Undefined or short MSX table window.");}
        if(instrument){s.Im=id==255?-1:id;s.ImStart=start;}else{s.Kt=id==255?-1:id;s.KtStart=start;}
    }
    private void ExtendedProgramControl(Channel c,int sub,ref int p)
    {
        switch(sub)
        {
            case 27:case 28:SelectTable(c,sub==28,ref p);return;
            case 29:var h=Program(c);h.Held=score.Read(ref p)!=0;if(h.Held&&!h.Valid)SaveSound(c,h);return;
            case 30:var x=Program(c);if(Owned(c)||Bit(c.W[14],0)){x.Active=x.Held=x.Valid=false;x.Cancelled=true;c.W[12]=c.W[13]=0;c.W[14]&=254;}else x.Held=false;return;
            case 31:var r=Program(c);if(r.Root>=0){r.Active=r.Cancelled=true;r.Wait=1;r.Aux.Pc=r.Root;r.Aux.Returns.Clear();r.Aux.W[5]=r.Aux.W[6]=0;}else if(r.Pattern>=0){c.PatternPc=r.Pattern;c.PatternRemaining=1;c.W[14]|=1;}return;
            case 32:QueueFade(score.Read(ref p),score.Read(ref p),score.Read(ref p),score.Read(ref p));return;
            case 33:c.BasePeriod=score.ReadWord(ref p);c.W[18]=B(score.Read(ref p)+2);Start(c);return;
            case 34:c.M[2]=B(score.Read(ref p));c.M[3]=c.M[4]=B(score.Read(ref p));c.M[25]|=8;c.M[5]&=223;c.M[28]=B((c.M[28]&15)|80);return;
            case 35:envelopeShape=score.Read(ref p);psgPending|=32;c.W[13]|=4;return;
            case 36:int noise=score.Read(ref p),policy=score.Read(ref p);SetPriorityNoise(c,noise,policy,true);return;
            case 37:SetDirectType(c,score.Read(ref p),false);return;
            case 38:
                int mask=score.Read(ref p);if((mask&(1<<c.Index))==0||score.Bytes[p]!=255)throw new FormatException("Group end must include its issuer and precede end.");
                for(int i=0;i<8;i++)if((mask&(1<<i))!=0){var v=channels[i];EndPrograms(v);v.Pc=p;Duration(v,1);v.W[0]=1;v.W[9]=1;Array.Clear(v.W,10,6);v.M[5]=v.M[28]=0;}
                c.Yield=true;return;
            case 39:if(score.Read(ref p)!=1||c.Index<3)throw new FormatException("Invalid morph refresh.");c.M[25]|=16;return;
            case 40:
                var l=Table(c,false);l.Root=score.ReadWord(ref p);score.CodePointer(l.Root);c.W[21]=B(score.Read(ref p));c.M[17]=B(score.Read(ref p));c.M[1]=B(score.Read(ref p));c.M[31]=B(c.M[1]>>4);c.M[29]=5;c.W[15]=B((c.W[15]&128)|1);return;
            case 41:
                var t=Table(c,true);t.Root=score.ReadWord(ref p);score.CodePointer(t.Root);int desc=score.Read(ref p);t.Policy=score.Read(ref p);t.Multiplier=desc&15;int hi=desc>>4;t.Delay=hi==0?0:hi==1?c.W[20]:B(c.W[12]+(hi-1)*c.W[20]);c.M[5]|=16;c.W[14]&=251;return;
            case 42:c.M[2]=B(score.Read(ref p));c.M[3]=c.M[4]=B(score.Read(ref p));int sign=score.Read(ref p);c.M[5]|=32;c.M[25]&=247;c.M[28]=B((c.M[28]&15)|80|(sign!=0?128:0));return;
            case 43:psgPending=0;c.W[13]&=243;return;
            case 44:SetDirectType(c,score.Read(ref p),true);return;
            case 53:case >=45 and <=49:WaveControl(c,sub,ref p);return;
            default:throw new FormatException("Unknown PN6 command "+sub);
        }
    }
    private void SetDirectType(Channel c,int type,bool literal)
    {
        if(type>4)throw new FormatException("Invalid direct MSX track type.");
        // Direct PN6 transitions operate on live effect state. The legacy FE
        // command alone saves/restores melodic flags through M[26..27].
        c.M[5]=B(literal?c.M[5]|4:c.M[5]&251);c.W[9]=B(type);
        if(type==4)
        {
            c.W[14]|=1;
            // Literal effect streams retain dormant note phases. Ordinary
            // direct rhythm mode retires them without creating a saved copy.
            if(!literal){c.W[14]&=17;c.W[15]&=252;}
        }
        else c.W[14]&=254;
    }
    private bool TryTablePitch(Channel c,int pitch,int raw,out int result)
    {
        result=0;if(!programs.TryGetValue(c,out var s)||s.Latched<0)return false;
        var table=score.PitchTables[s.Latched];int index=s.LatchedStart+(pitch&15);if(index>=table.Length)throw new FormatException("Pitch-table lookup exceeds table.");int octave=(pitch>>4)&7;
        result=table[index]>>octave;
        if(c.Standard){int delta=(short)(raw-NativeNotePeriods.SsgBase[pitch&15]);delta>>=octave;int scaled=(int)(((long)Math.Abs(delta)*(c.Index<3?58741:58624)+32768)>>16);result+=delta<0?-scaled:scaled;}
        return true;
    }
    private void ApplyPitchTable(Channel c,int pitch,bool unused){if(TryTablePitch(c,((5-c.W[22])<<4)|pitch,0,out int result))c.BasePeriod=result;}
    private void StartTableEnvelope(Channel c){var s=Table(c,false);ResetTable(s);ParseTable(c,s,false);}
    private static void ResetTable(TableProgram s){s.Pc=s.Root;s.RepeatA=s.RepeatB=s.Extra=0;s.Interval=1;s.Wait=1;s.Active=true;s.Calls.Clear();}
    private void TickTableEnvelope(Channel c)
    {
        if(c.Index<3&&Bit(c.W[13],2))return;var s=Table(c,false);
        if(c.M[17]!=0&&c.W[36]>=c.W[4]){c.W[12]=B(QuarthRelease(c,c.W[12]));s.Last=c.W[12];return;}
        if(s.Active&&--s.Wait==0)ParseTable(c,s,false);
    }
    private void TickPitchProgram(Channel c){var s=Table(c,true);if(s.Active&&--s.Wait==0)ParseTable(c,s,true);}
    private void ParseTable(Channel c,TableProgram s,bool pitch)
    {
        int p=s.Pc;
        for(int budget=256;--budget>0;)
        {
            int op=score.Read(ref p);
            if(op==255||op==250&&s.Calls.Count==0){s.Active=false;s.Pc=p;return;}
            if(op<208&&!pitch){int n=B((op&15)+2-s.Extra)-c.W[21];c.W[12]=B(n<0?0:Math.Max(c.M[0],n));s.Last=c.W[12];s.Pc=p;s.Wait=s.Interval;return;}
            switch(op)
            {
                case 225:s.Interval=score.Read(ref p);break;
                case 248:
                    if(score.Read(ref p)!=65)throw new FormatException("Invalid table program command.");int sub=score.Read(ref p);
                    if(!pitch&&sub==24){s.Extra=score.Read(ref p);break;}
                    if(pitch&&sub==3){int offset=(short)score.ReadWord(ref p);if(offset is <-8 or >7)throw new FormatException("Pitch program offset outside -8..7.");if(!(Bit(c.M[5],5)&&Bit(c.M[28],6))){ApplyDetune(c);c.Period+=Math.Sign(offset)*B(Math.Abs(offset)*s.Multiplier);}s.Pc=p;s.Wait=s.Interval;return;}
                    throw new FormatException("Unsupported table program instruction.");
                case 249:int call=score.ReadWord(ref p);if(s.Calls.Count>=8)throw new FormatException("Table call depth exceeds 8.");s.Calls.Push(p);p=call;break;
                case 250:p=s.Calls.Pop();break;
                case 251:case 252:
                    int count=score.Read(ref p),target=score.ReadWord(ref p);if(op==251&&pitch&&s.Policy==1){c.W[32]++;if(count!=0)p=target;break;}
                    int n=B((op==251?s.RepeatA:s.RepeatB)+1);if(n==count)n=0;else p=target;if(op==251)s.RepeatA=n;else s.RepeatB=n;break;
                case 253:p=score.ReadWord(ref p);break;
                default:throw new FormatException("Invalid table program opcode.");
            }
            score.CodePointer(p);
        }
        throw new FormatException("Table program has no duration boundary.");
    }
    private void ArmExtendedOnset(Channel c)
    {
        if(Bit(c.M[5],5)){ApplyDetune(c);int sweep=c.Period,carry=0;for(int i=0;i<(c.M[3]==0?256:c.M[3]);i++){int n=Bit(c.M[28],7)?sweep-c.M[2]-carry:sweep+c.M[2];carry=n<0||n>65535?1:0;sweep=n&65535;}c.Period=sweep;c.M[4]=c.M[3];c.M[28]|=64;}
        if(Bit(c.M[5],4)){var s=Table(c,true);ResetTable(s);s.Wait=s.Delay+1;if(s.Delay==0)ParseTable(c,s,true);}
        WaveOnset(c);
    }
    private void WordSweep(Channel c){if(!Bit(c.M[28],6))return;c.Period+=Bit(c.M[28],7)?c.M[2]:-c.M[2];c.M[4]--;if(c.M[4]==0){c.M[28]&=191;c.M[4]=c.M[3];ApplyDetune(c);}}
    private void ByteSweep(Channel c)
    {
        if(!Bit(c.M[28],6))return;c.M[4]--;int count=c.M[3]&7;if(count==0||(Bit(c.M[3],3)?!Bit(c.M[4],3):c.M[4]==0)){c.M[28]&=191;c.M[4]=c.M[3];c.Period=c.BasePeriod;return;}
        if(Bit(c.M[3],3)){int n=c.M[4]&7;c.Period=c.BasePeriod-(n==0?1:B(c.M[2]*n));}else c.Period=c.BasePeriod+c.M[2]*c.M[4];
    }
    private void StartRawLiteral(Channel c){if((c.W[13]&3)==1||!Bit(c.W[14],7))c.W[12]=B(Math.Max(0,c.W[18]-c.M[11]));if((c.W[13]&3)!=1)c.Period=c.BasePeriod;}
    private int noisePolicy;
    private void SetPriorityNoise(Channel c,int value,int policy,bool explicitPolicy)
    {
        if(explicitPolicy){if(policy>3||value!=255&&value>31)throw new FormatException("Invalid noise policy.");noisePolicy=policy;if(value==255)return;}
        value&=31;int primary=noisePolicy==3?-1:noisePolicy==0?0:2;if(c.Index==primary){noisePrimary=value;psgPending=(psgPending|1)&~2;}else{noiseSecondary=value;psgPending=(psgPending|4)&~8;}
    }
    private void EndNoise(Channel c){int primary=noisePolicy==3?-1:noisePolicy==0?0:2;if(noisePolicy==2){if(c.Index==primary){psgPending&=~3;if((psgPending&12)==12)psgPending=(psgPending|4)&~8;}else psgPending&=~12;return;}if(c.Index==primary){psgPending&=~2;if((psgPending&8)!=0)psgPending=(psgPending|4)&~8;}else psgPending&=~8;}
    private void CommitNoise(){if((psgPending&1)!=0){Psg(6,noisePrimary);psgPending=(psgPending|2)&~1;}else if((psgPending&2)==0&&(psgPending&4)!=0){Psg(6,noiseSecondary);psgPending=(psgPending|8)&~4;}if(noisePolicy==2&&(channels[0].W[13]&1)!=0&&(channels[2].W[13]&1)!=0)channels[0].W[13]=B((channels[0].W[13]|2)&254);}
}
