using System.Diagnostics;
using System.Security.Cryptography;

namespace Hub.Core;

// Archives are transport containers, never emulator media formats. Originals are read-only;
// only fully extracted payloads are published. A cancelled review removes unregistered data.
public sealed class MediaArchiveService(HubPaths paths, Catalog catalog)
{
    public static bool IsArchive(string path) => Path.GetExtension(path).ToLowerInvariant() is ".7z" or ".zip" or ".rar";
    public const long MaximumExpandedBytes = 128L * 1024 * 1024 * 1024;
    private sealed record ArchiveFile(string Name, long Size, long Stamp);
    private sealed record Receipt(string Hash, List<ArchiveFile> Files);
    private sealed record ImportTiming(string Source, string Result, string ExtractionEngine,
        double HashSeconds, double ExtractSeconds, double ScanSeconds, double TotalSeconds, string? Error);
    private sealed class InlineProgress(Action<ProgressInfo> report) : IProgress<ProgressInfo>
    { public void Report(ProgressInfo value) => report(value); }

    public async Task<MediaImportBatch> PrepareAsync(IEnumerable<MediaCandidate> input, HubState state,
        IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var batch = new MediaImportBatch(paths, state); var media = new MediaService(paths, catalog);
        var timings = new List<ImportTiming>();
        try
        {
            foreach (var candidate in input.DistinctBy(c => c.Path, StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                if (candidate.Format != "archive") { batch.Candidates.Add(candidate); continue; }
                var watch = Stopwatch.StartNew(); double hashSeconds = 0, extractSeconds = 0, scanSeconds = 0;
                var engine = "Reused stored files"; var result = "Failed"; string? error = null;
                try
                {
                    progress?.Report(new("Checking archive " + Path.GetFileName(candidate.Path)));
                    SafeFiles.NoLinks(candidate.Path);
                    var sourceInfo = new FileInfo(candidate.Path); var sourceLength = sourceInfo.Length; var sourceStamp = sourceInfo.LastWriteTimeUtc;
                    var hash = await HashArchiveAsync(candidate.Path, progress, ct); hashSeconds = watch.Elapsed.TotalSeconds;
                    var target = paths.Games("archives", hash);
                    var payload = Path.Combine(target, "files"); var marker = Path.Combine(target, "hub-archive.json");
                    if (Directory.Exists(target))
                    {
                        SafeFiles.NoLinks(marker);
                        if (!File.Exists(marker) || new FileInfo(marker).Length > 16 * 1024 * 1024) throw new HubException("The stored archive is incomplete. Use a fresh Hub storage folder or repair the existing copy.");
                        var receipt = JsonData.Read<Receipt>(marker);
                        if (receipt.Hash != hash || receipt.Files.Count > 50000 || receipt.Files.Any(f => {
                            var file = new FileInfo(SafeFiles.Within(payload,f.Name));
                            return !file.Exists || file.Length != f.Size || file.LastWriteTimeUtc.Ticks != f.Stamp;
                        })) throw new HubException("Previously unpacked files changed. Existing files were preserved; repair or import a separate copy.");
                    }
                    else
                    {
                        var stage = target + ".partial-" + Guid.NewGuid().ToString("N");
                        try
                        {
                            var unpacked = Path.Combine(stage,"files");
                            engine = "Managed"; var extraction = Stopwatch.StartNew();
                            var extractionProgress = new InlineProgress(p => {
                                if (p.Message.Contains("native 7-Zip", StringComparison.Ordinal)) engine = "Native 7-Zip 26.04";
                                progress?.Report(p);
                            });
                            try { await Task.Run(() => ArchiveInstaller.ExtractAsync(candidate.Path, unpacked, ct, MaximumExpandedBytes, extractionProgress, true), ct); }
                            finally { extractSeconds = extraction.Elapsed.TotalSeconds; }
                            sourceInfo.Refresh();
                            if (sourceInfo.Length != sourceLength || sourceInfo.LastWriteTimeUtc != sourceStamp) throw new HubException("The archive changed while it was unpacked. Wait for the download or copy to finish, then add it again.");
                            var files = SafeFiles.Files(unpacked).Select(f => { var info=new FileInfo(f); return new ArchiveFile(Path.GetRelativePath(unpacked,f),info.Length,info.LastWriteTimeUtc.Ticks); }).ToList();
                            JsonData.WriteAtomic(Path.Combine(stage,"hub-archive.json"),new Receipt(hash,files));
                            ct.ThrowIfCancellationRequested(); await SafeFiles.MoveManagedDirectoryAsync(paths.GameStorageRoot,stage,target,ct);
                            batch.Created.Add(target);
                        }
                        finally { SafeFiles.DeleteManagedTree(paths.GameStorageRoot,stage); }
                    }
                    progress?.Report(new("Identifying unpacked games " + Path.GetFileName(candidate.Path)));
                    var scan = Stopwatch.StartNew();
                    var found = await media.ScanAsync(payload, progress, ct); scanSeconds = scan.Elapsed.TotalSeconds;
                    foreach (var item in found)
                        batch.Candidates.Add(item.Format == "archive"
                            ? item with { Format="inaccessible", Reason="Nested archives are not unpacked automatically.", Warnings=["Unpack this nested archive separately, then add its game files."] }
                            : item with { ArchiveSource=Path.GetFileName(candidate.Path) });
                    if (found.Count == 0) batch.Candidates.Add(candidate with { Format="inaccessible", Reason="No supported game files were found inside this archive.", Warnings=["Use an archive containing a disc image or extracted console game folder."] });
                    result = "Prepared";
                }
                catch (Exception ex) when (ex is not OperationCanceledException && (ex is IOException or UnauthorizedAccessException or HubException or InvalidOperationException or System.Text.Json.JsonException or NotSupportedException || ex.GetType().Namespace?.StartsWith("SharpCompress",StringComparison.Ordinal) == true))
                {
                    error = ex.GetType().Name + ": " + ex.Message;
                    batch.Candidates.Add(candidate with { Format="inaccessible", Reason="Could not unpack this archive.", Warnings=[ex is HubException ? ex.Message : "The archive may be damaged, password protected or incomplete. Check all parts with your archive tool."] });
                }
                catch (OperationCanceledException) { result = "Cancelled"; throw; }
                finally
                {
                    timings.Add(new(candidate.Path, result, engine, hashSeconds, extractSeconds, scanSeconds, watch.Elapsed.TotalSeconds, error));
                    JsonData.WriteAtomic(paths.Managed("logs", "last-archive-import.json"), timings.TakeLast(1000).ToList());
                }
            }
            return batch;
        }
        catch { batch.Dispose(); throw; }
    }

    private static async Task<string> HashArchiveAsync(string path, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024]; long completed = 0; var report = Stopwatch.StartNew();
        var message = "Checking archive " + Path.GetFileName(path);
        progress?.Report(new(message, 0, stream.Length));
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            hash.AppendData(buffer, 0, read); completed += read;
            if (report.ElapsedMilliseconds >= 250) { progress?.Report(new(message, completed, stream.Length)); report.Restart(); }
        }
        progress?.Report(new(message, completed, stream.Length));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

public sealed class MediaImportBatch(HubPaths paths, HubState state) : IDisposable
{
    public List<MediaCandidate> Candidates { get; } = [];
    internal List<string> Created { get; } = [];
    public void Dispose()
    {
        foreach (var folder in Created)
            if (!state.Media.Any(m => SafeFiles.IsWithin(folder, paths.Resolve(m.Path)))) SafeFiles.DeleteManagedTree(paths.GameStorageRoot,folder);
        Created.Clear();
    }
}
