namespace Hub.Core;

public static class RecommendedBuilds
{
    public static ReleaseAsset? For(CompatibilityProfile profile, Backend backend) => profile.BuildRecommendation?.Release ?? backend.Recommended;
    public static BuildIssue? Issue(CompatibilityProfile profile, InstalledBuild build) => profile.BuildRecommendation?.Issues
        .FirstOrDefault(i => BuildVersion.Compare(build.Version, i.First) is >= 0 && BuildVersion.Compare(build.Version, i.Last) is <= 0);
}

// Explicit repair only: catalog updates and ordinary launches never replace a selection.
public sealed class CompatibilityRepair(HubPaths paths, Catalog catalog, RuntimeService runtimes)
{
    public async Task ApplyAsync(string editionId, HubState state, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var edition = catalog.Editions.Single(e => e.Id == editionId);
        var choice = LaunchPlanner.Selection(edition, state);
        var profile = catalog.Profiles.Single(p => p.Id == (choice.ProfileId ?? edition.PreferredProfile) && p.EditionId == editionId);
        var backend = catalog.Backends.Single(b => b.Id == profile.BackendId);
        var release = RecommendedBuilds.For(profile, backend) ?? throw new HubException("No recommended build is available.");
        var source = state.Media.FirstOrDefault(m => m.Id == choice.MediaId && m.EditionId == editionId);
        var existing = state.Builds.FirstOrDefault(b => b.BackendId == backend.Id && b.ArchiveSha256 == release.Sha256);
        InstalledBuild? build = null;
        if (existing != null)
        {
            var executable = SafeFiles.Within(paths.Resolve(existing.Path), existing.Executable);
            if (File.Exists(executable) && await SafeFiles.HashAsync(executable, ct) == existing.ExecutableSha256) build = existing;
        }
        build ??= await runtimes.InstallAsync(backend.Id, release, state.Offline, progress, ct);
        MediaRecord? prepared = null;
        if (source != null && profile.BuildRecommendation is { } recommendation && !recommendation.Formats.Contains(source.Format))
        {
            if (source.Format != "ps3-iso" || recommendation.Preparation != "ps3-iso9660")
                throw new HubException("The recommended build needs a different game format. Import a supported copy.");
            prepared = await new Ps3DiscPreparation(paths, catalog).PrepareAsync(source, state, progress, ct);
        }
        ct.ThrowIfCancellationRequested();
        // Commit the in-memory choice only after both operations succeeded. Original media,
        // old builds, profile settings and the save family stay available for rollback.
        state.Builds.RemoveAll(b => b.Id == build.Id); state.Builds.Add(build);
        if (prepared != null) { state.Media.RemoveAll(m => m.Id == prepared.Id); state.Media.Add(prepared); choice.MediaId = prepared.Id; }
        choice.BuildId = build.Id; state.Selections[editionId] = choice;
    }
}

public sealed class Ps3DiscPreparation(HubPaths paths, Catalog catalog)
{
    private sealed record Receipt(string SourceHash, MediaRecord Media);
    public async Task<MediaRecord> PrepareAsync(MediaRecord source, HubState state, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (source.Format != "ps3-iso" || source.EditionId == null) throw new HubException("Choose a recognized PS3 ISO first.");
        var file = paths.Resolve(source.Path); SafeFiles.NoLinks(file);
        progress?.Report(new("Checking original game image"));
        var hash = await SafeFiles.HashAsync(file, ct);
        if (hash != source.Fingerprint) throw new HubException("The original game image changed. Import it again before repair.");
        var target = paths.Games("prepared-ps3", hash);
        var marker = Path.Combine(target, "hub-disc.json"); var payload = Path.Combine(target, "files");
        var service = new MediaService(paths, catalog);
        if (Directory.Exists(target))
        {
            SafeFiles.NoLinks(marker);
            if (!File.Exists(marker) || new FileInfo(marker).Length > 32 * 1024 * 1024) throw new HubException("The prepared game folder is incomplete. Existing files were preserved.");
            var receipt = JsonData.Read<Receipt>(marker);
            var record = receipt.Media with { Path = paths.Portable(payload) };
            if (receipt.SourceHash != hash || record.EditionId != source.EditionId || record.Format != "ps3-folder"
                || record.Files.Count is < 1 or > 50000 || SafeFiles.Files(payload).Count() != record.Files.Count || !await service.VerifyAsync(record, ct))
                throw new HubException("The prepared game folder changed. Existing files were preserved; import a separate complete copy.");
            return record;
        }
        var disc = Ps3Disc.Inspect(file);
        var edition = catalog.Editions.Single(e => e.Id == source.EditionId);
        if (disc == null || disc.Problem != null || MediaService.NormalizeSerial(disc.ProductId ?? "") != MediaService.NormalizeSerial(edition.Serial))
            throw new HubException(disc?.Problem ?? "The disc does not match the selected edition.");
        var stage = target + ".partial-" + Guid.NewGuid().ToString("N");
        try
        {
            var unpacked = Path.Combine(stage, "files");
            await Task.Run(async () => { using var iso = new IsoReader(file, 2048, 0); await iso.ExtractPs3Async(unpacked, progress, ct); }, ct);
            if (await SafeFiles.HashAsync(file, ct) != hash) throw new HubException("The original image changed during preparation. Try again after the copy or download finishes.");
            var candidate = service.Probe(unpacked);
            if (candidate.Format != "ps3-folder" || candidate.EditionId != source.EditionId) throw new HubException("The prepared game could not be identified. Original files were preserved.");
            // Fingerprint independently; an ISO's exact-dump hash never certifies its derived representation.
            var record = await service.RegisterAsync(candidate, source.EditionId, false, new HubState(), progress, ct);
            var duplicate = state.Media.FirstOrDefault(m => m.Id == record.Id && m.EditionId == source.EditionId && m.Format == "ps3-folder");
            if (duplicate != null && Directory.Exists(paths.Resolve(duplicate.Path)) && await service.VerifyAsync(duplicate, ct)) return duplicate;
            record = record with { Path = paths.Portable(payload), Managed = true };
            JsonData.WriteAtomic(Path.Combine(stage, "hub-disc.json"), new Receipt(hash, record));
            ct.ThrowIfCancellationRequested(); await SafeFiles.MoveManagedDirectoryAsync(paths.GameStorageRoot, stage, target, ct);
            return record;
        }
        finally { SafeFiles.DeleteManagedTree(paths.GameStorageRoot, stage); }
    }
}
