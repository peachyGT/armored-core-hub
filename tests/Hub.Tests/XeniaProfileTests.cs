using System.Diagnostics;
using Hub.Core;

static class XeniaProfileTests
{
    public static void Register(Action<string,Action> test,Action<string,Func<Task>> asyncTest,string scratch,Catalog catalog)
    {
        void Check(bool value){if(!value)throw new Exception("Xenia local profile regression");}
        async Task Reject(Func<Task> action){try{await action();}catch(HubException){return;}throw new Exception("Expected profile rejection");}
        asyncTest("Fresh Xenia save storage receives one valid stable offline player and complete native header",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"xenia-fresh-profile"));var save=new SaveService(paths).Live("fixture","xenia");var config=paths.Managed("cache","settings.toml");
            var id=await XeniaProfiles.EnsureLocalAsync(paths,save,config);var profile=XeniaProfiles.ExistingLocal(save).Single();var account=Path.Combine(save,id,"FFFE07D1","00010000",id,"Account");var hash=await SafeFiles.HashAsync(account);
            Check(profile.Id==id&&profile.Gamertag=="Hub"&&profile.IsValid&&id.StartsWith("E0300000")&&new FileInfo(account).Length==0x194);
            var header=Path.Combine(save,id,"FFFE07D1","Headers","00010000",id+".header");Check(new FileInfo(header).Length==0xa000);
            Check(await XeniaProfiles.EnsureLocalAsync(paths,save,config)==id&&await SafeFiles.HashAsync(account)==hash&&XeniaProfiles.ExistingLocal(save).Count==1);
            var contents=XeniaProfiles.ApplyLocal("[Profiles]\nlogged_profile_slot_0_xuid = \"0000000000000000\"\n[Other]\nvalue=true\n",id);Check(contents.Contains(id)&&!contents.Contains("0000000000000000")&&contents.Contains("[Other]"));
        });
        asyncTest("Multiple Xenia players require selection and preserve account bytes across build changes",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"xenia-multiple-profiles"));var service=new SaveService(paths);var save=service.Live("fixture","xenia");var other=service.Live("other","xenia");var config=paths.Managed("cache","settings.toml");
            var first=await XeniaProfiles.EnsureLocalAsync(paths,save,config);var second=await XeniaProfiles.EnsureLocalAsync(paths,other,config);
            foreach(var file in SafeFiles.Files(Path.Combine(other,second))){var target=SafeFiles.Within(Path.Combine(save,second),Path.GetRelativePath(Path.Combine(other,second),file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}
            await Reject(()=>XeniaProfiles.EnsureLocalAsync(paths,save,config));
            var original=XeniaProfiles.ExistingLocal(save).ToDictionary(p=>p.Id,p=>File.ReadAllBytes(Path.Combine(save,p.Id,"FFFE07D1","00010000",p.Id,"Account")));
            XeniaProfiles.ChooseLocal(paths,save,second);File.WriteAllText(config,XeniaProfiles.ApplyLocal("",first));Check(XeniaProfiles.SelectLocal(save,config)==second);
            Check(await XeniaProfiles.EnsureLocalAsync(paths,save,paths.Managed("cache","new-build.toml"))==second);
            Check(original.All(p=>p.Value.SequenceEqual(File.ReadAllBytes(Path.Combine(save,p.Key,"FFFE07D1","00010000",p.Key,"Account")))));
        });
        asyncTest("Damaged or older Xenia player data is kept instead of silently receiving a new identity",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"xenia-profile-recovery"));var save=new SaveService(paths).Live("fixture","xenia");var config=paths.Managed("cache","settings.toml");
            var id=await XeniaProfiles.EnsureLocalAsync(paths,save,config);var account=Path.Combine(save,id,"FFFE07D1","00010000",id,"Account");var data=File.ReadAllBytes(account);data[^1]^=1;File.WriteAllBytes(account,data);
            await Reject(()=>XeniaProfiles.EnsureLocalAsync(paths,save,config));Check(XeniaProfiles.ExistingLocal(save).Single().IsValid==false&&File.ReadAllBytes(account).SequenceEqual(data));
            var legacy=new SaveService(paths).Live("legacy","xenia");Directory.CreateDirectory(legacy);var progress=Path.Combine(legacy,"older-save.dat");File.WriteAllText(progress,"keep progress");
            await Reject(()=>XeniaProfiles.EnsureLocalAsync(paths,legacy,config));Check(File.ReadAllText(progress)=="keep progress"&&Directory.GetDirectories(legacy).Length==0);
        });
        asyncTest("Cancelled or unmanaged Xenia profile setup publishes no account",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"xenia-profile-cancel"));var save=new SaveService(paths).Live("fixture","xenia");using var cancellation=new CancellationTokenSource();cancellation.Cancel();
            try{await XeniaProfiles.EnsureLocalAsync(paths,save,paths.Managed("cache","settings.toml"),cancellation.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
            Check(!Directory.Exists(save));var outside=Path.Combine(scratch,"outside-player-storage");await Reject(()=>XeniaProfiles.EnsureLocalAsync(paths,outside,"unused"));Check(!Directory.Exists(outside));
        });
        asyncTest("Xenia launch preparation selects its fresh offline player without native setup dialogs",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"xenia-profile-prepare"));var edition=catalog.Editions.First(e=>e.Platform=="x360");var source=paths.Managed("runtime","fixture");Directory.CreateDirectory(source);var exe=Path.Combine(source,"xenia_canary.exe");await File.WriteAllTextAsync(exe,"synthetic executable");
            var game=paths.Games("fixture.iso");await File.WriteAllTextAsync(game,"fixture");var state=new HubState{Builds=[new("fixture","xenia","828014b",paths.Portable(source),Path.GetFileName(exe),"",await SafeFiles.HashAsync(exe),"fixture",DateTimeOffset.UtcNow)],Media=[new("media",paths.Portable(game),"iso",edition.Id,"",MediaConfidence.UnknownRevision,"fixture",[],DateTimeOffset.UtcNow,false)]};
            state.Selections[edition.Id]=new(){EditionId=edition.Id,MediaId="media",BuildId="fixture"};var plan=new LaunchPlanner(paths,catalog).Resolve(edition.Id,state);await new LaunchService(paths,catalog).PrepareAsync(plan,state);
            var profile=XeniaProfiles.ExistingLocal(plan.SaveDirectory).Single();Check(File.ReadAllText(Path.Combine(plan.WorkingDirectory,"xenia-canary.config.toml")).Contains("logged_profile_slot_0_xuid = \""+profile.Id+"\"")&&profile.IsValid);
        });
        if(Environment.GetEnvironmentVariable("HUB_TEST_XENIA_PROFILES") is string native&&File.Exists(native))
        asyncTest("Official Canary decrypts and signs in the Hub-created local profile with no guest game",async()=>{
            var paths=new HubPaths(Path.Combine(Environment.CurrentDirectory,"work","xenia-profile-parser-"+Guid.NewGuid().ToString("N")[..8]));var save=new SaveService(paths).Live("fixture","xenia");var home=paths.Managed("runtime","parser");Directory.CreateDirectory(home);var config=Path.Combine(home,"xenia-canary.config.toml");
            var id=await XeniaProfiles.EnsureLocalAsync(paths,save,config);var executable=Path.Combine(home,"xenia_canary.exe");File.Copy(native,executable);File.WriteAllText(config,XeniaProfiles.ApplyLocal("",id));var log=Path.Combine(home,"native-profile-parser.log");
            using var process=new Process{StartInfo=LaunchService.ProcessInfo(executable,home,["--config="+config,"--storage_root="+home,"--content_root="+save,"--cache_root="+Path.Combine(home,"cache"),"--log_file="+log,"--discord=false","--apu=nop","--hid=nop"])};
            process.StartInfo.WindowStyle=ProcessWindowStyle.Normal;process.Start();var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();string evidence="";
            try
            {
                var until=DateTime.UtcNow.AddSeconds(25);
                while(DateTime.UtcNow<until&&!process.HasExited)
                {
                    await Task.Delay(100);if(File.Exists(log)){using var input=new FileStream(log,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var reader=new StreamReader(input);evidence=await reader.ReadToEndAsync();}
                    if(evidence.Contains("Loaded Hub (GUID: "+id+") to slot 0"))break;
                }
            }
            finally{if(!process.HasExited){process.CloseMainWindow();if(!process.WaitForExit(3000))process.Kill(true);}await process.WaitForExitAsync();await Task.WhenAll(output,error);await File.WriteAllTextAsync(log+".stdout",await output);await File.WriteAllTextAsync(log+".stderr",await error);}
            Console.WriteLine("Native profile parser exit: "+process.ExitCode+"; evidence: "+log);
            Check(evidence.Contains("Loaded Hub (GUID: "+id+") to slot 0")&&!evidence.Contains("Failed to decrypt account"));
            Console.WriteLine("Native profile evidence: "+log);
        });
    }
}
