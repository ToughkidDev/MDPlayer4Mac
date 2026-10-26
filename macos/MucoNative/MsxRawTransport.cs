namespace MDPlayer.Driver.MUCOM;

// Chip declarations live in the raw marker, not the msPb chip-directory IDs.
internal static class MsxRawTransport
{
    internal static void ValidateFlags(byte flags)
    {
        if(flags is 0 or 1)return;
        if((flags&0xf0)!=16 || (flags&1)==0 || ((flags&8)!=0 && (flags&4)==0))
            throw new FormatException("Unsupported MSX raw transport flags.");
    }

    internal static byte? ReadFlags(ReadOnlySpan<byte> page,int part)
    {
        byte? flags=null;
        // Walk instruction boundaries, including repeat bodies. Never interpret
        // register operands, notes or waveform bytes as a transport declaration.
        for(int at=0;at<page.Length;)
        {
            byte code=page[at];
            if(code==0)break;
            int size;
            if(code<0x80)size=2;
            else if(code<0xf0 || code==0xfd)size=1;
            else if(code==0xf4)
            {
                if(at+1>=page.Length)return flags;
                size=page[at+1] switch {0=>7,1 or 2=>2,3 or 4 or 6=>3,5=>4,_=>0};
                if(page[at+1]==7)
                {
                    if(at+2>=page.Length)return flags;
                    size=part is >=3 and <=5 || page[at+2]==0?3:4;
                }
            }
            else if(code==0xf1)size=part==6 && at+1<page.Length && (page[at+1]&128)==0?8:2;
            else if(code==0xf7)size=part is >=3 and <=5?2:9;
            else if(code==0xfa)size=part is >=3 and <=5?7:3;
            else if(code==0xf8)
            {
                if(at+1>=page.Length)return flags;
                int pan=page[at+1];
                bool automatic=part==6 ? (pan>>4) is >=4 and <=6
                    : part is not (>=3 and <=5) && pan is >=4 and <=6;
                size=automatic?3:2;
            }
            else if(code==0xff)
            {
                if(at+1>=page.Length)return flags;
                byte sub=page[at+1];
                bool ssg=part is >=3 and <=5;
                size=sub switch {0xf0 or 0xf1 or 0xf3 or 0xf4 or 0xf5 or 0xfb=>3,0xf2 or 0xf8=>4,0xf6 or 0xf7 or 0xf9 or 0xfc=>6,0xfa=>8,0xff=>2,_=>0};
                if(ssg && sub==0xf0)
                {
                    if(at+2>=page.Length)return flags;
                    if(page[at+2]>=4)size=4;
                }
                if(ssg && sub==0xf6)
                {
                    if(at+2>=page.Length)return flags;
                    size=page[at+2]==255?4:3;
                }
                if(sub==0xfb && !ssg)
                {
                    if(at+2>=page.Length)return flags;
                    size=page[at+2] switch {1=>23,255=>7,_=>3};
                }
                if(sub==0xf7 && at+6<=page.Length && page[at+2]==0 && page[at+3]==0 && page[at+4]==191)
                {
                    byte next=page[at+5];ValidateFlags(next);
                    if(next!=0)
                    {
                        if(flags==1 && next!=1)flags=19;
                        if(next==1 && flags.GetValueOrDefault()>=16)next=19;
                        flags=(byte)(flags.GetValueOrDefault()|next);
                    }
                }
            }
            else size=code switch {0xf0 or 0xf3 or 0xf8 or 0xf9 or 0xfb=>2,0xf2 or 0xfc=>4,0xf5 or 0xfe=>3,0xf6=>5,_=>0};
            // Older native MML may use additional upstream commands. Keep its
            // route unchanged; generated raw control streams use the set above.
            if(size==0 || at+size>page.Length)break;
            at+=size;
        }
        return flags;
    }
}
