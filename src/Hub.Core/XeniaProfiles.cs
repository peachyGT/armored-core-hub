using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json;

namespace Hub.Core;

public sealed record XeniaLocalProfile(string Id,string Gamertag,bool IsValid=true);

// Local emulator identities only; these are not Xbox Live accounts. Keep their
// selection when regenerating settings or switching an immutable emulator build.
public static partial class XeniaProfiles
{
    public static string? SelectLocal(string saveDirectory, string configFile)
    {
        if (!Directory.Exists(saveDirectory)) return null;
        SafeFiles.NoLinks(saveDirectory);
        var candidates = new List<string>();
        var scanned=0;var identities=0;
        foreach (var directory in Directory.EnumerateDirectories(saveDirectory))
        {
            if(++scanned>1024)throw new HubException("Too many folders in Xbox save storage. Keep your files and choose a smaller save collection.");
            var id = Path.GetFileName(directory);
            if (!Identity().IsMatch(id) || id.All(c => c == '0')) continue;
            if(++identities>128)throw new HubException("Too many Xbox player profiles. Keep your profiles and choose a smaller save collection.");
            var account = SafeFiles.Within(directory, $"FFFE07D1/00010000/{id}/Account");
            if (File.Exists(account)) { SafeFiles.NoLinks(account); candidates.Add(id.ToUpperInvariant()); }
        }
        var choice=SafeFiles.Within(saveDirectory,"hub-selected-profile.json");
        if(File.Exists(choice))
        {
            string? stored;
            try{stored=JsonData.ReadBounded<Dictionary<string,string>>(choice,16*1024).GetValueOrDefault("id");}
            catch(JsonException e){throw new HubException("Xbox player selection is damaged. Its files were kept; choose your player again in Achievements.",e);}
            if(stored==null||!candidates.Contains(stored,StringComparer.OrdinalIgnoreCase))throw new HubException("The selected Xbox player profile is unavailable. Its files were kept; choose an existing player in Achievements.");
            return stored.ToUpperInvariant();
        }
        if (File.Exists(configFile))
        {
            SafeFiles.NoLinks(configFile);
            if (new FileInfo(configFile).Length > 1024 * 1024) throw new HubException("The emulator settings file is unexpectedly large. Use Repair before launching.");
            var inProfiles = false;
            foreach (var raw in File.ReadLines(configFile))
            {
                var line = raw.Trim();
                if (line.StartsWith('[')) { inProfiles = line == "[Profiles]"; continue; }
                if (!inProfiles) continue;
                var match = Login().Match(line);
                if (match.Success && candidates.Contains(match.Groups[1].Value, StringComparer.OrdinalIgnoreCase))
                    return match.Groups[1].Value.ToUpperInvariant();
            }
        }
        // Never choose arbitrarily among several existing player/save identities.
        return candidates.Count == 1 ? candidates[0] : null;
    }

    public static IReadOnlyList<XeniaLocalProfile> ExistingLocal(string saveDirectory)
    {
        if(!Directory.Exists(saveDirectory))return [];
        SafeFiles.NoLinks(saveDirectory);var profiles=new List<XeniaLocalProfile>();var scanned=0;var identities=0;
        foreach(var directory in Directory.EnumerateDirectories(saveDirectory))
        {
            if(++scanned>1024)throw new HubException("Too many folders in Xbox save storage. Keep your files and choose a smaller save collection.");
            var id=Path.GetFileName(directory);if(!Identity().IsMatch(id)||id.All(c=>c=='0'))continue;
            if(++identities>128)throw new HubException("Too many Xbox player profiles. Keep your profiles and choose a smaller save collection.");
            var account=SafeFiles.Within(directory,$"FFFE07D1/00010000/{id}/Account");
            if(File.Exists(account))
            {
                try{profiles.Add(new(id.ToUpperInvariant(),XeniaLocalAccount.ReadGamertag(account)));}
                catch(HubException){profiles.Add(new(id.ToUpperInvariant(),"Damaged player profile",false));}
            }
        }
        return profiles.OrderBy(p=>p.Gamertag,StringComparer.OrdinalIgnoreCase).ToList();
    }
    public static void ChooseLocal(HubPaths paths,string saveDirectory,string id)
    {
        ValidateSaveRoot(paths,saveDirectory);
        var selected=ExistingLocal(saveDirectory).SingleOrDefault(p=>p.IsValid&&p.Id.Equals(id,StringComparison.OrdinalIgnoreCase))??throw new HubException("Choose an existing Xbox player profile. No profile was changed.");
        JsonData.WriteAtomic(SafeFiles.Within(saveDirectory,"hub-selected-profile.json"),new Dictionary<string,string>{{"id",selected.Id}});
    }
    public static async Task<string> EnsureLocalAsync(HubPaths paths,string saveDirectory,string configFile,CancellationToken ct=default)
    {
        ValidateSaveRoot(paths,saveDirectory);ct.ThrowIfCancellationRequested();Directory.CreateDirectory(saveDirectory);
        var selected=SelectLocal(saveDirectory,configFile);
        if(selected!=null)
        {
            if(!ExistingLocal(saveDirectory).Any(p=>p.IsValid&&p.Id==selected))throw new HubException("The selected Xbox player profile is damaged. Its files were kept.");
            return selected;
        }
        if(ExistingLocal(saveDirectory).Count>0)throw new HubException("Choose your Xbox player in this game's Achievements tab before playing. Existing player profiles were kept.");
        // Never disconnect orphaned or older-format progress by assigning a new
        // identity. The all-zero directory holds shared DLC, not player saves.
        if(Directory.EnumerateFileSystemEntries(saveDirectory).Any(p=>Path.GetFileName(p)!="0000000000000000"))
            throw new HubException("Older Xbox save or player data needs migration. Its files were kept; import or repair its player profile before playing.");
        var xuid=0xe030000000000000UL+(uint)RandomNumberGenerator.GetInt32(int.MaxValue);var id=xuid.ToString("X16",CultureInfo.InvariantCulture);
        var staging=SafeFiles.Within(saveDirectory,".hub-profile-"+Guid.NewGuid().ToString("N")+".partial");var target=SafeFiles.Within(saveDirectory,id);
        try
        {
            var account=SafeFiles.Within(staging,$"FFFE07D1/00010000/{id}/Account");var header=SafeFiles.Within(staging,$"FFFE07D1/Headers/00010000/{id}.header");
            Directory.CreateDirectory(Path.GetDirectoryName(account)!);Directory.CreateDirectory(Path.GetDirectoryName(header)!);
            await File.WriteAllBytesAsync(account,XeniaLocalAccount.Create("Hub"),ct);await File.WriteAllBytesAsync(header,XeniaLocalAccount.Header(xuid,"Hub"),ct);
            await SafeFiles.MoveManagedDirectoryAsync(paths.Root,staging,target,ct);
            return id;
        }
        finally{SafeFiles.DeleteManagedTree(paths.Root,staging);}
    }
    public static string ApplyLocal(string contents,string id)
    {
        if(!Identity().IsMatch(id)||id.All(c=>c=='0'))throw new HubException("Invalid local Xbox player identity.");
        var lines=contents.Split('\n').ToList();var start=lines.FindIndex(l=>l.Trim()=="[Profiles]");var line="logged_profile_slot_0_xuid = \""+id.ToUpperInvariant()+"\"";
        if(start<0)return contents+"\n[Profiles]\n"+line+"\n";
        for(var index=start+1;index<lines.Count&&!lines[index].TrimStart().StartsWith('[');index++)
            if(lines[index].TrimStart().StartsWith("logged_profile_slot_0_xuid",StringComparison.Ordinal)){lines[index]=line;return string.Join('\n',lines);}
        lines.Insert(start+1,line);return string.Join('\n',lines);
    }
    private static void ValidateSaveRoot(HubPaths paths,string saveDirectory)
    {
        if(!SafeFiles.IsWithin(paths.Managed("saves"),saveDirectory))throw new HubException("Xbox player setup requires managed save storage.");
        SafeFiles.NoLinks(saveDirectory);
    }

    [GeneratedRegex("^[0-9A-Fa-f]{16}$", RegexOptions.CultureInvariant)] private static partial Regex Identity();
    [GeneratedRegex("^logged_profile_slot_0_xuid\\s*=\\s*\"([0-9A-Fa-f]{16})\"\\s*(?:#.*)?$", RegexOptions.CultureInvariant)] private static partial Regex Login();
}
