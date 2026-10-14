using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Hub.Core;

public sealed partial class LaunchService(HubPaths paths, Catalog catalog)
{
    private readonly SemaphoreSlim sessionGate = new(1, 1);
    public bool IsRunning { get; private set; }
    // Zero outside an owned session; callers must never discover ownership by process name.
    public int ActiveProcessId { get; private set; }
    public event Action<string>? StatusChanged;
    public async Task PrepareAsync(LaunchPlan plan, HubState state, CancellationToken ct = default,bool includeAchievementSecrets=false)
    {
        var build = state.Builds.Single(x => x.Id == plan.BuildId); var source = paths.Resolve(build.Path);
        if (await SafeFiles.HashAsync(Path.Combine(source, build.Executable), ct) != build.ExecutableSha256) throw new HubException("The runtime executable has changed. Reinstall this build before launching.");
        var home = paths.Managed("runtime", "sessions", plan.EditionId, build.Id);
        Directory.CreateDirectory(home);
        var receipt = SafeFiles.Within(home, "hub-session-build.txt");
        var identity = build.ArchiveSha256 + "/" + build.InstalledAt.ToString("O");
        var refreshAssets = !File.Exists(receipt) || await File.ReadAllTextAsync(receipt, ct) != identity;
        foreach (var file in SafeFiles.Files(source))
        {
            ct.ThrowIfCancellationRequested(); var relative = Path.GetRelativePath(source, file);
            if (relative == "hub-build.json") continue;
            var dest = SafeFiles.Within(home, relative);
            if (File.Exists(dest))
            {
                var executable = string.Equals(dest, plan.Executable, StringComparison.OrdinalIgnoreCase);
                if (!refreshAssets && (!executable || await SafeFiles.HashAsync(dest, ct) == build.ExecutableSha256)) continue;
                File.Delete(dest);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            // An imported archive can include mutable settings and caches. Only code
            // assets are shared; native writes to other session files must stay private.
            var code=Path.GetExtension(file).ToLowerInvariant() is ".exe" or ".dll" or ".pdb";
            if (!code || !OperatingSystem.IsWindows() || !CreateHardLink(dest, file, IntPtr.Zero)) File.Copy(file, dest);
        }
        await File.WriteAllTextAsync(receipt, identity, ct);
        Directory.CreateDirectory(plan.SaveDirectory);
        var edition = catalog.Editions.Single(x => x.Id == plan.EditionId);
        var xboxPlayer=build.BackendId=="xenia"?await XeniaProfiles.EnsureLocalAsync(paths,plan.SaveDirectory,Path.Combine(plan.WorkingDirectory,"xenia-canary.config.toml"),ct):null;
        foreach (var (file, contents) in plan.ConfigFiles)
        {
            if (!SafeFiles.IsWithin(home, file)) throw new HubException("Configuration target escaped the runtime session.");
            SafeFiles.NoLinks(file); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var merged=EmulatorAccounts.ConfigFile(build.BackendId) is string config && Path.GetFullPath(file)==Path.GetFullPath(Path.Combine(plan.WorkingDirectory,config)) ? NativeAchievementAccounts.MergeConfig(paths,build.BackendId,edition,contents,includeAchievementSecrets) : contents;
            if(xboxPlayer!=null&&Path.GetFileName(file)=="xenia-canary.config.toml")merged=XeniaProfiles.ApplyLocal(merged,xboxPlayer);
            await WriteSessionConfigAsync(file,merged,ct);
        }
        if(includeAchievementSecrets)NativeAchievementAccounts.WriteSessionSecrets(paths,build.BackendId,plan.WorkingDirectory,plan.SaveDirectory);
        else NativeAchievementAccounts.CleanSessionSecrets(paths,build.BackendId,plan.WorkingDirectory,plan.SaveDirectory);
        if (edition.Platform == "ps3")
        {
            // RPCS3 checks custom mounts before boot and does not create a missing mount root.
            var user = SafeFiles.Within(plan.SaveDirectory, "dev_hdd0/home/00000001");
            Directory.CreateDirectory(user);
            var username = Path.Combine(user, "localusername");
            if (!File.Exists(username)) await File.WriteAllTextAsync(username, "Hub", ct);
        }
        if (edition.Platform == "ps2")
        {
            var card = Path.Combine(plan.SaveDirectory, "shared.ps2");
            if (!File.Exists(card))
            {
                // A blank raw 8MB+ECC PS2 card, formatted by the BIOS/game on first use.
                var blank = new byte[528 * 16]; Array.Fill(blank, (byte)0xFF);
                var partial = card + "." + Guid.NewGuid().ToString("N") + ".partial";
                try
                {
                    await using (var stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write))
                        for (var i=0;i<1024;i++) await stream.WriteAsync(blank, ct);
                    File.Move(partial, card);
                }
                finally { if (File.Exists(partial)) File.Delete(partial); }
            }
        }
        if (edition.Platform == "ps3" && !FirmwareInstallSucceeded(plan.WorkingDirectory,0))
        {
            var backend=catalog.Backends.Single(b=>b.Id==build.BackendId);
            var firmware=LaunchPlanner.PickFirmware(backend,edition,state,f=>File.Exists(paths.Resolve(f.Path)))??throw new HubException("Import PS3 system software first.");
            StatusChanged?.Invoke("Installing official PS3 system software into this runtime…");
            Directory.CreateDirectory(Path.Combine(plan.WorkingDirectory, "log"));
            var emulatorLog = Path.Combine(plan.WorkingDirectory, "log", "RPCS3.log");
            if (File.Exists(emulatorLog)) File.Move(emulatorLog, emulatorLog + ".previous", true);
            var exit = await RunUtilityAsync(plan.Executable, plan.WorkingDirectory, ["--headless", "--installfw", paths.Resolve(firmware.Path)], paths.Managed("logs", "firmware-install.log"), TimeSpan.FromMinutes(5), ct);
            if (!FirmwareInstallSucceeded(plan.WorkingDirectory, exit)) throw new HubException("PS3 system software installation did not complete. See the firmware log and try Repair.");
            if (exit != 0) await File.AppendAllTextAsync(paths.Managed("logs", "firmware-install.log"), "\nRPCS3 confirmed installation, then hit the documented 0xC0000409 headless teardown error. Fresh success log and system files verified.\n", ct);
        }
    }
    private static async Task WriteSessionConfigAsync(string file,string contents,CancellationToken ct)
    {
        var partial=file+"."+Guid.NewGuid().ToString("N")+".partial";
        try
        {
            await File.WriteAllTextAsync(partial,contents,ct);ct.ThrowIfCancellationRequested();
            // Replacing the directory entry severs any previous hardlink without
            // truncating upstream bytes, and cancellation retains the old config.
            File.Move(partial,file,true);
        }
        finally{if(File.Exists(partial))File.Delete(partial);}
    }
    public async Task<int> LaunchAsync(LaunchPlan plan, HubState state, CancellationToken ct = default)
    {
        if (!await sessionGate.WaitAsync(0, ct)) throw new HubException("A game is already running. Close it before starting another session.");
        var helpers = new List<Process>(); var log = paths.Managed("logs", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + plan.GameId + ".log");
        var extensionsStarted = false;
        try
        {
            using var ownedProcesses=new OwnedProcessGroup();
            IsRunning = true; StatusChanged?.Invoke("Preparing " + catalog.Games.Single(x => x.Id == plan.GameId).Title);
            var edition = catalog.Editions.Single(x => x.Id == plan.EditionId);
            var media = state.Media.Single(x => x.Id == plan.MediaId); var mediaPath = paths.Resolve(media.Path);
            // Cheap launch-time change detection; full verification remains a separate explicit action.
            var root = Directory.Exists(mediaPath) ? mediaPath : Path.GetDirectoryName(mediaPath)!;
            foreach (var file in media.Files)
            {
                var path=SafeFiles.Within(root,file.RelativeName);var info=new FileInfo(path);
                if (!info.Exists || info.Length != file.Size || !media.Managed && info.LastWriteTimeUtc.Ticks != file.LastWriteUtcTicks)
                    throw new HubException("Media changed since import. Verify or re-import it before launching.");
            }
            await new SaveService(paths).BackupAsync(edition.SaveFamily, edition.GameId, state.Builds.Single(x=>x.Id==plan.BuildId).BackendId, "Before launch", ct);
            await PrepareAsync(plan, state, ct,true);
            JsonData.WriteAtomic(paths.Managed("logs", "last-launch.json"), plan);
            await File.WriteAllTextAsync(log, $"Hub {typeof(LaunchService).Assembly.GetName().Version?.ToString(3)} | catalog {catalog.Version}\nProfile {plan.ProfileId} | build {plan.BuildId}\nExecutable: {plan.Executable}\nWorking directory: {plan.WorkingDirectory}\nArguments: {System.Text.Json.JsonSerializer.Serialize(plan.Arguments)}\n", ct);
            extensionsStarted = true; await StartExtensions("PreLaunch", plan, helpers, ct,ownedProcesses);
            using var process = new Process {StartInfo = ProcessInfo(plan.Executable,plan.WorkingDirectory,plan.Arguments)};
            foreach(var (key,value) in plan.Environment)process.StartInfo.Environment[key]=value;
            try { if(!process.Start())throw new HubException("The runtime did not start."); }
            catch(Exception e) when(e is System.ComponentModel.Win32Exception or InvalidOperationException){throw new HubException("The emulator could not start. Open diagnostics for the executable and configuration details.",e);}
            ActiveProcessId = process.Id;
            ownedProcesses.Add(process);
            PlaytimeSession? playtime=null;
            try{playtime=new PlaytimeService(paths,catalog).Start(edition,state,_=>StatusChanged?.Invoke("Playtime could not be saved. Check config/playtime and available disk space."));}
            catch(Exception ex)when(ex is HubException or IOException or UnauthorizedAccessException){await File.AppendAllTextAsync(log,"Playtime unavailable; existing history preserved.\n",CancellationToken.None);}
            var stdout = DrainAsync(process.StandardOutput, log + ".stdout", CancellationToken.None);
            var stderr = DrainAsync(process.StandardError, log + ".stderr", CancellationToken.None);
            try
            {
                await StartExtensions("PostLaunch",plan,helpers,ct,ownedProcesses);await StartExtensions("RuntimeExtension",plan,helpers,ct,ownedProcesses);
                StatusChanged?.Invoke("Playing · " + catalog.Games.Single(x=>x.Id==plan.GameId).Title);
                var selected=LaunchPlanner.Selection(edition,state);
                await GameWindowSize.ApplyAsync(process,selected.Graphics,selected.Fullscreen,ct);
                await process.WaitForExitAsync(ct);
            }
            catch
            {
                if(!process.HasExited)
                {
                    await StopOwnedAsync(process,TimeSpan.FromSeconds(3));
                }
                throw;
            }
            finally
            {
                if(playtime!=null)await playtime.DisposeAsync();
                // End descendants before draining pipes: a child can retain a pipe
                // even after the emulator has exited. This also owns abrupt Hub exits.
                ownedProcesses.Dispose();
                await Task.WhenAll(stdout,stderr);
                // Cancellation also ends a real session. Preserve its final save after the
                // process releases files, even though the caller's token is now cancelled.
                if(process.HasExited)
                {
                    await File.AppendAllTextAsync(log,"Exit code: "+process.ExitCode+"\n",CancellationToken.None);
                    NativeAchievementAccounts.CleanSessionSecrets(paths,state.Builds.Single(x=>x.Id==plan.BuildId).BackendId,plan.WorkingDirectory,plan.SaveDirectory);
                    await new SaveService(paths).BackupAsync(edition.SaveFamily, edition.GameId, state.Builds.Single(x=>x.Id==plan.BuildId).BackendId, "After session", CancellationToken.None);
                    try
                    {
                        var achievements=new AchievementService(paths);var backend=state.Builds.Single(x=>x.Id==plan.BuildId).BackendId;
                        if(!ct.IsCancellationRequested&&AchievementSupport.NativeRetroBackend(backend)&&!state.Offline&&AchievementPreferences.Load(paths).Tracking(edition)&&AchievementSupport.Get(edition).HasPublishedSet&&NativeAchievementAccounts.Load(paths)!=null)
                        {using var refresh=new CancellationTokenSource(TimeSpan.FromSeconds(30));await achievements.SyncNativeAsync(edition,false,refresh.Token);}
                        else await Task.Run(()=>achievements.ReadLocal(edition,backend,mediaPath));
                    }
                    catch(Exception e) when(e is IOException or HubException or System.Xml.XmlException or FormatException or UnauthorizedAccessException or OperationCanceledException)
                    { await File.AppendAllTextAsync(log,"Achievement refresh unavailable; previous snapshot retained.\n",CancellationToken.None); }
                }
            }
            if(process.ExitCode!=0)throw new HubException("The emulator stopped with exit code "+process.ExitCode+". Open logs for details; your previous build remains available.");
            StatusChanged?.Invoke("Session ended · saves retained");return process.ExitCode;
        }
        finally
        {
            try
            {
                if (extensionsStarted)
                {
                    try { await StartExtensions("Shutdown",plan,helpers,CancellationToken.None); }
                    catch(Exception e) { await File.AppendAllTextAsync(log, "Shutdown extension failed: " + e.Message + "\n"); }
                }
            }
            finally
            {
                try
                {
                    foreach(var helper in helpers)
                    {
                        try { if(!helper.HasExited)await StopOwnedAsync(helper,TimeSpan.FromSeconds(2)); }
                        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                        finally { helper.Dispose(); }
                    }
                }
                finally
                {
                    try{NativeAchievementAccounts.CleanSessionSecrets(paths,state.Builds.Single(x=>x.Id==plan.BuildId).BackendId,plan.WorkingDirectory,plan.SaveDirectory);}
                    finally{ActiveProcessId=0;IsRunning=false;sessionGate.Release();}
                }
            }
        }
    }
    private async Task StartExtensions(string phase,LaunchPlan plan,List<Process> owned,CancellationToken ct,OwnedProcessGroup? group=null)
    {
        foreach(var module in plan.Extensions.Where(x=>x.Phase==phase))
        {
            if(module.Executable==null||module.Sha256==null)throw new HubException("Input extension executable is not installed.");
            var exe=paths.Managed("extensions",SafeFiles.Id(module.Id),module.Executable);
            if(!File.Exists(exe)||await SafeFiles.HashAsync(exe,ct)!=module.Sha256)throw new HubException("Input extension integrity check failed.");
            if (phase == "Shutdown")
            {
                var exit = await RunUtilityAsync(exe,Path.GetDirectoryName(exe)!,module.Arguments,paths.Managed("logs",module.Id+"-shutdown.log"),TimeSpan.FromSeconds(15),ct);
                if (exit != 0) throw new HubException("Input extension shutdown returned " + exit);
                continue;
            }
            var info = ProcessInfo(exe,Path.GetDirectoryName(exe)!,module.Arguments);
            // Do not redirect helpers without a consumer: a full pipe can deadlock their runtime hooks.
            info.RedirectStandardOutput = false; info.RedirectStandardError = false;
            var process=Process.Start(info)??throw new HubException("Input extension failed to start.");owned.Add(process);group?.Add(process);
            // Helpers are isolated processes. Runtime hooks are real extension capabilities, never stick emulation.
        }
    }
    private static async Task StopOwnedAsync(Process process,TimeSpan grace)
    {
        if(process.HasExited)return;
        process.CloseMainWindow();
        try{await process.WaitForExitAsync(CancellationToken.None).WaitAsync(grace);}
        catch(TimeoutException){if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync(CancellationToken.None);}
    }
    public static ProcessStartInfo ProcessInfo(string exe,string cwd,IEnumerable<string> args)
    {
        var result=new ProcessStartInfo(exe){WorkingDirectory=cwd,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        foreach(var arg in args)result.ArgumentList.Add(arg);return result;
    }
    public static bool FirmwareInstallSucceeded(string home, int exit)
    {
        var complete = File.Exists(Path.Combine(home, "dev_flash", "vsh", "module", "vsh.self"))
            && File.Exists(Path.Combine(home, "dev_flash", "sys", "external", "liblv2.sprx"));
        if (!complete) return false;
        if (exit == 0) return true;
        var log = Path.Combine(home,"log","RPCS3.log");
        return exit == unchecked((int)0xC0000409) && File.Exists(log)
            && File.ReadAllText(log).Contains("Successfully installed PS3 firmware version ", StringComparison.Ordinal);
    }
    public static async Task<int> RunUtilityAsync(string exe,string cwd,IEnumerable<string> args,string log,TimeSpan timeout,CancellationToken ct)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct);linked.CancelAfter(timeout);
        using var owned=new OwnedProcessGroup();
        using var process=Process.Start(ProcessInfo(exe,cwd,args))??throw new HubException("The helper process could not start.");
        owned.Add(process);
        var a=DrainAsync(process.StandardOutput,log,CancellationToken.None);var b=DrainAsync(process.StandardError,log+".stderr",CancellationToken.None);
        try{await process.WaitForExitAsync(linked.Token);}catch{if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync(CancellationToken.None);throw;}finally{owned.Dispose();await Task.WhenAll(a,b);}
        return process.ExitCode;
    }
    private static async Task DrainAsync(StreamReader input,string file,CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);await using var output=new StreamWriter(file,false,Encoding.UTF8);var buffer=new char[8192];long total=0;int read;
        while((read=await input.ReadAsync(buffer,ct))>0){if(total<4*1024*1024)await output.WriteAsync(buffer.AsMemory(0,read),ct);total+=read;}
        if(total>=4*1024*1024)await output.WriteLineAsync("[Log truncated at 4 MiB]");
    }
    public string RedactedDiagnostics(LaunchPlan? plan,IEnumerable<Diagnostic> diagnostics)
    {
        var text=$"Armored Core Hub {typeof(LaunchService).Assembly.GetName().Version?.ToString(3)} | catalog {catalog.Version}\n"+string.Join('\n',diagnostics.Select(x=>$"{x.Name}: {x.Status} — {x.Message}"));
        if(plan!=null)text+=$"\nEdition {plan.EditionId}\nProfile {plan.ProfileId}\nBuild {plan.BuildId}\n";
        return Regex.Replace(text,@"[A-Za-z]:[\\/][^\r\n]*","[private path omitted]");
    }
    [LibraryImport("kernel32.dll",EntryPoint="CreateHardLinkW",StringMarshalling=StringMarshalling.Utf16,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)] private static partial bool CreateHardLink(string newName,string existingName,IntPtr security);
}
