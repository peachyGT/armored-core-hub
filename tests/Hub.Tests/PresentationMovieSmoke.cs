using System.Diagnostics;
using Hub.Core;

static class PresentationMovieSmoke
{
    public static async Task<int> Run(string[] args)
    {
        var sourcePaths=new HubPaths(args[0]);var catalog=CatalogStore.Load(args[1]);var targetPaths=new HubPaths(args[2]);
        var state=new StateStore(sourcePaths).Load();var ids=args.Length>3?args.Skip(3).ToArray():new[]{"ac2-ps2-slus20014","aclr-ps2-slus21338","acv-x360-4e4d084f-us"};
        using var http=ProviderDownloader.CreateHttpClient();var service=new MediaPreviewService(targetPaths,catalog,new(targetPaths,http));
        var results=new List<object>();var passed=true;
        foreach(var edition in ids)
        {
            var choice=state.Selections.GetValueOrDefault(edition);var media=state.Media.First(m=>m.EditionId==edition&&(choice?.MediaId==null||m.Id==choice.MediaId));media=media with{Path=sourcePaths.Resolve(media.Path)};
            var movie=GameMovieDiscovery.Find(media.Path);var sourceFile=movie?.File??media.Path;
            var original=File.Exists(sourceFile)?new FileInfo(sourceFile):null;var length=original?.Length;var stamp=original?.LastWriteTimeUtc;var watch=Stopwatch.StartNew();
            var assets=await service.ReadAsync(media,true);original?.Refresh();
            var unchanged=original!=null&&original.Length==length&&original.LastWriteTimeUtc==stamp;
            var ok=assets.Background!=null&&assets.Video!=null&&File.Exists(targetPaths.Managed("artwork",assets.Background))&&File.Exists(targetPaths.Managed("artwork",assets.Video))&&unchanged;
            passed&=ok;results.Add(new{edition,passed=ok,elapsedSeconds=watch.Elapsed.TotalSeconds,movie,assets,sourceSizeAndStampUnchanged=unchanged});
        }
        JsonData.WriteAtomic(targetPaths.Managed("logs","movie-presentation-smoke.json"),results);Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(results,JsonData.Options));return passed?0:1;
    }
}
