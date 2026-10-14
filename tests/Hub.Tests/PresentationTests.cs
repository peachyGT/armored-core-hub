using System.Buffers.Binary;
using System.Text;
using Hub.Core;

static class PresentationTests
{
    public static void Register(Action<string, Action> test, string scratch)
    {
        void Check(bool value) { if (!value) throw new Exception("Asset fixture assertion failed"); }
        HubPaths Paths() => new(Path.Combine(scratch, "assets-" + Guid.NewGuid().ToString("N")));
        MediaRecord Media(string file, string format) => new("asset-fixture", file, format, "fixture-edition", "fixture", MediaConfidence.UnknownRevision, "Synthetic", [], DateTimeOffset.UtcNow, false);
        test("PS3 presentation slots copy by hash without modifying game files", () => {
            var paths = Paths(); var source = paths.Managed("games", "disc", "PS3_GAME"); Directory.CreateDirectory(source);
            File.WriteAllBytes(Path.Combine(source, "ICON0.PNG"), [1,2,3]); File.WriteAllBytes(Path.Combine(source, "PIC1.PNG"), [4,5,6]);
            File.WriteAllBytes(Path.Combine(source, "PIC0.PNG"), [7,8,9]); File.WriteAllBytes(Path.Combine(source, "ICON1.PAM"), [10]);
            var reader = new GameAssetReader(paths); var media = Media(Path.GetDirectoryName(source)!, "ps3-folder");
            var result = reader.Read(media);
            Check(result.Cover != null && result.Background != null && result.Logo == null && result.Video == null && result.ConsoleMovie != null);
            Check(File.ReadAllBytes(paths.Managed("artwork", result.Cover!)).SequenceEqual(new byte[]{1,2,3}));
            Check(Directory.GetFiles(source).Length == 4);
            reader.Read(media, true); Check(Directory.GetFiles(paths.Managed("artwork"), "*.png").Length == 2);
            reader.Read(media with {Id="another-copy"}); Check(Directory.GetFiles(paths.Managed("artwork"), "*.png").Length == 2);
        });
        test("PBP extracts bounded icon and background; overlays are not title logos", () => {
            var paths = Paths(); var file = paths.Managed("games", "EBOOT.PBP"); var bytes = new byte[64];
            new byte[]{0,80,66,80}.CopyTo(bytes,0);
            for (var i=0;i<8;i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8+i*4),(uint)(40+i*3));
            for (var i=40;i<bytes.Length;i++)bytes[i]=(byte)i; File.WriteAllBytes(file,bytes);
            var result = new GameAssetReader(paths).Read(Media(file,"pbp"));
            Check(File.ReadAllBytes(paths.Managed("artwork",result.Cover!)).SequenceEqual(bytes[43..46]));
            Check(result.Logo == null);
            Check(File.ReadAllBytes(paths.Managed("artwork",result.Background!)).SequenceEqual(bytes[52..55]));
        });
        test("Explicit companion logos are read instead of console marketing overlays", () => {
            var paths=Paths();var source=paths.Managed("games","with-logo","PS3_GAME");Directory.CreateDirectory(source);
            File.WriteAllBytes(Path.Combine(source,"PIC0.PNG"),[1,2,3]);File.WriteAllBytes(Path.Combine(source,"logo.png"),[4,5,6]);
            var result=new GameAssetReader(paths).Read(Media(Path.GetDirectoryName(source)!,"ps3-folder"));
            Check(result.Logo!=null&&File.ReadAllBytes(paths.Managed("artwork",result.Logo)).SequenceEqual(new byte[]{4,5,6}));
        });
        test("Malformed PBP offsets cannot read outside file or allocate oversized artwork", () => {
            var paths=Paths();var file=paths.Managed("games","bad.pbp");var bytes=new byte[40];new byte[]{0,80,66,80}.CopyTo(bytes,0);
            for(var i=0;i<8;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8+i*4),i%2==0?uint.MaxValue:1);
            File.WriteAllBytes(file,bytes);var result=new GameAssetReader(paths).Read(Media(file,"pbp"));
            Check(result.Cover==null&&result.Background==null&&result.Logo==null);
        });
        test("ISO9660 PSP menu artwork is extracted from standard paths", () => {
            var paths=Paths();var file=paths.Managed("games","fixture.iso");var bytes=new byte[25*2048];var pvd=bytes.AsSpan(16*2048,2048);pvd[0]=1;"CD001"u8.CopyTo(pvd[1..]);pvd[6]=1;
            byte[] Record(uint block,uint size,byte[] name,bool dir) { var r=new byte[34+name.Length];r[0]=(byte)r.Length;BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(2),block);BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(10),size);r[25]=dir?(byte)2:(byte)0;r[32]=(byte)name.Length;name.CopyTo(r,33);return r; }
            Record(20,2048,[0],true).CopyTo(pvd[156..]);Record(21,2048,"PSP_GAME"u8.ToArray(),true).CopyTo(bytes,20*2048);
            Record(22,3,"ICON0.PNG;1"u8.ToArray(),false).CopyTo(bytes,21*2048);new byte[]{1,2,3}.CopyTo(bytes,22*2048);File.WriteAllBytes(file,bytes);
            var result=new GameAssetReader(paths).Read(Media(file,"iso"));Check(result.Cover!=null&&File.ReadAllBytes(paths.Managed("artwork",result.Cover)).SequenceEqual(new byte[]{1,2,3}));
        });
        test("Per-file companion assets never borrow another game's cover", () => {
            var paths=Paths();var file=paths.Managed("games","mine.chd");File.WriteAllText(file,"fixture");File.WriteAllBytes(paths.Managed("games","cover.png"),[1]);
            var reader=new GameAssetReader(paths);Check(reader.Read(Media(file,"chd")).Cover==null);
            Directory.CreateDirectory(file+".artwork");File.WriteAllBytes(Path.Combine(file+".artwork","cover.png"),[2]);File.WriteAllBytes(Path.Combine(file+".artwork","background.mp4"),[3]);
            var result=reader.Read(Media(file,"chd"),true);Check(result.Cover!=null&&result.Video!=null&&File.ReadAllBytes(paths.Managed("artwork",result.Cover))[0]==2);
        });
        test("Damaged presentation cache recovers and missing media keeps a quiet fallback", () => {
            var paths=Paths();var media=Media(paths.Managed("games","missing.iso"),"iso");var reader=new GameAssetReader(paths);
            reader.Read(media);File.WriteAllText(paths.Managed("artwork","extracted",media.Id,"assets.json"),"broken");var result=reader.Read(media);
            Check(result.Cover==null&&result.Background==null&&result.Video==null&&result.Notes.Length>0);
        });
        test("Cancelled asset extraction does not publish an index", () => {
            var paths=Paths();var media=Media(paths.Managed("games","missing.iso"),"iso");using var ct=new CancellationTokenSource();ct.Cancel();
            try {new GameAssetReader(paths).Read(media,ct:ct.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
            Check(!File.Exists(paths.Managed("artwork","extracted",media.Id,"assets.json")));
        });
    }
}
