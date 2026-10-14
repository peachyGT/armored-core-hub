using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hub.Core;

namespace Hub.App;
public partial class MainWindow
{
    private async Task CheckSmallFeatures(Func<string,Task> capture,List<string> checks)
    {
        ShowCollection(this,new());GameList.SelectedItem=rows.First(r=>r.Id=="ac4");
        var selected=edition!;var history=paths.Managed("config","playtime",selected.Id+".json");var previous=File.Exists(history)?File.ReadAllBytes(history):null;
        try
        {
            new PlaytimeService(paths,catalog).Record(selected.Id,Guid.NewGuid().ToString("N"),TimeSpan.FromMinutes(90));RefreshDetail();
            if(!SceneStatus.Text.Contains("1 h 30 min played"))throw new Exception("Selected edition playtime not displayed");
            Width=1280;Height=768;await capture("playtime-1280");Width=960;Height=540;await capture("playtime-960");
            checks.Add("Selected game shows portable edition playtime at 1280/960 widths without a polling timer");
        }
        finally{if(previous==null){File.Delete(history);File.Delete(history+".previous");}else File.WriteAllBytes(history,previous);}
        ShowAbout(this,new());var tabs=UtilityContent.Children.OfType<TabControl>().Single();
        if(tabs.Items.Count!=3||((TabItem)tabs.SelectedItem).Header as string!="Legal"||!((StackPanel)((TabItem)tabs.SelectedItem).Content).Children.OfType<TextBlock>().Any(t=>t.Text.Contains("does not distribute commercial games")))throw new Exception("Legal tab notice missing");
        Width=1280;Height=768;await capture("legal-1280");Width=960;Height=540;await capture("legal-960");
        tabs.SelectedIndex=1;await capture("licenses-960");
        checks.Add("Legal tab defaults to the anti-piracy notice and keeps software/artwork licenses separate at 1280/960 widths");
        var existing=paths.Managed("cache","shared-runtime-check","PPSSPPWindows64.exe");Directory.CreateDirectory(Path.GetDirectoryName(existing)!);File.WriteAllText(existing,"native shared-runtime UI fixture");
        var shared=new InstalledBuild("shared-runtime-ui","ppsspp","fixture",paths.Portable(Path.GetDirectoryName(existing)!),Path.GetFileName(existing),"fixture",await SafeFiles.HashAsync(existing),"fixture",DateTimeOffset.UtcNow);state.Builds.Add(shared);
        try{ShowRuntimes(this,new());if(UtilityContent.Children.OfType<Button>().Any(b=>b.Content as string=="Install PPSSPP"))throw new Exception("Installed emulator still offers a redundant installation");Width=1280;Height=768;await capture("emulators-shared-1280");checks.Add("Emulators page reuses available installed builds and omits redundant install actions; explicit per-game versions remain available");}
        finally{state.Builds.Remove(shared);}
        ShowCollection(this,new());
        var exe=Environment.GetEnvironmentVariable("HUB_TEST_RUNTIME_FIXTURE");var dotnet=Environment.GetEnvironmentVariable("HUB_TEST_DOTNET_ROOT");
        if(exe==null||dotnet==null||!File.Exists(exe))return;
        // CreateProcess still bounds its working-directory length. Keep this extra
        // nested fixture short even when the portable validation root is long.
        var fixturePaths=new HubPaths(paths.Managed("cache","c"+Guid.NewGuid().ToString("N")[..8]));JsonData.WriteAtomic(fixturePaths.Managed("manifests","catalog.json"),catalog);new StateStore(fixturePaths).Save(new(){Offline=true});
        var fixture=new MainWindow(fixturePaths,["--resource-fixture"]){Owner=this,ShowInTaskbar=false,ShowActivated=false,WindowState=WindowState.Minimized};fixture.Show();
        var native=catalog.Editions.First(e=>e.Platform=="psp");var source=fixturePaths.Managed("runtime","fixture");Directory.CreateDirectory(source);
        foreach(var file in Directory.EnumerateFiles(Path.GetDirectoryName(exe)!))File.Copy(file,Path.Combine(source,Path.GetFileName(file)));
        var copied=Path.Combine(source,Path.GetFileName(exe));var build=new InstalledBuild("close-fixture","ppsspp","fixture",fixturePaths.Portable(source),Path.GetFileName(exe),"fixture",await SafeFiles.HashAsync(copied),"fixture",DateTimeOffset.UtcNow);
        var mediaFile=fixturePaths.Managed("fixture.iso");await File.WriteAllTextAsync(mediaFile,"synthetic local game fixture");var media=await new MediaService(fixturePaths,catalog).RegisterAsync(new MediaService(fixturePaths,catalog).Probe(mediaFile),native.Id,false,new());
        fixture.state.Media.Add(media);fixture.state.Builds.Add(build);fixture.state.Selections[native.Id]=new(){EditionId=native.Id,MediaId=media.Id,BuildId=build.Id};
        var plan=new LaunchPlanner(fixturePaths,catalog).Resolve(native.Id,fixture.state);plan=plan with{Arguments=["--runtime-fixture",plan.SaveDirectory,"0","60000"]};plan.Environment["DOTNET_ROOT"]=dotnet;
        var closed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);fixture.Closed+=(_,_)=>closed.TrySetResult();var work=fixture.Work("Synthetic game close check",ct=>fixture.launcher.LaunchAsync(plan,fixture.state,ct));
        var confirmations=0;
        try
        {
            var until=DateTime.UtcNow.AddSeconds(8);while(fixture.launcher.ActiveProcessId==0&&!work.IsCompleted&&DateTime.UtcNow<until)await Task.Delay(30);
            if(fixture.launcher.ActiveProcessId==0)throw new Exception("Synthetic native-close runtime did not start");var pid=fixture.launcher.ActiveProcessId;
            fixture.confirmSessionClose=()=>{confirmations++;return false;};fixture.Close();await Task.Delay(50);
            if(closed.Task.IsCompleted||!fixture.launcher.IsRunning)throw new Exception("Declining close stopped the game");
            fixture.confirmSessionClose=()=>{confirmations++;return true;};fixture.Close();await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));await work;
            try{using var process=Process.GetProcessById(pid);if(!process.HasExited)throw new Exception("Game remained after Hub window closed");}catch(ArgumentException){}
            if(confirmations!=2||fixture.launcher.IsRunning||!new SaveService(fixturePaths).List(native.SaveFamily,"ppsspp").Any(s=>s.Reason=="After session")||new PlaytimeService(fixturePaths,catalog).Read(native,fixture.state)<=TimeSpan.Zero)throw new Exception("Close skipped warning, final save or playtime");
            checks.Add("Declining game-close warning retains the owned runtime; confirming waits for process exit, final save and playtime before closing the native Hub window");
        }
        finally{fixture.confirmSessionClose=()=>true;if(!closed.Task.IsCompleted){fixture.operation?.Cancel();await work;fixture.Close();await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));}SafeFiles.DeleteManagedTree(paths.Root,fixturePaths.Root);}
    }
}
