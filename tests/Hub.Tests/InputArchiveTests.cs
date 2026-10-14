using System.Buffers.Binary;
using System.IO.Compression;
using Hub.Core;

static class InputArchiveTests
{
    public static void Register(Action<string,Action> test, Action<string,Func<Task>> asyncTest, string scratch, Catalog catalog)
    {
        void Check(bool ok) { if(!ok) throw new Exception("Input/archive regression failed"); }
        test("Xenia retains a local save identity across regenerated settings and build switches",()=>{
            var root=Path.Combine(scratch,"xenia-profiles");Directory.CreateDirectory(root);
            var config=Path.Combine(root,"settings.toml");
            void Account(string id){var file=Path.Combine(root,id,"FFFE07D1","00010000",id,"Account");Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllText(file,"synthetic profile");}
            const string first="E030000000000001",second="E030000000000002";
            Check(XeniaProfiles.SelectLocal(root,config)==null);
            Account(first);Check(XeniaProfiles.SelectLocal(root,config)==first);
            Account(second);Check(XeniaProfiles.SelectLocal(root,config)==null);
            File.WriteAllText(config,"[Profiles]\nlogged_profile_slot_0_xuid = \""+second.ToLowerInvariant()+"\" # native selection\n");
            Check(XeniaProfiles.SelectLocal(root,config)==second);
            File.WriteAllText(config,"[Other]\nlogged_profile_slot_0_xuid = \""+second+"\"\n");
            Check(XeniaProfiles.SelectLocal(root,config)==null);
            File.WriteAllText(config,"[Profiles]\nlogged_profile_slot_0_xuid = \"../../escape\"\n");
            Check(XeniaProfiles.SelectLocal(root,config)==null);
        });
        test("RPCS3 keyboard is player one and all XInput slots can hot-plug as native co-pilots",()=>{
            var input=InputDefaults.RpcS3(true,[]); var yaml=LaunchPlanner.Yaml(input);
            Check(input["Player 1 Input/Handler"]=="Keyboard" && input["Player 1 Input/Config/Start"]=="Return");
            for(var i=0;i<4;i++) Check(input[$"Player {i+2} Input/Device"]==$"XInput Pad #{i+1}" && input[$"Player {i+2} Input/Config/Device Class Type"]=="40964");
            Check(yaml.Contains("  \"Config\":") && yaml.Contains("    \"Start\": \"Return\""));
            Check(!yaml.Contains("Config/Start"));
            Check(InputDefaults.RpcS3(false,[2])["Player 1 Input/Device"]=="XInput Pad #3");
            Check(InputDefaults.RpcS3(false,[])["Player 1 Input/Handler"]=="Keyboard");
        });
        test("Every catalog edition resolves keyboard defaults and suppresses routine emulator setup",()=>{
            var paths=new HubPaths(Path.Combine(scratch,"all-inputs"));
            foreach(var edition in catalog.Editions)
            {
                var backend=catalog.Backends.Single(b=>b.Id==catalog.Profiles.Single(p=>p.Id==edition.PreferredProfile).BackendId);
                var executable=paths.Managed("runtime",backend.Id,backend.Executable);Directory.CreateDirectory(Path.GetDirectoryName(executable)!);File.WriteAllText(executable,"fixture");
                var source=paths.Managed("games",edition.Id); Directory.CreateDirectory(source);
                var format=edition.Formats.Contains("ps3-folder")?"ps3-folder":edition.Formats.First();
                if(format=="ps3-folder"){Directory.CreateDirectory(Path.Combine(source,"USRDIR"));File.WriteAllText(Path.Combine(source,"USRDIR","EBOOT.BIN"),"fixture");}
                else {source=Path.Combine(source,"fixture."+format);File.WriteAllText(source,"fixture");}
                var firmware=paths.Managed("firmware","fixture.bin");File.WriteAllText(firmware,"fixture");
                var media=new MediaRecord("media",source,format,edition.Id,"",MediaConfidence.RecognizedUnverified,"",[],DateTimeOffset.UtcNow,false);
                // Synthetic future version exercises the hot-plug schema independently of the known 20250 game regression.
                var build=new InstalledBuild("build",backend.Id,backend.Id=="rpcs3"?"0.0.43-20251":backend.Recommended!.Version,Path.GetDirectoryName(executable)!,Path.GetFileName(executable),"","","fixture",DateTimeOffset.UtcNow);
                var state=new HubState {Media=[media],Builds=[build],Firmware=[new("firmware",backend.FirmwareClass,firmware,"","fixture",false,edition.Region)]};
                state.Selections[edition.Id]=new(){EditionId=edition.Id,MediaId=media.Id,BuildId=build.Id,Fullscreen=false};
                var plan=new LaunchPlanner(paths,catalog).Resolve(edition.Id,state);var text=string.Join('\n',plan.ConfigFiles.Values);
                switch(backend.Id)
                {
                    case "rpcs3":
                        Check(text.Contains("infoBoxEnabledWelcome=false") && text.Contains("checkUpdateStart=false") && text.Contains("\"Player 1 Input\":") && text.Contains("\"Keyboard\"") && text.Contains("40964"));
                        Check(text.Contains("\"Start games in fullscreen mode\": false") && !plan.Arguments.Contains("--fullscreen"));
                        state.Selections[edition.Id].Fullscreen=true;
                        var full=new LaunchPlanner(paths,catalog).Resolve(edition.Id,state);
                        Check(full.Arguments.Contains("--fullscreen") && string.Join('\n',full.ConfigFiles.Values).Contains("\"Start games in fullscreen mode\": true"));
                        break;
                    case "duckstation": case "pcsx2":
                        var sdlCross=backend.Id=="pcsx2"?"FaceSouth":"A";
                        Check(text.Contains("Start=Keyboard/Return") && text.Contains("Cross=Keyboard/X") && text.Contains("Cross=SDL-0/"+sdlCross) && text.Contains("SetupWizardIncomplete=false") && plan.Arguments.Contains("-nogui"));
                        // Regression from the real AC1 boot: current DuckStation reads Pad1,
                        // so bindings in only Controller1 silently leave the game uncontrollable.
                        var pad=text.Split("[Pad1]",StringSplitOptions.None).Skip(1).Select(s=>s.Split('[')[0]).ToArray();
                        Check(pad.Any(s=>s.Contains("Cross=SDL-0/"+sdlCross)) && pad.Any(s=>s.Contains("Cross=Keyboard/X")));
                        Check(text.Contains("XInput=true"));
                        if(backend.Id=="pcsx2") Check(plan.Arguments.Contains("-portable") && !plan.Arguments.Contains("-datapath")
                            && plan.ConfigFiles.ContainsKey(Path.Combine(plan.WorkingDirectory,"portable.txt")));
                        for(var slot=0;slot<4;slot++) Check(pad.Any(s=>s.Contains($"Cross=XInput-{slot}/A")));
                        if(backend.Id=="duckstation")
                        {
                            JsonData.WriteAtomic(paths.Managed("config","controls",edition.Id,"controller-overrides.json"),new Dictionary<string,string>{{"Controller1/Cross","SDL-0/B"}});
                            var overridden=string.Join('\n',new LaunchPlanner(paths,catalog).Resolve(edition.Id,state).ConfigFiles.Values);
                            Check(overridden.Split("[Pad1]").Skip(1).Any(s=>s.Split('[')[0].Contains("Cross=SDL-0/B")));
                            Check(overridden.Split("[Pad1]").Skip(1).Any(s=>s.Split('[')[0].Contains("Cross=XInput-0/B")));
                        }
                        break;
                    case "ppsspp": Check(plan.Arguments.Any(a=>a.StartsWith("--controlconfig=")) && text.Contains("CheckForNewVersion=false"));break;
                    case "xenia":
                        Check(text.Contains("keyboard_mode = 1") && text.Contains("keybind_start = \"0x0D\"") && text.Contains("hid = \"any\""));
                        Check(plan.Arguments.Contains("--config=" + Path.Combine(plan.WorkingDirectory,"xenia-canary.config.toml")));
                        if(edition.Id=="acv-x360-4e4d084f-us")
                        {
                            Check(text.Contains("protect_zero = false") && text.Contains("readback_resolve = \"full\""));
                            Check(new LaunchPlanner(paths,catalog).Diagnose(edition.Id,state).Single(d=>d.Name=="Compatibility").Message.Contains("later missions remain untested"));
                            Check(!text.Contains("framerate_limit = 120"));
                            state.Selections[edition.Id].EnhancementId=edition.PreferredProfile+"-60fps-experimental";
                            var faster=string.Join('\n',new LaunchPlanner(paths,catalog).Resolve(edition.Id,state).ConfigFiles.Values);
                            Check(faster.Contains("readback_resolve = \"full\"") && faster.Contains("vsync = false") && faster.Contains("framerate_limit = 120"));
                            state.Selections[edition.Id].EnhancementId=null;
                            Check(!string.Join('\n',new LaunchPlanner(paths,catalog).Resolve(edition.Id,state).ConfigFiles.Values).Contains("framerate_limit = 120"));
                        }
                        var identity="0000000000001234";
                        var account=Path.Combine(plan.SaveDirectory,identity,"FFFE07D1","00010000",identity,"Account");
                        Directory.CreateDirectory(Path.GetDirectoryName(account)!);File.WriteAllText(account,"fixture");
                        var profilePlan=new LaunchPlanner(paths,catalog).Resolve(edition.Id,state);
                        Check(string.Join('\n',profilePlan.ConfigFiles.Values).Contains("logged_profile_slot_0_xuid = \"0000000000001234\""));
                        break;
                }
            }
        });
        asyncTest("ZIP import identifies PS3 ISO, retains originals and publishes one relocatable managed copy",async()=>{
            var root=new HubPaths(Path.Combine(scratch,"zip-import"));var state=new HubState();var source=Path.Combine(scratch,"archive-source");Directory.CreateDirectory(source);
            ImportTests.Ps3Iso(Path.Combine(source,"game.iso"));var zip=Path.Combine(scratch,"ac4-fixture.zip");ZipFile.CreateFromDirectory(source,zip);var hash=await SafeFiles.HashAsync(zip);
            var media=new MediaService(root,catalog);
            using(var batch=await new MediaArchiveService(root,catalog).PrepareAsync([media.Probe(zip)],state))
            {
                var candidate=batch.Candidates.Single();Check(candidate.EditionId=="ac4-ps3-bles00039" && candidate.ArchiveSource==Path.GetFileName(zip));
                var added=await media.RegisterAsync(candidate,candidate.EditionId,true,state);state.Media.Add(added);
                Check(added.Managed && !Path.IsPathRooted(added.Path) && root.Resolve(added.Path)==candidate.Path);
            }
            using(var again=await new MediaArchiveService(root,catalog).PrepareAsync([media.Probe(zip)],state))
            {var c=again.Candidates.Single();var same=await media.RegisterAsync(c,c.EditionId,false,state);Check(same.Id==state.Media[0].Id);}
            Check(await SafeFiles.HashAsync(zip)==hash && File.Exists(root.Resolve(state.Media[0].Path)));
        });
        asyncTest("Real 7z fixture unpacks and cancelling review removes only new unregistered files",async()=>{
            var root=new HubPaths(Path.Combine(scratch,"seven-import"));var media=new MediaService(root,catalog);var state=new HubState();
            var fixture=Path.Combine(AppContext.BaseDirectory,"fixtures","synthetic-media.7z");
            var statuses=new List<ProgressInfo>();
            string extracted;
            using(var batch=await new MediaArchiveService(root,catalog).PrepareAsync([media.Probe(fixture)],state,new InlineProgress(statuses.Add)))
            {var c=batch.Candidates.Single();Check(c.Format=="chd" && c.ArchiveSource!=null);extracted=c.Path;Check(File.Exists(extracted));}
            Check(!File.Exists(extracted) && File.Exists(fixture));
            Check(statuses.Any(p=>p.Message.StartsWith("Checking archive") && p.Completed==new FileInfo(fixture).Length));
            Check(File.Exists(root.Managed("logs","last-archive-import.json")));
            if(OperatingSystem.IsWindows() && File.Exists(Path.Combine(AppContext.BaseDirectory,"third-party","7zip","7zr.exe")))
                Check(statuses.Any(p=>p.Message.Contains("native 7-Zip")));
        });
        asyncTest("7z Unix symlink/device and Windows reparse attributes are rejected before native writes",async()=>{
            var fixture=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"fixtures","synthetic-media.7z"));
            // Original synthetic fixture has one uncompressed header; replace only its WinAttrib
            // property, and recompute both 7z header CRCs so a valid header exercises attribute checks.
            foreach(var attributes in new uint[]{0xa1ff0020,0x61ff0020,0x00000420})
            {
                var bytes=fixture.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0xc5,4),attributes);
                var offset=checked(32+(int)BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(12,8)));
                var length=checked((int)BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(20,8)));
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28,4),Crc32(bytes.AsSpan(offset,length)));
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8,4),Crc32(bytes.AsSpan(12,20)));
                var source=Path.Combine(scratch,"attributes-"+attributes.ToString("x")+".7z");File.WriteAllBytes(source,bytes);
                var root=new HubPaths(Path.Combine(scratch,"unsafe-attributes-"+attributes.ToString("x")));
                using var batch=await new MediaArchiveService(root,catalog).PrepareAsync([new MediaService(root,catalog).Probe(source)],new());
                Check(batch.Candidates.Single().Warnings.Any(w=>w.Contains("linked")));
                Check(!Directory.EnumerateFiles(root.Managed("games"),"*",SearchOption.AllDirectories).Any());
            }
        });
        asyncTest("Cancelling native 7z stops its owned child and removes unregistered staging",async()=>{
            if(!OperatingSystem.IsWindows() || !File.Exists(Path.Combine(AppContext.BaseDirectory,"third-party","7zip","7zr.exe"))) return;
            var source=Path.Combine(AppContext.BaseDirectory,"fixtures","synthetic-media.7z");var original=await SafeFiles.HashAsync(source);
            var root=new HubPaths(Path.Combine(scratch,"cancel-native-seven"));using var cancel=new CancellationTokenSource();
            var progress=new InlineProgress(p=>{if(p.Message.Contains("native 7-Zip"))cancel.Cancel();});
            try { using var batch=await new MediaArchiveService(root,catalog).PrepareAsync([new MediaService(root,catalog).Probe(source)],new(),progress,cancel.Token);throw new Exception("Native cancellation ignored"); }
            catch(OperationCanceledException){}
            Check(!Directory.EnumerateFiles(root.Managed("games"),"*",SearchOption.AllDirectories).Any());
            Check(await SafeFiles.HashAsync(source)==original);
        });
        asyncTest("Archive traversal, duplicate names and damaged files fail without losing valid scan results",async()=>{
            var root=new HubPaths(Path.Combine(scratch,"bad-archives"));var media=new MediaService(root,catalog);
            var traversal=Path.Combine(scratch,"media-traversal.zip");using(var a=ZipFile.Open(traversal,ZipArchiveMode.Create)){using var w=new StreamWriter(a.CreateEntry("../escape.chd").Open());w.Write("bad");}
            var duplicate=Path.Combine(scratch,"media-duplicate.zip");using(var a=ZipFile.Open(duplicate,ZipArchiveMode.Create)){a.CreateEntry("a.chd");a.CreateEntry("A.chd");}
            var broken=Path.Combine(scratch,"broken.7z");File.WriteAllText(broken,"not an archive");var valid=Path.Combine(scratch,"normal.chd");File.WriteAllText(valid,"fixture");
            using(var batch=await new MediaArchiveService(root,catalog).PrepareAsync(new[]{traversal,duplicate,broken,valid}.Select(media.Probe),new()))
            {Check(batch.Candidates.Count(c=>c.Format=="inaccessible")==3 && batch.Candidates.Any(c=>c.Path==valid));}
            Check(!Directory.EnumerateFiles(root.Managed("games"),"*",SearchOption.AllDirectories).Any());
        });
        asyncTest("Archive expansion budgets and mid-stream cancellation leave no published payload",async()=>{
            var zip=Path.Combine(scratch,"cancel-archive.zip");using(var a=ZipFile.Open(zip,ZipArchiveMode.Create)){using var s=a.CreateEntry("large.chd").Open();s.Write(new byte[10*1024*1024]);}
            try{await ArchiveInstaller.ExtractAsync(zip,Path.Combine(scratch,"too-large"),maxBytes:1024);throw new Exception("Budget ignored");}catch(HubException){}
            var root=new HubPaths(Path.Combine(scratch,"cancel-archive"));using var ct=new CancellationTokenSource();
            var progress=new InlineProgress(p=>{if(p.Message.StartsWith("Unpacking") && p.Completed>0)ct.Cancel();});
            try{using var batch=await new MediaArchiveService(root,catalog).PrepareAsync([new MediaService(root,catalog).Probe(zip)],new(),progress,ct.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
            Check(!Directory.EnumerateFiles(root.Managed("games"),"*",SearchOption.AllDirectories).Any());
        });
        asyncTest("Password-protected 7z reports a useful skip without leaving an extracted copy",async()=>{
            var root=new HubPaths(Path.Combine(scratch,"encrypted-archive"));
            var file=Path.Combine(AppContext.BaseDirectory,"fixtures","password-media.7z");
            using(var batch=await new MediaArchiveService(root,catalog).PrepareAsync([new MediaService(root,catalog).Probe(file)],new()))
                Check(batch.Candidates.Single().Format=="inaccessible");
            Check(!Directory.EnumerateFiles(root.Managed("games"),"*",SearchOption.AllDirectories).Any());
        });
    }
    private sealed class InlineProgress(Action<ProgressInfo> report):IProgress<ProgressInfo> { public void Report(ProgressInfo value)=>report(value); }
    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {var value=uint.MaxValue;foreach(var b in bytes){value^=b;for(var bit=0;bit<8;bit++)value=(value>>1)^((value&1)!=0?0xedb88320u:0);}return ~value;}
}
