using System.IO;
using System.Net.Http;
using System.Windows;
using Hub.Core;

namespace Hub.App;

public partial class MainWindow
{
    private CoverService? covers;
    private readonly HashSet<string> coverAttempts = [];
    private CancellationTokenSource? coverDownloads;
    private bool coverNetworkEnabled;

    private void InitializeCovers()
    {
        // Artwork has its own versioned manifest; compatibility settings are untouched.
        var file = paths.Managed("manifests", "covers.json");
        if (!File.Exists(file)) file = Path.Combine(AppContext.BaseDirectory, "manifests", "covers.json");
        try
        {
            var manifest = JsonData.Read<CoverCatalog>(file) ?? new();
            manifest.Validate(catalog);
            covers = new(manifest, new ProviderDownloader(paths, http));
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or HubException or InvalidOperationException)
        { StatusText.Text = "Cover list could not be read. Local artwork is still available."; }
    }
    private Edition RowEdition(string key, string gameId, bool portable)
    {
        var choices = catalog.Editions.Where(e => e.GameId == gameId && (e.Platform == "psp") == portable).ToArray();
        var preferred = state.PreferredEditions.GetValueOrDefault(key) ?? state.PreferredEditions.GetValueOrDefault(gameId);
        return choices.FirstOrDefault(e => e.Id == preferred)
            ?? choices.FirstOrDefault(e => state.Media.Any(m => m.EditionId == e.Id))
            ?? choices.OrderBy(e => e.Region == "US" ? 0 : e.Region == "EU" ? 1 : e.Region == "JP" ? 2 : 3).ThenBy(e => e.Disc).First();
    }
    private Artwork EditionArtwork(string key, string editionId)
    {
        // A PSP entry cannot inherit a custom PS2 cover. Existing manual files stay authoritative.
        var chosen = artwork.Get(key, key);
        return artwork.WithCover(chosen, covers?.Cached(editionId));
    }
    private async Task StartCovers(bool allowNetwork, bool manual = false)
    {
        if(presentationClosed)return;
        coverDownloads?.Cancel(); coverDownloads?.Dispose();
        coverDownloads = CancellationTokenSource.CreateLinkedTokenSource(presentationLifetime.Token);
        var cancelled = coverDownloads.Token;
        coverAttempts.Clear(); coverNetworkEnabled = allowNetwork && (appearance.DownloadCovers || manual);
        foreach (var row in rows.ToArray())
        {
            if (cancelled.IsCancellationRequested) break;
            await FetchCover(RowEdition(row.Id, row.GameId, row.Platform == "psp").Id);
        }
    }
    private async Task FetchCover(string editionId)
    {
        if (presentationClosed || covers == null || coverDownloads == null || coverDownloads.IsCancellationRequested || !coverAttempts.Add(editionId)) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(coverDownloads.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            await covers.FetchAsync(editionId, state.Offline || !coverNetworkEnabled, timeout.Token);
            if (presentationClosed || timeout.IsCancellationRequested) return;
            UpdateCoverRows();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or HttpRequestException or HubException or UnauthorizedAccessException)
        { /* Artwork is optional. Retry and source information remain available in Artwork. */ }
    }
    private void UpdateCoverRows()
    {
        if(presentationClosed)return;
        var selectedId = (GameList.SelectedItem as GameRow)?.Id;
        var wasLoading = loading; loading = true;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; var selected = RowEdition(row.Id, row.GameId, row.Platform == "psp");
            var automatic = state.Media.Where(m => m.EditionId == selected.Id).Select(m => automaticAssets.GetValueOrDefault(m.Id)).FirstOrDefault(a => a?.Cover != null);
            var cover = SystemParameters.HighContrast ? null : artwork.Merge(EditionArtwork(row.Id, selected.Id), automatic).Cover;
            if (!ReferenceEquals(cover, row.Cover)) rows[i] = row with { Cover = cover };
        }
        GameList.SelectedItem = rows.FirstOrDefault(r => r.Id == selectedId);
        loading = wasLoading;
        UpdateArtwork();
    }
}
