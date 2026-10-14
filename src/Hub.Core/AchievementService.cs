using System.Buffers.Binary;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Hub.Core;

public sealed record AchievementEntry(string Id,string Title,string Description,int Points,bool Unlocked,string? EarnedAt,bool Hardcore=false,string? Grade=null,string? Icon=null,string? ProviderIconId=null,bool Hidden=false);
public sealed record AchievementSnapshot(string EditionId,string Provider,string Account,string SetId,DateTimeOffset Updated,List<AchievementEntry> Items);
public sealed record AchievementLink(string Username,int GameId);

// Read-only adapters; the emulator/service remains the authority for unlocks. JSON
// snapshots are portable, offline-capable, and separated by edition, source and account.
public sealed class AchievementService(HubPaths paths)
{
    public List<AchievementSnapshot> Cached(string edition)
    {
        var root=paths.Managed("achievements",SafeFiles.Id(edition));if(!Directory.Exists(root))return [];
        var result=new List<AchievementSnapshot>();long bytes=0;var items=0;
        foreach(var file in Directory.EnumerateFiles(root,"*.json").Take(128))
        {
            try
            {
                SafeFiles.NoLinks(file);using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);var length=stream.Length;
                if(length>4*1024*1024||bytes+length>16*1024*1024)continue;
                // A failed parse still consumed a read. Charge it before opening
                // the JSON so damaged historical files cannot bypass the budget.
                bytes+=length;
                var s=JsonSerializer.Deserialize<AchievementSnapshot>(stream,JsonData.Options);
                if(s==null)continue;
                if(s.EditionId==edition&&Valid(s)&&items+s.Items.Count<=10000){result.Add(s);items+=s.Items.Count;}
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or HubException){} // Keep unreadable records; another snapshot can still be shown.
        }
        return result.Where(s=>s.Account!="Game metadata"||!result.Any(p=>p.Provider==s.Provider&&p.SetId==s.SetId&&p.Account!="Game metadata")).OrderByDescending(s=>s.Updated).ToList();
    }
    private static bool Valid(AchievementSnapshot? s)
    {
        static bool Text(string? value,int maximum,bool nonempty=false)=>value!=null&&value.Length<=maximum&&(!nonempty||value.Length>0);
        return s!=null&&Text(s.EditionId,128,true)&&s.Provider is "RPCS3" or "Xenia" or "RetroAchievements"&&Text(s.Account,128,true)&&Text(s.SetId,128,true)&&s.Items is {Count:<=10000}&&s.Items.All(i=>i!=null&&Text(i.Id,64,true)&&Text(i.Title,512)&&Text(i.Description,8192)&&i.Points is >=0 and <=10000&&(i.EarnedAt==null||Text(i.EarnedAt,128))&&(i.Grade==null||i.Grade is "Platinum" or "Gold" or "Silver" or "Bronze")&&(i.Icon==null||Text(i.Icon,256))&&(i.ProviderIconId==null||Text(i.ProviderIconId,80)))&&s.Items.Select(i=>i.Id).Distinct().Count()==s.Items.Count;
    }
    public static AchievementSnapshot MergeEarned(AchievementSnapshot current,AchievementSnapshot? previous)
    {
        if(!Valid(current))throw new HubException("Achievement records are malformed or exceed their limits.");
        if(!Valid(previous)||previous!.EditionId!=current.EditionId||previous.Provider!=current.Provider||previous.Account!=current.Account||previous.SetId!=current.SetId)return current;
        var old=previous.Items.ToDictionary(i=>i.Id);
        // Keep only provider-current definitions. Earned progress is monotonic within
        // one account/set; restoring a save must not revoke a genuine earlier unlock.
        return current with{Items=current.Items.Select(i=>old.TryGetValue(i.Id,out var prior)?i with
        {Unlocked=i.Unlocked||prior.Unlocked,Hardcore=i.Hardcore||prior.Hardcore,EarnedAt=prior.EarnedAt??i.EarnedAt,
         Title=i.Hidden&&!i.Unlocked&&prior.Unlocked?prior.Title:i.Title,Description=i.Hidden&&!i.Unlocked&&prior.Unlocked?prior.Description:i.Description,Icon=i.Icon??prior.Icon}:i).ToList()};
    }
    private AchievementSnapshot Save(AchievementSnapshot snapshot)
    {
        if(!Valid(snapshot))throw new HubException("Achievement records are malformed or exceed their limits.");
        var name=SafeFiles.HashText(snapshot.Provider+"/"+snapshot.Account+"/"+snapshot.SetId);
        var file=paths.Managed("achievements",SafeFiles.Id(snapshot.EditionId),name+".json");
        if(File.Exists(file)&&new FileInfo(file).Length<=4*1024*1024)try{snapshot=MergeEarned(snapshot,JsonData.ReadBounded<AchievementSnapshot>(file,4*1024*1024));}catch(Exception e)when(e is JsonException or HubException or IOException or UnauthorizedAccessException){}
        if(JsonSerializer.SerializeToUtf8Bytes(snapshot,JsonData.Options).Length>4*1024*1024)throw new HubException("Achievement records exceed their storage limit.");JsonData.WriteAtomic(file,snapshot);return snapshot;
    }
    public List<AchievementSnapshot> ReadLocal(Edition edition,string backend,string? gameMedia=null)
    {
        var live=new SaveService(paths).Live(edition.SaveFamily,backend);var result=new List<AchievementSnapshot>();var icons=new AchievementIconStore(paths);
        if(backend=="xenia"&&Directory.Exists(live))
        {
            var title=MediaService.NormalizeSerial(edition.TitleId??edition.Serial);
            foreach(var path in SafeFiles.Files(live).Where(p=>Path.GetFileName(p).Equals(title+".gpd",StringComparison.OrdinalIgnoreCase)).Take(16))
            {
                var account=Path.GetRelativePath(live,path).Split(Path.DirectorySeparatorChar)[0];
                var data=Bounded(path);var items=ReadGpd(data);Dictionary<ulong,byte[]> images;try{images=ReadGpdImages(data);}catch(HubException){images=[];}
                items=items.Select(i=>i.Hidden&&!i.Unlocked?i:i with {Icon=i.ProviderIconId!=null&&ulong.TryParse(i.ProviderIconId,out var image)&&images.TryGetValue(image,out var png)?CacheIcon(png):null}).ToList();
                result.Add(new(edition.Id,"Xenia",account,title,DateTimeOffset.UtcNow,items));
            }
        }
        else if(backend=="rpcs3")
        {
            string? trophyRoot=null;HashSet<string>? mediaSets=null;
            if(gameMedia!=null&&Directory.Exists(gameMedia))
            {
                SafeFiles.NoLinks(gameMedia);var game=Directory.Exists(SafeFiles.Within(gameMedia,"PS3_GAME"))?SafeFiles.Within(gameMedia,"PS3_GAME"):gameMedia;
                trophyRoot=SafeFiles.Within(game,"TROPDIR");mediaSets=Directory.Exists(trophyRoot)?Directory.EnumerateDirectories(trophyRoot).Take(32).Select(Path.GetFileName).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase):[];
            }
            var home=SafeFiles.Within(live,"dev_hdd0/home");
            if(Directory.Exists(home))foreach(var conf in SafeFiles.Files(home).Where(p=>Path.GetFileName(p).Equals("TROPCONF.SFM",StringComparison.OrdinalIgnoreCase)).Take(128))
            {
                var folder=Path.GetDirectoryName(conf)!;var dat=SafeFiles.Within(folder,"TROPUSR.DAT");
                if(mediaSets!=null&&!mediaSets.Contains(Path.GetFileName(folder)))continue;
                var account=Path.GetRelativePath(home,conf).Split(Path.DirectorySeparatorChar)[0];
                var items=File.Exists(dat)?ReadTrophies(Bounded(dat),Bounded(conf)):ReadTrophyMetadata(Bounded(conf));
                items=items.Select(i=>i.Hidden&&!i.Unlocked?i:i with{Icon=ReadIconFile(folder,"TROP"+uint.Parse(i.Id,CultureInfo.InvariantCulture).ToString("D3",CultureInfo.InvariantCulture)+".PNG")}).ToList();
                result.Add(new(edition.Id,"RPCS3",account,Path.GetFileName(folder),DateTimeOffset.UtcNow,items));
            }
            if(trophyRoot!=null&&Directory.Exists(trophyRoot))
            {
                foreach(var folder in Directory.EnumerateDirectories(trophyRoot).Take(32))
                {
                    SafeFiles.NoLinks(folder);var set=Path.GetFileName(folder);if(result.Any(s=>s.SetId==set))continue;
                    var conf=SafeFiles.Within(folder,"TROPCONF.SFM");var archive=SafeFiles.Within(folder,"TROPHY.TRP");List<AchievementEntry> items;
                    if(File.Exists(conf))items=ReadTrophyMetadata(Bounded(conf)).Select(i=>i.Hidden?i:i with{Icon=ReadIconFile(folder,"TROP"+uint.Parse(i.Id,CultureInfo.InvariantCulture).ToString("D3",CultureInfo.InvariantCulture)+".PNG")}).ToList();
                    else if(File.Exists(archive))
                    {
                        var files=TrophyArchive.Read(Bounded(archive));
                        var metadata=new[]{"TROP01.SFM","TROPCONF.SFM","TROP.SFM"}.Select(n=>files.GetValueOrDefault(n)).FirstOrDefault(v=>v!=null);if(metadata==null)continue;
                        items=ReadTrophyMetadata(metadata).Select(i=>i.Hidden?i:i with{Icon=files.TryGetValue("TROP"+uint.Parse(i.Id,CultureInfo.InvariantCulture).ToString("D3",CultureInfo.InvariantCulture)+".PNG",out var png)?CacheIcon(png):null}).ToList();
                    }
                    else continue;
                    result.Add(new(edition.Id,"RPCS3","Game metadata",set,DateTimeOffset.UtcNow,items));
                }
            }
        }
        return result.Select(Save).ToList();
        string? CacheIcon(byte[] png){try{return icons.Cache(png);}catch(HubException){return null;}}
        string? ReadIconFile(string folder,string name)
        {
            try{var file=SafeFiles.Within(folder,name);return File.Exists(file)&&new FileInfo(file).Length<=AchievementIconStore.MaxBytes?CacheIcon(Bounded(file)):null;}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or HubException){return null;}
        }
    }
    private static byte[] Bounded(string path)
    {SafeFiles.NoLinks(path);if(new FileInfo(path).Length>16*1024*1024)throw new HubException("Achievement data exceeds its size limit.");return File.ReadAllBytes(path);}
    public static List<AchievementEntry> ReadGpd(byte[] bytes)
    {
        if(bytes.Length<24 || !bytes.AsSpan(0,4).SequenceEqual("XDBF"u8))throw new HubException("Unrecognized Xbox achievement data.");
        uint U32(int p){if(p<0||p>bytes.Length-4)throw new HubException("Truncated achievement data.");return BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(p,4));}
        var count=U32(8);var used=U32(12);var free=U32(16);long dataStart=24L+count*18L+free*8L;
        if(count>100000||used>count||dataStart>bytes.Length)throw new HubException("Invalid Xbox achievement table.");
        var result=new List<AchievementEntry>();
        for(var i=0;i<used;i++)
        {
            var p=24+i*18;if(BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(p,2))!=1)continue;
            long at=dataStart+U32(p+10);var length=U32(p+14);if(at>bytes.Length||length>bytes.Length-at)throw new HubException("Invalid achievement entry.");
            if(length<28 || U32((int)at)!=28)continue;var end=checked((int)(at+length));var n=(int)at;
            string Str(ref int pos){var start=pos;while(pos+1<end && (bytes[pos]!=0||bytes[pos+1]!=0))pos+=2;if(pos+1>=end)throw new HubException("Truncated achievement text.");var text=Encoding.BigEndianUnicode.GetString(bytes,start,pos-start);pos+=2;return text;}
            var textAt=n+28;var title=Str(ref textAt);var earned=Str(ref textAt);var locked=Str(ref textAt);var unlocked=(U32(n+16)&0x20000)!=0;
            string? stamp=null;var fileTime=BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(n+20,8));
            if(unlocked && fileTime>0){try{stamp=DateTimeOffset.FromFileTime(fileTime).ToString("O");}catch(ArgumentOutOfRangeException){}}
            var hidden=(U32(n+16)&8)==0;
            result.Add(new(U32(n+4).ToString(),hidden&&!unlocked?"Secret achievement":title,hidden&&!unlocked?"Unlock this achievement to reveal its details.":unlocked?earned:locked,checked((int)U32(n+12)),unlocked,stamp,ProviderIconId:U32(n+8).ToString(CultureInfo.InvariantCulture),Hidden:hidden));
        }
        return result;
    }
    public static Dictionary<ulong,byte[]> ReadGpdImages(byte[] bytes)
    {
        if(bytes.Length<24||!bytes.AsSpan(0,4).SequenceEqual("XDBF"u8))throw new HubException("Unrecognized Xbox achievement data.");
        var count=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8));var used=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(12));var free=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16));
        var start=24UL+count*18UL+free*8UL;if(count>100000||used>count||start>(ulong)bytes.Length)throw new HubException("Invalid Xbox image table.");
        var result=new Dictionary<ulong,byte[]>();long total=0;
        for(var i=0;i<used;i++)
        {
            var p=24+checked((int)i)*18;if(BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(p))!=2)continue;
            var id=BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(p+2));var at=start+BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(p+10));var length=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(p+14));
            if(at>(ulong)bytes.Length||length>(ulong)bytes.Length-at||length>AchievementIconStore.MaxBytes||(total+=length)>16*1024*1024)throw new HubException("Invalid Xbox achievement image extent.");
            if(!result.TryAdd(id,bytes.AsSpan(checked((int)at),checked((int)length)).ToArray()))throw new HubException("Duplicate Xbox achievement image ID.");
        }
        return result;
    }
    public static List<AchievementEntry> ReadTrophies(byte[] dat,byte[] xml)
    {
        if(dat.Length<48 || BinaryPrimitives.ReadUInt32BigEndian(dat)!=0x818F54AD)throw new HubException("Unrecognized PS3 trophy data.");
        var count=BinaryPrimitives.ReadUInt32BigEndian(dat.AsSpan(8));if(count>128||48L+count*32>dat.Length)throw new HubException("Invalid trophy table.");
        var unlocked=new Dictionary<uint,bool>();
        for(var i=0;i<count;i++)
        {
            var p=48+i*32;if(BinaryPrimitives.ReadUInt32BigEndian(dat.AsSpan(p))!=6)continue;
            var entries=BinaryPrimitives.ReadUInt32BigEndian(dat.AsSpan(p+12));var offset=BinaryPrimitives.ReadUInt64BigEndian(dat.AsSpan(p+16));
            if(entries>10000||offset>(ulong)dat.Length||entries*112UL>(ulong)dat.Length-offset)throw new HubException("Truncated trophy entries.");
            for(var j=0;j<entries;j++){var n=checked((int)(offset+(ulong)j*112UL));unlocked[BinaryPrimitives.ReadUInt32BigEndian(dat.AsSpan(n+16))]=BinaryPrimitives.ReadUInt32BigEndian(dat.AsSpan(n+20))!=0;}
        }
        return ReadTrophyMetadata(xml,unlocked);
    }
    public static List<AchievementEntry> ReadTrophyMetadata(byte[] xml,Dictionary<uint,bool>? unlocked=null)
    {
        using var stream=new MemoryStream(xml);using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4*1024*1024});var document=XDocument.Load(reader);var ids=new HashSet<uint>();
        return document.Descendants("trophy").Take(10000).Select(t=>
        {
            if(!uint.TryParse(t.Attribute("id")?.Value,NumberStyles.None,CultureInfo.InvariantCulture,out var id)||id>999||!ids.Add(id))throw new HubException("Invalid or duplicate trophy ID.");
            var grade=t.Attribute("ttype")?.Value switch{"B"=>"Bronze","S"=>"Silver","G"=>"Gold","P"=>"Platinum",_=>null};
            var earned=unlocked?.GetValueOrDefault(id)==true;var hidden=t.Attribute("hidden")?.Value.Equals("y",StringComparison.OrdinalIgnoreCase)==true;
            return new AchievementEntry(id.ToString(),hidden&&!earned?"Secret trophy":t.Element("name")?.Value??"Trophy "+id,hidden&&!earned?"Unlock this trophy to reveal its details.":t.Element("detail")?.Value??"",0,earned,null,Grade:grade,ProviderIconId:"TROP"+id.ToString("D3",CultureInfo.InvariantCulture),Hidden:hidden);
        }).ToList();
    }
    public async Task<AchievementSnapshot> SyncNativeAsync(Edition edition,bool offline,CancellationToken ct=default,HttpClient? nativeClient=null)
    {
        var support=AchievementSupport.Get(edition);if(support.Provider!="RetroAchievements"||support.GameId is not {} id)throw new HubException("There is no verified RetroAchievements set ID for this title/platform.");
        using var native=new NativeRetroClient(paths,nativeClient);var data=await native.ReadProgressAsync(id,offline,ct);
        var snapshot=ParseNative(edition,data.Account.Username,id,data.Patch,data.Soft,data.Hardcore);
        using var imageClient=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(30)};
        snapshot=Save(await CacheRetroBadges(snapshot,imageClient,ct));JsonData.WriteAtomic(paths.Managed("config","achievements",edition.Id+".json"),new AchievementLink(data.Account.Username,id));return snapshot;
    }
    public static AchievementSnapshot ParseNative(Edition edition,string username,int gameId,byte[] patch,byte[] soft,byte[] hardcore)
    {
        if(string.IsNullOrWhiteSpace(username)||username.Length>128||patch.Length>8*1024*1024||soft.Length>256*1024||hardcore.Length>256*1024)throw new HubException("Achievement account or response exceeds its limits.");
        try
        {
            using var document=JsonDocument.Parse(patch);var root=document.RootElement;NativeRetroClient.RequireSuccess(root,"RetroAchievements could not load this set. Sign in again or try later.");
            var data=root.GetProperty("PatchData");if(data.GetProperty("ID").GetInt32()!=gameId||data.GetProperty("ConsoleID").GetInt32()!=AchievementSupport.ConsoleId(edition))throw new HubException("That achievement data belongs to another game or console.");
            if(AchievementSupport.Get(edition).GameId is {} known&&known!=gameId)throw new HubException("That achievement data does not match this game's verified set ID.");
            HashSet<string> Unlocks(byte[] bytes)
            {
                using var response=JsonDocument.Parse(bytes);NativeRetroClient.RequireSuccess(response.RootElement,"RetroAchievements could not read progress. Sign in again or try later.");
                var entries=response.RootElement.GetProperty("UserUnlocks");if(entries.ValueKind!=JsonValueKind.Array||entries.GetArrayLength()>10000)throw new HubException("Invalid achievement unlock list.");
                return entries.EnumerateArray().Select(i=>i.GetUInt32().ToString(System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
            }
            var earned=Unlocks(soft);var hard=Unlocks(hardcore);var definitions=data.GetProperty("Achievements");if(definitions.ValueKind!=JsonValueKind.Array||definitions.GetArrayLength()>10000)throw new HubException("Invalid achievement definition list.");
            var items=new List<AchievementEntry>();var ids=new HashSet<string>();
            foreach(var row in definitions.EnumerateArray())
            {
                if(!row.TryGetProperty("Flags",out var flags)||flags.GetInt32()!=3)continue;var id=row.GetProperty("ID").GetUInt32().ToString(System.Globalization.CultureInfo.InvariantCulture);if(!ids.Add(id))throw new HubException("Duplicate achievement ID.");
                var badge=row.TryGetProperty("BadgeName",out var art)?art.GetString():null;var title=row.GetProperty("Title").GetString()??"";var description=row.GetProperty("Description").GetString()??"";
                var points=row.GetProperty("Points").GetInt32();if(title.Length>512||description.Length>8192||points is <0 or >10000)throw new HubException("Achievement text or points exceed their limits.");
                items.Add(new(id,title,description,points,earned.Contains(id)||hard.Contains(id),null,hard.Contains(id),ProviderIconId:AchievementIconStore.BadgeUri(badge)!=null?badge:null));
            }
            return new(edition.Id,"RetroAchievements",username,gameId.ToString(),DateTimeOffset.UtcNow,items);
        }
        catch(Exception e)when(e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException){throw new HubException("RetroAchievements returned unreadable game/progress data. Cached records were kept.");}
    }
    public static AchievementSnapshot ParseRetro(Edition edition,AchievementLink link,byte[] bytes)
    {
        using var json=JsonDocument.Parse(bytes);var root=json.RootElement;
        var expected=edition.Platform switch{"ps1"=>12,"ps2"=>21,"psp"=>41,_=>-1};
        if(!root.TryGetProperty("ConsoleID",out var console)||!console.TryGetInt32(out var platform)||platform!=expected)throw new HubException("That achievement set belongs to another console.");
        if(!root.TryGetProperty("Title",out var title)||title.ValueKind!=JsonValueKind.String||!(title.GetString()??"").Contains("Armored Core",StringComparison.OrdinalIgnoreCase))throw new HubException("Choose an Armored Core achievement set.");
        var rows=new List<AchievementEntry>();
        if(root.TryGetProperty("Achievements",out var achievements)&&achievements.ValueKind==JsonValueKind.Object)
            foreach(var item in achievements.EnumerateObject().Take(10000))
            {
                var v=item.Value;string Text(string name)=>v.TryGetProperty(name,out var value)?value.ToString():"";
                var earned=Text("DateEarned");var hardcore=Text("DateEarnedHardcore");
                var badge=Text("BadgeName");rows.Add(new(item.Name,Text("Title"),Text("Description"),int.TryParse(Text("Points"),out var points)?points:0,earned.Length>0||hardcore.Length>0,earned.Length>0?earned:hardcore.Length>0?hardcore:null,hardcore.Length>0,ProviderIconId:AchievementIconStore.BadgeUri(badge)!=null?badge:null));
            }
        return new(edition.Id,"RetroAchievements",link.Username,link.GameId.ToString(),DateTimeOffset.UtcNow,rows);
    }
    private async Task<AchievementSnapshot> CacheRetroBadges(AchievementSnapshot snapshot,HttpClient client,CancellationToken ct)
    {
        var icons=new AchievementIconStore(paths);var prior=Cached(snapshot.EditionId).Where(s=>s.Provider==snapshot.Provider&&s.SetId==snapshot.SetId).SelectMany(s=>s.Items).Where(i=>i.ProviderIconId!=null&&icons.TryPath(i.Icon)!=null).GroupBy(i=>i.ProviderIconId!).ToDictionary(g=>g.Key,g=>g.First().Icon!);
        var cached=new System.Collections.Concurrent.ConcurrentDictionary<string,string>(prior);var ids=snapshot.Items.Where(i=>!i.Hidden||i.Unlocked).Select(i=>i.ProviderIconId).Where(b=>AchievementIconStore.BadgeUri(b)!=null).Distinct().Take(512).ToArray();long downloaded=0;
        await Parallel.ForEachAsync(ids,new ParallelOptions{MaxDegreeOfParallelism=4,CancellationToken=ct},async(badge,token)=>
        {
            if(cached.ContainsKey(badge!))return;
            try
            {
                using var response=await client.GetAsync(AchievementIconStore.BadgeUri(badge),HttpCompletionOption.ResponseHeadersRead,token);if(!response.IsSuccessStatusCode)return;
                if(response.Content.Headers.ContentLength>AchievementIconStore.MaxBytes)return;
                await using var input=await response.Content.ReadAsStreamAsync(token);using var output=new MemoryStream();var chunk=new byte[8192];int read;
                while((read=await input.ReadAsync(chunk,token))>0){if(output.Length+read>AchievementIconStore.MaxBytes||Interlocked.Add(ref downloaded,read)>16*1024*1024)return;output.Write(chunk,0,read);}
                cached[badge!]=icons.Cache(output.ToArray());
            }
            catch(Exception e)when(e is HttpRequestException or IOException or HubException){ } // Missing art does not discard valid progress.
            catch(OperationCanceledException)when(!token.IsCancellationRequested){ }
        });
        return snapshot with{Items=snapshot.Items.Select(i=>i.Hidden&&!i.Unlocked?i:i with{Icon=i.ProviderIconId!=null?cached.GetValueOrDefault(i.ProviderIconId):null}).ToList()};
    }
}
