using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hub.Core;

namespace Hub.App;

public partial class MainWindow
{
    private async Task CheckResources(List<string> checks)
    {
        var fixture=new HubPaths(paths.Managed("cache","resource-ui-fixture-"+Guid.NewGuid().ToString("N")));
        var images=new LibraryArtwork(fixture);var source=fixture.Managed("replacement.png");
        WeakReference<BitmapSource>? retired=null;
        for(var i=0;i<80;i++)
        {
            WriteResourceImage(source,256,256,i);images.Import("fixture",source,"background");
            var decoded=images.Get("fixture","fixture").Background!;
            retired??=new(decoded);
            if(images.CacheUsage.Count>48||images.CacheUsage.Bytes>LibraryArtwork.DecodedBudget)throw new Exception("Decoded artwork cache exceeded budget");
            // OnLoad must detach both decoder and image from the user's chosen file.
            using var exclusive=new FileStream(source,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        }
        for(var i=0;i<8;i++){WriteResourceImage(source,1600,1800,100+i);images.Import("fixture",source,"background");_ = images.Get("fixture","fixture");}
        if(images.CacheUsage.Bytes>LibraryArtwork.DecodedBudget)throw new Exception("Large artwork replacements exceeded memory budget");
        WriteResourceImage(source,100,4000,200);images.Import("fixture",source,"cover");
        var tall=images.Get("fixture","fixture").Cover!;
        if(tall.PixelHeight>1800||tall.PixelWidth>1800)throw new Exception("Tall artwork bypassed decode dimension limit");
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        if(retired!.TryGetTarget(out _))throw new Exception("Evicted artwork stayed retained by the lookup cache");
        var budget=images.CacheUsage;images.ClearCaches();if(images.CacheUsage.Count!=0||images.CacheUsage.Bytes!=0)throw new Exception("Artwork cache did not release");
        checks.Add("80 artwork replacements keep decoded cache within 48 entries / 64 MiB, evicted image collects, files unlock and tall images remain bounded");
        JsonData.WriteAtomic(fixture.Managed("manifests","catalog.json"),catalog);new StateStore(fixture).Save(new(){Offline=true});
        using var process=Process.GetCurrentProcess();process.Refresh();var minimalBaselineHandles=process.HandleCount;
        var plainWindows=new List<WeakReference>();
        for(var i=0;i<3;i++)plainWindows.Add(CreateClosedMinimalWindow(this,false));
        await DrainResourceDisposal();process.Refresh();var plainWindowHandles=process.HandleCount;
        for(var i=0;i<3;i++)plainWindows.Add(CreateClosedMinimalWindow(this,true));
        await DrainResourceDisposal();process.Refresh();var mediaWindowHandles=process.HandleCount;
        for(var i=0;i<3;i++)plainWindows.Add(CreateClosedMinimalWindow(this,true,true));
        await DrainResourceDisposal();process.Refresh();var explicitlyClosedMediaHandles=process.HandleCount;
        if(plainWindows.Any(w=>w.IsAlive))throw new Exception("Minimal comparison windows remain retained");
        var warmup=CreateClosedResourceWindow(fixture,this);
        await DrainResourceDisposal();
        if(warmup.IsAlive)throw new Exception("Warm-up fixture window remained retained");
        process.Refresh();var baselineManaged=GC.GetTotalMemory(false);var baselinePrivate=process.PrivateMemorySize64;var baselineHandles=process.HandleCount;var windowTime=Stopwatch.StartNew();
        var windows=new List<WeakReference>();
        for(var i=0;i<6;i++)windows.Add(CreateClosedResourceWindow(fixture,this));
        await DrainResourceDisposal();
        process.Refresh();var firstBatch=new{managedBytes=GC.GetTotalMemory(false),privateBytes=process.PrivateMemorySize64,handles=process.HandleCount};
        for(var i=0;i<6;i++)windows.Add(CreateClosedResourceWindow(fixture,this));
        await DrainResourceDisposal();
        var alive=windows.Count(w=>w.IsAlive);
        if(alive!=0)throw new Exception($"{alive} closed fixture windows remained rooted after callbacks/timers were detached");
        checks.Add("Twelve shown/minimized/closed native windows collect after descriptor, timer and preference callbacks detach; native resource snapshots recorded after two batches");
        process.Refresh();
        JsonData.WriteAtomic(paths.Managed("logs","ui-resource-check.json"),new{artworkReplacements=80,largeReplacements=8,cacheCount=budget.Count,cacheBytes=budget.Bytes,closedFixtureWindows=windows.Count,retainedFixtureWindows=alive,minimalBaselineHandles,plainWindowHandles,mediaWindowHandles,explicitlyClosedMediaHandles,baselineManagedBytes=baselineManaged,managedBytes=GC.GetTotalMemory(false),baselinePrivateBytes=baselinePrivate,privateBytes=process.PrivateMemorySize64,baselineHandles,firstBatch,handles=process.HandleCount,windowFixtureElapsedMs=windowTime.ElapsedMilliseconds,scope="Decoded cache ownership excludes active visuals. Bounded managed-object/resource fixtures; not a complete WPF/GPU leak certification."});
        await CheckSceneVideoReuse(fixture,checks);
        await CheckMalformedSceneVideo(fixture,checks);
    }
    private async Task DrainResourceDisposal()
    {
        // WPF/native finalizers can post work back to the UI thread; pump between collections.
        for(var i=0;i<2;i++)
        {
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            await Task.Delay(250);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        }
        await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
    }
    private async Task CheckSceneVideoReuse(HubPaths fixture,List<string> checks)
    {
        var video=Environment.GetEnvironmentVariable("HUB_TEST_PREVIEW");
        if(video==null||!File.Exists(video)){video=fixture.Managed("reuse.avi");WriteFixtureVideo(video);}
        var uri=new Uri(Path.GetFullPath(video));
        var second=fixture.Managed("second"+Path.GetExtension(video));File.Copy(video,second);var secondUri=new Uri(second);
        var thumbnail=BitmapSource.Create(144,80,96,96,PixelFormats.Bgra32,null,new byte[144*80*4],144*4);
        var previousMotion=appearance.VideoBackgrounds;
        try
        {
            if(sceneVideo!=null)throw new Exception("The still-only resource run initialized a player before a video was selected");
            appearance.VideoBackgrounds=false;
            ApplyPresentation(new(thumbnail,null,LibraryArtwork.Neutral,Video:uri),true);
            if(sceneVideo!=null||SceneCover.Visibility!=Visibility.Visible||SceneCover.Source!=thumbnail)throw new Exception("Disabled video initialized native media or hid the cover fallback");
            appearance.VideoBackgrounds=true;SetSceneVideo(uri);
            await WaitForResourceMovie();var player=sceneVideo!;
            player.Play();await Task.Delay(180);if(player.Position<=TimeSpan.Zero)throw new Exception("Lazy video did not advance");
            await DrainResourceDisposal();using var process=Process.GetCurrentProcess();process.Refresh();var baselineHandles=process.HandleCount;
            await ResourceVideoCheckpoint("baseline");
            var batches=new List<int>();
            for(var batch=0;batch<3;batch++)
            {
                for(var i=0;i<8;i++)
                {
                    SetSceneVideo(null);SetSceneVideo(i%2==0?secondUri:uri);await WaitForResourceMovie();
                    if(!ReferenceEquals(sceneVideo,player))throw new Exception("A selection created another native media player");
                }
                await DrainResourceDisposal();process.Refresh();batches.Add(process.HandleCount);
                await ResourceVideoCheckpoint("batch-"+batch);
            }
            await Task.Delay(10000);await DrainResourceDisposal();process.Refresh();var settledHandles=process.HandleCount;
            await ResourceVideoCheckpoint("settled");
            JsonData.WriteAtomic(paths.Managed("logs","ui-video-reuse-check.json"),new{sourceSwitches=24,format=Path.GetExtension(video),samePlayer=true,baselineHandles,batches,settledHandles,warmBatchGrowth=batches[0]-baselineHandles,secondBatchGrowth=batches[1]-batches[0],thirdBatchGrowth=batches[2]-batches[1]});
            if(batches[1]>batches[0]+20||batches[2]>batches[1]+20)throw new Exception("Native handles grew across warmed video source-switch batches");
            checks.Add("No native player is created for stills or disabled video; actual movie opens/advances and 24 different-URI switches reuse one player with stable warmed handle counts");
        }
        finally{SetSceneVideo(null);appearance.VideoBackgrounds=previousMotion;UpdateArtwork();}
    }
    private async Task ResourceVideoCheckpoint(string phase)
    {
        if(Environment.GetEnvironmentVariable("HUB_RESOURCE_SAMPLE")!="1")return;
        JsonData.WriteAtomic(paths.Managed("logs","ui-video-reuse-stage.json"),new{phase,processId=Environment.ProcessId});
        await Task.Delay(1500);
    }
    private async Task CheckMalformedSceneVideo(HubPaths fixture,List<string> checks)
    {
        var thumbnail=BitmapSource.Create(144,80,96,96,PixelFormats.Bgra32,null,new byte[144*80*4],144*4);
        using var process=Process.GetCurrentProcess();await DrainResourceDisposal();process.Refresh();var baseline=process.HandleCount;var batches=new List<int>();
        for(var batch=0;batch<3;batch++)
        {
            for(var i=0;i<4;i++)
            {
                var file=fixture.Managed($"malformed-{batch}-{i}.mp4");File.WriteAllText(file,"Synthetic unsupported preview fixture.");var uri=new Uri(file);
                ApplyPresentation(new(thumbnail,null,LibraryArtwork.Neutral,Video:uri),true);
                var until=DateTime.UtcNow.AddSeconds(6);while(failedVideo!=uri&&DateTime.UtcNow<until)await Task.Delay(40);
                if(failedVideo!=uri||SceneCover.Visibility!=Visibility.Visible||SceneCover.Source!=thumbnail||sceneVideo?.Source!=null)throw new Exception("Malformed preview did not close cleanly and show its cover fallback");
                ApplyPresentation(new(thumbnail,null,LibraryArtwork.Neutral,Video:uri),true);
                if(sceneVideo?.Source!=null)throw new Exception("The same malformed preview was retried on refresh");
            }
            await DrainResourceDisposal();process.Refresh();batches.Add(process.HandleCount);
        }
        JsonData.WriteAtomic(paths.Managed("logs","ui-video-failure-check.json"),new{malformedSources=12,baselineHandles=baseline,batches,sourceClosed=true,coverFallback=true,sameSourceRetrySuppressed=true});
        if(batches[1]>batches[0]+20||batches[2]>batches[1]+20)throw new Exception("Native handles grew across malformed-preview batches");
        checks.Add("Twelve malformed previews close, keep a small cover fallback, avoid refresh retries and preserve stable warmed native handles");
        UpdateArtwork();
    }
    private async Task WaitForResourceMovie()
    {
        var until=DateTime.UtcNow.AddSeconds(6);
        while(!videoReady&&failedVideo==null&&DateTime.UtcNow<until)await Task.Delay(60);
        if(!videoReady)throw new Exception("Resource AVI did not open: "+videoFailureDetail);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateClosedMinimalWindow(Window owner,bool media,bool forceNativeClose=false)
    {
        var window=new Window{Owner=owner,ShowInTaskbar=false,ShowActivated=false,WindowState=WindowState.Minimized,Width=820,Height=500};
        var player=media?new MediaElement{LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Close}:null;
        window.Content=(object?)player??new TextBlock{Text="Native lifetime comparison"};
        var reference=new WeakReference(window);window.Show();
        if(forceNativeClose&&player!=null){player.UnloadedBehavior=MediaState.Manual;player.Play();}
        player?.Close();window.Close();return reference;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateClosedResourceWindow(HubPaths fixture,Window owner)
    {
        var window=new MainWindow(fixture,["--resource-fixture"]){Owner=owner,ShowInTaskbar=false,ShowActivated=false,WindowState=WindowState.Minimized};
        if(window.sceneVideo!=null)throw new Exception("An unopened fixture initialized the native media player");
        window.StatusText.Text="Lifecycle fixture";var reference=new WeakReference(window);window.Show();window.Close();return reference;
    }
    private static void WriteResourceImage(string file,int width,int height,int variant)
    {
        var pixels=new byte[width*height*4];
        for(var i=0;i<pixels.Length;i+=4){pixels[i]=(byte)(variant+20);pixels[i+1]=(byte)(variant*3+40);pixels[i+2]=(byte)(variant*7+60);pixels[i+3]=255;}
        var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(file);encoder.Save(output);
    }
}
