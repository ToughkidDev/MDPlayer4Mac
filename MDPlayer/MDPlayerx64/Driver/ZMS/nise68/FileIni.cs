namespace MDPlayer.Driver.ZMS.nise68
{
    public class FileIni
    {
        public bool IsOpen = false;
        public string filename = "";
        public int ptr = 0;
        //public byte[] dat = null;
        public bool IsTemp = false;
        public MemoryStream memoryStream = null;
        public UInt32 datetime = ((2024-1980)<<25)|(1<<21)|(1<<16)|(12<<11)|(12<<5)|(12<<0);//2024-1-1 12:12:12
    }
}