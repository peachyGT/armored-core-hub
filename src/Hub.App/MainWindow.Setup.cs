using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Hub.Core;
using Microsoft.Win32;

namespace Hub.App;

public partial class MainWindow
{
    private SetupChecklist? setupChecklist;
    private void ShowSetup(object sender,RoutedEventArgs e)
    {
        if(operation!=null)return;
        if(edition==null)ClearFilters(this,new());
        if(edition==null){Utility("Set up a game","Choose a game in Library to see its setup requirements.");SetNavigation("Help");ActionButton("Return to library",ShowCollection);return;}
        setupChecklist=SetupChecklist.Read(paths,catalog,state,edition);
        Utility("Set up "+game!.Title,edition.Platform.ToUpperInvariant()+" · "+edition.Region);
        SetNavigation("Help");
        if(UtilityContent.Children[0] is TextBlock title){title.FontSize=30;title.TextWrapping=TextWrapping.Wrap;title.MaxWidth=880;}
        foreach(var requirement in setupChecklist.Requirements)
        {
            var content=new StackPanel {Margin=new(0,10,0,10)};
            var line=new StackPanel {Orientation=Orientation.Horizontal};
            line.Children.Add(new System.Windows.Shapes.Path {
                Data=Geometry.Parse(requirement.Complete?"M1,8 L6,13 L16,2":"M8,0 A8,8 0 1 1 7.99,0 M8,4 L8,8 L11,8"),
                Stroke=(Brush)FindResource(requirement.Complete?"Good":"Muted"),StrokeThickness=1.75,Width=17,Height=17,
                Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,11,0)
            });
            line.Children.Add(new TextBlock {Text=requirement.Title,FontSize=18,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            line.Children.Add(new TextBlock {Text=requirement.State,Foreground=(Brush)FindResource(requirement.Complete?"Good":"Muted"),Margin=new(14,0,0,0),VerticalAlignment=VerticalAlignment.Center});
            content.Children.Add(line);
            content.Children.Add(new TextBlock {Text=requirement.Detail,TextWrapping=TextWrapping.Wrap,Foreground=(Brush)FindResource("Muted"),MaxWidth=760,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(28,7,0,0)});
            var row=new Border {Child=content,BorderBrush=(Brush)FindResource("Rule"),BorderThickness=new(0,0,0,1),Tag="setup-"+requirement.Id};
            AutomationProperties.SetName(row,requirement.Title+": "+requirement.State);UtilityContent.Children.Add(row);
        }
        Section(setupChecklist.Ready?"Ready to play":"Next step",setupChecklist.Ready?
            setupChecklist.FirmwareInstallPending?"Press Play to install the imported system update and open the game.":"Your game files, emulator and required system files are available.":
            setupChecklist.NextRepair switch {"media"=>"Add your copy of this game. You can choose an image, archive or game folder.","firmware"=>"Choose a BIOS dump or system update for this console. ZIP and 7z archives are supported; the best matching BIOS is selected automatically.","runtime"=>"Install the recommended emulator. Existing compatible installations are reused.",_=>"Repair the indicated requirement while keeping your game files and saves."});
        if(setupChecklist.Ready)SetupAction("Play",PlayFromSetup,true);
        else switch(setupChecklist.NextRepair)
        {
            case "media":SetupAction("Add game files…",ImportGameFromSetup,true);SetupAction("Scan collection folder…",ScanGameFromSetup);break;
            case "firmware":if(Backend.FirmwareClass=="ps3-firmware")SetupAction("Download official PS3 system software",DownloadFirmware,true);SetupAction("Import system files…",ImportFirmware,Backend.FirmwareClass!="ps3-firmware");SetupAction("Detect existing system files",DetectFirmware);break;
            case "runtime":SetupAction("Install "+Backend.Name,InstallFromSetup,true);break;
            case "profile":SetupAction("Review game settings",OpenGameSettings,true);break;
            default:SetupAction("Repair setup",RepairFromSetup,true);break;
        }
        if(Backend.FirmwareClass!="none"&&setupChecklist.NextRepair!="firmware")SetupAction(Backend.FirmwareClass.EndsWith("bios",StringComparison.Ordinal)?"Change BIOS…":"Change system software…",ImportFirmware);
        SetupAction("Return to library",ShowCollection);
        var help=new StackPanel();var more=new Expander {Header="Dumping guides, catalog updates and logs",Content=help,Margin=new(0,22,0,0)};UtilityContent.Children.Add(more);
        void Help(string text,RoutedEventHandler action){var button=new Button {Content=text,HorizontalAlignment=HorizontalAlignment.Left};button.Click+=action;help.Children.Add(button);}
        if(edition.Platform=="ps1")Help("PlayStation BIOS guide",(_,_)=>OpenUrl("https://www.duckstation.org/wiki/BIOS-Dumping"));
        if(edition.Platform=="ps2")Help("PlayStation 2 BIOS dumping guide",(_,_)=>OpenUrl("https://pcsx2.net/docs/setup/bios/"));
        if(edition.Platform=="ps3")Help("PlayStation 3 setup and dumping guide",(_,_)=>OpenUrl("https://rpcs3.net/quickstart"));
        if(edition.Platform=="psp")Help("PSP dumping guide",(_,_)=>OpenUrl("https://www.ppsspp.org/docs/getting-started/dumping-games/"));
        Help("Import signed catalog update…",ImportCatalog);Help("Roll back previous catalog",RollbackCatalog);Help("Open logs",(_,_)=>OpenFolder(paths.Managed("logs")));
    }
    private void SetupAction(string title,RoutedEventHandler action,bool primary=false)
    {
        var button=new Button {Content=title,HorizontalAlignment=HorizontalAlignment.Left};
        if(primary)button.Style=(Style)FindResource("Primary");AutomationProperties.SetName(button,title);button.Click+=action;
        if(primary)UtilityContent.Children.Insert(Math.Min(2,UtilityContent.Children.Count),button);else UtilityContent.Children.Add(button);
    }
    private void Section(string title,string description)
    {
        UtilityContent.Children.Add(new TextBlock {Text=title,FontFamily=(FontFamily)FindResource("Display"),FontSize=25,Margin=new(0,21,0,0)});UtilityContent.Children.Add(Body(description));
    }
    private async void ImportGameFromSetup(object sender,RoutedEventArgs e)
    {
        if(operation!=null)return;
        var dialog=new OpenFileDialog {Title="Choose this game's image or archive",Multiselect=true,Filter="Games and archives|*.7z;*.zip;*.rar;*.cue;*.bin;*.iso;*.chd;*.cso;*.pbp;*.xex;*.img;*.mdf;*.stfs;*.000|All files|*.*"};
        if(dialog.ShowDialog(this)==true){await ScanPaths(dialog.FileNames,game?.Id);ShowSetup(this,new());}
    }
    private async void ScanGameFromSetup(object sender,RoutedEventArgs e)
    {
        if(operation!=null)return;var dialog=new OpenFolderDialog {Title="Choose this game's folder or collection"};
        if(dialog.ShowDialog(this)==true){await ScanPaths([dialog.FolderName],game?.Id);ShowSetup(this,new());}
    }
    private async void InstallFromSetup(object sender,RoutedEventArgs e){await InstallRecommended();ShowSetup(this,new());}
    private async void RepairFromSetup(object sender,RoutedEventArgs e){await RepairCurrent();ShowSetup(this,new());}
    private void PlayFromSetup(object sender,RoutedEventArgs e){ShowCollection(this,new());PlayClicked(sender,e);}
    private async void ImportFirmware(object sender,RoutedEventArgs e)
    {
        if(edition==null||operation!=null)return;var target=edition;
        var dialog=new OpenFileDialog {Title="Choose BIOS dumps, a ZIP/7z archive or the official PS3 update",Multiselect=true,Filter="System files and archives|*.bin;*.rom;*.rom0;*.pup;*.zip;*.7z|All files|*.*"};
        if(dialog.ShowDialog(this)!=true)return;
        string? result=null;
        await Work("Import system software",async ct=>{
            var imported=new List<FirmwareImportResult>();
            foreach(var file in dialog.FileNames){var item=await firmware.ImportForEditionAsync(file,target,Progress,ct);AddFirmware(item.Selected);imported.Add(item);}
            result="Selected "+FirmwareService.Choose(FirmwareService.ClassFor(target),target,state.Firmware,f=>File.Exists(paths.Resolve(f.Path)))!.Label;
            if(imported.Sum(i=>i.CandidatesFound)>1)result+=" · best match for this game";
        });
        ShowSetup(this,new());if(result!=null)StatusText.Text=result;
    }
    private void AddFirmware(FirmwareRecord record){state.Firmware.RemoveAll(f=>f.Id==record.Id);state.Firmware.Add(record);Save();}
    private async void DetectFirmware(object sender,RoutedEventArgs e)
    {
        if(edition==null||operation!=null)return;var target=edition;
        await Work("Detect system software",async ct=>{
            var found=new List<FirmwareRecord>();foreach(var root in FirmwareService.SuggestedRoots())found.AddRange(await firmware.ScanForEditionAsync(root,target,Progress,ct));
            if(found.Count==0)throw new HubException("No valid "+FirmwareService.ClassName(FirmwareService.ClassFor(target))+" was found. Use Import system files to choose your dump or BIOS archive.");
            foreach(var item in found)AddFirmware(item);
        });ShowSetup(this,new());
    }
    private async void DownloadFirmware(object sender,RoutedEventArgs e)
    {
        if(edition==null||operation!=null)return;var target=edition;
        await Work("Download official PS3 system software",async ct=>{
            var provider=catalog.Providers.Single(p=>p.Kind=="OfficialFirmware");var file=await downloader.FetchAsync(provider,state.Offline,Progress,ct);
            AddFirmware((await firmware.ImportForEditionAsync(file,target,Progress,ct)).Selected);
        });ShowSetup(this,new());
    }
}
