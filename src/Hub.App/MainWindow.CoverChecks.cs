using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hub.Core;

namespace Hub.App;

public partial class MainWindow
{
    private async Task CheckCovers(List<string> checks)
    {
        var savedCovers = covers; var savedArtwork = artwork;
        var originalPreferences = new Dictionary<string, string>(state.PreferredEditions);
        var fixturePaths = new HubPaths(paths.Managed("cache", "cover-ui-fixture-" + Guid.NewGuid().ToString("N")));
        var fixture = new CoverCatalog();
        foreach (var id in new[] { "acfa-ps3-blus30187", "acfa-ps3-bljm60066" })
        {
            var color = id.EndsWith("30187") ? new byte[] { 25, 40, 190, 255 } : new byte[] { 190, 40, 25, 255 };
            var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, Enumerable.Range(0, 4).SelectMany(_ => color).ToArray(), 8);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = new MemoryStream(); encoder.Save(stream); var bytes = stream.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            Directory.CreateDirectory(fixturePaths.Managed("cache", "objects")); File.WriteAllBytes(fixturePaths.Managed("cache", "objects", hash), bytes);
            fixture.Assets.Add(new(id, "acfa", "ps3", id.EndsWith("30187") ? "US" : "JP", "https://raw.githubusercontent.com/fixture/cover.png", hash, 128, 128, bytes.Length, "Synthetic", "https://example.com", "MIT"));
            fixture.Editions.Add(new(id, id, null, null));
        }
        try
        {
            covers = new(fixture, new(fixturePaths, http)); artwork = new(fixturePaths);
            foreach (var e in fixture.Editions) await covers.FetchAsync(e.EditionId, true);
            ClearFilters(this, new()); GameList.SelectedItem = rows.Single(r => r.Id == "acfa");
            EditionBox.SelectedValue = "acfa-ps3-blus30187"; UpdateCoverRows();
            var us = SceneCover.Source;
            EditionBox.SelectedValue = "acfa-ps3-bljm60066"; UpdateCoverRows();
            if (SceneCover.Source == null || ReferenceEquals(us, SceneCover.Source) || !ReferenceEquals(SceneCover.Source, rows.Single(r => r.Id == "acfa").Cover)) throw new Exception("Edition cover did not update in both panes");
            var custom = fixturePaths.Managed("custom.png"); File.Copy(covers.Cached("acfa-ps3-blus30187")!, custom, true);
            artwork.Import("acfa", custom, false); UpdateCoverRows();
            if (LibraryArtwork.SampleTint((BitmapSource)SceneCover.Source).R < 100) throw new Exception("Custom cover lost priority");
            if (artwork.Get("acfa-psp", "acfa-psp").Cover != null) throw new Exception("Custom cover crossed platforms");
            checks.Add("Edition changes update library and detail art; custom covers win without crossing platforms");
            state.PreferredEditions["ac3"] = catalog.Editions.First(e => e.GameId == "ac3" && e.Platform == "ps2").Id;
            GameList.SelectedItem = rows.Single(r => r.Id == "ac3-psp"); EditionBox.SelectedValue = "ac3-psp-uljm05492";
            GameList.SelectedItem = rows.Single(r => r.Id == "ac3");
            if (edition?.Platform != "ps2" || RowEdition("ac3-psp", "ac3", true).Id != "ac3-psp-uljm05492") throw new Exception("Portable edition selection was not independent");
            checks.Add("Console and portable edition preferences remain independent");
        }
        finally
        {
            covers = savedCovers; artwork = savedArtwork; state.PreferredEditions.Clear();
            foreach (var entry in originalPreferences) state.PreferredEditions.Add(entry.Key, entry.Value);
            Save(); RefreshGames(); UpdateArtwork();
        }
    }
}
