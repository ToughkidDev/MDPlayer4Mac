namespace MDPlayer.MucoNative;

internal sealed partial class PackedMusicPlayer
{
    private int fadeProfile,fadeInterval,fadeStep,fadeTarget,fadeCurrent,fadeCounter;
    private int[] pendingFade;
    private readonly int[] fadeOpllRaw=new int[9],fadeOpllPhysical=new int[9];
    private readonly bool[] fadeOpllWritten=new bool[9];
    private void QueueFade(int profile,int interval,int step,int target)
    {
        if(profile==255?interval!=0||step!=0||target!=0:profile>1||step is <1 or >15||target>15)throw new FormatException("Invalid global fade configuration.");pendingFade=new[]{profile,interval,step,target};
    }
    private void TickFade()
    {
        int previous=fadeCurrent,profile=fadeProfile;
        if(pendingFade!=null)
        {
            var q=pendingFade;pendingFade=null;
            if(q[0]==255)fadeProfile=fadeInterval=fadeStep=fadeTarget=fadeCurrent=fadeCounter=0;
            else{fadeProfile=q[0];fadeInterval=q[1];fadeStep=q[2];fadeTarget=q[3];fadeCounter=fadeInterval;}
        }
        if(fadeCurrent!=fadeTarget&&(fadeInterval==0||--fadeCounter==0))
        {
            fadeCounter=fadeInterval;fadeCurrent=fadeInterval==0?fadeTarget:fadeCurrent<fadeTarget?Math.Min(fadeTarget,fadeCurrent+fadeStep):Math.Max(fadeTarget,fadeCurrent-fadeStep);
        }
        if(previous!=fadeCurrent||profile!=fadeProfile)
        {
            for(int i=0;i<5;i++)sccCache[i*3+2]=255;
            if(fadeProfile==0)for(int i=0;i<9;i++)if(fadeOpllWritten[i])EmitOpllFaded(0x30+i,fadeOpllRaw[i]);
        }
    }
    private int ScaleLevel(int raw,bool hardware)=>hardware&&(raw&16)!=0?raw:(raw&240)|Math.Max(0,(raw&15)-fadeCurrent);
    private int ScaleOpll(int raw,bool dual)=>(dual?Math.Min(15,(raw>>4)+fadeCurrent)<<4:raw&240)|Math.Min(15,(raw&15)+fadeCurrent);
    private int FadeLevel(int raw,bool hardware)=>fadeProfile==0?ScaleLevel(raw,hardware):raw;
    private bool ShouldWriteShape()=>fadeProfile!=1||fadeTarget==0&&fadeCurrent==0;
    private void ApplyNoteFade(Channel c){if(fadeProfile==1){c.W[18]=B(ScaleLevel(c.W[18],c.Index<3));c.W[12]=B(ScaleLevel(c.W[12],c.Index<3));}}
    private void ApplyProgramFade(Channel c){if(fadeProfile==1&&fadeCurrent!=0&&(c.Index>=3||(c.W[12]&16)==0))c.W[12]=0;}
    private void ApplyOpllNoteFade(Channel c){if(fadeProfile==1){c.W[18]=B(ScaleOpll(c.W[18],false));c.W[12]=B(ScaleOpll(c.W[12],false));}}
    private void WriteOpllVolume(int address,int value)
    {
        int index=address-0x30;if(index is <0 or >8)throw new FormatException("Invalid OPLL volume register.");fadeOpllRaw[index]=value&255;fadeOpllWritten[index]=true;EmitOpllFaded(address,value);
    }
    private void EmitOpllFaded(int address,int value){int v=fadeProfile==0?ScaleOpll(value,address>=0x37):value;fadeOpllPhysical[address-0x30]=v;Opll(address,v);}
    private void OpllNoteDrumFade(int mask)
    {
        if(fadeProfile!=1)return;
        int[] hi={0,1,4},lo={16,8,2};
        for(int i=0;i<3;i++)
        {
            int index=6+i,raw=fadeOpllRaw[index],v=fadeOpllPhysical[index],next=v;
            if((mask&lo[i])!=0)next=(next&240)|Math.Min(15,(raw&15)+fadeCurrent);
            if((mask&hi[i])!=0)next=(next&15)|(Math.Min(15,(raw>>4)+fadeCurrent)<<4);
            if((mask&(hi[i]|lo[i]))!=0){fadeOpllPhysical[index]=next;Opll(0x36+i,next);}
        }
    }
}
