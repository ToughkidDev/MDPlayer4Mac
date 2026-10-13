namespace mucomDotNET.Driver;

// Native raw streams are scheduled by MUCOMSX's sample clock, not an
// idealized OPNA timer. Preserve its per-period integer rounding.
public sealed class MsxRawTimer : OPNATimer
{
    private readonly int renderingFrequency;
    private long elapsed;
    private int threshold=TickSamples(198);
    public static int TickSamples(int timerB)=>(((256-timerB)*203+16)/32)*2;

    public MsxRawTimer(int renderingFreq,int masterClock):base(renderingFreq,masterClock)
        =>renderingFrequency=renderingFreq;

    public override bool WriteReg(byte address,byte value)
    {
        bool result=base.WriteReg(address,value);
        if(address==0x26)threshold=TickSamples(value);
        return result;
    }
    public override void timer()
    {
        // Timer A retains the existing implementation. B follows the Work
        // sample accumulator; acknowledgements and tempo writes retain phase.
        int control=TimerReg;
        TimerReg=control&~2;
        base.timer();
        TimerReg=control;
        if((control&2)==0)return;
        elapsed+=44100;
        long interval=(long)threshold*renderingFrequency;
        if(elapsed>=interval)
        {
            elapsed-=interval;
            StatReg|=(control>>2)&2;
        }
    }
}
