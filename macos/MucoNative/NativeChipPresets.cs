namespace MDPlayer.MucoNative;

// Musical preset data owned by this native implementation. No runtime file or
// toolchain lookup is needed. PSG records retain MUCOM88 SSGLIB parameter order.
internal static class NativeChipPresets
{
    internal static readonly byte[][] Psg={
        new byte[]{8,0,255,255,255,255,0,255,1,0,0,0,0,0,0,0},
        new byte[]{8,0,255,255,255,200,0,10,1,0,0,0,0,0,0,0},
        new byte[]{8,0,255,255,255,200,1,10,1,0,0,0,0,0,0,0},
        new byte[]{8,0,255,255,255,190,0,10,0,0,16,1,25,0,4,0},
        new byte[]{8,0,255,255,255,190,1,10,0,0,16,1,25,0,4,0},
        new byte[]{8,0,255,255,255,170,0,10,1,0,0,0,0,0,0,0},
        new byte[]{8,0,40,70,14,190,0,15,0,0,16,1,24,0,5,0},
        new byte[]{8,0,120,30,255,255,0,10,0,0,16,1,25,0,4,0},
        new byte[]{8,0,255,255,255,225,8,15,1,0,0,0,0,0,0,0},
        new byte[]{1,0,255,255,255,1,255,255,1,0,0,0,0,0,0,0},
        new byte[]{1,0,255,255,255,200,8,255,1,0,0,0,0,0,0,0},
        new byte[]{8,0,255,255,255,220,20,8,0,0,1,1,44,1,255,0},
        new byte[]{8,0,255,255,255,255,0,10,0,0,1,1,112,254,4,0},
        new byte[]{8,0,255,255,255,255,0,10,0,0,1,1,80,0,255,0},
        new byte[]{8,0,120,80,255,255,0,255,0,0,1,1,6,255,1,0},
        new byte[]{8,0,255,255,255,220,0,255,0,0,1,1,184,11,255,0}
    };
    internal static readonly byte[][] Opll={
        new byte[]{113,97,30,23,239,127,0,23},
        new byte[]{19,65,26,13,248,247,35,19},
        new byte[]{19,1,153,0,242,196,17,35},
        new byte[]{49,97,14,7,152,100,112,39},
        new byte[]{34,33,30,6,191,118,0,40},
        new byte[]{49,34,22,5,224,113,15,24},
        new byte[]{33,97,29,7,130,143,16,7},
        new byte[]{35,33,45,20,255,127,0,7},
        new byte[]{65,97,27,6,100,101,16,23},
        new byte[]{97,97,11,24,133,255,129,7},
        new byte[]{19,1,131,17,250,228,16,4},
        new byte[]{23,129,35,7,248,248,34,18},
        new byte[]{97,80,12,5,242,245,41,66},
        new byte[]{1,1,84,3,195,146,3,2},
        new byte[]{65,65,137,3,241,229,17,19}
    };
    internal static int ProjectOpll(ReadOnlySpan<byte> voice)
    {
        int mod=(voice[24]&7)<5?1:0;
        int[] projected={((voice[16+mod]&31)==0?32:0)|(voice[mod]&15),
            ((voice[19]&31)==0?32:0)|(voice[3]&15),Math.Min(voice[4+mod]&127,63),(voice[24]>>3)&7,
            ((voice[8+mod]&31)>>1)*16+((voice[12+mod]&31)>>1),((voice[11]&31)>>1)*16+((voice[15]&31)>>1),voice[20+mod],voice[23]};
        int best=1,minimum=int.MaxValue;
        for(int n=0;n<Opll.Length;n++)
        {
            int score=0;for(int j=0;j<8;j++)score+=Math.Abs((projected[j]&15)-(Opll[n][j]&15))+Math.Abs((projected[j]>>4)-(Opll[n][j]>>4));
            if(score<minimum){minimum=score;best=n+1;}
        }
        return best;
    }
}
