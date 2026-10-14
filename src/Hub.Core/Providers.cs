using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using SharpCompress.Archives;

namespace Hub.Core;

public sealed class ProviderDownloader(HubPaths paths, HttpClient http)
{
    public static HttpClient CreateHttpClient() => new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(30) };
    public event Action<string, ProviderHealth>? HealthChanged;
    public static void ValidateProvider(AssetProvider p)
    {
        SafeFiles.Id(p.Id);
        if (!new[] {"EmulatorRelease", "OfficialFirmware", "Patch", "Artwork", "Metadata", "TranslationPatch", "InputExtension", "MediaTool"}.Contains(p.Kind)) throw new HubException("This asset provider kind is not supported.");
        CatalogStore.ValidateHash("sha256", p.Sha256);
        if (p.License is "unknown" or "prohibited" || string.IsNullOrWhiteSpace(p.License)) throw new HubException("This asset has no confirmed redistribution permission.");
        if (p.Retries is < 0 or > 4 || p.MaxBytes is <= 0 or > 8L * 1024 * 1024 * 1024) throw new HubException("Invalid provider limits.");
        foreach (var url in p.FallbackUris.Prepend(p.PrimaryUri)) ValidateUri(p, new Uri(url));
    }
    private static void ValidateUri(AssetProvider p, Uri uri)
    {
        var sonyFirmware = p.Kind == "OfficialFirmware" && uri.Scheme == "http" && uri.Host == "dus01.ps3.update.playstation.net" && uri.AbsolutePath.EndsWith("/PS3UPDAT.PUP", StringComparison.Ordinal);
        if ((!sonyFirmware && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo) || !p.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            throw new HubException("The download endpoint is outside this provider's approved HTTPS hosts.");
    }
    public async Task<string> FetchAsync(AssetProvider provider, bool offline, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateProvider(provider);
        var cache = paths.Managed("cache", "objects", provider.Sha256.ToLowerInvariant()); Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
        async Task<bool> Cached() => File.Exists(cache) && await SafeFiles.HashAsync(cache, ct) == provider.Sha256.ToLowerInvariant();
        if (await Cached()) { HealthChanged?.Invoke(provider.Id, new("cached", DateTimeOffset.UtcNow, null, null)); return cache; }
        if (offline) throw new HubException("This component is not cached. Turn off Offline mode and try again.");
        var failures = new List<string>();
        foreach (var endpoint in provider.FallbackUris.Prepend(provider.PrimaryUri))
        {
            for (var attempt = 0; attempt <= provider.Retries; attempt++)
            {
                ct.ThrowIfCancellationRequested(); var part = cache + "." + Guid.NewGuid().ToString("N") + ".partial";
                try
                {
                    progress?.Report(new("Downloading " + provider.Id + (attempt > 0 ? " · retry " + attempt : "")));
                    using var response = await SendAsync(provider, new Uri(endpoint), ct);
                    if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden) throw new HubException("Upstream download limit reached. Wait and retry, or import an official archive.");
                    response.EnsureSuccessStatusCode();
                    ValidateUri(provider, response.RequestMessage?.RequestUri ?? new Uri(endpoint));
                    var type = response.Content.Headers.ContentType?.MediaType ?? "";
                    if (type.Contains("text/html", StringComparison.OrdinalIgnoreCase) || type.Contains("application/json", StringComparison.OrdinalIgnoreCase) && provider.Kind != "Metadata")
                        throw new HubException("The source returned a web page instead of the requested asset.");
                    var total = response.Content.Headers.ContentLength;
                    if (total > provider.MaxBytes) throw new HubException("This download is larger than the permitted asset size.");
                    await using (var input = await response.Content.ReadAsStreamAsync(ct))
                    await using (var output = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1048576, true))
                    {
                        var buffer = new byte[1048576]; long done = 0; int read;
                        while ((read = await input.ReadAsync(buffer, ct)) > 0)
                        {
                            done += read; if (done > provider.MaxBytes) throw new HubException("The download exceeded its size limit.");
                            await output.WriteAsync(buffer.AsMemory(0, read), ct); progress?.Report(new("Downloading " + provider.Id, done, total));
                        }
                        if (total.HasValue && done != total.Value) throw new IOException("The download was interrupted.");
                        await output.FlushAsync(ct);
                    }
                    if (!string.Equals(await SafeFiles.HashAsync(part, ct), provider.Sha256, StringComparison.OrdinalIgnoreCase)) throw new HubException("The downloaded file failed its integrity check. No runtime was changed.");
                    File.Move(part, cache, true);
                    HealthChanged?.Invoke(provider.Id, new("healthy", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)); return cache;
                }
                catch (Exception e) when (e is HttpRequestException or IOException or HubException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    failures.Add(new Uri(endpoint).Host + ": " + e.Message);
                    if (attempt < provider.Retries) await Task.Delay(TimeSpan.FromMilliseconds(300 * (attempt + 1)), ct);
                }
                finally { if (File.Exists(part)) File.Delete(part); }
            }
        }
        if (await Cached()) return cache;
        HealthChanged?.Invoke(provider.Id, new("unavailable", DateTimeOffset.UtcNow, null, failures.LastOrDefault()));
        throw new HubException("The component could not be downloaded. Check your connection or import an official archive. " + failures.LastOrDefault());
    }
    private async Task<HttpResponseMessage> SendAsync(AssetProvider provider, Uri uri, CancellationToken ct)
    {
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            ValidateUri(provider, uri);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("ArmoredCoreHub/0.3.3 (+portable-Windows)");
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308)) return response;
            using (response)
            {
                if (response.Headers.Location is not Uri location) throw new HubException("The download source returned an incomplete redirect.");
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
            }
        }
        throw new HubException("The download source redirected too many times.");
    }
}

public static class ArchiveInstaller
{
    public static async Task ExtractAsync(string archivePath, string destination, CancellationToken ct = default,
        long maxBytes = 4L * 1024 * 1024 * 1024, IProgress<ProgressInfo>? progress = null, bool checkFreeSpace = false)
    {
        archivePath = Path.GetFullPath(archivePath); destination = Path.GetFullPath(destination);
        SafeFiles.NoLinks(archivePath); SafeFiles.NoLinks(destination); Directory.CreateDirectory(destination);
        // A native extractor also reads this path. FileShare.Read prevents changes or replacement
        // between metadata validation and extraction on Windows, while permitting its read handle.
        using var immutableSource = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = ArchiveFactory.OpenArchive(immutableSource);
        var entries = archive.Entries.Take(50001).ToList(); long total = 0, completed = 0; var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expected = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (entries.Count > 50000) throw new HubException("The archive contains too many entries (limit: 50,000).");
        foreach (var e in entries)
        {
            ct.ThrowIfCancellationRequested();
            var name = (e.Key ?? "").Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (e.IsEncrypted || !string.IsNullOrEmpty(e.LinkTarget) || UnsafeAttributes(e)) throw new HubException("Encrypted or linked archive entries are not permitted.");
            foreach (var component in name.TrimEnd('/').Split('/'))
            {
                if (component is "." or ".." || component.EndsWith('.') || component.EndsWith(' ') || component.Contains(':') || component.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new HubException("Unsafe archive entry.");
                var stem = component.Split('.')[0];
                if (Regex.IsMatch(stem, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase)) throw new HubException("Reserved archive filename.");
            }
            var target = SafeFiles.Within(destination, name);
            if (!seen.Add(target)) throw new HubException("Duplicate archive paths are not permitted.");
            if (e.Size < 0 || e.Size > maxBytes - total) throw new HubException("The extracted archive exceeds the size limit.");
            total += e.Size;
            if (!e.IsDirectory) expected.Add(target, e.Size);
        }
        if (checkFreeSpace && new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destination))!).AvailableFreeSpace < total + 256L * 1024 * 1024)
            throw new HubException($"Not enough space to unpack this archive. Free at least {total / 1073741824d:0.0} GB in the selected game storage drive. Choose another location in Settings → Game storage.");
        progress?.Report(new("Unpacking " + Path.GetFileName(archivePath), 0, total));
        if (archive.Type == SharpCompress.Common.ArchiveType.SevenZip && entries.All(NativeCodecSupported) &&
            await NativeSevenZip.TryExtractAsync(archivePath, destination, expected, total, progress, ct))
        {
            progress?.Report(new("Unpacked " + Path.GetFileName(archivePath), total, total));
            return;
        }
        async Task ExtractEntry(SharpCompress.Common.IEntry e, Func<Stream> open)
        {
            ct.ThrowIfCancellationRequested(); if (string.IsNullOrWhiteSpace(e.Key)) return;
            var target = SafeFiles.Within(destination, e.Key.Replace('\\', '/'));
            if (e.IsDirectory) { Directory.CreateDirectory(target); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = open(); await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
            var buffer = new byte[131072]; long count = 0; int read;
            long reported = completed;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                count += read; if (count > e.Size) throw new HubException("An archive entry exceeded its declared size.");
                await output.WriteAsync(buffer.AsMemory(0, read), ct); completed += read;
                if (completed - reported >= 4 * 1024 * 1024) { progress?.Report(new("Unpacking " + Path.GetFileName(archivePath), completed, total)); reported=completed; }
            }
            if (count != e.Size) throw new HubException("An archive entry was truncated.");
        }
        if (archive.IsSolid || archive.Type == SharpCompress.Common.ArchiveType.SevenZip)
        {
            using var reader = archive.ExtractAllEntries();
            while (reader.MoveToNextEntry()) await ExtractEntry(reader.Entry, () => reader.OpenEntryStream());
        }
        else foreach (var entry in entries) await ExtractEntry(entry, () => entry.OpenEntryStream());
        progress?.Report(new("Unpacked " + Path.GetFileName(archivePath), total, total));
    }

    private static bool UnsafeAttributes(SharpCompress.Common.IEntry entry)
    {
        // SharpCompress's SevenZipEntry.LinkTarget is always null. 7z Unix attributes can
        // still describe symlinks/devices, so reject non-file/non-directory types explicitly.
        static bool Unsafe(int? attributes)
        {
            if (attributes is not int value) return false;
            var kind = (value >> 16) & 0xf000;
            return (value & (int)FileAttributes.ReparsePoint) != 0 || kind is not (0 or 0x4000 or 0x8000);
        }
        return Unsafe(entry.Attrib) || entry is SharpCompress.Common.SevenZip.SevenZipEntry seven && Unsafe(seven.ExtendedAttrib);
    }

    private static bool NativeCodecSupported(SharpCompress.Common.IEntry entry)
    {
        if (entry.IsDirectory) return true;
        try { return entry.CompressionType is SharpCompress.Common.CompressionType.LZMA or SharpCompress.Common.CompressionType.None; }
        // BCJ and other architecture filters can precede LZMA in a coder chain that
        // SharpCompress cannot classify here. The reduced native tool supports them.
        catch (SharpCompress.Common.InvalidFormatException) { return true; }
    }
}

public sealed class RuntimeService(HubPaths paths, Catalog catalog, ProviderDownloader downloader, HttpClient http)
{
    private static readonly SemaphoreSlim installGate=new(1,1);
    public async Task<InstalledBuild> InstallAsync(string backendId, ReleaseAsset release, bool offline, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        await installGate.WaitAsync(ct);
        try
        {
        var backend = catalog.Backends.Single(x => x.Id == backendId);
        if(await ExistingAsync(backendId,release,ct) is {} installed){progress?.Report(new("Using installed "+backend.Name+" "+installed.Version));return installed;}
        var provider = new AssetProvider(backend.Id + "-" + release.Sha256[..12], "EmulatorRelease", 0, release.Uri, [], release.Sha256,
            "application/octet-stream", backend.License, ["github.com", "api.github.com", "release-assets.githubusercontent.com", "objects.githubusercontent.com"]);
        var archive = await downloader.FetchAsync(provider, offline, progress, ct);
        return await InstallArchiveCoreAsync(backendId, archive, release, progress, ct);
        }
        finally{installGate.Release();}
    }
    public async Task<InstalledBuild> InstallArchiveAsync(string backendId, string archive, ReleaseAsset release, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        await installGate.WaitAsync(ct);try{return await InstallArchiveCoreAsync(backendId,archive,release,progress,ct);}finally{installGate.Release();}
    }
    private async Task<InstalledBuild?> ExistingAsync(string backendId,ReleaseAsset release,CancellationToken ct)
    {
        CatalogStore.ValidateHash("sha256",release.Sha256);
        var home=paths.Managed("runtime",SafeFiles.Id(backendId),SafeFiles.Id(backendId+"-"+release.Sha256.ToLowerInvariant()[..16]));var marker=Path.Combine(home,"hub-build.json");
        if(!File.Exists(marker))return null;
        try
        {
            var build=JsonData.ReadBounded<InstalledBuild>(marker,16384);
            if(build.BackendId!=backendId||build.Id!=backendId+"-"+release.Sha256.ToLowerInvariant()[..16]||string.IsNullOrWhiteSpace(build.Executable)||!string.Equals(build.ArchiveSha256,release.Sha256,StringComparison.OrdinalIgnoreCase))return null;
            var exe=SafeFiles.Within(home,build.Executable);
            return File.Exists(exe)&&string.Equals(await SafeFiles.HashAsync(exe,ct),build.ExecutableSha256,StringComparison.OrdinalIgnoreCase)?build with{Path=paths.Portable(home)}:null;
        }
        catch(Exception ex)when(ex is HubException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException){return null;}
    }
    private async Task<InstalledBuild> InstallArchiveCoreAsync(string backendId, string archive, ReleaseAsset release,IProgress<ProgressInfo>? progress,CancellationToken ct)
    {
        var backend = catalog.Backends.Single(x => x.Id == backendId);
        if (!string.Equals(await SafeFiles.HashAsync(archive, ct), release.Sha256, StringComparison.OrdinalIgnoreCase)) throw new HubException("This archive does not match the selected upstream release.");
        var buildId = SafeFiles.Id(backendId + "-" + release.Sha256.ToLowerInvariant()[..16]); var destination = paths.Managed("runtime", backendId, buildId);
        var marker = Path.Combine(destination, "hub-build.json");
        if (File.Exists(marker))
        {
            var existing = JsonData.Read<InstalledBuild>(marker);
            var file = SafeFiles.Within(destination, existing.Executable);
            if (File.Exists(file) && await SafeFiles.HashAsync(file, ct) == existing.ExecutableSha256) return existing;
            // Preserve the damaged installation for investigation; a repaired build has the same identity.
            var quarantine = destination + ".damaged-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            await SafeFiles.MoveManagedDirectoryAsync(paths.Root, destination, quarantine, ct);
        }
        var stage = destination + ".partial-" + Guid.NewGuid().ToString("N");
        try
        {
            progress?.Report(new("Installing " + backend.Name + " " + release.Version));
            await Task.Run(() => ArchiveInstaller.ExtractAsync(archive, stage, ct), ct);
            var executable = SafeFiles.Files(stage).SingleOrDefault(f => Path.GetFileName(f).Equals(backend.Executable, StringComparison.OrdinalIgnoreCase))
                ?? throw new HubException("The archive does not contain the expected " + backend.Name + " executable.");
            var relativeExe = Path.GetRelativePath(stage, executable);
            var build = new InstalledBuild(buildId, backendId, release.Version, paths.Portable(destination), relativeExe, release.Sha256,
                await SafeFiles.HashAsync(executable, ct), release.Uri, DateTimeOffset.UtcNow);
            // Original package is extracted without generated configuration. Config lives in separate session homes.
            JsonData.WriteAtomic(Path.Combine(stage, "hub-build.json"), build);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!); await SafeFiles.MoveManagedDirectoryAsync(paths.Root, stage, destination, ct);
            return build;
        }
        finally { SafeFiles.DeleteManagedTree(paths.Root, stage); }
    }
    public async Task<ReleaseAsset> CheckLatestAsync(string backendId, bool offline, CancellationToken ct = default)
    {
        var backend = catalog.Backends.Single(x => x.Id == backendId);
        var cache = paths.Managed("cache", backendId + "-release.json");
        if (offline) return File.Exists(cache) ? JsonData.Read<ReleaseAsset>(cache) : backend.Recommended ?? throw new HubException("No cached release information.");
        using var request = new HttpRequestMessage(HttpMethod.Get, backend.ReleaseEndpoint); request.Headers.UserAgent.ParseAdd("ArmoredCoreHub/0.1");
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new HubException("The upstream release service is unavailable or rate limited. Your installed builds are unchanged.");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var root = doc.RootElement;
        var asset = root.GetProperty("assets").EnumerateArray().SingleOrDefault(a => Regex.IsMatch(a.GetProperty("name").GetString()!, backend.AssetPattern, RegexOptions.IgnoreCase));
        if (asset.ValueKind == JsonValueKind.Undefined) throw new HubException("No supported Windows archive was found in this release.");
        var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
        if (digest?.StartsWith("sha256:") != true) throw new HubException("Upstream did not publish an integrity digest for this archive. Keep the pinned build or import a maintainer-verified manifest.");
        var version = root.GetProperty("name").GetString() ?? root.GetProperty("tag_name").GetString()!;
        if (backendId == "rpcs3" && Regex.Match(asset.GetProperty("name").GetString()!, @"^rpcs3-v(\d+\.\d+\.\d+-\d+)-") is { Success: true } rpcVersion) version = rpcVersion.Groups[1].Value;
        var release = new ReleaseAsset(version, root.GetProperty("tag_name").GetString()!, asset.GetProperty("browser_download_url").GetString()!, digest[7..],
            asset.GetProperty("name").GetString()!, root.GetProperty("published_at").GetString()!, "GitHub release asset digest; not a gameplay certification");
        CatalogStore.ValidateHash("sha256", release.Sha256); JsonData.WriteAtomic(cache, release); return release;
    }
}
