using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Hub.Core;

public sealed class MediaService(HubPaths paths, Catalog catalog)
{
    public static readonly string[] Formats = [".cue", ".bin", ".iso", ".chd", ".cso", ".pbp", ".xex", ".mdf", ".img"];
    public async Task<List<MediaCandidate>> ScanAsync(string root, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var results = new List<MediaCandidate>();
            if (File.Exists(root)) { results.Add(Probe(root)); return results; }
            SafeFiles.NoLinks(root);
            var pending = new Stack<string>(); pending.Push(Path.GetFullPath(root));
            var companionFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seen = 0;
            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var dir = pending.Pop();
                if (++seen > 100000) throw new HubException("The scan is too large. Choose a more specific collection folder.");
                progress?.Report(new("Scanning " + Path.GetFileName(dir), results.Count));
                if (File.Exists(Path.Combine(dir, "PS3_GAME", "PARAM.SFO")) || File.Exists(Path.Combine(dir, "PARAM.SFO")) && Directory.Exists(Path.Combine(dir, "USRDIR")))
                { results.Add(Probe(dir)); continue; }
                if (File.Exists(Path.Combine(dir, "default.xex"))) { results.Add(Probe(dir)); continue; }
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir))
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                        if (Formats.Contains(Path.GetExtension(file).ToLowerInvariant()) || MediaArchiveService.IsArchive(file))
                        {
                            results.Add(Probe(file));
                            if (Path.GetExtension(file).Equals(".cue", StringComparison.OrdinalIgnoreCase))
                            { try { foreach (var part in CueFiles(file)) companionFiles.Add(part); } catch (HubException) { } }
                        }
                        else if (IsContainerCandidate(file) && new FileInfo(file).Length >= 0x400 && IsStfs(file)) results.Add(Probe(file));
                        }
                        catch (Exception e) when (e is IOException or UnauthorizedAccessException or HubException)
                        { results.Add(Unreadable(file, e)); }
                    }
                    foreach (var child in Directory.EnumerateDirectories(dir))
                    {
                        try {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0 && !Path.GetFileName(child).StartsWith('.')) pending.Push(child);
                        } catch (Exception e) when (e is IOException or UnauthorizedAccessException or HubException) { results.Add(Unreadable(child, e)); }
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or HubException) { results.Add(Unreadable(dir, e)); }
            }
            return results.Where(x => !companionFiles.Contains(x.Path)).ToList();
        }, ct);
    }

    private static bool IsContainerCandidate(string file) => Path.GetExtension(file).ToLowerInvariant() is "" or ".stfs" or ".000";
    private static MediaCandidate Unreadable(string path, Exception error) => new(path, "inaccessible", null, null,
        "Skipped an unreadable file or folder.", ["Access failed (" + error.HResult.ToString("X8") + ")."]);

    public MediaCandidate Probe(string path)
    {
        path = Path.GetFullPath(path);
        string? product = null; var warnings = new List<string>();
        string format; string? platform = null, problem = null;
        try
        {
            SafeFiles.NoLinks(path);
            if (MediaArchiveService.IsArchive(path)) return new(path, "archive", null, null, "Unpack to identify the games inside.", []);
            if (Directory.Exists(path))
            {
                var sfo = Path.Combine(path, "PS3_GAME", "PARAM.SFO");
                if (!File.Exists(sfo)) sfo = Path.Combine(path, "PARAM.SFO");
                if (File.Exists(sfo)) { SafeFiles.NoLinks(sfo); if (new FileInfo(sfo).Length > 1048576) throw new HubException("Game metadata exceeds its size limit."); format = "ps3-folder"; platform = "ps3"; product = ReadSfo(File.ReadAllBytes(sfo)).GetValueOrDefault("TITLE_ID"); }
                else if (File.Exists(Path.Combine(path, "default.xex"))) { format = "x360-folder"; platform = "x360"; product = XexTitle(Path.Combine(path, "default.xex")); }
                else return new(path, "folder", null, null, "Choose a scan to search this folder.", []);
            }
            else
            {
                format = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                if (IsStfs(path)) { format = "stfs"; using var f = File.OpenRead(path); f.Position = 0x360; var b = new byte[4]; f.ReadExactly(b); product = Convert.ToHexString(b); }
                else if (format == "xex") { platform = "x360"; product = XexTitle(path); }
                else if (format == "cue")
                {
                    var files = CueFiles(path).ToList();
                    if (files.Count == 0) throw new HubException("The CUE file has no referenced tracks.");
                    foreach (var f in files) if (!File.Exists(f)) throw new HubException("A CUE track is missing: " + Path.GetFileName(f));
                    product = ReadDiscProduct(files[0]);
                }
                else if (format == "iso" && Ps3Disc.Inspect(path) is { } ps3)
                { format = "ps3-iso"; platform = "ps3"; product = ps3.ProductId; problem = ps3.Problem; }
                else if (format == "iso" && XboxDisc.Inspect(path) is { } xbox)
                { platform = "x360"; product = xbox.TitleId; problem = xbox.Problem; }
                else if (format is "iso" or "bin" or "img" or "mdf") product = ReadDiscProduct(path);
                else warnings.Add("Compressed media needs manual edition confirmation unless its exact hash is in the catalog.");
            }
            var normalized = NormalizeSerial(product ?? "");
            var editions = catalog.Editions.Where(e => e.Formats.Contains(format) && (platform == null || e.Platform == platform) && (NormalizeSerial(e.Serial) == normalized || e.TitleId != null && NormalizeSerial(e.TitleId) == normalized)).ToList();
            var edition = normalized.Length > 0 && editions.Count == 1 ? editions[0] : null;
            if (editions.Count > 1) warnings.Add("This product ID is shared by multiple regions. Choose the edition printed on your copy.");
            if (format == "ps3-folder" && !File.Exists(Path.Combine(path, "PS3_GAME", "USRDIR", "EBOOT.BIN")) && !File.Exists(Path.Combine(path, "USRDIR", "EBOOT.BIN"))) warnings.Add("PS3 executable EBOOT.BIN is missing.");
            return new(path, format, edition?.Id, product, edition == null ? "Choose the game and edition for this file." : "Edition detected from game data.", warnings.ToArray(), platform ?? edition?.Platform, problem);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or HubException or InvalidDataException or ArgumentException)
        { return new(path, "inaccessible", null, product, "This file could not be read.", [e is HubException ? e.Message : "Access failed (" + e.HResult.ToString("X8") + ")."], platform); }
    }

    public async Task<MediaRecord> RegisterAsync(MediaCandidate candidate, string? editionId, bool copy,
        HubState state, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var edition = catalog.Editions.SingleOrDefault(e => e.Id == editionId);
        if (edition == null) throw new HubException("Select an Armored Core edition before importing this file.");
        if (candidate.Problem != null) throw new HubException(candidate.Problem);
        if (candidate.EditionId != null && candidate.EditionId != editionId) throw new HubException("This copy was identified as a different edition. Use its detected edition.");
        if (candidate.Platform != null && candidate.Platform != edition.Platform) throw new HubException("This copy belongs to a different console.");
        if (candidate.ProductId != null && NormalizeSerial(candidate.ProductId) != NormalizeSerial(edition.Serial)
            && NormalizeSerial(candidate.ProductId) != NormalizeSerial(edition.TitleId ?? "")) throw new HubException("This copy belongs to a different game. Use an edition matching its product ID.");
        if (!edition.Formats.Contains(candidate.Format)) throw new HubException("This media format is not supported by the selected edition.");
        if (candidate.Format == "ps3-iso")
        {
            var current = Probe(candidate.Path);
            if (current.Format != "ps3-iso" || current.Problem != null || current.ProductId != candidate.ProductId)
                throw new HubException(current.Problem ?? "The PS3 disc metadata changed. Scan it again before importing.");
        }
        SafeFiles.NoLinks(candidate.Path);
        var files = new List<MediaFile>();
        var directory = Directory.Exists(candidate.Path);
        var sourceRoot = directory ? candidate.Path : Path.GetDirectoryName(candidate.Path)!;
        var sources = directory ? SafeFiles.Files(candidate.Path).Order(StringComparer.Ordinal).ToList()
            : candidate.Format == "cue" ? CueFiles(candidate.Path).Prepend(candidate.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : [candidate.Path];
        long lastProgress = 0;
        foreach (var file in sources)
        {
            ct.ThrowIfCancellationRequested(); SafeFiles.NoLinks(file);
            if (files.Count == 0 || Environment.TickCount64 - lastProgress >= 150)
            { progress?.Report(new("Checking game files", files.Count, sources.Count, "files")); lastProgress = Environment.TickCount64; }
            var info = new FileInfo(file); var before = info.LastWriteTimeUtc.Ticks; var size = info.Length;
            var hash = await SafeFiles.HashAsync(file, ct);
            info.Refresh(); if (before != info.LastWriteTimeUtc.Ticks || size != info.Length) throw new HubException("The media changed during import. Close other tools and try again.");
            files.Add(new(Path.GetRelativePath(sourceRoot, file), size, hash, before));
        }
        progress?.Report(new("Game files checked", files.Count, sources.Count, "files"));
        if (files.Count == 0) throw new HubException("The selected folder is empty.");
        var fingerprint = files.Count == 1 ? files[0].Sha256 : SafeFiles.HashText(string.Join('\n', files.Select(f => f.RelativeName.Replace('\\', '/') + ":" + f.Size + ":" + f.Sha256)));
        var duplicate = state.Media.FirstOrDefault(m => m.Fingerprint == fingerprint);
        if (duplicate != null)
        {
            if (duplicate.EditionId != editionId) throw new HubException("This same media is already registered as another edition. Correct its edition instead of duplicating it.");
            var priorPath = paths.Resolve(duplicate.Path);
            // A moved external copy can be relinked by importing identical content again.
            var samePath=Path.GetFullPath(priorPath).Equals(Path.GetFullPath(candidate.Path),StringComparison.OrdinalIgnoreCase);
            if(!samePath&&(File.Exists(priorPath)||Directory.Exists(priorPath))&&await VerifyAsync(duplicate,ct))return duplicate;
            // A damaged existing location must not defeat reimport of clean identical
            // content. Refresh timestamps too, so touching an unchanged image can recover.
            return duplicate with { Path = paths.Portable(candidate.Path), Format=candidate.Format,Files = files, Managed = SafeFiles.IsWithin(paths.Games(), candidate.Path) };
        }
        var confidence = candidate.EditionId == editionId ? MediaConfidence.RecognizedUnverified : MediaConfidence.UnknownRevision;
        // Exact comparison only when a reference explicitly covers this file representation.
        var references = edition.Hashes.Where(h => h.Format == null || h.Format == candidate.Format).ToArray();
        if (files.Count == 1 && references.Length > 0)
        {
            var matched = false;
            foreach (var reference in references)
            {
                if (reference.Size.HasValue && reference.Size.Value != files[0].Size) continue;
                var digest = files[0].Sha256;
                var algorithm = reference.Algorithm.ToLowerInvariant();
                if (algorithm != "sha256")
                {
                    await using var stream = File.OpenRead(sources[0]);
                    digest = Convert.ToHexStringLower(algorithm == "sha1" ? await SHA1.HashDataAsync(stream, ct) : await MD5.HashDataAsync(stream, ct));
                }
                matched |= digest.Equals(reference.Value, StringComparison.OrdinalIgnoreCase);
            }
            confidence = matched ? MediaConfidence.VerifiedExactDump : MediaConfidence.ModifiedImage;
        }
        var registered = candidate.Path;
        if (copy && !SafeFiles.IsWithin(paths.Games(), candidate.Path))
        {
            var target = paths.Games(fingerprint); var stage = target + ".partial-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(stage);
                for (var i = 0; i < sources.Count; i++)
                {
                    var dest = SafeFiles.Within(stage, files[i].RelativeName); Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    await using (var input = File.OpenRead(sources[i])) await using (var output = File.Create(dest)) await input.CopyToAsync(output, ct);
                    if (await SafeFiles.HashAsync(dest, ct) != files[i].Sha256) throw new HubException("The copied media failed its integrity check.");
                }
                if (Directory.Exists(target)) throw new HubException("A managed media directory already exists. Register that directory instead.");
                await SafeFiles.MoveManagedDirectoryAsync(paths.GameStorageRoot, stage, target, ct);
                registered = directory ? target : Path.Combine(target, Path.GetFileName(candidate.Path));
            }
            finally { SafeFiles.DeleteManagedTree(paths.GameStorageRoot, stage); }
        }
        return new(fingerprint, paths.Portable(registered), candidate.Format, editionId, fingerprint, confidence,
            confidence == MediaConfidence.UnknownRevision ? "Edition selected manually; revision is unknown." : candidate.Reason, files, DateTimeOffset.UtcNow, SafeFiles.IsWithin(paths.Games(), registered));
    }

    public async Task<bool> VerifyAsync(MediaRecord media, CancellationToken ct = default)
    {
        if(media.Files.Count==0)return false;
        var path = paths.Resolve(media.Path); var root = Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
        if(!File.Exists(path)&&!Directory.Exists(path))return false;
        foreach (var f in media.Files)
        { var file = SafeFiles.Within(root, f.RelativeName); if (!File.Exists(file) || new FileInfo(file).Length != f.Size || await SafeFiles.HashAsync(file, ct) != f.Sha256) return false; }
        return true;
    }
    public static string NormalizeSerial(string text) => Regex.Replace(text.ToUpperInvariant(), "[^A-Z0-9]", "");
    public static IEnumerable<string> CueFiles(string cue)
    {
        SafeFiles.NoLinks(cue);
        if (new FileInfo(cue).Length > 1048576) throw new HubException("The CUE metadata is too large. Choose a valid disc descriptor.");
        foreach (Match m in Regex.Matches(File.ReadAllText(cue), "^\\s*FILE\\s+(?:\"([^\"]+)\"|(\\S+))\\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            yield return SafeFiles.Within(Path.GetDirectoryName(Path.GetFullPath(cue))!, m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
    }
    private static bool IsStfs(string file)
    {
        using var stream = File.OpenRead(file); Span<byte> b = stackalloc byte[4]; if (stream.Read(b) != 4) return false;
        return b.SequenceEqual("CON "u8) || b.SequenceEqual("LIVE"u8) || b.SequenceEqual("PIRS"u8);
    }
    private static string? XexTitle(string file)
    {
        SafeFiles.NoLinks(file); using var f = File.OpenRead(file);
        return XboxDisc.ReadXexTitle(f, 0, f.Length);
    }
    private static string? ReadDiscProduct(string file)
    {
        foreach (var (sector, offset) in new[] {(2048, 0), (2352, 24), (2352, 16)})
        {
            using var reader = new IsoReader(file, sector, offset);
            if (!reader.Valid) continue;
            var sfo = reader.ReadFile("PSP_GAME/PARAM.SFO");
            if (sfo != null) return ReadSfo(sfo).GetValueOrDefault("DISC_ID");
            var cnf = reader.ReadFile("SYSTEM.CNF");
            if (cnf != null)
            {
                var match = Regex.Match(Encoding.ASCII.GetString(cnf), @"(?:BOOT2?|cdrom\d*)[^\r\n]*?([A-Z]{4})[_-](\d{3})\.(\d{2})", RegexOptions.IgnoreCase);
                if (match.Success) return match.Groups[1].Value.ToUpperInvariant() + "-" + match.Groups[2].Value + match.Groups[3].Value;
            }
        }
        return null;
    }
    public static Dictionary<string, string> ReadSfo(byte[] data)
    {
        var result = new Dictionary<string, string>();
        if (data.Length < 20 || !data.AsSpan(0, 4).SequenceEqual(new byte[] {0, 80, 83, 70})) return result;
        uint U32(int p) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(p, 4));
        var keys = U32(8); var values = U32(12); var count = U32(16); if (count > 4096 || 20L + count * 16L > data.Length) return result;
        for (var i = 0; i < count; i++)
        {
            var pos = 20 + (int)i * 16; var k = (long)keys + BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos, 2));
            var v = (long)values + U32(pos + 12); var len = U32(pos + 4);
            if (k >= data.Length || v + len > data.Length || len > 1048576) continue;
            var end = Array.IndexOf(data, (byte)0, (int)k); if (end < 0) continue;
            result[Encoding.UTF8.GetString(data, (int)k, end - (int)k)] = Encoding.UTF8.GetString(data, (int)v, (int)len).TrimEnd('\0');
        }
        return result;
    }
}

// Minimal read-only ISO9660 directory reader. Reads structural metadata, never searches arbitrary filenames for identity.
internal sealed class IsoReader : IDisposable
{
    private readonly FileStream stream; private readonly int sectorSize; private readonly int userOffset;
    private readonly byte[]? root;
    public bool Valid => root != null;
    public IsoReader(string file, int sectorSize, int userOffset)
    {
        stream = File.OpenRead(file); this.sectorSize = sectorSize; this.userOffset = userOffset;
        var pvd = Read(16, 2048);
        if (pvd?.AsSpan(1, 5).SequenceEqual("CD001"u8) == true && pvd[156] >= 34) root = pvd.AsSpan(156, pvd[156]).ToArray();
    }
    private byte[]? Read(uint lba, uint length)
    {
        if (length > 16 * 1024 * 1024 || (long)lba * sectorSize + userOffset >= stream.Length) return null;
        var result = new byte[length]; var total = 0;
        while (total < length)
        {
            var position = ((long)lba + total / 2048) * sectorSize + userOffset; var count = Math.Min(2048, result.Length - total);
            if (position + count > stream.Length) return null;
            stream.Position = position; stream.ReadExactly(result.AsSpan(total, count)); total += count;
        }
        return result;
    }
    public byte[]? ReadFile(string path, int maxBytes = 16 * 1024 * 1024)
    {
        if (maxBytes is < 1 or > 16 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        var record = root; if (record == null) return null;
        foreach (var segment in path.Split('/'))
        {
            if ((record[25] & 2) == 0) return null;
            var directory = Read(BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(2)), BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(10)));
            if (directory == null) return null; record = null;
            for (var p = 0; p < directory.Length;)
            {
                var len = directory[p]; if (len == 0) { p = ((p / 2048) + 1) * 2048; continue; }
                if (len < 34 || p + len > directory.Length || directory[p + 32] > len - 33) return null;
                var name = Encoding.ASCII.GetString(directory, p + 33, directory[p + 32]).Split(';')[0];
                if (name.Equals(segment, StringComparison.OrdinalIgnoreCase)) { record = directory.AsSpan(p, len).ToArray(); break; }
                p += len;
            }
            if (record == null) return null;
        }
        if ((record[25] & 2) != 0) return null;
        var size = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(10));
        var lba = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(2));
        if (size > 0 && ((long)lba + (size - 1L) / 2048) * sectorSize + userOffset + (size - 1L) % 2048 + 1 > stream.Length) return null;
        if (maxBytes == 16 * 1024 * 1024 && size > maxBytes) return null;
        return Read(lba, Math.Min(size, (uint)maxBytes));
    }
    public void Dispose() => stream.Dispose();

    // Only the ordinary ISO9660 view of decrypted PS3 discs. Never mount a disk,
    // follow Rock Ridge links, decrypt content, or write beside the user's image.
    public async Task ExtractPs3Async(string destination, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        if (root == null || sectorSize != 2048 || userOffset != 0) throw new HubException("This disc has no supported ISO9660 file view. Import a decrypted game folder instead.");
        var extractionRoot = root; var joliet = false;
        // PS3 discs can expose lossy ISO aliases alongside the complete Joliet tree.
        // Prefer Joliet exactly as an ISO extraction tool does; do not invent aliases.
        for (uint sector = 17; sector < 80; sector++)
        {
            var descriptor = Read(sector, 2048);
            if (descriptor == null || !descriptor.AsSpan(1, 5).SequenceEqual("CD001"u8)) break;
            if (descriptor[0] == 255) break;
            if (descriptor[0] == 2 && descriptor[88] == '%' && descriptor[89] == '/' && descriptor[90] is 0x40 or 0x43 or 0x45)
            {
                if (descriptor[156] < 34) throw new HubException("The disc's extended directory is damaged.");
                extractionRoot = descriptor.AsSpan(156, descriptor[156]).ToArray(); joliet = true;
            }
        }
        var files = new List<(string Name, uint Lba, uint Size)>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new HashSet<uint>();
        long total = 0, metadata = 0; int count = 0;
        void Visit(byte[] record, string parent, int depth)
        {
            ct.ThrowIfCancellationRequested();
            if (depth > 32 || ++count > 50000) throw new HubException("The disc contains too many files or nested folders.");
            if (record[1] != 0 || record[26] != 0 || record[27] != 0 || (record[25] & 128) != 0)
                throw new HubException("This disc uses an unsupported ISO9660 layout. Import a decrypted game folder instead.");
            var lba = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(2));
            var size = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(10));
            if ((long)lba * 2048 + size > stream.Length) throw new HubException("The disc is truncated. Import a complete copy.");
            if ((record[25] & 2) == 0)
            {
                total += size;
                if (total > MediaArchiveService.MaximumExpandedBytes) throw new HubException("The extracted disc exceeds the size limit.");
                files.Add((parent, lba, size)); return;
            }
            metadata += size;
            if (!directories.Add(lba) || metadata > 64 * 1024 * 1024) throw new HubException("The disc directory structure is invalid.");
            var directory = Read(lba, size) ?? throw new HubException("The disc directory cannot be read.");
            for (var p = 0; p < directory.Length;)
            {
                ct.ThrowIfCancellationRequested();
                var length = directory[p];
                if (length == 0) { p = ((p / 2048) + 1) * 2048; continue; }
                if (length < 34 || p + length > directory.Length || directory[p + 32] > length - 33)
                    throw new HubException("The disc directory is damaged.");
                var item = directory.AsSpan(p, length).ToArray(); p += length;
                if (item[32] == 1 && item[33] <= 1) continue; // ISO self and parent entries
                var raw = item.AsSpan(33, item[32]).ToArray();
                if (joliet ? raw.Length % 2 != 0 : raw.Any(b => b < 32 || b > 126)) throw new HubException("The disc contains unsupported filenames.");
                var name = joliet ? new UnicodeEncoding(true, false, true).GetString(raw) : Encoding.ASCII.GetString(raw);
                name = Regex.Replace(name, @";[0-9]+$", "");
                if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' ')
                    || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\') || name.Contains(':')
                    || Regex.IsMatch(name.Split('.')[0], "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase))
                    throw new HubException("The disc contains an unsafe filename.");
                var relative = parent.Length == 0 ? name : parent + "/" + name;
                SafeFiles.Within(destination, relative);
                if (!names.Add(relative)) throw new HubException("The disc contains duplicate paths.");
                Visit(item, relative, depth + 1);
            }
        }
        Visit(extractionRoot, "", 0);
        if (!files.Any(f => f.Name.Equals("PS3_GAME/USRDIR/EBOOT.BIN", StringComparison.OrdinalIgnoreCase)))
            throw new HubException("The disc is missing its PS3 executable.");
        SafeFiles.NoLinks(destination);
        if (new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destination))!).AvailableFreeSpace < total + 256L * 1024 * 1024)
            throw new HubException($"Free at least {total / 1073741824d:0.0} GB on the Hub drive to prepare this game.");
        var buffer = new byte[1024 * 1024]; long completed = 0, reported = 0;
        progress?.Report(new("Preparing game folder", 0, total));
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested(); var target = SafeFiles.Within(destination, file.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, true);
            stream.Position = (long)file.Lba * 2048; long remaining = file.Size;
            while (remaining > 0)
            {
                var bytes = (int)Math.Min(remaining, buffer.Length);
                await stream.ReadExactlyAsync(buffer.AsMemory(0, bytes), ct);
                await output.WriteAsync(buffer.AsMemory(0, bytes), ct);
                remaining -= bytes; completed += bytes;
                if (completed - reported >= 4 * 1024 * 1024) { progress?.Report(new("Preparing game folder", completed, total)); reported = completed; }
            }
        }
        progress?.Report(new("Game folder prepared", total, total));
    }
}
