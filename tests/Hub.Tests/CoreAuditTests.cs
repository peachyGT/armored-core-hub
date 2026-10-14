using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Hub.Core;

static class CoreAuditTests
{
    public static void Register(Action<string,Action> test,Action<string,Func<Task>> asyncTest,string scratch,Catalog catalog)
    {
        void Check(bool value){if(!value)throw new Exception("Core audit regression");}
        async Task Reject(Func<Task> action){try{await action();}catch(HubException){return;}throw new Exception("Expected rejection");}
        asyncTest("Save restore removes progress created after a snapshot while retaining installed PS3 game data",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-save-members"));var service=new SaveService(paths);var live=service.Live("fixture","rpcs3");
            var progress=Path.Combine(live,"dev_hdd0","home","00000001","savedata","old.dat");Directory.CreateDirectory(Path.GetDirectoryName(progress)!);await File.WriteAllTextAsync(progress,"checkpoint");
            var snapshot=await service.BackupAsync("fixture","test","rpcs3","checkpoint");await File.WriteAllTextAsync(progress,"later progress");
            var extra=Path.Combine(Path.GetDirectoryName(progress)!,"new.dat");await File.WriteAllTextAsync(extra,"created later");
            var installed=Path.Combine(live,"dev_hdd0","game","installed.dat");Directory.CreateDirectory(Path.GetDirectoryName(installed)!);await File.WriteAllTextAsync(installed,"installed game");
            await service.RestoreAsync(snapshot,"test");Check(await File.ReadAllTextAsync(progress)=="checkpoint"&&!File.Exists(extra)&&await File.ReadAllTextAsync(installed)=="installed game");
            Check(service.List("fixture","rpcs3").Any(s=>s.Reason.StartsWith("Before restoring")&&s.Files.Any(f=>f.RelativeName.EndsWith("new.dat"))));
        });
        asyncTest("A snapshot changed during pre-restore backup cannot replace current saves",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-snapshot-race"));var service=new SaveService(paths);var live=service.Live("fixture","pcsx2");Directory.CreateDirectory(live);
            var file=Path.Combine(live,"save.dat");await File.WriteAllTextAsync(file,"checkpoint");var snapshot=await service.BackupAsync("fixture","test","pcsx2","checkpoint");await File.WriteAllTextAsync(file,"current progress");
            var created=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watcher=new FileSystemWatcher(Path.GetDirectoryName(paths.Resolve(snapshot.Path))!){NotifyFilter=NotifyFilters.DirectoryName};
            watcher.Created+=(_,eventArgs)=>{if(eventArgs.Name!.EndsWith(".partial",StringComparison.Ordinal))created.TrySetResult();};watcher.EnableRaisingEvents=true;
            Task restore;using(var held=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            {
                restore=service.RestoreAsync(snapshot,"test");await created.Task.WaitAsync(TimeSpan.FromSeconds(4));
                await File.WriteAllTextAsync(Path.Combine(paths.Resolve(snapshot.Path),"data","save.dat"),"tampered checkpoint");
            }
            await Reject(()=>restore);Check(await File.ReadAllTextAsync(file)=="current progress");
        });
        asyncTest("Reimporting an identical local patch preserves its selected options and immutable payload",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-patch-reimport"));var source=Path.Combine(scratch,"audit-patch-game.iso");Fixtures.Iso(source,"SLUS_200.14");var mediaService=new MediaService(paths,catalog);
            var media=await mediaService.RegisterAsync(mediaService.Probe(source),"ac2-ps2-slus20014",false,new());var state=new HubState{Media=[media]};var patches=new LocalPatchService(paths,catalog);
            var file=Path.Combine(scratch,"12345678.pnach");await File.WriteAllTextAsync(file,"[Optional analogs]\npatch=1,EE,00100000,word,00000000\n");
            var first=await patches.ImportAsync(file,media,state,true);patches.SetSections(first,[]);
            using(var locked=new FileStream(paths.Resolve(first.Payload),FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                var again=await patches.ImportAsync(file,media,state,true);Check(again.Id==first.Id&&again.EnabledSections?.Length==0);
            }
            Check(await SafeFiles.HashAsync(paths.Resolve(first.Payload))==first.PayloadHash);
        });
        test("Valid JSON with null state collections recovers the previous library instead of crashing later",()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-null-library"));var store=new StateStore(paths);store.Save(new(){Offline=true});store.Save(new(){Offline=false});
            File.WriteAllText(paths.Managed("config","state.json"),"{\"schemaVersion\":1,\"media\":null}");
            var recovered=store.Load();Check(recovered.Offline&&recovered.Media!=null&&store.RecoveryNotice!=null);
        });
        asyncTest("PlayStation BIOS reviewed MD5 references accept documented algorithm capitalization",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-bios-algorithm"));var file=Path.Combine(scratch,"audit-synthetic-ps1.bin");var bytes=new byte[524288];await File.WriteAllBytesAsync(file,bytes);
            var clone=JsonSerializer.Deserialize<Catalog>(JsonSerializer.Serialize(catalog,JsonData.Options),JsonData.Options)!;
            clone.Firmware.Add(new("audit-synthetic","Synthetic reviewed fixture",[524288],[new("MD5",Convert.ToHexStringLower(MD5.HashData(bytes)),"US","https://example.com/reference")],"https://example.com/guide","ps1-bios"));
            var record=await new FirmwareService(paths,clone).ImportAsync(file);Check(record.Verified&&record.Region=="US");
        });
        asyncTest("Reimporting a clean duplicate repairs a stale external registration and refreshed timestamps",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-media-reimport"));var service=new MediaService(paths,catalog);var original=Path.Combine(scratch,"audit-stale.iso");var clean=Path.Combine(scratch,"audit-clean.iso");Fixtures.Iso(original,"SLUS_200.14");File.Copy(original,clean);
            var record=await service.RegisterAsync(service.Probe(original),"ac2-ps2-slus20014",false,new());var state=new HubState{Media=[record]};File.WriteAllText(original,"damaged old copy");
            var repaired=await service.RegisterAsync(service.Probe(clean),record.EditionId,false,state);Check(repaired.Id==record.Id&&paths.Resolve(repaired.Path)==clean&&await service.VerifyAsync(repaired));
            state.Media=[repaired];File.SetLastWriteTimeUtc(clean,File.GetLastWriteTimeUtc(clean).AddDays(1));
            var refreshed=await service.RegisterAsync(service.Probe(clean),record.EditionId,false,state);
            Check(refreshed.Files.Single().LastWriteUtcTicks==File.GetLastWriteTimeUtc(clean).Ticks&&await service.VerifyAsync(refreshed));
        });
        asyncTest("Runtime session writes to shipped settings cannot change the pristine build",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-mutable-runtime"));var source=paths.Managed("runtime","fixture");Directory.CreateDirectory(source);
            var exe=Path.Combine(source,"PPSSPPWindows64.exe");await File.WriteAllTextAsync(exe,"synthetic executable");await File.WriteAllTextAsync(Path.Combine(source,"upstream.ini"),"immutable upstream settings");
            var edition=catalog.Editions.First(e=>e.Platform=="psp");var game=paths.Managed("games","fixture.iso");await File.WriteAllTextAsync(game,"fixture");var state=new HubState{
                Builds=[new("fixture","ppsspp","1.20.3",paths.Portable(source),Path.GetFileName(exe),"",await SafeFiles.HashAsync(exe),"fixture",DateTimeOffset.UtcNow)],
                Media=[new("media",paths.Portable(game),"iso",edition.Id,"",MediaConfidence.UnknownRevision,"fixture",[],DateTimeOffset.UtcNow,false)]};
            state.Selections[edition.Id]=new(){EditionId=edition.Id,MediaId="media",BuildId="fixture"};var plan=new LaunchPlanner(paths,catalog).Resolve(edition.Id,state);
            await new LaunchService(paths,catalog).PrepareAsync(plan,state);await File.WriteAllTextAsync(Path.Combine(plan.WorkingDirectory,"upstream.ini"),"native emulator changed settings");
            Check(await File.ReadAllTextAsync(Path.Combine(source,"upstream.ini"))=="immutable upstream settings");
        });
        asyncTest("Partial RPCS3 firmware does not pass preparation as a complete installation",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-partial-firmware"));var edition=catalog.Editions.Single(e=>e.Id=="acfa-ps3-blus30187");var source=paths.Managed("runtime","fixture");Directory.CreateDirectory(source);
            var exe=Path.Combine(source,"rpcs3.exe");await File.WriteAllTextAsync(exe,"synthetic executable");var home=paths.Managed("runtime","sessions",edition.Id,"fixture");Directory.CreateDirectory(home);
            var one=Path.Combine(home,"dev_flash","vsh","module","vsh.self");Directory.CreateDirectory(Path.GetDirectoryName(one)!);await File.WriteAllTextAsync(one,"partial install");
            var state=new HubState{Builds=[new("fixture","rpcs3","0.0.43-20152",paths.Portable(source),"rpcs3.exe","",await SafeFiles.HashAsync(exe),"fixture",DateTimeOffset.UtcNow)]};
            var plan=new LaunchPlan(edition.GameId,edition.Id,"unused",edition.PreferredProfile,"fixture",Path.Combine(home,"rpcs3.exe"),home,[],[],[],new SaveService(paths).Live(edition.SaveFamily,"rpcs3"),[],[]);
            await Reject(()=>new LaunchService(paths,catalog).PrepareAsync(plan,state));Check(!LaunchService.FirmwareInstallSucceeded(home,0));
        });
        asyncTest("Snapshot traversal cannot overwrite installed RPCS3 game data",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-save-scope"));var service=new SaveService(paths);var live=service.Live("fixture","rpcs3");
            var installed=Path.Combine(live,"dev_hdd0","game","installed.dat");Directory.CreateDirectory(Path.GetDirectoryName(installed)!);await File.WriteAllTextAsync(installed,"keep installed data");
            var folder=paths.Managed("saves","fixture","rpcs3","backups","malformed");var source=Path.Combine(folder,"data","dev_hdd0","game","installed.dat");Directory.CreateDirectory(Path.GetDirectoryName(source)!);await File.WriteAllTextAsync(source,"must never restore here");
            var snapshot=new SaveSnapshot("malformed","fixture","test","rpcs3","fixture",DateTimeOffset.UtcNow,paths.Portable(folder),[new("dev_hdd0/home/../game/installed.dat",new FileInfo(source).Length,await SafeFiles.HashAsync(source),0)]);
            await Reject(()=>service.RestoreAsync(snapshot,"test"));Check(await File.ReadAllTextAsync(installed)=="keep installed data");
        });
        test("Malformed installed configuration modules fail with a path and leave the catalog unchanged",()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-module-validation"));var clone=JsonSerializer.Deserialize<Catalog>(JsonSerializer.Serialize(catalog,JsonData.Options),JsonData.Options)!;
            var edition=clone.Editions.First(e=>e.Platform=="ps2");var profile=clone.Profiles.Single(p=>p.Id==edition.PreferredProfile);
            var valid=new PatchModule("audit-valid","Test module","Test","1","Optional","https://example.com","MIT",[edition.Id],[new string('a',64)],[],[],"configuration",new(){[profile.Settings.Keys.First()]="false"},null,null,"Fixture","Fixture",false);
            var good=paths.Managed("patches","audit-valid","1","module.json");JsonData.WriteAtomic(good,valid);
            var bad=paths.Managed("patches","audit-bad","1","module.json");var before=clone.Patches.Count;
            foreach(var invalid in new[]{valid with{Id="audit-bad",SourceHashes=null!},valid with{Id="audit-bad",Editions=null!},valid with{Id="audit-bad",Dependencies=null!},valid with{Id="audit-bad",Settings=new(){[profile.Settings.Keys.First()]=null!}}})
            {
                JsonData.WriteAtomic(bad,invalid);try{new PatchService(paths,clone).LoadInstalled();throw new Exception("Expected malformed module rejection");}
                catch(HubException e){Check(e.Message.Contains("module.json")&&clone.Patches.Count==before&&!clone.Patches.Any(p=>p.Id==valid.Id));}
            }
        });
        test("Configuration module imports enforce a finite metadata limit",()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-module-bound"));var file=Path.Combine(scratch,"audit-oversized-module.json");File.WriteAllText(file,"{}"+new string(' ',1024*1024));
            try{new PatchService(paths,catalog).Import(file);throw new Exception("Expected oversized module rejection");}
            catch(HubException e){Check(e.Message.Contains("size",StringComparison.OrdinalIgnoreCase));}
        });
        asyncTest("Media verification rejects absent media and an empty integrity record",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-empty-media"));var service=new MediaService(paths,catalog);var absent=paths.Managed("games","absent.iso");
            var record=new MediaRecord("fixture",paths.Portable(absent),"iso","ac2-ps2-slus20014","",MediaConfidence.UnknownRevision,"fixture",[],DateTimeOffset.UtcNow,false);
            Check(!await service.VerifyAsync(record));await File.WriteAllTextAsync(absent,"fixture");Check(!await service.VerifyAsync(record));
        });
        test("Malformed local patch metadata is a bounded actionable error before reading its payload",()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-local-patch-bound"));var id=new string('a',64);var edition="ac2-ps2-slus20014";var file=paths.Managed("patches","local",edition,id,"patch.json");
            JsonData.WriteAtomic(file,new LocalGamePatch(id,edition,"Fixture","pnach",null!,new string('b',64),"unrelated.bin","12345678",null,false));
            try{new LocalPatchService(paths,catalog).List(edition);throw new Exception("Expected invalid local patch rejection");}
            catch(HubException e){Check(e.Message.Contains("patch.json"));}
            File.WriteAllText(file,"{}"+new string(' ',256*1024));
            try{new LocalPatchService(paths,catalog).List(edition);throw new Exception("Expected oversized local patch rejection");}
            catch(HubException e){Check(e.Message.Contains("size",StringComparison.OrdinalIgnoreCase));}
        });
        asyncTest("PPSSPP backup excludes native achievement credentials even in a misplaced save folder",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"audit-save-secrets"));var service=new SaveService(paths);var live=service.Live("fixture","ppsspp");var folder=Path.Combine(live,"PSP","SAVEDATA","fixture");Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"DATA.BIN"),"game progress");await File.WriteAllTextAsync(Path.Combine(folder,"ppsspp_retroachievements.dat"),"synthetic private account data");
            var snapshot=await service.BackupAsync("fixture","test","ppsspp","fixture");Check(snapshot.Files.Count==1&&snapshot.Files[0].RelativeName.EndsWith("DATA.BIN"));
        });
    }
}
