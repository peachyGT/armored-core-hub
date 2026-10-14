using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using Hub.Core;
using Microsoft.Win32;

namespace Hub.App;
public partial class MainWindow
{
    private bool HasAnalogPatch
    {
        get
        {
            if (edition == null) return false;
            var patches = new LocalPatchService(paths, catalog);
            return patches.AnalogActive(patches.List(edition.Id).Where(p => Current.LocalPatchIds.Contains(p.Id)));
        }
    }
    private ControlChoice ActiveInputChoice => ControlProfiles.ForLaunch(edition!, Current.Controls, HasAnalogPatch);
    private ControlScheme ActiveControlScheme => GameControlCatalog.Resolve(edition!, Current.Controls, HasAnalogPatch);
    private void RefreshControls()
    {
        GameSchemeBox.ItemsSource = GameControlCatalog.Schemes(edition!).Select(s => s.Id == "auto" ? s with { Name = "Automatic · " + ActiveControlScheme.Name } : s).ToArray(); GameSchemeBox.SelectedValue = Current.Controls.GameScheme;
        GameSchemeNotes.Text = ActiveControlScheme.Notes;
        ConfigureControlPreset(KeyboardPresetBox, true); ConfigureControlPreset(ControllerPresetBox, false);
        CommunityNotes.Text = CommunityControlPresets.Description(edition!, ActiveControlScheme.Id);
        UpdateCommunityNoteVisibility();
        ImportSdlButton.Visibility = Backend.Id == "xenia" ? Visibility.Visible : Visibility.Collapsed;
        ImportedProfilesList.Children.Clear();
        foreach (var saved in Current.Controls.Imported)
        {
            var row = new StackPanel { Margin = new(0, 10, 0, 12) };
            var name = new TextBox { Text = saved.Profile.Name }; AutomationProperties.SetName(name, "Imported profile name"); row.Children.Add(name);
            var buttons = new WrapPanel(); row.Children.Add(buttons);
            void Add(string label, RoutedEventHandler action) { var b = new Button { Content = label }; b.Click += action; buttons.Children.Add(b); }
            Add("Rename", (_, _) => { try { ControlProfiles.Rename(Current.Controls, saved.Id, name.Text); Save(); RefreshDetail(); } catch (HubException ex) { StatusText.Text = ex.Message; } });
            Add("Export…", (_, _) => ExportBindings(ControlProfiles.Export(Current.Controls, saved.Id, edition!)));
            Add("Remove", (_, _) => { ControlProfiles.Remove(Current.Controls, saved.Id); Save(); RefreshDetail(); });
            row.Children.Add(Body(saved.Profile.Source.Length == 0 ? "Personal layout · export the JSON file to share it." : "Source: " + saved.Profile.Source));
            ImportedProfilesList.Children.Add(row);
        }
        if (Current.Controls.Imported.Count == 0) ImportedProfilesList.Children.Add(Body("Imported and named personal layouts appear here. Keyboard and controller can choose different profiles."));
    }
    private void ConfigureControlPreset(ComboBox box, bool keyboard)
    {
        var choice = Current.Controls; var selected = keyboard ? choice.KeyboardPreset : choice.ControllerPreset;
        var profileId = keyboard ? choice.KeyboardProfileId : choice.ControllerProfileId;
        var profiles = choice.Imported.Where(p => keyboard ? p.Profile.Keyboard.Count > 0 : p.Profile.Controller.Count > 0 || p.Profile.XeniaMapping != null);
        box.DisplayMemberPath = "Label"; box.SelectedValuePath = "Id";
        box.ItemsSource = new[] { new Choice("Default", "Default"), new Choice("Community", "Community"), new Choice("Custom", "Custom") }.Concat(profiles.Select(p => new Choice("import:" + p.Id, "Imported · " + p.Profile.Name))).ToList();
        box.SelectedValue = selected == "Imported" ? "import:" + profileId : selected;
    }
    private void UpdateCommunityNoteVisibility() => CommunityNotes.Visibility = Current.Controls.KeyboardPreset == "Community" || Current.Controls.ControllerPreset == "Community" ? Visibility.Visible : Visibility.Collapsed;
    private void GameSchemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || edition == null || GameSchemeBox.SelectedValue is not string id) return;
        var previous = Current.Controls.GameScheme;
        Current.Controls.GameScheme = id;
        try
        {
            ControlProfiles.Resolve(Backend.Id, ActiveInputChoice, true, edition);
            ControlProfiles.Resolve(Backend.Id, ActiveInputChoice, false, edition);
        }
        catch (HubException ex)
        {
            Current.Controls.GameScheme = previous; RefreshDetail(); StatusText.Text = ex.Message; return;
        }
        Save(); RefreshDetail(); StatusText.Text = "Action labels updated · match this layout in the game";
    }
    private void ControlsPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || edition == null || sender is not ComboBox box || box.SelectedValue is not string id) return;
        var keyboard = ReferenceEquals(box, KeyboardPresetBox);
        var saved = id.StartsWith("import:", StringComparison.Ordinal) ? Current.Controls.Imported.FirstOrDefault(p => p.Id == id[7..]) : null;
        if (saved?.Profile.GameScheme is string scheme && scheme != "auto" && scheme != ActiveControlScheme.Id)
        {
            StatusText.Text = "This profile uses " + scheme + ". Choose that in-game control layout before applying it.";
            loading = true; ConfigureControlPreset(box, keyboard); loading = false; return;
        }
        if (keyboard) { Current.Controls.KeyboardPreset = saved == null ? id : "Imported"; Current.Controls.KeyboardProfileId = saved?.Id; }
        else { Current.Controls.ControllerPreset = saved == null ? id : "Imported"; Current.Controls.ControllerProfileId = saved?.Id; }
        Save(); RefreshDetail(); StatusText.Text = "Bindings saved · applies next launch";
    }
    private void UpdateControlSummary()
    {
        if (edition == null) return;
        FillBindingRows(KeyboardBindings, true);
        FillBindingRows(ControllerBindings, false);
    }
    private void FillBindingRows(StackPanel panel, bool keyboard)
    {
        panel.Children.Clear();
        try
        {
            if (!keyboard && Backend.Id == "xenia" && Current.Controls.ControllerPreset == "Imported" && ControlProfiles.ImportedProfile(ActiveInputChoice, false, edition) is { Controller.Count: 0, XeniaMapping: not null })
            { panel.Children.Add(Body("This imported SDL file has no action map. Choose Default or Community to edit actions here, or edit and re-import the SDL file under Advanced input.")); return; }
            var actions = ActiveControlScheme.Actions;
            var rows = ControlProfiles.Resolve(Backend.Id, ActiveInputChoice, keyboard, edition)
                .Where(p => !actions[p.Key].StartsWith("No default", StringComparison.OrdinalIgnoreCase))
                .Select(p => new { Action = actions[p.Key].Replace(" / no combat action", "", StringComparison.OrdinalIgnoreCase), Button = p.Key, Value = p.Value, Group = KeyboardBindingGroup(actions[p.Key]) })
                .GroupBy(p => p.Group).OrderBy(g => g.Key).ToArray();
            panel.Children.Add(BindingColumns(keyboard, "Action", "Game button", keyboard ? "Key" : "Physical input"));
            foreach (var group in rows)
            {
                panel.Children.Add(new TextBlock { Text = group.Key switch { 0 => "Movement", 1 => "Camera", 2 => "Weapons", 3 => "Boost & mobility", _ => "Menus & other actions" }, FontWeight = FontWeights.SemiBold, Margin = new(0, 18, 0, 4) });
                foreach (var row in group) panel.Children.Add(BindingEditorRow(keyboard, row.Action, row.Button, row.Value));
            }
        }
        catch (HubException ex) { panel.Children.Add(Body(ex.Message)); }
    }
    private static int KeyboardBindingGroup(string action)
    {
        var text = action.ToLowerInvariant();
        if (text.StartsWith("move") || text.StartsWith("strafe")) return 0;
        if (text.StartsWith("look") || text.StartsWith("turn") || text.Contains("camera") || text.Contains("lock mode") || text.Contains("target lock")) return 1;
        if (text.Contains("weapon") || text.Contains("reload") || text.Contains("inside part") || text.Contains("extension")) return 2;
        if (text.Contains("boost") || text.Contains("jump")) return 3;
        return 4;
    }
    private Grid BindingColumns(bool keyboard, string action, string button, string? input = null)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = new(110) });
        row.ColumnDefinitions.Add(new() { Width = new(keyboard ? 124 : 164) });
        row.ColumnDefinitions.Add(new() { Width = new(46) });
        for (var column = 0; column < (input == null ? 2 : 3); column++)
        {
            var text = new TextBlock { Text = new[] { action, button, input }[column], TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 6, 12, 6), Foreground = (Brush)FindResource(input != null || column == 1 ? "Muted" : "Ink"), FontWeight = input != null ? FontWeights.SemiBold : FontWeights.Normal };
            Grid.SetColumn(text, column); row.Children.Add(text);
        }
        return row;
    }
    private Grid BindingEditorRow(bool keyboard, string action, string button, string value)
    {
        var selectedEdition = edition!.Id;
        var native = GameControlCatalog.NativeButton(edition, button);
        var row = BindingColumns(keyboard, action, native); row.Tag = button;
        var defaults = ControlProfiles.Resolve(Backend.Id, new(), keyboard, edition);
        var options = (keyboard ? ControlProfiles.Keys : ControlProfiles.Pad.Values.Distinct().ToArray()).Select(p => new Choice(p, keyboard ? p : PhysicalInput(p))).ToList();
        var box = new ComboBox { ItemsSource = options, DisplayMemberPath = "Label", SelectedValuePath = "Id", SelectedValue = value, Margin = new(0, 4, 8, 4) };
        AutomationProperties.SetName(box, action + " · " + native + " · " + (keyboard ? "keyboard input" : "controller input")); Grid.SetColumn(box, 2); row.Children.Add(box);
        var reset = new Button { Content = new System.Windows.Shapes.Path { Data = Geometry.Parse("M3,4 A6,6 0 1 1 2,10 M3,0 L3,5 8,5"), Stroke = (Brush)FindResource("Ink"), StrokeThickness = 1.35, Width = 15, Height = 15, Stretch = Stretch.Uniform }, ToolTip = "Reset " + action + " to " + (keyboard ? defaults[button] : PhysicalInput(defaults[button])), Padding = new(7), MinHeight = 38, Margin = new(0, 4, 0, 4), IsEnabled = value != defaults[button] };
        AutomationProperties.SetName(reset, "Reset " + action + " to Default"); Grid.SetColumn(reset, 3); row.Children.Add(reset);
        var rule = new Border { BorderBrush = (Brush)FindResource("Rule"), BorderThickness = new(0, 0, 0, 1), IsHitTestVisible = false, Opacity = .45 }; Grid.SetColumnSpan(rule, 4); row.Children.Insert(0, rule);
        void Describe() => AutomationProperties.SetName(row, action + " · " + native + " → " + (keyboard ? value : PhysicalInput(value)));
        Describe(); var reverting = false;
        box.SelectionChanged += (_, _) =>
        {
            if (reverting || loading || edition?.Id != selectedEdition || box.SelectedValue is not string next || next == value) return;
            try
            {
                SaveInlineBinding(keyboard, button, next); value = next; reset.IsEnabled = value != defaults[button]; Describe();
            }
            catch (Exception ex) when (ex is HubException or IOException or UnauthorizedAccessException)
            { reverting = true; box.SelectedValue = value; reverting = false; StatusText.Text = "Binding could not be saved: " + ex.Message; }
        };
        reset.Click += (_, _) => { if (edition?.Id == selectedEdition) box.SelectedValue = defaults[button]; };
        return row;
    }
    private void SaveInlineBinding(bool keyboard, string button, string value)
    {
        if (edition == null || operation != null) throw new HubException("Wait for the current operation to finish before editing bindings.");
        var choice = Current.Controls;
        var previous = keyboard ? choice.Keyboard : choice.Controller;
        var preset = keyboard ? choice.KeyboardPreset : choice.ControllerPreset;
        var profile = keyboard ? choice.KeyboardProfileId : choice.ControllerProfileId;
        var sdl = choice.XeniaMapping;
        var bindings = ControlProfiles.Resolve(Backend.Id, ActiveInputChoice, keyboard, edition);
        bindings[button] = value; ControlProfiles.Validate(bindings, keyboard);
        if (keyboard) { choice.Keyboard = bindings; choice.KeyboardPreset = "Custom"; choice.KeyboardProfileId = null; }
        else { choice.Controller = bindings; choice.ControllerPreset = "Custom"; choice.ControllerProfileId = null; choice.XeniaMapping = null; }
        try { Save(); }
        catch
        {
            if (keyboard) { choice.Keyboard = previous; choice.KeyboardPreset = preset; choice.KeyboardProfileId = profile; }
            else { choice.Controller = previous; choice.ControllerPreset = preset; choice.ControllerProfileId = profile; choice.XeniaMapping = sdl; }
            throw;
        }
        var wasLoading = loading; loading = true;
        try { ConfigureControlPreset(keyboard ? KeyboardPresetBox : ControllerPresetBox, keyboard); }
        finally { loading = wasLoading; }
        UpdateCommunityNoteVisibility();
        StatusText.Text = "Custom " + (keyboard ? "keyboard" : "controller") + " binding saved · applies next launch";
    }
    private static string PhysicalInput(string value) => value switch
    {
        "LeftShoulder" => "LB", "RightShoulder" => "RB", "+LeftTrigger" => "LT", "+RightTrigger" => "RT", "LeftStick" => "LS click", "RightStick" => "RS click",
        "-LeftX" => "Left stick left", "+LeftX" => "Left stick right", "-LeftY" => "Left stick up", "+LeftY" => "Left stick down",
        "-RightX" => "Right stick left", "+RightX" => "Right stick right", "-RightY" => "Right stick up", "+RightY" => "Right stick down", _ => value
    };
    private void ImportCommunityBindings(object sender, RoutedEventArgs e)
    {
        if (edition == null) return; var dialog = new OpenFileDialog { Title = "Import a named binding profile", Filter = "Hub binding profile|*.json" }; if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 128 * 1024) throw new HubException("The binding profile is too large.");
            ControlProfiles.Import(Current.Controls, JsonData.Read<CommunityBindings>(dialog.FileName), edition); Save(); RefreshDetail(); StatusText.Text = "Imported profile saved · choose it for keyboard or controller";
        }
        catch (Exception ex) when (ex is HubException or System.Text.Json.JsonException or IOException) { MessageBox.Show(this, ex.Message, "Bindings could not be imported"); }
    }
    private void ImportSdlBindings(object sender, RoutedEventArgs e)
    {
        if (edition == null) return;
        var dialog = new OpenFileDialog { Title = "Import SDL controller mapping", Filter = "SDL mapping database|*.txt" }; if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 128 * 1024) throw new HubException("Choose a mapping smaller than 128 KB.");
            var text = File.ReadAllText(dialog.FileName); ControlProfiles.ValidateSdl(text);
            ControlProfiles.Import(Current.Controls, new(Path.GetFileNameWithoutExtension(dialog.FileName), "", [edition.Id], [], [], text, ActiveControlScheme.Id), edition);
            Save(); RefreshDetail(); StatusText.Text = "SDL mapping saved as an imported controller profile · its device GUID must match";
        }
        catch (Exception ex) when (ex is HubException or IOException) { MessageBox.Show(this, ex.Message, "Mapping could not be imported"); }
    }
    private void SaveNamedBindings(object sender, RoutedEventArgs e)
    {
        if (edition == null) return;
        var window = new Window { Owner = this, Title = "Save binding profile", Width = 470, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Style = (Style)FindResource(typeof(Window)) }; window.SourceInitialized += (_, _) => NativeWindowTheme.Apply(window);
        var panel = new StackPanel { Margin = new(20) }; window.Content = panel; panel.Children.Add(Body("Save the current keyboard and controller bindings together. You can rename or export the profile later."));
        var name = new TextBox { Text = game!.Title + " layout " + (Current.Controls.Imported.Count + 1) }; AutomationProperties.SetName(name, "Profile name"); panel.Children.Add(name);
        var buttons = new WrapPanel(); panel.Children.Add(buttons); var save = new Button { Content = "Save profile", Style = (Style)FindResource("Primary"), IsDefault = true }; buttons.Children.Add(save); buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true });
        save.Click += (_, _) => { try { ControlProfiles.Import(Current.Controls, new(name.Text, "", [edition.Id], ControlProfiles.Resolve(Backend.Id, ActiveInputChoice, true, edition), NativeProfileController(), Backend.Id == "xenia" ? ControlProfiles.XeniaMap(ActiveInputChoice, edition) : null, ActiveControlScheme.Id), edition); window.DialogResult = true; } catch (HubException ex) { MessageBox.Show(window, ex.Message, "Check profile"); } };
        if (window.ShowDialog() == true) { Save(); RefreshDetail(); StatusText.Text = "Named profile saved"; }
    }
    private Dictionary<string,string> NativeProfileController()
    {
        var imported=ControlProfiles.ImportedProfile(ActiveInputChoice,false,edition);
        return Backend.Id=="xenia" && imported is {Controller.Count:0,XeniaMapping:not null} ? [] : ControlProfiles.Resolve(Backend.Id,ActiveInputChoice,false,edition);
    }
    private void ExportBindings(CommunityBindings profile)
    {
        var dialog = new SaveFileDialog { Title = "Export bindings to share", Filter = "Hub binding profile|*.json", FileName = "armored-core-bindings.json" };
        if (dialog.ShowDialog(this) == true) { try { JsonData.WriteAtomic(dialog.FileName, profile); StatusText.Text = "Profile exported · share this JSON file to import on another Hub"; } catch (IOException ex) { StatusText.Text = "Could not export bindings: " + ex.Message; } }
    }
    private void OpenControlSources(object sender, RoutedEventArgs e)
    {
        if (edition == null) return; var scheme = ActiveControlScheme;
        Utility("Control sources", game!.Title + " · " + scheme.Name); UtilityContent.Children.Add(Body(scheme.Notes));
        foreach (var source in scheme.Sources.Distinct()) ActionButton("Game manual / patch source", (_, _) => OpenUrl(source));
        UtilityContent.Children.Add(Body(CommunityControlPresets.Description(edition, scheme.Id)));
        ActionButton("Community layout source", (_, _) => OpenUrl(CommunityControlPresets.Get(edition, scheme.Id).Source));
        ActionButton("Back to controls", (_, _) => { ShowCollection(this, new()); OpenGameSettings(this, new()); DetailsTabs.SelectedItem = ControlsTab; });
    }
}
