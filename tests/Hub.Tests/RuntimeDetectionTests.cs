using Hub.Core;

static class RuntimeDetectionTests
{
    public static void Register(Action<string,Action> test,string scratch,Catalog catalog)
    {
        void Check(bool value){if(!value)throw new Exception("Runtime detection regression");}
        (HubPaths paths, HubState state, Edition edition, InstalledBuild build) Fixture(string name, string backendId, string? editionId=null)
        {
            var paths=new HubPaths(Path.Combine(scratch,"runtime-detection-"+name));
            var profile=editionId==null ? catalog.Profiles.First(p=>p.BackendId==backendId)
                : catalog.Profiles.Single(p=>p.Id==catalog.Editions.Single(e=>e.Id==editionId).PreferredProfile);
            var edition=catalog.Editions.Single(e=>e.Id==profile.EditionId);
            var backend=catalog.Backends.Single(b=>b.Id==backendId);
            var release=RecommendedBuilds.For(profile,backend)!;
            var exe=paths.Managed("runtime","installed",backend.Executable);Directory.CreateDirectory(Path.GetDirectoryName(exe)!);File.WriteAllText(exe,"synthetic runtime");
            var build=new InstalledBuild("shared",backendId,release.Version,paths.Portable(Path.GetDirectoryName(exe)!),backend.Executable,release.Sha256,new string('1',64),"fixture",DateTimeOffset.UtcNow);
            var format=edition.Formats.Contains("ps3-folder") ? "ps3-folder" : edition.Formats.First(f=>backend.Formats.Contains(f));
            var file=paths.Managed("games",format=="ps3-folder" ? "disc" : "disc.iso");
            if(format=="ps3-folder"){Directory.CreateDirectory(Path.Combine(file,"PS3_GAME","USRDIR"));File.WriteAllText(Path.Combine(file,"PS3_GAME","USRDIR","EBOOT.BIN"),"fixture");}
            else File.WriteAllText(file,"fixture");
            var media=new MediaRecord("media",paths.Portable(file),format,edition.Id,new string('2',64),MediaConfidence.RecognizedUnverified,"fixture",[],DateTimeOffset.UtcNow,false);
            var firmware=paths.Managed("firmware","test.bin");File.WriteAllText(firmware,"fixture");
            var state=new HubState{Builds=[build],Media=[media],Firmware=[new("firmware",backend.FirmwareClass,paths.Portable(firmware),new string('3',64),"Imported system software",false,edition.Region)]};
            state.Selections[edition.Id]=new(){EditionId=edition.Id,ProfileId=profile.Id,MediaId=media.Id};
            return(paths,state,edition,build);
        }
        test("Every emulator reuses an installed build before an edition setup click",()=>{
            foreach(var backend in catalog.Backends)
            {
                var (paths,state,edition,build)=Fixture(backend.Id,backend.Id);
                var planner=new LaunchPlanner(paths,catalog);
                Check(!planner.Diagnose(edition.Id,state).Any(d=>d.Status is "missing" or "error"));
                Check(planner.Resolve(edition.Id,state).BuildId==build.Id);
                Check(state.Selections[edition.Id].BuildId==null && state.Builds.Count==1);
            }
        });
        test("For Answer immediately shares AC4's recommended RPCS3 installation",()=>{
            var(paths,state,edition,good)=Fixture("for-answer","rpcs3","acfa-ps3-blus30187");
            var bad=good with{Id="newer-regression",Version="0.0.43-20250",ArchiveSha256=new string('9',64),InstalledAt=good.InstalledAt.AddMinutes(1)};
            state.Builds.Insert(0,bad);
            var planner=new LaunchPlanner(paths,catalog);
            Check(planner.SelectBuild(edition,state)?.Id==good.Id);
            Check(planner.Diagnose(edition.Id,state).Single(d=>d.Name=="Runtime").Status=="ready");
            Check(planner.Resolve(edition.Id,state).BuildId==good.Id);
        });
        test("Explicit edition builds remain pinned even when another recommendation is installed",()=>{
            var(paths,state,edition,good)=Fixture("explicit","rpcs3","acfa-ps3-blus30187");
            var bad=good with{Id="explicit-old",Version="0.0.43-20250",ArchiveSha256=new string('9',64)};state.Builds.Add(bad);
            state.Selections[edition.Id].BuildId=bad.Id;var planner=new LaunchPlanner(paths,catalog);
            Check(planner.SelectBuild(edition,state)?.Id==bad.Id);
            Check(planner.Diagnose(edition.Id,state).Any(d=>d.Repair=="compatibility"&&d.Status=="error"));
            state.Selections[edition.Id].BuildId="absent";
            Check(planner.SelectBuild(edition,state)==null);
            Check(planner.Diagnose(edition.Id,state).Single(d=>d.Name=="Runtime").Status=="missing");
        });
        test("Automatic build detection skips missing executables and other emulator families",()=>{
            var(paths,state,edition,good)=Fixture("missing","ppsspp");
            state.Builds.Insert(0,good with{Id="missing-exe",Path="runtime/missing",InstalledAt=good.InstalledAt.AddMinutes(1)});
            state.Builds.Insert(0,good with{Id="wrong-backend",BackendId="pcsx2",InstalledAt=good.InstalledAt.AddMinutes(2)});
            Check(new LaunchPlanner(paths,catalog).SelectBuild(edition,state)?.Id==good.Id);
            File.Delete(Path.Combine(paths.Resolve(good.Path),good.Executable));
            Check(new LaunchPlanner(paths,catalog).SelectBuild(edition,state)==null);
        });
        test("Installed recommended RPCS3 ISO repair is detected without choosing its regressed successor",()=>{
            var(paths,state,edition,good)=Fixture("iso-repair","rpcs3","ac4-ps3-bles00039");
            var iso=paths.Managed("games","original.iso");ImportTests.Ps3Iso(iso);
            state.Media[0]=state.Media[0] with{Path=paths.Portable(iso),Format="ps3-iso"};
            state.Builds.Insert(0,good with{Id="newer",Version="0.0.43-20250",ArchiveSha256=new string('9',64),InstalledAt=good.InstalledAt.AddMinutes(1)});
            var planner=new LaunchPlanner(paths,catalog);Check(planner.SelectBuild(edition,state)?.Id==good.Id);
            Check(planner.Diagnose(edition.Id,state).Any(d=>d.Name=="Game preparation"&&d.Repair=="compatibility"));
            Check(!planner.Diagnose(edition.Id,state).Any(d=>d.Name=="Compatibility repair"));
        });
    }
}
