using System.Runtime.InteropServices;
using Hub.Core;

static class CommunityControlTests
{
    public static void Register(Action<string,Action> test,Action<string,Func<Task>> asyncTest,string scratch,Catalog catalog)
    {
        void Check(bool value){if(!value)throw new Exception("Community control regression");}
        test("Every declared in-game scheme has a sourced independent Community template",()=>{
            foreach(var edition in catalog.Editions)
            foreach(var scheme in GameControlCatalog.Schemes(edition))
            {
                var profile=CommunityControlPresets.Get(edition,scheme.Id);ControlProfiles.Validate(profile.Keyboard,true);ControlProfiles.Validate(profile.Controller,false);
                Check(profile.Editions.SequenceEqual([edition.Id])&&Uri.TryCreate(profile.Source,UriKind.Absolute,out var uri)&&uri.Scheme=="https");
                Check(profile.Name.Contains("inferred")&&profile.GameScheme!=null&&CommunityControlPresets.Description(edition,scheme.Id).Contains("Menu"));
                var keyboard=new Dictionary<string,string>(profile.Keyboard);profile.Keyboard["Start"]="B";
                Check(CommunityControlPresets.Get(edition,scheme.Id).Keyboard.SequenceEqual(keyboard));
            }
        });
        test("Community layouts adapt arm weapons and mobility to each game's original buttons",()=>{
            foreach(var edition in catalog.Editions)
            foreach(var scheme in GameControlCatalog.Schemes(edition).Where(s=>s.Id!="auto"&&s.Id!="ai"))
            {
                var profile=CommunityControlPresets.Get(edition,scheme.Id);
                foreach(var(button,action)in scheme.Actions)
                {
                    if(action is "Right arm weapon" or "Fire selected weapon"||action.StartsWith("Right arm weapon /"))Check(profile.Controller[button]=="+RightTrigger"&&profile.Keyboard[button]=="R");
                    if(action.StartsWith("Left arm weapon"))Check(profile.Controller[button]=="+LeftTrigger"&&profile.Keyboard[button]=="F");
                    if(action is "Boost" or "Boost / jump" or "Jump / Boost Drive")Check(profile.Controller[button]=="LeftShoulder"&&profile.Keyboard[button]=="Space");
                    if(action is "Quick boost" or "Cycle weapon"||action.StartsWith("High boost"))Check(profile.Controller[button]=="RightShoulder");
                    if(action=="Pause")Check(profile.Keyboard[button]=="Return");
                }
            }
        });
        test("Early analog Community profile keeps both actual analog sticks and original digital movement",()=>{
            var e=catalog.Editions.Single(e=>e.Id=="ac2-ps2-slus20014");var p=CommunityControlPresets.Get(e,"analog");
            Check(p.Controller["LLeft"]=="-LeftX"&&p.Controller["RUp"]=="-RightY"&&p.Controller["L1"]=="-LeftX"&&p.Controller["L2"]=="-RightY");
            Check(p.Keyboard["LUp"]=="W"&&p.Keyboard["RRight"]=="L");
        });
        test("Formula Front AI preset preserves camera commands and does not promise manual weapons",()=>{
            foreach(var e in catalog.Editions.Where(e=>e.GameId=="acff"))
            {
                var p=CommunityControlPresets.Get(e,"ai");var description=CommunityControlPresets.Description(e,"ai");
                Check(p.Controller.SequenceEqual(ControlProfiles.Pad)&&p.Keyboard["LUp"]=="W"&&p.Keyboard["Start"]=="Return");
                Check(p.Name.Contains("AI camera")&&description.Contains("does not add manual weapons")&&p.Source.Contains("media.fromsoftware.jp"));
            }
        });
        test("Xenia generated mapping uses SDL's XInput namespace and preserves separate axis directions",()=>{
            var controller=new Dictionary<string,string>(ControlProfiles.Pad){["Cross"]="LeftShoulder",["Square"]="RightShoulder",["R1"]="X"};
            var map=XeniaControllerMappings.Create(controller);
            Check(map.StartsWith("xinput,")&&map.Contains("a:b4,")&&map.Contains("x:b5,")&&map.Contains("rightshoulder:b2,"));
            Check(map.Contains("-leftx:-a0,")&&map.Contains("+leftx:+a0,")&&map.Contains("lefttrigger:a2,"));
            var env=new Dictionary<string,string>();XeniaControllerMappings.ApplyEnvironment(env,map);Check(env["SDL_JOYSTICK_HIDAPI"]=="0"&&env["SDL_XINPUT_ENABLED"]=="1");
            var imported=new Dictionary<string,string>();XeniaControllerMappings.ApplyEnvironment(imported,"03000000123400005678000000000000,DirectInput Pad,a:b0,platform:Windows,");Check(imported.Count==0);
            XeniaControllerMappings.ApplyEnvironment(imported,"");Check(imported.Count==0);
        });
        (HubPaths paths,HubState state,Edition edition) Fixture(string name,string backendId,string? editionId=null)
        {
            var paths=new HubPaths(Path.Combine(scratch,"community-launch-"+name));
            var profile=editionId==null?catalog.Profiles.First(p=>p.BackendId==backendId&&(backendId!="xenia"||catalog.Editions.Single(e=>e.Id==p.EditionId).GameId=="acv")):
                catalog.Profiles.Single(p=>p.Id==catalog.Editions.Single(e=>e.Id==editionId).PreferredProfile);
            var edition=catalog.Editions.Single(e=>e.Id==profile.EditionId);
            var backend=catalog.Backends.Single(b=>b.Id==backendId);var release=RecommendedBuilds.For(profile,backend)!;
            var home=paths.Managed("runtime","fixture");Directory.CreateDirectory(home);File.WriteAllText(Path.Combine(home,backend.Executable),"synthetic executable");
            var format=edition.Formats.Contains("ps3-folder")?"ps3-folder":edition.Formats.First(backend.Formats.Contains);
            var file=paths.Managed("games",format=="ps3-folder"?"disc":"disc.iso");
            if(format=="ps3-folder"){Directory.CreateDirectory(Path.Combine(file,"PS3_GAME","USRDIR"));File.WriteAllText(Path.Combine(file,"PS3_GAME","USRDIR","EBOOT.BIN"),"synthetic executable");}
            else File.WriteAllText(file,"synthetic game");
            var firmware=paths.Managed("firmware","synthetic.bin");File.WriteAllText(firmware,"synthetic firmware");
            var media=new MediaRecord("media",paths.Portable(file),format,edition.Id,new string('2',64),MediaConfidence.RecognizedUnverified,"fixture",[],DateTimeOffset.UtcNow,false);
            var state=new HubState{Builds=[new("build",backend.Id,release.Version,paths.Portable(home),backend.Executable,release.Sha256,"","fixture",DateTimeOffset.UtcNow)],
                Firmware=[new("firmware",backend.FirmwareClass,paths.Portable(firmware),"","fixture",false,edition.Region)],Media=[media]};
            state.Selections[edition.Id]=new(){EditionId=edition.Id,MediaId=media.Id,Controls=new(){KeyboardPreset="Community",ControllerPreset="Community"}};
            return(paths,state,edition);
        }
        test("Every emulator launches with both Community profiles translated to its native config",()=>{
            foreach(var backend in catalog.Backends)
            {
                var(paths,state,edition)=Fixture(backend.Id,backend.Id);var plan=new LaunchPlanner(paths,catalog).Resolve(edition.Id,state);
                var text=string.Join('\n',plan.ConfigFiles.Values);
                Check(state.Selections[edition.Id].Controls.GameScheme=="auto");
                switch(backend.Id)
                {
                    case "duckstation":case "pcsx2":Check(text.Contains("Cross=Keyboard/Space")&&text.Contains("Square=XInput-0/+RightTrigger")&&text.Contains("Cross=XInput-0/LeftShoulder"));break;
                    case "ppsspp":Check(text.Contains("Cross=1-62,20-102")&&text.Contains("Square=1-46,20-4036")&&text.Contains("An.Left=1-38,20-4023"));break;
                    case "rpcs3":Check(text.Contains("\"Cross\": \"R\"")&&text.Contains("\"Square\": \"F\"")&&text.Contains("\"L2\": \"Space\""));break;
                    case "xenia":Check(text.Contains("keybind_a = \"0x20\"")&&text.Contains("a:b4,")&&text.Contains("x:b5,")&&plan.Environment["SDL_JOYSTICK_HIDAPI"]=="0");break;
                }
            }
        });
        test("Xenia custom action bindings apply independently of keyboard and preserve imported device mappings",()=>{
            var(paths,state,edition)=Fixture("xenia-custom","xenia");var choice=state.Selections[edition.Id].Controls;
            choice.ControllerPreset="Custom";choice.Controller=new(){{"Cross","Y"},{"RLeft","-LeftX"},{"RRight","+LeftX"}};
            var plan=new LaunchPlanner(paths,catalog).Resolve(edition.Id,state);var map=plan.ConfigFiles.Single(f=>f.Key.EndsWith("hub-controllerdb.txt")).Value;
            Check(map.Contains("a:b3,")&&map.Contains("-rightx:-a0,")&&map.Contains("+rightx:+a0,")&&plan.Environment["SDL_XINPUT_USE_OLD_JOYSTICK_MAPPING"]=="0");
            Check(plan.ConfigFiles.Values.Any(t=>t.Contains("keybind_a = \"0x20\"")));
            choice.XeniaMapping="03000000123400005678000000000000,DirectInput Pad,a:b0,platform:Windows,";
            plan=new LaunchPlanner(paths,catalog).Resolve(edition.Id,state);Check(plan.Environment.Count==0&&plan.ConfigFiles.Values.Contains(choice.XeniaMapping));
            choice.ControllerPreset="Default";plan=new LaunchPlanner(paths,catalog).Resolve(edition.Id,state);
            Check(plan.Environment.Count==0&&!plan.ConfigFiles.Keys.Any(k=>k.EndsWith("hub-controllerdb.txt")));
        });
        asyncTest("Enabled analog PNACH sections select the matching launch scheme without changing stored choices",async()=>{
            var(paths,state,edition)=Fixture("analog-sections","pcsx2","ac2-ps2-slus20014");var original=state.Media.Single();var file=paths.Resolve(original.Path);
            Fixtures.Iso(file,"SLUS_200.14");var service=new MediaService(paths,catalog);
            var registered=await service.RegisterAsync(service.Probe(file),edition.Id,false,new());state.Media=[registered];
            var selection=state.Selections[edition.Id];selection.MediaId=registered.Id;
            var patchFile=Path.Combine(paths.Root,"0D168765.pnach");File.WriteAllText(patchFile,"[True Analogs]\npatch=1,EE,00100000,word,00000000\n");
            var manager=new LocalPatchService(paths,catalog);var patch=await manager.ImportAsync(patchFile,registered,state,true);selection.LocalPatchIds=[patch.Id];
            var planner=new LaunchPlanner(paths,catalog);var plan=planner.Resolve(edition.Id,state);
            Check(plan.ConfigFiles.Values.Any(t=>t.Contains("LLeft=Keyboard/A")&&t.Contains("LLeft=XInput-0/-LeftX")&&t.Contains("Enable=True Analogs")));
            Check(manager.AnalogActive(manager.Selected(selection,registered))&&selection.Controls.GameScheme=="auto");
            manager.SetSections(patch,[]);plan=planner.Resolve(edition.Id,state);
            Check(!manager.AnalogActive(manager.Selected(selection,registered))&&!plan.ConfigFiles.Values.Any(t=>t.Contains("Enable=True Analogs")||t.Contains("patch=1")));
            Check(selection.Controls.GameScheme=="auto");
        });
        if(Environment.GetEnvironmentVariable("HUB_TEST_SDL3") is {} sdl&&File.Exists(sdl))
        test("Native SDL parser accepts generated controller mappings and signed half axes",()=>{
            var module=NativeLibrary.Load(sdl);
            try
            {
                var add=Marshal.GetDelegateForFunctionPointer<AddMapping>(NativeLibrary.GetExport(module,"SDL_AddGamepadMapping"));
                var error=Marshal.GetDelegateForFunctionPointer<GetError>(NativeLibrary.GetExport(module,"SDL_GetError"));
                foreach(var edition in catalog.Editions.Where(e=>e.Platform=="x360"))
                {
                    var map=XeniaControllerMappings.Create(CommunityControlPresets.Get(edition).Controller);
                    if(add(map)<0)throw new Exception("SDL rejected mapping: "+Marshal.PtrToStringUTF8(error()));
                }
                // SDL accepts unrecognized binding fragments by design. Rejection of
                // untrusted profile fields is covered by the Hub's own validation.
            }
            finally{NativeLibrary.Free(module);}
        });
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int AddMapping([MarshalAs(UnmanagedType.LPUTF8Str)]string mapping);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate nint GetError();
}
