using System.Diagnostics;

namespace Hub.Core;

// One bounded, muted preview from the user's own opening/menu movie. No game files or
// generated previews enter a release package. The optional decoder is a verified tool.
public sealed class MediaPreviewService(HubPaths paths, Catalog catalog, ProviderDownloader downloader)
{
    private static readonly SemaphoreSlim Gate = new(1,1);
    public static int PreviewStart(string? editionId, GameMovieSource movie)
    {
        var name=Path.GetFileNameWithoutExtension(movie.Name);
        if(movie.Length<50*1024*1024)return 0;
        if(editionId?.StartsWith("ac4-ps3-",StringComparison.Ordinal)==true&&name.Equals("opening",StringComparison.OrdinalIgnoreCase))return 80;
        if(editionId?.StartsWith("ac2-ps2-",StringComparison.Ordinal)==true&&name.StartsWith("AC2OP",StringComparison.OrdinalIgnoreCase))return 10;
        if(editionId?.StartsWith("aclr-ps2-",StringComparison.Ordinal)==true&&name.Equals("O1",StringComparison.OrdinalIgnoreCase))return 55;
        if(editionId?.StartsWith("acv-x360-",StringComparison.Ordinal)==true&&name.Equals("acv_opening",StringComparison.OrdinalIgnoreCase))return 15;
        return 0;
    }
    public static string? FindMovie(string source)
        => GameMovieDiscovery.Find(source)?.File;
    public async Task<GamePresentationAssets> ReadAsync(MediaRecord media, bool offline, CancellationToken ct=default)
    {
        var assets=await new GameAssetReader(paths).ReadAsync(media,ct:ct);
        if(assets.Video!=null) return assets;
        GameMovieSource? movie;
        try
        {
            if(assets.ConsoleMovie is {} embedded){var file=paths.Managed("artwork",embedded);movie=new(file,Path.GetFileName(file),0,new FileInfo(file).Length,GameMovieDiscovery.Format(file));}
            else movie=await Task.Run(()=>GameMovieDiscovery.Find(paths.Resolve(media.Path),ct),ct);
        }
        catch(Exception e)when(e is IOException or HubException or UnauthorizedAccessException)
        {return assets with{Notes=[..assets.Notes,"The disc movie directory could not be read. Showing still artwork."]};}
        if(movie==null)return assets;
        await Gate.WaitAsync(ct);
        try
        {
            var start=PreviewStart(media.EditionId,movie).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var id=SafeFiles.HashText(media.Fingerprint+"/preview-v3/"+movie.Name+"/"+movie.Offset+"/"+movie.Length+"/"+start);
            var output=paths.Managed("artwork",id+".mp4");var still=paths.Managed("artwork",id+".jpg");
            if(!File.Exists(output)||!File.Exists(still))
            {
                var exe=await Decoder(offline,ct);
                var temp=output+"."+Guid.NewGuid().ToString("N")+".mp4";var tempStill=still+"."+Guid.NewGuid().ToString("N")+".jpg";
                try
                {
                    // MPEG-4 Part 2 is available in the LGPL build and Windows Media Foundation.
                    var format=movie.Format.Length>0?new[]{"-f",movie.Format}:Array.Empty<string>();
                    // AC4's long opening begins with near-black particles. Its later factory scene
                    // gives a useful still; short menu loops start at zero to avoid empty output.
                    await Run(exe,["-nostdin","-hide_banner","-loglevel","error","-threads","2","-protocol_whitelist","file,subfile,pipe",..format,"-ss",start,"-i",movie.InputUrl(),"-t","20","-an","-sn","-dn","-vf","scale=1280:720:force_original_aspect_ratio=decrease:force_divisible_by=2,fps=24","-c:v","mpeg4","-q:v","4","-threads","2","-movflags","+faststart","-y",temp],ct);
                    await Run(exe,["-nostdin","-hide_banner","-loglevel","error","-protocol_whitelist","file,pipe","-i",temp,"-vf","thumbnail=480","-frames:v","1","-q:v","2","-threads","1","-y",tempStill],ct);
                    if(new FileInfo(temp).Length is <1024 or >64*1024*1024 || !File.Exists(tempStill)) throw new HubException("The game movie did not produce a usable preview.");
                    File.Move(temp,output,true); File.Move(tempStill,still,true);
                }
                finally {if(File.Exists(temp))File.Delete(temp);if(File.Exists(tempStill))File.Delete(tempStill);}
            }
            assets=assets with {Background=assets.Background??Path.GetFileName(still),Video=Path.GetFileName(output),Notes=["Preview generated from this game's opening/menu movie: "+movie.Name],ReaderVersion=5};
            JsonData.WriteAtomic(paths.Managed("artwork","extracted",media.Id,"assets.json"),assets); return assets;
        }
        catch(Exception e) when(e is IOException or HubException or System.Net.Http.HttpRequestException)
        {return assets with {Notes=[..assets.Notes,"Preview unavailable: "+(e is HubException?e.Message:"Could not prepare the local movie. Retry in Artwork.")]};}
        finally{Gate.Release();}
    }
    private async Task<string> Decoder(bool offline,CancellationToken ct)
    {
        var p=catalog.Providers.Single(x=>x.Id=="ffmpeg-preview-8.1");
        var root=paths.Managed("runtime","tools",p.Sha256);var receipt=Path.Combine(root,"hub-tool.json");
        if(File.Exists(receipt))
        {
            var files=JsonData.Read<List<MediaFile>>(receipt);
            foreach(var f in files) if(await SafeFiles.HashAsync(SafeFiles.Within(root,f.RelativeName),ct)!=f.Sha256)throw new HubException("The preview decoder changed. Reinstall its tool folder.");
            return SafeFiles.Within(root,files.Single(f=>Path.GetFileName(f.RelativeName)=="ffmpeg.exe").RelativeName);
        }
        var archive=await downloader.FetchAsync(p,offline,ct:ct);var stage=root+".partial-"+Guid.NewGuid().ToString("N");
        try
        {
            await ArchiveInstaller.ExtractAsync(archive,stage,ct,512L*1024*1024);
            var files=new List<MediaFile>();
            foreach(var file in SafeFiles.Files(stage).Where(f=>Path.GetExtension(f) is ".exe" or ".dll"))
                files.Add(new(Path.GetRelativePath(stage,file),new FileInfo(file).Length,await SafeFiles.HashAsync(file,ct),0));
            if(files.Count==0 || files.Count>50)throw new HubException("Unexpected preview decoder layout.");
            JsonData.WriteAtomic(Path.Combine(stage,"hub-tool.json"),files);await SafeFiles.MoveManagedDirectoryAsync(paths.Root,stage,root,ct);
            return SafeFiles.Within(root,files.Single(f=>Path.GetFileName(f.RelativeName)=="ffmpeg.exe").RelativeName);
        }
        finally{SafeFiles.DeleteManagedTree(paths.Root,stage);}
    }
    private static async Task Run(string exe,string[] args,CancellationToken ct)
    {
        var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};foreach(var a in args)info.ArgumentList.Add(a);
        using var owned=new OwnedProcessGroup();
        using var process=Process.Start(info)??throw new HubException("The preview decoder could not start.");owned.Add(process);
        var errors=process.StandardError.BaseStream.CopyToAsync(Stream.Null);var output=process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try{await process.WaitForExitAsync(timeout.Token);owned.Dispose();await Task.WhenAll(errors,output);if(process.ExitCode!=0)throw new HubException("This movie format could not be decoded.");}
        catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new HubException("The local movie preview timed out. Showing still artwork.");}
        finally
        {
            if(!process.HasExited){try{process.Kill(true);}catch(InvalidOperationException){}await process.WaitForExitAsync(CancellationToken.None);}
            owned.Dispose();await Task.WhenAll(errors,output);
        }
    }
}
