using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hub.Core;

namespace Hub.App;

public sealed class ImportWindow : Window
{
    public List<(MediaCandidate Candidate, string Edition)> Selected { get; } = [];
    public bool CopyIntoHub { get; private set; }
    internal List<(MediaCandidate Candidate, CheckBox Include, ComboBox Edition)> Rows { get; } = [];
    internal Button AcceptButton { get; }

    public ImportWindow(Catalog catalog, List<MediaCandidate> candidates, string? contextGameId = null, int skipped = 0, IReadOnlyList<MediaCandidate>? failures = null)
    {
        if (candidates.Count > 500) throw new HubException($"The scan found {candidates.Count} copies. Choose a narrower folder to review up to 500 at a time.");
        Style = (Style)FindResource(typeof(Window));
        Title = "Add games"; Width = 760; Height = candidates.Count == 1 ? 430 : 620; MinWidth = 540; MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SourceInitialized += (_, _) => NativeWindowTheme.Apply(this);
        Loaded += (_, _) => { Width = Math.Min(Width, Owner?.ActualWidth ?? SystemParameters.WorkArea.Width); Height = Math.Min(Height, Owner?.ActualHeight ?? SystemParameters.WorkArea.Height); };
        var grid = new Grid { Margin = new(24) };
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new()); grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); Content = grid;
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = candidates.Count == 1 ? "Add this game" : "Add games", FontSize = 27 });
        heading.Children.Add(new TextBlock { Text = candidates.Any(c => c.ArchiveSource != null) ? "Archives stay unchanged. Unpacked games are stored in the Hub folder." : "Game files stay in their current location.", Foreground = (Brush)FindResource("Muted"), Margin = new(0, 8, 0, 18) }); grid.Children.Add(heading);
        var rows = new StackPanel(); var scroll = new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(scroll, 1); grid.Children.Add(scroll);
        foreach (var candidate in candidates)
        {
            var detected = catalog.Editions.SingleOrDefault(e => e.Id == candidate.EditionId);
            var panel = new StackPanel { Margin = new(0, 0, 0, 18) };
            var include = new CheckBox { Content = detected == null ? Path.GetFileName(candidate.Path) : catalog.Games.Single(g => g.Id == detected.GameId).Title,
                IsChecked = detected != null && candidate.Problem == null, IsEnabled = candidate.Problem == null, FontWeight = FontWeights.SemiBold, FontSize = 16 }; panel.Children.Add(include);
            var editions = catalog.Editions.Where(e => e.Formats.Contains(candidate.Format) && (candidate.Platform == null || e.Platform == candidate.Platform)
                && (candidate.ProductId == null || MediaService.NormalizeSerial(e.Serial) == MediaService.NormalizeSerial(candidate.ProductId)
                    || MediaService.NormalizeSerial(e.TitleId ?? "") == MediaService.NormalizeSerial(candidate.ProductId))).ToList();
            var box = new ComboBox { DisplayMemberPath = "Label", SelectedValuePath = "Id" };
            System.Windows.Automation.AutomationProperties.SetName(box, "Edition for " + Path.GetFileName(candidate.Path));
            if (detected != null)
            {
                box.ItemsSource = new[] { new Choice(detected.Id, EditionLabel(detected)) }; box.SelectedValue = detected.Id; box.Visibility = Visibility.Collapsed;
                panel.Children.Add(new TextBlock { Text = EditionLabel(detected), Margin = new(0, 0, 0, 8) });
                panel.Children.Add(new TextBlock { Text = Path.GetFileName(candidate.Path), ToolTip = candidate.Path, Foreground = (Brush)FindResource("Muted"), FontSize = 12 });
            }
            else
            {
                var identifiedGame = candidate.ProductId != null && editions.Select(e=>e.GameId).Distinct().Count() == 1;
                panel.Children.Add(new TextBlock { Text = identifiedGame ? catalog.Games.Single(g=>g.Id==editions[0].GameId).Title + " · Choose your edition" : "Choose the game for this copy.", Margin = new(0, 0, 0, 6) });
                var games = new ComboBox { ItemsSource = catalog.Games.Where(g => editions.Any(e => e.GameId == g.Id)).Select(g => new Choice(g.Id, g.Title)).ToList(), DisplayMemberPath = "Label", SelectedValuePath = "Id" };
                System.Windows.Automation.AutomationProperties.SetName(games, "Game for " + Path.GetFileName(candidate.Path));
                games.SelectionChanged += (_, _) =>
                {
                    box.ItemsSource = editions.Where(e => e.GameId == games.SelectedValue as string).Select(e => new Choice(e.Id, EditionLabel(e))).ToList();
                    if (box.Items.Count == 1) box.SelectedIndex = 0;
                };
                box.SelectionChanged += (_, _) => { if (box.SelectedItem != null && candidate.Problem == null) include.IsChecked = true; };
                games.SelectedValue = identifiedGame ? editions[0].GameId : contextGameId;
                if (!identifiedGame) panel.Children.Add(games);
            }
            panel.Children.Add(box);
            if (candidate.ArchiveSource != null) panel.Children.Add(new TextBlock { Text = "From " + candidate.ArchiveSource, FontSize=12, Foreground=(Brush)FindResource("Muted"), Margin=new(0,4,0,0) });
            if (candidate.Problem != null) panel.Children.Add(new TextBlock { Text = candidate.Problem, Margin = new(0, 10, 0, 0) });
            else if (candidate.Warnings.Length > 0) panel.Children.Add(new TextBlock { Text = string.Join(" ", candidate.Warnings), Margin = new(0, 8, 0, 0) });
            rows.Children.Add(panel); Rows.Add((candidate, include, box));
        }
        var footer = new StackPanel { Margin = new(0, 10, 0, 0) };
        if (skipped > 0)
        {
            var details=new StackPanel();foreach(var failure in failures??[])details.Children.Add(new TextBlock{Text=Path.GetFileName(failure.Path)+"\n"+string.Join(" ",failure.Warnings),TextWrapping=TextWrapping.Wrap,Margin=new(0,4,0,8)});
            footer.Children.Add(new Expander{Header=$"{skipped} items could not be added · show reasons",Content=new ScrollViewer{Content=details,MaxHeight=150,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});
        }
        var copy = new CheckBox { Content = candidates.Any(c=>c.ArchiveSource!=null) ? "Copy other game files into game storage" : "Copy game files into game storage", IsChecked = false, ToolTip = "Uses additional disk space. Originals are kept.", Visibility=candidates.All(c=>c.ArchiveSource!=null) ? Visibility.Collapsed : Visibility.Visible }; footer.Children.Add(copy);
        var buttons = new WrapPanel(); AcceptButton = new Button { Content = candidates.Count == 1 ? "Add game" : "Add selected games", Style = (Style)FindResource("Primary"), IsDefault = true, IsEnabled = candidates.Any(c => c.Problem == null) };
        AcceptButton.Click += (_, _) =>
        {
            Selected.Clear();
            foreach (var row in Rows.Where(x => x.Include.IsChecked == true && x.Candidate.Problem == null))
            {
                if (row.Edition.SelectedValue is not string id) { MessageBox.Show(this, "Choose an edition for each selected copy.", "Edition needed"); return; }
                Selected.Add((row.Candidate, id));
            }
            if (Selected.Count == 0) { MessageBox.Show(this, "Select a game to add.", "Add games"); return; }
            CopyIntoHub = copy.IsChecked == true; DialogResult = true;
        };
        buttons.Children.Add(AcceptButton); buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true }); footer.Children.Add(buttons); Grid.SetRow(footer, 2); grid.Children.Add(footer);
    }
    private static string EditionLabel(Edition e) => (e.Platform switch { "ps1" => "PlayStation", "ps2" => "PlayStation 2", "ps3" => "PlayStation 3", "x360" => "Xbox 360", _ => "PSP" })
        + " · " + (e.Region switch { "US" => "North America", "EU" => "Europe", "JP" => "Japan", _ => e.Region }) + (e.Disc > 1 ? " · Disc " + e.Disc : "")
        + (e.Platform == "psp" && e.Serial.StartsWith("NP") ? " · Digital" : "");
}
