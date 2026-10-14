using System.Text;
using System.Text.RegularExpressions;

namespace Hub.Core;

public sealed record Ps3DiscInfo(string? ProductId, string? Problem);

// Bounded, read-only inspection. Identity is from disc structures, never the filename.
// Encryption/readability is separate from identity and full-dump hash verification.
public static class Ps3Disc
{
    public static Ps3DiscInfo? Inspect(string file)
    {
        SafeFiles.NoLinks(file);
        using var stream = File.OpenRead(file);
        if (stream.Length < 17 * 2048) return null;
        stream.Position = 2048; var header = new byte[48]; stream.ReadExactly(header);
        var marked = header.AsSpan(0, 12).SequenceEqual("PlayStation3"u8);
        var discId = marked ? Encoding.ASCII.GetString(header, 16, 32).Trim(' ', '\0') : null;
        if (discId != null && !Regex.IsMatch(discId, "^[A-Z]{4}-?[0-9]{5}$")) discId = null;
        using var iso = new IsoReader(file, 2048, 0);
        var sfo = iso.ReadFile("PS3_GAME/PARAM.SFO");
        var product = sfo == null ? null : MediaService.ReadSfo(sfo).GetValueOrDefault("TITLE_ID");
        if (!marked && product == null) return null;
        if (discId != null && product != null && MediaService.NormalizeSerial(discId) != MediaService.NormalizeSerial(product))
            return new(null, "The disc header and game metadata disagree. Import a complete, consistent copy.");
        product ??= discId;
        if (sfo == null || !MediaService.ReadSfo(sfo).ContainsKey("TITLE_ID"))
            return new(product, "The PS3 game metadata could not be read. Import a decrypted ISO or a complete game folder.");
        var executable = iso.ReadFile("PS3_GAME/USRDIR/EBOOT.BIN", 16);
        if (executable == null || executable.Length < 4)
            return new(product, "The PS3 game executable is missing or unreadable. Import a complete game copy.");
        var licence = iso.ReadFile("PS3_GAME/LICDIR/LIC.DAT", 16);
        if (!executable.AsSpan(0, 4).SequenceEqual("SCE\0"u8) && !executable.AsSpan(0, 4).SequenceEqual(new byte[] { 127, 69, 76, 70 })
            || licence != null && (licence.Length < 8 || !licence.AsSpan(0, 8).SequenceEqual("PS3LICDA"u8)))
            return new(product, "This PS3 ISO is encrypted or damaged. Import a decrypted copy made from your disc; see Help for the PS3 dumping guide.");
        return new(product, null);
    }
}
