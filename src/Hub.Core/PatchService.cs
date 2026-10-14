using System.Text.Json;

namespace Hub.Core;

/// <summary>Versioned local configuration modules; source-bound and disabled on import.</summary>
public sealed class PatchService(HubPaths paths, Catalog catalog)
{
    public void LoadInstalled()
    {
        var installed = SafeFiles.Files(paths.Managed("patches")).Where(x => Path.GetFileName(x) == "module.json").Select(file=>(File:file,Module:Read(file))).ToList();
        var prior=catalog.Patches;var staged=prior.ToList();var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var(file,module)in installed)
        {
            if(module.Id==null||!ids.Add(module.Id))throw new HubException($"Configuration module {file} has a missing or duplicate identifier.");
            var existing=staged.FirstOrDefault(x=>x.Id.Equals(module.Id,StringComparison.OrdinalIgnoreCase));
            if(existing!=null&&JsonSerializer.Serialize(existing,JsonData.Options)!=JsonSerializer.Serialize(module,JsonData.Options))
                throw new HubException($"Configuration module {file} conflicts with an installed module. Keep the original and give new versions distinct IDs.");
            if(existing==null)staged.Add(module);
        }
        // Dependencies may refer to other installed modules. Validate the entire
        // staged set before publishing any of them to the live catalog.
        catalog.Patches=staged;
        try{foreach(var(file,module)in installed)ValidateFile(module,file);}
        catch{catalog.Patches=prior;throw;}
    }
    public PatchModule Import(string file)
    {
        var module = Read(file); ValidateFile(module,file);
        if (catalog.Patches.Any(x => x.Id == module.Id))
            throw new HubException("This module ID is already installed. Use a distinct versioned ID to keep rollback available.");
        var folder = paths.Managed("patches", SafeFiles.Id(module.Id), SafeFiles.Id(module.Version));
        JsonData.WriteAtomic(Path.Combine(folder, "module.json"), module);
        catalog.Patches.Add(module); return module;
    }
    private static PatchModule Read(string file)
    {
        try{return JsonData.ReadBounded<PatchModule>(file,512*1024);}
        catch(Exception e)when(e is JsonException or HubException){throw new HubException($"Configuration module {file} could not be read: {e.Message}",e);}
    }
    private void ValidateFile(PatchModule module,string file)
    {
        try{Validate(module);}
        catch(HubException e){throw new HubException($"Configuration module {file} is invalid: {e.Message}",e);}
    }
    private void Validate(PatchModule module)
    {
        if(module.Id==null||module.Version==null||string.IsNullOrWhiteSpace(module.Name)||module.Name.Length>256
            ||module.SourceHashes==null||module.Editions==null||module.Dependencies==null||module.Incompatibilities==null||module.Settings==null
            ||module.SourceHashes.Length>512||module.Editions.Length>512||module.Dependencies.Length>128||module.Incompatibilities.Length>128||module.Settings.Count>512
            ||module.SourceHashes.Any(x=>x==null)||module.Editions.Concat(module.Dependencies).Concat(module.Incompatibilities).Any(x=>x==null)
            ||module.Settings.Any(x=>x.Value==null||x.Value.Length>4096))
            throw new HubException("Required module metadata is missing or exceeds its limit.");
        SafeFiles.Id(module.Id); SafeFiles.Id(module.Version);
        foreach(var id in module.Editions.Concat(module.Dependencies).Concat(module.Incompatibilities))SafeFiles.Id(id);
        if (module.Method != "configuration" || module.Redistribution is "unconfirmed" or "prohibited" || string.IsNullOrWhiteSpace(module.Redistribution))
            throw new HubException("Import a licensed configuration module. Binary patches require a separately prepared derived image in this version.");
        if (module.SourceHashes.Length == 0 || module.Editions.Length == 0) throw new HubException("Patch modules require exact source hashes and target editions.");
        foreach (var hash in module.SourceHashes) CatalogStore.ValidateHash("sha256", hash);
        foreach (var edition in module.Editions)
        {
            if (!catalog.Editions.Any(x => x.Id == edition)) throw new HubException("Unknown patch target edition.");
            var keys = catalog.Profiles.Where(x => x.EditionId == edition).SelectMany(x => x.Settings.Keys).ToHashSet();
            if (module.Settings.Keys.Any(x => !keys.Contains(x))) throw new HubException("A patch setting is not supported by the target profile.");
        }
        if (module.Settings.Values.Any(x => x.Contains('\n') || x.Contains('\r'))) throw new HubException("Patch values must be single-line settings.");
        var prior = catalog.Patches.ToList();
        try { if (!catalog.Patches.Any(x=>x.Id==module.Id)) catalog.Patches.Add(module); CatalogStore.ResolvePatches(catalog, [module.Id]); }
        finally { catalog.Patches = prior; }
    }
}
