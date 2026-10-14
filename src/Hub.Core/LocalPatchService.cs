using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Hub.Core;

public sealed record LocalGamePatch(string Id,string EditionId,string Name,string Kind,string SourceHash,string PayloadHash,string Payload,string? Crc,string? DerivedMediaId,bool Analog,string[]? EnabledSections=null);

public sealed class LocalPatchService(HubPaths paths,Catalog catalog)
{
    public void SetAnalog(LocalGamePatch patch,bool enabled)
    {
        var file=paths.Managed("patches","local",SafeFiles.Id(patch.EditionId),SafeFiles.Id(patch.Id),"patch.json");
        var stored=ReadPatch(file);
        JsonData.WriteAtomic(file,stored with{Analog=enabled});
    }
    public static string[] PnachSections(string text)=>text.Split('\n').Select(l=>l.Trim()).Where(l=>l.StartsWith('[')&&l.EndsWith(']')).Select(l=>l[1..^1]).Where(n=>n.Length is >0 and <=160&&!n.Any(char.IsControl)).Distinct().Take(128).ToArray();
    public string[] Sections(LocalGamePatch patch)=>patch.Kind=="pnach"?PnachSections(Encoding.UTF8.GetString(ReadPayload(patch))):[];
    public void SetSections(LocalGamePatch patch,string[] selected)
    {
        if(selected.Except(Sections(patch)).Any())throw new HubException("Unknown patch option.");
        var file=paths.Managed("patches","local",SafeFiles.Id(patch.EditionId),SafeFiles.Id(patch.Id),"patch.json");
        JsonData.WriteAtomic(file,ReadPatch(file) with{EnabledSections=selected});
    }
    public string PnachText(LocalGamePatch patch)
    {
        var text=Encoding.UTF8.GetString(ReadPayload(patch));var selected=patch.EnabledSections??PnachSections(text);var active=true;var lines=new List<string>();
        foreach(var line in text.Split('\n')){var trimmed=line.Trim();if(trimmed.StartsWith('[')&&trimmed.EndsWith(']'))active=selected.Contains(trimmed[1..^1]);if(active)lines.Add(line);}
        return string.Join('\n',lines);
    }
    public bool AnalogActive(IEnumerable<LocalGamePatch> patches)=>patches.Any(p=>p.Analog
        && (p.Kind!="pnach"||PnachText(p).Split('\n').Any(line=>line.TrimStart().StartsWith("patch=",StringComparison.OrdinalIgnoreCase))));
    public List<LocalGamePatch> List(string edition) => Directory.Exists(paths.Managed("patches","local",edition))
        ? SafeFiles.Files(paths.Managed("patches","local",edition)).Where(f=>Path.GetFileName(f)=="patch.json").Select(ReadPatch).ToList() : [];
    private LocalGamePatch ReadPatch(string file)
    {
        try
        {
            var patch=JsonData.ReadBounded<LocalGamePatch>(file,128*1024);
            if(patch.Id==null||patch.EditionId==null||patch.SourceHash==null||patch.PayloadHash==null||patch.Payload==null||string.IsNullOrWhiteSpace(patch.Name)||patch.Name.Length>256
                ||patch.Kind is not("pnach" or "ppf")||patch.EnabledSections is {Length:>128}||patch.EnabledSections?.Any(s=>string.IsNullOrEmpty(s)||s.Length>160||s.Any(char.IsControl))==true)
                throw new HubException("Required local patch metadata is missing or exceeds its limit.");
            SafeFiles.Id(patch.Id);SafeFiles.Id(patch.EditionId);CatalogStore.ValidateHash("sha256",patch.SourceHash);CatalogStore.ValidateHash("sha256",patch.PayloadHash);
            var expected=paths.Managed("patches","local",patch.EditionId,patch.Id,"payload."+patch.Kind);
            if(!paths.Resolve(patch.Payload).Equals(expected,StringComparison.OrdinalIgnoreCase)||!Path.GetFullPath(file).Equals(Path.Combine(Path.GetDirectoryName(expected)!,"patch.json"),StringComparison.OrdinalIgnoreCase)
                ||patch.Kind=="pnach"&&(patch.Crc==null||!Regex.IsMatch(patch.Crc,"^[0-9A-Fa-f]{8}$")))throw new HubException("The local patch path or game CRC is invalid.");
            return patch;
        }
        catch(Exception e)when(e is JsonException or HubException){throw new HubException($"Local patch {file} is invalid: {e.Message}",e);}
    }
    private static byte[] ReadBytes(string file)
    {
        SafeFiles.NoLinks(file);using var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(input.Length is <8 or >16*1024*1024)throw new HubException("Choose a patch smaller than 16 MB.");
        var bytes=new byte[(int)input.Length];input.ReadExactly(bytes);return bytes;
    }
    private byte[] ReadPayload(LocalGamePatch patch)
    {
        var file=paths.Resolve(patch.Payload);
        if(!SafeFiles.IsWithin(paths.Managed("patches"),file))throw new HubException("The local patch payload is outside managed storage.");
        var bytes=ReadBytes(file);
        if(!Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)).Equals(patch.PayloadHash,StringComparison.OrdinalIgnoreCase))throw new HubException("The local patch payload changed.");
        return bytes;
    }
    public async Task<LocalGamePatch> ImportAsync(string file,MediaRecord source,HubState state,bool analog,CancellationToken ct=default)
    {
        SafeFiles.NoLinks(file);if(new FileInfo(file).Length is <8 or >16*1024*1024)throw new HubException("Choose a patch smaller than 16 MB.");
        var edition=catalog.Editions.Single(e=>e.Id==source.EditionId);var kind=Path.GetExtension(file).ToLowerInvariant();
        if(kind==".pnach" && edition.Platform!="ps2" || kind==".ppf" && edition.Platform is not ("ps1" or "ps2" or "psp"))throw new HubException("This patch format does not match the selected console.");
        if(kind is not (".pnach" or ".ppf"))throw new HubException("Choose a PCSX2 PNACH patch or a PPF image patch. Configuration modules use their own import button.");
        if(!await new MediaService(paths,catalog).VerifyAsync(source,ct))throw new HubException("The game changed since import. Verify or re-import it before adding a patch.");
        ct.ThrowIfCancellationRequested();var bytes=ReadBytes(file);ct.ThrowIfCancellationRequested();var hash=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        var id=SafeFiles.HashText(source.Fingerprint+hash);var root=paths.Managed("patches","local",edition.Id,id);var payload=Path.Combine(root,"payload"+kind);
        var descriptor=Path.Combine(root,"patch.json");LocalGamePatch? existing=null;
        if(File.Exists(descriptor))
        {
            existing=ReadPatch(descriptor);
            if(existing.Id!=id||existing.EditionId!=edition.Id||existing.SourceHash!=source.Fingerprint||existing.PayloadHash!=hash||existing.Kind!=kind[1..]
                ||!Path.GetFullPath(paths.Resolve(existing.Payload)).Equals(Path.GetFullPath(payload),StringComparison.OrdinalIgnoreCase))throw new HubException("The existing local patch metadata changed. Its files and settings were kept.");
            if(File.Exists(payload)&&await SafeFiles.HashAsync(payload,ct)==hash)return existing;
        }
        string? crc=null;MediaRecord? derived=null;
        if(kind==".pnach")
        {
            var match=Regex.Match(Path.GetFileNameWithoutExtension(file),@"(?:^|[_-])([0-9A-Fa-f]{8})(?:$|[_-])");
            if(!match.Success)throw new HubException("The PNACH filename must contain its eight-digit game CRC, for example 0D168765.pnach.");
            crc=match.Groups[1].Value.ToUpperInvariant();var text=new UTF8Encoding(false,true).GetString(bytes);
            if(!text.Split('\n').Any(l=>l.TrimStart().StartsWith("patch=",StringComparison.OrdinalIgnoreCase)))throw new HubException("No PNACH patch instructions were found.");
        }
        else derived=await PreparePpf(source,bytes,id,ct);
        Directory.CreateDirectory(root);var partial=payload+"."+Guid.NewGuid().ToString("N")+".partial";
        try{await File.WriteAllBytesAsync(partial,bytes,ct);ct.ThrowIfCancellationRequested();File.Move(partial,payload,true);}finally{if(File.Exists(partial))File.Delete(partial);}
        var result=existing??new LocalGamePatch(id,edition.Id,Path.GetFileNameWithoutExtension(file),kind[1..],source.Fingerprint,hash,paths.Portable(payload),crc,derived?.Id,analog,kind==".pnach"?PnachSections(Encoding.UTF8.GetString(bytes)):null);
        ct.ThrowIfCancellationRequested();JsonData.WriteAtomic(descriptor,result);
        if(derived!=null && !state.Media.Any(m=>m.Id==derived.Id))state.Media.Add(derived);
        return result;
    }
    private async Task<MediaRecord> PreparePpf(MediaRecord source,byte[] patch,string id,CancellationToken ct)
    {
        if(source.Format is not ("cue" or "bin" or "iso" or "img"))throw new HubException("PPF needs an uncompressed BIN/CUE or ISO image.");
        var target=paths.Games("patched",id);var receipt=Path.Combine(target,"hub-patch-media.json");
        if(File.Exists(receipt))
        {
            var existing=JsonData.Read<MediaRecord>(receipt);
            if(await new MediaService(paths,catalog).VerifyAsync(existing,ct))return existing;
            throw new HubException("The prepared patched copy changed. Its files have been preserved.");
        }
        var sourcePath=paths.Resolve(source.Path);var baseDir=Path.GetDirectoryName(sourcePath)!;
        var image=source.Format=="cue" ? MediaService.CueFiles(sourcePath).First() : sourcePath;
        var records=ReadPpf(patch,image);var stage=target+".partial-"+Guid.NewGuid().ToString("N");
        if(new DriveInfo(Path.GetPathRoot(target)!).AvailableFreeSpace<source.Files.Sum(f=>f.Size)+256L*1024*1024)throw new HubException("Not enough space for a separate patched copy. Choose another game-storage location.");
        try
        {
            Directory.CreateDirectory(Path.Combine(stage,"files"));
            foreach(var f in source.Files)
            {
                var dest=SafeFiles.Within(Path.Combine(stage,"files"),f.RelativeName);Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                await using var input=File.OpenRead(SafeFiles.Within(baseDir,f.RelativeName));await using var output=File.Create(dest);await input.CopyToAsync(output,ct);
            }
            var patched=SafeFiles.Within(Path.Combine(stage,"files"),Path.GetRelativePath(baseDir,image));
            await using(var output=new FileStream(patched,FileMode.Open,FileAccess.Write))
                foreach(var (offset,data) in records){ct.ThrowIfCancellationRequested();output.Position=offset;await output.WriteAsync(data,ct);}
            var entry=Path.Combine(stage,"files",Path.GetFileName(sourcePath));var service=new MediaService(paths,catalog);
            var media=await service.RegisterAsync(new(entry,source.Format,source.EditionId,null,"Locally patched copy.",[]),source.EditionId,false,new(),ct:ct);
            media=media with {Path=paths.Portable(Path.Combine(target,"files",Path.GetFileName(sourcePath))),Managed=true,Confidence=MediaConfidence.ModifiedImage,Recognition="Local PPF patch · original kept"};
            JsonData.WriteAtomic(Path.Combine(stage,"hub-patch-media.json"),media);
            await SafeFiles.MoveManagedDirectoryAsync(paths.GameStorageRoot,stage,target,ct);return media;
        }
        finally{SafeFiles.DeleteManagedTree(paths.GameStorageRoot,stage);}
    }
    public static List<(long Offset,byte[] Data)> ReadPpf(byte[] bytes,string source)
    {
        if(bytes.Length<56 || Encoding.ASCII.GetString(bytes,0,3)!="PPF")throw new HubException("Invalid PPF header.");
        var version=bytes[5]+1;if(version is <1 or >3 || Encoding.ASCII.GetString(bytes,3,2)!=(version*10).ToString())throw new HubException("Unsupported PPF version.");
        var size=new FileInfo(source).Length;var p=56;var undo=false;var check=false;long checkAt=0x9320;
        void Need(int n){if(n<0||p>bytes.Length-n)throw new HubException("The PPF patch is truncated.");}
        if(version==2){Need(4);var expected=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(p,4));p+=4;if(expected!=size)throw new HubException("The PPF source size does not match this game image.");check=true;}
        if(version==3){Need(4);if(bytes[p]>1||bytes[p+1]>1||bytes[p+2]>1)throw new HubException("Invalid PPF flags.");checkAt=bytes[p]==1?0x80A0:0x9320;check=bytes[p+1]!=0;undo=bytes[p+2]!=0;p+=4;}
        if(check)
        {
            Need(1024);using var input=File.OpenRead(source);if(checkAt+1024>size)throw new HubException("PPF block check exceeds the game image.");input.Position=checkAt;var block=new byte[1024];input.ReadExactly(block);
            if(!block.AsSpan().SequenceEqual(bytes.AsSpan(p,1024)))throw new HubException("This PPF targets a different disc revision.");p+=1024;
        }
        var records=new List<(long,byte[])>();
        while(p<bytes.Length)
        {
            if(bytes.AsSpan(p).StartsWith("@BEGIN_FILE_ID.DIZ"u8))break;
            Need(version==3?9:5);var offset=version==3?BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(p,8)):BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(p,4));p+=version==3?8:4;var length=bytes[p++];Need(length*(undo?2:1));
            if(length==0||offset>(ulong)size||(ulong)length>(ulong)size-offset||records.Count>=1000000)throw new HubException("A PPF write is outside the game image or its limits.");
            records.Add(((long)offset,bytes.AsSpan(p,length).ToArray()));p+=length*(undo?2:1);
        }
        if(records.Count==0)throw new HubException("The PPF patch is empty.");return records;
    }
    public List<LocalGamePatch> Selected(GameSelection selection,MediaRecord source)
    {
        var all=List(selection.EditionId);var result=new List<LocalGamePatch>();
        foreach(var id in selection.LocalPatchIds)
        {
            var patch=all.SingleOrDefault(p=>p.Id==id)??throw new HubException("An enabled local patch is missing.");
            if(patch.SourceHash!=source.Fingerprint)throw new HubException("A local patch belongs to a different game copy. Disable it or choose its original copy.");
            ReadPayload(patch);
            result.Add(patch);
        }
        if(result.Count(p=>p.Kind=="ppf")>1)throw new HubException("Enable only one image patch at a time.");return result;
    }
}
