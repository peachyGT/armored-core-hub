using Hub.Core;

static class MediaSmoke
{
    // Opt-in local integration: never runs in the automated synthetic suite.
    public static async Task<int> Run(string[] args)
    {
        var paths = new HubPaths(args[1]); var catalog = CatalogStore.Load(Path.Combine(AppContext.BaseDirectory, "manifests"));
        var service = new MediaService(paths, catalog); var candidate = service.Probe(args[0]);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { candidate.Format, candidate.EditionId, candidate.ProductId, candidate.Problem }));
        if (candidate.EditionId == null || candidate.Problem != null) return 1;
        var stamp = File.GetLastWriteTimeUtc(args[0]); var size = new FileInfo(args[0]).Length;
        var state = new HubState(); var record = await service.RegisterAsync(candidate, candidate.EditionId, false, state); state.Media.Add(record);
        var build = JsonData.Read<InstalledBuild>(Path.Combine(args[2], "hub-build.json")) with { Path = Path.GetFullPath(args[2]) }; state.Builds.Add(build);
        state.Firmware.Add(await new FirmwareService(paths, catalog).ImportAsync(args[3]));
        var edition = catalog.Editions.Single(e => e.Id == record.EditionId);
        state.Selections[edition.Id] = new() { EditionId = edition.Id, MediaId = record.Id, BuildId = build.Id, Fullscreen = false };
        new StateStore(paths).Save(state); var plan = new LaunchPlanner(paths, catalog).Resolve(edition.Id, state);
        Console.WriteLine("Registered " + edition.Id + "; " + record.Confidence + "; direct ISO argument=" + (plan.Arguments.Last() == Path.GetFullPath(args[0])));
        if (args.Contains("--launch"))
        {
            var launch = new LaunchService(paths, catalog); launch.StatusChanged += Console.WriteLine;
            // Preparation (including firmware) has its own timeout. Limit only the actual session.
            await launch.PrepareAsync(plan, state);
            var secondsArg = args.FirstOrDefault(a=>a.StartsWith("--seconds="));
            var seconds = secondsArg == null ? 40 : int.Parse(secondsArg[10..]);
            if (seconds is < 10 or > 300) throw new ArgumentException("Smoke duration must be 10–300 seconds.");
            using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
            try { await launch.LaunchAsync(plan, state, ct.Token); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { Console.WriteLine("Owned smoke session closed at its time limit."); }
        }
        if (File.GetLastWriteTimeUtc(args[0]) != stamp || new FileInfo(args[0]).Length != size) throw new Exception("Source metadata changed");
        JsonData.WriteAtomic(paths.Managed("logs", "media-smoke.json"), new { edition.Id, record.Confidence, directIso = plan.Arguments.Last() == Path.GetFullPath(args[0]), sourceUnchanged = true });
        return 0;
    }
}
