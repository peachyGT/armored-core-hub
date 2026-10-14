using System.Buffers.Binary;
using System.Text;
using Hub.Core;

static class ImportTests
{
    public static void Register(Action<string, Action> test, Action<string, Func<Task>> asyncTest, string scratch, Catalog catalog)
    {
        var root = new HubPaths(Path.Combine(scratch, "import-regressions")); var media = new MediaService(root, catalog);
        var edition = catalog.Editions.Single(e => e.Serial == "BLES00039");
        var file = Path.Combine(scratch, "a completely unrelated name.iso"); Ps3Iso(file);
        void Check(bool value) { if (!value) throw new Exception("Import regression assertion failed"); }
        test("Xbox ISO reads XEX title at every supported partition offset, preserving region ambiguity", () => {
            foreach(var offset in new long[]{0,0xFB20,0x20600,0x2080000,0xFD90000})
            {
                var path=Path.Combine(scratch,"xbox.iso"); XboxIso(path,offset);
                var c=media.Probe(path);
                Check(c.Format=="iso" && c.Platform=="x360" && c.ProductId=="4E4D084F" && c.Problem==null);
                Check(c.EditionId==null && c.Warnings.Any(w=>w.Contains("shared")));
            }
        });
        test("Xbox disc malformed trees, extents and XEX offsets are rejected within bounds", () => {
            foreach(var fault in new[]{"cycle","name","size","extent","xex"})
            {
                var path=Path.Combine(scratch,"xbox-"+fault+".iso"); XboxIso(path,0,fault);
                var c=media.Probe(path); Check(c.Platform=="x360" && c.ProductId==null && c.Problem!=null);
            }
        });
        asyncTest("Xbox ISO cannot be registered as a PlayStation game", async () => {
            var path=Path.Combine(scratch,"wrong-console.iso");XboxIso(path,0);
            try { await media.RegisterAsync(media.Probe(path),"ac2-ps2-slus20014",false,new()); throw new Exception("Wrong console accepted"); }
            catch(HubException){}
        });
        test("PS3 ISO identifies AC4 EU from metadata without filename hints", () => {
            var c = media.Probe(file); Check(c.EditionId == edition.Id && c.Platform == "ps3" && c.Format == "ps3-iso" && c.Problem == null);
        });
        test("Conflicting PS3 disc identities are not automatically matched", () => {
            var path = Path.Combine(scratch, "conflicting.iso"); Ps3Iso(path, header: "BLUS-30027"); var c = media.Probe(path);
            Check(c.EditionId == null && c.Problem != null && c.Format == "ps3-iso");
        });
        test("Encrypted PS3 image stays identified but requires a readable copy", () => {
            var path = Path.Combine(scratch, "encrypted.iso"); Ps3Iso(path, encrypted: true); var c = media.Probe(path);
            Check(c.EditionId == edition.Id && c.Problem?.Contains("encrypted") == true);
        });
        asyncTest("Recognized PS3 import rejects another edition and a blocked image", async () => {
            foreach (var (candidate, id) in new[] { (media.Probe(file), "ac4-ps3-blus30027"), (media.Probe(Path.Combine(scratch, "encrypted.iso")), edition.Id) })
            { try { await media.RegisterAsync(candidate, id, false, new()); throw new Exception("Wrong import accepted"); } catch (HubException) { } }
        });
        asyncTest("PS3 ISO registers in place and creates direct RPCS3 launch arguments", async () => {
            var record = await media.RegisterAsync(media.Probe(file), edition.Id, false, new());
            Check(record.Confidence == MediaConfidence.RecognizedUnverified && !record.Managed && await media.VerifyAsync(record));
            var exeDir = root.Managed("runtime", "fixture"); Directory.CreateDirectory(exeDir); File.WriteAllText(Path.Combine(exeDir, "rpcs3.exe"), "fixture");
            var fw = root.Managed("firmware", "fixture.pup"); File.WriteAllText(fw, "fixture");
            var build = new InstalledBuild("ps3-iso-build", "rpcs3", "0.0.43-20251", root.Portable(exeDir), "rpcs3.exe", "", await SafeFiles.HashAsync(Path.Combine(exeDir,"rpcs3.exe")), "fixture", DateTimeOffset.UtcNow);
            var state = new HubState { Media = [record], Builds = [build], Firmware = [new("fw", "ps3-firmware", root.Portable(fw), "", "fixture", false, "Global")] };
            state.Selections[edition.Id] = new() { EditionId = edition.Id, MediaId = record.Id, BuildId = build.Id };
            var planner = new LaunchPlanner(root, catalog); var plan = planner.Resolve(edition.Id, state);
            Check(plan.Arguments.Last() == file && plan.BuildId == build.Id && plan.Arguments.Contains("--no-gui"));
            var module = Path.Combine(exeDir, "dev_flash", "vsh", "module"); Directory.CreateDirectory(module); File.WriteAllText(Path.Combine(module, "vsh.self"), "fixture");
            var external = Path.Combine(exeDir,"dev_flash","sys","external"); Directory.CreateDirectory(external); File.WriteAllText(Path.Combine(external,"liblv2.sprx"),"fixture");
            await new LaunchService(root,catalog).PrepareAsync(plan,state);
            var username = Path.Combine(plan.SaveDirectory,"dev_hdd0","home","00000001","localusername");
            Check(File.ReadAllText(username) == "Hub"); File.WriteAllText(username,"Existing user");
            await new LaunchService(root,catalog).PrepareAsync(plan,state); Check(File.ReadAllText(username) == "Existing user");
            state.Builds[0] = build with { Version = "0.0.20-10000" };
            Check(planner.Diagnose(edition.Id, state).Any(d => d.Name == "Game preparation" && d.Status == "error"));
        });
        asyncTest("Scan ignores unrelated locked DLLs and continues after an unreadable ISO", async () => {
            var dir = Path.Combine(scratch, "mixed-scan"); Directory.CreateDirectory(dir); File.Copy(file, Path.Combine(dir, "good.iso"));
            var dll = Path.Combine(dir, "unrelated.dll"); File.WriteAllBytes(dll, new byte[4096]);
            var locked = Path.Combine(dir, "locked.iso"); File.WriteAllBytes(locked, new byte[4096]);
            using var a = new FileStream(dll, FileMode.Open, FileAccess.Read, FileShare.None);
            using var b = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
            var results = await media.ScanAsync(dir);
            Check(results.Count == 2 && results.Count(c => c.EditionId == edition.Id) == 1 && results.Count(c => c.Format == "inaccessible") == 1);
        });
        test("Oversized PS3 folder metadata is rejected without allocating its file size", () => {
            var dir = Path.Combine(scratch, "large-sfo", "PS3_GAME"); Directory.CreateDirectory(dir);
            using (var s = File.Create(Path.Combine(dir, "PARAM.SFO"))) s.SetLength(1048577);
            Check(media.Probe(Path.GetDirectoryName(dir)!).Format == "inaccessible");
        });
        test("Truncated PS3 executable extents cannot be marked ready", () => {
            var path = Path.Combine(scratch,"truncated-ps3.iso"); Ps3Iso(path); using(var stream = new FileStream(path,FileMode.Open,FileAccess.Write)) stream.SetLength(25*2048);
            Check(media.Probe(path).Problem != null);
        });
        test("Oversized CUE metadata is rejected before parsing", () => {
            var path = Path.Combine(scratch,"large.cue"); using(var stream = File.Create(path)) stream.SetLength(1048577);
            Check(media.Probe(path).Format == "inaccessible");
        });
        asyncTest("Reimporting firmware repairs a damaged content-addressed copy", async () => {
            var source = Path.Combine(scratch, "synthetic.pup"); var bytes = new byte[128]; "SCEUF"u8.CopyTo(bytes); File.WriteAllBytes(source, bytes);
            var service = new FirmwareService(root, catalog); var original = await service.ImportAsync(source);
            File.WriteAllText(root.Resolve(original.Path), "damaged"); var repaired = await service.ImportAsync(source);
            Check(await SafeFiles.HashAsync(root.Resolve(repaired.Path)) == original.Sha256);
        });
    }

    private static void XboxIso(string file,long partition,string? fault=null)
    {
        using var stream=File.Create(file);stream.SetLength(partition+40*2048);
        var header=new byte[28];"MICROSOFT*XBOX*MEDIA"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20),33);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24),fault=="size"?uint.MaxValue:2048);
        stream.Position=partition+32*2048;stream.Write(header);
        var dir=new byte[2048];dir[13]=11;"default.xex"u8.CopyTo(dir.AsSpan(14));
        BinaryPrimitives.WriteUInt32LittleEndian(dir.AsSpan(4),34);
        BinaryPrimitives.WriteUInt32LittleEndian(dir.AsSpan(8),fault=="extent"?uint.MaxValue:96);
        if(fault=="cycle") {dir[12]=16;BinaryPrimitives.WriteUInt16LittleEndian(dir.AsSpan(2),8);dir[32+13]=1;dir[32+14]=(byte)'x';BinaryPrimitives.WriteUInt16LittleEndian(dir.AsSpan(32),8);}
        if(fault=="name") dir[13]=0;
        stream.Position=partition+33*2048;stream.Write(dir);
        var xex=new byte[96];"XEX2"u8.CopyTo(xex);
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(20),1);
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(24),0x40006);
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(28),fault=="xex"?uint.MaxValue:48);
        Convert.FromHexString("4E4D084F").CopyTo(xex,60);
        stream.Position=partition+34*2048;stream.Write(xex);
    }

    public static void Ps3Iso(string file, string header = "BLES-00039", bool encrypted = false)
    {
        var image = new byte[30 * 2048]; "PlayStation3"u8.CopyTo(image.AsSpan(2048)); Encoding.ASCII.GetBytes(header).CopyTo(image, 2064);
        var pvd = image.AsSpan(16 * 2048, 2048); pvd[0] = 1; "CD001"u8.CopyTo(pvd[1..]); pvd[6] = 1;
        byte[] Record(uint lba, uint size, byte[] name, bool dir) { var r = new byte[34 + name.Length]; r[0] = (byte)r.Length; BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(2), lba); BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(10), size); r[25] = dir ? (byte)2 : (byte)0; r[32] = (byte)name.Length; name.CopyTo(r,33); return r; }
        Record(20, 2048, [0], true).CopyTo(pvd[156..]); Record(21, 2048, "PS3_GAME"u8.ToArray(), true).CopyTo(image,20*2048);
        var sfo = Fixtures.Sfo("TITLE_ID", "BLES00039");
        var param = Record(22,(uint)sfo.Length,"PARAM.SFO;1"u8.ToArray(),false); param.CopyTo(image,21*2048); sfo.CopyTo(image,22*2048);
        Record(23,2048,"USRDIR"u8.ToArray(),true).CopyTo(image,21*2048+param.Length);
        // Large executable entry proves readiness reads only its prefix, not a 16+ MiB allocation.
        Record(24,16886768,"EBOOT.BIN;1"u8.ToArray(),false).CopyTo(image,23*2048);
        (encrypted ? "BAD!"u8 : "SCE\0"u8).CopyTo(image.AsSpan(24*2048)); File.WriteAllBytes(file,image);
        using var output = new FileStream(file, FileMode.Open, FileAccess.Write); output.SetLength(24*2048 + 16886768);
    }
}
