using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Hub.Core;

// The game/provider supplies the image. Cache only bounded PNG objects, never
// arbitrary paths or URLs embedded in a profile or achievement description.
public sealed class AchievementIconStore(HubPaths paths)
{
    public const int MaxBytes=4*1024*1024;
    public static (int Width,int Height) ValidatePng(byte[] bytes)
    {
        if(bytes.Length is <45 or >MaxBytes||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})
            ||BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8))!=13||!bytes.AsSpan(12,4).SequenceEqual("IHDR"u8))throw new HubException("Achievement artwork is not a bounded PNG image.");
        var width=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16));var height=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20));
        if(width is <1 or >1024||height is <1 or >1024||width*(ulong)height>1024*1024)throw new HubException("Achievement artwork dimensions exceed their limit.");
        var hasData=false;var ended=false;
        for(var p=8;p<bytes.Length;)
        {
            if(p>bytes.Length-12)throw new HubException("Achievement artwork contains a truncated PNG chunk.");
            var size=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(p));if(size>(ulong)(bytes.Length-p-12))throw new HubException("Achievement artwork contains a truncated PNG chunk.");
            var type=bytes.AsSpan(p+4,4);if(type.SequenceEqual("IDAT"u8))hasData=true;
            if(type.SequenceEqual("IEND"u8)){if(size!=0||p+12!=bytes.Length)throw new HubException("Achievement artwork contains an invalid PNG ending.");ended=true;break;}
            p+=checked((int)size+12);
        }
        if(!hasData||!ended)throw new HubException("Achievement artwork has no image data.");
        return (checked((int)width),checked((int)height));
    }
    public string Cache(byte[] bytes)
    {
        ValidatePng(bytes);var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));var relative="achievements/assets/"+hash+".png";var destination=paths.Managed(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if(File.Exists(destination)&&new FileInfo(destination).Length==bytes.Length&&File.ReadAllBytes(destination).AsSpan().SequenceEqual(bytes))return relative;
        var temp=destination+"."+Guid.NewGuid().ToString("N")+".part";
        try{File.WriteAllBytes(temp,bytes);File.Move(temp,destination,true);}finally{if(File.Exists(temp))File.Delete(temp);}
        return relative;
    }
    public string? TryPath(string? relative)
    {
        if(relative==null||!Regex.IsMatch(relative,@"^achievements/assets/[a-f0-9]{64}\.png$",RegexOptions.CultureInvariant))return null;
        try
        {
            var file=paths.Managed(relative);if(!File.Exists(file)||new FileInfo(file).Length>MaxBytes)return null;
            var bytes=File.ReadAllBytes(file);ValidatePng(bytes);
            if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=Path.GetFileNameWithoutExtension(file))return null;
            return file;
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or HubException){return null;}
    }
    public static string? BadgeUri(string? badge)=>badge!=null&&Regex.IsMatch(badge,@"^[0-9]{1,10}$",RegexOptions.CultureInvariant)?"https://media.retroachievements.org/Badge/"+badge+".png":null;
}

// TRP is an uncompressed trophy bundle. Header/entry layout follows RPCS3's
// Loader/TRP.h; only metadata and trophy PNGs are returned, never arbitrary files.
public static class TrophyArchive
{
    public const int MaxBytes=16*1024*1024;
    public static Dictionary<string,byte[]> Read(byte[] bytes)
    {
        if(bytes.Length is <64 or >MaxBytes||BinaryPrimitives.ReadUInt32BigEndian(bytes)!=0xDCA24D00)throw new HubException("Unrecognized trophy archive.");
        var version=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4));var length=BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(8));
        var count=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16));var entrySize=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20));
        if(version is <1 or >3||length!=(ulong)bytes.Length||count>2048||entrySize!=64||64UL+count*64UL>length)throw new HubException("Invalid trophy archive table.");
        if(version>=2)
        {
            var verify=(byte[])bytes.Clone();Array.Clear(verify,28,20);
            if(!SHA1.HashData(verify).AsSpan().SequenceEqual(bytes.AsSpan(28,20)))throw new HubException("Trophy archive checksum does not match.");
        }
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var result=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);var ranges=new List<(ulong Start,ulong End)>();
        for(var i=0;i<count;i++)
        {
            var p=64+checked((int)i)*64;var raw=bytes.AsSpan(p,32);var end=raw.IndexOf((byte)0);if(end<1)throw new HubException("Invalid trophy archive filename.");
            var name=Encoding.ASCII.GetString(raw[..end]);
            if(!Regex.IsMatch(name,@"^[A-Za-z0-9_.-]{1,31}$",RegexOptions.CultureInvariant)||name.Contains("..",StringComparison.Ordinal)||!seen.Add(name))throw new HubException("Unsafe or duplicate trophy archive filename.");
            var offset=BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(p+32));var size=BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(p+40));
            if(offset<64UL+count*64UL||offset>length||size>length-offset)throw new HubException("Trophy archive entry is outside the file.");
            ranges.Add((offset,offset+size));
            var supported=Regex.IsMatch(name,@"^(TROPCONF|TROP|TROP[0-9]{2})\.SFM$|^TROP[0-9]{3}\.PNG$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            if(supported){if(size>AchievementIconStore.MaxBytes)throw new HubException("Trophy archive entry exceeds its size limit.");result[name]=bytes.AsSpan(checked((int)offset),checked((int)size)).ToArray();}
        }
        var ordered=ranges.Where(r=>r.Start!=r.End).OrderBy(r=>r.Start).ToArray();for(var i=1;i<ordered.Length;i++)if(ordered[i].Start<ordered[i-1].End)throw new HubException("Trophy archive entries overlap.");
        return result;
    }
}
