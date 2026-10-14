using System.Buffers.Binary;
using System.Text;

namespace Hub.Core;

public sealed record DiscMovieFile(string Name, long Offset, long Length);

// A read-only directory inventory for presentation media. No mounting, decryption,
// executable loading or full-disc extraction. Only validated contiguous extents are exposed.
public static class DiscMovieReader
{
    public static IReadOnlyList<DiscMovieFile> Inventory(string file, CancellationToken ct = default)
    {
        SafeFiles.NoLinks(file); using var input = File.OpenRead(file);
        var files = new List<DiscMovieFile>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new HashSet<long>(); long metadata = 0; var count = 0;
        byte[] Read(long offset, long length)
        {
            ct.ThrowIfCancellationRequested();
            if (offset < 0 || length < 0 || length > 16 * 1024 * 1024 || offset > input.Length - length)
                throw new HubException("The disc presentation directory is incomplete or oversized.");
            var bytes = new byte[(int)length]; input.Position = offset; input.ReadExactly(bytes); return bytes;
        }
        void CheckExtent(long offset, long size)
        { if (offset < 0 || size < 0 || offset > input.Length - size) throw new HubException("The disc presentation file extends outside the image."); }
        string Name(byte[] bytes, bool unicode)
        {
            if (unicode && bytes.Length % 2 != 0) throw new HubException("Invalid extended disc filename.");
            var name = (unicode ? Encoding.BigEndianUnicode : Encoding.ASCII).GetString(bytes).Split(';')[0];
            if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(['/', '\\', ':', '\0']) >= 0 || name.Any(char.IsControl))
                throw new HubException("Unsafe disc presentation filename.");
            return name;
        }
        void Add(string name, long offset, long size)
        {
            CheckExtent(offset,size);
            if (!names.Add(name)) throw new HubException("Duplicate disc presentation paths.");
            files.Add(new(name,offset,size));
        }
        void CheckDirectory(long offset, long size, int depth)
        {
            if (depth > 24 || ++count > 50000 || !directories.Add(offset) || (metadata += size) > 64 * 1024 * 1024)
                throw new HubException("The disc presentation directory exceeds its traversal limits.");
        }
        void Iso(byte[] record, string parent, bool unicode, int depth)
        {
            ct.ThrowIfCancellationRequested();
            if (record.Length < 34 || record[1] != 0 || record[26] != 0 || record[27] != 0 || (record[25] & 128) != 0)
                throw new HubException("Unsupported disc presentation layout.");
            long offset=BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(2))*2048L;
            long size=BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(10));
            CheckExtent(offset,size);
            if ((record[25]&2)==0) { if (++count>50000) throw new HubException("Too many disc presentation files."); Add(parent,offset,size); return; }
            CheckDirectory(offset,size,depth); var data=Read(offset,size);
            for(var p=0;p<data.Length;)
            {
                var length=data[p]; if(length==0){p=(p/2048+1)*2048;continue;}
                if(length<34||p+length>data.Length||data[p+32]>length-33)throw new HubException("Damaged disc presentation directory.");
                var item=data.AsSpan(p,length).ToArray();p+=length;
                if(item[32]==1&&item[33]<=1)continue;
                var name=Name(item.AsSpan(33,item[32]).ToArray(),unicode);
                Iso(item,parent.Length==0?name:parent+"/"+name,unicode,depth+1);
            }
        }
        void Xbox(long partition, long offset, long size, string parent, int depth)
        {
            CheckDirectory(offset,size,depth);var data=Read(offset,size);
            var pending=new Stack<int>();pending.Push(0);var visited=new HashSet<int>();
            while(pending.TryPop(out var p))
            {
                ct.ThrowIfCancellationRequested();
                if(!visited.Add(p)||p<0||p>data.Length-14||++count>50000)throw new HubException("Invalid Xbox presentation directory tree.");
                var item=data.AsSpan(p);var length=item[13];
                if(length==0||length>item.Length-14)throw new HubException("Invalid Xbox presentation filename.");
                var left=BinaryPrimitives.ReadUInt16LittleEndian(item)*4;var right=BinaryPrimitives.ReadUInt16LittleEndian(item[2..])*4;
                if(left!=0)pending.Push(left);if(right!=0)pending.Push(right);
                var name=Name(item.Slice(14,length).ToArray(),false);var relative=parent.Length==0?name:parent+"/"+name;
                var start=partition+BinaryPrimitives.ReadUInt32LittleEndian(item[4..])*2048L;var lengthBytes=BinaryPrimitives.ReadUInt32LittleEndian(item[8..]);
                if((item[12]&0x10)!=0){if(lengthBytes>0)Xbox(partition,start,lengthBytes,relative,depth+1);}
                else Add(relative,start,lengthBytes);
            }
        }
        // Xbox images can also contain an ISO9660 video partition. Prefer the game
        // partition before that unrelated DVD view, exactly as the emulator does.
        foreach(var partition in new long[]{0,0xfb20,0x20600,0x2080000,0xfd90000})
        {
            var location=partition+32*2048;if(location+28>input.Length)continue;var header=Read(location,28);
            if(!header.AsSpan(0,20).SequenceEqual("MICROSOFT*XBOX*MEDIA"u8))continue;
            var offset=partition+BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(20))*2048L;
            var size=BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(24));
            if(size<14)throw new HubException("Invalid Xbox presentation root.");
            Xbox(partition,offset,size,"",0);return files;
        }
        if(input.Length>=17*2048)
        {
            var descriptor=Read(16*2048,2048);
            if(descriptor.AsSpan(1,5).SequenceEqual("CD001"u8))
            {
                var unicode=false;
                for(var sector=17;sector<80&&sector*2048L+2048<=input.Length;sector++)
                {
                    var next=Read(sector*2048L,2048);if(!next.AsSpan(1,5).SequenceEqual("CD001"u8)||next[0]==255)break;
                    if(next[0]==2&&next[88]=='%'&&next[89]=='/'&&next[90] is 0x40 or 0x43 or 0x45){descriptor=next;unicode=true;break;}
                }
                if(descriptor[156]<34||156+descriptor[156]>2048)throw new HubException("Invalid disc root directory.");
                Iso(descriptor.AsSpan(156,descriptor[156]).ToArray(),"",unicode,0);return files;
            }
        }
        return files;
    }
}
