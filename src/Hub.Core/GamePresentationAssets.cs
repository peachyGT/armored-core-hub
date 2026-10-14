using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Hub.Core;

public sealed record GamePresentationAssets(string? Cover, string? Background, string? Logo, string? Video, string[] Notes, int ReaderVersion = 0, string? ConsoleMovie = null);

// Reads only standard presentation slots and explicitly named local companion files.
// It never modifies game data, executes files, downloads assets, or guesses a logo from a cover.
public sealed class GameAssetReader(HubPaths paths)
{
    private const int ImageLimit = 16 * 1024 * 1024;
    private const int VideoLimit = 256 * 1024 * 1024;
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp"];
    private static readonly string[] VideoExtensions = [".mp4", ".m4v", ".wmv", ".avi", ".mpg", ".mpeg"];
    public Task<GamePresentationAssets> ReadAsync(MediaRecord media, bool refresh = false, CancellationToken ct = default) => Task.Run(() => Read(media, refresh, ct), ct);
    public GamePresentationAssets Read(MediaRecord media, bool refresh = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var folder = paths.Managed("artwork", "extracted", SafeFiles.Id(media.Id));
        var index = Path.Combine(folder, "assets.json");
        if (!refresh && File.Exists(index))
        {
            try { if (JsonData.Read<GamePresentationAssets>(index) is { ReaderVersion: 5 } cached
                && new[]{cached.Cover,cached.Background,cached.Logo,cached.Video,cached.ConsoleMovie}.Where(x=>x!=null).All(x=>File.Exists(paths.Managed("artwork",x!)))
                && (cached.Background != null || cached.Video != null)) return cached; }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or HubException) { }
        }
        Directory.CreateDirectory(folder);
        string? cover = null, background = null, logo = null, video = null, consoleMovie=null;
        var notes = new List<string>();
        var source = paths.Resolve(media.Path);
        string? Store(byte[]? data, string extension)
        {
            ct.ThrowIfCancellationRequested();
            if (data == null || data.Length == 0 || data.Length > VideoLimit) return null;
            var name = Convert.ToHexStringLower(SHA256.HashData(data)) + extension;
            // Keep full-hash objects shallow: the native Windows movie reader can reject
            // long paths even when normal .NET file access works. Shared objects also
            // deduplicate identical menu assets across registered copies.
            var destination = paths.Managed("artwork", name);
            if (!File.Exists(destination))
            {
                var temp = destination + "." + Guid.NewGuid().ToString("N") + ".part";
                try
                {
                    File.WriteAllBytes(temp, data); ct.ThrowIfCancellationRequested();
                    try { File.Move(temp, destination, false); }
                    catch (IOException) when (File.Exists(destination)) { }
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            return Path.GetRelativePath(paths.Managed("artwork"), destination);
        }
        string? ReadLocal(string root, string name, int limit)
        {
            var candidate = SafeFiles.Within(root, name);
            if (!File.Exists(candidate) || new FileInfo(candidate).Length > limit) return null;
            using var stream = File.OpenRead(candidate);
            var data = new byte[checked((int)stream.Length)]; stream.ReadExactly(data);
            return Store(data, Path.GetExtension(name).ToLowerInvariant());
        }
        void ReadFolder(string root)
        {
            if (!Directory.Exists(root)) return;
            SafeFiles.NoLinks(root); ct.ThrowIfCancellationRequested();
            cover ??= ReadLocal(root, "ICON0.PNG", ImageLimit);
            background ??= ReadLocal(root, "PIC1.PNG", ImageLimit);
            // PIC0 is a menu overlay, often marketing copy; it is not a reliable title logo.
            logo ??= ReadLocal(root, "logo.png", ImageLimit);
            foreach (var ext in ImageExtensions)
            {
                cover ??= ReadLocal(root, "cover" + ext, ImageLimit);
                background ??= ReadLocal(root, "background" + ext, ImageLimit) ?? ReadLocal(root, "fanart" + ext, ImageLimit);
            }
            foreach (var ext in VideoExtensions)
                foreach (var name in new[] { "background", "trailer", "intro", "opening", "ICON1" })
                    video ??= ReadLocal(root, name + ext, VideoLimit);
            consoleMovie ??= ReadLocal(root,"ICON1.PMF",ImageLimit) ?? ReadLocal(root,"ICON1.PAM",ImageLimit);
        }
        try
        {
            SafeFiles.NoLinks(source);
            if (Directory.Exists(source))
            {
                foreach (var relative in new[] { "PS3_GAME", "PSP_GAME", "", "artwork", "media", "PS3_GAME/USRDIR/movie", "PS3_GAME/USRDIR/movies", "PSP_GAME/USRDIR/movie" })
                    ReadFolder(relative.Length == 0 ? source : SafeFiles.Within(source, relative));
            }
            else if (File.Exists(source))
            {
                var extension = Path.GetExtension(source).ToLowerInvariant();
                if (extension == ".iso")
                {
                    using var iso = new IsoReader(source, 2048, 0);
                    if (iso.Valid)
                        foreach (var prefix in new[] { "PSP_GAME/", "PS3_GAME/", "" })
                        {
                            cover ??= Store(iso.ReadFile(prefix + "ICON0.PNG"), ".png");
                            background ??= Store(iso.ReadFile(prefix + "PIC1.PNG"), ".png");
                            video ??= Store(iso.ReadFile(prefix + "ICON1.MP4"), ".mp4");
                            consoleMovie ??= Store(iso.ReadFile(prefix+"ICON1.PMF"),".pmf") ?? Store(iso.ReadFile(prefix+"ICON1.PAM"),".pam");
                        }
                    else notes.Add("This disc container has no readable ISO9660 presentation slots. Extracted game folders can provide artwork.");
                }
                else if (extension == ".pbp")
                {
                    using var stream = File.OpenRead(source);
                    var header = new byte[40];
                    if (stream.Length >= header.Length)
                    {
                        stream.ReadExactly(header);
                        if (header.AsSpan(0, 4).SequenceEqual(new byte[] { 0, 80, 66, 80 }))
                        {
                            byte[]? Slot(int slot)
                            {
                                var start = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8 + slot * 4));
                                var end = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12 + slot * 4));
                                if (start < 40 || end <= start || end > stream.Length || end - start > ImageLimit) return null;
                                stream.Position = start; var bytes = new byte[end - start]; stream.ReadExactly(bytes); return bytes;
                            }
                            cover = Store(Slot(1), ".png"); background = Store(Slot(4), ".png");
                            consoleMovie=Store(Slot(2),".pmf");
                        }
                    }
                }
                // Only the per-file companion folder is scanned; never borrow another game's assets from a shared library.
                ReadFolder(source + ".artwork");
            }
            if (cover == null && background == null && video == null) notes.Add("No usable menu artwork was found. You can choose local artwork or a video.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HubException or InvalidDataException)
        { notes.Add("The game artwork could not be read. Your game files were left unchanged."); }
        ct.ThrowIfCancellationRequested();
        var result = new GamePresentationAssets(cover, background, logo, video, notes.Distinct().ToArray(), 5,consoleMovie);
        JsonData.WriteAtomic(index, result); return result;
    }
}
