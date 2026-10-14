using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Hub.Core;

static class FirmwareSetupTests
{
    public static void Register(Action<string,Action> test,Action<string,Func<Task>> asyncTest,string scratch,Catalog catalog)
    {
        void Check(bool value){if(!value)throw new Exception("Firmware/setup regression");}
        async Task Reject(Func<Task> action){try{await action();}catch(HubException){return;}throw new Exception("Expected firmware rejection");}
        var edition=catalog.Editions.Single(e=>e.Id=="ac2-ps2-slus20014");
        byte[] Bios(string version)
        {
            var bytes=new byte[4194304];var table=0x100;
            void Entry(int i,string name,uint size){Encoding.ASCII.GetBytes(name).CopyTo(bytes,table+i*16);BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(table+i*16+12,4),size);}
            Entry(0,"RESET",0x100);Entry(1,"ROMDIR",80);Entry(2,"ROMVER",14);Entry(3,"EXTINFO",32);
            Encoding.ASCII.GetBytes(version).CopyTo(bytes,0x150);return bytes;
        }
        asyncTest("BIOS ZIP validates candidates and imports only the best matching console/region",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"bios-zip"));var archive=Path.Combine(scratch,"bios-mixture.zip");
            using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create))
            {
                foreach(var(name,version)in new[]{("fake-europe-name.bin","0160AC20020207"),("modern-eu.bin","0200EC20040614"),("old-jp.bin","0100JC20000117")})
                {using var output=zip.CreateEntry(name).Open();await output.WriteAsync(Bios(version));}
                using(var output=zip.CreateEntry("junk.bin").Open())await output.WriteAsync(new byte[4194304]);
                using(var output=zip.CreateEntry("other-console.pup").Open()){var bytes=new byte[128];"SCEUF"u8.CopyTo(bytes);await output.WriteAsync(bytes);}
                using(var output=zip.CreateEntry("readme.txt").Open())await output.WriteAsync("ignore this file"u8.ToArray());
            }
            var before=await SafeFiles.HashAsync(archive);var result=await new FirmwareService(paths,catalog).ImportForEditionAsync(archive,edition);
            Check(result.Selected.Region=="US"&&result.Selected.RomVersion=="0160AC20020207"&&result.CandidatesFound==3&&!result.Selected.Verified);
            Check(SafeFiles.Files(paths.Managed("firmware")).Count()==1&&!SafeFiles.Files(paths.Managed("cache")).Any()&&await SafeFiles.HashAsync(archive)==before);
            var japanese=edition with{Region="JP"};var other=await new FirmwareService(paths,catalog).ImportForEditionAsync(archive,japanese);
            Check(other.Selected.Region=="EU"&&other.Selected.RomVersion=="0200EC20040614");
        });
        test("Automatic BIOS choice uses compatibility, exact region, verified hash and version deterministically",()=>{
            FirmwareRecord F(string id,string region,string rom,bool verified=false)=>new(id,"ps2-bios",id,"",id,verified,region,rom);
            var us=F("us","US","0160AC20020207");var eu=F("eu","EU","0200EC20040614");var old=F("old","JP","0100JC20000117");
            Check(FirmwareService.Choose("ps2-bios",edition,[eu,us,old])==us);
            Check(FirmwareService.Choose("ps2-bios",edition with{Region="JP"},[old,eu])==eu);
            var development=F("development","US","0200AD20040614");Check(FirmwareService.Choose("ps2-bios",edition,[development,eu])==eu);
            var trusted=F("trusted","US","0160AC20020207",true);var newer=F("newer","US","0200AC20040614");Check(FirmwareService.Choose("ps2-bios",edition,[newer,trusted])==trusted);
            Check(FirmwareService.Choose("ps2-bios",edition,[us,eu],f=>f.Id!="us")==eu);
        });
        asyncTest("Loose BIOS signatures, invalid ROM extents and wrong-console files never publish firmware",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"bios-rejections"));var service=new FirmwareService(paths,catalog);var file=Path.Combine(scratch,"bad-bios.bin");
            var loose=new byte[4194304];"ROMDIR ROMVER 0160AC20020207"u8.CopyTo(loose);await File.WriteAllBytesAsync(file,loose);await Reject(()=>service.ImportAsync(file));
            var truncated=Bios("0160AC20020207");BinaryPrimitives.WriteUInt32LittleEndian(truncated.AsSpan(0x100+12,4),uint.MaxValue);await File.WriteAllBytesAsync(file,truncated);await Reject(()=>service.ImportAsync(file));
            var pup=Path.Combine(scratch,"wrong-console.pup");var bytes=new byte[128];"SCEUF"u8.CopyTo(bytes);await File.WriteAllBytesAsync(pup,bytes);await Reject(()=>service.ImportForEditionAsync(pup,edition));
            Check(!SafeFiles.Files(paths.Managed("firmware")).Any());
        });
        asyncTest("BIOS archives reject traversal and excess expansion before publishing or writing unrelated members",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"bios-unsafe"));var service=new FirmwareService(paths,catalog);var archive=Path.Combine(scratch,"unsafe-bios.zip");
            using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create)){using var output=zip.CreateEntry("../escape.txt").Open();await output.WriteAsync(new byte[64]);}
            await Reject(()=>service.ImportForEditionAsync(archive,edition));Check(!File.Exists(paths.Managed("cache","escape.txt")));
            var large=Path.Combine(scratch,"large-bios.zip");using(var zip=ZipFile.Open(large,ZipArchiveMode.Create)){using var output=zip.CreateEntry("ignored.txt").Open();await output.WriteAsync(new byte[64]);}
            var bytes=await File.ReadAllBytesAsync(large);var central=bytes.AsSpan().IndexOf(new byte[]{0x50,0x4b,0x01,0x02});BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(central+24,4),600u*1024*1024);await File.WriteAllBytesAsync(large,bytes);
            await Reject(()=>service.ImportForEditionAsync(large,edition));Check(!SafeFiles.Files(paths.Managed("firmware")).Any()&&!SafeFiles.Files(paths.Managed("cache")).Any());
        });
        asyncTest("Cancelled BIOS archive import keeps originals and removes every temporary candidate",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"bios-cancel"));var archive=Path.Combine(scratch,"cancel-bios.zip");
            using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create)){using var output=zip.CreateEntry("bios.bin").Open();await output.WriteAsync(Bios("0160AC20020207"));}
            using var stop=new CancellationTokenSource();var progress=new InlineProgress(_=>stop.Cancel());
            try{await new FirmwareService(paths,catalog).ImportForEditionAsync(archive,edition,progress,stop.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
            Check(File.Exists(archive)&&!SafeFiles.Files(paths.Managed("firmware")).Any()&&!SafeFiles.Files(paths.Managed("cache")).Any());
        });
        asyncTest("BIOS archive links, encrypted members and duplicate paths are rejected before candidate writes",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"bios-links"));var service=new FirmwareService(paths,catalog);
            var duplicate=Path.Combine(scratch,"duplicate-bios.zip");using(var zip=ZipFile.Open(duplicate,ZipArchiveMode.Create)){zip.CreateEntry("bios.bin");zip.CreateEntry("BIOS.bin");}
            await Reject(()=>service.ImportForEditionAsync(duplicate,edition));
            var encrypted=Path.Combine(AppContext.BaseDirectory,"fixtures","password-media.7z");await Reject(()=>service.ImportForEditionAsync(encrypted,edition));
            var bytes=await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"fixtures","synthetic-media.7z"));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0xc5,4),0xa1ff0020);
            var offset=checked(32+(int)BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(12,8)));var length=checked((int)BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(20,8)));
            static uint Crc(ReadOnlySpan<byte> data){var value=uint.MaxValue;foreach(var b in data){value^=b;for(var bit=0;bit<8;bit++)value=(value>>1)^((value&1)!=0?0xedb88320u:0);}return ~value;}
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28,4),Crc(bytes.AsSpan(offset,length)));BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8,4),Crc(bytes.AsSpan(12,20)));
            var linked=Path.Combine(scratch,"linked-bios.7z");await File.WriteAllBytesAsync(linked,bytes);await Reject(()=>service.ImportForEditionAsync(linked,edition));
            Check(!SafeFiles.Files(paths.Managed("firmware")).Any()&&!SafeFiles.Files(paths.Managed("cache")).Any());
        });
        var tool=Path.Combine(AppContext.BaseDirectory,"third-party","7zip","7zr.exe");
        if(OperatingSystem.IsWindows()&&File.Exists(tool))asyncTest("BIOS 7z imports a valid dump with the same selection and staging guarantees",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"bios-sevenzip"));var file=Path.Combine(scratch,"sevenzip-bios.bin");var archive=Path.Combine(scratch,"owned-bios.7z");await File.WriteAllBytesAsync(file,Bios("0160AC20020207"));
            Check(await SafeFiles.HashAsync(tool)=="256feca8e274e5da655e2a284fabafd9f554365eb164862089dacd4e8276d282");
            var start=new ProcessStartInfo(tool){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in new[]{"a","-t7z","-mx=1","-mmt=2","--",archive,file})start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;var stdout=process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);var stderr=process.StandardError.BaseStream.CopyToAsync(Stream.Null);await process.WaitForExitAsync();await Task.WhenAll(stdout,stderr);Check(process.ExitCode==0);
            var result=await new FirmwareService(paths,catalog).ImportForEditionAsync(archive,edition);Check(result.Selected.Region=="US"&&File.Exists(file)&&!SafeFiles.Files(paths.Managed("cache")).Any());
        });
        if(Environment.GetEnvironmentVariable("HUB_TEST_BIOS_ARCHIVE") is {} real&&File.Exists(real))asyncTest("User-provided BIOS archive selects a valid matching PS2 revision without changing the original",async()=>{
            var paths=new HubPaths(Path.Combine(scratch,"bios-real-owned"));var before=await SafeFiles.HashAsync(real);
            var result=await new FirmwareService(paths,catalog).ImportForEditionAsync(real,edition);
            Check(result.Selected.Region=="US"&&result.Selected.RomVersion!=null&&result.CandidatesFound>1&&await SafeFiles.HashAsync(real)==before&&SafeFiles.Files(paths.Managed("firmware")).Count()==1);
            Console.WriteLine("BIOS archive smoke: "+result.Selected.Label+" · ROMVER "+result.Selected.RomVersion+" · "+result.CandidatesFound+" candidates · source unchanged");
            JsonData.WriteAtomic(paths.Managed("logs","bios-selection.json"),result);
        });
        test("Setup checklist separates the selected console and an imported PS3 update from installation",()=>{
            var paths=new HubPaths(Path.Combine(scratch,"setup-checklist"));var state=new HubState();var pup=paths.Managed("firmware","test.pup");File.WriteAllText(pup,"synthetic");state.Firmware=[new("ps3","ps3-firmware",paths.Portable(pup),"","PS3 update",false,"Global")];
            var ps2=SetupChecklist.Read(paths,catalog,state,edition);Check(ps2.Requirements.Single(r=>r.Id=="firmware").State=="Missing"&&!ps2.Requirements.Any(r=>r.Detail.Contains("PS3 update")));
            var psp=catalog.Editions.First(e=>e.Platform=="psp");Check(SetupChecklist.Read(paths,catalog,state,psp).Requirements.Single(r=>r.Id=="firmware").Complete);
            var ps3=catalog.Editions.Single(e=>e.Id=="acfa-ps3-blus30187");var backend=catalog.Backends.Single(b=>b.Id=="rpcs3");var profile=catalog.Profiles.Single(p=>p.Id==ps3.PreferredProfile);var release=RecommendedBuilds.For(profile,backend)!;
            var home=paths.Managed("runtime","fixture");Directory.CreateDirectory(home);File.WriteAllText(Path.Combine(home,backend.Executable),"fixture");state.Builds=[new("fixture",backend.Id,release.Version,paths.Portable(home),backend.Executable,release.Sha256,"","fixture",DateTimeOffset.UtcNow)];
            var pending=SetupChecklist.Read(paths,catalog,state,ps3);Check(pending.FirmwareInstallPending&&!pending.Requirements.Single(r=>r.Id=="firmware").Complete);
            var session=paths.Managed("runtime","sessions",ps3.Id,"fixture");foreach(var name in new[]{Path.Combine("dev_flash","vsh","module","vsh.self"),Path.Combine("dev_flash","sys","external","liblv2.sprx")}){var target=Path.Combine(session,name);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.WriteAllText(target,"fixture");}
            var installed=SetupChecklist.Read(paths,catalog,state,ps3);Check(!installed.FirmwareInstallPending&&installed.Requirements.Single(r=>r.Id=="firmware").State=="Installed");
        });
    }
    private sealed class InlineProgress(Action<ProgressInfo> action):IProgress<ProgressInfo>{public void Report(ProgressInfo info)=>action(info);}
}
