using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Hub.Core;

public sealed class PlaytimeRecord
{
    public string EditionId { get; set; } = "";
    public bool HistoryInitialized { get; set; }
    public long ImportedTicks { get; set; }
    public long TrackedTicks { get; set; }
    public string? LastSession { get; set; }
    public long SessionTicks { get; set; }
}

// A small portable ledger, independent of save rollback. No timer runs outside a game session.
public sealed class PlaytimeService(HubPaths paths,Catalog catalog)
{
    private static readonly object gate=new();
    private static readonly long Limit=TimeSpan.FromDays(36500).Ticks;
    public TimeSpan Read(Edition edition,HubState state)
    {
        lock(gate){var record=Load(edition.Id);Initialize(record,edition,state,false);return TimeSpan.FromTicks(Math.Min(Limit,record.ImportedTicks+record.TrackedTicks));}
    }
    public PlaytimeSession Start(Edition edition,HubState state,Action<Exception>? failure=null)
    {
        lock(gate){var record=Load(edition.Id);Initialize(record,edition,state,true);}
        return new(this,edition.Id,failure);
    }
    public void Record(string editionId,string session,TimeSpan elapsed)
    {
        SafeFiles.Id(editionId);if(!Guid.TryParseExact(session,"N",out _)||elapsed.Ticks<0||elapsed.Ticks>Limit)throw new HubException("Invalid playtime session.");
        lock(gate)
        {
            var record=Load(editionId);
            record.HistoryInitialized=true;
            if(record.LastSession!=session){record.LastSession=session;record.SessionTicks=0;}
            var ticks=elapsed.Ticks;if(ticks<=record.SessionTicks)return;
            record.TrackedTicks=Math.Min(Limit,record.TrackedTicks+ticks-record.SessionTicks);record.SessionTicks=ticks;
            JsonData.WriteAtomic(FileName(editionId),record);
        }
    }
    private string FileName(string edition)=>paths.Managed("config","playtime",SafeFiles.Id(edition)+".json");
    private PlaytimeRecord Load(string edition)
    {
        var file=FileName(edition);
        foreach(var candidate in new[]{file,file+".previous"})
        {
            if(!File.Exists(candidate))continue;
            try
            {
                var r=JsonData.ReadBounded<PlaytimeRecord>(candidate,16384);
                if(r.EditionId==edition&&r.ImportedTicks>=0&&r.ImportedTicks<=Limit&&r.TrackedTicks>=0&&r.TrackedTicks<=Limit&&r.SessionTicks>=0&&r.SessionTicks<=Limit&&(r.LastSession==null||Guid.TryParseExact(r.LastSession,"N",out _)))return r;
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or HubException){}
        }
        if(File.Exists(file)||File.Exists(file+".previous"))throw new HubException("Playtime history is damaged. Its files have been preserved in config/playtime.");
        return new(){EditionId=edition};
    }
    private void Initialize(PlaytimeRecord r,Edition edition,HubState state,bool force)
    {
        if(r.HistoryInitialized)return;
        var backend=catalog.Backends.Single(b=>b.Platform==edition.Platform).Id;
        long native=0;
        var homes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var build in state.Builds.Where(b=>b.BackendId==backend).Take(32))
        {
            homes.Add(Path.GetDirectoryName(SafeFiles.Within(paths.Resolve(build.Path),build.Executable))!);
            homes.Add(Path.GetDirectoryName(paths.Managed("runtime","sessions",SafeFiles.Id(edition.Id),SafeFiles.Id(build.Id),build.Executable))!);
        }
        foreach(var home in homes)
        {
            var names=backend switch{"duckstation"=>new[]{"playtime.dat"},"pcsx2"=>new[]{"inis/playtime.dat","playtime.dat"},"rpcs3"=>new[]{"GuiConfigs/persistent_settings.dat"},_=>Array.Empty<string>()};
            foreach(var name in names)
            {
                try
                {
                    var file=SafeFiles.Within(home,name);if(!File.Exists(file))continue;
                    using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
                    if(stream.Length>1024*1024)continue;
                    using var reader=new StreamReader(stream);var text=reader.ReadToEnd();
                    native=Math.Max(native,ParseNative(backend,edition.Serial,text).Ticks);
                }
                catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or HubException){}
            }
        }
        // Native histories can be copies of one another. Seed their highest total once,
        // before our first session, then count only our own deltas (never add native deltas twice).
        if(force||native>0){r.ImportedTicks=native;r.HistoryInitialized=true;JsonData.WriteAtomic(FileName(edition.Id),r);}
    }
    public static TimeSpan ParseNative(string backend,string serial,string text)
    {
        if(text.Length>1024*1024)return TimeSpan.Zero;
        var match=MediaService.NormalizeSerial(serial);long total=0;var section="";
        foreach(var raw in text.Split('\n').Take(16384))
        {
            var line=raw.Trim();if(line.Length>256)continue;
            if(backend=="rpcs3")
            {
                if(line.StartsWith('[')&&line.EndsWith(']')){section=line[1..^1];continue;}
                var equals=line.IndexOf('=');
                if(section=="Playtime"&&equals>0&&MediaService.NormalizeSerial(line[..equals])==match&&long.TryParse(line[(equals+1)..],NumberStyles.None,CultureInfo.InvariantCulture,out var milliseconds)&&milliseconds>=0&&milliseconds<=Limit/TimeSpan.TicksPerMillisecond)total=Math.Max(total,milliseconds*TimeSpan.TicksPerMillisecond);
            }
            else if(backend is "duckstation" or "pcsx2")
            {
                var parts=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length==3&&MediaService.NormalizeSerial(parts[0])==match&&long.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out var seconds)&&seconds>=0&&seconds<=Limit/TimeSpan.TicksPerSecond&&ulong.TryParse(parts[2],out _))total=Math.Max(total,seconds*TimeSpan.TicksPerSecond);
            }
        }
        return TimeSpan.FromTicks(total);
    }
    public static string Format(TimeSpan time)=>time==TimeSpan.Zero?"0 min":time.TotalMinutes<1?"Less than 1 min":time.TotalHours<1?$"{(long)time.TotalMinutes} min":$"{(long)time.TotalHours} h {time.Minutes} min";
}

public sealed class PlaytimeSession:IAsyncDisposable
{
    private readonly PlaytimeService store;private readonly string edition,session=Guid.NewGuid().ToString("N");
    private readonly Action<Exception>? failure;private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly CancellationTokenSource stop=new();private readonly Task heartbeat;private int disposed;
    internal PlaytimeSession(PlaytimeService store,string edition,Action<Exception>? failure){this.store=store;this.edition=edition;this.failure=failure;heartbeat=Pump();}
    private void Save(){try{store.Record(edition,session,clock.Elapsed);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or HubException){try{failure?.Invoke(ex);}catch(Exception){/* Reporting must never interrupt process/save cleanup. */}}}
    private async Task Pump(){using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));try{while(await timer.WaitForNextTickAsync(stop.Token))Save();}catch(OperationCanceledException)when(stop.IsCancellationRequested){}}
    public async ValueTask DisposeAsync(){if(Interlocked.Exchange(ref disposed,1)!=0)return;clock.Stop();stop.Cancel();await heartbeat;Save();stop.Dispose();}
}
