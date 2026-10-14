using System.Buffers.Binary;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hub.Core;

static class NativeAchievementTests
{
    private const string Token="0123456789ABCDEF0123456789ABCDEF";
    public static void Register(Action<string,Action> test,Action<string,Func<Task>> asyncTest,string scratch,Catalog catalog)
    {
        void Check(bool value){if(!value)throw new Exception("Native achievement regression");}
        void Reject(Action action){try{action();}catch(HubException){return;}throw new Exception("Expected achievement rejection");}
        async Task Rejected(Func<Task> action,string? secret=null){try{await action();}catch(HubException e){Check(secret==null||!e.ToString().Contains(secret));return;}throw new Exception("Expected achievement rejection");}
        HubPaths Paths()=>new(Path.Combine(scratch,"native-achievement-"+Guid.NewGuid().ToString("N")));
        NativeAchievementAccount Account(string user="FixtureUser")=>new(){Username=user,Token=Token,SignedInAt=DateTimeOffset.FromUnixTimeSeconds(1700000000)};
        var ps1=catalog.Editions.First(e=>e.GameId=="ac1"&&e.Platform=="ps1");
        test("All catalog achievement backends and provider IDs are explicit without invented unavailable sets",()=>{
            var combos=catalog.Editions.DistinctBy(e=>e.GameId+"/"+e.Platform).ToList();var retro=combos.Where(e=>e.Platform is "ps1" or "ps2" or "psp").ToList();
            Check(retro.Count==15&&retro.All(e=>AchievementSupport.Get(e).GameId>0)&&retro.Count(e=>AchievementSupport.Get(e).HasPublishedSet)==5);
            Check(AchievementSupport.Get(ps1).GameId==11348&&AchievementSupport.Get(catalog.Editions.First(e=>e.GameId=="acff"&&e.Platform=="ps2")).GameId==27005);
            Check(combos.Where(e=>e.Platform=="ps3"&&e.GameId is "ac4" or "acfa").All(e=>!AchievementSupport.Get(e).HasPublishedSet));
            Check(combos.Where(e=>e.Platform=="x360").All(e=>AchievementSupport.Get(e).Provider=="Xenia"));
        });
        test("RetroAchievements defaults enable native backends without Hardcore and preserve explicit global or game opt-outs",()=>{
            var paths=Paths();var prefs=AchievementPreferences.Load(paths);Check(prefs.Enabled&&!prefs.Hardcore);
            foreach(var backend in new[]{"duckstation","pcsx2","ppsspp"})
            {
                var edition=catalog.Editions.First(e=>e.Platform==(backend=="duckstation"?"ps1":backend=="pcsx2"?"ps2":"psp"));var map=new Dictionary<string,string>();AchievementPreferences.Apply(map,backend,edition,paths,false);
                var enable=backend=="ppsspp"?"Achievements/AchievementsEnable":backend=="duckstation"?"Cheevos/Enabled":"Achievements/Enabled";
                var hard=backend=="ppsspp"?"Achievements/AchievementsChallengeMode":backend=="duckstation"?"Cheevos/ChallengeMode":"Achievements/ChallengeMode";
                Check(map[enable]=="true"&&map[hard]=="false");AchievementPreferences.Apply(map,backend,edition,paths,true);Check(map[enable]=="false");
                prefs.GameEnabled[edition.Id]=false;prefs.Save(paths);AchievementPreferences.Apply(map,backend,edition,paths,false);Check(map[enable]=="false");prefs.GameEnabled.Clear();
            }
            prefs.GameHardcore[ps1.Id]=true;prefs.Save(paths);Check(AchievementPreferences.Load(paths).Challenge(ps1));prefs.Enabled=false;prefs.Save(paths);Check(!AchievementPreferences.Load(paths).Tracking(ps1));
        });
        test("DuckStation portable token encoding matches an independent AES test vector",()=>{
            // Vector generated with Python hashlib + cryptography AES-CBC from the official stretching/padding contract.
            Check(NativeAchievementAccounts.EncodeDuckToken("0123456789ABCDEF","FixtureUser")=="AkDZmfaLPgsWQir1+cN0DA==");
            var encoded=NativeAchievementAccounts.EncodeDuckToken(Token,"FixtureUser");Check(encoded!=Token&&NativeAchievementAccounts.DecodeDuckToken(encoded,"FixtureUser")==Token);
        });
        test("Native sign-in is Windows-protected, handles null metadata, and tombstone-first logout cannot resurrect legacy credentials",()=>{
            if(!OperatingSystem.IsWindows())return;var paths=Paths();NativeAchievementAccounts.Save(paths,Account());Check(NativeAchievementAccounts.Load(paths)?.Token==Token);
            var file=paths.Managed("config/private-achievements/native-ra.bin");Check(!Encoding.UTF8.GetString(File.ReadAllBytes(file)).Contains(Token));
            Reject(()=>NativeAchievementAccounts.Validate(new(){Username="FixtureUser",Token=null!}));
            JsonData.WriteAtomic(paths.Managed("config/private-achievements/signed-out.json"),new{signedOut=true});Check(NativeAchievementAccounts.Load(paths)==null);
            NativeAchievementAccounts.Save(paths,Account());var home=paths.Managed("runtime","sessions",ps1.Id,"fixture");Directory.CreateDirectory(home);File.WriteAllText(Path.Combine(home,"settings.ini"),"[Cheevos]\nUsername=FixtureUser\nToken="+NativeAchievementAccounts.EncodeDuckToken(Token,"FixtureUser"));EmulatorAccounts.Capture(paths,home,"duckstation");
            var pspToken=paths.Managed("saves","fixture","ppsspp","live","PSP","SYSTEM","ppsspp_retroachievements.dat");Directory.CreateDirectory(Path.GetDirectoryName(pspToken)!);File.WriteAllText(pspToken,Token);
            NativeAchievementAccounts.SignOut(paths);Check(NativeAchievementAccounts.Load(paths)==null&&!File.Exists(file)&&!File.Exists(paths.Managed("config/private-achievements/duckstation.bin"))&&!File.Exists(pspToken)&&!File.ReadAllText(Path.Combine(home,"settings.ini")).Contains(NativeAchievementAccounts.EncodeDuckToken(Token,"FixtureUser")));
        });
        test("Each native backend receives its documented token format and cleanup removes temporary secrets without discarding unrelated settings",()=>{
            if(!OperatingSystem.IsWindows())return;var paths=Paths();NativeAchievementAccounts.Save(paths,Account());
            foreach(var backend in new[]{"duckstation","pcsx2","ppsspp"})
            {
                var home=paths.Managed("runtime","sessions",ps1.Id,backend);var save=paths.Managed("saves","fixture",backend,"live");Directory.CreateDirectory(home);Directory.CreateDirectory(save);
                var section=backend=="duckstation"?"Cheevos":"Achievements";var merged=NativeAchievementAccounts.MergeConfig(paths,backend,ps1,"["+section+"]\nToken=old\nUsername=old\n[Other]\nKeep=yes\n["+section+"]\nToken=duplicate\n");
                var file=Path.Combine(home,EmulatorAccounts.ConfigFile(backend)!);Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllText(file,merged);NativeAchievementAccounts.WriteSessionSecrets(paths,backend,home,save);
                if(backend=="duckstation")Check(merged.Contains("Token="+NativeAchievementAccounts.EncodeDuckToken(Token,"FixtureUser"))&&!merged.Contains(Token)&&merged.Split('\n').Count(l=>l.StartsWith("Token="))==1);
                if(backend=="pcsx2")Check(File.ReadAllText(Path.Combine(home,"inis","secrets.ini")).Contains("Token="+Token)&&!merged.Contains(Token));
                if(backend=="ppsspp")Check(File.ReadAllText(Path.Combine(save,"PSP","SYSTEM","ppsspp_retroachievements.dat"))==Token&&merged.Contains("AchievementsUserName=FixtureUser")&&!merged.Contains(Token));
                NativeAchievementAccounts.CleanSessionSecrets(paths,backend,home,save);Check(File.ReadAllText(file).Contains("Keep=yes")&&!File.ReadAllText(file).Contains(Token));
                if(backend=="pcsx2")Check(!File.ReadAllText(Path.Combine(home,"inis","secrets.ini")).Contains(Token));if(backend=="ppsspp")Check(!File.Exists(Path.Combine(save,"PSP","SYSTEM","ppsspp_retroachievements.dat")));
            }
            Reject(()=>NativeAchievementAccounts.WriteSessionSecrets(paths,"pcsx2",Path.Combine(scratch,"outside-private"),paths.Managed("saves")));
        });
        asyncTest("Native sign-in posts credentials only to the fixed official endpoint and stores no password",async()=>{
            if(!OperatingSystem.IsWindows())return;var paths=Paths();var password="fixture &password=not-real";var seen=false;
            using var http=new HttpClient(new NativeFake(async(request,ct)=>{
                Check(request.Method==HttpMethod.Post&&request.RequestUri?.AbsoluteUri=="https://retroachievements.org/dorequest.php"&&request.RequestUri.Query=="");var body=await request.Content!.ReadAsStringAsync(ct);
                Check(body.Contains("r=login2")&&body.Contains("u=FixtureUser")&&Uri.UnescapeDataString(body.Replace('+',' ')).Contains(password));seen=true;
                return Reply("{\"Success\":true,\"User\":\"FixtureUser\",\"Token\":\""+Token+"\"}");}));
            using var native=new NativeRetroClient(paths,http);await native.SignInAsync("FixtureUser",password,false);Check(seen&&NativeAchievementAccounts.Load(paths)?.Username=="FixtureUser");Check(SafeFiles.Files(paths.Root).All(f=>!Encoding.UTF8.GetString(File.ReadAllBytes(f)).Contains(password)));
        });
        asyncTest("Offline, redirects, oversized or malformed native responses fail without credential-bearing transport errors",async()=>{
            var paths=Paths();var secret="SENSITIVE_TEST_PASSWORD";using(var http=new HttpClient(new NativeFake((_,_)=>throw new HttpRequestException("https://server/?p="+secret))))
            {using var native=new NativeRetroClient(paths,http);await Rejected(()=>native.SignInAsync("FixtureUser",secret,false),secret);await Rejected(()=>native.SignInAsync("FixtureUser",secret,true),secret);}
            foreach(var status in new[]{HttpStatusCode.Found,HttpStatusCode.TooManyRequests})
            {using var http=new HttpClient(new NativeFake((_,_)=>Task.FromResult(new HttpResponseMessage(status))));using var native=new NativeRetroClient(paths,http);await Rejected(()=>native.SignInAsync("FixtureUser",secret,false),secret);}
            foreach(var body in new[]{"{}","{\"Success\":true,\"User\":\"FixtureUser\",\"Token\":null}",new string('x',65537)})
            {using var http=new HttpClient(new NativeFake((_,_)=>Task.FromResult(Reply(body))));using var native=new NativeRetroClient(paths,http);await Rejected(()=>native.SignInAsync("FixtureUser",secret,false),secret);}
            using var canceled=new CancellationTokenSource();canceled.Cancel();using var noHttp=new HttpClient(new NativeFake((_,ct)=>Task.FromCanceled<HttpResponseMessage>(ct)));using var noNative=new NativeRetroClient(paths,noHttp);
            try{await noNative.SignInAsync("FixtureUser",secret,false,canceled.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
            Check(NativeAchievementAccounts.Load(paths)==null);
        });
        test("Native provider metadata validates game and console, filters unpublished definitions and does not invent unlock dates",()=>{
            var patch=Patch(11348,12);var parsed=AchievementService.ParseNative(ps1,"FixtureUser",11348,Encoding.UTF8.GetBytes(patch),Unlocks(1),Unlocks(2));Check(parsed.Items.Count==2&&parsed.Items.All(i=>i.Unlocked)&&!parsed.Items[0].Hardcore&&parsed.Items[1].Hardcore&&parsed.Items.All(i=>i.EarnedAt==null)&&parsed.Items[0].ProviderIconId==null);
            Reject(()=>AchievementService.ParseNative(ps1,"FixtureUser",11348,Encoding.UTF8.GetBytes(Patch(3316,21)),Unlocks(),Unlocks()));
            Reject(()=>AchievementService.ParseNative(ps1,"FixtureUser",11348,Encoding.UTF8.GetBytes(patch.Replace("\"Points\":5","\"Points\":-1")),Unlocks(),Unlocks()));
        });
        asyncTest("Refreshing restored progress preserves earned unlocks and Hardcore separately within one account, never across accounts",async()=>{
            if(!OperatingSystem.IsWindows())return;var paths=Paths();NativeAchievementAccounts.Save(paths,Account());var phase=0;
            using var http=new HttpClient(new NativeFake(async(request,ct)=>{Check(request.Method==HttpMethod.Post&&request.RequestUri!.Query=="");var body=await request.Content!.ReadAsStringAsync(ct);Check(body.Contains("t="+Token));return body.Contains("r=patch")?Reply(Patch(11348,12)):Reply(Encoding.UTF8.GetString(phase==0?Unlocks(1):Unlocks()));}));
            var service=new AchievementService(paths);var first=await service.SyncNativeAsync(ps1,false,nativeClient:http);Check(first.Items[0].Unlocked&&first.Items[0].Hardcore);
            phase=1;var restored=await service.SyncNativeAsync(ps1,false,nativeClient:http);Check(restored.Items[0].Unlocked&&restored.Items[0].Hardcore);
            NativeAchievementAccounts.Save(paths,Account("OtherFixtureUser"));var other=await service.SyncNativeAsync(ps1,false,nativeClient:http);Check(other.Items.All(i=>!i.Unlocked)&&service.Cached(ps1.Id).Count==2);
            var stamped=first with{Items=[first.Items[0] with{EarnedAt="2020-01-01",Hidden=true,Title="Revealed goal"}]};var hidden=first with{Items=[first.Items[0] with{Unlocked=false,Hardcore=false,EarnedAt=null,Hidden=true,Title="Secret achievement"}]};var union=AchievementService.MergeEarned(hidden,stamped);Check(union.Items[0].Title=="Revealed goal"&&union.Items[0].EarnedAt=="2020-01-01");
        });
        test("Cached achievement JSON skips null fields and applies a cumulative read budget",()=>{
            var paths=Paths();var folder=paths.Managed("achievements",ps1.Id);Directory.CreateDirectory(folder);var entry=new AchievementEntry("1","Fixture","Requirement",5,false,null);var snapshot=new AchievementSnapshot(ps1.Id,"RetroAchievements","FixtureUser","11348",DateTimeOffset.UtcNow,[entry]);
            JsonData.WriteAtomic(Path.Combine(folder,"good.json"),snapshot);JsonData.WriteAtomic(Path.Combine(folder,"null-title.json"),snapshot with{Items=[entry with{Title=null!}]});JsonData.WriteAtomic(Path.Combine(folder,"null-account.json"),snapshot with{Account=null!});JsonData.WriteAtomic(Path.Combine(folder,"null-id.json"),snapshot with{Items=[entry with{Id=null!}]});Check(new AchievementService(paths).Cached(ps1.Id).Count==1&&File.Exists(Path.Combine(folder,"null-title.json")));
            var large=snapshot with{Items=Enumerable.Range(0,220).Select(i=>entry with{Id=i.ToString(),Description=new string('r',8192)}).ToList()};for(var n=0;n<12;n++)JsonData.WriteAtomic(Path.Combine(folder,"large-"+n+".json"),large with{Account="Fixture "+n});
            var cached=new AchievementService(paths).Cached(ps1.Id);Check(cached.Count<13&&cached.Sum(s=>JsonSerializer.SerializeToUtf8Bytes(s,JsonData.Options).Length)<=16*1024*1024);
        });
        test("PS3 multi-game imported profiles cannot attribute another trophy set when disc metadata identifies the current game",()=>{
            var paths=Paths();var edition=catalog.Editions.First(e=>e.GameId=="acvd"&&e.Platform=="ps3");var disc=paths.Games("disc");Directory.CreateDirectory(Path.Combine(disc,"PS3_GAME","TROPDIR","NPWR_FIXTURE_00"));
            var home=Path.Combine(new SaveService(paths).Live(edition.SaveFamily,"rpcs3"),"dev_hdd0","home","00000001","trophy");foreach(var set in new[]{"NPWR_FIXTURE_00","NPWR_OTHER_00"}){var folder=Path.Combine(home,set);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"TROPCONF.SFM"),"<trophyconf><trophy id='0' ttype='B'><name>Fixture</name></trophy></trophyconf>");}
            var read=new AchievementService(paths).ReadLocal(edition,"rpcs3",disc);Check(read.Count==1&&read[0].SetId=="NPWR_FIXTURE_00");
        });
        test("Malformed achievement JSON consumes the cumulative read budget and remains preserved",()=>{
            var paths=Paths();var folder=paths.Managed("achievements",ps1.Id);Directory.CreateDirectory(folder);const int size=4*1024*1024;
            var malformed="{"+new string(' ',size-1);
            for(var n=0;n<9;n++)File.WriteAllText(Path.Combine(folder,"record-"+n+".json"),malformed,new UTF8Encoding(false));
            // Place the usable record last in the directory's observed enumeration,
            // without assuming a platform-specific filename enumeration order.
            var order=Directory.EnumerateFiles(folder,"*.json").ToArray();var last=order[^1];
            var snapshot=new AchievementSnapshot(ps1.Id,"RetroAchievements","FixtureUser","11348",DateTimeOffset.UtcNow,[]);var json=JsonSerializer.Serialize(snapshot,JsonData.Options);
            File.WriteAllText(last,json+new string(' ',size-Encoding.UTF8.GetByteCount(json)),new UTF8Encoding(false));
            Check(Directory.EnumerateFiles(folder,"*.json").SequenceEqual(order));
            Check(new AchievementService(paths).Cached(ps1.Id).Count==0&&order.All(File.Exists));
            foreach(var file in order.Where(f=>f!=last))File.Delete(file);
            Check(new AchievementService(paths).Cached(ps1.Id).Single().Account=="FixtureUser");
        });
        asyncTest("A failed native runtime start scrubs session credentials and never writes them into launch diagnostics",async()=>{
            if(!OperatingSystem.IsWindows())return;var paths=Paths();NativeAchievementAccounts.Save(paths,Account());var edition=catalog.Editions.First(e=>e.Platform=="psp");var source=paths.Managed("runtime","fixture");Directory.CreateDirectory(source);var exe=Path.Combine(source,"PPSSPPWindows64.exe");await File.WriteAllTextAsync(exe,"invalid synthetic executable");var game=paths.Games("fixture.iso");await File.WriteAllTextAsync(game,"fixture");
            var state=new HubState{Builds=[new("fixture","ppsspp","1.20.4",paths.Portable(source),Path.GetFileName(exe),"",await SafeFiles.HashAsync(exe),"fixture",DateTimeOffset.UtcNow)],Media=[new("media",paths.Portable(game),"iso",edition.Id,"",MediaConfidence.UnknownRevision,"fixture",[],DateTimeOffset.UtcNow,false)]};state.Selections[edition.Id]=new(){EditionId=edition.Id,MediaId="media",BuildId="fixture"};var plan=new LaunchPlanner(paths,catalog).Resolve(edition.Id,state);var launcher=new LaunchService(paths,catalog);
            await Rejected(()=>launcher.LaunchAsync(plan,state),Token);Check(!launcher.IsRunning&&launcher.ActiveProcessId==0&&!File.Exists(Path.Combine(plan.SaveDirectory,"PSP","SYSTEM","ppsspp_retroachievements.dat"))&&!JsonSerializer.Serialize(plan,JsonData.Options).Contains(Token));
            Check(File.Exists(paths.Managed("logs","last-launch.json"))&&!File.ReadAllText(paths.Managed("logs","last-launch.json")).Contains(Token));
        });
    }
    private static string Patch(int id,int console)=>"{\"Success\":true,\"PatchData\":{\"ID\":"+id+",\"ConsoleID\":"+console+",\"Title\":\"Fixture game\",\"Achievements\":[{\"ID\":1,\"Title\":\"Fixture goal\",\"Description\":\"Provider condition\",\"Flags\":3,\"Points\":5,\"BadgeName\":\"../invalid\"},{\"ID\":2,\"Title\":\"Other goal\",\"Description\":\"Other condition\",\"Flags\":3,\"Points\":10},{\"ID\":3,\"Title\":\"Unpublished\",\"Description\":\"Test\",\"Flags\":5,\"Points\":1}]}}";
    private static byte[] Unlocks(params int[] ids)=>Encoding.UTF8.GetBytes("{\"Success\":true,\"UserUnlocks\":["+string.Join(',',ids)+"]}");
    private static HttpResponseMessage Reply(string json)=>new(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")};
    private sealed class NativeFake(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>send(request,ct);}
}
