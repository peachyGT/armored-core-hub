using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hub.Core;

namespace Hub.App;

// Appearance is deliberately independent of emulator and game configuration.
public sealed class AppearancePreferences
{
    public double LibraryFraction { get; set; } = .36;
    public string LibraryMode { get; set; } = "normal";
    public string GroupBy { get; set; } = "console";
    public string View { get; set; } = "grid";
    public bool Ambient { get; set; } = true;
    public bool VideoBackgrounds { get; set; } = true;
    public bool DownloadCovers { get; set; } = true;
    public string? SelectedGameId { get; set; }
    public void Normalize()
    {
        LibraryFraction = double.IsFinite(LibraryFraction) ? Math.Clamp(LibraryFraction, .22, .68) : .36;
        if (LibraryMode is not ("normal" or "collapsed" or "expanded")) LibraryMode = "normal";
        if (GroupBy is not ("console" or "generation")) GroupBy = "console";
        if (View is not ("grid" or "list")) View = "grid";
    }
}

public sealed class ArtworkEntry
{
    public string Cover { get; set; } = "";
    public string? Background { get; set; }
    public string? Logo { get; set; }
    public string? Video { get; set; }
    public int[]? Crop { get; set; }
}

public sealed record Artwork(BitmapSource? Cover, BitmapSource? Background, Color Tint, BitmapSource? Logo = null, Uri? Video = null);

public sealed class LibraryArtwork
{
    private readonly HubPaths paths;
    private readonly Dictionary<string, ArtworkEntry> entries;
    private readonly Dictionary<string, WeakReference<Artwork>> cache = [];
    private sealed record DecodedImage(BitmapSource Image, long Bytes, LinkedListNode<string> Recency);
    private readonly Dictionary<string, DecodedImage> images = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> recentImages = [];
    private long decodedBytes;
    internal const long DecodedBudget = 64L * 1024 * 1024;
    internal (int Count, long Bytes) CacheUsage => (images.Count, decodedBytes);
    public static readonly Color Neutral = Color.FromRgb(117, 108, 90);
    public LibraryArtwork(HubPaths paths)
    {
        this.paths = paths;
        try { entries = JsonData.Read<Dictionary<string, ArtworkEntry>>(paths.Managed("artwork", "index.json")) ?? []; }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or HubException) { entries = []; }
    }
    public Artwork Get(string key, string gameId)
    {
        if (cache.TryGetValue(key, out var weak) && weak.TryGetTarget(out var result)) return result;
        if (!entries.TryGetValue(key, out var entry) && !entries.TryGetValue(gameId, out entry))
            return Remember(key, new(null, null, Neutral));
        if (entry == null) return Remember(key, new(null, null, Neutral));
        try
        {
            // Retire our old screenshot-crop placeholders without deleting users' files or backgrounds.
            var cover = entry.Cover == "collection-reference.png" && entry.Crop != null ? null : TryImage(entry.Cover);
            if (cover != null && entry.Crop is { Length: 4 } c)
            {
                if (c[0] < 0 || c[1] < 0 || c[2] <= 0 || c[3] <= 0 || (long)c[0] + c[2] > cover.PixelWidth || (long)c[1] + c[3] > cover.PixelHeight)
                    throw new InvalidDataException("Invalid cover crop.");
                cover = new CroppedBitmap(cover, new Int32Rect(c[0], c[1], c[2], c[3])); cover.Freeze();
            }
            var background = TryImage(entry.Background);
            var logo = TryImage(entry.Logo);
            return Remember(key, new(cover, background, cover == null ? Neutral : SampleTint(cover), IsLogo(logo) ? logo : null, TryVideo(entry.Video)));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or NotSupportedException or InvalidOperationException or FormatException or HubException)
        { return Remember(key, new(null, null, Neutral)); }
    }
    private Artwork Remember(string key, Artwork image)
    {
        if(cache.Count>=64&&!cache.ContainsKey(key))cache.Remove(cache.Keys.First());
        cache[key]=new(image);return image;
    }
    internal void ClearCaches() { cache.Clear(); images.Clear(); recentImages.Clear(); decodedBytes=0; }
    public Artwork Merge(Artwork chosen, GamePresentationAssets? automatic)
    {
        if (automatic == null) return chosen;
        var cover = chosen.Cover ?? TryImage(automatic.Cover);
        var logo = chosen.Logo ?? TryImage(automatic.Logo);
        return new(cover, chosen.Background ?? TryImage(automatic.Background), cover == null ? chosen.Tint : SampleTint(cover),
            IsLogo(logo) ? logo : null, chosen.Video ?? TryVideo(automatic.Video));
    }
    public Artwork WithCover(Artwork chosen, string? verifiedCacheFile)
    {
        if (chosen.Cover != null || verifiedCacheFile == null) return chosen;
        try
        {
            var cover = LoadFile(verifiedCacheFile);
            return chosen with { Cover = cover, Tint = SampleTint(cover) };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException) { return chosen; }
    }
    private BitmapSource? TryImage(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return null;
        try { return Load(relative); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException or InvalidOperationException or FormatException or HubException) { return null; }
    }
    private Uri? TryVideo(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return null;
        try
        {
            if (Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(p => p == "..")) return null;
            var file = paths.Managed("artwork", relative);
            if (!File.Exists(file) || new FileInfo(file).Length > 256 * 1024 * 1024 || !VideoExtension(Path.GetExtension(file).ToLowerInvariant())) return null;
            return new Uri(file);
        }
        catch (Exception ex) when (ex is IOException or HubException or ArgumentException) { return null; }
    }
    public static bool IsLogo(BitmapSource? image)
    {
        if (image == null || image.PixelWidth < image.PixelHeight * 1.2) return false;
        var scaled = new TransformedBitmap(image, new ScaleTransform(64d / image.PixelWidth, 32d / image.PixelHeight));
        var pixels = new byte[64 * 32 * 4]; new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, 64 * 4, 0);
        var transparent = 0; var visible = 0;
        for (var i = 3; i < pixels.Length; i += 4) { if (pixels[i] < 32) transparent++; if (pixels[i] > 200) visible++; }
        return transparent > 100 && visible > 30;
    }
    private static bool VideoExtension(string ext) => ext is ".mp4" or ".m4v" or ".wmv" or ".avi" or ".mpg" or ".mpeg";
    private BitmapSource Load(string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(p => p == "..")) throw new InvalidDataException("Artwork must stay in its managed folder.");
        var file = paths.Managed("artwork", relative);
        return LoadFile(file);
    }
    private BitmapSource LoadFile(string file)
    {
        file=Path.GetFullPath(file);
        if (images.TryGetValue(file, out var existing))
        {recentImages.Remove(existing.Recency);recentImages.AddLast(existing.Recency);return existing.Image;}
        if (new FileInfo(file).Length > 32 * 1024 * 1024) throw new InvalidDataException("Artwork is too large.");
        var image = new BitmapImage();
        // StreamSource avoids the process-wide URI image cache. IgnoreImageCache itself
        // requires a UriSource in WPF and cannot be combined with this stream-only path.
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        using var stream = File.OpenRead(file);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        if ((long)decoder.Frames[0].PixelWidth * decoder.Frames[0].PixelHeight > 80_000_000) throw new InvalidDataException("Artwork dimensions are too large.");
        var frame=decoder.Frames[0];var scale=Math.Min(1,1800d/Math.Max(frame.PixelWidth,frame.PixelHeight));
        image.DecodePixelWidth=Math.Max(1,(int)Math.Round(frame.PixelWidth*scale));image.DecodePixelHeight=Math.Max(1,(int)Math.Round(frame.PixelHeight*scale));
        image.StreamSource=stream;image.EndInit();image.Freeze();
        var bytes=checked((long)image.PixelWidth*image.PixelHeight*Math.Max(4,(image.Format.BitsPerPixel+7)/8));
        while(recentImages.First is {} first&&(images.Count>=48||decodedBytes+bytes>DecodedBudget))
        {var removed=images[first.Value];images.Remove(first.Value);recentImages.RemoveFirst();decodedBytes-=removed.Bytes;}
        images.Add(file,new(image,bytes,recentImages.AddLast(file)));decodedBytes+=bytes;return image;
    }
    public void Import(string key, string source, bool background) => Import(key, source, background ? "background" : "cover");
    public void Import(string key, string source, string kind)
    {
        var extension = Path.GetExtension(source).ToLowerInvariant();
        if (kind is not ("cover" or "background" or "logo" or "video")) throw new ArgumentException("Unknown artwork role.");
        if (kind == "video" ? !VideoExtension(extension) : extension is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp")) throw new HubException("Choose a supported image or video file.");
        if (new FileInfo(source).Length > (kind == "video" ? 256 : 32) * 1024 * 1024) throw new HubException(kind == "video" ? "Choose a video smaller than 256 MB." : "Choose artwork smaller than 32 MB.");
        Directory.CreateDirectory(paths.Managed("artwork"));
        using var input = File.OpenRead(source);
        var name = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant() + extension;
        var destination = paths.Managed("artwork", name);
        if (!File.Exists(destination)) File.Copy(source, destination);
        if (kind != "video")
        {
            var decoded = Load(name); // Invalid data never replaces a working selection.
            if (kind == "logo" && !IsLogo(decoded)) throw new HubException("Choose a wide logo with a transparent background.");
        }
        if (!entries.TryGetValue(key, out var entry)) entries[key] = entry = new();
        if (kind == "background") entry.Background = name;
        else if (kind == "logo") entry.Logo = name;
        else if (kind == "video") entry.Video = name;
        else { entry.Cover = name; entry.Crop = null; }
        JsonData.WriteAtomic(paths.Managed("artwork", "index.json"), entries);
        cache.Clear();
    }
    public static Color SampleTint(BitmapSource source)
    {
        // A small hue histogram ignores white titles and deep black shadows.
        var scaled = new TransformedBitmap(source, new ScaleTransform(48d / source.PixelWidth, 48d / source.PixelHeight));
        var bitmap = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[48 * 48 * 4]; bitmap.CopyPixels(pixels, 48 * 4, 0);
        var bins = new double[12, 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            double r = pixels[i + 2] / 255d, g = pixels[i + 1] / 255d, b = pixels[i] / 255d;
            var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b)); var delta = max - min;
            if (pixels[i + 3] < 160 || max < .14 || min > .85 || delta < .07) continue;
            var hue = max == r ? (g - b) / delta : max == g ? 2 + (b - r) / delta : 4 + (r - g) / delta;
            var bin = ((int)Math.Floor(hue * 2) + 12) % 12;
            var weight = delta * Math.Sqrt(max);
            bins[bin, 0] += weight; bins[bin, 1] += r * weight; bins[bin, 2] += g * weight; bins[bin, 3] += b * weight;
        }
        var winner = Enumerable.Range(0, 12).MaxBy(i => bins[i, 0]);
        var count = bins[winner, 0];
        return count < 1 ? Neutral : Color.FromRgb((byte)(bins[winner, 1] / count * 255), (byte)(bins[winner, 2] / count * 255), (byte)(bins[winner, 3] / count * 255));
    }
}
