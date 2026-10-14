using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hub.Core;

namespace Hub.App;

internal sealed class VideoFailureEventArgs(Exception error):EventArgs
{public Exception ErrorException{get;}=error;}

// A muted preview has no audio graph. One worker owns the decoder and waits while
// paused; it publishes one copied frame at a time rather than queuing render tasks.
internal sealed class BackgroundVideo : Image
{
    private readonly object gate=new();
    private Session? session;
    private Uri? source;
    private int revision,seekRevision,playRevision;
    private bool playing;
    private long seekTicks,positionTicks,durationTicks;
    private WriteableBitmap? bitmap;
    public event RoutedEventHandler? MediaOpened;
    public event RoutedEventHandler? MediaEnded;
    public event EventHandler<VideoFailureEventArgs>? MediaFailed;
    public new Uri? Source
    {
        get{lock(gate)return source;}
        set
        {
            VerifyAccess();if(value!=null&&(!value.IsAbsoluteUri||!value.IsFile))throw new ArgumentException("Choose a local preview video.");
            lock(gate)
            {
                if(source==value)return;
                source=value;revision++;seekRevision++;seekTicks=positionTicks=durationTicks=0;
                if(value!=null&&session==null)
                {
                    session=new();var owned=session;
                    _=Task.Factory.StartNew(()=>Decode(owned),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
                }
                session?.Wake();
            }
        }
    }
    public TimeSpan Position
    {
        get=>TimeSpan.FromTicks(Interlocked.Read(ref positionTicks));
        set
        {
            VerifyAccess();if(value<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(value));
            lock(gate){seekTicks=value.Ticks;Interlocked.Exchange(ref positionTicks,value.Ticks);seekRevision++;session?.Wake();}
        }
    }
    public TimeSpan Duration=>TimeSpan.FromTicks(Interlocked.Read(ref durationTicks));
    public void Play(){VerifyAccess();lock(gate){if(!playing){playing=true;playRevision++;}session?.Wake();}}
    public void Pause(){VerifyAccess();lock(gate){playing=false;session?.Wake();}}
    public void Close()
    {
        VerifyAccess();Session? closed;
        lock(gate){source=null;playing=false;revision++;closed=session;session=null;positionTicks=durationTicks=0;}
        closed?.Cancel();bitmap=null;base.Source=null;
    }
    private Snapshot ReadState(){lock(gate)return new(source,revision,seekRevision,playRevision,playing,seekTicks);}
    private void Decode(Session owned)
    {
        MediaFoundationVideoReader? decoder=null;byte[]? pixels=null;var activeRevision=-1;var activeSeek=-1;var activePlay=-1;var preroll=false;var ended=false;
        var clock=Stopwatch.StartNew();long anchorReal=0,anchorMedia=0;
        try
        {
            while(!owned.Token.IsCancellationRequested)
            {
                var state=ReadState();
                try
                {
                    if(state.Revision!=activeRevision)
                    {
                        decoder?.Dispose();decoder=null;pixels=null;activeRevision=state.Revision;activeSeek=state.SeekRevision;activePlay=-1;ended=false;preroll=true;
                        if(state.Source!=null)
                        {
                            decoder=new(state.Source.LocalPath);
                            if(!Publish(owned,activeRevision,()=>Interlocked.Exchange(ref durationTicks,decoder.Duration.Ticks)))continue;
                        }
                    }
                    if(decoder==null){owned.Wait();continue;}
                    if(state.SeekRevision!=activeSeek)
                    {decoder.Seek(TimeSpan.FromTicks(state.SeekTicks));activeSeek=state.SeekRevision;activePlay=-1;preroll=true;ended=false;}
                    if(ended||!state.Playing&&!preroll){owned.Wait();continue;}
                    var frame=decoder.ReadFrame(owned.Token,pixels);
                    if(frame==null)
                    {
                        ended=true;Publish(owned,activeRevision,()=>MediaEnded?.Invoke(this,new()),activeSeek);continue;
                    }
                    pixels=frame.Pixels;
                    if(!preroll)
                    {
                        while(!owned.Token.IsCancellationRequested)
                        {
                            var now=ReadState();
                            if(now.Revision!=activeRevision||now.SeekRevision!=activeSeek)break;
                            if(!now.Playing){owned.Wait();continue;}
                            if(now.PlayRevision!=activePlay)
                            {activePlay=now.PlayRevision;anchorReal=clock.ElapsedTicks;anchorMedia=Interlocked.Read(ref positionTicks);}
                            var remaining=(frame.Timestamp-anchorMedia)/(double)TimeSpan.TicksPerSecond-(clock.ElapsedTicks-anchorReal)/(double)Stopwatch.Frequency;
                            if(remaining<=0)break;
                            owned.Wait(Math.Clamp((int)Math.Ceiling(remaining*1000),1,200));
                        }
                    }
                    var current=ReadState();
                    if(current.Revision!=activeRevision||current.SeekRevision!=activeSeek)continue;
                    var opened=preroll;
                    if(Publish(owned,activeRevision,()=>
                    {
                        if(bitmap==null||bitmap.PixelWidth!=frame.Width||bitmap.PixelHeight!=frame.Height)
                        {bitmap=new(frame.Width,frame.Height,96,96,PixelFormats.Bgr32,null);base.Source=bitmap;}
                        bitmap.WritePixels(new Int32Rect(0,0,frame.Width,frame.Height),frame.Pixels,frame.Stride,0);
                        Interlocked.Exchange(ref positionTicks,frame.Timestamp);
                        if(opened)MediaOpened?.Invoke(this,new());
                    },activeSeek))preroll=false;
                }
                catch(OperationCanceledException)when(owned.Token.IsCancellationRequested){break;}
                catch(Exception ex)when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException or System.Runtime.InteropServices.COMException or DllNotFoundException or EntryPointNotFoundException or HubException)
                {
                    decoder?.Dispose();decoder=null;pixels=null;
                    Publish(owned,activeRevision,()=>MediaFailed?.Invoke(this,new(ex)));
                }
            }
        }
        finally{decoder?.Dispose();owned.Dispose();}
    }
    private bool Publish(Session owned,int expectedRevision,Action apply,int? expectedSeek=null)
    {
        if(owned.Token.IsCancellationRequested||Dispatcher.HasShutdownStarted)return false;
        var applied=false;
        try
        {
            // Synchronous on the worker only: buffer reuse waits for this one UI copy.
            Dispatcher.Invoke(()=>
            {
                var current=ReadState();
                if(owned.Token.IsCancellationRequested||current.Revision!=expectedRevision||expectedSeek is {} seek&&current.SeekRevision!=seek)return;
                apply();applied=true;
            },DispatcherPriority.Render,owned.Token);
        }
        catch(OperationCanceledException)when(owned.Token.IsCancellationRequested){}
        return applied;
    }
    private sealed record Snapshot(Uri? Source,int Revision,int SeekRevision,int PlayRevision,bool Playing,long SeekTicks);
    private sealed class Session:IDisposable
    {
        private readonly object sync=new();private readonly AutoResetEvent signal=new(false);private readonly CancellationTokenSource cancellation=new();private bool disposed;
        public CancellationToken Token{get;}public Session()=>Token=cancellation.Token;
        public void Wake(){lock(sync)if(!disposed)signal.Set();}
        public void Wait(int milliseconds=Timeout.Infinite){if(!Token.IsCancellationRequested)signal.WaitOne(milliseconds);}
        public void Cancel(){lock(sync)if(!disposed){cancellation.Cancel();signal.Set();}}
        public void Dispose(){lock(sync){if(disposed)return;disposed=true;signal.Dispose();cancellation.Dispose();}}
    }
}
