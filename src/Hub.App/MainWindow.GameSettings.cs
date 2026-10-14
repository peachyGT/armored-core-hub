using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Hub.Core;
using Microsoft.Win32;

namespace Hub.App;

public partial class MainWindow
{
    private readonly Dictionary<string,TextBox> graphicsEditors=[];
    private readonly Dictionary<string,string> graphicsSavedValues=[];
    private string graphicsContext="";
    private bool HasGraphicsDraft=>edition!=null&&graphicsContext==edition.Id+"/"+Profile.Id&&graphicsEditors.Any(p=>p.Value.Text!=graphicsSavedValues.GetValueOrDefault(p.Key,""));
    private void RefreshGameSettings()
    {
        ScaleBox.ItemsSource=new[]{new Choice("preset","Use preset")}.Concat(GraphicsSettings.Scales(Backend.Id).Select(x=>new Choice(x.ToString(CultureInfo.InvariantCulture),x.ToString("0.#",CultureInfo.InvariantCulture)+"×"))).ToArray();
        ScaleBox.SelectedValue=Current.Graphics.Scale?.ToString(CultureInfo.InvariantCulture)??"preset";
        ScreenBox.ItemsSource=GraphicsSettings.Screens.Select(x=>new Choice(x,x=="desktop"?"Display default":x.Replace("x"," × ")+" · windowed")).ToArray();ScreenBox.SelectedValue=Current.Graphics.Screen;
        FpsBox.ItemsSource=new[]{new Choice("default","Game default")}.Concat(GraphicsSettings.FrameRates(Backend.Id).Select(x=>new Choice(x.ToString(),x+" FPS"))).ToArray();
        FpsBox.SelectedValue=Current.Graphics.TargetFps?.ToString()??"default";
        FpsNotes.Text=GraphicsSettings.FrameRates(Backend.Id).Length==0?"Higher FPS needs a game-specific patch. Changing emulation speed would also speed up the game.":"A target is a limit, not a performance guarantee. Higher limits may need a game patch; check animation and game speed.";
        RefreshGraphicsAdvancedEditors();
        RefreshControls();
        RefreshLocalPatches();RefreshAchievements();
    }
    private void RefreshGraphicsAdvancedEditors()
    {
        var context=edition!.Id+"/"+Profile.Id;
        var draft=HasGraphicsDraft?graphicsEditors.Where(p=>p.Value.Text!=graphicsSavedValues.GetValueOrDefault(p.Key,""))
            .ToDictionary(p=>p.Key,p=>p.Value.Text):new Dictionary<string,string>();
        graphicsContext=context;graphicsEditors.Clear();graphicsSavedValues.Clear();GraphicsAdvancedPanel.Children.Clear();
        foreach(var (key,value) in Profile.Settings.Where(x=>x.Key.StartsWith("GPU/")||x.Key.StartsWith("Video/")||x.Key.StartsWith("Graphics/")||x.Key.StartsWith("EmuCore/GS/")))
        {
            if(key==GraphicsSettings.ScaleKey(Backend.Id)||key=="GPU/draw_resolution_scale_y"||key=="Video/Frame limit"||key=="GPU/framerate_limit")continue;
            GraphicsAdvancedPanel.Children.Add(new TextBlock{Text=key[(key.LastIndexOf('/')+1)..],Margin=new(0,8,0,0)});
            var saved=Current.AdvancedSettings.GetValueOrDefault(key,value);graphicsSavedValues[key]=saved;
            var box=new TextBox{Text=draft.GetValueOrDefault(key,saved),MinWidth=100};AutomationProperties.SetName(box,key);graphicsEditors[key]=box;GraphicsAdvancedPanel.Children.Add(box);
        }
    }
    private void GraphicsChanged(object sender,SelectionChangedEventArgs e)
    {
        if(loading||edition==null)return;
        Current.Graphics.Scale=ScaleBox.SelectedValue is string scale && scale!="preset"?double.Parse(scale,CultureInfo.InvariantCulture):null;
        Current.Graphics.TargetFps=FpsBox.SelectedValue is string fps && fps!="default"?int.Parse(fps):null;
        Current.Graphics.Screen=ScreenBox.SelectedValue as string??"desktop";
        if(ReferenceEquals(sender,ScreenBox)&&Current.Graphics.Screen!="desktop"){Current.Fullscreen=false;FullscreenBox.IsChecked=false;}
        SetEnhancementNotes();Save();StatusText.Text="Graphics saved · applies next launch";
    }
    private void SaveGraphicsAdvanced(object sender,RoutedEventArgs e)=>TrySaveGraphicsAdvanced();
    private bool TrySaveGraphicsAdvanced(bool showError=true)
    {
        if(edition==null)return true;
        try
        {
            var values=new Dictionary<string,string>(Current.AdvancedSettings);
            foreach(var (key,box) in graphicsEditors)
            {
                if(box.Text==graphicsSavedValues.GetValueOrDefault(key,""))continue;
                var value=box.Text.Trim();var original=Profile.Settings[key];
                if(value.Contains('\n')||value.Contains('\r')||value.Length>160)throw new HubException("Enter one short value per setting.");
                if(original is "true" or "false" && value is not ("true" or "false"))throw new HubException(key+" must be true or false.");
                if(double.TryParse(original,CultureInfo.InvariantCulture,out _) && (!double.TryParse(value,CultureInfo.InvariantCulture,out var n)||!double.IsFinite(n)))throw new HubException(key+" must be a finite number.");
                if(value==original)values.Remove(key);else values[key]=value;
            }
            var previous=Current.AdvancedSettings;Current.AdvancedSettings=values;
            try{Save();}catch{Current.AdvancedSettings=previous;throw;}
            SynchronizeAdvancedGraphics();
            foreach(var (key,box) in graphicsEditors){box.Text=box.Text.Trim();graphicsSavedValues[key]=box.Text;}
            RefreshDetail();StatusText.Text="Graphics overrides saved";return true;
        }
        catch(Exception ex)when(ex is HubException or IOException or UnauthorizedAccessException)
        {if(showError)MessageBox.Show(this,ex.Message,"Check graphics settings");StatusText.Text="Graphics edits were not saved · "+ex.Message;return false;}
    }
    private void DiscardGraphicsDraft(){foreach(var (key,box) in graphicsEditors)box.Text=graphicsSavedValues.GetValueOrDefault(key,box.Text);}
    private void SynchronizeAdvancedGraphics()
    {
        var before=new Dictionary<string,string>();
        foreach(var line in advancedSavedText.Split('\n'))
        {var split=line.IndexOf('=');if(split>0)before[line[..split]]=line[(split+1)..];}
        var after=Profile.Settings.ToDictionary(p=>p.Key,p=>Current.AdvancedSettings.GetValueOrDefault(p.Key,p.Value));
        var draft=AdvancedText.Text.Split('\n').Select(line=>
        {
            var split=line.IndexOf('=');if(split<1)return line;var key=line[..split];
            return graphicsEditors.ContainsKey(key)&&before.TryGetValue(key,out var old)&&line[(split+1)..]==old?key+"="+after[key]:line;
        });
        AdvancedText.Text=string.Join('\n',draft);advancedSavedText=string.Join('\n',after.Select(p=>p.Key+"="+p.Value));
        advancedContext=edition!.Id+"/"+Profile.Id;
    }
    private void RefreshLocalPatches()
    {
        LocalPatchList.Children.Clear();if(edition==null)return;
        var patches=new LocalPatchService(paths,catalog).List(edition.Id);var selected=state.Media.FirstOrDefault(m=>m.Id==Current.MediaId);
        foreach(var patch in patches)
        {
            var matches=patch.SourceHash==selected?.Fingerprint;
            var toggle=new CheckBox{Content=patch.Name,IsChecked=Current.LocalPatchIds.Contains(patch.Id),IsEnabled=matches||Current.LocalPatchIds.Contains(patch.Id),Margin=new(0,14,0,0)};
            toggle.Click+=(_,_)=>
            {
                if(toggle.IsChecked==true)
                {
                    if(patch.Kind=="ppf")foreach(var other in patches.Where(p=>p.Kind=="ppf"))Current.LocalPatchIds.Remove(other.Id);
                    Current.LocalPatchIds.Add(patch.Id);
                }
                else Current.LocalPatchIds.Remove(patch.Id);
                Save();RefreshPatchControls();
            };
            LocalPatchList.Children.Add(toggle);LocalPatchList.Children.Add(Body(matches?"Local "+patch.Kind.ToUpperInvariant()+" · assigned to this copy" : "Choose the original game copy used to import this patch."));
            var analog=new CheckBox{Content="This patch needs analog controls",IsChecked=patch.Analog,Margin=new(0,0,0,8)};
            analog.Click+=(_,_)=>{new LocalPatchService(paths,catalog).SetAnalog(patch,analog.IsChecked==true);RefreshPatchControls();};LocalPatchList.Children.Add(analog);
            if(patch.Kind=="pnach")
            {
                var manager=new LocalPatchService(paths,catalog);var sections=manager.Sections(patch);var selectedSections=new HashSet<string>(patch.EnabledSections??sections);
                foreach(var section in sections)
                {
                    var option=new CheckBox{Content=section,IsChecked=selectedSections.Contains(section),Margin=new(16,0,0,8)};
                    option.Click+=(_,_)=>{if(option.IsChecked==true)selectedSections.Add(section);else selectedSections.Remove(section);manager.SetSections(patch,selectedSections.ToArray());RefreshPatchControls();};LocalPatchList.Children.Add(option);
                }
                var edit=new Button{Content="Edit a copy…",IsEnabled=matches,HorizontalAlignment=HorizontalAlignment.Left};edit.Click+=(_,_)=>EditLocalPatch(patch);LocalPatchList.Children.Add(edit);
            }
        }
        if(patches.Count==0)LocalPatchList.Children.Add(Body("No local patches added."));
    }
    private void RefreshPatchControls()
    {
        var wasLoading=loading;loading=true;
        try{RefreshLocalPatches();RefreshControls();CheckControllers(this,new());}
        finally{loading=wasLoading;}
    }
    private async void EditLocalPatch(LocalGamePatch patch)
    {
        if(edition==null||operation!=null||state.Media.FirstOrDefault(m=>m.Id==Current.MediaId) is not {} source)return;
        var window=new Window{Owner=this,Title="Edit "+patch.Name,Width=720,Height=Math.Min(580,ActualHeight),WindowStartupLocation=WindowStartupLocation.CenterOwner,Style=(Style)FindResource(typeof(Window))};window.SourceInitialized+=(_,_)=>NativeWindowTheme.Apply(window);
        var dock=new DockPanel{Margin=new(20)};window.Content=dock;var text=new TextBox{Text=File.ReadAllText(paths.Resolve(patch.Payload)),AcceptsReturn=true,AcceptsTab=true,FontFamily=new System.Windows.Media.FontFamily("Consolas"),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};AutomationProperties.SetName(text,"PNACH instructions");
        var buttons=new WrapPanel();DockPanel.SetDock(buttons,Dock.Bottom);dock.Children.Add(buttons);var save=new Button{Content="Save new version",Style=(Style)FindResource("Primary")};save.Click+=(_,_)=>window.DialogResult=true;buttons.Children.Add(save);buttons.Children.Add(new Button{Content="Cancel",IsCancel=true});dock.Children.Add(text);
        if(window.ShowDialog()!=true)return;
        await Work("Save patch version",async ct=>
        {
            var temp=paths.Managed("cache","patch-edits",Guid.NewGuid().ToString("N"),patch.Crc+".pnach");Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
            try
            {
                await File.WriteAllTextAsync(temp,text.Text,ct);var next=await new LocalPatchService(paths,catalog).ImportAsync(temp,source,state,patch.Analog,ct);
                if(Current.LocalPatchIds.Remove(patch.Id)&&!Current.LocalPatchIds.Contains(next.Id))Current.LocalPatchIds.Add(next.Id);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        });
    }
    private async void ImportGamePatch(object sender,RoutedEventArgs e)
    {
        var source=state.Media.FirstOrDefault(m=>m.Id==Current.MediaId);if(edition==null||source==null){StatusText.Text="Add this game before importing a patch.";return;}
        var dialog=new OpenFileDialog{Title="Choose a patch for "+game!.Title,Filter="Game patch|*.pnach;*.ppf"};if(dialog.ShowDialog(this)!=true)return;
        var analog=MessageBox.Show(this,"Is this an analog-controls patch? Choose Yes to enable analog controller support; No keeps the current controller type.","Patch controls",MessageBoxButton.YesNo)==MessageBoxResult.Yes;
        await Work("Import patch",async ct=>await new LocalPatchService(paths,catalog).ImportAsync(dialog.FileName,source,state,analog,ct));
    }
    private void BrowsePatches(object sender,RoutedEventArgs e)
    {
        Utility("Known patches",game?.Title??"Armored Core");
        foreach(var patch in catalog.Patches.Where(p=>p.Editions.Contains(edition!.Id)))
        {Section(patch.Name,patch.Benefit);ActionButton("Open author page",(_,_)=>OpenUrl(patch.Upstream));}
        Section("Mouse controls","No reviewed AC1/AC2 mouse patch is bundled. Import a compatible local patch if you have one; the Hub's planned mouse extension is still in development.");
        ActionButton("Import a downloaded patch…",ImportGamePatch);ActionButton("Back to game settings",(_,_)=>{ShowCollection(this,new());OpenGameSettings(this,new());DetailsTabs.SelectedItem=PatchesTab;});
    }
    private void ChooseGameStorage(object sender,RoutedEventArgs e)
    {
        if(operation!=null)return;var dialog=new OpenFolderDialog{Title="Choose a dedicated folder for unpacked games",InitialDirectory=paths.GameStorageRoot};
        if(dialog.ShowDialog(this)!=true)return;
        try{paths.SetGameStorage(dialog.FolderName);StatusText.Text="Game storage changed · existing copies stay in place";ShowAppearance(this,new());}
        catch(Exception ex)when(ex is HubException or IOException or UnauthorizedAccessException){MessageBox.Show(this,ex.Message,"Game storage");}
    }
}
