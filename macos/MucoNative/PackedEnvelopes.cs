namespace MDPlayer.MucoNative;

internal sealed partial class PackedMusicPlayer
{
    private void Extended(Channel c,int sub,ref int p)
    {
        if(score.Version<4&&sub is not(23 or 24))throw new FormatException("Musical extension requires PN4 or later.");
        switch(sub)
        {
            case 0:
                int a=score.Read(ref p),b=score.Read(ref p),d=score.Read(ref p);
                c.M[16]=B(a);c.W[34]=B(a&7);c.W[33]=B((a>>4)&7);c.M[17]=B(b&15);c.W[35]=B(b>>4);c.M[1]=B(d);c.M[31]=B(d>>4);c.M[29]=3;c.W[15]=B((c.W[15]&128)|1|(Bit(c.M[16],3)?16:0)|(Bit(c.M[16],7)?2:0));return;
            case 1:int attack=score.Read(ref p);c.M[15]=B(attack);c.M[30]=B(attack>>4);return;
            case 2:SetTarget(c,score.Read(ref p));return;
            case 3:
                int detune=score.ReadWord(ref p);c.M[6]=B(detune);c.M[14]=B(detune>>8);c.M[5]=B((c.M[5]&254)|(detune!=0?1:0));c.W[14]&=253;return;
            case 4:c.M[5]&=254;c.W[14]&=253;return;
            case 5:c.W[15]&=254;c.M[29]=0;return;
            case 6:SetEnvelope(c,score.Read(ref p),score.Read(ref p),2);return;
            case 7:int softAttack=score.Read(ref p);c.M[9]=B(softAttack);c.M[19]=B(softAttack>>4);c.M[21]=B(softAttack&15);c.M[5]|=2;c.M[18]=B((c.M[18]&254)|(c.M[19]!=0?1:0));return;
            case >=8 and <=22:StandardCommand(c,sub,ref p);return;
            case 23:
                if(c.Index is <3 or >7)throw new FormatException("SCC wave policy used outside SCC.");int policy=score.Read(ref p);if(policy>2||policy!=0&&waveGateSample>=0)throw new FormatException("Invalid SCC wave-sharing policy.");wavePolicy=policy;return;
            case 24:c.M[11]=B(score.Read(ref p));c.M[5]|=2;return;
            case 25:
                int vib=score.Read(ref p);c.W[28]=B(vib&15);c.W[27]=B(vib>>4);if(!Bit(c.M[25],1))c.M[25]&=251;c.M[25]|=2;c.W[14]|=4;return;
            case 26:
                c.M[30]=B(score.Read(ref p));c.M[31]=B(score.Read(ref p));c.M[29]=4;c.M[5]|=2;c.W[15]=B((c.W[15]&128)|1);return;
            default:
                if(score.Version<6)throw new FormatException("PN6 effect used by an older object.");ExtendedProgramControl(c,sub,ref p);return;
        }
    }
    private void SetEnvelope(Channel c,int a,int b,int mode)
    {
        c.M[29]=B(mode);if(mode!=0)c.M[5]|=2;
        c.W[15]&=128;int high=a>>4;if(high<8)c.W[15]|=4;else c.W[15]|=2;c.W[33]=B((high&7)+1);
        c.W[34]=B(a&15);c.W[35]=B(b>>4);c.W[36]=B(b&15);
        if((mode&31)==2)
        {
            if(c.W[34]>=8)c.W[15]|=16;c.W[34]=B((c.W[34]&7)+1);c.W[15]|=1;return;
        }
        if(mode==1)
        {
            int interval=c.W[34];if(interval==0)throw new FormatException("Fine envelope interval cannot be zero.");c.M[17]=B(c.W[35]-c.W[36]);
            c.W[35]=FineRate(c.W[35],interval);c.W[36]=FineRate(c.W[36],interval);c.W[15]|=64;return;
        }
        c.W[15]|=1;
    }
    private static byte FineRate(int n,int interval)
    {
        int d=0,e=0,a=n;
        while(true){int sub=a-interval;if(sub<0){a=interval-a;break;}d++;a=sub;if(a==0)return B((d<<4)&0xf0);}
        a=(a<<4)&0xf0;
        while(a>=interval){e++;a-=interval;if(a==0)break;}
        if(e!=0)e=(~e)&15;return B(((d<<4)&0xf0)|e);
    }
    private void OnDuration(Channel c)
    {
        if(c.M[29]==4)c.M[17]=B(c.W[4]-c.M[30]);
        else if(c.M[29] is 3 or 5)c.W[36]=B(c.W[4]*c.M[17]/16);
    }
    private void OnNoteStart(Channel c)
    {
        ArmExtendedOnset(c);
        if(c.M[11]!=0){int n=B(c.W[18]-c.M[11]);c.W[18]=B((n&128)!=0?0:n);}
        c.M[8]=c.M[10]=0;c.M[25]&=254;if(Bit(c.M[5],3))c.W[15]|=128;
        c.M[28]&=223;if(!Bit(c.M[25],3)&&Bit(c.M[28],4))c.M[28]|=64;
    }
    private void StartSelectedEnvelope(Channel c)
    {
        if(c.M[29]==3)
        {
            int flags=(c.W[15]&128)|1;if(Bit(c.M[16],3))flags|=16;int e=c.W[18];if(Bit(c.M[16],7)){flags|=2;e=c.M[15]&15;}c.W[15]=B(flags);c.W[12]=B(e);OnDuration(c);return;
        }
        if(c.M[29]==5){StartTableEnvelope(c);return;}
        if(c.M[29]==0&&!Bit(c.M[5],1))return;
        c.W[15]=B((c.W[15]&0xd7)|4);c.M[18]=B((c.M[18]&0x15)|2);c.M[16]=0;c.W[12]=c.W[18];
        if(!Bit(c.M[29],7)&&Bit(c.M[18],0)){c.M[18]&=249;c.W[15]&=251;return;}
        if(Bit(c.W[15],1)){c.W[12]=Bit(c.M[29],7)?c.M[9]:c.M[21];c.W[15]&=251;}
    }
    private void StartLiteral(Channel c)
    {
        if(score.Version>=6&&Bit(c.M[5],2)){StartRawLiteral(c);return;}
        Start(c);
    }
    private void HeldExtensions(Channel c)
    {
        c.PreEnvelopeFlags=c.W[15];
        if(Bit(c.M[28],4))Sweep(c);
        if(Bit(c.M[5],4))TickPitchProgram(c);
        switch(c.M[29])
        {
            case 3:if(c.Index>=3||!Bit(c.W[13],2))c.W[12]=B(QuarthEnvelope(c));break;
            case 4:
                if(c.W[4]>=c.M[17]||c.W[4]<c.M[31])c.W[12]=B(Math.Max(c.M[0],(sbyte)B(c.W[12]-1)));break;
            case 5:TickTableEnvelope(c);break;
            default:if((c.M[29]&31)==2&&Bit(c.W[15],0)&&(c.Index>=3||!Bit(c.W[13],2)))c.W[12]=B(IntegerEnvelope(c));break;
        }
        if(Bit(c.W[15],6)&&(c.Index>=3||!Bit(c.W[13],2)))c.W[12]=B(FineEnvelope(c));
        if(Bit(c.M[28],0)||Bit(c.M[28],1))TickWaveProgram(c);
        if(Bit(c.M[5],3))
        {
            int previous=c.M[10];Morph(c);if(previous!=c.M[10]||Bit(c.M[25],4))c.W[15]|=128;
        }
    }
    private void LegacyEnvelopeTick(Channel c)
    {
        if(c.Index<3&&Bit(c.W[13],2))return;
        int e=c.W[12];c.W[31]++;int tick=c.W[31];
        if(Bit(c.W[15],4)){if(c.Remaining<=255&&c.W[4]<c.W[36]){int n=B(e-1);if((n&128)==0)e=n;}}
        else if(Bit(c.W[15],3))
        {
            if(tick==c.W[34]){int n=B(e-1);if((n&128)==0){e=n;c.W[32]++;c.W[31]=0;if(c.W[32]==c.W[35])c.W[15]|=16;}}
        }
        else if(Bit(c.W[15],2))
        {
            int n=B(e-1);if((n&128)==0)e=n;if(tick>=c.W[33]){c.W[31]=0;c.W[15]|=8;}
        }
        else{e=B(e+1);if(e>=c.W[18]){c.W[15]|=4;e=c.W[18];c.W[31]=0;}}
        c.W[12]=B(Math.Max(c.M[0],e));
    }
    private int IntegerEnvelope(Channel c)
    {
        int e=c.W[12];c.W[31]++;int tick=c.W[31];bool strict=Bit(c.M[29],7);
        if((c.M[29]&96)==0&&c.Remaining<=255&&c.W[36]>=c.W[4])c.W[15]|=32;
        if(Bit(c.W[15],5))
        {
            if(strict?c.W[4]>=c.W[36]:c.W[36]<c.W[4])return e;
            int n=B(e-1);if((n&128)==0&&n>=c.M[0])return n;return strict?e:c.M[0];
        }
        if(!Bit(c.W[15],3))
        {
            if(Bit(c.W[15],2))
            {
                int n=B(e-1);if((n&128)==0&&n>=c.M[0])e=n;else if(!strict)e=c.M[0];
                if(tick>=c.W[33]){c.W[31]=0;c.W[15]|=8;}return e;
            }
            if(!Bit(c.M[18],1)&&!Bit(c.M[18],2))
            {
                int n=e-c.M[19];e=n<0||n<c.M[0]?c.M[0]:n;if(tick==c.M[21])c.M[18]|=4;return e;
            }
            int target=Bit(c.M[18],1)?c.W[18]:B(c.W[18]-c.M[20]);e=B(e+1);
            if(e>=target){c.W[15]|=4;e=target;c.W[31]=0;}return e;
        }
        if(!Bit(c.W[15],4))
        {
            if(Bit(c.M[29],6)&&c.W[36]>=c.W[4])c.W[15]|=32;
            if(tick!=c.W[34])return e;int n=B(e-1);
            if((n&128)!=0||n<c.M[0])return strict?e:c.M[0];e=n;c.W[32]++;c.W[31]=0;if(c.W[32]==c.W[35])c.W[15]|=32;return e;
        }
        int level=e-c.W[34];e=level<0||level<c.M[0]?c.M[0]:level;if(strict&&(e&128)!=0)e=c.M[0];if(tick==c.W[35])c.W[15]|=32;return e;
    }
    private int FineEnvelope(Channel c)
    {
        int e=c.W[12];c.W[31]++;int tick=c.W[31];
        if(!Bit(c.W[15],3))
        {
            if(Bit(c.W[15],2)){int n=B(e-1);if((n&128)==0&&n>=c.M[0])e=n;else if(!Bit(c.M[29],7))e=c.M[0];if(tick>=c.W[33]){c.W[31]=0;c.W[15]|=8;}return e;}
            e=B(e+1);if(e>=c.W[18]){c.W[15]|=4;e=c.W[18];c.W[31]=0;}return e;
        }
        if(Bit(c.M[18],3))return e;
        if(!Bit(c.W[15],5))
        {
            int fraction=c.M[16];if(!Bit(c.M[18],5))
            {
                c.M[18]|=32;int target=e-c.M[17];c.W[32]=B(target<0||target<c.M[0]?c.M[0]:target);fraction=Rol4(e);
            }
            int n=fraction-c.W[35];
            if(n<0||Rol4(n)%16<c.M[0]){c.M[16]=B(c.M[0]<<4);e=c.M[0];c.M[18]|=16;}
            else
            {
                c.M[16]=B(n);int rounded=n;if((n&8)!=0)rounded=B(n+16);e=Rol4(rounded)&15;if(e==c.M[0])c.M[18]|=16;
            }
            if(tick==c.W[34]){c.W[15]|=32;c.M[18]&=223;c.W[31]=0;}return e;
        }
        int accum=c.M[16];if(!Bit(c.M[18],5))
        {
            c.M[18]|=32;if(Bit(c.M[18],4)&&e>=c.W[32]){c.M[18]|=8;c.W[15]&=223;c.M[18]&=223;c.W[31]=0;return e;}accum=Rol4(e);
        }
        int sum=accum+c.W[36];c.M[16]=B(sum);if(sum>255){c.M[16]=240;e=15;}
        else
        {
            int rounded=sum;if((sum&8)!=0)rounded+=16;if(rounded>255){c.M[16]=240;e=15;}else e=Rol4(rounded)&15;
        }
        if(Bit(c.M[18],4)&&e>=c.W[32]||tick==c.W[34]){c.W[15]&=223;c.M[18]&=223;c.W[31]=0;}return e;
    }
    private int QuarthEnvelope(Channel c)
    {
        int e=c.W[12];c.W[31]++;int tick=c.W[31];
        if(c.M[17]!=0&&c.W[36]>=c.W[4])return QuarthRelease(c,e);
        if(!Bit(c.W[15],3))
        {
            if(Bit(c.W[15],1))
            {
                c.M[30]--;if(c.M[30]!=0)return e;c.M[30]=B(c.M[15]>>4);e=B(e+1);if(e>=c.W[18]){c.W[15]&=253;e=c.W[18];c.W[31]=0;}return e;
            }
            e=ClampSigned(c,B(e-1));if(c.W[33]>=tick)return e;c.W[31]=1;tick=1;c.W[15]|=8;
        }
        if(Bit(c.W[15],5))return e;
        int rate=c.W[34];if(rate==0){c.W[15]|=32;return e;}
        if(!Bit(c.W[15],4)&&tick!=rate)return e;
        e=ClampSigned(c,B(e-(Bit(c.W[15],4)?rate:1)));c.W[31]=0;c.W[32]++;if(c.W[32]==c.W[35])c.W[15]|=32;return e;
    }
    private int QuarthRelease(Channel c,int e)
    {
        int interval=c.M[1]>>4;if(interval==0)return c.M[1]&15;c.M[31]--;if(c.M[31]!=0)return e;c.M[31]=B(interval);
        int n=e-(c.M[1]&15);return n<0?0:Math.Max(c.M[0],n);
    }
    private static int ClampSigned(Channel c,int n)=>n>=128||n<c.M[0]?c.M[0]:n;
    private void PhaseVibratoTick(Channel c)
    {
        if(c.W[29]==0){c.W[29]++;c.W[14]&=239;}c.W[30]++;
        if(!Bit(c.W[14],4)&&Bit(c.W[14],3)){if(c.W[30]<c.W[26])return;c.W[14]|=16;c.W[30]=0;return;}
        if(c.W[30]<c.W[27])return;c.W[30]=0;ApplyDetune(c);c.M[25]^=4;c.Period+=Bit(c.M[25],2)?-c.W[28]:c.W[28];
    }
    private void Sweep(Channel c)
    {
        if(Bit(c.M[5],5)){WordSweep(c);return;}if(Bit(c.M[25],3)){ByteSweep(c);return;}if(!Bit(c.M[28],6))return;
        bool up=Bit(c.M[28],7);int step=c.M[2];
        if(!Bit(c.M[28],5))
        {
            int value=c.BasePeriod;if(Bit(c.M[5],0))value=(value+(short)(c.M[6]|(c.M[14]<<8)))&65535;bool carry=false;
            for(int i=0;i<c.M[4];i++)
            {
                if(up){int n=value-step-(carry?1:0);value=n&65535;carry=n<0;if(carry){n=value+step;value=n&65535;carry=n>65535;}}
                else{int n=value+step;value=n&65535;carry=n>65535;if(carry){n=value-step-1;value=n&65535;carry=n<0;}}
            }
            c.Period=value;c.M[28]|=32;return;
        }
        if(up)c.Period+=step;else c.Period=Math.Max(1,c.Period-step);
        c.M[4]--;if(c.M[4]==0){c.M[28]&=191;c.M[4]=c.M[3];int value=c.BasePeriod;if(Bit(c.M[5],0))value+=(short)(c.M[6]|(c.M[14]<<8));c.Period=value;}
    }
    private void Morph(Channel c)
    {
        if(Bit(c.M[18],7))return;
        if(!Bit(c.M[25],0)){c.M[8]=16;c.M[18]&=191;c.M[25]|=1;}
        int value=c.M[8]+c.M[7];if(value>255)c.M[18]|=64;c.M[8]=B(value);int phase=(c.M[8]&240)>>4;
        if(Bit(c.M[18],6)){if(Bit(c.M[10],4)){c.M[18]&=191;phase&=15;}else phase+=16;}c.M[10]=B(phase);
    }
}
