using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Hub.Core;

static class AchievementVisualTests
{
    public static void Register(Action<string,Action> test,Action<string,Func<Task>> asyncTest,string scratch,Catalog catalog)
    {
        void Check(bool value){if(!value)throw new Exception("Achievement artwork regression");}
        void Reject(Action action){try{action();}catch(Exception e)when(e is HubException or System.Xml.XmlException){return;}throw new Exception("Expected unsafe achievement artwork rejection");}
        HubPaths Paths()=>new(Path.Combine(scratch,"achievement-art-"+Guid.NewGuid().ToString("N")));
        var png=Png();
        test("Achievement PNG cache bounds bytes and dimensions, rejects external paths and survives relocation",()=>{
            var paths=Paths();var icons=new AchievementIconStore(paths);var relative=icons.Cache(png);Check(icons.TryPath(relative)!=null&&icons.Cache(png)==relative);
            Check(icons.TryPath("../secret.png")==null&&icons.TryPath("C:/private.png")==null&&icons.TryPath("https://invalid.example/art.png")==null);
            var huge=(byte[])png.Clone();BinaryPrimitives.WriteUInt32BigEndian(huge.AsSpan(16),uint.MaxValue);Reject(()=>icons.Cache(huge));Reject(()=>icons.Cache(new byte[AchievementIconStore.MaxBytes+1]));Reject(()=>icons.Cache(png[..^1]));
            var moved=paths.Root+"-moved";Directory.Move(paths.Root,moved);Check(new AchievementIconStore(new HubPaths(moved)).TryPath(relative)!=null);
            File.WriteAllBytes(Path.Combine(moved,relative),[1,2,3]);Check(new AchievementIconStore(new HubPaths(moved)).TryPath(relative)==null);
        });
        test("PS3 trophy metadata shows locked items before progress and keeps secrets redacted",()=>{
            var xml=Encoding.UTF8.GetBytes("<trophyconf><trophy id='0' ttype='B' hidden='n'><name>Visible goal</name><detail>Provider requirement</detail></trophy><trophy id='1' ttype='G' hidden='y'><name>Secret spoiler</name><detail>Secret condition</detail></trophy></trophyconf>");
            var locked=AchievementService.ReadTrophyMetadata(xml);Check(locked.All(i=>!i.Unlocked)&&locked[0].Description=="Provider requirement"&&locked[1].Hidden&&locked[1].Title=="Secret trophy"&&!locked[1].Description.Contains("Secret condition"));
            var earned=AchievementService.ReadTrophyMetadata(xml,new(){{1,true}});Check(earned[1].Title=="Secret spoiler"&&earned[1].Description=="Secret condition");
            Reject(()=>AchievementService.ReadTrophyMetadata(Encoding.UTF8.GetBytes("<trophyconf><trophy id='../x'/></trophyconf>")));Reject(()=>AchievementService.ReadTrophyMetadata(Encoding.UTF8.GetBytes("<trophyconf><trophy id='0'/><trophy id='0'/></trophyconf>")));
        });
        test("Trophy bundles validate checksums, names, extents and overlaps before copying metadata",()=>{
            var files=new Dictionary<string,byte[]>{{"TROP01.SFM",Encoding.UTF8.GetBytes("<trophyconf><trophy id='0'/></trophyconf>")},{"TROP000.PNG",png}};var trp=Trp(files);var read=TrophyArchive.Read(trp);
            Check(read["TROP000.PNG"].SequenceEqual(png)&&read.ContainsKey("TROP01.SFM"));
            var broken=(byte[])trp.Clone();broken[^1]^=1;Reject(()=>TrophyArchive.Read(broken));
            Reject(()=>TrophyArchive.Read(Trp(new(){{"../escape.PNG",png}})));
            var outside=Trp(files,1);BinaryPrimitives.WriteUInt64BigEndian(outside.AsSpan(64+32),ulong.MaxValue);Reject(()=>TrophyArchive.Read(outside));
            var overlap=Trp(files,1);BinaryPrimitives.WriteUInt64BigEndian(overlap.AsSpan(128+32),192);Reject(()=>TrophyArchive.Read(overlap));
        });
        test("Local PS3 trophy artwork caches original bytes and metadata-only sets yield to actual progress",()=>{
            var paths=Paths();var edition=catalog.Editions.First(e=>e.GameId=="acvd"&&e.Platform=="ps3");var source=paths.Games("fixture","PS3_GAME","TROPDIR","NPWR00000_00");Directory.CreateDirectory(source);
            var xml=Encoding.UTF8.GetBytes("<trophyconf><trophy id='0' ttype='B' hidden='n'><name>Fixture goal</name><detail>Fixture requirement</detail></trophy><trophy id='1' hidden='y'><name>Hidden spoiler</name></trophy></trophyconf>");
            File.WriteAllBytes(Path.Combine(source,"TROPHY.TRP"),Trp(new(){{"TROP01.SFM",xml},{"TROP000.PNG",png},{"TROP001.PNG",png}}));
            var service=new AchievementService(paths);var metadata=service.ReadLocal(edition,"rpcs3",paths.Games("fixture"));Check(metadata.Single().Items[0].Icon!=null&&!metadata.Single().Items[0].Unlocked&&metadata.Single().Items[1].Icon==null);
            var live=Path.Combine(new SaveService(paths).Live(edition.SaveFamily,"rpcs3"),"dev_hdd0","home","00000001","trophy","NPWR00000_00");Directory.CreateDirectory(live);File.WriteAllBytes(Path.Combine(live,"TROPCONF.SFM"),xml);File.WriteAllBytes(Path.Combine(live,"TROP000.PNG"),png);File.WriteAllBytes(Path.Combine(live,"TROPUSR.DAT"),Dat(0));
            var actual=service.ReadLocal(edition,"rpcs3",paths.Games("fixture"));Check(actual.Single().Items[0].Unlocked&&actual.Single().Items[0].Icon!=null&&service.Cached(edition.Id).Count==1);
            Check(File.ReadAllBytes(new AchievementIconStore(paths).TryPath(actual.Single().Items[0].Icon)!).SequenceEqual(png));Check(File.Exists(Path.Combine(source,"TROPHY.TRP")));
        });
        test("Xbox achievement image IDs resolve original GPD images and honor hidden flags",()=>{
            var data=Gpd(png,false,true);var items=AchievementService.ReadGpd(data);var images=AchievementService.ReadGpdImages(data);Check(items.Single().ProviderIconId=="7"&&images[7].SequenceEqual(png));
            var paths=Paths();var edition=catalog.Editions.First(e=>e.GameId=="acv"&&e.Platform=="x360");var live=new SaveService(paths).Live(edition.SaveFamily,"xenia");Directory.CreateDirectory(live);File.WriteAllBytes(Path.Combine(live,MediaService.NormalizeSerial(edition.TitleId??edition.Serial)+".gpd"),data);
            Check(new AchievementService(paths).ReadLocal(edition,"xenia").Single().Items.Single().Icon!=null);
            var hidden=AchievementService.ReadGpd(Gpd(png,false,false)).Single();Check(hidden.Hidden&&hidden.Title=="Secret achievement"&&!hidden.Description.Contains("Fixture requirement"));
            var invalid=(byte[])data.Clone();BinaryPrimitives.WriteUInt32BigEndian(invalid.AsSpan(52),uint.MaxValue);Reject(()=>AchievementService.ReadGpdImages(invalid));
        });
        test("RetroAchievements accepts provider badge IDs and never treats supplied paths as artwork URLs",()=>{
            Check(AchievementIconStore.BadgeUri("12345")=="https://media.retroachievements.org/Badge/12345.png"&&AchievementIconStore.BadgeUri("../1")==null&&AchievementIconStore.BadgeUri("https://evil.example")==null);
            var edition=catalog.Editions.First(e=>e.Platform=="ps1");var body=Encoding.UTF8.GetBytes("{\"ConsoleID\":12,\"Title\":\"Armored Core\",\"Achievements\":{\"1\":{\"Title\":\"Fixture\",\"Description\":\"Provider requirement\",\"BadgeName\":\"12345\"},\"2\":{\"BadgeName\":\"../escape\"}}}");
            var snapshot=AchievementService.ParseRetro(edition,new("fixture-user",1),body);Check(snapshot.Items[0].ProviderIconId=="12345"&&snapshot.Items[1].ProviderIconId==null&&!snapshot.Items[0].Unlocked);
        });
    }
    public static byte[] Png()=>Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private static byte[] Dat(uint id)
    {
        var data=new byte[192];BinaryPrimitives.WriteUInt32BigEndian(data,0x818F54AD);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8),1);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(48),6);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(60),1);BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(64),80);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(96),id);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(100),1);return data;
    }
    private static byte[] Trp(Dictionary<string,byte[]> files,uint version=2)
    {
        var offset=64+files.Count*64;var data=new byte[offset+files.Sum(f=>f.Value.Length)];BinaryPrimitives.WriteUInt32BigEndian(data,0xDCA24D00);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4),version);BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(8),(ulong)data.Length);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16),(uint)files.Count);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20),64);var n=0;
        foreach(var(name,bytes)in files){var p=64+n++*64;Encoding.ASCII.GetBytes(name).CopyTo(data,p);BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(p+32),(ulong)offset);BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(p+40),(ulong)bytes.Length);bytes.CopyTo(data,offset);offset+=bytes.Length;}
        if(version>=2)SHA1.HashData(data).CopyTo(data,28);return data;
    }
    private static byte[] Gpd(byte[] png,bool unlocked,bool visible)
    {
        var text=Encoding.BigEndianUnicode.GetBytes("Fixture goal\0Fixture earned description\0Fixture requirement\0");var recordLength=28+text.Length;var start=24+36+8;var data=new byte[start+recordLength+png.Length];"XDBF"u8.CopyTo(data);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8),2);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(12),2);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16),1);BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(24),1);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(38),(uint)recordLength);BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(42),2);BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(44),7);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(52),(uint)recordLength);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(56),(uint)png.Length);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(start),28);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(start+4),1);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(start+8),7);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(start+12),10);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(start+16),(unlocked?0x20000u:0)|(visible?8u:0));text.CopyTo(data,start+28);png.CopyTo(data,start+recordLength);return data;
    }
}
