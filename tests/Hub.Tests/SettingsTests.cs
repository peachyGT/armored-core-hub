using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Hub.Core;

static class SettingsTests
{
    public static void Register(Action<string,Action> test,Action<string,Func<Task>> asyncTest,string scratch,Catalog catalog)
    {
        void Check(bool value){if(!value)throw new Exception("Settings regression failed");}
        void Reject(Action action){try{action();}catch(HubException){return;}throw new Exception("Expected rejection");}
        test("Game storage persists separately and preserves older relative paths",()=>{
            var p=new HubPaths(Path.Combine(scratch,"external-storage-hub"));var external=Path.Combine(scratch,"external-games");p.SetGameStorage(external);
            var reload=new HubPaths(p.Root);Check(reload.Games("a.iso")==Path.Combine(external,"a.iso"));Check(reload.Resolve("games/old.iso")==Path.Combine(p.Root,"games","old.iso"));Reject(()=>p.SetGameStorage(Path.GetPathRoot(scratch)!));Reject(()=>p.Games("../escape"));
        });
        asyncTest("Archive extraction uses selected game drive; cancelled review keeps originals",async()=>{
            var p=new HubPaths(Path.Combine(scratch,"external-extract"));p.SetGameStorage(Path.Combine(scratch,"external-extract-games"));
            var iso=Path.Combine(scratch,"external-game.iso");Fixtures.Iso(iso,"SLUS_200.14");var archive=Path.Combine(scratch,"external-game.zip");using(var z=ZipFile.Open(archive,ZipArchiveMode.Create))z.CreateEntryFromFile(iso,"disc.iso");
            string payload;using(var batch=await new MediaArchiveService(p,catalog).PrepareAsync([new(archive,"archive",null,null,"",[])],new())){payload=batch.Candidates.Single().Path;Check(SafeFiles.IsWithin(p.GameStorageRoot,payload));Check(File.Exists(payload));}
            Check(!File.Exists(payload)&&File.Exists(archive)&&File.Exists(iso));
        });
        test("Graphics map scale independently and reject unsupported fractions or speed controls",()=>{
            foreach(var backend in catalog.Backends){var values=new Dictionary<string,string>();GraphicsSettings.Apply(values,backend.Id,"ac4",new(){Scale=2});Check(values[GraphicsSettings.ScaleKey(backend.Id)]==(backend.Id=="rpcs3"?"200":"2"));}
            var ps3=new Dictionary<string,string>();GraphicsSettings.Apply(ps3,"rpcs3","ac4",new(){Scale=.5,TargetFps=60});Check(ps3["Video/Resolution Scale"]=="50"&&ps3["Video/Frame limit"]=="60");
            Reject(()=>GraphicsSettings.Apply([],"pcsx2","ac2",new(){Scale=.5}));Reject(()=>GraphicsSettings.Apply([],"duckstation","ac1",new(){TargetFps=120}));Reject(()=>GraphicsSettings.Apply([],"rpcs3","ac4",new(){Scale=double.NaN}));
            var xbox=new Dictionary<string,string>();GraphicsSettings.Apply(xbox,"xenia","acv",new(){TargetFps=60});Check(xbox["GPU/framerate_limit"]=="120"&&xbox["GPU/vsync"]=="false");
        });
        test("Every preset describes its actual distinct resolution",()=>{
            foreach(var p in catalog.Profiles){var presets=catalog.Enhancements.Where(e=>e.ProfileId==p.Id).ToList();Check(presets.Select(e=>e.Benefit).Distinct().Count()==presets.Count);}
        });
        test("Custom keyboard and controller profiles remain independent",()=>{
            var c=new ControlChoice{KeyboardPreset="Custom",Keyboard=new(){{"Cross","B"}},ControllerPreset="Custom",Controller=new(){{"Cross","Y"}}};
            Check(ControlProfiles.Resolve("pcsx2",c,true)["Cross"]=="B");Check(ControlProfiles.Resolve("pcsx2",c,false)["Cross"]=="Y");c.KeyboardPreset="Default";Check(ControlProfiles.Resolve("pcsx2",c,true)["Cross"]=="X");Check(ControlProfiles.Resolve("pcsx2",c,false)["Cross"]=="Y");
            Reject(()=>ControlProfiles.Validate(new(){{"Cross","X\nInjected=value"}},true));Reject(()=>ControlProfiles.Resolve("pcsx2",new(){KeyboardPreset="Community"},true));
        });
        test("SDL custom mappings are explicit, bounded and separate from keyboard",()=>{
            const string mapping="03000000123400005678000000000000,Test Pad,a:b0,b:b1,leftx:a0,lefty:a1,platform:Windows,";
            var c=new ControlChoice{ControllerPreset="Custom",XeniaMapping=mapping,KeyboardPreset="Custom",Keyboard=new(){{"Cross","B"}}};var values=new Dictionary<string,string>();ControlProfiles.XeniaApply(values,c);
            Check(values["HID/hid"]=="sdl"&&values["HID.Key/keybind_a"]=="B");Reject(()=>ControlProfiles.ValidateSdl("not-a-guid,broken,a:b0"));
            var canonical=new ControlChoice{ControllerPreset="Custom",Controller=new(){{"Cross","Y"}}};var customValues=new Dictionary<string,string>();ControlProfiles.XeniaApply(customValues,canonical);
            Check(customValues["HID/hid"]=="sdl"&&ControlProfiles.XeniaMap(canonical).Contains("a:b3"));
        });
        test("Native achievement settings survive encryption without unrelated settings",()=>{
            var p=new HubPaths(Path.Combine(scratch,"native-accounts"));var home=p.Managed("runtime","fake");Directory.CreateDirectory(home);
            const string input="[GPU]\nResolutionScale=3\n[Cheevos]\nEnabled=true\nToken=synthetic-token-not-a-secret\nHost=invalid.example\nPassword=discard\n[Other]\nToken=discard\n";
            File.WriteAllText(Path.Combine(home,"settings.ini"),input);EmulatorAccounts.Capture(p,home,"duckstation");var restored=EmulatorAccounts.Restore(p,"duckstation");
            Check(restored.Contains("Token=synthetic-token-not-a-secret")&&!restored.Contains("discard")&&!restored.Contains("Host=")&&!restored.Contains("ResolutionScale"));
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(p.Managed("config","private-achievements","duckstation.bin"))).Contains("synthetic-token"));
        });
        test("Damaged achievement cache is retained without breaking other records",()=>{
            var p=new HubPaths(Path.Combine(scratch,"achievement-cache"));var root=p.Managed("achievements","test");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"bad.json"),"broken");
            JsonData.WriteAtomic(Path.Combine(root,"good.json"),new AchievementSnapshot("test","RPCS3","00000001","NPWR00000_00",DateTimeOffset.UtcNow,[]));
            Check(new AchievementService(p).Cached("test").Count==1&&File.Exists(Path.Combine(root,"bad.json")));
        });
        asyncTest("PNACH imports bind their CRC and reject changed payloads",async()=>{
            var p=new HubPaths(Path.Combine(scratch,"pnach-flow"));var file=Path.Combine(scratch,"pnach-source.iso");Fixtures.Iso(file,"SLUS_200.14");var service=new MediaService(p,catalog);var state=new HubState();var source=await service.RegisterAsync(service.Probe(file),"ac2-ps2-slus20014",false,state);state.Media.Add(source);
            var patchFile=Path.Combine(scratch,"0D168765.pnach");File.WriteAllText(patchFile,"[True Analogs]\npatch=1,EE,00100000,word,00000000\n");var manager=new LocalPatchService(p,catalog);var installed=await manager.ImportAsync(patchFile,source,state,true);Check(installed.Crc=="0D168765"&&installed.Analog&&state.Media.Count==1);
            var selection=new GameSelection{EditionId=source.EditionId!,MediaId=source.Id,BuildId="patch-test",LocalPatchIds=[installed.Id]};Check(manager.Selected(selection,source).Count==1);
            var buildDir=p.Managed("runtime","fixture");Directory.CreateDirectory(buildDir);File.WriteAllText(Path.Combine(buildDir,"fake.exe"),"fixture");var bios=p.Managed("firmware","bios.bin");File.WriteAllText(bios,"synthetic");
            state.Builds.Add(new("patch-test","pcsx2","2.8.2",p.Portable(buildDir),"fake.exe","","","fixture",DateTimeOffset.UtcNow));state.Firmware.Add(new("fw","ps2-bios",p.Portable(bios),"","fixture",false,"US"));state.Selections[source.EditionId!]=selection;
            var planner=new LaunchPlanner(p,catalog);var plan=planner.Resolve(source.EditionId!,state);Check(plan.ConfigFiles.Any(f=>f.Key.EndsWith("0D168765.pnach")&&f.Value.Contains("patch=1"))&&plan.ConfigFiles.Values.Any(v=>v.Contains("EnableCheats=true")&&v.Contains("Enable=True Analogs")));
            manager.SetSections(installed,[]);plan=planner.Resolve(source.EditionId!,state);Check(!plan.ConfigFiles.Values.Any(v=>v.Contains("patch=1")||v.Contains("Enable=True Analogs")));manager.SetSections(installed,["True Analogs"]);
            selection.LocalPatchIds.Clear();plan=planner.Resolve(source.EditionId!,state);Check(!plan.ConfigFiles.Keys.Any(k=>k.EndsWith(".pnach"))&&!plan.ConfigFiles.Values.Any(v=>v.Contains("EnableCheats=true")&&v.Contains("Enable=True Analogs")));
            selection.LocalPatchIds.Add(installed.Id);File.AppendAllText(p.Resolve(installed.Payload),"changed");Reject(()=>manager.Selected(selection,source));
        });
        test("PPF2 little-endian size and PPF3 block and undo records are validated",()=>{
            var source=Path.Combine(scratch,"ppf-versions.bin");var original=new byte[65536];File.WriteAllBytes(source,original);
            var ppf2=new byte[1090];"PPF20"u8.CopyTo(ppf2);ppf2[5]=1;BinaryPrimitives.WriteUInt32LittleEndian(ppf2.AsSpan(56),65536);BinaryPrimitives.WriteUInt32LittleEndian(ppf2.AsSpan(1084),10);ppf2[1088]=1;ppf2[1089]=42;Check(LocalPatchService.ReadPpf(ppf2,source).Single().Offset==10);
            var ppf3=new byte[1095];"PPF30"u8.CopyTo(ppf3);ppf3[5]=2;ppf3[57]=1;ppf3[58]=1;BinaryPrimitives.WriteUInt64LittleEndian(ppf3.AsSpan(1084),12);ppf3[1092]=1;ppf3[1093]=43;Check(LocalPatchService.ReadPpf(ppf3,source).Single().Data[0]==43);ppf3[60]=1;Reject(()=>LocalPatchService.ReadPpf(ppf3,source));
        });
        test("PBP console menu movies are extracted into bounded cache slots",()=>{
            var p=new HubPaths(Path.Combine(scratch,"pbp-movie"));var file=Path.Combine(scratch,"movie.pbp");var data=new byte[48];new byte[]{0,80,66,80}.CopyTo(data,0);for(var i=0;i<8;i++)BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8+i*4),(uint)(i<=2?40:48));"PSMFtest"u8.CopyTo(data.AsSpan(40));File.WriteAllBytes(file,data);
            var media=new MediaRecord("movie",file,"pbp",null,"fixture",MediaConfidence.UnknownRevision,"fixture",[],DateTimeOffset.UtcNow,false);var assets=new GameAssetReader(p).Read(media);Check(assets.ConsoleMovie!=null&&File.ReadAllBytes(p.Managed("artwork",assets.ConsoleMovie)).SequenceEqual("PSMFtest"u8.ToArray()));
        });
        test("RPCS3 custom bindings reach keyboard and native co-pilots",()=>{
            var values=InputDefaults.RpcS3(true,[]);ControlProfiles.RpcApply(values,new(){KeyboardPreset="Custom",Keyboard=new(){{"Cross","B"}},ControllerPreset="Custom",Controller=new(){{"Cross","Y"}}});Check(values["Player 1 Input/Config/Cross"]=="B"&&values["Player 2 Input/Config/Cross"]=="Y");
        });
        test("PPSSPP custom map keeps XInput alternatives and Xenia uses virtual keys",()=>{
            var choice=new ControlChoice{KeyboardPreset="Custom",Keyboard=new(){{"Cross","B"}}};var psp=ControlProfiles.Psp(choice);Check(psp["ControlMapping/Cross"]=="1-30,20-96,21-96,22-96,23-96");Check(psp["ControlMapping/An.Up"].Contains("20-4002"));
            var xbox=new Dictionary<string,string>();ControlProfiles.XeniaApply(xbox,choice);Check(xbox["HID.Key/keybind_a"]=="B"&&xbox["HID.Key/keybind_start"]=="0x0D");
        });
        asyncTest("PPF creates a separate hash-bound copy without changing the original",async()=>{
            var p=new HubPaths(Path.Combine(scratch,"patch-flow"));var file=Path.Combine(scratch,"patch-source.iso");Fixtures.Iso(file,"SLUS_200.14");var service=new MediaService(p,catalog);var state=new HubState();var source=await service.RegisterAsync(service.Probe(file),"ac2-ps2-slus20014",false,state);state.Media.Add(source);
            var patch=new byte[62];"PPF10"u8.CopyTo(patch);BinaryPrimitives.WriteUInt32LittleEndian(patch.AsSpan(56),100);patch[60]=1;patch[61]=42;var patchFile=Path.Combine(scratch,"synthetic.ppf");File.WriteAllBytes(patchFile,patch);
            var local=new LocalPatchService(p,catalog);var installed=await local.ImportAsync(patchFile,source,state,false);Check(state.Media.Count==2);Check(File.ReadAllBytes(file)[100]==0);var derived=state.Media.Single(m=>m.Id==installed.DerivedMediaId);Check(File.ReadAllBytes(p.Resolve(derived.Path))[100]==42);Check(await service.VerifyAsync(source));
            var choice=new GameSelection{EditionId=source.EditionId!,LocalPatchIds=[installed.Id]};Check(local.Selected(choice,source).Count==1);Reject(()=>local.Selected(choice,source with{Fingerprint=new string('a',64)}));BinaryPrimitives.WriteUInt32LittleEndian(patch.AsSpan(56),uint.MaxValue);Reject(()=>LocalPatchService.ReadPpf(patch,file));
        });
        test("GPD achievements read names, scores and unlock flags without modifying progress",()=>{
            var data=Gpd();var items=AchievementService.ReadGpd(data);Check(items.Count==1&&items[0].Title=="First sortie"&&items[0].Unlocked&&items[0].Points==10);data[8]=255;Reject(()=>AchievementService.ReadGpd(data));
        });
        test("PS3 trophy tables reject hostile extents and XML DTDs",()=>{
            var dat=new byte[192];BinaryPrimitives.WriteUInt32BigEndian(dat,0x818F54AD);BinaryPrimitives.WriteUInt32BigEndian(dat.AsSpan(8),1);BinaryPrimitives.WriteUInt32BigEndian(dat.AsSpan(48),6);BinaryPrimitives.WriteUInt32BigEndian(dat.AsSpan(60),1);BinaryPrimitives.WriteUInt64BigEndian(dat.AsSpan(64),80);BinaryPrimitives.WriteUInt32BigEndian(dat.AsSpan(100),1);
            var items=AchievementService.ReadTrophies(dat,Encoding.UTF8.GetBytes("<trophyconf><trophy id='0' ttype='P'><name>Test</name><detail>Complete</detail></trophy></trophyconf>"));Check(items.Single().Unlocked&&items.Single().Grade=="Platinum");
            var grades=AchievementService.ReadTrophies(dat,Encoding.UTF8.GetBytes("<trophyconf><trophy id='0' ttype='B'/><trophy id='1' ttype='S'/><trophy id='2' ttype='G'/><trophy id='3' ttype='P'/><trophy id='4' ttype='?'/></trophyconf>"));Check(grades.Select(i=>i.Grade).SequenceEqual(new string?[]{"Bronze","Silver","Gold","Platinum",null}));BinaryPrimitives.WriteUInt64BigEndian(dat.AsSpan(64),ulong.MaxValue);Reject(()=>AchievementService.ReadTrophies(dat,[]));
        });
        test("RetroAchievements parsing separates hardcore and rejects cross-console sets",()=>{
            var edition=catalog.Editions.First(e=>e.Platform=="ps1");var response=Encoding.UTF8.GetBytes("{\"ConsoleID\":12,\"Title\":\"Armored Core\",\"Achievements\":{\"1\":{\"Title\":\"First mission\",\"Description\":\"Finish\",\"Points\":5,\"DateEarnedHardcore\":\"2026-10-11\"}}}");var s=AchievementService.ParseRetro(edition,new("player",1),response);Check(s.Items.Single().Hardcore&&s.Items.Single().Unlocked);Reject(()=>AchievementService.ParseRetro(catalog.Editions.First(e=>e.Platform=="ps2"),new("player",1),response));
        });
    }
    private static byte[] Gpd()
    {
        var strings=Encoding.BigEndianUnicode.GetBytes("First sortie\0Complete a mission\0Finish one mission\0");var b=new byte[24+18+8+28+strings.Length];"XDBF"u8.CopyTo(b);BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(8),1);BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(12),1);BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(16),1);BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(24),1);BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(38),(uint)(28+strings.Length));BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(50),28);BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(54),1);BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(62),10);BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(66),0x20000);strings.CopyTo(b,78);return b;
    }
}
