using System.Diagnostics;
using Hub.Core;

// Opt-in real-media QA. Never runs in the synthetic suite. Caller supplies an isolated lab.
static class QaRunner
{
    public sealed record Inputs(string[] Media, string[] FirmwareArchives, string[] FirmwareFiles, Dictionary<string,string>? Editions = null);
    public static async Task<int> Run(string[] args)
    {
        if(args.Length<3) throw new ArgumentException("--qa <init|install-build|launch|report> <lab-root> <project-root> [inputs.json|edition|backend] [enhancement|release.json] [seconds|archive] [build-id]");
        var mode=args[0]; var paths=new HubPaths(args[1]); var project=Path.GetFullPath(args[2]);
        if(SafeFiles.IsWithin(project,paths.Root) && !SafeFiles.IsWithin(Path.Combine(project,"work"),paths.Root))
            throw new HubException("Use a dedicated lab outside the project or under work, never a release or user Hub.");
        var catalog=CatalogStore.Load(Path.Combine(project,"manifests"));
        var marker=paths.Managed("config","qa-lab.json");
        if(!File.Exists(marker))
        {
            if(File.Exists(paths.Managed("config","state.json"))) throw new HubException("Refusing to use an existing non-QA Hub.");
            JsonData.WriteAtomic(marker,new {created=DateTimeOffset.UtcNow,purpose="Isolated local compatibility testing"});
        }
        var store=new StateStore(paths); var state=store.Load();
        var progress=new ThrottledProgress(); var media=new MediaService(paths,catalog);
        if(mode=="repair-compatibility")
        {
            var id=args[3];var edition=catalog.Editions.Single(e=>e.Id==id);
            var choice=state.Selections[id];
            JsonData.WriteAtomic(paths.Managed("logs","qa-before-repair-"+SafeFiles.Id(id)+".json"),choice);
            // Test the actual ISO preparation route even when an earlier manual extraction exists.
            var iso=state.Media.FirstOrDefault(m=>m.EditionId==id && m.Format=="ps3-iso");
            if(iso!=null) choice.MediaId=iso.Id;
            using var http=ProviderDownloader.CreateHttpClient();
            var runtimes=new RuntimeService(paths,catalog,new ProviderDownloader(paths,http),http);
            await new CompatibilityRepair(paths,catalog,runtimes).ApplyAsync(id,state,progress);
            var plan=new LaunchPlanner(paths,catalog).Resolve(id,state);
            store.Save(state);
            JsonData.WriteAtomic(paths.Managed("logs","qa-compatibility-repair-"+SafeFiles.Id(id)+".json"),new {edition=id,source=iso?.Id,selected=choice.MediaId,build=plan.BuildId,plan.SaveDirectory,mediaCount=state.Media.Count,completed=DateTimeOffset.UtcNow});
            Console.WriteLine("Compatibility repair complete: "+id+" / "+plan.BuildId);return 0;
        }
        if(mode=="init")
        {
            var input=JsonData.Read<Inputs>(args[3]); var firmware=new FirmwareService(paths,catalog);
            foreach(var archive in input.FirmwareArchives)
            {
                var hash=await SafeFiles.HashAsync(archive); var root=paths.Managed("cache","qa-firmware",hash);
                if(!Directory.Exists(root)) await ArchiveInstaller.ExtractAsync(archive,root,maxBytes:256L*1024*1024);
                foreach(var record in await firmware.ScanAsync(root,progress))
                {state.Firmware.RemoveAll(f=>f.Id==record.Id);state.Firmware.Add(record);store.Save(state);}
            }
            foreach(var source in input.FirmwareFiles)
            {
                var record=await firmware.ImportAsync(source);state.Firmware.RemoveAll(f=>f.Id==record.Id);state.Firmware.Add(record);store.Save(state);
            }
            using var http=new HttpClient();
            var runtimes=new RuntimeService(paths,catalog,new ProviderDownloader(paths,http),http);
            foreach(var backend in catalog.Backends)
            {
                if(state.Builds.Any(b=>b.BackendId==backend.Id)) continue;
                var archive=Path.Combine(project,"work","emulator-smoke",backend.Recommended!.FileName);
                var build=await runtimes.InstallArchiveAsync(backend.Id,archive,backend.Recommended,progress);
                state.Builds.Add(build);store.Save(state);
            }
            var imports=new List<object>();
            foreach(var source in input.Media)
            {
                var before=File.Exists(source)?new FileInfo(source):null;
                var length=before?.Length;var stamp=before?.LastWriteTimeUtc;
                try
                {
                    if(before==null && !Directory.Exists(source)) throw new HubException("The QA input is unavailable.");
                    using var batch=await new MediaArchiveService(paths,catalog).PrepareAsync(await media.ScanAsync(source,progress),state,progress);
                    foreach(var candidate in batch.Candidates)
                    {
                        var selectedEdition = candidate.EditionId ?? input.Editions?.GetValueOrDefault(Path.GetFileName(source));
                        if(selectedEdition==null || candidate.Problem!=null || candidate.Format=="inaccessible")
                        {imports.Add(new {source=Path.GetFileName(source),candidate,registered=false});continue;}
                        // Explicit lab choice for shared-region IDs. RegisterAsync still validates
                        // platform and product ID, and keeps confidence at UnknownRevision.
                        var record=await media.RegisterAsync(candidate,selectedEdition,false,state,progress);
                        state.Media.RemoveAll(m=>m.Id==record.Id);state.Media.Add(record);
                        var edition=catalog.Editions.Single(e=>e.Id==record.EditionId);
                        var profile=catalog.Profiles.Single(p=>p.Id==edition.PreferredProfile);
                        var previous=state.Selections.GetValueOrDefault(edition.Id);
                        var recommended=catalog.Backends.Single(b=>b.Id==profile.BackendId).Recommended;
                        var build=state.Builds.FirstOrDefault(b=>b.BackendId==profile.BackendId && b.Id==previous?.BuildId)
                            ?? state.Builds.FirstOrDefault(b=>b.BackendId==profile.BackendId && b.ArchiveSha256==recommended?.Sha256)
                            ?? state.Builds.Single(b=>b.BackendId==profile.BackendId);
                        state.Selections[edition.Id]=new(){EditionId=edition.Id,MediaId=record.Id,ProfileId=profile.Id,
                            BuildId=build.Id,Fullscreen=false};
                        store.Save(state); imports.Add(new {source=Path.GetFileName(source),candidate,registered=true,record.Fingerprint,record.Confidence});
                        Console.WriteLine("REGISTERED "+edition.Id);
                    }
                    if(before!=null)
                    {
                        before.Refresh();
                        if(before.Length!=length || before.LastWriteTimeUtc!=stamp) throw new HubException("Source changed during test import.");
                    }
                }
                catch(Exception ex){ imports.Add(new {source=Path.GetFileName(source),registered=false,error=ex.ToString()});Console.WriteLine("IMPORT ERROR "+Path.GetFileName(source)+": "+ex.Message); }
                JsonData.WriteAtomic(paths.Managed("logs","qa-imports.json"),imports);
            }
            state.Offline=true;state.FirstRunComplete=true;store.Save(state);
        }
        else if(mode=="install-build")
        {
            var backend=catalog.Backends.Single(b=>b.Id==args[3]);
            var release=JsonData.Read<ReleaseAsset>(args[4]);
            using var http=ProviderDownloader.CreateHttpClient();
            var runtime=new RuntimeService(paths,catalog,new(paths,http),http);
            var build=await runtime.InstallArchiveAsync(backend.Id,args[5],release,progress);
            if(!state.Builds.Any(b=>b.Id==build.Id)){state.Builds.Add(build);store.Save(state);}
            Console.WriteLine("INSTALLED "+build.Id+"; existing selections retained");
        }
        else if(mode=="launch")
        {
            var edition=catalog.Editions.Single(e=>e.Id==args[3]);var selection=state.Selections[edition.Id];
            var profile=catalog.Profiles.Single(p=>p.Id==selection.ProfileId);
            var enhancement=args.ElementAtOrDefault(4) ?? "Original";
            selection.BuildId=args.ElementAtOrDefault(6) ?? selection.BuildId;
            selection.EnhancementId=catalog.Enhancements.Single(e=>e.ProfileId==profile.Id && (e.Name==enhancement || e.Id==enhancement)).Id;
            // QA selections are session-local. Import preparation may continue independently;
            // never overwrite its newer catalog state with this launch's read snapshot.
            selection.Fullscreen=false;
            var settingsFile=paths.Managed("config","qa-settings",edition.Id+".json");
            if(File.Exists(settingsFile))
            {
                // Mutate only this run's in-memory catalog. The normal resolver
                // still validates values and serializes sections without duplicates.
                foreach(var (key,value) in JsonData.Read<Dictionary<string,string>>(settingsFile)) profile.Settings[key]=value;
                Console.WriteLine("QA-only profile settings: "+settingsFile);
            }
            var plan=new LaunchPlanner(paths,catalog).Resolve(edition.Id,state);
            var extraFile=paths.Managed("config","qa-extra",edition.Id+".json");
            if(File.Exists(extraFile))
            {
                var extra=JsonData.Read<Dictionary<string,string>>(extraFile);
                foreach(var (name,append) in extra)
                {
                    var target=plan.ConfigFiles.Keys.Single(k=>Path.GetFileName(k)==name);
                    plan.ConfigFiles[target]=Path.GetExtension(target).Equals(".ini",StringComparison.OrdinalIgnoreCase)
                        ? OverrideIni(plan.ConfigFiles[target],append) : plan.ConfigFiles[target]+"\n"+append;
                }
                Console.WriteLine("QA-only configuration instrumentation: "+extraFile);
            }
            var launch=new LaunchService(paths,catalog); launch.StatusChanged+=Console.WriteLine;
            await launch.PrepareAsync(plan,state);
            var seconds=int.Parse(args.ElementAtOrDefault(5) ?? "900");
            if(seconds is < 20 or > 3600)throw new ArgumentException("QA session duration must be 20–3600 seconds.");
            using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
            var run=launch.LaunchAsync(plan,state,ct.Token);
            var session=paths.Managed("logs","qa-session.json");var start=DateTimeOffset.UtcNow;
            var evidence=paths.Managed("logs","qa-sessions",edition.Id+"-"+start.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(evidence);JsonData.WriteAtomic(Path.Combine(evidence,"launch-plan.json"),plan);
            var observedPid=0; var stoppedByRequest=false;
            while(!run.IsCompleted)
            {
                if(observedPid==0 && launch.ActiveProcessId is var ownedPid && ownedPid!=0)
                {
                    observedPid=ownedPid;
                    JsonData.WriteAtomic(session,new {edition=edition.Id,backend=profile.BackendId,enhancement,processId=ownedPid,
                        executable=plan.Executable,workingDirectory=plan.WorkingDirectory,saveDirectory=plan.SaveDirectory,started=start,running=true});
                }
                if(File.Exists(paths.Managed("config","qa-stop"))) {File.Delete(paths.Managed("config","qa-stop"));stoppedByRequest=true;ct.Cancel();}
                await Task.Delay(250);
            }
            var timedOut=false; int? exit=null;string? error=null;
            try{exit=await run;}catch(OperationCanceledException)when(ct.IsCancellationRequested){timedOut=!stoppedByRequest;}
            catch(Exception ex){error=ex.ToString();Console.WriteLine("SESSION ERROR: "+ex.Message);}
            var final=new {edition=edition.Id,backend=profile.BackendId,enhancement,processId=observedPid,started=start,
                ended=DateTimeOffset.UtcNow,running=false,timedOut,stoppedByRequest,exit,error};
            JsonData.WriteAtomic(session,final);JsonData.WriteAtomic(Path.Combine(evidence,"session.json"),final);
            foreach(var native in new[]{"duckstation.log","emulog.txt","logs/emulog.txt","log/RPCS3.log","xenia.log"})
            {
                var source=SafeFiles.Within(plan.WorkingDirectory,native);
                if(File.Exists(source)) File.Copy(source,Path.Combine(evidence,Path.GetFileName(native)),true);
            }
            if(error!=null || exit is not null and not 0) return 1;
        }
        else if(mode!="report")throw new ArgumentException("Unknown QA command.");
        var planner=new LaunchPlanner(paths,catalog);
        var report=new {at=DateTimeOffset.UtcNow,catalog=catalog.Version,controllers=InputDefaults.ConnectedXInputSlots(),
            firmware=state.Firmware.Select(f=>new{f.Class,f.Label,f.Verified,f.Region}),
            games=state.Media.Select(m=>new {m.EditionId,m.Format,m.Confidence,m.Fingerprint,diagnostics=planner.Diagnose(m.EditionId!,state),
                enhancements=catalog.Enhancements.Where(e=>e.ProfileId==state.Selections[m.EditionId!].ProfileId).Select(e=>new {e.Id,e.Name,e.Settings})})};
        JsonData.WriteAtomic(paths.Managed("logs","qa-readiness.json"),report);
        Console.WriteLine("Readiness: "+paths.Managed("logs","qa-readiness.json"));
        return 0;
    }
    // Scalar SimpleINI settings may use the first duplicate value. Replace explicit
    // lab overrides rather than silently appending an ineffective second value.
    private static string OverrideIni(string original,string overrides)
    {
        var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var section="";
        foreach(var raw in overrides.Split('\n'))
        {
            var line=raw.Trim();
            if(line.StartsWith('[') && line.EndsWith(']'))section=line[1..^1];
            else if(!line.StartsWith(';') && line.IndexOf('=') is var equal && equal>0)keys.Add(section+"/"+line[..equal].Trim());
        }
        var retained=new List<string>();section="";
        foreach(var raw in original.Split('\n'))
        {
            var line=raw.Trim();
            if(line.StartsWith('[') && line.EndsWith(']'))section=line[1..^1];
            else if(!line.StartsWith(';') && line.IndexOf('=') is var equal && equal>0 && keys.Contains(section+"/"+line[..equal].Trim()))continue;
            retained.Add(raw);
        }
        return string.Join('\n',retained)+"\n"+overrides;
    }
    private sealed class ThrottledProgress:IProgress<ProgressInfo>
    {
        private string last=""; private DateTime previous=DateTime.MinValue;
        public void Report(ProgressInfo p)
        {
            if(p.Message==last && (DateTime.UtcNow-previous).TotalSeconds<10)return;
            last=p.Message;previous=DateTime.UtcNow;
            Console.WriteLine(p.Message+(p.Total>0?$" {p.Completed*100.0/p.Total:0.0}%":""));
        }
    }
}

