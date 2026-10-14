using Hub.Core;

static class GameControlTests
{
    public static void Register(Action<string,Action> test,string scratch,Catalog catalog)
    {
        void Check(bool value){if(!value)throw new Exception("Game action/profile regression");}
        void Reject(Action action){try{action();}catch(HubException){return;}throw new Exception("Expected invalid profile rejection");}
        Edition Game(string id,string platform)=>catalog.Editions.First(e=>e.GameId==id&&e.Platform==platform);
        test("Every catalog title and platform declares sourced complete game actions",()=>{
            foreach(var edition in catalog.Editions)
            foreach(var scheme in GameControlCatalog.Schemes(edition))
            {
                Check(scheme.Sources.Length>0&&scheme.Sources.All(s=>Uri.TryCreate(s,UriKind.Absolute,out var uri)&&uri.Scheme=="https"));
                var backend=catalog.Profiles.First(p=>p.EditionId==edition.Id).BackendId;
                Check(ControlProfiles.Buttons(backend).ToHashSet().SetEquals(scheme.Actions.Keys));
                Check(scheme.Actions.Values.All(a=>!string.IsNullOrWhiteSpace(a)));
            }
        });
        test("Fourth generation labels distinguish face weapon and shoulder weapon schemes",()=>{
            var e=Game("ac4","ps3");var a=GameControlCatalog.Resolve(e,new(){GameScheme="type-a"});var b=GameControlCatalog.Resolve(e,new(){GameScheme="type-b"});
            Check(a.Actions["Cross"]=="Right arm weapon"&&b.Actions["R1"]=="Right arm weapon"&&b.Actions["Cross"].StartsWith("Switch right"));
            Check(GameControlCatalog.NativeButton(e,"Cross")=="Cross"&&GameControlCatalog.NativeButton(Game("ac4","x360"),"Cross")=="A");
            var simple=GameControlCatalog.Resolve(Game("acfa","ps3"),new(){GameScheme="simple"});
            Check(simple.Actions["LLeft"]=="Turn left"&&simple.Actions["RLeft"]=="No default combat action"&&simple.Actions["Cross"]=="Right arm weapon"&&simple.Actions["Square"]=="Left arm weapon");
        });
        test("Portable Formula Front manual and AI actions do not inherit another PSP game",()=>{
            var portable=GameControlCatalog.Resolve(Game("ac3","psp"),new());var ff=GameControlCatalog.Resolve(Game("acff","psp"),new());
            Check(portable.Actions["Triangle"]=="Look up"&&portable.Actions["Circle"]=="Look down");
            Check(ff.Actions["Triangle"]=="Cycle weapon"&&ff.Actions["Circle"]=="Left arm weapon"&&ff.Actions["Down"]=="Use inside part");
            var ai=GameControlCatalog.Resolve(Game("acff","psp"),new(){GameScheme="ai"});
            Check(ai.Actions["LUp"]=="Rotate camera up"&&ai.Actions["Select"]=="No default combat action"&&ai.Actions.Values.All(v=>!v.Contains("weapon")&&!v.Contains("Map")));
        });
        test("Japanese early game pause and map labels match region-specific manuals",()=>{
            var ac2=Game("ac2","ps2");var japanese=GameControlCatalog.Resolve(ac2 with {Region="JP"},new());var western=GameControlCatalog.Resolve(ac2 with {Region="US"},new());
            Check(japanese.Actions["Start"]=="Map"&&japanese.Actions["Select"]=="Pause"&&western.Actions["Start"]=="Pause"&&western.Actions["Select"]=="Map");
        });
        test("Active analog launch scheme preserves saved choice and weapon labels",()=>{
            var edition=Game("ac2","ps2");var choice=new ControlChoice{KeyboardPreset="Community",ControllerPreset="Community"};
            var before=GameControlCatalog.Resolve(edition,choice);var resolved=ControlProfiles.ForLaunch(edition,choice,true);var analog=GameControlCatalog.Resolve(edition,resolved);
            Check(choice.GameScheme=="auto"&&resolved.GameScheme=="analog"&&analog.Actions["LLeft"]=="Strafe left"&&analog.Actions["RUp"]=="Look up");
            foreach(var button in new[]{"Cross","Circle","Square","Triangle"})Check(before.Actions[button]==analog.Actions[button]);
            Check(ControlProfiles.Resolve("pcsx2",resolved,false,edition)["LLeft"]=="-LeftX");
        });
        test("Named imports keep stable identities and independent selections with safe export copies",()=>{
            var edition=Game("ac4","ps3");var choice=new ControlChoice();
            var source=new CommunityBindings("My layout","",[edition.Id],new(){{"Cross","B"}},new(){{"Cross","Y"}},null,"type-a");
            var first=ControlProfiles.Import(choice,source,edition);source.Keyboard["Cross"]="Q";
            var second=ControlProfiles.Import(choice,source with {Name="Alternative"},edition);
            Check(first.Id!=second.Id&&choice.Imported[0].Profile.Keyboard["Cross"]=="B");
            choice.KeyboardPreset="Imported";choice.ControllerPreset="Imported";choice.KeyboardProfileId=first.Id;choice.ControllerProfileId=second.Id;
            ControlProfiles.Rename(choice,first.Id," Renamed layout ");Check(choice.Imported[0].Id==first.Id&&choice.Imported[0].Profile.Name=="Renamed layout");
            var exported=ControlProfiles.Export(choice,first.Id,edition);exported.Keyboard["Cross"]="Z";exported.Editions[0]="changed";
            Check(ControlProfiles.Resolve("rpcs3",choice,true,edition)["Cross"]=="B"&&choice.Imported[0].Profile.Editions[0]==edition.Id);
            var path=Path.Combine(scratch,"named-controls.json");JsonData.WriteAtomic(path,choice);var restored=JsonData.Read<ControlChoice>(path);
            Check(restored.KeyboardProfileId==first.Id&&restored.ControllerProfileId==second.Id&&restored.Imported.Count==2);
            ControlProfiles.Remove(choice,first.Id);Check(choice.KeyboardPreset=="Default"&&choice.ControllerPreset=="Imported"&&choice.ControllerProfileId==second.Id);
            Reject(()=>ControlProfiles.Rename(choice,first.Id,"missing"));Reject(()=>ControlProfiles.Rename(choice,second.Id,"Injected\nname"));
        });
        test("Imported profiles reject unsupported edition and changed in-game schemes at resolution",()=>{
            var edition=Game("ac4","ps3");var choice=new ControlChoice();var profile=new CommunityBindings("Layout","",[edition.Id],[],[],null,"type-a");
            var saved=ControlProfiles.Import(choice,profile,edition);choice.KeyboardPreset="Imported";choice.KeyboardProfileId=saved.Id;choice.GameScheme="type-b";
            Reject(()=>ControlProfiles.Resolve("rpcs3",choice,true,edition));
            choice.GameScheme="type-a";Check(ControlProfiles.Resolve("rpcs3",choice,true,edition).Count>0);
            Reject(()=>ControlProfiles.Import(choice,profile with {Editions=["different-edition"]},edition));
            Reject(()=>ControlProfiles.Import(choice,profile with {GameScheme="analog"},edition));
            Reject(()=>ControlProfiles.Import(choice,profile with {Source="file:///C:/private"},edition));
            Reject(()=>ControlProfiles.Import(choice,profile with {Controller=new(){{"Cross","invalid"}}},edition));
        });
        test("Legacy Community import migrates once without altering independent custom settings",()=>{
            var edition=Game("ac2","ps2");var profile=new CommunityBindings("Legacy","https://example.com/layout",[edition.Id],new(){{"Cross","B"}},new(){{"Cross","Y"}});
            var choice=new ControlChoice{Community=profile,KeyboardPreset="Community",ControllerPreset="Custom",Controller=new(){{"Cross","X"}}};
            ControlProfiles.MigrateLegacy(choice);var id=choice.KeyboardProfileId;ControlProfiles.MigrateLegacy(choice);
            Check(choice.Community==null&&choice.Imported.Count==1&&choice.KeyboardPreset=="Imported"&&choice.KeyboardProfileId==id&&choice.ControllerPreset=="Custom");
            Check(ControlProfiles.Resolve("pcsx2",choice,true,edition)["Cross"]=="B"&&ControlProfiles.Resolve("pcsx2",choice,false,edition)["Cross"]=="X");
        });
    }
}
