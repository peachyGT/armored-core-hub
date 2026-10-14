using System.Security.Cryptography;
using Hub.Core;

static class BiosSetupSmoke
{
    public static async Task<int> Run(string[] args)
    {
        var paths=new HubPaths(args[0]);var catalog=CatalogStore.Load(args[1]);var archive=Path.GetFullPath(args[2]);var edition=catalog.Editions.Single(e=>e.Id==args[3]);
        var identity=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(paths.Root.ToUpperInvariant())));
        using var mutex=new Mutex(true,"Local\\ArmoredCoreHub-"+identity,out var first);
        if(!first)throw new HubException("Close this portable Hub before importing system software from the verification helper.");
        var store=new StateStore(paths);var state=store.Load();var previousMedia=System.Text.Json.JsonSerializer.Serialize(state.Media,JsonData.Options);
        var sourceHash=await SafeFiles.HashAsync(archive);
        var imported=await new FirmwareService(paths,catalog).ImportForEditionAsync(archive,edition);
        state.Firmware.RemoveAll(f=>f.Id==imported.Selected.Id);state.Firmware.Add(imported.Selected);store.Save(state);
        var checklist=SetupChecklist.Read(paths,catalog,state,edition);var unchanged=sourceHash==await SafeFiles.HashAsync(archive);
        var report=new {edition=edition.Id,selected=imported.Selected,candidates=imported.CandidatesFound,ignored=imported.IgnoredItems,sourceArchiveUnchanged=unchanged,gameRegistrationsPreserved=System.Text.Json.JsonSerializer.Serialize(store.Load().Media,JsonData.Options)==previousMedia,checklist};
        JsonData.WriteAtomic(paths.Managed("logs","bios-setup-import.json"),report);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report,JsonData.Options));
        return unchanged&&checklist.Requirements.Single(r=>r.Id=="firmware").Complete?0:1;
    }
}
