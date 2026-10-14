namespace Hub.Core;

// Cover identity follows the edition, never the emulator or the UI's game label.
public sealed record CoverAsset(string Id, string GameId, string Platform, string Region, string Uri,
    string Sha256, int Width, int Height, long Bytes, string Credit, string SourceUrl, string Usage);
public sealed record EditionCover(string EditionId, string? AssetId, string? FallbackEditionId, string? Note);
public sealed record ResolvedCover(CoverAsset Asset, string? Note);
public sealed class CoverCatalog
{
    public int SchemaVersion { get; set; } = 1;
    public string Version { get; set; } = "1.0.0";
    public List<CoverAsset> Assets { get; set; } = [];
    public List<EditionCover> Editions { get; set; } = [];

    public ResolvedCover? Resolve(string editionId)
    {
        var visited = new HashSet<string>(); string? note = null;
        while (visited.Add(editionId))
        {
            var entry = Editions.SingleOrDefault(e => e.EditionId == editionId);
            if (entry == null) return null;
            note ??= entry.Note;
            if (entry.AssetId != null) return new(Assets.Single(a => a.Id == entry.AssetId), note);
            if (entry.FallbackEditionId == null) return null;
            editionId = entry.FallbackEditionId;
        }
        throw new HubException("The cover catalog contains a fallback loop.");
    }

    public void Validate(Catalog games)
    {
        if (SchemaVersion != 1 || string.IsNullOrWhiteSpace(Version)) throw new HubException("Unsupported cover catalog.");
        if (Assets.Select(a => a.Id).Distinct().Count() != Assets.Count || Editions.Select(e => e.EditionId).Distinct().Count() != Editions.Count)
            throw new HubException("Duplicate cover catalog entries.");
        foreach (var a in Assets)
        {
            SafeFiles.Id(a.Id);
            if (a.Width is < 128 or > 4096 || a.Height is < 128 or > 4096 || a.Bytes is <= 0 or > 2_000_000 || string.IsNullOrWhiteSpace(a.Credit))
                throw new HubException("Invalid cover dimensions, size or attribution.");
            if (!System.Uri.TryCreate(a.SourceUrl, UriKind.Absolute, out var source) || source.Scheme != "https") throw new HubException("Invalid cover source.");
            ProviderDownloader.ValidateProvider(Provider(a));
        }
        foreach (var e in Editions)
        {
            var edition = games.Editions.SingleOrDefault(x => x.Id == e.EditionId) ?? throw new HubException("Unknown cover edition.");
            if ((e.AssetId == null) == (e.FallbackEditionId == null)) throw new HubException("A cover needs one asset or one explicit fallback.");
            if (e.FallbackEditionId != null && (string.IsNullOrWhiteSpace(e.Note) || !Editions.Any(x => x.EditionId == e.FallbackEditionId))) throw new HubException("Cover fallback needs a target and an explanation.");
            if (e.AssetId != null && !Assets.Any(a => a.Id == e.AssetId)) throw new HubException("Unknown cover asset.");
            var resolved = Resolve(e.EditionId) ?? throw new HubException("Missing cover resolution.");
            if (resolved.Asset.GameId != edition.GameId || resolved.Asset.Platform != edition.Platform) throw new HubException("Cover fallback cannot cross games or platforms.");
            if (resolved.Asset.Region != edition.Region && string.IsNullOrWhiteSpace(resolved.Note)) throw new HubException("Regional cover fallback must be explained.");
        }
    }
    public static AssetProvider Provider(CoverAsset a) => new("cover-" + a.Id, "Artwork", 0, a.Uri, [], a.Sha256,
        "image/*", a.Usage, ["raw.githubusercontent.com", "art.gametdb.com", "images.launchbox-app.com"], Retries: 1, MaxBytes: 2_000_000);
}

public sealed class CoverService(CoverCatalog catalog, ProviderDownloader downloader)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> available = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    public ResolvedCover? Resolve(string editionId) => catalog.Resolve(editionId);
    public string? Cached(string editionId) => Resolve(editionId) is { } r ? available.GetValueOrDefault(r.Asset.Sha256) : null;
    public async Task<string?> FetchAsync(string editionId, bool offline, CancellationToken ct = default)
    {
        if (Resolve(editionId) is not { } cover) return null;
        await gate.WaitAsync(ct);
        try
        {
            if (available.TryGetValue(cover.Asset.Sha256, out var path)) return path;
            path = await downloader.FetchAsync(CoverCatalog.Provider(cover.Asset), offline, ct: ct);
            available[cover.Asset.Sha256] = path;
            return path;
        }
        finally { gate.Release(); }
    }
}
