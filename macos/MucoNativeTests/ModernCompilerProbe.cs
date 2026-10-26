using System.Text;
using System.Text.Json;
using MDPlayer.MucoNative;

internal static class ModernCompilerProbe
{
    internal static void Run(string manifest,string output)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(manifest));Directory.CreateDirectory(output);var results=new List<object>();
        foreach(var item in doc.RootElement.EnumerateArray())
        {
            string id=item.GetProperty("id").GetString(),path=item.GetProperty("source").GetString();
            try
            {
                Stream Asset(string name) {string p=Path.Combine(Path.GetDirectoryName(path),name.Replace('\\',Path.DirectorySeparatorChar));return File.Exists(p)?File.OpenRead(p):null;}
                byte[] voice=item.TryGetProperty("voice",out var bank)&&bank.ValueKind==JsonValueKind.String?File.ReadAllBytes(bank.GetString()):null;
                var compiled=MsxMusicalCompiler.Parse(Encoding.Latin1.GetString(File.ReadAllBytes(path)),Asset,voice)??throw new Exception("No modern source detected");
                File.WriteAllBytes(Path.Combine(output,id+".obj"),compiled.Object);File.WriteAllText(Path.Combine(output,id+".legacy"),compiled.LegacySource);
                byte[] mub=PackedMubContainer.Write(compiled.Object,compiled.Title,compiled.Timer);
                File.WriteAllBytes(Path.Combine(output,id+".mub"),mub);
                _=MDPlayer.Driver.MUCOM.MsxMubProfile.Parse(mub);
                string publicSource=Encoding.Latin1.GetString(File.ReadAllBytes(path));
                if(voice!=null)publicSource="#voice __TEST_VOICE.DAT\n"+publicSource;
                Stream PublicAsset(string name)=>name=="__TEST_VOICE.DAT"?new MemoryStream(voice,false):Asset(name) ?? (name.Equals("voice.dat",StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(AppContext.BaseDirectory,"../../../../EngineSmokeTest/testdata/msx/voice.dat"))?File.OpenRead(Path.Combine(AppContext.BaseDirectory,"../../../../EngineSmokeTest/testdata/msx/voice.dat")):null);
                byte[] fromPublic=NativeMucCompiler.Compile(Encoding.Latin1.GetBytes(publicSource),PublicAsset,path);
                _=MDPlayer.Driver.MUCOM.MsxMubProfile.Parse(fromPublic);
                File.WriteAllBytes(Path.Combine(output,id+".public.mub"),fromPublic);
                results.Add(new{id,success=true,compiled.Title,compiled.Timer,size=compiled.Object.Length,profile=Encoding.ASCII.GetString(compiled.Object,0,3)});
            }
            catch(Exception e){results.Add(new{id,success=false,error=e.Message,kind=e.GetType().Name});}
        }
        File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Attempted "+results.Count+" modern MUC compilations");
    }
}
