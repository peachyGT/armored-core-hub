using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation.Peers;
using System.Windows.Threading;
using Hub.Core;
using Microsoft.Win32;

namespace Hub.App;

public sealed record Choice(string Id, string Label);

public partial class MainWindow : Window
{
    private readonly HubPaths paths;
    private readonly Catalog catalog;
    private readonly StateStore store;
    private readonly HubState state;
    private readonly HttpClient http = ProviderDownloader.CreateHttpClient();
    private readonly MediaService media;
    private readonly FirmwareService firmware;
    private readonly ProviderDownloader downloader;
    private readonly RuntimeService runtimes;
    private readonly SaveService saves;
    private readonly LaunchPlanner planner;
    private readonly LaunchService launcher;
    private readonly ObservableCollection<GameRow> rows = [];
    private bool loading = true;
    private CancellationTokenSource? operation;
    private TaskCompletionSource? operationCompletion;
    private readonly bool suppressOperationDialogs;
    private Edition? edition;
    private Game? game;
    private string advancedContext = "";
    private string advancedSavedText = "";
    private string acceptedSearch = "";
    private bool acceptedImportedOnly;
    private IProgress<ProgressInfo> Progress => new Progress<ProgressInfo>(p =>
    {
        if(presentationClosed)return;
        StatusText.Text = p.Message + (p.Total > 0 ? p.Unit == "files" ? $" · {p.Completed} / {p.Total} files" : $" · {p.Completed / 1048576d:0.0} / {p.Total / 1048576d:0.0} MiB" : "");
        ActivityProgress.IsIndeterminate = p.Total is null or <= 0;
        if (p.Total > 0) ActivityProgress.Value = 100d * p.Completed / p.Total.Value;
    });

    public MainWindow(HubPaths paths, string[] args)
    {
#if HUB_DIAGNOSTICS
        suppressOperationDialogs=args.Contains("--self-test")||args.Contains("--resource-fixture");
#else
        suppressOperationDialogs=false;
#endif
        this.paths = paths; catalog = CatalogStore.Load(paths.Managed("manifests")); store = new(paths); state = store.Load();
        foreach(var selection in state.Selections.Values) ControlProfiles.MigrateLegacy(selection.Controls);
        new PatchService(paths, catalog).LoadInstalled();
        media = new(paths, catalog); firmware = new(paths, catalog); saves = new(paths); planner = new(paths, catalog);
        downloader = new(paths, http); runtimes = new(paths, catalog, downloader, http); launcher = new(paths, catalog);
        InitializeComponent(); InitializeLibrary(); InitializePresentation(); InitializeCovers(); InitializeWindowChrome(); LibraryDivider.KeyUp += RememberKeyboardSplit;
        Width = Math.Min(Width, SystemParameters.WorkArea.Width);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
        SizeChanged += (_, _) => AdaptHeight(); ScenePane.SizeChanged += (_, _) => AdaptHeight(); SceneActions.SizeChanged += (_, _) => AdaptHeight();
        InitializeLifecycle(args);
        GameList.ItemsSource = rows;
        CollectionViewSource.GetDefaultView(rows).GroupDescriptions.Add(new PropertyGroupDescription("Group"));
        OfflineBox.IsChecked = state.Offline;
        loading = false; RefreshGames();
        if (store.RecoveryNotice != null) StatusText.Text = store.RecoveryNotice;
        Loaded += async (_, _) =>
        {
            CheckControllers(this, new());
#if HUB_DIAGNOSTICS
            await StartCovers(!args.Contains("--self-test"));
            if (args.Contains("--self-test")) await SelfTest(args);
#else
            await StartCovers(true);
#endif
        };
    }
    private GameSelection Current
    {
        get
        {
            if (edition == null) throw new HubException("Choose an edition first.");
            if (!state.Selections.TryGetValue(edition.Id, out var value))
            {
                value = new() { EditionId = edition.Id, ProfileId = edition.PreferredProfile };
                state.Selections.Add(edition.Id, value);
            }
            return value;
        }
    }
    private CompatibilityProfile Profile => catalog.Profiles.Single(x => x.Id == (Current.ProfileId ?? edition!.PreferredProfile));
    private Backend Backend => catalog.Backends.Single(x => x.Id == Profile.BackendId);
    private void Save() { store.Save(state); }
    private void SearchChanged(object sender, RoutedEventArgs e)
    {
        if (loading || operation != null) return;
        if (!ConfirmAdvanced())
        {
            loading = true; SearchBox.Text = acceptedSearch; ImportedOnly.IsChecked = acceptedImportedOnly; loading = false; return;
        }
        acceptedSearch = SearchBox.Text; acceptedImportedOnly = ImportedOnly.IsChecked == true; RefreshGames();
    }
    private void GameChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || GameList.SelectedItem is not GameRow row) return;
        if (!ConfirmAdvanced())
        {
            loading = true; GameList.SelectedItem = rows.FirstOrDefault(x => x.GameId == game?.Id); loading = false; return;
        }
        game = catalog.Games.Single(x => x.Id == row.GameId); appearance.SelectedGameId = row.Id;
        loading = true;
        SettingsGameTitle.Text = game.Title + (row.Platform == "psp" ? " Portable" : "");
        var editions = catalog.Editions.Where(x => x.GameId == game.Id && (x.Platform == "psp") == (row.Platform == "psp")).OrderBy(x => x.Platform == "psp").ThenBy(x => x.Region == "US" ? 0 : x.Region == "EU" ? 1 : 2).ThenBy(x => x.Disc).ToList();
        EditionBox.ItemsSource = editions.Select(x => new Choice(x.Id, ConsoleName(x.Platform) + " · " + RegionName(x.Region) + (x.Disc > 1 ? " · Disc " + x.Disc : ""))).ToArray();
        var preferred = RowEdition(row.Id, row.GameId, row.Platform == "psp").Id;
        EditionBox.SelectedValue = editions.FirstOrDefault(x => x.Id == preferred)?.Id ?? editions.First().Id;
        loading = false; EditionChanged(this, new SelectionChangedEventArgs(SelectorEvent(), Array.Empty<object>(), Array.Empty<object>()));
    }
    private static RoutedEvent SelectorEvent() => System.Windows.Controls.Primitives.Selector.SelectionChangedEvent;
    private void EditionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || EditionBox.SelectedValue is not string id) return;
        if (!ConfirmAdvanced()) { loading = true; EditionBox.SelectedValue = edition?.Id; loading = false; return; }
        edition = catalog.Editions.Single(x => x.Id == id);
        state.PreferredEditions[(GameList.SelectedItem as GameRow)?.Id ?? edition.GameId] = id;
        _ = Current; Save(); RefreshDetail(); UpdateCoverRows(); _ = FetchCover(id);
    }
    private void RefreshDetail()
    {
        if (edition == null) return;
        loading = true;
        var diagnostics = planner.Diagnose(edition.Id, state);
        Readiness.ItemsSource = diagnostics;
        var missing = diagnostics.FirstOrDefault(x => x.Status is "missing" or "error");
        PlayLabel.Text = missing?.Repair switch { "media" => "Add game", "runtime" => "Set up " + Backend.Name, "firmware" => "Finish setup", _ => missing != null ? "Repair setup" : "Play" };
        PlayIcon.Data = Geometry.Parse(missing == null ? "M1,0 L10,6 1,12 Z" : "M0,6 L12,6 M6,0 L6,12");
        System.Windows.Automation.AutomationProperties.SetName(PlayButton, PlayLabel.Text);
        SceneEdition.Text = game!.Year + "   ·   " + ConsoleName(edition.Platform) + " · " + RegionName(edition.Region); SceneStatus.Text = missing == null ? "Ready to play" : missing.Repair == "media" ? "Not added" : "Setup needed";
        try{SceneStatus.Text+=" · "+PlaytimeService.Format(new PlaytimeService(paths,catalog).Read(edition,state))+" played";}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or HubException){SceneStatus.Text+=" · playtime unavailable";}
        SceneStatus.ToolTip="Time played for this edition. Hub session time includes menus/loading/pauses; setup is excluded. Available native history is imported once.";
        CompatibilityNotes.Text = string.Join("\n\n", edition.Warnings.Concat(Profile.Warnings));
        EnhancementBox.ItemsSource = catalog.Enhancements.Where(x => x.ProfileId == Profile.Id).ToArray();
        EnhancementBox.SelectedValue = Current.EnhancementId ?? Profile.DefaultEnhancement;
        SetEnhancementNotes();
        // WPF caches enumerable views. Materialize choices so a retained view cannot capture this window.
        ProfileBox.ItemsSource = catalog.Profiles.Where(x => x.EditionId == edition.Id).Select(x => new Choice(x.Id,
            catalog.Backends.Single(b => b.Id == x.BackendId).Name + " · " + x.Version + (x.TestStatus switch
            {
                "locally-tested" => " · tested",
                "locally-sampled" => " · sampled gameplay",
                _ => " · upstream-informed"
            }))).ToArray();
        ProfileBox.SelectedValue = Profile.Id;
        MediaBox.ItemsSource = state.Media.Where(x => x.EditionId == edition.Id).Select(x => new Choice(x.Id, Path.GetFileName(paths.Resolve(x.Path)) + " · " + LaunchPlanner.ConfidenceText(x.Confidence) + " · " + x.Id[..Math.Min(8,x.Id.Length)])).ToArray();
        MediaBox.SelectedValue = Current.MediaId;
        ForgetMediaButton.IsEnabled = Current.MediaId != null;
        BuildBox.ItemsSource = state.Builds.Where(x => x.BackendId == Backend.Id).OrderByDescending(x => x.InstalledAt).Select(x => new Choice(x.Id, x.Version + " · " + x.Id[Math.Max(0,x.Id.Length-8)..])).ToArray();
        BuildBox.SelectedValue = planner.SelectBuild(edition, state)?.Id;
        BuildNotes.Text = Backend.Name + " · " + edition.Serial + "\nRecommended release: " + RecommendedBuilds.For(Profile, Backend)?.Version + "\nOlder installed versions remain available. Gameplay has not been certified by this Hub project.";
        FullscreenBox.IsChecked = Current.Fullscreen;
        var context = edition.Id + "/" + Profile.Id;
        if (advancedContext != context)
        {
            advancedContext = context;
            AdvancedText.Text = advancedSavedText = string.Join("\n", Profile.Settings.Select(x => x.Key + "=" + Current.AdvancedSettings.GetValueOrDefault(x.Key, x.Value)));
        }
        SaveNotes.Text = "Family: " + edition.SaveFamily + "\nAutomatic backup before each launch. Original in-game import rules and region restrictions still apply.";
        SnapshotBox.ItemsSource = saves.List(edition.SaveFamily, Backend.Id).Select(x => new Choice(x.Id, x.CreatedAt.ToLocalTime().ToString("g") + " · " + x.Reason)).ToArray();
        SnapshotBox.SelectedIndex = 0;
        BuildPatchList(); RefreshGameSettings(); CheckControllers(this,new()); loading = false; UpdateArtwork();
    }
    private void SetEnhancementNotes()
    {
        if (EnhancementBox.SelectedItem is EnhancementProfile p)
            EnhancementNotes.Text = Current.Graphics.Scale is {} scale ? "Custom resolution · "+scale.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"×. Other settings use "+p.Name+"." : p.Name + " · " + p.Benefit + (p.Name == "Original" ? "" : "\n" + p.Drawback);
    }
    private void BuildPatchList()
    {
        PatchList.Children.Clear();
        foreach (var patch in catalog.Patches.Where(x => x.Editions.Contains(edition!.Id)))
        {
            if (patch.Method == "research-only")
            {
                var author = new Button { Content = patch.Name + " · author documentation", HorizontalAlignment = HorizontalAlignment.Left };
                author.Click += (_, _) => OpenUrl(patch.Upstream); PatchList.Children.Add(author);
                PatchList.Children.Add(Body("Research only: redistribution and exact revision gates are unresolved. " + patch.Drawback));
            }
            else
            {
                var check = new CheckBox { Content = patch.Name + " · " + patch.Category, IsChecked = Current.PatchIds.Contains(patch.Id), Margin = new(0, 8, 0, 8) };
                check.Click += (_, _) =>
                {
                    if (check.IsChecked == true)
                    {
                        if (patch.GameplayChanging && MessageBox.Show(this, patch.Benefit + "\n\n" + patch.Drawback + "\nEnable this gameplay-changing module?", patch.Name, MessageBoxButton.YesNo) != MessageBoxResult.Yes) { check.IsChecked = false; return; }
                        if (!Current.PatchIds.Contains(patch.Id)) Current.PatchIds.Add(patch.Id);
                    }
                    else Current.PatchIds.Remove(patch.Id);
                    Save();
                };
                PatchList.Children.Add(check);
            }
        }
        if (PatchList.Children.Count == 0) PatchList.Children.Add(Body("No reviewed patch modules for this edition yet."));
    }
    private async Task Work(string title, Func<CancellationToken, Task> action)
    {
        if (operation != null || !ConfirmAdvanced()) return;
        operation = new(); var token = operation.Token;var completion=operationCompletion=new(TaskCreationOptions.RunContinuationsAsynchronously);
        StatusText.Text = title; ActivityProgress.Visibility = Visibility.Visible; ActivityProgress.IsIndeterminate = true; CancelButton.Visibility = Visibility.Visible;
        CollectionView.IsEnabled = false; UtilityView.IsEnabled = false;
        try { await action(token); Save(); StatusText.Text = title + " · complete"; }
        catch (OperationCanceledException) { StatusText.Text = "Cancelled · completed imports and installed builds retained"; }
        catch (Exception ex)
        {
            StatusText.Text = ex is HubException ? ex.Message : title + " could not complete. Open logs for details.";
            File.AppendAllText(paths.Managed("logs", "hub.log"), DateTimeOffset.UtcNow + " " + title + "\n" + ex + "\n");
            if(!closeRequested&&!suppressOperationDialogs)MessageBox.Show(this, StatusText.Text, "Armored Core Hub", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            operation.Dispose(); operation = null; ActivityProgress.Visibility = Visibility.Collapsed; CancelButton.Visibility = Visibility.Collapsed;
            try{CollectionView.IsEnabled = true; UtilityView.IsEnabled = true;if(!closeRequested){RefreshGames();RefreshDetail();}}finally{completion.TrySetResult();if(operationCompletion==completion)operationCompletion=null;}
        }
    }
    private void CancelOperation(object sender, RoutedEventArgs e) => operation?.Cancel();
    private void OfflineChanged(object sender, RoutedEventArgs e) { if (loading) return; state.Offline = OfflineBox.IsChecked == true; Save(); RefreshDetail(); _ = StartCovers(coverNetworkEnabled); }
    private async void ScanFolder(object sender, RoutedEventArgs e)
    {
        if (operation != null) return;
        var dialog = new OpenFolderDialog { Title = "Choose your Armored Core collection folder" };
        if (dialog.ShowDialog(this) == true) await ScanPaths([dialog.FolderName]);
    }
    private async void ImportFiles(object sender, RoutedEventArgs e)
    {
        if (operation != null) return;
        var dialog = new OpenFileDialog { Title = "Choose your game files or archives", Multiselect = true, Filter = "Games and archives|*.7z;*.zip;*.rar;*.cue;*.bin;*.iso;*.chd;*.cso;*.pbp;*.xex;*.img;*.mdf;*.stfs;*.000|All files|*.*" };
        if (dialog.ShowDialog(this) == true) await ScanPaths(dialog.FileNames, game?.Id);
    }
    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (operation == null && e.Data.GetData(DataFormats.FileDrop) is string[] files) await ScanPaths(files);
    }
    private async Task ScanPaths(string[] input, string? contextGameId = null)
    {
        Edition? addedEdition = null;
        await Work("Import collection", async ct =>
        {
            var candidates = new List<MediaCandidate>();
            foreach (var path in input)
            {
                candidates.AddRange(await media.ScanAsync(path, Progress, ct));
                if (Directory.Exists(path) && !state.ScanRoots.Contains(path)) state.ScanRoots.Add(path);
            }
            using var batch = await new MediaArchiveService(paths,catalog).PrepareAsync(candidates,state,Progress,ct);
            candidates = batch.Candidates;
            var skipped = candidates.Where(x => x.Format == "inaccessible").ToList();
            JsonData.WriteAtomic(paths.Managed("logs", "last-scan.json"), new { at = DateTimeOffset.UtcNow, storage = paths.GameStorageRoot, skipped = skipped.Select(x => new { name = Path.GetFileName(x.Path), x.Reason, x.Warnings }) });
            candidates = candidates.DistinctBy(x => x.Path, StringComparer.OrdinalIgnoreCase).Where(x => x.Format != "inaccessible").ToList();
            if (candidates.Count == 0) throw new HubException("No supported game media was found." + (skipped.Count > 0 ? $" Skipped {skipped.Count} unreadable items; see the scan log." : " Choose the folder containing your games."));
            var review = new ImportWindow(catalog, candidates, contextGameId, skipped.Count, skipped) { Owner = this };
            if (review.ShowDialog() != true) return;
            foreach (var (candidate, id) in review.Selected)
            {
                var item = await media.RegisterAsync(candidate, id, review.CopyIntoHub, state, Progress, ct);
                state.Media.RemoveAll(x => x.Id == item.Id); state.Media.Add(item);
                var ed = catalog.Editions.Single(x => x.Id == id);
                if (!state.Selections.TryGetValue(id, out var selection)) state.Selections[id] = selection = new() { EditionId = id, ProfileId = ed.PreferredProfile };
                selection.MediaId = item.Id;
                var backend = catalog.Profiles.Single(x => x.Id == (selection.ProfileId ?? ed.PreferredProfile)).BackendId;
                selection.BuildId ??= planner.SelectBuild(ed, state)?.Id;
                state.PreferredEditions[ed.Platform == "psp" ? ed.GameId + "-psp" : ed.GameId] = id; addedEdition = ed; Save();
            }
            state.FirstRunComplete = true;
        });
        if (addedEdition != null)
        {
            SearchBox.Text = ""; ConsoleFilter.SelectedValue = "all"; GenerationFilter.SelectedValue = "all";
            ShowCollection(this, new()); CloseGameSettings(this, new()); RefreshGames();
            GameList.SelectedItem = rows.FirstOrDefault(x => x.GameId == addedEdition.GameId && (x.Platform == "psp") == (addedEdition.Platform == "psp"));
        }
    }
    private async void PlayClicked(object sender, RoutedEventArgs e)
    {
        if (edition == null || operation != null) return;
        var diagnostics = planner.Diagnose(edition.Id, state);
        if (diagnostics.Any(x => x.Repair == "media")) { ImportFiles(sender, e); return; }
        if (diagnostics.Any(x => x.Status is "missing" or "error")) { await RepairCurrent(); return; }
        Current.BuildId ??= planner.SelectBuild(edition, state)?.Id; Save();
        await Work("Play " + game!.Title, async ct => await launcher.LaunchAsync(planner.Resolve(edition.Id, state), state, ct));
    }
    private void EnhancementChanged(object sender, SelectionChangedEventArgs e)
    { if (loading || edition == null) return; Current.EnhancementId = EnhancementBox.SelectedValue as string; Current.Graphics.Scale=null; loading=true; ScaleBox.SelectedValue="preset"; loading=false; SetEnhancementNotes(); Save(); }
    private void MediaChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || edition == null || MediaBox.SelectedValue is not string id) return;
        Current.MediaId = id; Save(); RefreshDetail();
    }
    private void ForgetMedia(object sender, RoutedEventArgs e)
    {
        if (edition == null || Current.MediaId is not string id || !ConfirmAdvanced()) return;
        if (MessageBox.Show(this, "Remove this copy from the Hub's catalog? All game files, saves and backups stay where they are. You can import it again and choose another edition.", "Forget registration", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        state.Media.RemoveAll(x => x.Id == id);
        foreach (var choice in state.Selections.Values.Where(x => x.MediaId == id)) choice.MediaId = null;
        Save(); RefreshGames(); RefreshDetail(); StatusText.Text = "Registration removed · original files and saves retained";
    }
    private void ProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || edition == null || ProfileBox.SelectedValue is not string id) return;
        if (!ConfirmAdvanced()) { loading = true; ProfileBox.SelectedValue = Current.ProfileId; loading = false; return; }
        Current.ProfileChoices[Profile.Id] = new(Current.BuildId, Current.EnhancementId, Current.PatchIds.ToList(), new(Current.AdvancedSettings));
        Current.ProfileId = id; Current.EnhancementId = null; Current.AdvancedSettings.Clear(); Current.PatchIds.Clear();
        Current.BuildId = null;
        if (Current.ProfileChoices.TryGetValue(id, out var prior)) { Current.BuildId = prior.BuildId; Current.EnhancementId = prior.EnhancementId; Current.PatchIds = prior.PatchIds.ToList(); Current.AdvancedSettings = new(prior.Settings); }
        Save(); RefreshDetail();
    }
    private void BuildChanged(object sender, SelectionChangedEventArgs e)
    { if (loading || edition == null) return; Current.BuildId = BuildBox.SelectedValue as string; Save(); RefreshDetail(); }
    private void FullscreenChanged(object sender, RoutedEventArgs e)
    { if (loading || edition == null) return; Current.Fullscreen = FullscreenBox.IsChecked == true; Save(); }
    private async void InstallRuntime(object sender, RoutedEventArgs e)
    { if (edition != null) await InstallRecommended(); }
    private async Task InstallRecommended()
    {
        if (edition == null) return;
        if (Profile.BuildRecommendation == null) { await Install(Backend, Backend.Recommended, Current); return; }
        var id = edition.Id;
        await Work("Prepare recommended setup", ct => new CompatibilityRepair(paths, catalog, runtimes).ApplyAsync(id, state, Progress, ct));
    }
    private async Task Install(Backend backend, ReleaseAsset? release, GameSelection? selection)
    {
        if (release == null) return;
        await Work("Set up " + backend.Name, async ct =>
        {
            var build = await runtimes.InstallAsync(backend.Id, release, state.Offline, Progress, ct);
            state.Builds.RemoveAll(x => x.Id == build.Id); state.Builds.Add(build);
            if (selection != null) selection.BuildId = build.Id;
        });
    }
    private async void CheckUpdate(object sender, RoutedEventArgs e)
    {
        if (edition == null) return;
        ReleaseAsset? release = null; var backend = Backend; var selection = Current;
        await Work("Check " + backend.Name + " release", async ct => release = await runtimes.CheckLatestAsync(backend.Id, state.Offline, ct));
        if (release != null && MessageBox.Show(this, "Available upstream build: " + release.Version + "\n\nThis is not a gameplay-tested recommendation. Install alongside existing builds and select it for this edition?", "Newer runtime", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            await Install(backend, release, selection);
    }
    private async void ImportRuntime(object sender, RoutedEventArgs e)
    {
        if (edition == null) return;
        var dialog = new OpenFileDialog { Title = "Select the pinned upstream runtime archive", Filter = "Runtime archives|*.zip;*.7z" };
        var release = RecommendedBuilds.For(Profile, Backend);
        if (dialog.ShowDialog(this) != true || release == null) return;
        await Work("Import " + Backend.Name, async ct =>
        {
            var build = await runtimes.InstallArchiveAsync(Backend.Id, dialog.FileName, release, Progress, ct);
            state.Builds.RemoveAll(x => x.Id == build.Id); state.Builds.Add(build); Current.BuildId = build.Id;
        });
    }
    private async void VerifyMedia(object sender, RoutedEventArgs e)
    {
        var record = state.Media.FirstOrDefault(x => x.Id == Current.MediaId);
        if (record == null) { ImportFiles(sender, e); return; }
        await Work("Verify media", async ct =>
        {
            if (!await media.VerifyAsync(record, ct)) throw new HubException("The media differs from its imported fingerprint. Re-import the current copy to record an intentional modification.");
        });
    }
    private async void RepairSetup(object sender, RoutedEventArgs e) => await RepairCurrent();
    private async Task RepairCurrent()
    {
        if (edition == null || operation != null) return;
        var missing = planner.Diagnose(edition.Id, state).FirstOrDefault(x => x.Status is "missing" or "error");
        if (missing?.Repair is "runtime" or "compatibility") { await InstallRecommended(); }
        else if (missing?.Repair == "media") ImportFiles(this, new());
        else if (missing?.Repair == "firmware") ShowSetup(this, new());
        else if (missing == null) await Work("Repair local configuration", ct => launcher.PrepareAsync(planner.Resolve(edition.Id, state), state, ct));
    }
    private void SaveAdvanced(object sender, RoutedEventArgs e) => TrySaveAdvanced();
    private bool TrySaveAdvanced(bool showError=true)
    {
        try
        {
            var values = new Dictionary<string, string>();
            foreach (var line in AdvancedText.Text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var split = line.IndexOf('=');
                if (split < 1) throw new HubException("Use key=value on each line.");
                var key = line[..split]; var value = line[(split + 1)..];
                if (!Profile.Settings.TryGetValue(key, out var original)) throw new HubException("Unknown profile setting: " + key);
                if (original is "true" or "false" && value is not ("true" or "false")) throw new HubException(key + " must be true or false.");
                if (value.Length>160||value.Contains('\r'))throw new HubException("Enter one short value per setting.");
                if (double.TryParse(original, System.Globalization.CultureInfo.InvariantCulture, out _) && (!double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var number)||!double.IsFinite(number))) throw new HubException(key + " must be a finite number.");
                if (value != original) values[key] = value;
            }
            var previous=Current.AdvancedSettings;Current.AdvancedSettings=values;
            try{Save();}catch{Current.AdvancedSettings=previous;throw;}
            advancedSavedText = AdvancedText.Text;RefreshGraphicsAdvancedEditors();StatusText.Text = "Advanced overrides saved"; return true;
        }
        catch (Exception ex)when(ex is HubException or IOException or UnauthorizedAccessException)
        { if(showError)MessageBox.Show(this, ex.Message, "Check these settings");StatusText.Text="Advanced edits were not saved · "+ex.Message;return false; }
    }
    private bool ConfirmAdvanced()
    {
        if (edition == null || AdvancedText.Text == advancedSavedText && !HasGraphicsDraft) return true;
        var answer = MessageBox.Show(this, "Save the edited graphics and advanced settings before continuing?\nNo discards only these unsaved edits.", "Unsaved settings", MessageBoxButton.YesNoCancel);
        if (answer == MessageBoxResult.Yes) return (AdvancedText.Text==advancedSavedText||TrySaveAdvanced())&&(!HasGraphicsDraft||TrySaveGraphicsAdvanced());
        if (answer == MessageBoxResult.No) { AdvancedText.Text = advancedSavedText;DiscardGraphicsDraft();return true; }
        return false;
    }
    private async void OpenConfig(object sender, RoutedEventArgs e)
    {
        if (edition == null) return;
        await Work("Prepare configuration", async ct => { var plan = planner.Resolve(edition.Id, state); await launcher.PrepareAsync(plan, state, ct); OpenFolder(plan.WorkingDirectory); });
    }
    private void OpenControlConfig(object sender, RoutedEventArgs e)
    {
        if (edition == null) return;
        var dir = paths.Managed("config", "controls", edition.Id); Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "controller-overrides.json");
        if (!File.Exists(file)) JsonData.WriteAtomic(file, new Dictionary<string, string>());
        OpenFolder(dir);
        StatusText.Text = "Edit controller-overrides.json · keys merge into this edition's controller settings on launch";
    }
    private void CheckControllers(object sender, RoutedEventArgs e)
    {
        if (edition == null) return;
        var count = InputDefaults.ConnectedXInputSlots().Length;
        ControllerStatus.Text = count > 0 ? $"{count} Xbox-compatible controller{(count == 1 ? "" : "s")} connected." : "No Xbox-compatible controller connected.";
        UpdateControlSummary();
        var version = planner.SelectBuild(edition, state)?.Version ?? RecommendedBuilds.For(Profile, Backend)?.Version;
        InputBehavior.Text = Backend.Id == "xenia" ? "A connected controller takes priority. Turn it off to use the keyboard."
            : Backend.Id == "rpcs3" && BuildVersion.Compare(version ?? "", "0.0.43-20250") is not >= 0
                ? "This older emulator version chooses keyboard or controller at launch. Connect your controller before pressing Play."
                : "Keyboard stays available. You can turn on a controller while playing.";
    }
    private async void BackupSaves(object sender, RoutedEventArgs e)
    { if (edition != null) await Work("Back up saves", async ct => await saves.BackupAsync(edition.SaveFamily, edition.GameId, Backend.Id, "Manual backup", ct)); }
    private async void RestoreSaves(object sender, RoutedEventArgs e)
    {
        if (edition == null || SnapshotBox.SelectedValue is not string id) return;
        var snapshot = saves.List(edition.SaveFamily, Backend.Id).Single(x => x.Id == id);
        if (MessageBox.Show(this, "Restore this point? Your current progress will be backed up first.", "Restore save data", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            await Work("Restore saves", ct => saves.RestoreAsync(snapshot, edition.GameId, ct));
    }
    private async void ImportSaves(object sender, RoutedEventArgs e)
    {
        if (edition == null) return;
        var dialog = new OpenFolderDialog { Title = "Choose save root (memory cards, PSP/SAVEDATA, or dev_hdd0/home structure)" };
        if (dialog.ShowDialog(this) == true) await Work("Import save data", ct => saves.ImportAsync(dialog.FolderName, edition.SaveFamily, edition.GameId, Backend.Id, ct));
    }
    private void OpenSaves(object sender, RoutedEventArgs e) { if (edition != null) OpenFolder(saves.Live(edition.SaveFamily, Backend.Id)); }
    private void ImportPatch(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Import a licensed, exact-hash configuration module", Filter = "Patch module|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { new PatchService(paths, catalog).Import(dialog.FileName); RefreshDetail(); StatusText.Text = "Patch installed · disabled until you enable it"; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Patch could not be imported"); }
    }
    private void OpenEvidence(object sender, RoutedEventArgs e) { if (edition?.Sources.FirstOrDefault() is Evidence source) OpenUrl(source.Url); }
    private void ShowCollection(object sender, RoutedEventArgs e) { if (operation != null) return; UtilityView.Visibility = Visibility.Collapsed; CollectionView.Visibility = Visibility.Visible; SetNavigation("Library"); UpdateVideoPlayback(); }
    private void Utility(string title, string description)
    {
        CollectionView.Visibility = Visibility.Collapsed; UtilityView.Visibility = Visibility.Visible; UtilityContent.Children.Clear(); SetNavigation(title); UpdateVideoPlayback();
        UtilityView.ScrollToTop();
        UtilityContent.Children.Add(new TextBlock { Text = title, FontFamily = (FontFamily)FindResource("Display"), FontSize = 38 });
        UtilityContent.Children.Add(Body(description));
    }
    private static TextBlock Body(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new(0, 10, 0, 16), MaxWidth = 940, Opacity = .8 };
    private void ActionButton(string title, RoutedEventHandler action)
    { var b = new Button { Content = title, HorizontalAlignment = HorizontalAlignment.Left }; b.Click += action; UtilityContent.Children.Add(b); }
    private void ShowRuntimes(object sender, RoutedEventArgs e)
    {
        if (operation != null) return;
        Utility("Emulators", "Installed builds are shared across games. Choose a different build in Game settings when needed.");
        foreach (var backend in catalog.Backends)
        {
            UtilityContent.Children.Add(new TextBlock { Text = backend.Name, FontFamily = (FontFamily)FindResource("Display"), FontSize = 26, Margin = new(0, 16, 0, 0) });
            var installed = state.Builds.Where(x => x.BackendId == backend.Id && File.Exists(SafeFiles.Within(paths.Resolve(x.Path),x.Executable))).Select(x => x.Version).Distinct().ToList();
            UtilityContent.Children.Add(Body(ConsoleName(backend.Platform) + "\n" + (installed.Count == 0 ? "Not installed" : string.Join(" / ", installed)) + "\nRecommended version: " + backend.Recommended?.Version));
            if(installed.Count==0)ActionButton("Install " + backend.Name, async (_, _) => { await Install(backend, backend.Recommended, null); ShowRuntimes(this, new()); });

        }
    }
    private void ImportCatalog(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Select a signed catalog envelope", Filter = "Signed manifest|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var key = paths.Managed("config", "catalog-trust.pem"); var keyId = paths.Managed("config", "catalog-trust-id.txt");
            if (!File.Exists(key) || !File.Exists(keyId)) throw new HubException("No release signing key is configured yet. Install a maintainer's trusted public key before accepting remote catalogs. See the updater documentation.");
            new ManifestUpdates(paths).Install(JsonData.Read<SignedCatalog>(dialog.FileName), File.ReadAllText(key), File.ReadAllText(keyId).Trim());
            MessageBox.Show(this, "Catalog installed. Restart the Hub to use it. Existing build selections were preserved.", "Catalog updated");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Catalog update"); }
    }
    private void RollbackCatalog(object sender, RoutedEventArgs e)
    {
        try { new ManifestUpdates(paths).Rollback(); MessageBox.Show(this, "Previous catalog restored. Restart the Hub to load it.", "Rollback complete"); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Catalog rollback"); }
    }
    private void ShowDiagnostics(object sender, RoutedEventArgs e)
    {
        if (edition == null || operation != null) return;
        Utility(game!.Title + " / Diagnostics", EditionBox.Text);
        var diagnostics = planner.Diagnose(edition.Id, state);
        ActionButton("Repair setup", RepairSetup);
        foreach (var d in diagnostics.OrderBy(d => d.Status is "error" or "missing" ? 0 : 1)) UtilityContent.Children.Add(Body(d.Name + " · " + d.Status.ToUpperInvariant() + "\n" + d.Message));
        ActionButton("Verify media", VerifyMedia);
        ActionButton("Copy diagnostics", (_, _) =>
        {
            LaunchPlan? plan = null; try { plan = planner.Resolve(edition.Id, state); } catch (HubException) { }
            Clipboard.SetText(launcher.RedactedDiagnostics(plan, diagnostics)); StatusText.Text = "Diagnostics copied · private paths omitted";
        });
        ActionButton("Open logs", (_, _) => OpenFolder(paths.Managed("logs")));
        ActionButton("Back to collection", ShowCollection);
    }
    private static void OpenUrl(string url)
    { if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https") Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
    private static void OpenFolder(string folder)
    { Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { folder }, UseShellExecute = true }); }
    #if HUB_DIAGNOSTICS
    private async Task SelfTest(string[] args)
    {
        var at = Array.IndexOf(args, "--capture"); var dir = at >= 0 ? Path.GetFullPath(args[at + 1]) : paths.Managed("logs", "ui-test");
        Directory.CreateDirectory(dir);
        var checks = new List<string>();
        async Task Capture(string name)
        {
            await Dispatcher.InvokeAsync(UpdateLayout, System.Windows.Threading.DispatcherPriority.Render);
            await Task.Delay(100); var surface = (FrameworkElement)Content; var bitmap = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(dir, name + ".png")); encoder.Save(stream);
        }
        try
        {
            if (rows.Count != 19) throw new Exception("Catalog count"); checks.Add("15 console titles and 4 portable editions loaded");
            await CheckCovers(checks);
            await Capture("first-run");
            SearchBox.Text = "For Answer"; if (rows.Count != 1 || game?.Id != "acfa") throw new Exception("Search workflow"); checks.Add("Search selects For Answer");
            if (((IEnumerable<Choice>)EditionBox.ItemsSource).Count() < 4) throw new Exception("Cross-platform editions"); checks.Add("PS3 and Xbox editions available");
            await Capture("for-answer"); OpenGameSettings(this, new()); DetailsTabs.SelectedItem = AdvancedTab; await Capture("advanced");
            var edited = AdvancedText.Text + "\n";
            AdvancedText.Text = edited; OfflineBox.IsChecked = true;
            if (AdvancedText.Text != edited || !store.Load().Offline) throw new Exception("Offline refresh lost draft or preference");
            checks.Add("Offline preference persists without overwriting advanced draft");
            AdvancedText.Text = advancedSavedText;
            SearchBox.Text = "no-such-armored-core-title";
            if (rows.Count != 0 || edition != null || SceneActions.Visibility != Visibility.Hidden) throw new Exception("Empty search retained title actions");
            SearchBox.Text = "For Answer";
            if (game?.Id != "acfa") throw new Exception("Empty search recovery");
            checks.Add("Empty search clears title actions and recovers selection");
            OfflineBox.IsChecked = false;
            ShowRuntimes(this, new()); await Capture("runtimes");
            ShowSetup(this, new()); await Capture("setup");
            ShowCollection(this, new()); CloseGameSettings(this, new()); SearchBox.Text = ""; Width = 1280; Height = 768; DetailsTabs.SelectedIndex = 0; await Capture("compact");
            Width = 960; Height = 540; await Capture("small-work-area");
            OpenGameSettings(this, new()); await Capture("small-settings"); if (DetailsTabs.ActualHeight < 80) throw new Exception("Small work area leaves no task viewport"); CloseGameSettings(this, new()); await CheckLibraryAppearance(Capture, checks); await CheckPresentation(Capture, checks);
            await CheckChrome(Capture, checks);
            await CheckImport(Capture, checks, dir);
            await CheckCompatibilityRepair(Capture, checks);
            await CheckGameSettings(Capture, checks, dir);
            CheckGraphicsDrafts(checks);
            await CheckSetup(Capture, checks);
            await CheckLocalMoviePreviews(Capture, checks);
            await CheckAchievementLibrary(Capture, checks);
            await CheckAchievementVisuals(Capture, checks);
            await CheckAchievementAccount(Capture, checks);
            await CheckSmallFeatures(Capture,checks);
            checks.Add("960 by 540 DIP work area keeps task viewport visible");
            JsonData.WriteAtomic(Path.Combine(dir, "ui-results.json"), new { passed = true, checks });
        }
        catch (Exception ex) { JsonData.WriteAtomic(Path.Combine(dir, "ui-results.json"), new { passed = false, error = ex.ToString(), checks }); Environment.ExitCode = 1; }
        Close();
    }
    #endif
}
