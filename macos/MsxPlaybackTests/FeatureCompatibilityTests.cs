using MDPlayer;
using MDPlayer.Driver.MUCOM;
using System.Text.Json;

internal static class FeatureCompatibilityTests
{
    internal static int Run(string folder,string output)
    {
        List<object> rows=new();int failures=0;
        foreach(string muc in Directory.GetFiles(folder,"*.muc").OrderBy(x=>x))
        {
            string name=Path.GetFileNameWithoutExtension(muc);
            byte[] compiled=MucomDotNET.CompileMuc(File.ReadAllBytes(muc),muc,out string error);
            object Render(byte[] bytes,string path)
            {
                if(bytes==null)return new{passed=false,error=error};
                var session=MusicEngine.Load(bytes,path);
                if(session==null)return new{passed=false,error="load rejected"};
                short[] samples=new short[4096];long energy=0;long frames=0;
                for(int i=0;i<1200&&!session.Driver.Stopped;i++)
                {
                    int n=session.RenderSamplesWithMasterVolume(samples,0,samples.Length);
                    frames+=n/2;foreach(short s in samples.Take(n))energy+=Math.Abs((int)s);
                }
                string playbackError=(session.Driver as MucomDotNET)?.PlaybackError??"";
                bool pass=session.Driver.Stopped&&playbackError.Length==0;
                if(!pass)failures++;
                return new{passed=pass,frames,energy,error=playbackError};
            }
            if(compiled==null)failures++;
            var row=new{name,compileError=error,muc=Render(compiled,Path.ChangeExtension(muc,".mub")),mub=Render(File.ReadAllBytes(Path.ChangeExtension(muc,".mub")),Path.ChangeExtension(muc,".mub"))};
            rows.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new{failures,rows},new JsonSerializerOptions{WriteIndented=true}));
        return failures==0?0:1;
    }
}
