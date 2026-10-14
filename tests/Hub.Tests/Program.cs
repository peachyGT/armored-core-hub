using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hub.Core;

if (args.FirstOrDefault() == "--integration") return await Integration.Run(args.Skip(1).ToArray());
if (args.FirstOrDefault() == "--download-smoke") return await Integration.Download(args.Skip(1).ToArray());
if (args.FirstOrDefault() == "--media-smoke") return await MediaSmoke.Run(args.Skip(1).ToArray());
if (args.FirstOrDefault() == "--qa") return await QaRunner.Run(args.Skip(1).ToArray());
if (args.FirstOrDefault() == "--feature-smoke") return await FeatureSmoke.Run(args.Skip(1).ToArray());
if (args.FirstOrDefault() == "--runtime-detection-smoke") return RuntimeDetectionSmoke.Run(args.Skip(1).ToArray());
if (args.FirstOrDefault() == "--movie-presentation-smoke") return await PresentationMovieSmoke.Run(args.Skip(1).ToArray());
if (args.FirstOrDefault() == "--bios-setup-smoke") return await BiosSetupSmoke.Run(args.Skip(1).ToArray());
if (args.FirstOrDefault() == "--owned-child-fixture") {File.WriteAllText(args[1],Environment.ProcessId.ToString());await Task.Delay(60000);return 0;}
if (args.FirstOrDefault() == "--owned-parent-fixture")
{
    using var owned=new OwnedProcessGroup();
    using var child=System.Diagnostics.Process.Start(LaunchService.ProcessInfo(Environment.ProcessPath!,Environment.CurrentDirectory,["--owned-child-fixture",args[1]]))!;owned.Add(child);
    while(!File.Exists(args[1]))await Task.Delay(20);File.WriteAllText(args[1]+".ready",Environment.ProcessId.ToString());await Task.Delay(60000);return 0;
}
if (args.FirstOrDefault() == "--runtime-fixture")
{
    File.WriteAllText(Path.Combine(Environment.CurrentDirectory,"fixture-process-id"),Environment.ProcessId.ToString());
    var save=Path.Combine(args[1],"PSP","SAVEDATA","FIXTURE");Directory.CreateDirectory(save);File.WriteAllText(Path.Combine(save,"SAVE.DAT"),"progress from synthetic runtime");
    if(args.Length>3) await Task.Delay(int.Parse(args[3]));
    Console.WriteLine("Synthetic runtime completed");return int.Parse(args[2]);
}

var reportRoot = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "test-work"));
var scratch = Path.Combine(reportRoot, "fixtures-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
var root = Path.Combine(scratch, "run-" + Guid.NewGuid().ToString("N"));
var paths = new HubPaths(root);
var catalog = CatalogStore.Load(Path.Combine(AppContext.BaseDirectory, "manifests"));
var tests = new List<(string, Func<Task>)>();
void Test(string name, Action action) => tests.Add((name, () => { action(); return Task.CompletedTask; }));
void Async(string name, Func<Task> action) => tests.Add((name, action));
SmallFeaturesTests.Register(Test,Async,scratch,catalog);
void Check(bool value, string why = "Assertion failed") { if (!value) throw new Exception(why); }
void Throws(Action action) { try { action(); } catch (HubException) { return; } throw new Exception("Expected HubException"); }
async Task Reject(Func<Task> action) { try { await action(); } catch (Exception e) when (e is HubException or IOException or InvalidDataException) { return; } throw new Exception("Expected rejection"); }
Catalog Clone() => JsonSerializer.Deserialize<Catalog>(JsonSerializer.Serialize(catalog,JsonData.Options),JsonData.Options)!;
var mediaService = new MediaService(paths,catalog);
var ps2 = catalog.Editions.Single(x=>x.Serial=="SLUS-20014");
var psp = catalog.Editions.Single(x=>x.Serial=="ULJM-05492");
var ps3 = catalog.Editions.Single(x=>x.Serial=="BLUS30187");

Test("Catalog covers all requested games and separates platform editions",()=>{Check(catalog.Games.Count==15);Check(catalog.Editions.Count>=90);Check(catalog.Editions.Any(x=>x.GameId=="acfa"&&x.Platform=="x360"));Check(catalog.Editions.Any(x=>x.GameId=="acff"&&x.Platform=="ps2"));});
Test("Catalog schema rejection",()=>{var c=Clone();c.SchemaVersion=99;Throws(()=>CatalogStore.Validate(c));});
Test("Duplicate IDs rejected",()=>{var c=Clone();c.Games.Add(c.Games[0]);Throws(()=>CatalogStore.Validate(c));});
Test("Unknown edition reference rejected",()=>{var c=Clone();c.Editions[0]=c.Editions[0] with {GameId="missing"};Throws(()=>CatalogStore.Validate(c));});
Test("Wrong-platform profile rejected",()=>{var c=Clone();c.Profiles[0]=c.Profiles[0] with {BackendId="rpcs3"};Throws(()=>CatalogStore.Validate(c));});
Test("Native Windows exclusions enforced",()=>{var c=Clone();c.Games.Add(new("ac6","Armored Core VI: Fires of Rubicon","VI",6,2023,false,"","VI"));Throws(()=>CatalogStore.Validate(c));});
Test("Version comparison is numeric",()=>{Check(BuildVersion.Compare("v2.10.0","2.9.9")>0);Check(BuildVersion.Compare("1.2.0","1.2.0-rc1")>0);Check(BuildVersion.Compare("828014b","ab342c1")==null);Check(BuildVersion.Compare("0.0.43-20250","0.0.43-19999")>0);});
Test("Path traversal rejected",()=>{Throws(()=>paths.Managed("games","..","..","escape"));Throws(()=>paths.Managed("C:\\escape"));Throws(()=>SafeFiles.Id("../bad"));});
Test("Portable internal and external path round trip",()=>{var inside=paths.Managed("games","a.iso");Check(paths.Resolve(paths.Portable(inside))==inside);var outside=Path.Combine(scratch,"outside.iso");Check(paths.Resolve(paths.Portable(outside))==outside);});
Test("Atomic state and prior recovery",()=>{var store=new StateStore(paths);store.Save(new(){Offline=true});store.Save(new(){Offline=false});File.WriteAllText(paths.Managed("config","state.json"),"{");Check(store.Load().Offline);Check(store.RecoveryNotice!=null);});
var fixture=Path.Combine(scratch,"fixture.iso");Fixtures.Iso(fixture,"SLUS_200.14");
Test("ISO SYSTEM.CNF identifies exact product ID without filename guessing",()=>{var c=mediaService.Probe(fixture);Check(c.EditionId==ps2.Id,c.Reason);Check(c.ProductId=="SLUS-20014");});
Test("Arbitrary text is not treated as a disc identity",()=>{var f=Path.Combine(scratch,"Armored Core SLUS-20014.iso");File.WriteAllText(f,"SLUS_200.14");Check(mediaService.Probe(f).EditionId==null);});
var ps3Folder=Path.Combine(scratch,"ps3-disc");Directory.CreateDirectory(Path.Combine(ps3Folder,"PS3_GAME","USRDIR"));File.WriteAllBytes(Path.Combine(ps3Folder,"PS3_GAME","PARAM.SFO"),Fixtures.Sfo("TITLE_ID","BLUS30187"));File.WriteAllText(Path.Combine(ps3Folder,"PS3_GAME","USRDIR","EBOOT.BIN"),"synthetic executable");
Test("PS3 binary PARAM.SFO identifies edition",()=>Check(mediaService.Probe(ps3Folder).EditionId==ps3.Id));
Test("Malformed SFO offsets safely ignored",()=>Check(MediaService.ReadSfo(new byte[30]).Count==0));
Test("XEX title ID shared-region ambiguity stays explicit",()=>{var dir=Path.Combine(scratch,"xbox");Directory.CreateDirectory(dir);var b=new byte[96];"XEX2"u8.CopyTo(b);BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(20),1);BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(24),0x40006);BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(28),48);Convert.FromHexString("465307D6").CopyTo(b,60);File.WriteAllBytes(Path.Combine(dir,"default.xex"),b);var m=mediaService.Probe(dir);Check(m.ProductId=="465307D6");Check(m.EditionId==null);Check(m.Warnings.Any(w=>w.Contains("shared")));});
Test("CUE escape blocked",()=>{var f=Path.Combine(scratch,"bad.cue");File.WriteAllText(f,"FILE \"../private.bin\" BINARY\n");Throws(()=>MediaService.CueFiles(f).ToList());});
Async("CUE tracks are not duplicate scan candidates",async()=>{var d=Path.Combine(scratch,"cue");Directory.CreateDirectory(d);File.Copy(fixture,Path.Combine(d,"track.bin"));File.WriteAllText(Path.Combine(d,"disc.cue"),"FILE \"track.bin\" BINARY\n TRACK 01 MODE1/2048\n INDEX 01 00:00:00\n");var scan=await mediaService.ScanAsync(d);Check(scan.Count==1);Check(scan[0].Format=="cue");});
MediaRecord? imported=null;
Async("Import fingerprints and registers in place",async()=>{imported=await mediaService.RegisterAsync(mediaService.Probe(fixture),ps2.Id,false,new());Check(imported.Confidence==MediaConfidence.RecognizedUnverified);Check(imported.Fingerprint==await SafeFiles.HashAsync(fixture));Check(!imported.Managed);});
Async("Duplicate content returns existing media",async()=>{Check(imported!=null);var state=new HubState{Media=[imported!]};var second=await mediaService.RegisterAsync(mediaService.Probe(fixture),ps2.Id,false,state);Check(second.Id==imported!.Id);});
Async("Managed copy leaves source intact and relocatable",async()=>{var record=await mediaService.RegisterAsync(mediaService.Probe(fixture),ps2.Id,true,new());Check(record.Managed);Check(!Path.IsPathRooted(record.Path));Check(File.Exists(fixture));Check(await mediaService.VerifyAsync(record));});
Async("Moving the whole Hub preserves registered media and save paths",async()=>{
    var first=new HubPaths(Path.Combine(scratch,"portable-before"));var service=new MediaService(first,catalog);
    var record=await service.RegisterAsync(service.Probe(fixture),ps2.Id,true,new());var saved=new HubState{Media=[record],Offline=true};
    new StateStore(first).Save(saved);var live=new SaveService(first).Live(ps2.SaveFamily,"pcsx2");Directory.CreateDirectory(live);File.WriteAllText(Path.Combine(live,"marker.ps2"),"synthetic save");
    var destination=Path.Combine(scratch,"portable-after");
    await SafeFiles.MoveManagedDirectoryAsync(scratch,first.Root,destination);
    var moved=new HubPaths(destination);
    var reloaded=new StateStore(moved).Load();Check(reloaded.Offline);Check(await new MediaService(moved,catalog).VerifyAsync(reloaded.Media.Single()));
    Check(File.ReadAllText(Path.Combine(new SaveService(moved).Live(ps2.SaveFamily,"pcsx2"),"marker.ps2"))=="synthetic save");
});
Async("Unknown compressed revision can be manually registered",async()=>{var p=Path.Combine(scratch,"unknown.chd");File.WriteAllText(p,"synthetic CHD fixture");var m=await mediaService.RegisterAsync(mediaService.Probe(p),ps2.Id,false,new());Check(m.Confidence==MediaConfidence.UnknownRevision);});
Async("Reference hash distinguishes verified and modified",async()=>{var c=Clone();var i=c.Editions.FindIndex(x=>x.Id==ps2.Id);c.Editions[i]=ps2 with {Hashes=[new("sha256",await SafeFiles.HashAsync(fixture),"fixture","synthetic-test")]};var service=new MediaService(paths,c);var m=await service.RegisterAsync(service.Probe(fixture),ps2.Id,false,new());Check(m.Confidence==MediaConfidence.VerifiedExactDump);var f=Path.Combine(scratch,"changed.iso");File.Copy(fixture,f);using(var fs=new FileStream(f,FileMode.Open,FileAccess.Write)){fs.Position=1;fs.WriteByte(1);}var changed=await service.RegisterAsync(service.Probe(f),ps2.Id,false,new());Check(changed.Confidence==MediaConfidence.ModifiedImage);});
Async("Wrong platform media rejected",async()=>await Reject(()=>mediaService.RegisterAsync(mediaService.Probe(ps3Folder),ps2.Id,false,new())));
Async("Hash references respect container format and algorithm case",async()=>{
    var c=Clone();var index=c.Editions.FindIndex(x=>x.Id==ps2.Id);
    c.Editions[index]=ps2 with {Hashes=[new("SHA256",await SafeFiles.HashAsync(fixture),"fixture","synthetic","iso",new FileInfo(fixture).Length)]};
    var service=new MediaService(paths,c);var original=await service.RegisterAsync(service.Probe(fixture),ps2.Id,false,new());Check(original.Confidence==MediaConfidence.VerifiedExactDump);
    var compressed=Path.Combine(scratch,"different-representation.chd");File.WriteAllText(compressed,"synthetic compressed representation");
    var other=await service.RegisterAsync(service.Probe(compressed),ps2.Id,false,new());Check(other.Confidence==MediaConfidence.UnknownRevision);
});
Async("Interrupted import does not publish managed media",async()=>{using var cts=new CancellationTokenSource();cts.Cancel();try{await mediaService.RegisterAsync(mediaService.Probe(fixture),ps2.Id,true,new(),ct:cts.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}Check(!Directory.EnumerateDirectories(paths.Managed("games")).Any(x=>x.Contains("partial")));});
Test("Missing BIOS blocks launch clearly",()=>{var state=new HubState{Media=[imported!]};state.Selections[ps2.Id]=new(){EditionId=ps2.Id,MediaId=imported!.Id};var d=new LaunchPlanner(paths,catalog).Diagnose(ps2.Id,state);Check(d.Any(x=>x.Name=="System software"&&x.Status=="missing"));Throws(()=>new LaunchPlanner(paths,catalog).Resolve(ps2.Id,state));});
Test("Missing registered BIOS retains actionable repair",()=>{var state=new HubState{Firmware=[new("missing-bios","ps2-bios","firmware/missing.bin","","Previously imported BIOS",false,"US")]};var d=new LaunchPlanner(paths,catalog).Diagnose(ps2.Id,state);var f=d.Single(x=>x.Name=="System software");Check(f.Status=="missing"&&f.Repair=="firmware");});
Test("Patch dependency order and cycles",()=>{var c=Clone();var p=c.Patches[0];c.Patches=[p with {Id="a",Dependencies=["b"]},p with {Id="b",Dependencies=[]}];Check(CatalogStore.ResolvePatches(c,["a"])[0].Id=="b");c.Patches[1]=c.Patches[1] with {Dependencies=["a"]};Throws(()=>CatalogStore.ResolvePatches(c,["a"]));});
Test("Patch incompatibilities rejected",()=>{var c=Clone();var p=c.Patches[0];c.Patches=[p with {Id="a",Incompatibilities=["b"]},p with {Id="b"}];Throws(()=>CatalogStore.ResolvePatches(c,["a","b"]));});
var bytes=Encoding.UTF8.GetBytes("known-good runtime");var sha=Convert.ToHexStringLower(SHA256.HashData(bytes));
AssetProvider Provider(string id)=>new(id,"EmulatorRelease",0,"https://assets.example/primary",["https://assets.example/fallback"],sha,"application/octet-stream","MIT",["assets.example"],0);
Async("Download fallback and offline cache",async()=>{var handler=new FakeHttp((req,n)=>req.RequestUri!.AbsolutePath.Contains("primary")?new(HttpStatusCode.ServiceUnavailable):new(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});var down=new ProviderDownloader(paths,new(handler));var file=await down.FetchAsync(Provider("fallback"),false);Check(await SafeFiles.HashAsync(file)==sha);var count=handler.Count;Check(await down.FetchAsync(Provider("fallback"),true)==file);Check(handler.Count==count);});
Async("Corrupted downloads never commit",async()=>{var p=Provider("corrupt") with {Sha256=new string('a',64)};var down=new ProviderDownloader(paths,new(new FakeHttp((_,_)=>new(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)})));await Reject(()=>down.FetchAsync(p,false));Check(!File.Exists(paths.Managed("cache","objects",p.Sha256)));});
Async("Offline missing cache gives actionable error",async()=>{var down=new ProviderDownloader(paths,new(new FakeHttp((_,_)=>throw new Exception("Network must not run"))));await Reject(()=>down.FetchAsync(Provider("offline") with {Sha256=new string('b',64)},true));});
Async("Truncated response never commits",async()=>{var p=Provider("truncated") with {Sha256=new string('c',64),FallbackUris=[]};var down=new ProviderDownloader(paths,new(new FakeHttp((_,_)=>{var content=new ByteArrayContent(bytes);content.Headers.ContentLength=bytes.Length+10;return new(HttpStatusCode.OK){Content=content};})));await Reject(()=>down.FetchAsync(p,false));});
Test("Provider rejects unsupported assets and hosts",()=>{Throws(()=>ProviderDownloader.ValidateProvider(Provider("bad") with {Kind="GameMedia"}));Throws(()=>ProviderDownloader.ValidateProvider(Provider("bad") with {PrimaryUri="https://evil.example/a"}));});
Async("Download rejects an unapproved redirect before contacting that host", async () => {
    var handler = new FakeHttp((req,n) => { if(n > 1) throw new Exception("Unapproved host contacted"); var response = new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location = new Uri("https://unapproved.example/asset"); return response; });
    await Reject(() => new ProviderDownloader(paths,new(handler)).FetchAsync(Provider("redirect-rejected") with { Sha256 = new string('e',64), FallbackUris = [] },false)); Check(handler.Count == 1);
});
Async("Approved redirects still verify and cache the final asset", async () => {
    var payload = "redirect fixture"u8.ToArray(); var digest = Convert.ToHexStringLower(SHA256.HashData(payload));
    var handler = new FakeHttp((req,n) => { if(n == 1) { var response = new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location = new Uri("/final",UriKind.Relative); return response; } return new(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }; });
    var file = await new ProviderDownloader(paths,new(handler)).FetchAsync(Provider("redirect-accepted") with { Sha256 = digest, FallbackUris = [] },false); Check(await SafeFiles.HashAsync(file) == digest && handler.Count == 2);
});
Async("Archive traversal rejected before writing",async()=>{var zip=Path.Combine(scratch,"traversal.zip");using(var a=ZipFile.Open(zip,ZipArchiveMode.Create)){using var w=new StreamWriter(a.CreateEntry("../escape.txt").Open());w.Write("unsafe");}await Reject(()=>ArchiveInstaller.ExtractAsync(zip,paths.Managed("cache","extract")));Check(!File.Exists(paths.Managed("cache","escape.txt")));});
Async("Archive extraction and build installation are immutable",async()=>{var zip=Path.Combine(scratch,"runtime.zip");using(var a=ZipFile.Open(zip,ZipArchiveMode.Create)){using var w=new StreamWriter(a.CreateEntry("PPSSPPWindows64.exe").Open());w.Write("synthetic executable");}var release=new ReleaseAsset("fixture","fixture","https://github.com/fixture",await SafeFiles.HashAsync(zip),"runtime.zip","2026-10-09","synthetic");var runtime=new RuntimeService(paths,catalog,new(paths,new()),new());var one=await runtime.InstallArchiveAsync("ppsspp",zip,release);var two=await runtime.InstallArchiveAsync("ppsspp",zip,release);Check(one.Id==two.Id);Check(File.Exists(Path.Combine(paths.Resolve(one.Path),one.Executable)));});
Async("Save restore backs up current content",async()=>{var saves=new SaveService(paths);var live=saves.Live("fixture-family","duckstation");Directory.CreateDirectory(live);File.WriteAllText(Path.Combine(live,"card.mcd"),"original save");var snapshot=await saves.BackupAsync("fixture-family","ac1","duckstation","test");File.WriteAllText(Path.Combine(live,"card.mcd"),"new progress");await saves.RestoreAsync(snapshot,"ac1");Check(File.ReadAllText(Path.Combine(live,"card.mcd"))=="original save");Check(saves.List("fixture-family","duckstation").Count==2);});
Async("A temporary Windows file lock does not lose a staged directory",async()=>{
    var source=paths.Managed("cache","locked-stage");var destination=paths.Managed("cache","unlocked-stage");Directory.CreateDirectory(source);var file=Path.Combine(source,"data");File.WriteAllText(file,"preserve staged bytes");
    var held=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
    var release=Task.Run(async()=>{await Task.Delay(180);held.Dispose();});
    await SafeFiles.MoveManagedDirectoryAsync(paths.Root,source,destination);await release;
    Check(File.ReadAllText(Path.Combine(destination,"data"))=="preserve staged bytes");
});
Async("Save backup retries a transient native file lock without changing save bytes",async()=>{
    if(!OperatingSystem.IsWindows())return;
    var saves=new SaveService(paths);var live=saves.Live("locked-save","pcsx2");Directory.CreateDirectory(live);
    var file=Path.Combine(live,"shared.ps2");await File.WriteAllTextAsync(file,"retained progress");
    using var held=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
    var pending=saves.BackupAsync("locked-save","ac2","pcsx2","after native shutdown");
    await Task.Delay(160);Check(!pending.IsCompleted);held.Dispose();
    var snapshot=await pending;
    Check(await File.ReadAllTextAsync(Path.Combine(paths.Resolve(snapshot.Path),"data","shared.ps2"))=="retained progress");
    using var heldAgain=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
    using var ct=new CancellationTokenSource(100);
    try{await saves.BackupAsync("locked-save","ac2","pcsx2","cancel locked backup",ct.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
    Check(saves.List("locked-save","pcsx2").Count==1);
    Check(!Directory.EnumerateDirectories(Path.GetDirectoryName(paths.Resolve(snapshot.Path))!,"*.partial").Any());
});
Async("Damaged save snapshot cannot overwrite current save",async()=>{var saves=new SaveService(paths);var live=saves.Live("damaged-family","pcsx2");Directory.CreateDirectory(live);File.WriteAllText(Path.Combine(live,"card.ps2"),"keep");var snapshot=await saves.BackupAsync("damaged-family","ac2","pcsx2","test");File.WriteAllText(Path.Combine(paths.Resolve(snapshot.Path),"data","card.ps2"),"corrupt");await Reject(()=>saves.RestoreAsync(snapshot,"ac2"));Check(File.ReadAllText(Path.Combine(live,"card.ps2"))=="keep");});
Test("Signed catalog update validates, retains prior and rolls back",()=>{var dest=paths.Managed("manifests","catalog.json");JsonData.WriteAtomic(dest,catalog);using var key=RSA.Create(2048);var next=Clone();next.Version="99.0.0";var payload=JsonSerializer.SerializeToUtf8Bytes(next,JsonData.Options);var envelope=new SignedCatalog(Convert.ToBase64String(payload),Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss)),"test");var updater=new ManifestUpdates(paths);Check(updater.Install(envelope,key.ExportSubjectPublicKeyInfoPem(),"test").Version=="99.0.0");Throws(()=>updater.Install(envelope with {PayloadBase64=Convert.ToBase64String("tampered"u8)},key.ExportSubjectPublicKeyInfoPem(),"test"));Check(updater.Rollback().Version==catalog.Version);});
Test("Process arguments are separate tokens, never shell text",()=>{var p=LaunchService.ProcessInfo("test.exe",root,["--", "C:\\a b\\game & other.iso"]);Check(!p.UseShellExecute);Check(p.ArgumentList.Count==2);Check(p.ArgumentList[1].Contains('&'));});
Async("Launch plan resolves selected PPSSPP build and enhancements",async()=>{var f=Path.Combine(scratch,"psp.iso");File.WriteAllText(f,"synthetic PSP");var m=await mediaService.RegisterAsync(mediaService.Probe(f),psp.Id,false,new());var b=new InstalledBuild("ppsspp-test","ppsspp","1.0",paths.Portable(paths.Managed("runtime","ppsspp-test")),"fake.exe","", "", "fixture",DateTimeOffset.UtcNow);Directory.CreateDirectory(paths.Resolve(b.Path));File.WriteAllText(Path.Combine(paths.Resolve(b.Path),b.Executable),"test");var s=new HubState{Media=[m],Builds=[b]};s.Selections[psp.Id]=new(){EditionId=psp.Id,MediaId=m.Id,BuildId=b.Id,EnhancementId=psp.PreferredProfile+"-enhanced"};var plan=new LaunchPlanner(paths,catalog).Resolve(psp.Id,s);Check(plan.BuildId==b.Id);Check(plan.ConfigFiles.Any(x=>Path.GetFileName(x.Key)=="installed.txt" && x.Value==plan.SaveDirectory));Check(plan.ConfigFiles.Values.Any(x=>x.Contains("InternalResolution=2")));foreach(var invalid in new[]{"999","NaN","Infinity"}) {s.Selections[psp.Id].AdvancedSettings["Graphics/InternalResolution"]=invalid;Throws(()=>new LaunchPlanner(paths,catalog).Resolve(psp.Id,s));}});

Async("Repair replaces a damaged session executable without changing upstream files",async()=>{
    var f=Path.Combine(scratch,"repair-psp.iso");File.WriteAllText(f,"synthetic media for repair");var m=await mediaService.RegisterAsync(mediaService.Probe(f),psp.Id,false,new());
    var source=paths.Managed("runtime","repair-build");Directory.CreateDirectory(source);var exe=Path.Combine(source,"fake.exe");File.WriteAllText(exe,"original immutable executable");
    var b=new InstalledBuild("repair-build","ppsspp","fixture",paths.Portable(source),"fake.exe","archive",await SafeFiles.HashAsync(exe),"fixture",DateTimeOffset.UtcNow);
    var s=new HubState{Media=[m],Builds=[b]};s.Selections[psp.Id]=new(){EditionId=psp.Id,MediaId=m.Id,BuildId=b.Id};
    var plan=new LaunchPlanner(paths,catalog).Resolve(psp.Id,s);var launch=new LaunchService(paths,catalog);await launch.PrepareAsync(plan,s);
    File.Delete(plan.Executable);File.WriteAllText(plan.Executable,"damaged session copy");await launch.PrepareAsync(plan,s);
    Check(await SafeFiles.HashAsync(plan.Executable)==b.ExecutableSha256);Check(await SafeFiles.HashAsync(exe)==b.ExecutableSha256);
});
Test("Windows reserved identifiers rejected",()=>{Throws(()=>SafeFiles.Id("COM7"));Throws(()=>SafeFiles.Id("lpt9.txt"));});
Async("Owned runtime process records exit and protects saves before and after play",async()=>{
    var f=Path.Combine(scratch,"process-psp.iso");File.WriteAllText(f,"synthetic launch media");var m=await mediaService.RegisterAsync(mediaService.Probe(f),psp.Id,false,new());
    var source=paths.Managed("runtime","process-build");Directory.CreateDirectory(source);
    foreach(var file in Directory.EnumerateFiles(AppContext.BaseDirectory)) File.Copy(file,Path.Combine(source,Path.GetFileName(file)));
    var name=OperatingSystem.IsWindows()?"Hub.Tests.exe":"Hub.Tests";var exe=Path.Combine(source,name);
    var b=new InstalledBuild("process-build","ppsspp","fixture",paths.Portable(source),name,"fixture",await SafeFiles.HashAsync(exe),"synthetic",DateTimeOffset.UtcNow);
    var s=new HubState{Media=[m],Builds=[b]};s.Selections[psp.Id]=new(){EditionId=psp.Id,MediaId=m.Id,BuildId=b.Id};
    var plan=new LaunchPlanner(paths,catalog).Resolve(psp.Id,s);
    var liveFile=Path.Combine(plan.SaveDirectory,"PSP","SAVEDATA","FIXTURE","SAVE.DAT");Directory.CreateDirectory(Path.GetDirectoryName(liveFile)!);File.WriteAllText(liveFile,"before session");
    plan=plan with {Arguments=["--runtime-fixture",plan.SaveDirectory,"0"]};
    plan.Environment["DOTNET_ROOT"]=Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),"..","..",".."));
    var launcher=new LaunchService(paths,catalog);Check(launcher.ActiveProcessId==0);Check(await launcher.LaunchAsync(plan,s)==0);Check(!launcher.IsRunning && launcher.ActiveProcessId==0);
    Check(File.ReadAllText(liveFile)=="progress from synthetic runtime");
    var snapshots=new SaveService(paths).List(psp.SaveFamily,"ppsspp");
    foreach(var snapshot in snapshots)
    {
        Check(snapshot.Files.Count==1);var expected=snapshot.Reason=="Before launch"?"before session":"progress from synthetic runtime";
        Check(File.ReadAllText(Path.Combine(paths.Resolve(snapshot.Path),"data",snapshot.Files[0].RelativeName))==expected);
    }
    Check(snapshots.Any(x=>x.Reason=="Before launch")&&snapshots.Any(x=>x.Reason=="After session"));
    await Reject(()=>launcher.LaunchAsync(plan with {Arguments=["--runtime-fixture",plan.SaveDirectory,"9"]},s));Check(!launcher.IsRunning && launcher.ActiveProcessId==0);
    var beforeCancel=new SaveService(paths).List(psp.SaveFamily,"ppsspp").Count;
    File.WriteAllText(liveFile,"before cancelled session");
    using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var running=launcher.LaunchAsync(plan with {Arguments=["--runtime-fixture",plan.SaveDirectory,"0","60000"]},s,cancel.Token);
    while(!running.IsCompleted && File.ReadAllText(liveFile)!="progress from synthetic runtime") await Task.Delay(50);
    Check(launcher.ActiveProcessId==int.Parse(File.ReadAllText(Path.Combine(plan.WorkingDirectory,"fixture-process-id"))),"Ownership must identify the actual launched child, not another process with the same executable name.");
    cancel.Cancel();
    try{await running;throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
    Check(!launcher.IsRunning && launcher.ActiveProcessId==0);
    var cancelledSnapshots=new SaveService(paths).List(psp.SaveFamily,"ppsspp");
    Check(cancelledSnapshots.Count==beforeCancel+2,"Cancelled sessions must retain before and after restore points.");
    Check(cancelledSnapshots[0].Reason=="After session");
    Check(File.ReadAllText(Path.Combine(paths.Resolve(cancelledSnapshots[0].Path),"data",cancelledSnapshots[0].Files[0].RelativeName))=="progress from synthetic runtime");
});
Async("Moved external media relinks without duplicate content",async()=>{
    var from=Path.Combine(scratch,"move.iso");File.WriteAllText(from,"synthetic moved media");var s=new HubState();
    var one=await mediaService.RegisterAsync(mediaService.Probe(from),ps2.Id,false,s);s.Media.Add(one);
    var to=Path.Combine(scratch,"relocated.iso");File.Move(from,to);
    var two=await mediaService.RegisterAsync(mediaService.Probe(to),ps2.Id,false,s);
    Check(one.Id==two.Id);Check(paths.Resolve(two.Path)==to);Check(await mediaService.VerifyAsync(two));
});
Async("Save import preflights all collisions",async()=>{
    var service=new SaveService(paths);var live=service.Live("collision","duckstation");Directory.CreateDirectory(live);File.WriteAllText(Path.Combine(live,"z.mcd"),"keep");
    var source=Path.Combine(scratch,"save-source");Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"a.mcd"),"new");File.WriteAllText(Path.Combine(source,"z.mcd"),"collision");
    await Reject(()=>service.ImportAsync(source,"collision","ac1","duckstation"));
    Check(File.ReadAllText(Path.Combine(live,"z.mcd"))=="keep");Check(!File.Exists(Path.Combine(live,"a.mcd")));
});
Async("Cancelled save import preserves existing save root",async()=>{
    var service=new SaveService(paths);var live=service.Live("cancel-save","duckstation");Directory.CreateDirectory(live);File.WriteAllText(Path.Combine(live,"card"),"keep");
    using var cts=new CancellationTokenSource();cts.Cancel();
    try{await service.ImportAsync(scratch,"cancel-save","ac1","duckstation",cts.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
    Check(File.ReadAllText(Path.Combine(live,"card"))=="keep");
});
Test("Licensed local patch installs disabled and validates exact hashes",()=>{
    var c=Clone();var profile=c.Profiles.Single(x=>x.EditionId==ps2.Id);
    var patch=new PatchModule("test-module","Synthetic test module","Test suite","1.0","Optional","https://example.com","MIT",[ps2.Id],[new string('a',64)],[],[],"configuration",new(){[profile.Settings.Keys.First()]="false"},null,null,"Fixture","Fixture",false);
    var file=Path.Combine(scratch,"module.json");JsonData.WriteAtomic(file,patch);var service=new PatchService(paths,c);service.Import(file);
    Check(c.Patches.Any(x=>x.Id==patch.Id));Check(new HubState().Selections.Count==0);
    var invalid=patch with {Id="bad-module",SourceHashes=[]};JsonData.WriteAtomic(file,invalid);Throws(()=>service.Import(file));
    var loaded=Clone();new PatchService(paths,loaded).LoadInstalled();Check(loaded.Patches.Any(x=>x.Id==patch.Id));
});
Test("RPCS3 teardown workaround requires success evidence and complete files",()=>{
    var h=paths.Managed("cache","firmware-check");Directory.CreateDirectory(Path.Combine(h,"log"));File.WriteAllText(Path.Combine(h,"log","RPCS3.log"),"Successfully installed PS3 firmware version 4.93.");
    Check(!LaunchService.FirmwareInstallSucceeded(h,unchecked((int)0xC0000409)));
    Directory.CreateDirectory(Path.Combine(h,"dev_flash","vsh","module"));Directory.CreateDirectory(Path.Combine(h,"dev_flash","sys","external"));
    File.WriteAllText(Path.Combine(h,"dev_flash","vsh","module","vsh.self"),"fixture");File.WriteAllText(Path.Combine(h,"dev_flash","sys","external","liblv2.sprx"),"fixture");
    Check(LaunchService.FirmwareInstallSucceeded(h,unchecked((int)0xC0000409)));Check(!LaunchService.FirmwareInstallSucceeded(h,7));
});
Async("Cancelled download never publishes a partial cache object",async()=>{
    var p=Provider("cancel-download") with {Sha256=new string('d',64)};using var cts=new CancellationTokenSource();cts.Cancel();
    try{await new ProviderDownloader(paths,new(new FakeHttp((_,_)=>throw new Exception("Network must not run")))).FetchAsync(p,false,null,cts.Token);throw new Exception("Expected cancel");}catch(OperationCanceledException){}
    Check(!File.Exists(paths.Managed("cache","objects",p.Sha256)));
});
Test("Controller override cannot escape its input section",()=>{
    var planPaths = new HubPaths(Path.Combine(scratch,"controller-check"));
    var c=Clone();var e=c.Editions.First(x=>x.Platform=="ps1");var p=c.Profiles.Single(x=>x.EditionId==e.Id);
    var dir=planPaths.Managed("runtime","fixture");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"fake.exe"),"x");
    var f=planPaths.Managed("firmware","bios.bin");File.WriteAllText(f,"synthetic");
    var m=planPaths.Managed("games","test.iso");File.WriteAllText(m,"synthetic");
    var s=new HubState{Builds=[new("build","duckstation","fixture",planPaths.Portable(dir),"fake.exe","","","fixture",DateTimeOffset.UtcNow)],Firmware=[new("fw","ps1-bios",planPaths.Portable(f),"","fixture",false,e.Region)],Media=[new("media",planPaths.Portable(m),"iso",e.Id,"",MediaConfidence.UnknownRevision,"",[],DateTimeOffset.UtcNow,false)]};
    s.Selections[e.Id]=new(){EditionId=e.Id,BuildId="build",MediaId="media"};
    JsonData.WriteAtomic(planPaths.Managed("config","controls",e.Id,"controller-overrides.json"),new Dictionary<string,string>{{"Folders/MemoryCards","C:/outside"}});
    Throws(()=>new LaunchPlanner(planPaths,c).Resolve(e.Id,s));
});

PresentationTests.Register(Test, scratch);
ImportTests.Register(Test, Async, scratch, catalog);
InputArchiveTests.Register(Test, Async, scratch, catalog);
FirmwareSetupTests.Register(Test, Async, scratch, catalog);
AchievementVisualTests.Register(Test, Async, scratch, catalog);
DiscMovieTests.Register(Test, scratch);
CompatibilityTests.Register(Test, Async, scratch, catalog);
RuntimeDetectionTests.Register(Test, scratch, catalog);
CommunityControlTests.Register(Test, Async, scratch, catalog);
SettingsTests.Register(Test, Async, scratch, catalog);
GameControlTests.Register(Test, scratch, catalog);
CoverTests.Register(Test, scratch, catalog);
CoreAuditTests.Register(Test, Async, scratch, catalog);
ControlAuditTests.Register(Test, scratch, catalog);
NativeAchievementTests.Register(Test, Async, scratch, catalog);
XeniaProfileTests.Register(Test, Async, scratch, catalog);
MediaFoundationVideoTests.Register(Test, scratch);
var passed=0;var failures=new List<string>();var watch=System.Diagnostics.Stopwatch.StartNew();
foreach(var (name,test) in tests){try{await test();Console.WriteLine("PASS "+name);passed++;}catch(Exception e){Console.WriteLine("FAIL "+name+" — "+e);failures.Add(name+": "+e.Message);}}
var report=new {passed,total=tests.Count,elapsedMs=watch.ElapsedMilliseconds,failures};JsonData.WriteAtomic(Path.Combine(reportRoot,"test-results.json"),report);
Console.WriteLine($"\n{passed}/{tests.Count} passed in {watch.ElapsedMilliseconds} ms");return failures.Count==0?0:1;

sealed class FakeHttp(Func<HttpRequestMessage,int,HttpResponseMessage> respond):HttpMessageHandler
{public int Count;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){ct.ThrowIfCancellationRequested();return Task.FromResult(respond(request,++Count));}}
static class Fixtures
{
    public static byte[] Sfo(string key,string value)
    {
        var keyBytes=Encoding.UTF8.GetBytes(key+'\0');var data=Encoding.UTF8.GetBytes(value+'\0');var result=new byte[36+keyBytes.Length+data.Length];new byte[]{0,80,83,70}.CopyTo(result,0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8),36);BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12),(uint)(36+keyBytes.Length));BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16),1);BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(24),(uint)data.Length);keyBytes.CopyTo(result,36);data.CopyTo(result,36+keyBytes.Length);return result;
    }
    public static void Iso(string file,string serial)
    {
        var image=new byte[24*2048];var pvd=image.AsSpan(16*2048,2048);pvd[0]=1;"CD001"u8.CopyTo(pvd[1..]);pvd[6]=1;
        var content=Encoding.ASCII.GetBytes("BOOT2 = cdrom0:\\"+serial+";1\nVER = 1.00\n");
        static byte[] Record(uint lba,uint length,byte[] name,bool dir){var r=new byte[33+name.Length+(name.Length%2==0?1:0)];r[0]=(byte)r.Length;BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(2),lba);BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(10),length);r[25]=dir?(byte)2:(byte)0;r[32]=(byte)name.Length;name.CopyTo(r,33);return r;}
        Record(20,2048,[0],true).CopyTo(pvd[156..]);Record(21,(uint)content.Length,Encoding.ASCII.GetBytes("SYSTEM.CNF;1"),false).CopyTo(image,20*2048);content.CopyTo(image,21*2048);File.WriteAllBytes(file,image);
    }
}
