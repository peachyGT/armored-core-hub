using System.Text.RegularExpressions;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Hub.Core;

// BIOS containers are small, bounded, one-operation staging areas. Never execute their
// contents or publish a whole archive; FirmwareService stores only its selected candidate.
internal static class FirmwareArchive
{
    internal const long MaximumSourceBytes=300L*1024*1024;
    internal const long MaximumExpandedBytes=512L*1024*1024;
    internal static bool IsArchive(string path)=>Path.GetExtension(path).ToLowerInvariant() is ".zip" or ".7z";
    internal static async Task<FirmwareArchiveBatch> PrepareAsync(HubPaths paths,string source,IProgress<ProgressInfo>? progress,CancellationToken ct)
    {
        var stage=paths.Managed("cache","firmware-imports","stage-"+Guid.NewGuid().ToString("N"));
        try
        {
            SafeFiles.NoLinks(source);var info=new FileInfo(source);
            if(info.Length is <8 or >MaximumSourceBytes)throw new HubException("Choose a BIOS ZIP or 7z smaller than 300 MB.");
            using var immutable=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read);
            using var archive=ArchiveFactory.OpenArchive(immutable);
            if(archive.Type is not (ArchiveType.Zip or ArchiveType.SevenZip))throw new HubException("Choose a ZIP or 7z archive for system files.");
            var entries=archive.Entries.Take(513).ToList();if(entries.Count>512)throw new HubException("The system-file archive has too many entries (limit: 512).");
            var expected=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);long total=0;var files=new List<string>();var fileCount=0;
            foreach(var entry in entries)
            {
                ct.ThrowIfCancellationRequested();var name=Validate(entry);
                if(!expected.TryAdd(name,entry.Size))throw new HubException("Duplicate system-file archive paths are not permitted.");
                if(entry.Size<0||entry.Size>MaximumExpandedBytes-total)throw new HubException("The system-file archive expands beyond 512 MB.");
                total+=entry.Size;if(!entry.IsDirectory)fileCount++;
                _=SafeFiles.Within(stage,name);
            }
            Directory.CreateDirectory(stage);long completed=0;
            async Task ReadEntry(IEntry entry,Func<Stream> open)
            {
                ct.ThrowIfCancellationRequested();if(entry.IsDirectory)return;
                var name=Validate(entry);if(!expected.TryGetValue(name,out var size)||size!=entry.Size)throw new HubException("Archive metadata changed while reading system files.");
                var plausible=entry.Size is 524288 or 4194304 or 8388608||Path.GetExtension(name).Equals(".pup",StringComparison.OrdinalIgnoreCase)&&entry.Size is >=64 and <=MaximumSourceBytes;
                // ZIP can skip irrelevant members entirely; solid 7z must decode them once.
                if(!plausible&&!archive.IsSolid&&archive.Type!=ArchiveType.SevenZip)return;
                var target=SafeFiles.Within(stage,name);if(plausible)Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input=open();await using var output=plausible?new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None,131072,true):null;
                var buffer=new byte[131072];long count=0;int read;
                while((read=await input.ReadAsync(buffer,ct))>0)
                {
                    count+=read;if(count>size)throw new HubException("A system-file archive entry exceeded its declared size.");
                    if(output!=null)await output.WriteAsync(buffer.AsMemory(0,read),ct);
                    completed+=read;ct.ThrowIfCancellationRequested();
                }
                if(count!=size)throw new HubException("A system-file archive entry was truncated.");
                if(plausible)files.Add(target);progress?.Report(new("Checking archive system files",completed,total));
            }
            if(archive.IsSolid||archive.Type==ArchiveType.SevenZip)
            {
                using var reader=archive.ExtractAllEntries();while(reader.MoveToNextEntry())await ReadEntry(reader.Entry,()=>reader.OpenEntryStream());
            }
            else foreach(var entry in entries)await ReadEntry(entry,()=>entry.OpenEntryStream());
            ct.ThrowIfCancellationRequested();return new(paths,stage,files,fileCount);
        }
        catch(Exception ex)when(ex is not OperationCanceledException&&ex is not HubException)
        {
            SafeFiles.DeleteManagedTree(paths.Root,stage);
            throw new HubException("The system-file archive could not be read. Check that it is complete, unencrypted and a valid ZIP or 7z.",ex);
        }
        catch{SafeFiles.DeleteManagedTree(paths.Root,stage);throw;}
    }
    private static string Validate(IEntry entry)
    {
        static bool Unsafe(int? attributes){if(attributes is not int value)return false;var kind=(value>>16)&0xf000;return (value&(int)FileAttributes.ReparsePoint)!=0||kind is not (0 or 0x4000 or 0x8000);}
        if(entry.IsEncrypted||!string.IsNullOrEmpty(entry.LinkTarget)||Unsafe(entry.Attrib)||entry is SharpCompress.Common.SevenZip.SevenZipEntry seven&&Unsafe(seven.ExtendedAttrib))throw new HubException("Encrypted, linked or device entries are not permitted in system-file archives.");
        var name=(entry.Key??"").Replace('\\','/').TrimEnd('/');
        if(name.Length is <1 or >1024||name.Split('/').Length>32)throw new HubException("Unsafe system-file archive path.");
        foreach(var component in name.Split('/'))
            if(component.Length==0||component is "." or ".."||component.EndsWith('.')||component.EndsWith(' ')||component.Contains(':')||component.IndexOfAny(Path.GetInvalidFileNameChars())>=0||Regex.IsMatch(component.Split('.')[0],"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",RegexOptions.IgnoreCase))throw new HubException("Unsafe system-file archive path.");
        return name;
    }
}
internal sealed class FirmwareArchiveBatch(HubPaths paths,string stage,List<string> files,int count):IDisposable
{
    internal List<string> Files{get;}=files;
    internal int FileCount{get;}=count;
    public void Dispose()=>SafeFiles.DeleteManagedTree(paths.Root,stage);
}
