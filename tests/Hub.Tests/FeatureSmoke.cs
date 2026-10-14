using Hub.Core;
static class FeatureSmoke
{
    public static async Task<int> Run(string[] args)
    {
        var paths=new HubPaths(args[0]);var catalog=CatalogStore.Load(args[1]);var store=new StateStore(paths);var state=store.Load();
        if(args[2]=="presentation")
        {
            var source=state.Media.Single(m=>m.Id==state.Selections["ac4-ps3-bles00039"].MediaId);
            using var http=ProviderDownloader.CreateHttpClient();var assets=await new MediaPreviewService(paths,catalog,new(paths,http)).ReadAsync(source,false);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(assets,JsonData.Options));return assets.Background!=null&&assets.Video!=null?0:1;
        }
        if(args[2]=="achievements")
        {
            var edition=catalog.Editions.Single(e=>e.Id=="acv-x360-4e4d084f-us");var result=new AchievementService(paths).ReadLocal(edition,"xenia");Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result.Select(s=>new{s.Provider,s.SetId,total=s.Items.Count,unlocked=s.Items.Count(a=>a.Unlocked)})));return result.Count>0?0:1;
        }
        if(args[2]=="imports")
        {
            paths.SetGameStorage(args[3]);var media=new MediaService(paths,catalog);
            foreach(var file in args.Skip(4))
            {
                using var batch=await new MediaArchiveService(paths,catalog).PrepareAsync([media.Probe(file)],state);
                foreach(var candidate in batch.Candidates)
                {
                    var editionId=candidate.EditionId;
                    if(editionId==null && candidate.Platform=="x360" && candidate.ProductId=="4E4D084F")editionId="acv-x360-4e4d084f-us";
                    if(editionId==null){Console.WriteLine(candidate.Reason+" "+string.Join(" ",candidate.Warnings));return 1;}
                    var registered=await media.RegisterAsync(candidate,editionId,false,state);state.Media.RemoveAll(m=>m.Id==registered.Id);state.Media.Add(registered);
                    if(!state.Selections.TryGetValue(editionId,out var choice))state.Selections[editionId]=choice=new(){EditionId=editionId};choice.MediaId=registered.Id;
                    store.Save(state);Console.WriteLine("Registered "+editionId+" in external game storage");
                }
            }
            return 0;
        }
        if(args[2]=="native-patch")
        {
            var labPaths=new HubPaths(args[3]);var lab=new StateStore(labPaths).Load();var id="ac2-ps2-slus20014";var old=lab.Selections[id];
            var source=lab.Media.Single(m=>m.Id==old.MediaId);source=source with{Path=labPaths.Resolve(source.Path)};
            var build=lab.Builds.Single(b=>b.Id==old.BuildId);build=build with{Path=labPaths.Resolve(build.Path)};
            var bios=lab.Firmware.Where(f=>f.Class=="ps2-bios").Select(f=>f with{Path=labPaths.Resolve(f.Path)}).ToList();
            var testState=new HubState{Media=[source],Builds=[build],Firmware=bios};var patch=await new LocalPatchService(paths,catalog).ImportAsync(args[4],source,testState,true);
            testState.Selections[id]=new(){EditionId=id,MediaId=source.Id,BuildId=build.Id,Fullscreen=false,LocalPatchIds=[patch.Id]};
            var plan=new LaunchPlanner(paths,catalog).Resolve(id,testState);using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try{await new LaunchService(paths,catalog).LaunchAsync(plan,testState,stop.Token);}catch(OperationCanceledException)when(stop.IsCancellationRequested){}
            var matches=SafeFiles.Files(paths.Managed("logs")).Where(f=>Path.GetExtension(f) is ".log" or ".stdout" or ".stderr").Concat(SafeFiles.Files(plan.WorkingDirectory).Where(f=>Path.GetExtension(f)==".txt"||Path.GetExtension(f)==".log"))
                .Where(f=>new FileInfo(f).Length<16*1024*1024).SelectMany(f=>File.ReadLines(f).Where(l=>l.Contains("True Analogs",StringComparison.Ordinal)||l.Contains("cheat patches are active",StringComparison.Ordinal))).Distinct().Take(10).ToArray();
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(matches));return matches.Any(l=>l.Contains("Enabled")&&l.Contains("True Analogs"))?0:1;
        }
        return 2;
    }
}
