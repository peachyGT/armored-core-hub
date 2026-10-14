using System.Diagnostics;
using System.Security.Cryptography;

namespace Hub.Core;

// Only the pinned reduced 7-Zip executable is used. Never find an executable on PATH,
// and never launch anything from an archive. The managed reader owns the preflight.
internal static class NativeSevenZip
{
    internal const string ToolSha256 = "256feca8e274e5da655e2a284fabafd9f554365eb164862089dacd4e8276d282";

    internal static async Task<bool> TryExtractAsync(string source, string destination,
        IReadOnlyDictionary<string, long> expected, long total, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var executable = Path.Combine(AppContext.BaseDirectory, "third-party", "7zip", "7zr.exe");
        if (!File.Exists(executable)) return false;
        SafeFiles.NoLinks(executable);
        // Keep the executable immutable while it is verified and executed, as with the source archive.
        await using var tool = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (Convert.ToHexStringLower(await SHA256.HashDataAsync(tool, ct)) != ToolSha256)
            throw new HubException("The bundled archive tool changed. Reinstall the Hub; the unverified tool was not run.");
        if (Directory.EnumerateFileSystemEntries(destination).Any())
            throw new HubException("Archive staging must be empty before extraction.");
        var start = new ProcessStartInfo(executable) {
            // Windows cannot start a process with a >260-character current directory,
            // even though 7-Zip and .NET can write the long staging path passed via -o.
            WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        // Stop option/list-file parsing before the source, and use argument vectors instead of shell quoting.
        foreach (var argument in new[] { "x", "-y", "-bd", "-bb0", "-bso0", "-bse1", "-mmt=2",
            "-o" + Path.GetFullPath(destination), "--", Path.GetFullPath(source) }) start.ArgumentList.Add(argument);
        using var owned=new OwnedProcessGroup();
        using var process = Process.Start(start) ?? throw new HubException("The bundled archive tool could not start.");owned.Add(process);
        process.StandardInput.Close();
        var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        // Bound diagnostic memory: drain both pipes without retaining arbitrary archive names/errors.
        var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        try
        {
            progress?.Report(new("Unpacking " + Path.GetFileName(source) + " (native 7-Zip)", 0, total));
            var finished = process.WaitForExitAsync(ct);
            while (!finished.IsCompleted)
            {
                await Task.WhenAny(finished, Task.Delay(500, ct)); ct.ThrowIfCancellationRequested();
                var completed = InspectOutput(destination, expected, false);
                progress?.Report(new("Unpacking " + Path.GetFileName(source) + " (native 7-Zip)", completed, total));
                ct.ThrowIfCancellationRequested();
            }
            await finished; owned.Dispose();await Task.WhenAll(stdout, stderr);
            if (process.ExitCode != 0) throw new HubException("7-Zip could not unpack this archive. It may be damaged, incomplete or use an unsupported codec.");
            progress?.Report(new("Checking unpacked files " + Path.GetFileName(source)));
            ct.ThrowIfCancellationRequested();
            InspectOutput(destination, expected, true);
            return true;
        }
        finally
        {
            // Only this child belongs to the operation. Do not touch the user's archiver/emulator processes.
            if (!process.HasExited)
            {
                try { process.Kill(true); }
                catch (InvalidOperationException) { /* The owned process exited between the check and kill. */ }
                await process.WaitForExitAsync();
            }
            owned.Dispose();await Task.WhenAll(stdout, stderr);
        }
    }

    private static long InspectOutput(string destination, IReadOnlyDictionary<string, long> expected, bool complete)
    {
        long bytes = 0; var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>(); pending.Push(destination); var count = 0;
        while (pending.Count > 0)
        {
            foreach (var file in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                if (++count > 50000) throw new HubException("The archive tool produced too many entries.");
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new HubException("Linked archive output is not permitted.");
                if ((attributes & FileAttributes.Directory) != 0) { pending.Push(file); continue; }
                if (!expected.TryGetValue(file, out var size)) throw new HubException("The archive tool produced an unexpected file.");
                var actual = new FileInfo(file).Length;
                if (actual > size || (complete && actual != size)) throw new HubException("An unpacked file differs from its declared size.");
                bytes = checked(bytes + actual); found.Add(file);
            }
        }
        if (complete && found.Count != expected.Count) throw new HubException("The archive tool did not unpack every file.");
        return bytes;
    }
}
