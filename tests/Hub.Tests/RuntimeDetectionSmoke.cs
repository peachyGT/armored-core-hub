using System.Security.Cryptography;
using System.Text.Json;
using Hub.Core;

static class RuntimeDetectionSmoke
{
    public static int Run(string[] args)
    {
        var live=Path.GetFullPath(args[0]);var source=Path.Combine(live,"config","state.json");
        var bytes=File.ReadAllBytes(source);var hash=SHA256.HashData(bytes);
        var state=JsonSerializer.Deserialize<HubState>(bytes,JsonData.Options)!;
        string Absolute(string path)=>Path.IsPathRooted(path)?path:Path.Combine(live,path);
        state.Builds=state.Builds.Select(b=>b with{Path=Absolute(b.Path)}).ToList();
        state.Media=state.Media.Select(m=>m with{Path=Absolute(m.Path)}).ToList();
        state.Firmware=state.Firmware.Select(f=>f with{Path=Absolute(f.Path)}).ToList();
        // All managed paths and any constructor-created folders remain in the lab.
        // Only the copied selection is cleared; the actual user state is never saved.
        var paths=new HubPaths(Path.GetFullPath(args[2]));var catalog=CatalogStore.Load(args[1]);
        var edition=catalog.Editions.Single(e=>e.Id==args[3]);var selection=LaunchPlanner.Selection(edition,state);
        var previous=selection.BuildId;selection.BuildId=null;
        var planner=new LaunchPlanner(paths,catalog);var build=planner.SelectBuild(edition,state);
        var diagnostics=planner.Diagnose(edition.Id,state);
        var unchanged=hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source)));
        Console.WriteLine(JsonSerializer.Serialize(new{edition=edition.Id,previousPin=previous,selectedBuild=build?.Id,version=build?.Version,diagnostics,stateFileUnchanged=unchanged}));
        return unchanged&&diagnostics.Single(d=>d.Name=="Runtime").Status=="ready"?0:1;
    }
}
