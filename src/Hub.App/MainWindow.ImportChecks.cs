using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hub.Core;

namespace Hub.App;

public partial class MainWindow
{
    private async Task CheckImport(Func<string, Task> capture, List<string> checks, string directory)
    {
        Width = 960; Height = 540; ShowCollection(this, new()); CloseGameSettings(this, new());
        var detected = new MediaCandidate("Armored Core 4 [BLES00039].iso", "ps3-iso", "ac4-ps3-bles00039", "BLES00039", "Edition detected from game data.", [], "ps3");
        Exception? failure = null;
        await Work("Import regression", _ =>
        {
            var dialog = new ImportWindow(catalog, [detected], "acfa", 1) { Owner = this };
            dialog.Loaded += async (_, _) =>
            {
                try
                {
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    if (CollectionView.IsEnabled || GameList.IsEnabled) throw new Exception("Import did not exercise the disabled owner state");
                    var surface = (Border)GameList.Template.FindName("ListSurface", GameList);
                    if (surface.Background is not SolidColorBrush b || b.Color.A != 0) throw new Exception("Disabled library acquired an opaque background");
                    var row = dialog.Rows.Single();
                    if (row.Include.IsChecked != true || row.Edition.SelectedValue as string != detected.EditionId || row.Edition.Visibility != Visibility.Collapsed)
                        throw new Exception("Recognized PS3 copy did not select its edition independently of the selected game");
                    await capture("import-disabled-library");
                    var content = (FrameworkElement)dialog.Content;
                    var visual = new DrawingVisual();
                    using (var drawing = visual.RenderOpen())
                    {
                        var bounds = new Rect(0, 0, content.ActualWidth, content.ActualHeight);
                        drawing.DrawRectangle(dialog.Background, null, bounds); drawing.DrawRectangle(new VisualBrush(content), null, bounds);
                    }
                    var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(directory, "import-ac4.png"))) png.Save(stream);
                    if (content.ActualHeight < 250 || !dialog.AcceptButton.IsVisible) throw new Exception("Import actions do not fit in the compact window");
                    if (dialog.AcceptButton.TransformToAncestor(content).Transform(new Point(0, dialog.AcceptButton.ActualHeight)).Y > content.ActualHeight) throw new Exception("Import confirmation is clipped");
                    dialog.AcceptButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (Exception ex) { failure = ex; dialog.Close(); }
            };
            var accepted = dialog.ShowDialog();
            if (failure == null && (accepted != true || dialog.Selected.Single().Edition != detected.EditionId)) failure = new Exception("Import confirmation lost detected edition");
            return Task.CompletedTask;
        });
        if (failure != null) throw failure;
        if (!GameList.IsEnabled) throw new Exception("Library stayed disabled after import");
        checks.Add("Actual modal import keeps the disabled library transparent, auto-selects AC4 PS3 EU and restores interaction");
        var unknown = new ImportWindow(catalog, [new("unknown.chd", "chd", null, null, "", [])], "ac2");
        if (unknown.Rows.Single().Edition.Items.Cast<Choice>().Any(c => catalog.Editions.Single(e => e.Id == c.Id).GameId != "ac2")) throw new Exception("Unknown contextual import showed unrelated editions");
        var blocked = new ImportWindow(catalog, [detected with { Problem = "This PS3 ISO is encrypted or damaged." }]);
        if (blocked.Rows.Single().Include.IsChecked == true || blocked.AcceptButton.IsEnabled) throw new Exception("Blocked disc offered import");
        unknown.Close(); blocked.Close();
        var xbox = new ImportWindow(catalog, [new("unknown.iso", "iso", null, "4E4D084F", "", [], "x360")], "ac4");
        var choices = xbox.Rows.Single().Edition.Items.Cast<Choice>().ToList();
        if (choices.Count != 2 || choices.Any(c=>!c.Id.StartsWith("acv-x360-")) || xbox.Rows.Single().Include.IsChecked == true)
            throw new Exception("Shared Xbox title did not narrow to its two unconfirmed region editions");
        xbox.Close();
        checks.Add("Xbox disc title ID narrows import to matching regional editions without guessing the region");
        checks.Add("Unknown import is scoped to its selected game; encrypted/damaged discs cannot be added as ready");
        SearchBox.Text="For Answer"; OpenGameSettings(this,new()); DetailsTabs.SelectedItem=ControlsTab;
        if(!KeyboardBindingText.Contains("Start → Return")) throw new Exception("PS3 keyboard guide is missing");
        await capture("controls-960");
        SearchBox.Text="Last Raven Portable";
        if(!KeyboardBindingText.Contains("Start → Space")) throw new Exception("PSP keyboard guide did not update with the selected game");
        checks.Add("Keyboard controls are discoverable and switch between console and PSP defaults");
        CloseGameSettings(this,new()); SearchBox.Text="";
    }

    private async Task CheckCompatibilityRepair(Func<string, Task> capture, List<string> checks)
    {
        const string id="ac4-ps3-bles00039";
        var prior=state.Selections.GetValueOrDefault(id);
        var recommendation=catalog.Profiles.Single(p=>p.EditionId==id).BuildRecommendation!;
        var dir=paths.Managed("cache","ui-repair-fixture");Directory.CreateDirectory(Path.Combine(dir,"USRDIR"));
        File.WriteAllText(Path.Combine(dir,"USRDIR","EBOOT.BIN"),"fixture");
        var executable=Path.Combine(dir,"rpcs3.exe");File.WriteAllText(executable,"fixture");
        var good=new InstalledBuild("ui-repair-good","rpcs3",recommendation.Release.Version,paths.Portable(dir),"rpcs3.exe",recommendation.Release.Sha256,await SafeFiles.HashAsync(executable),"fixture",DateTimeOffset.UtcNow);
        var bad=good with {Id="ui-repair-bad",Version="0.0.43-20250",ArchiveSha256=new string('0',64)};
        var source=new MediaRecord("ui-repair-media",paths.Portable(dir),"ps3-folder",id,"fixture",MediaConfidence.RecognizedUnverified,"fixture",[],DateTimeOffset.UtcNow,false);
        var fw=new FirmwareRecord("ui-repair-fw","ps3-firmware",paths.Portable(executable),"fixture","PS3 system software",false,"Global");
        state.Media.Add(source);state.Builds.AddRange([bad,good]);state.Firmware.Add(fw);
        state.Selections[id]=new(){EditionId=id,MediaId=source.Id,BuildId=bad.Id};
        try
        {
            SearchBox.Text="Armored Core 4";EditionBox.SelectedValue=id;RefreshDetail();
            if(edition?.Id!=id || !SceneStatus.Text.StartsWith("Setup needed ·",StringComparison.Ordinal) || !BuildNotes.Text.Contains("0.0.43-20152"))throw new Exception("Known regression was presented as ready");
            ShowDiagnostics(this,new());Width=960;Height=540;await capture("compatibility-repair-960");
            if(UtilityView.VerticalOffset!=0)throw new Exception("Diagnostics inherited another page's scroll position");
            Width=1280;Height=768;await capture("compatibility-repair-1280");
            await RepairCurrent();ShowCollection(this,new());RefreshDetail();
            if(Current.BuildId!=good.Id || Current.MediaId!=source.Id || !SceneStatus.Text.StartsWith("Ready to play ·",StringComparison.Ordinal))throw new Exception("Repair did not restore readiness");
            checks.Add("Known RPCS3 regression shows actionable repair and selects cached recommended build without changing media");
        }
        finally
        {
            state.Media.RemoveAll(m=>m.Id==source.Id);state.Builds.RemoveAll(b=>b.Id==good.Id||b.Id==bad.Id);state.Firmware.RemoveAll(f=>f.Id==fw.Id);
            if(prior==null)state.Selections.Remove(id);else state.Selections[id]=prior;
            Save();ShowCollection(this,new());SearchBox.Text="";RefreshGames();RefreshDetail();
        }
    }
}
