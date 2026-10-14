namespace Hub.Core;

public sealed class SaveService(HubPaths paths)
{
    public string Live(string family, string backend) => paths.Managed("saves", SafeFiles.Id(family), SafeFiles.Id(backend), "live");
    public List<SaveSnapshot> List(string family, string backend)
    {
        var dir = paths.Managed("saves", SafeFiles.Id(family), SafeFiles.Id(backend), "backups");
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateDirectories(dir).Where(x => File.Exists(Path.Combine(x, "snapshot.json"))).Select(x => JsonData.Read<SaveSnapshot>(Path.Combine(x, "snapshot.json"))).OrderByDescending(x => x.CreatedAt).ToList();
    }
    public async Task<SaveSnapshot> BackupAsync(string family, string game, string backend, string reason, CancellationToken ct = default)
    {
        var live = Live(family, backend); Directory.CreateDirectory(live);
        var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..6];
        var folder = paths.Managed("saves", SafeFiles.Id(family), SafeFiles.Id(backend), "backups", id);
        var staging = folder + ".partial"; var data = Path.Combine(staging, "data"); var files = new List<MediaFile>();
        try
        {
            Directory.CreateDirectory(data);
            foreach (var file in SafeFiles.Files(live))
            {
                var relative = Path.GetRelativePath(live, file);
                // Installed game data and shaders are not save data.
                if (!IsSaveFile(relative,backend)) continue;
                ct.ThrowIfCancellationRequested(); var target = SafeFiles.Within(data, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var hash = await CopySnapshotFileAsync(file, target, ct);
                files.Add(new(relative, new FileInfo(target).Length, hash, File.GetLastWriteTimeUtc(file).Ticks));
            }
            var snapshot = new SaveSnapshot(id, family, game, backend, reason, DateTimeOffset.UtcNow, paths.Portable(folder), files);
            JsonData.WriteAtomic(Path.Combine(staging, "snapshot.json"), snapshot);
            await SafeFiles.MoveManagedDirectoryAsync(paths.Root, staging, folder, ct); return snapshot;
        }
        finally { SafeFiles.DeleteManagedTree(paths.Root, staging); }
    }
    private static async Task<string> CopySnapshotFileAsync(string file, string target, CancellationToken ct)
    {
        for(var attempt=0;;attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using(var input=File.OpenRead(file)) await using(var output=File.Create(target)) await input.CopyToAsync(output,ct);
                var hash=await SafeFiles.HashAsync(target,ct);
                if(await SafeFiles.HashAsync(file,ct)!=hash) throw new HubException("Save data changed during backup. Close the emulator and try again.");
                return hash;
            }
            catch(IOException e) when(attempt<5 && (e.HResult & 0xffff) is 32 or 33)
            {
                // Native shutdown or antivirus inspection can briefly retain a save
                // handle. Retry the whole copy, retaining strict source verification.
                await Task.Delay(100*(1<<attempt),ct);
            }
        }
    }
    public async Task RestoreAsync(SaveSnapshot snapshot, string gameId, CancellationToken ct = default)
    {
        var source = paths.Resolve(snapshot.Path);
        if (!SafeFiles.IsWithin(paths.Managed("saves"), source)) throw new HubException("Save restore points must be in managed storage.");
        var expected=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in snapshot.Files)
        {
            var path = SafeFiles.Within(Path.Combine(source, "data"), file.RelativeName);
            if (!expected.TryAdd(path,file.Sha256)||!IsSaveFile(file.RelativeName,snapshot.BackendId)||!File.Exists(path)||new FileInfo(path).Length!=file.Size||await SafeFiles.HashAsync(path,ct)!=file.Sha256)
                throw new HubException("This restore point is incomplete or damaged. Current saves were not changed.");
        }
        await BackupAsync(snapshot.Family, gameId, snapshot.BackendId, "Before restoring " + snapshot.Id, ct);
        var live=Live(snapshot.Family,snapshot.BackendId);
        var targets=snapshot.Files.Select(f=>SafeFiles.Within(live,f.RelativeName)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removed=SafeFiles.Files(live).Where(f=>IsSaveFile(Path.GetRelativePath(live,f),snapshot.BackendId)&&!targets.Contains(f)).Select(f=>Path.GetRelativePath(live,f)).ToList();
        await ApplyFilesAsync(Path.Combine(source,"data"),live,snapshot.Files.Select(f=>f.RelativeName).ToList(),true,ct,expected,removed);
    }
    public async Task ImportAsync(string selectedFolder, string family, string game, string backend, CancellationToken ct = default)
    {
        SafeFiles.NoLinks(selectedFolder); await BackupAsync(family, game, backend, "Before importing external saves", ct);
        var live = Live(family, backend); Directory.CreateDirectory(live);
        var files = SafeFiles.Files(selectedFolder).Select(x => Path.GetRelativePath(selectedFolder, x)).ToList();
        if (files.Any(x => File.Exists(SafeFiles.Within(live, x))))
            throw new HubException("A save with this name already exists. No save was imported or overwritten.");
        await ApplyFilesAsync(selectedFolder, live, files, false, ct);
    }
    private static bool IsSaveFile(string relative,string backend)
    {
        // Imported metadata is not an enumerated filesystem path. Reject dot
        // segments before checking its save subtree so normalization cannot
        // redirect a restore into installed games or unrelated emulator data.
        var normalized=relative.Replace('\\','/');
        if(normalized.Split('/').Any(part=>part is "" or "." or ".."))return false;
        if(backend=="ppsspp"&&Path.GetFileName(normalized).Equals("ppsspp_retroachievements.dat",StringComparison.OrdinalIgnoreCase))return false;
        return backend switch
        {
            "rpcs3"=>normalized.StartsWith("dev_hdd0/home/",StringComparison.OrdinalIgnoreCase),
            "ppsspp"=>normalized.StartsWith("PSP/SAVEDATA/",StringComparison.OrdinalIgnoreCase),_=>true
        };
    }
    private async Task ApplyFilesAsync(string source, string live, List<string> files, bool replace, CancellationToken ct,Dictionary<string,string>? expected=null,List<string>? removed=null)
    {
        var staging = paths.Managed("cache", "save-transaction-" + Guid.NewGuid().ToString("N"));
        var committed = new List<(string Target, string? Previous)>();
        try
        {
            foreach (var relative in files)
            {
                ct.ThrowIfCancellationRequested();
                var from = SafeFiles.Within(source, relative); var staged = SafeFiles.Within(Path.Combine(staging, "new"), relative);
                Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
                await using (var input = File.OpenRead(from)) await using (var output = File.Create(staged)) await input.CopyToAsync(output, ct);
                var hash=await SafeFiles.HashAsync(staged,ct);
                if(hash!=await SafeFiles.HashAsync(from,ct)||expected!=null&&hash!=expected[from])throw new HubException("Save data changed during import or restore. Current saves were not changed.");
            }
            ct.ThrowIfCancellationRequested();
            // Once commit starts, finish or roll back synchronously; cancellation cannot leave half an import.
            foreach (var relative in files)
            {
                var target = SafeFiles.Within(live, relative); string? previous = null;
                if (File.Exists(target))
                {
                    if (!replace) throw new HubException("A destination save appeared during import. No existing save was overwritten.");
                    previous = SafeFiles.Within(Path.Combine(staging,"old"), relative); Directory.CreateDirectory(Path.GetDirectoryName(previous)!);
                    File.Copy(target, previous);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(SafeFiles.Within(Path.Combine(staging, "new"), relative), target, replace); committed.Add((target, previous));
            }
            foreach(var relative in removed??[])
            {
                var target=SafeFiles.Within(live,relative);var previous=SafeFiles.Within(Path.Combine(staging,"old"),relative);
                Directory.CreateDirectory(Path.GetDirectoryName(previous)!);File.Move(target,previous);committed.Add((target,previous));
            }
        }
        catch
        {
            foreach (var (target, previous) in committed.AsEnumerable().Reverse())
                if (previous != null) File.Copy(previous, target, true); else File.Delete(target);
            throw;
        }
        finally { SafeFiles.DeleteManagedTree(paths.Root, staging); }
    }
}
