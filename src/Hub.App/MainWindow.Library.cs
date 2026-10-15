using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hub.Core;
using Microsoft.Win32;

namespace Hub.App;

public sealed record GameRow(string Id, string GameId, string Platform, string Title, string State, Brush StateBrush, string Group, BitmapSource? Cover)
{
    public string AccessibleName => Title + ", " + Group + ", " + State;
}

public partial class MainWindow
{
    private AppearancePreferences appearance = new();
    private LibraryArtwork artwork = null!;
    private bool applyingLayout;
    private string acceptedConsole = "all", acceptedGeneration = "all";
    private static string RegionName(string region) => region switch
    { "US" => "North America", "EU" => "Europe", "JP" => "Japan", "ASIA" => "Asia", _ => region };
    private static string ConsoleName(string platform) => platform switch
    {
        "ps1" => "PlayStation", "ps2" => "PlayStation 2", "psp" => "PSP", "ps3" => "PlayStation 3", "x360" => "Xbox 360", "ps3-x360" => "PlayStation 3 / Xbox 360", _ => platform
    };
    private void InitializeLibrary()
    {
        try { appearance = JsonData.Read<AppearancePreferences>(paths.Managed("config", "appearance.json")) ?? new(); }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or HubException) { appearance = new(); }
        appearance.Normalize(); artwork = new(paths);
        GroupBox.ItemsSource = new Choice[] { new("console", "By console"), new("generation", "By generation") };
        GroupBox.SelectedValue = appearance.GroupBy;
        ConsoleFilter.ItemsSource = new Choice[] { new("all", "All consoles"), new("ps1", "PlayStation"), new("ps2", "PlayStation 2"), new("ps3", "PlayStation 3"), new("x360", "Xbox 360"), new("psp", "PSP") };
        ConsoleFilter.SelectedValue = "all";
        GenerationFilter.ItemsSource = new[] { new Choice("all", "All generations") }.Concat(Enumerable.Range(1, 5).Select(n => new Choice(n.ToString(), "Generation " + n))).ToArray();
        GenerationFilter.SelectedValue = "all";
        ApplyLibraryView(); ApplyLibrarySplit();
        SystemParameters.StaticPropertyChanged += ContrastChanged;
        Closed += (_, _) => SystemParameters.StaticPropertyChanged -= ContrastChanged;
    }
    private void ContrastChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if(presentationClosed||Dispatcher.HasShutdownStarted||e.PropertyName is not (nameof(SystemParameters.HighContrast) or nameof(SystemParameters.ClientAreaAnimation)))return;
        _=Dispatcher.InvokeAsync(()=>{if(!presentationClosed){ApplyTitlebarColors();UpdateArtwork();RefreshGames();}});
    }
    private void SaveAppearance()
    {
        appearance.Normalize();
        try { JsonData.WriteAtomic(paths.Managed("config", "appearance.json"), appearance); }
        catch (IOException) { StatusText.Text = "The layout could not be saved. Check the Hub folder is writable."; }
    }
    private void ApplyLibrarySplit()
    {
        if (LibraryPane == null || applyingLayout) return;
        applyingLayout = true;
        var collapsed = appearance.LibraryMode == "collapsed";
        var expanded = appearance.LibraryMode == "expanded";
        LibraryPane.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        LibraryRail.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
        LibraryDivider.IsEnabled = !collapsed;
        LibraryColumn.MinWidth = collapsed ? 48 : 280;
        var total = Math.Max(600, CollectionView.ActualWidth - 7);
        var fraction = expanded ? .68 : appearance.LibraryFraction;
        var width = collapsed ? 48 : Math.Clamp(total * fraction, 280, total - 320);
        LibraryColumn.Width = new GridLength(width);
        SceneColumn.Width = new GridLength(1, GridUnitType.Star);
        applyingLayout = false;
    }
    private void LibrarySizeChanged(object sender, SizeChangedEventArgs e) { ApplyLibrarySplit(); AdaptHeight(); }
    private void LibraryDragCompleted(object sender, DragCompletedEventArgs e)
    {
        appearance.LibraryFraction = LibraryColumn.ActualWidth / Math.Max(1, CollectionView.ActualWidth - 7);
        appearance.LibraryMode = "normal"; SaveAppearance();
    }
    private void RememberKeyboardSplit(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right) LibraryDragCompleted(sender, new DragCompletedEventArgs(0, 0, false));
    }
    private void CollapseLibrary(object sender, RoutedEventArgs e)
    {
        appearance.LibraryMode = appearance.LibraryMode == "collapsed" ? "normal" : "collapsed";
        ApplyLibrarySplit(); SaveAppearance();
        if (appearance.LibraryMode == "collapsed") ((Button)LibraryRail.Children[0]).Focus(); else GameList.Focus();
    }
    private void ExpandLibrary(object sender, RoutedEventArgs e)
    { appearance.LibraryMode = appearance.LibraryMode == "expanded" ? "normal" : "expanded"; ApplyLibrarySplit(); SaveAppearance(); }
    private void ResetLibrarySplit(object sender, MouseButtonEventArgs e)
    { appearance.LibraryFraction = .36; appearance.LibraryMode = "normal"; ApplyLibrarySplit(); SaveAppearance(); e.Handled = true; }
    private void ApplyLibraryView()
    {
        var list = appearance.View == "list";
        GameList.ItemTemplate = (DataTemplate)FindResource(list ? "ListTemplate" : "CoverTemplate");
        GameList.ItemsPanel = (ItemsPanelTemplate)FindResource(list ? "ListPanel" : "CoverPanel");
        GridButton.Background = (Brush)FindResource(list ? "Ground" : "Raised");
        ListButton.Background = (Brush)FindResource(list ? "Raised" : "Ground");
        GridButton.BorderBrush = (Brush)FindResource(list ? "Rule" : "Accent");
        ListButton.BorderBrush = (Brush)FindResource(list ? "Accent" : "Rule");
    }
    private void GridViewClicked(object sender, RoutedEventArgs e) { appearance.View = "grid"; ApplyLibraryView(); SaveAppearance(); }
    private void ListViewClicked(object sender, RoutedEventArgs e) { appearance.View = "list"; ApplyLibraryView(); SaveAppearance(); }
    private void GroupingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || GroupBox.SelectedValue is not string value || !ConfirmAdvanced()) return;
        appearance.GroupBy = value; SaveAppearance(); RefreshGames();
    }
    private void ToggleFilters(object sender, RoutedEventArgs e)
    { FilterPanel.Visibility = FilterPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; if (FilterPanel.IsVisible) ConsoleFilter.Focus(); }
    private void FilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || operation != null) return;
        if (!ConfirmAdvanced())
        { loading = true; ConsoleFilter.SelectedValue = acceptedConsole; GenerationFilter.SelectedValue = acceptedGeneration; loading = false; return; }
        acceptedConsole = ConsoleFilter.SelectedValue as string ?? "all"; acceptedGeneration = GenerationFilter.SelectedValue as string ?? "all";
        RefreshGames();
    }
    private void ClearFilters(object sender, RoutedEventArgs e)
    {
        if (!ConfirmAdvanced()) return;
        loading = true; ConsoleFilter.SelectedValue = GenerationFilter.SelectedValue = "all"; SearchBox.Text = ""; ImportedOnly.IsChecked = false; loading = false;
        acceptedConsole = acceptedGeneration = "all"; acceptedSearch = ""; acceptedImportedOnly = false; RefreshGames();
    }
    private void RefreshGames()
    {
        if (GameList == null || artwork == null) return;
        var prior = (GameList.SelectedItem as GameRow)?.Id ?? appearance.SelectedGameId;
        loading = true; rows.Clear();
        var console = ConsoleFilter.SelectedValue as string ?? "all";
        var generation = GenerationFilter.SelectedValue as string ?? "all";
        var candidates = catalog.Games.Where(g => generation == "all" || g.Generation.ToString() == generation);
        foreach (var g in candidates)
        {
            var editions = catalog.Editions.Where(e => e.GameId == g.Id).ToArray();
            // Portable releases get a separate library entry; console backend choices remain editions.
            foreach (var portable in new[] { false, true })
            {
                if (!(g.Title + (portable ? " Portable" : "")).Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase)
                    && !(g.ShortTitle + (portable ? " Portable" : "")).Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase)) continue;
                var group = editions.Where(e => (e.Platform == "psp") == portable).ToArray();
                if (group.Length == 0 || console != "all" && !group.Any(e => e.Platform == console)) continue;
                var platform = portable ? "psp" : group.Any(e => e.Platform == "ps3") && group.Any(e => e.Platform == "x360") ? "ps3-x360" : group[0].Platform;
                var key = portable ? g.Id + "-psp" : g.Id;
                var imported = state.Media.Where(m => group.Any(e => e.Id == m.EditionId)).ToList();
                if (ImportedOnly.IsChecked == true && imported.Count == 0) continue;
                var ready = imported.Any(m => m.EditionId != null && !planner.Diagnose(m.EditionId, state).Any(d => d.Status is "missing" or "error"));
                var selectedEdition = RowEdition(key, g.Id, portable);
                var image = artwork.Merge(EditionArtwork(key, selectedEdition.Id), imported.Where(m => m.EditionId == selectedEdition.Id).Select(m => automaticAssets.GetValueOrDefault(m.Id)).FirstOrDefault(a => a?.Cover != null));
                rows.Add(new(key, g.Id, platform, g.ShortTitle + (portable ? " Portable" : ""), ready ? "Ready to play" : imported.Count > 0 ? "Setup needed" : "Not added", (Brush)FindResource(ready ? "Good" : "Muted"), appearance.GroupBy == "generation" ? "Generation " + g.Generation : ConsoleName(platform), SystemParameters.HighContrast ? null : image.Cover));
            }
        }
        var sorted = rows.OrderBy(r => appearance.GroupBy == "generation" ? r.Group : r.Platform switch { "ps1" => "1", "ps2" => "2", "ps3-x360" => "3", "psp" => "4", _ => "5" }).ToArray();
        rows.Clear(); foreach (var row in sorted) rows.Add(row);
        CollectionCount.Text = $"{rows.Count} titles · {rows.Count(r => r.State != "Not added")} added";
        var filterCount = (console != "all" ? 1 : 0) + (generation != "all" ? 1 : 0) + (ImportedOnly.IsChecked == true ? 1 : 0) + (SearchBox.Text.Length > 0 ? 1 : 0);
        FilterButton.Content = filterCount > 0 ? $"Filter ({filterCount})" : "Filter";
        EmptySearch.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        loading = false; GameList.SelectedItem = rows.FirstOrDefault(r => r.Id == prior) ?? rows.FirstOrDefault();
        SceneActions.Visibility = rows.Count == 0 ? Visibility.Hidden : Visibility.Visible;
        if (rows.Count == 0) { game = null; edition = null; DetailPane.Visibility = Visibility.Collapsed; SceneActions.IsEnabled = true; ArtworkButton.IsEnabled = true; UpdateArtwork(); }
        AdaptHeight();
    }
    private void AdaptHeight()
    {
        if (GameTitle == null) return;
        var compact = ActualHeight > 0 && ActualHeight < 650;
        var narrowHeader = ActualWidth < 1050;
        HeaderBrand.Width = 201;
        HeaderBrand.Margin = new Thickness(0, 0, narrowHeader ? 15 : 25, 0);
        EmulatorsNavLabel.Visibility = HelpNavLabel.Visibility = narrowHeader ? Visibility.Collapsed : Visibility.Visible;
        GameTitle.FontSize = compact || ScenePane.ActualWidth < 480 ? 28 : 38;
        GameTitle.LineHeight = compact || ScenePane.ActualWidth < 480 ? 34 : 45;
        SceneActions.Margin = new Thickness(28, 15, 24, compact ? 20 : 36);
        TitleShade.Height = Math.Max(160, SceneActions.ActualHeight + SceneActions.Margin.Bottom + 70);
        GameSubtitle.FontSize = compact || ScenePane.ActualWidth < 480 ? 21 : 29;
        GameSubtitle.LineHeight = compact || ScenePane.ActualWidth < 480 ? 27 : 35;
        var coverWidth = LibraryColumn.ActualWidth < 330 ? 96d : 108d;
        if (Resources["CoverWidth"] is not double previous || previous != coverWidth)
        { Resources["CoverWidth"] = coverWidth; Resources["CoverHeight"] = coverWidth * 1.324; }
        LayoutBackdrop();
    }
    private void OpenGameSettings(object sender, RoutedEventArgs e)
    { if (game == null) return; DetailPane.Visibility = Visibility.Visible; SceneActions.IsEnabled = false; ArtworkButton.IsEnabled = false; CloseSettingsButton.Focus(); UpdateVideoPlayback(); }
    private void CloseGameSettings(object sender, RoutedEventArgs e)
    { if (!ConfirmAdvanced()) return; DetailPane.Visibility = Visibility.Collapsed; SceneActions.IsEnabled = true; ArtworkButton.IsEnabled = true; GameSettingsButton.Focus(); UpdateVideoPlayback(); }
    private void WindowKeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Tab) LibraryDivider.Tag = "keyboard"; if (e.Key == Key.Escape && DetailPane.Visibility == Visibility.Visible) { CloseGameSettings(sender, e); e.Handled = true; } }
    private void ChooseArtwork(object sender, RoutedEventArgs e)
    {
        if (GameList.SelectedItem is not GameRow row) return;
        Utility("Artwork", row.Title);
        ActionButton("Download covers", async (_, _) =>
        {
            await StartCovers(true, manual: true);
            StatusText.Text = state.Offline ? "Offline: showing cached covers." : covers?.Cached(edition?.Id ?? "") != null ? "Covers updated" : "Cover download unavailable. Check your connection and try again.";
        });
        if (edition != null && covers?.Resolve(edition.Id) is { } source)
        {
            ActionButton("Cover source · " + source.Asset.Credit, (_, _) => OpenUrl(source.Asset.SourceUrl));
            if (source.Note != null) UtilityContent.Children.Add(Body(source.Note));
        }
        ActionButton("Choose cover image…", (_, _) => ImportArtwork(row, "cover"));
        ActionButton("Choose background image…", (_, _) => ImportArtwork(row, "background"));
        ActionButton("Choose transparent title logo…", (_, _) => ImportArtwork(row, "logo"));
        UtilityContent.Children.Add(Body("A genuine transparent logo replaces the title above Play. Console menu overlays are not treated as game logos; otherwise the two-line title is used."));
        ActionButton("Choose background video…", (_, _) => ImportArtwork(row, "video"));
        if (PresentationMedia is { } registered)
        {
            ActionButton("Read artwork from game again", async (_, _) =>
            {
                var originalPage=UtilityContent.Children.Count>0?UtilityContent.Children[0]:null;
                try
                {
                    CancelAssetRead();automaticAssets.Remove(registered.Id);
                    await new GameAssetReader(paths).ReadAsync(registered, true, presentationLifetime.Token);
                    var found = await new MediaPreviewService(paths,catalog,downloader).ReadAsync(registered,state.Offline,presentationLifetime.Token);
                    if(presentationClosed)return;
                    automaticAssets[registered.Id] = found; assetReads.Remove(registered.Id);UpdateCoverRows();
                    if(UtilityView.Visibility==Visibility.Visible&&UtilityContent.Children.Count>0&&ReferenceEquals(originalPage,UtilityContent.Children[0]))
                    {ShowCollection(this,new());StatusText.Text=found.Notes.FirstOrDefault()??"Game artwork updated";}
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) when (ex is IOException or HubException) { if(!presentationClosed)StatusText.Text = "Game artwork could not be read."; }
            });
            if (automaticAssets.TryGetValue(registered.Id, out var found))
                foreach (var note in found.Notes) UtilityContent.Children.Add(Body(note));
        }
        UtilityContent.Children.Add(Body("Custom artwork is stored in this Hub. Backgrounds and muted videos appear for added games."));
        ActionButton("Back to library", ShowCollection);
    }
    private void ImportArtwork(GameRow row, string kind)
    {
        var dialog = new OpenFileDialog { Title = "Choose " + kind, Filter = kind == "video" ? "Videos|*.mp4;*.m4v;*.wmv;*.avi;*.mpg;*.mpeg" : "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp" };
        if (dialog.ShowDialog(this) != true) return;
        try { artwork.Import(row.Id, dialog.FileName, kind); RefreshGames(); UpdateArtwork(); ShowCollection(this, new()); StatusText.Text = "Artwork updated"; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or FormatException or HubException) { MessageBox.Show(this, ex.Message, "Artwork could not be imported"); }
    }
    private void ShowAppearance(object sender, RoutedEventArgs e)
    {
        if (operation != null) return;
        Utility("Settings", "Appearance");
        var downloadCovers = new CheckBox { Content = "Download game covers automatically", IsChecked = appearance.DownloadCovers };
        downloadCovers.Click += async (_, _) => { appearance.DownloadCovers = downloadCovers.IsChecked == true; SaveAppearance(); await StartCovers(true); };
        UtilityContent.Children.Add(downloadCovers);
        var ambient = new CheckBox { Content = "Use cover colors for ambient lighting", IsChecked = appearance.Ambient };
        ambient.Click += (_, _) => { appearance.Ambient = ambient.IsChecked == true; SaveAppearance(); UpdateArtwork(); };
        UtilityContent.Children.Add(ambient);
        var videos = new CheckBox { Content = "Play muted background videos", IsChecked = appearance.VideoBackgrounds };
        videos.Click += (_, _) => { appearance.VideoBackgrounds = videos.IsChecked == true; SaveAppearance(); UpdateArtwork(); };
        UtilityContent.Children.Add(videos);
        ActionButton("Reset library width", (_, _) => { appearance.LibraryFraction = .36; appearance.LibraryMode = "normal"; ApplyLibrarySplit(); SaveAppearance(); ShowCollection(this, new()); });
        Section("Game storage", paths.GameStorageRoot + "\nNew archive imports and prepared copies go here. Existing copies stay in their current locations.");
        ActionButton("Change game storage…", ChooseGameStorage);
        ActionButton("About and legal", ShowAbout);
        ActionButton("Back to library", ShowCollection);
    }
    private void ShowAbout(object sender, RoutedEventArgs e)
    {
        if(operation!=null||!ConfirmAdvanced())return;
        Utility("About and legal", "Armored Core Hub · " + typeof(MainWindow).Assembly.GetName().Version?.ToString(3));
        var tabs=new TabControl();var legal=new StackPanel();var licenses=new StackPanel();var about=new StackPanel();
        tabs.Items.Add(new TabItem{Header="Legal",Content=legal});tabs.Items.Add(new TabItem{Header="Licenses",Content=licenses});tabs.Items.Add(new TabItem{Header="About",Content=about});tabs.SelectedIndex=0;UtilityContent.Children.Add(tabs);
        legal.Children.Add(new TextBlock{Text="Anti-piracy notice",FontSize=24,Margin=new(0,16,0,2)});
        legal.Children.Add(Body("The Hub does not distribute commercial games or console BIOS files, provide unauthorized game-download links, or bypass copy protection."));
        legal.Children.Add(Body("Use only games, system software, artwork and patches that you have lawful permission to use. A disc or ownership checkbox does not give another party permission to distribute a copy."));
        legal.Children.Add(Body("Game and BIOS import uses your local files. Emulator downloads use upstream releases; official PS3 system software comes from Sony. Game media and BIOS files are excluded from Hub releases."));
        void Link(StackPanel panel,string title,RoutedEventHandler action){var button=new Button{Content=title,HorizontalAlignment=HorizontalAlignment.Left};button.Click+=action;panel.Children.Add(button);}
        foreach(var backend in catalog.Backends)Link(licenses,backend.Name+" · "+backend.License,(_,_)=>OpenUrl(backend.LicenseUrl));
        Link(licenses,"Cover artwork · GameTDB",(_,_)=>OpenUrl("https://www.gametdb.com"));
        void Document(string relative){var file=Path.Combine(AppContext.BaseDirectory,relative);if(File.Exists(file))System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file){UseShellExecute=true});}
        Link(licenses,"Cover artwork sources and credits",(_,_)=>Document("docs/artwork-credits.md"));Link(licenses,"Open third-party notices",(_,_)=>Document("THIRD-PARTY-NOTICES.md"));Link(legal,"Open legal notice",(_,_)=>Document("LEGAL.md"));
        about.Children.Add(Body("Independent project, unaffiliated with FromSoftware, Bandai Namco, Sony, Microsoft or emulator authors. Game names identify supported titles; trademarks and artwork remain the property of their owners."));
        ActionButton("Back to settings", ShowAppearance);
    }
}
