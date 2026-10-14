using System.Text.RegularExpressions;

namespace Hub.Core;

public sealed record GameMovieSource(string File, string Name, long Offset, long Length, string Format)
{
    public string InputUrl()
    {
        SafeFiles.NoLinks(File);var path=Path.GetFullPath(File);var size=new FileInfo(path).Length;
        if(Offset<0||Length<=0||Offset>size-Length)throw new HubException("The movie extends outside its source file.");
        return Offset==0&&Length==size?path:$"subfile,,start,{Offset},end,{checked(Offset+Length)},,:file:{path}";
    }
}

public static class GameMovieDiscovery
{
    private static readonly HashSet<string> Extensions=new(StringComparer.OrdinalIgnoreCase){".pam",".pmf",".pss",".mpg",".mpeg",".mp4",".m4v",".wmv",".avi",".m2v",".bik",".bk2",".str"};
    public static string Format(string name)=>Path.GetExtension(name).ToLowerInvariant() switch {
        ".pam" or ".pmf" or ".pss" or ".mpg" or ".mpeg"=>"mpeg", ".m2v"=>"mpegvideo", ".bik" or ".bk2"=>"bink", ".str"=>"psxstr", _=>""};
    public static int Rank(string path)
    {
        var name=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();var normalized=path.Replace('\\','/').ToLowerInvariant();
        if(!Extensions.Contains(Path.GetExtension(path))||normalized.Contains("/mission/")||normalized.Contains("/keyconf/")||normalized.Contains("/mail/")
            ||Regex.IsMatch(name,@"^(?:end|staff|credit|logo|tutorial|es[0-9])"))return int.MaxValue;
        if(name.Contains("opening")||name.Contains("intro")||Regex.IsMatch(name,@"(?:^|[a-z0-9])op(?:[0-9]{0,3})$")||name is "o1" or "opmovie")return 0;
        if(name is "title" or "icon1"||name.StartsWith("title_"))return 1;
        // Disc-specific abbreviated movie names cannot safely be guessed as opening movies.
        return int.MaxValue;
    }
    public static GameMovieSource? Find(string source, CancellationToken ct=default)
    {
        SafeFiles.NoLinks(source);
        if(File.Exists(source))
        {
            if(!Path.GetExtension(source).Equals(".iso",StringComparison.OrdinalIgnoreCase))return null;
            var choice=DiscMovieReader.Inventory(source,ct).Where(f=>f.Length>1024&&Rank(f.Name)<int.MaxValue)
                .OrderBy(f=>Rank(f.Name)).ThenBy(f=>f.Name,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            return choice==null?null:new(source,choice.Name,choice.Offset,choice.Length,Format(choice.Name));
        }
        if(!Directory.Exists(source))return null;
        var candidates=new List<GameMovieSource>();var pending=new Queue<(string,int)>();var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var relative in new[]{"PS3_GAME/USRDIR/movie","PS3_GAME/USRDIR/movies","PSP_GAME/USRDIR/movie","movie","movies"})
        {var root=SafeFiles.Within(source,relative);if(Directory.Exists(root))pending.Enqueue((root,0));}
        pending.Enqueue((source,0));var seen=0;var files=0;
        while(pending.Count>0&&seen++<300&&files<10000)
        {
            ct.ThrowIfCancellationRequested();var (directory,depth)=pending.Dequeue();if(!visited.Add(directory))continue;SafeFiles.NoLinks(directory);
            foreach(var path in Directory.EnumerateFiles(directory))
            {
                if(++files>10000)break;
                var relative=Path.GetRelativePath(source,path);if(Rank(relative)==int.MaxValue)continue;SafeFiles.NoLinks(path);
                var size=new FileInfo(path).Length;if(size>1024)candidates.Add(new(path,relative,0,size,Format(path)));
            }
            if(depth<6)foreach(var child in Directory.EnumerateDirectories(directory).Take(300).OrderBy(d=>Path.GetFileName(d).Contains("movie",StringComparison.OrdinalIgnoreCase)?0:1)){SafeFiles.NoLinks(child);pending.Enqueue((child,depth+1));}
        }
        return candidates.OrderBy(c=>Rank(c.Name)).ThenBy(c=>c.Name,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }
}
