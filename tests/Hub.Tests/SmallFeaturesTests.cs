using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using Hub.Core;

static class SmallFeaturesTests
{
    public static void Register(Action<string,Action> test,Action<string,Func<Task>> asyncTest,string scratch,Catalog catalog)
    {
        static void Check(bool value){if(!value)throw new Exception("Small feature regression");}
        test("Native playtime imports seconds or RPCS3 milliseconds by exact serial and rejects hostile values",()=>{
            Check(PlaytimeService.ParseNative("pcsx2","SLUS-20014","SLUS-20014 3600 1700000000\nSLUS-99999 9999 0")==TimeSpan.FromHours(1));
            Check(PlaytimeService.ParseNative("duckstation","SLUS-01323","SLUS-01323 120 1700000000")==TimeSpan.FromMinutes(2));
            Check(PlaytimeService.ParseNative("rpcs3","BLES00039","[Other]\nBLES00039=999\n[Playtime]\nBLES00039=90000\nBLUS30187=99999999")==TimeSpan.FromSeconds(90));
            foreach(var value in new[]{"-1","9223372036854775807","NaN"})Check(PlaytimeService.ParseNative("pcsx2","SLUS-20014","SLUS-20014 "+value+" 0")==TimeSpan.Zero);
            Check(PlaytimeService.ParseNative("pcsx2","SLUS-20014",new string('x',1024*1024+1))==TimeSpan.Zero);
        });
        test("Portable playtime deltas are idempotent, edition-specific and survive a new service instance",()=>{
            var paths=new HubPaths(Path.Combine(scratch,"playtime-deltas"));var service=new PlaytimeService(paths,catalog);var edition=catalog.Editions.First(e=>e.Platform=="psp");var other=catalog.Editions.Last(e=>e.Platform=="psp");var id=Guid.NewGuid().ToString("N");
            service.Record(edition.Id,id,TimeSpan.FromSeconds(30));service.Record(edition.Id,id,TimeSpan.FromSeconds(30));service.Record(edition.Id,id,TimeSpan.FromSeconds(25));service.Record(edition.Id,id,TimeSpan.FromSeconds(45));
            service.Record(edition.Id,Guid.NewGuid().ToString("N"),TimeSpan.FromSeconds(15));
            Check(new PlaytimeService(paths,catalog).Read(edition,new())==TimeSpan.FromMinutes(1));Check(service.Read(other,new())==TimeSpan.Zero);
            Check(PlaytimeService.Format(TimeSpan.Zero)=="0 min"&&PlaytimeService.Format(TimeSpan.FromMinutes(90))=="1 h 30 min");
        });
        test("Copied emulator histories seed once and never double count later Hub sessions",()=>{
            var paths=new HubPaths(Path.Combine(scratch,"playtime-native"));var edition=catalog.Editions.First(e=>e.Platform=="ps2");var state=new HubState();
            foreach(var id in new[]{"first","copy"}){var home=paths.Managed("runtime",id);Directory.CreateDirectory(Path.Combine(home,"inis"));File.WriteAllText(Path.Combine(home,"inis","playtime.dat"),edition.Serial+" 600 1700000000");state.Builds.Add(new(id,"pcsx2","fixture",paths.Portable(home),"fake.exe","","","fixture",DateTimeOffset.UtcNow));}
            var service=new PlaytimeService(paths,catalog);Check(service.Read(edition,state)==TimeSpan.FromMinutes(10));service.Record(edition.Id,Guid.NewGuid().ToString("N"),TimeSpan.FromMinutes(2));
            foreach(var build in state.Builds)File.WriteAllText(Path.Combine(paths.Resolve(build.Path),"inis","playtime.dat"),edition.Serial+" 720 1700000000");
            Check(new PlaytimeService(paths,catalog).Read(edition,state)==TimeSpan.FromMinutes(12));
        });
        asyncTest("Active playtime ticker stops and persists when the session is disposed",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"playtime-ticker"));var edition=catalog.Editions.First(e=>e.Platform=="psp");var service=new PlaytimeService(paths,catalog);var session=service.Start(edition,new());await Task.Delay(100);await session.DisposeAsync();var total=service.Read(edition,new());await Task.Delay(100);await session.DisposeAsync();Check(total>TimeSpan.Zero&&service.Read(edition,new())==total);
        });
        asyncTest("Playtime moves with portable storage without changing its recorded total",async()=>{
            var from=Path.Combine(scratch,"playtime-original");var to=Path.Combine(scratch,"playtime-moved");var paths=new HubPaths(from);var edition=catalog.Editions.First(e=>e.Platform=="psp");new PlaytimeService(paths,catalog).Record(edition.Id,Guid.NewGuid().ToString("N"),TimeSpan.FromMinutes(42));
            if(!SafeFiles.IsWithin(scratch,from)||!SafeFiles.IsWithin(scratch,to))throw new Exception("Unsafe fixture move");await SafeFiles.MoveManagedDirectoryAsync(scratch,from,to);Check(new PlaytimeService(new HubPaths(to),catalog).Read(edition,new())==TimeSpan.FromMinutes(42));
        });
        test("Damaged playtime preserves original bytes and recovers its prior atomic record",()=>{
            var paths=new HubPaths(Path.Combine(scratch,"playtime-recovery"));var edition=catalog.Editions.First(e=>e.Platform=="psp");var service=new PlaytimeService(paths,catalog);var id=Guid.NewGuid().ToString("N");service.Record(edition.Id,id,TimeSpan.FromSeconds(10));service.Record(edition.Id,id,TimeSpan.FromSeconds(20));var file=paths.Managed("config","playtime",edition.Id+".json");File.WriteAllText(file,"damaged");Check(service.Read(edition,new())==TimeSpan.FromSeconds(10));Check(File.ReadAllText(file)=="damaged");
        });
        asyncTest("Installed emulator reuse works without a cached archive or network and concurrent imports deduplicate",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"runtime-reuse"));var backend=catalog.Backends.First(b=>b.Id=="ppsspp");var zip=Path.Combine(scratch,"runtime-reuse.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){using var writer=new StreamWriter(archive.CreateEntry(backend.Executable).Open());writer.Write("synthetic emulator executable");}
            var hash=await SafeFiles.HashAsync(zip);var release=new ReleaseAsset("fixture","fixture","https://github.com/example/example/releases/download/test/runtime.zip",hash,"runtime.zip","2026-10-13","fixture");
            using var http=new HttpClient(new NoNetwork());var runtime=new RuntimeService(paths,catalog,new ProviderDownloader(paths,http),http);
            var installed=await Task.WhenAll(runtime.InstallArchiveAsync(backend.Id,zip,release),runtime.InstallArchiveAsync(backend.Id,zip,release));Check(installed[0].Id==installed[1].Id&&Directory.GetDirectories(paths.Managed("runtime",backend.Id)).Length==1);
            Check((await runtime.InstallAsync(backend.Id,release,true)).Id==installed[0].Id);Check((await runtime.InstallAsync(backend.Id,release,false)).Id==installed[0].Id);
        });
        if(!OperatingSystem.IsWindows())return;
        asyncTest("Windows job closes owned children while leaving an unrelated fixture alive",async()=>{
            var one=Path.Combine(scratch,"job-child.pid");var sibling=Path.Combine(scratch,"unrelated-child.pid");using var unrelated=Process.Start(LaunchService.ProcessInfo(Environment.ProcessPath!,scratch,["--owned-child-fixture",sibling]))!;
            using var group=new OwnedProcessGroup();using var child=Process.Start(LaunchService.ProcessInfo(Environment.ProcessPath!,scratch,["--owned-child-fixture",one]))!;group.Add(child);
            try{await WaitFile(one);await WaitFile(sibling);group.Dispose();await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));Check(child.HasExited&&!unrelated.HasExited);}
            finally{if(!unrelated.HasExited)unrelated.Kill(true);await unrelated.WaitForExitAsync();}
        });
        asyncTest("Unexpected owner exit releases its Windows job and leaves no background child",async()=>{
            var file=Path.Combine(scratch,"orphan-child.pid");using var parent=Process.Start(LaunchService.ProcessInfo(Environment.ProcessPath!,scratch,["--owned-parent-fixture",file]))!;Process? child=null;
            try{await WaitFile(file+".ready");child=Process.GetProcessById(int.Parse(File.ReadAllText(file)));parent.Kill();await parent.WaitForExitAsync();await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));Check(child.HasExited);}
            finally{if(!parent.HasExited)parent.Kill(true);if(child!=null){if(!child.HasExited)child.Kill(true);child.Dispose();}}
        });
        asyncTest("Cancelled utility waits for the owned process to exit and releases output pipes",async()=>{
            var file=Path.Combine(scratch,"utility.pid");using var cancel=new CancellationTokenSource();var pending=LaunchService.RunUtilityAsync(Environment.ProcessPath!,scratch,["--owned-child-fixture",file],Path.Combine(scratch,"utility.log"),TimeSpan.FromSeconds(10),cancel.Token);
            await WaitFile(file);var pid=int.Parse(File.ReadAllText(file));cancel.Cancel();try{await pending;throw new Exception("Expected utility cancellation");}catch(OperationCanceledException){}try{using var p=Process.GetProcessById(pid);Check(p.HasExited);}catch(ArgumentException){}
        });
    }
    private static async Task WaitFile(string file){var until=DateTime.UtcNow.AddSeconds(10);while(!File.Exists(file)&&DateTime.UtcNow<until)await Task.Delay(25);if(!File.Exists(file))throw new Exception("Fixture failed to start");}
    private sealed class NoNetwork:HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>throw new Exception("Installed runtime unexpectedly contacted the network");}
}
