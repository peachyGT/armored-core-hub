using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Hub.Core;

static class CompatibilityTests
{
    public static void Register(Action<string, Action> test, Action<string, Func<Task>> asyncTest, string scratch, Catalog catalog)
    {
        void Check(bool ok) { if (!ok) throw new Exception("Compatibility repair regression"); }
        async Task Reject(Func<Task> f) { try { await f(); } catch (Exception e) when (e is HubException or IOException or OperationCanceledException) { return; } throw new Exception("Expected rejection"); }
        var edition = catalog.Editions.Single(e => e.Id == "ac4-ps3-bles00039");
        var profile = catalog.Profiles.Single(p => p.Id == edition.PreferredProfile);
        var recommendation = profile.BuildRecommendation!;
        test("Exact edition build recommendation and bounded regression range", () => {
            Check(recommendation.Release.Version == "0.0.43-20152" && recommendation.Formats.SequenceEqual(["ps3-folder"]));
            Check(catalog.Profiles.Count(p => p.BuildRecommendation != null) == 2);
            var b = new InstalledBuild("b", "rpcs3", "0.0.43-20250", "", "", "", "", "", DateTimeOffset.UtcNow);
            Check(RecommendedBuilds.Issue(profile, b) != null);
            Check(RecommendedBuilds.Issue(profile, b with { Version = "0.0.43-20152" }) == null);
            Check(RecommendedBuilds.Issue(profile, b with { Version = "0.0.43-20251" }) == null);
            Check(RecommendedBuilds.Issue(profile, b with { Version = "unorderable" }) == null);
        });
        test("Manifest rejects a recommended build that cannot read its declared format", () => {
            var c = JsonSerializer.Deserialize<Catalog>(JsonSerializer.Serialize(catalog, JsonData.Options), JsonData.Options)!;
            var index = c.Profiles.FindIndex(p => p.Id == profile.Id);
            c.Profiles[index] = profile with { BuildRecommendation = recommendation with { Formats = ["ps3-iso"] } };
            try { CatalogStore.Validate(c); } catch (HubException) { return; }
            throw new Exception("Unsupported build/format recommendation accepted");
        });
        asyncTest("PS3 derived folder preserves ISO, identifies edition and verifies repeat reuse", async () => {
            var paths = new HubPaths(Path.Combine(scratch, "prepare-disc"));
            var iso = Path.Combine(scratch, "prepare-source.iso"); ImportTests.Ps3Iso(iso);
            var media = new MediaService(paths, catalog); var state = new HubState();
            var original = await media.RegisterAsync(media.Probe(iso), edition.Id, false, state); state.Media.Add(original);
            var service = new Ps3DiscPreparation(paths, catalog);
            var prepared = await service.PrepareAsync(original with { Confidence = MediaConfidence.VerifiedExactDump }, state);
            Check(prepared.Format == "ps3-folder" && prepared.EditionId == edition.Id && prepared.Managed && prepared.Confidence == MediaConfidence.RecognizedUnverified);
            Check(prepared.Files.Count == 2 && prepared.Files.Single(f => f.RelativeName.EndsWith("EBOOT.BIN")).Size == 16886768);
            Check(await SafeFiles.HashAsync(iso) == original.Fingerprint && state.Media.Count == 1);
            var again = await service.PrepareAsync(original, state); Check(again.Id == prepared.Id && again.Path == prepared.Path);
            File.WriteAllText(Path.Combine(paths.Resolve(prepared.Path), "PS3_GAME", "USRDIR", "EBOOT.BIN"), "modified");
            await Reject(() => service.PrepareAsync(original, state));
            Check(File.ReadAllText(Path.Combine(paths.Resolve(prepared.Path), "PS3_GAME", "USRDIR", "EBOOT.BIN")) == "modified");
        });
        asyncTest("PS3 preparation rejects unsafe, cyclic, duplicate and unsupported ISO records", async () => {
            foreach (var fault in new[] { "../escape", "NUL", "duplicate", "cycle", "extent", "multi" })
            {
                var paths = new HubPaths(Path.Combine(scratch, "bad-disc-" + fault.Replace('/', '_')));
                var iso = paths.Managed("games", "source.iso"); ImportTests.Ps3Iso(iso);
                var name = fault switch { "duplicate" => "PS3_GAME", "cycle" => "LOOP", "extent" or "multi" => "EXTRA.BIN", _ => fault };
                var bytes = Encoding.ASCII.GetBytes(name); var record = new byte[34 + bytes.Length]; record[0] = (byte)record.Length;
                BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(2), fault == "extent" ? uint.MaxValue : fault == "cycle" ? 20U : 24U);
                BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(10), fault == "cycle" ? 2048U : 4U);
                record[25] = fault == "cycle" ? (byte)2 : fault == "multi" ? (byte)128 : (byte)0;
                record[32] = (byte)bytes.Length; bytes.CopyTo(record, 33);
                using (var stream = new FileStream(iso, FileMode.Open, FileAccess.Write)) { stream.Position = 20 * 2048 + 42; stream.Write(record); }
                var media = new MediaService(paths, catalog); var source = await media.RegisterAsync(media.Probe(iso), edition.Id, false, new());
                await Reject(() => new Ps3DiscPreparation(paths, catalog).PrepareAsync(source, new()));
                var derived = paths.Managed("games", "prepared-ps3");
                Check(!Directory.Exists(derived) || !Directory.EnumerateFileSystemEntries(derived).Any());
                Check(await SafeFiles.HashAsync(iso) == source.Fingerprint);
            }
        });
        asyncTest("PS3 preparation prefers complete Joliet names over lossy ISO aliases", async () => {
            var paths = new HubPaths(Path.Combine(scratch, "joliet-disc")); var iso = paths.Managed("games", "source.iso"); ImportTests.Ps3Iso(iso);
            byte[] Record(uint lba,uint size,byte[] name,bool directory) {
                var r=new byte[34+name.Length];r[0]=(byte)r.Length;r[25]=directory?(byte)2:(byte)0;r[32]=(byte)name.Length;name.CopyTo(r,33);
                BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(2),lba);BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(10),size);return r;
            }
            using(var stream=new FileStream(iso,FileMode.Open,FileAccess.Write)) {
                void Write(int sector,byte[] bytes) {stream.Position=sector*2048;stream.Write(bytes);}
                var descriptor=new byte[2048];descriptor[0]=2;"CD001"u8.CopyTo(descriptor.AsSpan(1));descriptor[6]=1;"%/@"u8.CopyTo(descriptor.AsSpan(88));
                Record(26,2048,[0],true).CopyTo(descriptor,156);Write(17,descriptor);
                descriptor[0]=255;Write(18,descriptor);
                Write(26,Record(27,2048,Encoding.BigEndianUnicode.GetBytes("PS3_GAME"),true));
                Write(27,Record(22,(uint)Fixtures.Sfo("TITLE_ID","BLES00039").Length,Encoding.BigEndianUnicode.GetBytes("PARAM.SFO;1"),false)
                    .Concat(Record(28,2048,Encoding.BigEndianUnicode.GetBytes("USRDIR"),true)).ToArray());
                Write(28,Record(24,16886768,Encoding.BigEndianUnicode.GetBytes("EBOOT.BIN;1"),false)
                    .Concat(Record(29,3,Encoding.BigEndianUnicode.GetBytes("original-name.dat;1"),false)).ToArray());
                Write(29,[1,2,3]);
            }
            var media=new MediaService(paths,catalog);var source=await media.RegisterAsync(media.Probe(iso),edition.Id,false,new());
            var result=await new Ps3DiscPreparation(paths,catalog).PrepareAsync(source,new());
            Check(result.Files.Count==3 && File.ReadAllBytes(Path.Combine(paths.Resolve(result.Path),"PS3_GAME","USRDIR","original-name.dat")).SequenceEqual(new byte[]{1,2,3}));
        });
        asyncTest("Cancelled disc preparation publishes no partial folder", async () => {
            var paths = new HubPaths(Path.Combine(scratch, "cancel-disc")); var iso = paths.Managed("games", "source.iso"); ImportTests.Ps3Iso(iso);
            var media = new MediaService(paths, catalog); var source = await media.RegisterAsync(media.Probe(iso), edition.Id, false, new());
            using var cancel = new CancellationTokenSource();
            await Reject(() => new Ps3DiscPreparation(paths, catalog).PrepareAsync(source, new(), new SyncProgress(p => { if (p.Message == "Preparing game folder" && p.Completed > 0) cancel.Cancel(); }), cancel.Token));
            Check(!Directory.EnumerateDirectories(paths.Managed("games", "prepared-ps3")).Any());
            Check(await SafeFiles.HashAsync(iso) == source.Fingerprint);
        });
        asyncTest("Offline compatibility repair is transactional and keeps old media, builds and saves", async () => {
            var paths = new HubPaths(Path.Combine(scratch, "repair-state")); var iso = paths.Managed("games", "source.iso"); ImportTests.Ps3Iso(iso);
            var media = new MediaService(paths, catalog); var source = await media.RegisterAsync(media.Probe(iso), edition.Id, false, new());
            var exe = paths.Managed("runtime", "fixture", "rpcs3.exe"); Directory.CreateDirectory(Path.GetDirectoryName(exe)!); File.WriteAllText(exe, "synthetic executable");
            var good = new InstalledBuild("good", "rpcs3", recommendation.Release.Version, paths.Portable(Path.GetDirectoryName(exe)!), "rpcs3.exe", recommendation.Release.Sha256, await SafeFiles.HashAsync(exe), "fixture", DateTimeOffset.UtcNow);
            var bad = good with { Id = "old", Version = "0.0.43-20250", ArchiveSha256 = new string('0',64) };
            var choice = new GameSelection { EditionId = edition.Id, MediaId = source.Id, BuildId = bad.Id, Fullscreen = false, AdvancedSettings = new() { ["Video/Resolution Scale"] = "100" } };
            var state = new HubState { Offline = true, Media = [source], Builds = [bad, good], Selections = new() { [edition.Id] = choice } };
            var save = paths.Managed("saves", "sentinel"); File.WriteAllText(save, "only save");
            using var http = new HttpClient(new DenyNetwork()); var runtime = new RuntimeService(paths, catalog, new ProviderDownloader(paths, http), http);
            var repair = new CompatibilityRepair(paths, catalog, runtime);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Reject(() => repair.ApplyAsync(edition.Id, state, ct: cancelled.Token));
            Check(choice.MediaId == source.Id && choice.BuildId == bad.Id && state.Media.Count == 1);
            await repair.ApplyAsync(edition.Id, state);
            Check(choice.BuildId == good.Id && choice.MediaId != source.Id && state.Builds.Count == 2 && state.Media.Count == 2);
            Check(File.ReadAllText(save) == "only save" && choice.AdvancedSettings.Count == 1 && !choice.Fullscreen);
            Check(!new LaunchPlanner(paths, catalog).Diagnose(edition.Id,state).Any(d => d.Repair == "compatibility"));
            choice.BuildId = bad.Id;
            Check(new LaunchPlanner(paths,catalog).Diagnose(edition.Id,state).Any(d => d.Repair == "compatibility"));
            await repair.ApplyAsync(edition.Id, state); Check(state.Media.Count == 2 && state.Builds.Count == 2);
        });
    }
    sealed class SyncProgress(Action<ProgressInfo> action) : IProgress<ProgressInfo> { public void Report(ProgressInfo value) => action(value); }
    sealed class DenyNetwork : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new Exception("Unexpected network request"); }
}
