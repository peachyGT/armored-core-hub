using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Hub.Core;

public static partial class JsonData
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? throw new HubException($"{Path.GetFileName(path)} is empty.");
    public static T ReadBounded<T>(string path,long maximumBytes)
    {
        SafeFiles.NoLinks(path);
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(stream.Length>maximumBytes)throw new HubException($"{Path.GetFileName(path)} exceeds its metadata size limit.");
        return JsonSerializer.Deserialize<T>(stream,Options)??throw new HubException($"{Path.GetFileName(path)} is empty.");
    }
    public static void WriteAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, value, Options); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, path + ".previous", true);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

public sealed class HubPaths
{
    public string Root { get; }
    public string GameStorageRoot { get; private set; }
    public HubPaths(string root)
    {
        Root = Path.GetFullPath(root);
        GameStorageRoot = Path.Combine(Root, "games");
        SafeFiles.NoLinks(Root);
        Directory.CreateDirectory(Root);
        foreach (var name in new[] { "config", "manifests", "runtime", "games", "saves", "firmware", "patches", "extensions", "cache", "logs" })
            Directory.CreateDirectory(Managed(name));
        var storage = Managed("config", "game-storage.json");
        if (File.Exists(storage))
        {
            var saved = JsonData.Read<Dictionary<string,string>>(storage).GetValueOrDefault("path");
            if (!string.IsNullOrWhiteSpace(saved)) { GameStorageRoot = Resolve(saved); SafeFiles.NoLinks(GameStorageRoot); }
        }
    }
    public string Games(params string[] parts) => parts.Length == 0 ? GameStorageRoot : SafeFiles.Within(GameStorageRoot, Path.Combine(parts));
    public void SetGameStorage(string folder)
    {
        var full = Path.GetFullPath(folder); SafeFiles.NoLinks(full);
        if (Path.GetPathRoot(full) == Path.TrimEndingDirectorySeparator(full) || Path.GetPathRoot(full) == full)
            throw new HubException("Choose a dedicated game storage folder, not a drive root.");
        Directory.CreateDirectory(full);
        JsonData.WriteAtomic(Managed("config", "game-storage.json"), new Dictionary<string,string> { ["path"] = Portable(full) });
        GameStorageRoot = full;
    }
    public string Managed(params string[] parts) => SafeFiles.Within(Root, Path.Combine(parts));
    public string Resolve(string path) => Path.IsPathRooted(path) ? Path.GetFullPath(path) : Managed(path);
    public string Portable(string path) => SafeFiles.IsWithin(Root, path) ? Path.GetRelativePath(Root, path) : Path.GetFullPath(path);
}

public static partial class SafeFiles
{
    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9._-]{0,159}$")] private static partial Regex SafeIdRegex();
    public static string Id(string id)
    {
        if (!SafeIdRegex().IsMatch(id) || id.EndsWith('.') || Regex.IsMatch(id.Split('.')[0], "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase))
            throw new HubException("Invalid identifier: " + id);
        return id;
    }
    public static bool IsWithin(string root, string target)
    {
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(target).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
    public static string Within(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains(':')) throw new HubException("An absolute path is not allowed in managed storage.");
        var target = Path.GetFullPath(Path.Combine(root, relative));
        if (!IsWithin(root, target)) throw new HubException("A path would escape managed storage.");
        NoLinks(target);
        return target;
    }
    public static void NoLinks(string path)
    {
        for (var p = Path.GetFullPath(path); p != null; p = Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new HubException("Linked folders are not supported for this operation. Choose the actual folder.");
    }
    public static IEnumerable<string> Files(string root)
    {
        NoLinks(root);
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
            {
                var attrs = File.GetAttributes(entry);
                if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                if ((attrs & FileAttributes.Directory) != 0) pending.Push(entry); else yield return entry;
            }
        }
    }
    public static async Task<string> HashAsync(string path, CancellationToken ct = default)
    { await using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576, true); return Convert.ToHexStringLower(await SHA256.HashDataAsync(s, ct)); }
    public static string HashText(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static async Task MoveManagedDirectoryAsync(string root, string source, string destination, CancellationToken ct = default)
    {
        if (!IsWithin(root, source) || !IsWithin(root, destination)) throw new HubException("Refusing to move outside managed storage.");
        NoLinks(source); NoLinks(destination);
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { Directory.Move(source, destination); return; }
            catch (IOException e) when (attempt < 5 && ((e.HResult & 0xffff) is 5 or 32 or 33) && Directory.Exists(source) && !Directory.Exists(destination))
            {
                // Windows can briefly hold newly written files during inspection. Never retry a conflicting destination.
                await Task.Delay(50 * (1 << attempt), ct).ConfigureAwait(false);
            }
        }
    }
    public static void DeleteManagedTree(string root, string path)
    { if (!IsWithin(root, path)) throw new HubException("Refusing to delete outside managed storage."); NoLinks(path); if (Directory.Exists(path)) Directory.Delete(path, true); }
}

public sealed class StateStore(HubPaths paths)
{
    public string? RecoveryNotice { get; private set; }
    private readonly object gate = new();
    public HubState Load()
    {
        var path = paths.Managed("config", "state.json");
        if (!File.Exists(path)) return new();
        try { return Validate(JsonData.Read<HubState>(path)); }
        catch (Exception e) when (e is JsonException or HubException)
        {
            if (!File.Exists(path + ".previous")) throw new HubException("Library settings are damaged. Keep the config folder and restore a backup.", e);
            var state = Validate(JsonData.Read<HubState>(path + ".previous"));
            File.Copy(path, path + ".damaged-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true);
            File.Copy(path + ".previous", path, true);
            RecoveryNotice = "Recovered library settings from the previous saved copy.";
            return state;
        }
    }
    private static HubState Validate(HubState state)
    {
        if (state.SchemaVersion != 1) throw new HubException("This library was created by an unsupported Hub version.");
        if(state.ScanRoots==null||state.Media==null||state.Builds==null||state.Firmware==null||state.Selections==null||state.ProviderHealth==null||state.PreferredEditions==null
            ||state.Media.Any(m=>m==null||m.Files==null||string.IsNullOrWhiteSpace(m.Id)||string.IsNullOrWhiteSpace(m.Path))
            ||state.Builds.Any(b=>b==null||string.IsNullOrWhiteSpace(b.Id)||string.IsNullOrWhiteSpace(b.Path)||string.IsNullOrWhiteSpace(b.Executable))
            ||state.Firmware.Any(f=>f==null||string.IsNullOrWhiteSpace(f.Id)||string.IsNullOrWhiteSpace(f.Path))
            ||state.Selections.Values.Any(s=>s==null||s.Controls==null||s.Graphics==null||s.PatchIds==null||s.LocalPatchIds==null||s.ExtensionIds==null||s.AdvancedSettings==null||s.ProfileChoices==null))
            throw new HubException("Library settings contain incomplete records. Keep the config folder and restore a backup.");
        foreach(var selection in state.Selections.Values)ControlProfiles.ValidateStored(selection.Controls);
        return state;
    }
    public void Save(HubState state) { lock (gate) JsonData.WriteAtomic(paths.Managed("config", "state.json"), Validate(state)); }
}
