namespace MDPlayer.MucoNative;

internal sealed partial class PackedMusicPlayer
{
    private readonly int[] olLow=new int[6],olHigh=new int[6],olVolume=new int[6];
    private readonly Dictionary<int,Channel> olEnvelopes=new();
    private readonly Dictionary<int,int> olPatches=new();
    private readonly int[] olDrumVolumes=new int[3];
    private bool bassPending;
    private bool HasOpll=>score.Version>=3&&score.UsesOpll;
    private static readonly int[] OpllCompactPitches={348,369,392,414,439,467,498,263,279,293,312,329};
    private void InitializeOpll()
    {
        if(score.Version<3)return;
        foreach(var c in channels.Skip(8))
        {
            c.W[20]=1;c.W[31]=1;c.W[15]=4;c.W[43]=4;
            if(!HasOpll)c.W[0]=0;
        }
        if(!HasOpll)return;Opll(14,32);for(int i=0;i<9;i++)Opll(0x20+i,0);
    }
    private Channel Shadow(Channel c)
    {
        if(!olEnvelopes.TryGetValue(c.Index,out var e))
        {
            e=new Channel(c.Index,c.Pc);e.M=c.M;e.Auxiliary=true;olEnvelopes[c.Index]=e;
        }
        e.Remaining=c.Remaining;e.W[4]=c.W[4];return e;
    }
    private void TickOpll()
    {
        if(!HasOpll)return;
        for(int i=8;i<15;i++)
        {
            var c=channels[i];if(!c.Active)continue;
            if(c.Standard)TickStandard(c);
            else
            {
                Duration(c,B(c.Remaining-1));
                if(c.Remaining==0){if(i<14)c.W[15]|=4;int p=c.Pc;ParseOpll(c,ref p);}
                else if(i<14)HeldOpll(c);
            }
        }
        CommitOpll();
    }
    private void ParseOpll(Channel c,ref int p)
    {
        int budget=4096;
        while(true)
        {
            if(--budget==0)throw new FormatException("OPLL command loop has no duration boundary.");
            int start=p,op=score.Read(ref p);
            if(op==255){EndOpll(c);return;}
            if(op>=0xd0)
            {
                if(op<0xd8){c.W[9]=B(15-2*(op&7));continue;}
                if(op==0xd8)
                {
                    if(c.Index!=14)throw new FormatException("OPLL drum hit outside MSXG.");int mask=score.Read(ref p),length=score.Read(ref p);
                    if(mask>32||length>15)throw new FormatException("Invalid OPLL drum hit.");Duration(c,B((length+1)*c.W[20]));c.Pc=p;if(mask==32){FlushOpllDrums(c);Opll(14,0);}else OpllDrumHit(mask);return;
                }
                OpllControl(c,op,ref p,start);
                if(!c.Active)return;if(c.Standard){ParseStandard(c,ref p);return;}continue;
            }
            Duration(c,B((op%16+1)*c.W[20]));c.Pc=p;
            if(c.Index==14||c.W[31]==64)
            {
                FlushOpllDrums(c);
                int mask=op>>4;if(bassPending){mask|=16;bassPending=false;}OpllDrumHit(mask);return;
            }
            if(c.W[31]!=1)throw new FormatException("Unsupported native OPLL track type.");
            int pitch=op>>4;
            if(pitch>=12){c.W[12]=15;c.W[13]=0;return;}
            SetCompactOpllPitch(c,pitch);c.W[18]=c.W[21];c.W[13]=2;StartOpll(c);return;
        }
    }
    private void FlushOpllDrums(Channel c)
    {
                if(Bit(c.W[15],6))
                {
                    int id=c.Word(34);var drum=score.Drums[id]??throw new FormatException("Undefined OPLL drum preset.");for(int n=0;n<3;n++)Opll(0x16+n,drum[n]);for(int n=0;n<3;n++)Opll(0x26+n,drum[n+3]);c.W[15]&=191;
                }
                if(Bit(c.W[15],5))
                {
                    int att=c.W[21],one=Math.Max(0,att-1);olDrumVolumes[0]=one;olDrumVolumes[1]=B(((one+2+c.W[32])<<4)|one);olDrumVolumes[2]=B((att<<4)|(att+3));
                    for(int n=0;n<3;n++)WriteOpllVolume(0x36+n,olDrumVolumes[n]);c.W[15]&=223;
                }
    }
    private void OpllControl(Channel c,int op,ref int p,int start)
    {
        switch(op)
        {
            case 0xe7:
                int drum=score.Read(ref p);if(drum>15||score.Drums[drum]==null)throw new FormatException("Undefined OPLL drum preset.");c.Word(34,drum);c.W[15]|=64;return;
            case 0xe8:c.W[36]=B(score.Read(ref p));return;
            case 0xe9:
                c.W[20]=B(score.Read(ref p));if(c.W[20] is 0 or >15)throw new FormatException("OPLL duration unit must be 1..15.");return;
            case 0xea:c.W[21]=B((~score.Read(ref p))&15);c.W[15]|=32;return;
            case 0xeb:c.W[15]|=1;return;
            case 0xec:c.W[15]&=254;return;
            case 0xed:bassPending=true;return;
            case 0xee:c.M[5]&=254;c.W[38]=B(score.Read(ref p));c.W[14]|=2;return;
            case 0xef:c.M[5]&=254;c.W[14]&=253;return;
            case 0xf0:c.W[14]&=0xe2;return;
            case 0xf1:int v=score.Read(ref p);c.W[14]|=0x14;c.W[28]=B(v&15);c.W[27]=B(v>>4);return;
            case 0xf2:c.W[26]=B(score.Read(ref p));c.W[14]|=12;return;
            case 0xf3:c.W[14]&=247;return;
            case 0xf8:
                int voice=score.Read(ref p);if(voice==65){OpllExtended(c,score.Read(ref p),ref p);return;}
                c.W[39]=B(voice&240);
                if(c.W[39]==0){if(voice>15||score.Patches[voice]==null)throw new FormatException("Undefined OPLL custom patch.");olPatches[c.Index]=voice;}
                c.W[15]|=128;return;
            case 0xf9:
                int call=score.ReadWord(ref p);score.CodePointer(call);if(c.Returns.Count>=32)throw new FormatException("OPLL phrase call depth exceeds 32.");c.Returns.Push(p);p=call;return;
            case 0xfa:
                if(c.Returns.Count==0)throw new FormatException("OPLL return without a call.");p=c.Returns.Pop();score.CodePointer(p);return;
            case 0xfb:case 0xfc:
                int total=score.Read(ref p),target=score.ReadWord(ref p),counter=op==0xfb?5:6;int count=B(c.W[counter]+1);c.W[counter]=count==total?(byte)0:B(count);if(count!=total){score.CodePointer(target);p=target;}return;
            case 0xfd:
                int jump=score.ReadWord(ref p);score.CodePointer(jump);if(jump<start)c.Loops++;p=jump;return;
            case 0xfe:
                int type=score.Read(ref p);if(type==0){c.W[31]=c.W[42]=c.W[43]=0;return;}if(type!=1&&(type!=64||c.Index!=14))throw new FormatException("OPLL type must be 1, or 64 on MSXG.");
                if(type==64)SetupOpllRhythm();int old=c.W[31];c.W[31]=B(type);
                if(type==1){if(old==1){c.W[14]=c.W[42];c.W[15]=c.W[43];}}
                else if(old==1){c.W[42]=c.W[14];c.W[43]=c.W[15];c.W[14]=c.W[15]=0;}return;
            default:throw new FormatException($"PSG/unknown opcode {op:X2} in OPLL score.");
        }
    }
    private void OpllExtended(Channel c,int sub,ref int p)
    {
        if(score.Version<4)throw new FormatException("OPLL software effects require PN4 or later.");
        if(sub is >=8 and <=22){StandardCommand(c,sub,ref p);return;}
        if(sub==51&&score.Version==6){int mode=score.Read(ref p);if(mode>2||c.Index==14&&mode==1)throw new FormatException("Invalid OPLL end policy.");c.M[26]=B((c.M[26]&249)|(mode<<1));return;}
        if(sub==52&&score.Version==6){int q=score.Read(ref p),mode=score.Read(ref p);if(c.Index==14||c.Standard||mode>1)throw new FormatException("Invalid OPLL gate mode.");c.W[36]=B(q);c.M[26]=B((c.M[26]&254)|mode);return;}
        if(sub==50&&score.Version==6){int bias=score.Read(ref p);if(c.Index!=14||bias>15)throw new FormatException("Invalid OPLL drum bias.");c.W[32]=B(bias);return;}
        if(sub==27&&score.Version==6){SelectOpllPitchTable(c,ref p);return;}
        if(sub==32&&score.Version==6){ExtendedProgramControl(c,sub,ref p);return;}
        if(c.Index==14)throw new FormatException("OPLL melody extension on rhythm channel.");
        if(sub is 3 or 4){Extended(c,sub,ref p);return;}
        if(sub is <0 or >7)throw new FormatException("Unsupported PSG/SCC extension on OPLL.");
        var e=Shadow(c);if(sub==5){e.M[29]=0;return;}Extended(e,sub,ref p);
    }
    private void SelectOpllPitchTable(Channel c,ref int p)
    {
        int id=score.Read(ref p),start=score.Read(ref p);
        if(c.Index==14||c.Standard)throw new FormatException("?KT requires compact OPLL melody.");
        if(id==255){c.W[22]=c.W[23]=0;return;}
        if(!score.PitchTables.TryGetValue(id,out var values)||start+12>values.Length||values.Skip(start).Take(12).Any(v=>v is 0 or >1023))throw new FormatException("Invalid OPLL pitch-table window.");
        c.W[22]=B(id+1);c.W[23]=B(start);
    }
    private void SetCompactOpllPitch(Channel c,int pitch)
    {
        int value=OpllCompactPitches[pitch]+1;bool upper=pitch>=7;
        if(c.W[22]!=0){value=score.PitchTables[c.W[22]-1][c.W[23]+pitch];upper=value>=512;if(upper)value>>=1;}
        c.W[9]=B((c.W[9]&127)|(upper?128:0));c.BasePeriod=value;
    }
    private void StartOpll(Channel c)
    {
        c.Period=c.BasePeriod+(Bit(c.W[14],1)?c.W[38]:0);c.W[14]&=239;c.W[29]=c.W[30]=0;
        int level=c.W[18];if(Bit(c.W[14],7))level=Math.Max(0,level-c.W[21]);c.W[12]=B(level);
        ApplyOpllNoteFade(c);
        if(c.M[29]!=0)
        {
            var e=Shadow(c);e.W[18]=B((~c.W[18])&15);e.W[31]=e.W[32]=0;StartSelectedEnvelope(e);c.W[12]=B((~e.W[12])&15);
        }
        if(c.W[36]!=0)
        {
            if(Bit(c.M[26],0))c.W[37]=c.W[40]=c.W[41]=0;
            int product=(c.W[36]+1)*c.W[4];c.W[40]=B(product);c.W[41]=B(c.W[41]+(product>>8)+(B(product)>=128?1:0));c.W[37]=0;c.W[15]|=8;
        }
        else c.W[15]&=247;
    }
    private void HeldOpll(Channel c)
    {
        if(Bit(c.W[14],2))Vibrato(c);
        if(Bit(c.W[15],3)){c.W[37]++;if(c.W[37]==c.W[41]){c.W[15]=B((c.W[15]|2)&247);c.W[37]=c.W[40]=c.W[41]=0;}}
        if(c.M[29]!=0)
        {
            var e=Shadow(c);int level=c.M[29]==3?QuarthEnvelope(e):IntegerEnvelope(e);e.W[12]=B(level);c.W[12]=B((~level)&15);
        }
    }
    private void EndOpll(Channel c)
    {
        int end=(c.M[26]>>1)&3;
        if(end!=0)
        {
            c.W[0]=c.W[1]=c.W[5]=c.W[6]=0;
            if(end==1){c.W[9]=c.W[10]=c.W[11]=c.W[12]=c.W[13]=c.W[14]=0;c.W[15]=4;c.M[5]&=254;}
            else c.W[15]&=121;
            return;
        }

        c.W[0]=c.W[1]=c.W[5]=c.W[6]=c.W[9]=c.W[10]=c.W[11]=c.W[13]=c.W[14]=0;c.W[12]=15;c.W[39]=240;c.W[15]=130;
        if(c.Index==14&&!Bit(c.M[26],1))Opll(14,32);
    }
    private void SetupOpllRhythm()
    {
        int[] setup={14,32,0x16,32,0x17,80,0x18,192,0x26,5,0x27,5,0x28,1,0x36,0,0x37,0,0x38,0};
        for(int i=0;i<setup.Length;i+=2){if(setup[i]>=0x30)WriteOpllVolume(setup[i],setup[i+1]);else Opll(setup[i],setup[i+1]);}
    }
    private void OpllDrumHit(int mask)
    {
        if(mask>31)throw new FormatException("OPLL rhythm mask exceeds 31.");Opll(14,32);OpllNoteDrumFade(mask);Opll(14,32|mask);
    }
    private void CommitOpll()
    {
        var changedLow=new bool[6];var changedVolume=new bool[6];
        for(int i=0;i<6;i++)
        {
            var c=channels[8+i];int low=c.W[10],block=c.W[9];
            if(Bit(c.M[5],0)&&c.Active)
            {
                int original=block&15;if((block&128)!=0&&original!=15)original+=2;
                int number=low|((original&1)<<8),octave=original>>1;number+=(short)(c.M[6]|(c.M[14]<<8));if(number<0)number=0;
                while(number>=512&&octave<7){number>>=1;octave++;}if(number>=512){number=511;octave=7;}low=number&255;block=octave*2|(number>>8);
            }
            changedLow[i]=olLow[i]!=low;olLow[i]=low;
            int high=(olHigh[i]&240)|block;if((block&128)!=0&&(block&15)!=15)high=B(high+2);if(Bit(c.W[15],0))high|=32;olHigh[i]=high;
            changedVolume[i]=(olVolume[i]&15)!=c.W[12];olVolume[i]=(olVolume[i]&240)|c.W[12];
            if(Bit(c.W[15],7)){c.W[15]&=127;changedVolume[i]=true;olVolume[i]=(olVolume[i]&15)|c.W[39];}
        }
        for(int i=0;i<6;i++)if(changedLow[i])Opll(0x10+i,olLow[i]);
        for(int i=0;i<6;i++)if(Bit(channels[8+i].W[15],2))Opll(0x20+i,olHigh[i]&239);
        for(int i=0;i<6;i++)if(changedVolume[i])
        {
            WriteOpllVolume(0x30+i,olVolume[i]);
            // A rest before an explicit instrument must never read patch 0
            // from machine vectors. The shared user patch changes in channel
            // order only after an actual custom-instrument selection.
            if((olVolume[i]&240)==0&&olPatches.TryGetValue(8+i,out int patch))for(int n=0;n<8;n++)Opll(n,score.Patches[patch][n]);
        }
        for(int i=0;i<6;i++)
        {
            var c=channels[8+i];if(Bit(c.W[15],2)){c.W[15]&=251;Opll(0x20+i,olHigh[i]|16);}
            else if(Bit(c.W[15],1)){c.W[15]&=253;Opll(0x20+i,olHigh[i]&239);}
        }
    }
}
