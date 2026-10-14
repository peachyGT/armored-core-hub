using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;
using Hub.Core;

namespace Hub.App;

public partial class MainWindow
{
    private bool closeRequested,closeAuthorized;
    private Func<bool>? confirmSessionClose;
    private async void OnClosing(object? sender,CancelEventArgs e)
    {
        if(closeAuthorized)return;
        e.Cancel=true;if(closeRequested||!ConfirmAdvanced())return;
        if(launcher.IsRunning&&!(confirmSessionClose?.Invoke()??MessageBox.Show(this,"A game session is active. Closing the Hub will also close its emulator. Save your progress before continuing.\n\nClose the game and Hub?","Close running game",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes))return;
        closeRequested=true;StatusText.Text="Closing game and background work · protecting saves…";
        try
        {
            var pending=operationCompletion?.Task??Task.CompletedTask;
            operation?.Cancel();presentationLifetime.Cancel();coverDownloads?.Cancel();CancelAssetRead();
            await pending;
            var reads=backgroundReads.Keys.ToArray();
            try{await Task.WhenAll(reads);}catch(Exception){/* Presentation failures are already observed; their stopped tasks must not prevent closing. */}
            SaveAppearance();Save();closeAuthorized=true;
            _=Dispatcher.InvokeAsync(Close,DispatcherPriority.Normal);
        }
        catch(Exception ex){closeRequested=false;StatusText.Text="Closing could not finish. Check logs and available disk space.";File.AppendAllText(paths.Managed("logs","hub.log"),"Shutdown: "+ex+"\n");}
    }
    private DispatcherTimer? statusAnnouncements;
    private DependencyPropertyDescriptor? statusDescriptor;
    private EventHandler? statusValueChanged;
    private void InitializeLifecycle(string[] args)
    {
        statusAnnouncements=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};
        statusAnnouncements.Tick+=AnnounceStatus;
        statusValueChanged=(_,_)=>{if(presentationClosed)return;statusAnnouncements.Stop();statusAnnouncements.Start();};
        statusDescriptor=DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty,typeof(TextBlock));
        statusDescriptor.AddValueChanged(StatusText,statusValueChanged);
        downloader.HealthChanged+=ProviderHealthChanged;launcher.StatusChanged+=LauncherStatusChanged;
        Closed+=(_,_)=>
        {
            statusAnnouncements.Stop();statusAnnouncements.Tick-=AnnounceStatus;
            statusDescriptor.RemoveValueChanged(StatusText,statusValueChanged);
            downloader.HealthChanged-=ProviderHealthChanged;launcher.StatusChanged-=LauncherStatusChanged;http.Dispose();
        };
        if(args.Contains("--resource-self-test"))Loaded+=async(_,_)=>
        {
            var checks=new List<string>();
            try{CheckGraphicsDrafts(checks);await CheckResources(checks);JsonData.WriteAtomic(paths.Managed("logs","resource-self-test.json"),new{passed=true,checks});}
            catch(Exception ex)
            {
                JsonData.WriteAtomic(paths.Managed("logs","resource-self-test.json"),new{passed=false,error=ex.ToString(),checks});Environment.ExitCode=1;
            }
            if(args.Contains("--resource-dump"))await Task.Delay(TimeSpan.FromSeconds(30));
            Close();
        };
    }
    private void AnnounceStatus(object? sender,EventArgs args)
    {
        statusAnnouncements?.Stop();if(presentationClosed)return;
        (UIElementAutomationPeer.FromElement(StatusText)??UIElementAutomationPeer.CreatePeerForElement(StatusText))?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
    private void ProviderHealthChanged(string id,ProviderHealth health)
    {
        if(presentationClosed||Dispatcher.HasShutdownStarted)return;
        _=Dispatcher.InvokeAsync(()=>{if(!presentationClosed){state.ProviderHealth[id]=health;store.Save(state);}});
    }
    private void LauncherStatusChanged(string message)
    {
        if(presentationClosed||Dispatcher.HasShutdownStarted)return;
        _=Dispatcher.InvokeAsync(()=>{if(!presentationClosed)StatusText.Text=message;});
    }
}
