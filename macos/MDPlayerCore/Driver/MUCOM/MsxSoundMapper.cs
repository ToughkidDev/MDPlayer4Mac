namespace MDPlayer.Driver.MUCOM;

// Port of MUCOMSX shared/MSXTargets.asm and SSGExtendDriver.asm (2026-10-11).
// Keep integer factors, rounding, preset tie order and repeated register writes.
public sealed class MsxSoundMapper
{
    private readonly Action<int,int> opll, psg, opna;
    private readonly Action<int,int,int> scc;
    private readonly byte[,] fm = new byte[2,256];
    private readonly int[] high=new int[6], flags=new int[6], instruments=Enumerable.Repeat(1,6).ToArray(), pan=Enumerable.Repeat(192,6).ToArray();
    private readonly byte[] ay=new byte[14], ssg=new byte[14], drum=new byte[6];
    private readonly int[] gains={255,255,255};
    private int drumTotal, drumKeys;
    public bool RawMode {get;private set;}
    public int WarningFlags {get;private set;}
    private static readonly int[] carriers={8,8,8,8,12,14,14,15};
    private static readonly int[] drumBits={16,8,2,1,4};
    private static readonly int[] levels={0,0,0,0,0,0,1,1,1,2,3,4,5,8,11,15};
    private static readonly int[] noiseGains={215,152,107,76,54,38,27,19,13,9,7,5,3,2,1,0};
    // YM2413 ROM parameter table from ymfm (BSD-3-Clause); attribution in MucomDriver/README.md.
    private static readonly byte[][] rom={
        new byte[]{0x71,0x61,0x1e,0x17,0xef,0x7f,0,0x17},new byte[]{0x13,0x41,0x1a,0x0d,0xf8,0xf7,0x23,0x13},
        new byte[]{0x13,1,0x99,0,0xf2,0xc4,0x11,0x23},new byte[]{0x31,0x61,0x0e,7,0x98,0x64,0x70,0x27},
        new byte[]{0x22,0x21,0x1e,6,0xbf,0x76,0,0x28},new byte[]{0x31,0x22,0x16,5,0xe0,0x71,0x0f,0x18},
        new byte[]{0x21,0x61,0x1d,7,0x82,0x8f,0x10,7},new byte[]{0x23,0x21,0x2d,0x14,0xff,0x7f,0,7},
        new byte[]{0x41,0x61,0x1b,6,0x64,0x65,0x10,0x17},new byte[]{0x61,0x61,0x0b,0x18,0x85,0xff,0x81,7},
        new byte[]{0x13,1,0x83,0x11,0xfa,0xe4,0x10,4},new byte[]{0x17,0x81,0x23,7,0xf8,0xf8,0x22,0x12},
        new byte[]{0x61,0x50,0x0c,5,0xf2,0xf5,0x29,0x42},new byte[]{1,1,0x54,3,0xc3,0x92,3,2},
        new byte[]{0x41,0x41,0x89,3,0xf1,0xe5,0x11,0x13}
    };
    public MsxSoundMapper(Action<int,int> opll, Action<int,int> psg, Action<int,int> opna, Action<int,int,int> scc)
        => (this.opll,this.psg,this.opna,this.scc)=(opll,psg,opna,scc);
    public void Start(bool native,bool extended)
    {
        if(native)
        {
            StopNative(); PsgWrite(7,0x38);
            int[] setup={0x0e,0x20,0x16,0x20,0x17,0x50,0x18,0xc0,0x26,5,0x27,5,0x28,1,0x36,15,0x37,255,0x38,255};
            for(int i=0;i<setup.Length;i+=2) opll(setup[i],setup[i+1]);
        }
        if(extended)
        {
            scc(5,0,0);scc(3,0,0);
            for(int c=2;c>=0;c--) Upload(c,mucomDotNET.Driver.Music2.MsxPreset(0),255);
            scc(3,0,7);
        }
    }
    public void StopNative()
    {
        opll(14,32);
        for(int c=0;c<9;c++){opll(0x20+c,0);opll(0x30+c,15);}
        psg(7,0xbf);for(int c=0;c<3;c++) psg(8+c,0);
    }
    public void StopScc(){scc(3,0,0);for(int c=0;c<5;c++)scc(2,c,0);}
    private static int Scale(int value,int factor)=>(int)(((long)value*factor+32768)>>16);
    public void WriteNative(int port,int reg,int value)
    {
        if(port is <0 or >1 || reg is <0 or >255 || value is <0 or >255) return;
        if(port==0 && reg==0xbf){RawMode=(value&1)!=0;return;}
        if(RawMode)
        {
            if(port!=0 || reg<0x40) return;
            if(reg<0x80)opll(reg-0x40,value);
            else if(reg>=0xc0)opll(reg-0xc0,value);
            else if(reg<0x8e)psg(reg-0x80,reg==0x87?(value&63)|128:value);
            return;
        }
        fm[port,reg]=(byte)value;
        if(port==0)
        {
            if(reg<14){PsgWrite(reg,value);return;}
            if(reg<32){RhythmWrite(reg,value);return;}
            if(reg==0x28)
            {
                int ch=value&7; if(ch==3 || ch==7){WarningFlags|=8;return;} if(ch>3)ch--;
                flags[ch]=(value&0xf0)!=0?16:0;
                opll(0x20+ch,high[ch]|flags[ch]);return;
            }
        }
        if(reg<0x30){if(reg is not (0x26 or 0x27 or 0x29) && value!=0)WarningFlags|=8;return;}
        int channel=reg&3;if(channel==3){WarningFlags|=8;return;}
        int chn=channel+port*3;
        int Get(int address)=>fm[port,address+channel];
        if((reg&0xfc)==0xa0)
        {
            int packed=Get(0xa0)|(Get(0xa4)<<8), block=(packed>>11)&7, number=packed&2047;
            while(block>0 && number<918 && number!=0){number*=2;block--;}
            number=Scale(number,18279);
            if(number>=512){if(block<7){block++;number=(number+1)/2;}else{number=511;WarningFlags|=4;}}
            high[chn]=(block<<1)|(number>>8);opll(0x10+chn,number&255);opll(0x20+chn,high[chn]|flags[chn]);return;
        }
        if((reg&0xfc)==0xb0)
        {
            byte[] patch=new byte[8];int algorithm=Get(0xb0)&7;
            int mod=algorithm<5?4:0;patch[3]=(byte)((Get(0xb0)>>3)&7);patch[2]=(byte)Math.Min(63,Get(0x40+mod)&127);
            for(int op=0;op<2;op++)
            {
                int offset=op==0?mod:12;
                patch[op]=(byte)(((Get(0x70+offset)&31)==0?32:0)|(Get(0x30+offset)&15));
                patch[4+op]=(byte)(((Get(0x50+offset)&31)>>1)*16+((Get(0x60+offset)&31)>>1));
                patch[6+op]=(byte)Get(0x80+offset);
            }
            int best=int.MaxValue;
            for(int preset=0;preset<rom.Length;preset++)
            {
                int score=0;
                for(int n=0;n<8;n++)score+=Math.Abs((patch[n]&15)-(rom[preset][n]&15))+Math.Abs((patch[n]>>4)-(rom[preset][n]>>4));
                if(score<best){best=score;instruments[chn]=preset+1;}
            }
        }
        else if((reg&0xfc)==0xb4){pan[chn]=value&192;if((value&63)!=0)WarningFlags|=8;if(pan[chn] is 64 or 128)WarningFlags|=2;}
        else if((reg&0xf0)!=0x40){if((reg&0xf0)==0x90 && (value&15)!=0)WarningFlags|=8;return;}
        int mask=carriers[Get(0xb0)&7], tl=127;
        for(int op=0;op<4;op++)if((mask&(1<<op))!=0)tl=Math.Min(tl,Get(0x40+op*4)&127);
        int atten=pan[chn]==0?15:Math.Min(15,tl/4);
        opll(0x30+chn,(instruments[chn]<<4)|atten);
    }
    private void PsgWrite(int reg,int value)
    {
        if(reg>=14)return;ay[reg]=(byte)value;
        if(reg<6 || reg is 11 or 12)
        {
            int address=reg<6?reg&~1:11;
            int period=ay[address]|(ay[address+1]<<8);if(reg<6)period&=4095;
            period=Scale(period,58741);psg(address,period&255);psg(address+1,period>>8);
        }
        else psg(reg,reg==6?Scale(value&31,58741):reg==7?(value&63)|128:reg==13?value&15:value&31);
    }
    private int DrumVolume(int c)
    {
        if((drum[c]&192)==0)return -1;
        int atten=(drumTotal^63)+((drum[c]&31)^31);
        return atten>=63?-1:Math.Min(15,(atten+2)/4);
    }
    private int AudibleDrums()
    {
        int mask=0;for(int c=0;c<5;c++)if(DrumVolume(c)>=0)mask|=drumBits[c];return mask;
    }
    private void RhythmWrite(int reg,int value)
    {
        if(reg==16)
        {
            int mask=0;for(int c=0;c<5;c++)if((value&(1<<c))!=0)mask|=drumBits[c];
            if((value&128)==0){if((value&32)!=0)WarningFlags|=1;mask&=AudibleDrums();}
            if(mask==0)return;
            drumKeys&=~mask;opll(14,32|drumKeys);
            if((value&128)==0){drumKeys|=mask;opll(14,32|drumKeys);}return;
        }
        if(reg==17)drumTotal=value&63;
        else if(reg is >=24 and <30){drum[reg-24]=(byte)value;if((value&192) is 64 or 128)WarningFlags|=2;}
        else{WarningFlags|=8;return;}
        int V(int c) => DrumVolume(c)<0 ? 15 : DrumVolume(c);
        opll(0x36,V(0));opll(0x37,(V(3)<<4)|V(1));opll(0x38,(V(4)<<4)|V(2));
        int keys=drumKeys&AudibleDrums();if(keys!=drumKeys){drumKeys=keys;opll(14,32|keys);}
    }
    public void Upload(int channel,byte[] wave,int gain)
    {
        if(channel is <0 or >2 || wave?.Length!=32 || gain is <0 or >255)throw new FormatException("Invalid SCC+ wave/gain.");
        gains[channel]=gain;
        for(int n=0;n<32;n++)scc(4,channel*32+n,wave[n]);
        SsgVolume(channel);
    }
    public void WriteSsg(int reg,int value)
    {
        if(reg is <0 or >=14)return;
        ssg[reg]=(byte)value;
        if(reg<6)
        {
            opna(reg,value);int address=reg&~1,period=ssg[address]|((ssg[address+1]&15)<<8);
            period=Math.Max(9,((period*229+128)>>8)-1);
            scc(1,address,period&255);scc(1,address+1,period>>8);
        }
        else if(reg==7){WriteSsgMixer();for(int c=2;c>=0;c--)SsgVolume(c);}
        else if(reg is >=8 and <=10){SsgVolume(reg-8);WriteSsgMixer();}
        else opna(reg,value);
    }
    private void WriteSsgMixer()
    {
        int mixer=ssg[7];for(int c=0;c<3;c++)if((ssg[8+c]&16)==0)mixer|=1<<c;opna(7,mixer);
    }
    private void SsgVolume(int channel)
    {
        int authored=ssg[8+channel],gain=gains[channel];
        int volume=(authored&16)!=0?0:(levels[authored&15]*gain+128)>>8;
        if((ssg[7]&(1<<channel))!=0)volume=0;
        scc(2,channel,volume);
        int noise=authored;
        if(gain==0)noise=0;
        else if((authored&16)==0)foreach(int threshold in noiseGains){if(gain>=threshold || noise==0)break;noise--;}
        opna(8+channel,noise);
    }
}
