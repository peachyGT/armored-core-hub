using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using Hub.Core;

namespace Hub.App;
public partial class MainWindow
{
    private void ShowAchievementLibrary(object sender, RoutedEventArgs e) => ShowAchievementPage(null);
    private void ShowGameAchievements(object sender, RoutedEventArgs e) { if (edition != null) ShowAchievementPage(edition.Id); }
    private void ShowAchievementPage(string? selected)
    {
        if (operation != null || !ConfirmAdvanced()) return;
        var service = new AchievementService(paths);
        Utility("Achievements", "Cached progress from your emulators and linked RetroAchievements accounts. Available offline.");
        ActionButton("Achievement account and defaults…",ShowAchievementAccount);
        var available = catalog.Editions.Where(x => x.Id == selected || state.Media.Any(m => m.EditionId == x.Id) || service.Cached(x.Id).Count > 0)
            .OrderBy(x => catalog.Games.First(g => g.Id == x.GameId).Title).ThenBy(x => x.Platform).ThenBy(x => x.Region).ToList();
        var choices = new[] { new Choice("all", "All games") }.Concat(available.Select(x => new Choice(x.Id, AchievementGameName(x)))).ToList();
        var filter = new ComboBox { ItemsSource = choices, DisplayMemberPath = "Label", SelectedValuePath = "Id", SelectedValue = selected ?? "all", MaxWidth = 650, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 260 };
        AutomationProperties.SetName(filter, "Achievement game"); UtilityContent.Children.Add(filter);
        var records = new StackPanel(); UtilityContent.Children.Add(records);
        void Render()
        {
            records.Children.Clear(); var id = filter.SelectedValue as string;
            foreach (var gameEdition in available.Where(x => id == "all" || x.Id == id))
            {
                var snapshots = service.Cached(gameEdition.Id);
                var name = new TextBlock { Text = AchievementGameName(gameEdition), FontSize = 22, Margin = new(0, 22, 0, 4), TextWrapping = TextWrapping.Wrap };
                if (id == "all")
                {
                    records.Children.Add(name);
                    records.Children.Add(Body(snapshots.Count == 0 ? "No progress recorded yet." : string.Join("\n", snapshots.Select(AchievementSummary))));
                    var view = new Button { Content = "View this game's achievements", HorizontalAlignment = HorizontalAlignment.Left };
                    view.Click += (_, _) => filter.SelectedValue = gameEdition.Id; records.Children.Add(view);
                    continue;
                }
                var backend = catalog.Backends.Single(b => b.Platform == gameEdition.Platform).Id;
                var actions = new WrapPanel { Margin = new(0, 4, 0, 0) }; records.Children.Add(actions);
                if (backend is "rpcs3" or "xenia")
                {
                    var refresh = new Button { Content = "Refresh local progress", HorizontalAlignment = HorizontalAlignment.Left };
                    refresh.Click += async (_, _) => { var source=AchievementGameMedia(gameEdition);await Work("Read achievements", ct => Task.Run(() => service.ReadLocal(gameEdition, backend,source), ct)); Render(); };
                    actions.Children.Add(refresh);
                }
                else if(AchievementSupport.Get(gameEdition).GameId!=null)
                {
                    var refresh=new Button{Content="Refresh achievements",IsEnabled=!state.Offline&&NativeAchievementAccounts.Load(paths)!=null};
                    refresh.Click+=async(_,_)=>{await Work("Refresh achievements",ct=>service.SyncNativeAsync(gameEdition,state.Offline,ct));Render();};actions.Children.Add(refresh);
                }
                var setup = new Button { Content = "Tracking settings", HorizontalAlignment = HorizontalAlignment.Left, ToolTip = "Achievement account and tracking settings" };
                setup.Click += (_, _) =>
                {
                    ShowCollection(this, new()); ClearFilters(this, new());
                    GameList.SelectedItem = rows.First(r => r.Id == (gameEdition.Platform == "psp" ? gameEdition.GameId + "-psp" : gameEdition.GameId));
                    EditionBox.SelectedValue = gameEdition.Id; OpenGameSettings(this, new()); DetailsTabs.SelectedItem = AchievementsTab;
                };
                actions.Children.Add(setup);
                if (snapshots.Count == 0) records.Children.Add(Body(AchievementSupport.Get(gameEdition).Notes));
                foreach (var snapshot in snapshots) RenderAchievementSnapshot(records, snapshot);
            }
            if (available.Count == 0) records.Children.Add(Body("Add a game to start tracking achievements. Your records will appear here after a session or account refresh."));
        }
        filter.SelectionChanged += (_, _) => Render(); Render();
    }
    private string AchievementGameName(Edition item) => catalog.Games.First(g => g.Id == item.GameId).Title + " · " + ConsoleName(item.Platform) + " · " + item.Region;
    private static string AchievementSummary(AchievementSnapshot snapshot)
    {
        var summary = $"{snapshot.Items.Count(a => a.Unlocked)} / {snapshot.Items.Count} unlocked · {snapshot.Provider} · {snapshot.Account}";
        var grades = new[] { "Platinum", "Gold", "Silver", "Bronze" }.Where(g => snapshot.Items.Any(a => a.Grade == g))
            .Select(g => $"{g} {snapshot.Items.Count(a => a.Grade == g && a.Unlocked)} / {snapshot.Items.Count(a => a.Grade == g)}");
        var detail = string.Join(" · ", grades);
        if(snapshot.Items.Any(i=>i.Points>0))summary+=$" · {snapshot.Items.Where(i=>i.Unlocked).Sum(i=>(long)i.Points)} / {snapshot.Items.Sum(i=>(long)i.Points)} points";
        return summary + (detail.Length > 0 ? "\n" + detail : "");
    }
    private void RenderAchievementSnapshot(StackPanel panel, AchievementSnapshot snapshot)
    {
        panel.Children.Add(new TextBlock { Text = AchievementSummary(snapshot), FontSize = 16, TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 4), ToolTip = "Updated " + snapshot.Updated.ToLocalTime().ToString("g") });
        var tiles=new WrapPanel{Margin=new(0,8,0,8),Tag="achievement-tiles"};
        foreach(var item in snapshot.Items.OrderByDescending(a=>a.Unlocked))tiles.Children.Add(AchievementTile(item));
        panel.Children.Add(tiles);
    }
    private string? AchievementGameMedia(Edition selected)
    {
        var media=state.Media.FirstOrDefault(m=>m.Id==state.Selections.GetValueOrDefault(selected.Id)?.MediaId)??state.Media.FirstOrDefault(m=>m.EditionId==selected.Id);
        return media!=null?paths.Resolve(media.Path):null;
    }
}
