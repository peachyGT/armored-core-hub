using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hub.Core;

namespace Hub.App;

public partial class MainWindow
{
    private async Task CheckLibraryAppearance(Func<string, Task> capture, List<string> checks)
    {
        ClearFilters(this, new());
        ConsoleFilter.SelectedValue = "psp";
        if (rows.Count != 4 || rows.Any(r => r.Platform != "psp") || edition?.Platform != "psp") throw new Exception("Portable edition filter");
        ClearFilters(this, new()); GroupBox.SelectedValue = "generation"; GenerationFilter.SelectedValue = "4";
        if (rows.Count != 2 || rows.Any(r => r.Group != "Generation 4")) throw new Exception("Generation grouping and filtering");
        checks.Add("Console and generation filters select the correct editions");
        ClearFilters(this, new()); GroupBox.SelectedValue = "console";
        GameList.SelectedItem = rows.Single(r => r.Id == "acfa");
        Width = 1280; Height = 720; appearance.LibraryFraction = .36; appearance.LibraryMode = "normal"; ApplyLibrarySplit();
        await capture("hybrid-1280");
        if (LibraryColumn.ActualWidth >= SceneColumn.ActualWidth || SceneColumn.ActualWidth < 320) throw new Exception("Default library must be smaller than scene");
        var oldWidth = LibraryColumn.ActualWidth;
        CollapseLibrary(this, new()); await capture("library-collapsed");
        if (LibraryColumn.ActualWidth != 48 || LibraryPane.Visibility != Visibility.Collapsed) throw new Exception("Library collapse");
        CollapseLibrary(this, new()); UpdateLayout();
        if (Math.Abs(LibraryColumn.ActualWidth - oldWidth) > 2) throw new Exception("Library restore width");
        ExpandLibrary(this, new()); await capture("library-expanded");
        if (LibraryColumn.ActualWidth <= SceneColumn.ActualWidth || SceneColumn.ActualWidth < 319) throw new Exception("Library expansion");
        ExpandLibrary(this, new());
        LibraryColumn.Width = new GridLength(490); UpdateLayout(); LibraryDragCompleted(this, new DragCompletedEventArgs(0, 0, false));
        var saved = JsonData.Read<AppearancePreferences>(paths.Managed("config", "appearance.json"));
        if (Math.Abs(saved.LibraryFraction - 490d / (CollectionView.ActualWidth - 7)) > .01) throw new Exception("Resized library preference");
        checks.Add("Resize, collapse, expansion and restored width persist");
        ListViewClicked(this, new()); Width = 960; Height = 540; await capture("hybrid-list-960");
        if (SceneColumn.ActualWidth < 319 || LibraryColumn.ActualWidth < 279) throw new Exception("Small window column bounds");
        ToggleFilters(this, new()); await capture("hybrid-filters-960"); ToggleFilters(this, new());
        OpenGameSettings(this, new()); DetailsTabs.SelectedItem = AdvancedTab; await capture("hybrid-advanced-960");
        if (DetailsTabs.ActualHeight < 80 || SceneActions.IsEnabled || ArtworkButton.IsEnabled) throw new Exception("Settings viewport or covered controls");
        CloseGameSettings(this, new());
        checks.Add("Small-window list, filters and settings retain usable space");
        BitmapSource Solid(byte r, byte g, byte b) => BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat(new byte[] { b, g, r, 255 }, 4).SelectMany(x => x).ToArray(), 8);
        var red = LibraryArtwork.SampleTint(Solid(200, 40, 20)); var blue = LibraryArtwork.SampleTint(Solid(20, 80, 190));
        if (red.R < red.B || blue.B < blue.R || LibraryArtwork.SampleTint(Solid(0, 0, 0)) != LibraryArtwork.Neutral) throw new Exception("Artwork color extraction");
        appearance.Ambient = false; UpdateArtwork(); if (AmbientLayer.Visibility != Visibility.Collapsed) throw new Exception("Ambient disabled state");
        appearance.Ambient = true; UpdateArtwork();
        checks.Add("Cover sampling distinguishes warm/cool art and ambient lighting can be disabled");
        var fixturePaths = new HubPaths(paths.Managed("cache", "artwork-test"));
        var fixture = fixturePaths.Managed("source.png");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Solid(200, 40, 20)));
        using (var stream = File.Create(fixture)) encoder.Save(stream);
        var localArt = new LibraryArtwork(fixturePaths); localArt.Import("fixture", fixture, false); localArt.Import("fixture", fixture, false);
        if (localArt.Get("fixture", "fixture").Cover == null || Directory.GetFiles(fixturePaths.Managed("artwork"), "*.png").Length != 1 || !File.Exists(fixture)) throw new Exception("Local artwork import or duplicate handling");
        JsonData.WriteAtomic(fixturePaths.Managed("artwork", "index.json"), new Dictionary<string, ArtworkEntry> { ["bad"] = new() { Cover = "../source.png" } });
        if (new LibraryArtwork(fixturePaths).Get("bad", "bad").Cover != null) throw new Exception("Artwork traversal accepted");
        File.WriteAllText(fixturePaths.Managed("artwork", "index.json"), "invalid json");
        if (new LibraryArtwork(fixturePaths).Get("bad", "bad").Cover != null) throw new Exception("Damaged artwork index fallback");
        checks.Add("Artwork import preserves originals, deduplicates copies and rejects unsafe paths");
        var invalid = new AppearancePreferences { LibraryFraction = double.NaN, LibraryMode = "bad", View = "bad", GroupBy = "bad" }; invalid.Normalize();
        if (invalid.LibraryFraction != .36 || invalid.LibraryMode != "normal" || invalid.View != "grid") throw new Exception("Invalid appearance preferences");
        checks.Add("Invalid appearance preferences recover to safe defaults");
        GridViewClicked(this, new()); appearance.LibraryFraction = .36; appearance.LibraryMode = "normal"; ApplyLibrarySplit();
        Width = 1320; Height = 880; await capture("hybrid-final");
    }
}
