namespace Hub.Core;

public sealed record SetupRequirement(string Id,string Title,string State,string Detail,bool Complete);
public sealed record SetupChecklist(SetupRequirement[] Requirements,bool Ready,string? NextRepair,bool FirmwareInstallPending)
{
    public static SetupChecklist Read(HubPaths paths,Catalog catalog,HubState state,Edition edition)
    {
        var planner=new LaunchPlanner(paths,catalog);var diagnostics=planner.Diagnose(edition.Id,state);
        var selection=LaunchPlanner.Selection(edition,state);var profile=catalog.Profiles.Single(p=>p.Id==(selection.ProfileId??edition.PreferredProfile));
        var backend=catalog.Backends.Single(b=>b.Id==profile.BackendId);var build=planner.SelectBuild(edition,state);
        var media=diagnostics.Single(d=>d.Name=="Game media");var discIssue=diagnostics.FirstOrDefault(d=>(d.Name is "Disc structure" or "Game preparation")&&(d.Status is "missing" or "error"));
        var gameReady=media.Status=="ready"&&discIssue==null;
        var runtime=diagnostics.Single(d=>d.Name=="Runtime");var runtimeIssue=diagnostics.FirstOrDefault(d=>(d.Name is "Compatibility repair" or "Emulator version")&&(d.Status is "missing" or "error"));
        var runtimeReady=runtime.Status=="ready"&&runtimeIssue==null;
        var firmware=LaunchPlanner.PickFirmware(backend,edition,state,f=>File.Exists(paths.Resolve(f.Path)));
        var system=diagnostics.Single(d=>d.Name=="System software");var pending=false;
        if(system.Status=="ready"&&backend.Id=="rpcs3"&&build!=null)
        {
            var home=paths.Managed("runtime","sessions",edition.Id,build.Id);var exe=SafeFiles.Within(home,build.Executable);var dir=Path.GetDirectoryName(exe)!;
            pending=!File.Exists(Path.Combine(dir,"dev_flash","vsh","module","vsh.self"))||!File.Exists(Path.Combine(dir,"dev_flash","sys","external","liblv2.sprx"));
        }
        var systemState=backend.FirmwareClass=="none"?"Not required":system.Status!="ready"?"Missing":pending?"Imported · installation pending":backend.Id=="rpcs3"?"Installed":"BIOS selected";
        var systemDetail=backend.FirmwareClass=="none"?"This console does not need a separate BIOS.":system.Status!="ready"?system.Message:
            pending?"The imported update is available. It installs automatically before the first launch.":backend.Id=="rpcs3"?"System software is installed for this game’s emulator session.":
            firmware!.Label+". The Hub configures it automatically at launch."+(backend.Id=="pcsx2"&&firmware.Region!=edition.Region?" This BIOS is from another region; PCSX2 does not enforce PS2 region locking.":"");
        var issue=diagnostics.FirstOrDefault(d=>d.Status is "missing" or "error");
        return new([
            new("media","Game files",gameReady?"Added":discIssue==null?"Missing":"Needs preparation",discIssue?.Message??(gameReady?"This edition is registered and its files are available.":media.Message),gameReady),
            new("runtime","Emulator",runtimeReady?"Installed":runtimeIssue==null?"Missing":"Repair needed",runtimeIssue?.Message??(runtimeReady?runtime.Message+". This game’s settings are applied automatically at launch.":runtime.Message),runtimeReady),
            new("firmware",backend.FirmwareClass.EndsWith("bios",StringComparison.Ordinal)?"BIOS":"System software",systemState,systemDetail,system.Status=="ready"&&!pending)
        ],issue==null,issue?.Repair,pending);
    }
}
