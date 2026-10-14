using System.Windows;
using System.Windows.Controls;
using System.IO;
using Hub.Core;
namespace Hub.App;
public partial class MainWindow
{
    private string KeyboardBindingText => string.Join("\n", KeyboardBindings.Children.OfType<Grid>().Select(System.Windows.Automation.AutomationProperties.GetName));
    private async Task CheckGameSettings(Func<string,Task> capture,List<string> checks,string captureDirectory)
    {
        ClearFilters(this,new());GameList.SelectedItem=rows.Single(r=>r.Id=="ac4");Width=1280;Height=800;OpenGameSettings(this,new());DetailsTabs.SelectedItem=GraphicsTab;
        var saved=JsonData.Read<HubState>(paths.Managed("config","state.json"));
        try
        {
            var before=EnhancementNotes.Text;EnhancementBox.SelectedIndex=1;
            if(EnhancementNotes.Text==before)throw new Exception("Preset text did not update");
            ScaleBox.SelectedValue="3";ScreenBox.SelectedValue="1280x720";FpsBox.SelectedValue="60";
            var persisted=store.Load().Selections[edition!.Id];
            if(persisted.Graphics.Scale!=3||persisted.Graphics.TargetFps!=60||persisted.Fullscreen||!EnhancementNotes.Text.Contains("Custom resolution"))throw new Exception("Independent graphics settings did not persist");
            await capture("settings-graphics-1280");
            Width=960;Height=540;await capture("settings-graphics-960");
            if(DetailsTabs.Items[^1]!=InfoTab||GraphicsTab.Header as string!="Settings")throw new Exception("Settings order");
            checks.Add("Preset descriptions update; independent scale, screen and FPS persist; Details is last");
            Width=1280;Height=800;DetailsTabs.SelectedItem=ControlsTab;Current.Controls.Keyboard["Cross"]="B";KeyboardPresetBox.SelectedIndex=2;
            if(Current.Controls.KeyboardPreset!="Custom"||Current.Controls.ControllerPreset!="Default"||!KeyboardBindingText.Contains("Cross → B"))throw new Exception("Independent custom controls");
            KeyboardBindingsExpander.IsExpanded=true;
            if(KeyboardBindings.Children.OfType<Grid>().Count()<10||KeyboardBindings.Children.OfType<Grid>().Any(r=>r.ColumnDefinitions.Count!=4))throw new Exception("Keyboard bindings do not use inline action/button/key/reset rows");
            ScrollKeyboardBindings();await capture("settings-controls");Width=960;Height=540;ScrollKeyboardBindings();await capture("keyboard-bindings-960");Width=1280;Height=800;KeyboardBindingsExpander.IsExpanded=false;checks.Add("Custom keyboard preset leaves controller default; grouped action/button/key rows update at 1280/960 widths");
            GameSchemeBox.SelectedValue="type-b";KeyboardPresetBox.SelectedValue="Community";
            if(Current.Controls.KeyboardPreset!="Community"||Current.Controls.ControllerPreset!="Default"||!GameSchemeNotes.Text.Contains("in-game layout"))throw new Exception("Community/layout selection");
            var community=ControlProfiles.Resolve(Backend.Id,ActiveInputChoice,true,edition);
            BindingRow(KeyboardBindings,"R1").Children.OfType<ComboBox>().Single().SelectedValue="P";
            if(Current.Controls.KeyboardPreset!="Custom"||Current.Controls.Keyboard["R1"]!="P"||community.Any(p=>p.Key!="R1"&&Current.Controls.Keyboard[p.Key]!=p.Value)||Current.Controls.ControllerPreset!="Default")throw new Exception("Community inline edit discarded other resolved actions");
            KeyboardPresetBox.SelectedValue="Community";
            if(ControlProfiles.Resolve(Backend.Id,ActiveInputChoice,true,edition)["R1"]!=community["R1"])throw new Exception("Inline edit altered Community source bindings");
            var keyboard=ControlProfiles.Resolve(Backend.Id,Current.Controls,true,edition);var named=ControlProfiles.Import(Current.Controls,new("Synthetic named layout","",[edition.Id],keyboard,new(ControlProfiles.Pad),GameScheme:ActiveControlScheme.Id),edition);
            RefreshDetail();KeyboardPresetBox.SelectedValue="import:"+named.Id;
            if(Current.Controls.KeyboardPreset!="Imported"||Current.Controls.KeyboardProfileId!=named.Id||Current.Controls.ControllerPreset!="Default")throw new Exception("Named imported keyboard selection");
            ControlProfiles.Rename(Current.Controls,named.Id,"Renamed fixture");RefreshDetail();
            if((KeyboardPresetBox.SelectedItem as Choice)?.Label!="Imported · Renamed fixture")throw new Exception("Named profile rename");
            GameSchemeBox.SelectedValue="type-a";
            if(Current.Controls.GameScheme!="type-b"||Current.Controls.KeyboardProfileId!=named.Id||GameSchemeBox.SelectedValue as string!="type-b"||!StatusText.Text.Contains("another in-game layout"))throw new Exception("Imported layout mismatch did not recover safely");
            checks.Add("Changing game layout preserves a selected incompatible import and reports its required scheme safely");
            StatusText.Text="Named profile retained · matching Type B action labels";
            var cross=BindingRow(KeyboardBindings,"Cross");var box=cross.Children.OfType<ComboBox>().Single();
            if(!System.Windows.Automation.AutomationProperties.GetName(box).Contains(ActiveControlScheme.Actions["Cross"]))throw new Exception("Inline binding lacks game action labels");
            var retained=Current.Controls.Imported.Single(p=>p.Id==named.Id).Profile.Keyboard["Cross"];
            box.SelectedValue="H";
            if(Current.Controls.KeyboardPreset!="Custom"||Current.Controls.KeyboardProfileId!=null||store.Load().Selections[edition.Id].Controls.Keyboard["Cross"]!="H"||Current.Controls.ControllerPreset!="Default"||Current.Controls.Imported.Single(p=>p.Id==named.Id).Profile.Keyboard["Cross"]!=retained)throw new Exception("Inline keyboard edit did not persist independently or altered its source import");
            BindingRow(KeyboardBindings,"L1").Children.OfType<ComboBox>().Single().SelectedValue="F";
            cross.Children.OfType<Button>().Single().RaiseEvent(new(Button.ClickEvent));
            var expectedDefault=ControlProfiles.Resolve(Backend.Id,new(),true,edition)["Cross"];
            if(Current.Controls.Keyboard["Cross"]!=expectedDefault||Current.Controls.Keyboard["L1"]!="F"||cross.Children.OfType<Button>().Single().IsEnabled||box.SelectedValue as string!=expectedDefault)throw new Exception("Per-action keyboard reset changed another action or failed");
            var controller=BindingRow(ControllerBindings,"R1");var pad=controller.Children.OfType<ComboBox>().Single();pad.SelectedValue="+RightTrigger";
            if(Current.Controls.ControllerPreset!="Custom"||store.Load().Selections[edition.Id].Controls.Controller["R1"]!="+RightTrigger"||Current.Controls.Keyboard["L1"]!="F")throw new Exception("Inline controller edit did not persist independently");
            controller.Children.OfType<Button>().Single().RaiseEvent(new(Button.ClickEvent));
            if(Current.Controls.Controller["R1"]!=ControlProfiles.Pad["R1"]||controller.Children.OfType<Button>().Single().IsEnabled)throw new Exception("Per-action controller reset failed");
            KeyboardBindingsExpander.IsExpanded=true;ScrollKeyboardBindings();await capture("controls-inline-keyboard-1280");Width=960;Height=540;ScrollKeyboardBindings();await capture("controls-inline-keyboard-960");
            ScrollBindingPanel(ControllerBindings);await capture("controls-inline-controller-960");Width=1280;Height=800;ScrollBindingPanel(ControllerBindings);await capture("controls-inline-controller-1280");
            var previousEdition=edition.Id;GameList.SelectedItem=rows.Single(r=>r.Id=="ac2");var otherBefore=System.Text.Json.JsonSerializer.Serialize(Current.Controls);box.SelectedValue="J";
            if(System.Text.Json.JsonSerializer.Serialize(Current.Controls)!=otherBefore)throw new Exception("Detached binding row changed another edition");
            GameList.SelectedItem=rows.Single(r=>r.Id=="ac4");EditionBox.SelectedValue=previousEdition;DetailsTabs.SelectedItem=ControlsTab;
            await capture("controls-named-profiles");
            checks.Add("Inline keyboard/controller edits persist separately; per-action resets retain other changes/imports; stale rows cannot edit another edition");
            DetailsTabs.SelectedItem=PatchesTab;await capture("settings-patches");
            DetailsTabs.SelectedItem=AchievementsTab;await capture("settings-achievements");
            checks.Add("Patches and achievements are reachable native tabs");
            if(Environment.GetEnvironmentVariable("HUB_TEST_PREVIEW") is string movie&&File.Exists(movie))
            {
                CloseGameSettings(this,new());ApplyPresentation(new(null,null,LibraryArtwork.Neutral,Video:new Uri(movie)),true);
                var until=DateTime.UtcNow.AddSeconds(8);while(!videoReady&&failedVideo==null&&DateTime.UtcNow<until)await Task.Delay(80);
                if(!videoReady)throw new Exception("Generated game preview did not open: "+videoFailureDetail);
                SceneVideo.Play();await Task.Delay(300);if(SceneVideo.Position<=TimeSpan.Zero)throw new Exception("Generated game preview did not advance");
                await capture("local-game-preview");checks.Add("Real game-derived MPEG4 preview opens and advances in native Windows playback");
            }
        }
        finally{state.Selections=saved.Selections;Save();RefreshGames();RefreshDetail();CloseGameSettings(this,new());}
    }
    private void ScrollKeyboardBindings()
        => ScrollBindingPanel(KeyboardBindings);
    private void ScrollBindingPanel(FrameworkElement panel)
    {
        UpdateLayout();DependencyObject parent=panel;
        while(parent is not ScrollViewer)parent=System.Windows.Media.VisualTreeHelper.GetParent(parent)??throw new Exception("Keyboard list has no native scroll viewport");
        var scroll=(ScrollViewer)parent;scroll.ScrollToVerticalOffset(scroll.VerticalOffset+panel.TranslatePoint(new Point(0,0),scroll).Y);UpdateLayout();
    }
    private static Grid BindingRow(StackPanel panel,string button)=>panel.Children.OfType<Grid>().Single(r=>r.Tag as string==button);
}
