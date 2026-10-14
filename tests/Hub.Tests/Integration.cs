using Hub.Core;

internal static class Integration
{
    public static async Task<int> Download(string[] args)
    {
        var project = Path.GetFullPath(args[0]);
        var paths = new HubPaths(Path.Combine(project,"work","download-smoke-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        var catalog = CatalogStore.Load(Path.Combine(project,"manifests"));
        using var http = ProviderDownloader.CreateHttpClient();
        var runtime = new RuntimeService(paths,catalog,new(paths,http),http);
        var release = catalog.Backends.Single(x=>x.Id=="xenia").Recommended!;
        try
        {
            var installed = await runtime.InstallAsync("xenia",release,false);
            var cached = await runtime.InstallAsync("xenia",release,true);
            if(installed.Id != cached.Id) throw new Exception("Offline install changed build identity");
            JsonData.WriteAtomic(Path.Combine(project,"work","download-results.json"),new {passed=true,backend="xenia",source=release.Uri,archiveSha256=release.Sha256,offlineReuse=true});
            Console.WriteLine("PASS authoritative upstream download, hash verification, extraction and offline reuse"); return 0;
        }
        catch(Exception ex)
        {
            JsonData.WriteAtomic(Path.Combine(project,"work","download-results.json"),new {passed=false,error=ex.Message});
            Console.WriteLine(ex.Message); return 1;
        }
    }
    public static async Task<int> Run(string[] args)
    {
        var project = Path.GetFullPath(args[0]); var sandbox = Path.Combine(project, "work", "integration-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        var paths = new HubPaths(sandbox); var catalog = CatalogStore.Load(Path.Combine(project, "manifests"));
        using var http = ProviderDownloader.CreateHttpClient(); var service = new RuntimeService(paths, catalog, new(paths, http), http);
        var state = new HubState(); var results = new List<object>(); var failures = 0;
        foreach (var backend in catalog.Backends)
        {
            try
            {
                var release = backend.Recommended!; var archive = Path.Combine(project, "work", "emulator-smoke", release.FileName);
                var build = await service.InstallArchiveAsync(backend.Id, archive, release); state.Builds.Add(build);
                var executable = Path.Combine(paths.Resolve(build.Path), build.Executable); var cwd = Path.GetDirectoryName(executable)!;
                var flags = backend.Id is "duckstation" or "pcsx2" ? new[] {"-version"} : new[] {"--help"};
                if (backend.Id == "duckstation") File.WriteAllText(Path.Combine(cwd, "portable.txt"), "");
                if (backend.Id == "ppsspp")
                {
                    var stick=Path.Combine(cwd,"memstick");Directory.CreateDirectory(stick);File.WriteAllText(Path.Combine(cwd,"installed.txt"),stick);
                    var config=Path.Combine(cwd,"hub-test.ini");File.WriteAllText(config,"[General]\nFirstRun=false\nCheckForNewVersion=false\n");
                    using var process=System.Diagnostics.Process.Start(LaunchService.ProcessInfo(executable,cwd,["--config="+config,"--windowed"]))!;
                    await Task.Delay(2500);
                    if(process.HasExited)throw new Exception("PPSSPP exited during portable UI startup.");
                    process.CloseMainWindow(); if(!process.WaitForExit(5000))process.Kill(true);
                    if(!Directory.Exists(Path.Combine(stick,"PSP","SYSTEM")))throw new Exception("PPSSPP did not initialize the selected portable memory stick.");
                    Console.WriteLine("PASS PPSSPP pinned archive, portable config startup and owned-process shutdown");
                    results.Add(new {backend=backend.Id,passed=true,check="portable window/config startup; release has no --help exit"});continue;
                }
                var log = paths.Managed("logs", backend.Id + "-cli.log");
                var exit = await LaunchService.RunUtilityAsync(executable, cwd, flags, log, TimeSpan.FromSeconds(20), default);
                var output = File.ReadAllText(log) + File.ReadAllText(log + ".stderr");
                var expectedVersionExit = backend.Id is "duckstation" or "pcsx2" && exit == 1 && output.Contains(backend.Name, StringComparison.OrdinalIgnoreCase);
                if (exit != 0 && !expectedVersionExit) throw new Exception("CLI exit " + exit + "; see " + log);
                Console.WriteLine("PASS " + backend.Name + " pinned archive integrity, extraction and CLI");
                results.Add(new { backend = backend.Id, archive = release.Sha256, exit, passed = true });
                if (backend.Id == "pcsx2")
                {
                    // Exercise the same portable root as actual launch. The prior test
                    // reread its own untouched file while PCSX2 initialized elsewhere.
                    Directory.CreateDirectory(Path.Combine(cwd,"inis"));
                    File.WriteAllText(Path.Combine(cwd,"portable.txt"), "");
                    File.WriteAllText(Path.Combine(cwd,"inis","PCSX2.ini"), "[UI]\nSettingsVersion=1\nSetupWizardIncomplete=false\n[EmuCore/GS]\nupscale_multiplier=1\n");
                    var code = await LaunchService.RunUtilityAsync(executable, cwd, ["-portable", "-testconfig"], paths.Managed("logs","pcsx2-config.log"),TimeSpan.FromSeconds(20),default);
                    if (code != 0 || !Directory.Exists(Path.Combine(cwd,"logs")) || Directory.Exists(Path.Combine(cwd,"PCSX2","inis")))
                        throw new Exception("PCSX2 did not initialize the intended portable configuration root");
                    Console.WriteLine("PASS PCSX2 generated configuration initialization");
                }
                if (backend.Id == "rpcs3")
                {
                    var pup = Path.Combine(project,"work","PS3UPDAT.PUP");
                    var imported = await new FirmwareService(paths,catalog).ImportAsync(pup); state.Firmware.Add(imported);
                    Directory.CreateDirectory(Path.Combine(cwd,"log"));
                    var code = await LaunchService.RunUtilityAsync(executable,cwd,["--headless","--installfw",paths.Resolve(imported.Path)],paths.Managed("logs","rpcs3-firmware.log"),TimeSpan.FromMinutes(4),default);
                    if (!LaunchService.FirmwareInstallSucceeded(cwd,code)) throw new Exception("RPCS3 firmware installation failed: "+code);
                    Console.WriteLine("PASS official Sony firmware hash, managed import and RPCS3 headless install");
                    results.Add(new {backend=backend.Id,check="firmware installation",exit=code,passed=true,teardownWorkaround=code!=0});
                }
            }
            catch (Exception ex) { failures++; Console.WriteLine("FAIL " + backend.Name + " " + ex.Message); results.Add(new { backend = backend.Id, passed = false, error = ex.ToString() }); }
        }
        new StateStore(paths).Save(state);
        JsonData.WriteAtomic(Path.Combine(project,"work","integration-results.json"),new { sandbox, failures, results, gameplayTested = false });
        return failures == 0 ? 0 : 1;
    }
}
