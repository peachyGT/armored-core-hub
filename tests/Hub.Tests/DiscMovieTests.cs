using System.Buffers.Binary;
using System.Text;
using Hub.Core;

static class DiscMovieTests
{
    public static void Register(Action<string,Action> test,string scratch)
    {
        void Check(bool ok){if(!ok)throw new Exception("Disc movie assertion failed");}
        test("PS2 opening movie is found by ISO extents without extracting the disc",()=>{
            var source=Path.Combine(scratch,"movie.iso");var bytes=Iso();File.WriteAllBytes(source,bytes);
            var before=File.ReadAllBytes(source);var movie=GameMovieDiscovery.Find(source)!;
            Check(movie.Name=="MOVIE/AC2OP01.PSS"&&movie.Offset==22*2048&&movie.Length==2048&&movie.Format=="mpeg");
            Check(movie.InputUrl().StartsWith("subfile,,start,45056,end,47104,,:file:"));Check(File.ReadAllBytes(source).SequenceEqual(before));
            Check(!GameMovieDiscovery.Rank("MOVIE/ENDE.PSS").Equals(0));
        });
        test("Disc presentation inventory rejects cycles, unsafe names and truncated extents",()=>{
            foreach(var kind in new[]{"cycle","name","extent"})
            {
                var bytes=Iso();
                if(kind=="cycle")BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20*2048+2),20);
                if(kind=="name")bytes[21*2048+33]=(byte)'/';
                if(kind=="extent")BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(21*2048+10),uint.MaxValue);
                var source=Path.Combine(scratch,"bad-movie-"+kind+".iso");File.WriteAllBytes(source,bytes);
                try{DiscMovieReader.Inventory(source);throw new Exception("Unsafe movie directory accepted");}catch(HubException){}
            }
        });
        test("Xbox game movie tree takes priority over an ISO9660 DVD video partition",()=>{
            var source=Path.Combine(scratch,"xbox-movie.iso");var bytes=Xbox();File.WriteAllBytes(source,bytes);
            var movie=GameMovieDiscovery.Find(source)!;
            Check(movie.Name=="movie/acv_opening.wmv"&&movie.Length==2048&&movie.Offset==0xfb20+70*2048&&movie.Format=="");
            Check(movie.InputUrl().Contains("subfile,,start,"));
        });
        test("Xbox movie trees reject cycles and extents outside the source",()=>{
            foreach(var kind in new[]{"cycle","extent"})
            {
                var bytes=Xbox();var node=0xfb20+60*2048;
                if(kind=="cycle")BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(node+4),60);
                if(kind=="extent")BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(node+8),uint.MaxValue);
                var source=Path.Combine(scratch,"bad-xbox-movie-"+kind+".iso");File.WriteAllBytes(source,bytes);
                try{DiscMovieReader.Inventory(source);throw new Exception("Unsafe Xbox directory accepted");}catch(HubException){}
            }
        });
        test("Opening selection understands console abbreviations and avoids endings and tutorials",()=>{
            Check(GameMovieDiscovery.Rank("MOVIE/AC2OP01.PSS")==0&&GameMovieDiscovery.Rank("MOVIE/O1.PSS")==0&&GameMovieDiscovery.Rank("movie/jp/acv_opening.wmv")==0);
            Check(GameMovieDiscovery.Rank("MOVIE/ES1.PSS")==int.MaxValue&&GameMovieDiscovery.Rank("MOVIE/KEYCONF/KEYM_001.PSS")==int.MaxValue&&GameMovieDiscovery.Rank("MOVIE/MISSION/intro.pss")==int.MaxValue);
            Check(GameMovieDiscovery.Format("intro.bik")=="bink"&&GameMovieDiscovery.Format("intro.str")=="psxstr");
            var movie=new GameMovieSource("unused","MOVIE/O1.PSS",0,150*1024*1024,"mpeg");
            Check(MediaPreviewService.PreviewStart("aclr-ps2-slus21338",movie)==55&&MediaPreviewService.PreviewStart("aclr-ps2-slus21338",movie with{Length=1024})==0);
        });
        test("Folder discovery reaches real game movie directories without borrowing companion neighbors",()=>{
            var root=Path.Combine(scratch,"movie-folder","PS3_GAME","USRDIR","movie","us");Directory.CreateDirectory(root);
            File.WriteAllBytes(Path.Combine(root,"opening.pam"),new byte[2048]);File.WriteAllBytes(Path.Combine(root,"ending.pam"),new byte[2048]);
            var source=Path.Combine(scratch,"movie-folder");var movie=GameMovieDiscovery.Find(source)!;
            Check(movie.Name.Replace('\\','/').EndsWith("movie/us/opening.pam")&&movie.Format=="mpeg");
            var empty=Path.Combine(scratch,"empty-movie-folder");Directory.CreateDirectory(empty);Check(GameMovieDiscovery.Find(empty)==null);
        });
        test("Movie source URL validation rejects out-of-bounds cached extents",()=>{
            var file=Path.Combine(scratch,"movie-url.bin");File.WriteAllBytes(file,new byte[32]);
            foreach(var movie in new[]{new GameMovieSource(file,"intro.pss",-1,3,"mpeg"),new(file,"intro.pss",0,33,"mpeg"),new(file,"intro.pss",0,0,"mpeg")})
                try{movie.InputUrl();throw new Exception("Invalid subfile URL accepted");}catch(HubException){}
        });
    }
    private static byte[] Iso()
    {
        var data=new byte[32*2048];var descriptor=data.AsSpan(16*2048,2048);descriptor[0]=1;"CD001"u8.CopyTo(descriptor[1..]);descriptor[6]=1;
        Record(20,2048,[0],true).CopyTo(descriptor[156..]);Record(21,2048,"MOVIE"u8.ToArray(),true).CopyTo(data,20*2048);
        Record(22,2048,"AC2OP01.PSS;1"u8.ToArray(),false).CopyTo(data,21*2048);return data;
    }
    private static byte[] Record(uint block,uint size,byte[] name,bool directory)
    {var bytes=new byte[(33+name.Length+1)&~1];bytes[0]=(byte)bytes.Length;BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2),block);BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(10),size);bytes[25]=directory?(byte)2:(byte)0;bytes[32]=(byte)name.Length;name.CopyTo(bytes,33);return bytes;}
    private static byte[] Xbox()
    {
        var data=Iso();Array.Resize(ref data,300*2048);const int partition=0xfb20;
        var header=data.AsSpan(partition+32*2048,28);"MICROSOFT*XBOX*MEDIA"u8.CopyTo(header);BinaryPrimitives.WriteUInt32LittleEndian(header[20..],60);BinaryPrimitives.WriteUInt32LittleEndian(header[24..],64);
        void Node(int location,string name,uint sector,uint size,bool directory){var entry=data.AsSpan(location);BinaryPrimitives.WriteUInt32LittleEndian(entry[4..],sector);BinaryPrimitives.WriteUInt32LittleEndian(entry[8..],size);entry[12]=directory?(byte)0x10:(byte)0;entry[13]=(byte)name.Length;Encoding.ASCII.GetBytes(name).CopyTo(entry[14..]);}
        Node(partition+60*2048,"movie",61,64,true);Node(partition+61*2048,"acv_opening.wmv",70,2048,false);return data;
    }
}
