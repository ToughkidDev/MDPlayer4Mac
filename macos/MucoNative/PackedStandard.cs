namespace MDPlayer.MucoNative;

internal sealed partial class PackedMusicPlayer
{
    private static int UWord(Channel c,int at)=>c.U[at]|(c.U[at+1]<<8);
    private static void UWord(Channel c,int at,int value){c.U[at]=B(value);c.U[at+1]=B(value>>8);}
    private void TickStandard(Channel c)
    {
        Duration(c,c.Remaining-1);UWord(c,24,c.Remaining);
        if(c.Remaining==0){int p=c.Pc;ParseStandard(c,ref p);return;}
        if(c.Index<8&&Owned(c))return;if(c.Index>=8)HeldOpll(c);else Held(c);StandardGate(c);StandardEffects(c);
    }
    private void ParseStandard(Channel c,ref int p)
    {
        int budget=4096;
        while(true)
        {
            if(--budget==0)throw new FormatException("Standard MSX command loop has no note boundary.");
            int start=p,op=score.Read(ref p);
            if(op==255){if(c.Index>=8)EndOpll(c);else End(c);return;}
            if(op<0xd0)
            {
                int length=(op%16+1)*c.W[20],pitch=(c.U[30]<<4)|(op>>4),flags=(op>>4)>=12?4:0;StandardNote(c,length,pitch,flags,p);return;
            }
            if(op<0xd8&&(op<0xd6||c.Index>=8)){c.U[30]=B(op-0xd0);continue;}
            if(op==0xd8&&c.Index==14)
            {
                int mask=score.Read(ref p),length=score.Read(ref p);if(mask>31||length>15)throw new FormatException("Invalid OPLL drum step.");Duration(c,(length+1)*c.W[20]);UWord(c,24,c.Remaining);c.Pc=p;OpllDrumHit(mask);return;
            }
            if(op==0xf8&&p+1<score.CodeEnd&&score.Bytes[p]==65)
            {
                int sub=score.Bytes[p+1];
                if(sub is 8 or 18)
                {
                    p+=2;int duration=score.ReadWord(ref p),pitch=score.Read(ref p),flags=score.Read(ref p);
                    if(duration==0||flags>7||sub==18&&c.Index!=14)throw new FormatException("Invalid standard note packet.");StandardNote(c,duration,pitch,flags,p);return;
                }
            }
            if(op is 0xdd or 0xed or 0xee)c.U[0]&=254;
            if(op==0xf8&&p<score.CodeEnd)
            {
                if(score.Bytes[p]==64)c.U[0]&=254;
                else if(score.Bytes[p]==65&&p+1<score.CodeEnd&&score.Bytes[p+1] is 0 or 5 or 6 or 26)c.U[0]&=254;
            }
            if(c.Index>=8)OpllControl(c,op,ref p,start);else Control(c,op,ref p,start);
            if(op==0xea)
            {
                c.W[18]=c.W[21];c.U[0]&=239;c.U[29]=c.Index>=8?B((~c.W[21])&15):c.W[21];if(!Bit(c.U[0],0))c.W[12]=c.W[21];
            }
            if(!c.Active)return;
        }
    }
    private void StandardCommand(Channel c,int sub,ref int p)
    {
        switch(sub)
        {
            case 9:
                for(int i=4;i<10;i++)c.U[i]=B(score.Read(ref p));c.U[0]|=1;c.U[11]=1;c.M[29]=0;if(c.Index<8)c.W[15]&=0xbe;return;
            case 10:
                int detune=score.ReadWord(ref p),add=score.Read(ref p);if(add>1)throw new FormatException("Invalid detune addition mode.");if(add!=0)detune+=UWord(c,2);UWord(c,2,detune);c.U[31]=0;return;
            case 11:c.U[1]=B(score.Read(ref p));return;
            case 12:
                int mode=score.Read(ref p);switch(mode)
                {
                    case 0:
                        c.U[12]=c.U[17]=B(score.Read(ref p));c.U[13]=c.U[18]=B(score.Read(ref p));UWord(c,14,score.ReadWord(ref p));UWord(c,19,UWord(c,14));c.U[0]&=251;c.U[16]=B(score.Read(ref p));c.U[21]=B(c.U[16]>>1);c.U[0]|=2;return;
                    case 1:c.U[0]&=253;return;case 2:c.U[0]|=2;return;
                    case 3:c.U[12]=c.U[17]=B(score.Read(ref p));return;
                    case 4:c.U[13]=c.U[18]=B(score.Read(ref p));return;
                    case 5:UWord(c,14,score.ReadWord(ref p));UWord(c,19,UWord(c,14));c.U[0]&=251;return;
                    case 6:c.U[16]=B(score.Read(ref p));c.U[21]=B(c.U[16]>>1);return;
                    default:throw new FormatException("Unknown standard LFO mode.");
                }
            case 13:tempo?.Invoke(score.Read(ref p));return;
            case 14:
                c.Standard=true;c.W[20]=1;c.W[9]=1;c.W[31]=1;c.W[21]=c.W[18]=0;c.U[30]=5;c.U[31]=255;UWord(c,24,1);c.M[5]|=128;
                if(c.Index>=8){c.W[21]=c.W[18]=c.W[12]=15;if(c.Index<14){c.U[0]|=16;c.W[21]=c.W[18]=c.W[12]=13;}}
                if(c.Index<3){c.U[0]|=1;c.U[28]=8;}if(c.Index==14)StandardRhythmInit();return;
            case 15:
                int counter=score.Read(ref p),count=score.Read(ref p),target=score.ReadWord(ref p);if(counter>1||count==0)throw new FormatException("Invalid repeat escape.");
                if(B(c.W[5+counter]+1)==count){c.W[5+counter]=0;score.CodePointer(target);p=target;}return;
            case 16:
                int mixerValue=score.Read(ref p);if((mixerValue&0xf6)!=0||c.Index>=3)throw new FormatException("Standard mixer requires a PSG channel.");c.U[28]=B(mixerValue);ApplyMixer(c);return;
            case 17:
                int noise=score.Read(ref p);if(noise>31||c.Index>=3)throw new FormatException("Standard noise requires PSG and a 0..31 period.");
                // MUCOM w updates the shared latch, including when used in a
                // compact phrase. ?NP keeps its separate ownership policy.
                noisePrimary=(int)(((long)noise*58741+32768)>>16);psgPending=(psgPending|1)&~2;return;
            case 19:StandardRhythmVolumes(c,ref p);return;
            case 20:
                int raw=B(c.U[29]+score.Read(ref p));if(!Bit(c.U[0],4)&&raw>=16)return;StandardVolume(c,raw);return;
            case 21:
                int kind=score.Read(ref p),value=score.Read(ref p);if(kind>1||kind==1&&(c.Index<8||c.Index==14))throw new FormatException("Invalid standard volume kind.");c.U[0]=B((c.U[0]&239)|(kind==1?16:0));if(kind==0&&value>=16)throw new FormatException("Direct volume outside 0..15.");StandardVolume(c,value);return;
            case 22:
                int carrier=score.Read(ref p);if(c.Index is <8 or >=14||carrier>127)throw new FormatException("Carrier correction requires OPLL melody.");c.U[28]=B(carrier);if(Bit(c.U[0],4))StandardVolume(c,c.U[29]);return;
            default:throw new FormatException("Unknown standard musical command "+sub);
        }
    }
    private void StandardNote(Channel c,int duration,int pitch,int flags,int next)
    {
        Duration(c,duration);UWord(c,24,duration);c.U[26]=B(pitch);c.U[27]=B(flags);c.Pc=next;
        if(c.Index==14){StandardRhythmNote(c);return;}
        OnDuration(c);
        if(c.Index<8&&NoteProgram(c,pitch,flags))return;
        if((flags&4)!=0)
        {
            c.U[26]=c.U[31]==255?(byte)0:c.U[31];OpllKeyOff(c);c.U[0]|=8;c.U[11]=0;StandardEnvelopeTick(c);
            if(!Bit(c.U[0],0)&&c.Index<8){c.W[13]=0;c.W[12]=0;}StandardLfoTick(c);return;
        }
        if((flags&1)!=0&&pitch==c.U[31]){StandardEffects(c);return;}
        StandardSetPitch(c);c.U[31]=B(pitch);
        if((flags&1)==0)
        {
            c.W[18]=c.W[21];c.W[13]=2;if(c.Index<3)ApplyMixer(c);
            if(c.Index>=8){c.W[15]=B((c.W[15]&253)|4);StartOpll(c);}else{Start(c);ApplyNoteFade(c);}
            c.U[0]&=0xf3;c.U[10]=c.U[4];c.U[11]=1;c.U[17]=c.U[12];
        }
        c.U[18]=c.U[13];c.U[19]=c.U[14];c.U[20]=c.U[15];c.U[21]=B(c.U[16]>>1);
        if((flags&1)!=0)StandardEffects(c);else{StandardEnvelopeOutput(c);StandardLfoTick(c);}
    }
    private void StandardGate(Channel c)
    {
        if(Bit(c.U[27],1)||c.Remaining>255||c.U[1]==0)return;
        if(c.Index<8?c.U[1]!=c.W[4]:c.U[1]<c.W[4])return;
        if(c.Index==14){StandardRhythmOff(c);return;}OpllKeyOff(c);c.U[0]|=8;c.U[11]=0;
        if(!Bit(c.U[0],0)&&c.Index<8){c.W[13]=0;c.W[12]=0;}
    }
    private void OpllKeyOff(Channel c){if(c.Index is >=8 and <14)c.W[15]=B((c.W[15]|2)&251);}
    private void StandardEffects(Channel c){StandardEnvelopeTick(c);StandardLfoTick(c);}
    private void StandardEnvelopeTick(Channel c)
    {
        if(!Bit(c.U[0],0))return;int level=c.U[10];
        switch(c.U[11])
        {
            case 1:level=Math.Min(255,level+c.U[5]);if(level==255)c.U[11]=2;break;
            case 2:level=Math.Max(c.U[7],level-c.U[6]);if(level==c.U[7])c.U[11]=3;break;
            case 3:level=Math.Max(0,level-c.U[8]);if(level==0)c.U[11]=0;break;
            default:level=Math.Max(0,level-c.U[9]);break;
        }
        c.U[10]=B(level);StandardEnvelopeOutput(c);
    }
    private void StandardEnvelopeOutput(Channel c)
    {
        if(!Bit(c.U[0],0))return;int volume=(c.Index>=8?~c.W[18]:c.W[18])&15,level=c.U[10];int result=level==255?volume:(volume+1)*level/256;c.W[12]=B(c.Index>=8?(~result)&15:result);
    }
    private void StandardVolume(Channel c,int raw)
    {
        c.U[29]=B(raw);int volume;
        if(Bit(c.U[0],4)){int index=raw<20?raw:0;volume=Math.Min(15,(NativeNotePeriods.FmVolumes[index]+c.U[28])/4);}
        else volume=c.Index>=8?(~raw)&15:raw;
        c.W[21]=c.W[18]=c.W[12]=B(volume);StandardEnvelopeOutput(c);
    }
    private void ApplyMixer(Channel c)
    {
        c.W[13]=B((c.W[13]&252)|((c.U[28]&1)==0?2:0)|((c.U[28]&8)==0?1:0));
    }
    private void StandardSetPitch(Channel c)
    {
        int pitch=c.U[26]&15,octave=(c.U[26]>>4)&7;if(pitch>11)throw new FormatException("Invalid standard note pitch.");
        int raw=c.Index>=8?NativeNotePeriods.OpnaFnum[pitch]|(octave<<11):NativeNotePeriods.SsgBase[pitch];raw=(raw+(short)UWord(c,2))&65535;UWord(c,22,raw);
        if(c.Index<8&&TryTablePitch(c,c.U[26],raw,out int custom)){SetPeriod(c,custom);return;}
        if(UWord(c,2)==0)
        {
            if(c.Index<8){SetPeriod(c,(c.Index<3?NativeNotePeriods.Psg:NativeNotePeriods.Scc)[octave*12+pitch]);return;}
            int block=octave,value=NativeNotePeriods.Opll[pitch];if(pitch<7){if(block==0)value=NativeNotePeriods.OpllLow[pitch];else block--;}
            c.BasePeriod=c.Period=value;c.W[9]=B(block*2|(value>>8));return;
        }
        StandardPitchOutput(c);
    }
    private void SetPeriod(Channel c,int value)
    {
        c.BasePeriod=value;ApplyDetune(c);
    }
    private void StandardPitchOutput(Channel c)
    {
        int raw=UWord(c,22),octave=(c.U[26]>>4)&7;
        if(c.Index<8)
        {
            if(TryTablePitch(c,c.U[26],raw,out int custom)){SetPeriod(c,custom);return;}
            int n=((short)raw>>octave)&4095;
            int value=c.Index<3?(int)(((long)n*58741+32768)>>16):(int)(((long)n*229+128)>>8)-1;
            if(c.Index>=3&&(value<9||value>=4096))value=9;SetPeriod(c,value);return;
        }
        int block=(raw>>11)&7,fnum=raw&2047;while(block>0&&fnum<918){fnum*=2;block--;}
        int result=(int)(((long)fnum*18279+32768)>>16);if(result>=512){if(block<7){block++;result=(result+1)>>1;}else result=511;}
        c.BasePeriod=c.Period=result;c.W[9]=B(block*2|(result>>8));
    }
    private void StandardLfoTick(Channel c)
    {
        if(!Bit(c.U[0],1))return;if(c.U[17]!=0){c.U[17]--;return;}c.U[18]--;if(c.U[18]!=0)return;c.U[18]=c.U[13];
        if(c.U[21]==0){UWord(c,19,-(short)UWord(c,19));c.U[21]=c.U[16];}c.U[21]--;
        int raw=UWord(c,22),delta=(short)UWord(c,19);UWord(c,22,c.Index>=8?AddOpnaPitch(raw,delta):(raw+delta));int independent=(short)(c.Period-c.BasePeriod);StandardPitchOutput(c);c.Period=c.BasePeriod+independent;
    }
    private static int AddOpnaPitch(int raw,int delta)
    {
        int block=(raw>>11)&7,fnum=((raw&2047)+delta)&65535;
        if(delta<0)
        {
            while(block>0&&(fnum>=32768||fnum<618)){fnum=(fnum+618)&65535;block--;}
            if(block==0&&fnum>=32768)fnum=0;
        }
        else
        {
            while(block<7&&fnum>=1237){fnum-=618;block++;}if(block==7&&fnum>=2048)fnum=2047;
        }
        raw=(block<<11)|fnum;if(fnum==1236&&block<7)raw+=2048-618;return raw&65535;
    }
    private void StandardRhythmInit()
    {
        int[] setup={14,32,0x16,32,0x17,80,0x18,192,0x26,5,0x27,5,0x28,1,0x36,15,0x37,255,0x38,255};for(int i=0;i<setup.Length;i+=2){if(setup[i]>=0x30)WriteOpllVolume(setup[i],setup[i+1]);else Opll(setup[i],setup[i+1]);}
    }
    private static int DrumMask(int mask)=>(mask&1)*16|(mask&2)*4|(mask&4)/2|(mask&8)/8|(mask&16)/4;
    private void StandardRhythmNote(Channel c)
    {
        if(c.U[26]>31)throw new FormatException("Invalid standard rhythm mask.");if(Bit(c.U[27],2)){StandardRhythmOff(c);return;}if(Bit(c.U[27],0))return;
        int mask=DrumMask(c.U[26])&c.U[11];if(mask==0)return;c.U[12]=B(c.U[12]&~mask);Opll(14,32|c.U[12]);c.U[12]|=B(mask);OpllNoteDrumFade(mask);Opll(14,32|c.U[12]);
    }
    private void StandardRhythmOff(Channel c)
    {
        int mask=DrumMask(c.U[26]);if(mask==0)return;c.U[12]=B(c.U[12]&~mask);Opll(14,32|c.U[12]);
    }
    private void StandardRhythmVolumes(Channel c,ref int p)
    {
        if(c.Index!=14)throw new FormatException("Rhythm volume requires MSXG.");int total=score.Read(ref p);if(total>63)throw new FormatException("Rhythm total volume exceeds 63.");c.U[4]=B(total);
        for(int i=5;i<=10;i++){int v=score.Read(ref p);if(v>31)throw new FormatException("Rhythm level exceeds 31.");c.U[i]=B(v);}c.U[11]=0;
        int Att(int volume,int bit)
        {
            int n=(total^63)+(volume^31);if(n>=63)return 15;c.U[11]|=B(bit);return Math.Min(15,(n+2)/4);
        }
        WriteOpllVolume(0x36,Att(c.U[5],16));int hh=Att(c.U[8],1),sd=Att(c.U[6],8);WriteOpllVolume(0x37,hh*16|sd);int tom=Att(c.U[9],4),cy=Att(c.U[7],2);WriteOpllVolume(0x38,tom*16|cy);
        int live=c.U[12]&c.U[11];if(live!=c.U[12]){c.U[12]=B(live);Opll(14,32|live);}
    }
}
