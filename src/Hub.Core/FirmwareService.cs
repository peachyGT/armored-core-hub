using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Hub.Core;

public sealed class FirmwareService(HubPaths paths, Catalog catalog)
{
    public async Task<FirmwareRecord> ImportAsync(string source, CancellationToken ct = default)
    {
        var record=await InspectAsync(source,ct);return await StoreAsync(source,record,ct);
    }
    public async Task<FirmwareRecord> InspectAsync(string source,CancellationToken ct=default)
    {
        SafeFiles.NoLinks(source);await using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read,131072,true);var size=input.Length;
        if (size > 300 * 1024 * 1024 || size < 64) throw new HubException("This file is not a supported system component.");
        var sha=Convert.ToHexStringLower(await SHA256.HashDataAsync(input,ct));input.Position=0;
        string kind, label, region = "Unknown";string? romVersion=null; bool verified = false;
        if (size == 524288)
        {
            var md5 = Convert.ToHexStringLower(await MD5.HashDataAsync(input, ct));
            var definition = catalog.Firmware.FirstOrDefault(f => f.RequiredFor == "ps1-bios" && f.Hashes.Any(h => h.Algorithm.Equals("md5",StringComparison.OrdinalIgnoreCase) && h.Value.Equals(md5, StringComparison.OrdinalIgnoreCase)));
            if (definition == null) throw new HubException("This PlayStation BIOS is not in the verified reference set. Import a supported owned-hardware dump or add a reviewed hash record.");
            kind = "ps1-bios"; label = definition.Name; verified = true; region = definition.Hashes.First(h=>h.Value.Equals(md5,StringComparison.OrdinalIgnoreCase)).Release;
        }
        else if (size is 4194304 or 8388608)
        {
            var bytes=new byte[(int)size];await input.ReadExactlyAsync(bytes,ct);romVersion=Ps2Bios.RomVersion(bytes);
            if(romVersion==null)throw new HubException("This file does not contain a complete PlayStation 2 ROMDIR and ROMVER structure.");
            kind = "ps2-bios"; region = romVersion[4] switch { 'A' => "US", 'E' => "EU", 'J' => "JP", 'C' => "CN", _ => "ASIA" };
            verified=catalog.Firmware.Where(f=>f.RequiredFor==kind).SelectMany(f=>f.Hashes).Any(h=>h.Algorithm.Equals("sha256",StringComparison.OrdinalIgnoreCase)&&h.Value.Equals(sha,StringComparison.OrdinalIgnoreCase));
            label = "PS2 v"+int.Parse(romVersion[..2])+"."+romVersion[2..4]+" · "+region+(romVersion[5]=='D'?" · development BIOS":"")+(verified?" · hash verified":" · structurally recognized");
        }
        else
        {
            Span<byte> head = stackalloc byte[8]; input.ReadExactly(head);
            if (!head[..5].SequenceEqual("SCEUF"u8)) throw new HubException("No supported BIOS or official PS3 update was recognized.");
            kind = "ps3-firmware"; label = "PlayStation 3 system software · PUP";
            verified = catalog.Firmware.Any(f => f.OfficialSha256 == sha); region = "Global";
        }
        return new(sha,kind,source,sha,label,verified,region,romVersion);
    }
    private async Task<FirmwareRecord> StoreAsync(string source,FirmwareRecord record,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();var dir=paths.Managed("firmware",record.Class);Directory.CreateDirectory(dir);
        var destination=SafeFiles.Within(dir,record.Sha256+(record.Class=="ps3-firmware"?".pup":".bin"));
        if (!File.Exists(destination) || await SafeFiles.HashAsync(destination, ct) != record.Sha256)
        {
            var part = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                await using(var input=File.OpenRead(source))await using(var output=File.Create(part))
                {
                    if(input.Length is <64 or >300L*1024*1024)throw new HubException("The system component changed during import.");
                    await input.CopyToAsync(output,ct);
                }
                if (await SafeFiles.HashAsync(part, ct) != record.Sha256) throw new HubException("The system component changed during import.");
                ct.ThrowIfCancellationRequested();
                File.Move(part, destination, true);
            }
            finally { if (File.Exists(part)) File.Delete(part); }
        }
        return record with {Path=paths.Portable(destination)};
    }
    public async Task<FirmwareImportResult> ImportForEditionAsync(string source,Edition edition,IProgress<ProgressInfo>? progress=null,CancellationToken ct=default)
    {
        var kind=ClassFor(edition);if(kind=="none")throw new HubException("This console does not need separate system software.");
        if(!FirmwareArchive.IsArchive(source))
        {
            var inspected=await InspectAsync(source,ct);if(inspected.Class!=kind)throw new HubException("This game needs "+ClassName(kind)+". The selected file belongs to another console.");
            return new(await StoreAsync(source,inspected,ct),1,0);
        }
        using var unpacked=await Task.Run(()=>FirmwareArchive.PrepareAsync(paths,source,progress,ct),ct);
        var candidates=new List<FirmwareRecord>();
        foreach(var file in unpacked.Files)
        {
            ct.ThrowIfCancellationRequested();progress?.Report(new("Checking BIOS "+Path.GetFileName(file)));
            try{var candidate=await InspectAsync(file,ct);if(candidate.Class==kind)candidates.Add(candidate);}catch(HubException){}
        }
        var selected=Choose(kind,edition,candidates)??throw new HubException("No valid "+ClassName(kind)+" was found in this archive. Choose a complete BIOS dump or the official system update for this console.");
        var imported=await StoreAsync(selected.Path,selected,ct);
        return new(imported,candidates.Count,unpacked.FileCount-1);
    }
    public static string ClassFor(Edition edition)=>edition.Platform switch {"ps1"=>"ps1-bios","ps2"=>"ps2-bios","ps3"=>"ps3-firmware",_=>"none"};
    public static string ClassName(string kind)=>kind switch {"ps1-bios"=>"a PlayStation BIOS","ps2-bios"=>"a PlayStation 2 BIOS","ps3-firmware"=>"PlayStation 3 system software",_=>"system software"};
    public static FirmwareRecord? Choose(string kind,Edition edition,IEnumerable<FirmwareRecord> records,Func<FirmwareRecord,bool>? available=null)=>records
        .Where(f=>f.Class==kind&&(available==null||available(f)))
        .OrderByDescending(f=>kind!="ps2-bios"||!(f.Region=="JP"&&Version(f)==100))
        .ThenByDescending(f=>kind!="ps2-bios"||f.RomVersion is not {Length:>=6}||f.RomVersion[5]=='C')
        .ThenByDescending(f=>f.Region.Equals(edition.Region,StringComparison.OrdinalIgnoreCase))
        .ThenByDescending(f=>f.Verified).ThenByDescending(Version).ThenBy(f=>f.Id,StringComparer.Ordinal).FirstOrDefault();
    private static int Version(FirmwareRecord record)
    {
        if(record.RomVersion is {Length:>=4}&&int.TryParse(record.RomVersion[..4],out var known))return known;
        var legacy=Regex.Match(record.Label,@"PS2 (\d{4})");return legacy.Success?int.Parse(legacy.Groups[1].Value):0;
    }
    public static List<string> SuggestedRoots()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new[] {Path.Combine(docs, "DuckStation", "bios"), Path.Combine(docs, "PCSX2", "bios"), Path.Combine(local, "DuckStation", "bios"), Path.Combine(docs, "Emulation", "bios"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads")}.Where(Directory.Exists).ToList();
    }
    public async Task<List<FirmwareRecord>> ScanAsync(string root, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var result = new List<FirmwareRecord>();
        foreach (var file in SafeFiles.Files(root))
        {
            ct.ThrowIfCancellationRequested(); var size = new FileInfo(file).Length;
            if (size is not (524288 or 4194304 or 8388608) && !Path.GetExtension(file).Equals(".pup", StringComparison.OrdinalIgnoreCase)) continue;
            progress?.Report(new("Checking system component " + Path.GetFileName(file)));
            try { result.Add(await ImportAsync(file, ct)); } catch (HubException) { }
        }
        return result.DistinctBy(f => f.Sha256).ToList();
    }
    public async Task<List<FirmwareRecord>> ScanForEditionAsync(string root,Edition edition,IProgress<ProgressInfo>? progress=null,CancellationToken ct=default)
    {
        var result=new List<FirmwareRecord>();var downloads=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
        SafeFiles.NoLinks(root);var files=Path.GetFullPath(root).Equals(downloads,StringComparison.OrdinalIgnoreCase)?Directory.EnumerateFiles(root):SafeFiles.Files(root);
        foreach(var file in files.Take(5000))
        {
            ct.ThrowIfCancellationRequested();var size=new FileInfo(file).Length;
            var archive=FirmwareArchive.IsArchive(file);
            if(archive&&(size>FirmwareArchive.MaximumSourceBytes||!Path.GetFileName(file).Contains("bios",StringComparison.OrdinalIgnoreCase)))continue;
            if(!archive&&size is not (524288 or 4194304 or 8388608)&&!Path.GetExtension(file).Equals(".pup",StringComparison.OrdinalIgnoreCase))continue;
            progress?.Report(new("Checking system files "+Path.GetFileName(file)));
            try{result.Add((await ImportForEditionAsync(file,edition,progress,ct)).Selected);}catch(Exception ex)when(ex is HubException or IOException or UnauthorizedAccessException){}
        }
        return result.DistinctBy(f=>f.Sha256).ToList();
    }
}
public sealed record FirmwareImportResult(FirmwareRecord Selected,int CandidatesFound,int IgnoredItems);
