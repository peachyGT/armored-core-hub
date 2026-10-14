using System.Diagnostics;
using Hub.Core;

static class MediaFoundationVideoTests
{
    public static void Register(Action<string,Action> test,string scratch)
    {
        if(!OperatingSystem.IsWindows())return;
        void Check(bool value){if(!value)throw new Exception("Media Foundation reader regression");}
        test("Native video reader rejects nonlocal, missing and empty input before decoding",()=>{
            foreach(var file in new[]{"https://example.com/video.mp4",@"\\unavailable-server\share\video.mp4",Path.Combine(scratch,"absent-video.mp4")})
            {try{using var reader=new MediaFoundationVideoReader(file);throw new Exception("Expected local input rejection");}catch(ArgumentException){}}
            var empty=Path.Combine(scratch,"empty-video.mp4");File.WriteAllBytes(empty,[]);
            try{using var reader=new MediaFoundationVideoReader(empty);throw new Exception("Expected empty input rejection");}catch(InvalidDataException){}
        });
        test("Malformed videos release source locks and native setup across repeated failures",()=>{
            var file=Path.Combine(scratch,"malformed-video.mp4");File.WriteAllText(file,"invalid local movie fixture");
            void Reject(){try{using var reader=new MediaFoundationVideoReader(file);throw new Exception("Expected invalid decoder rejection");}catch(System.Runtime.InteropServices.COMException){}using var writable=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None);}
            Reject();using var process=Process.GetCurrentProcess();process.Refresh();var before=process.HandleCount;for(var n=0;n<12;n++)Reject();Thread.Sleep(500);process.Refresh();var first=process.HandleCount;for(var n=0;n<12;n++)Reject();Thread.Sleep(500);process.Refresh();Console.WriteLine($"Failed video handles: warm={before}, batch12={first}, batch24={process.HandleCount}");Check(process.HandleCount-first<16);
        });
        if(Environment.GetEnvironmentVariable("HUB_TEST_MEDIAFOUNDATION_VIDEO") is not string clip||!File.Exists(clip))return;
        test("Native video frames are scaled bounded top-down buffers with timestamp, seek, cancellation and loop support",()=>{
            using var reader=new MediaFoundationVideoReader(Path.GetFullPath(clip));Check(reader.Width<=960&&reader.Height>0&&reader.Duration>TimeSpan.Zero);
            var buffer=new byte[checked(reader.Width*reader.Height*4)];var first=reader.ReadFrame(reuse:buffer)??throw new Exception("No first frame");Check(ReferenceEquals(first.Pixels,buffer)&&first.Stride==first.Width*4&&first.Pixels.Length==first.Stride*first.Height);
            var next=reader.ReadFrame(reuse:buffer)??throw new Exception("No next frame");Check(next.Timestamp>=first.Timestamp&&ReferenceEquals(next.Pixels,buffer));reader.Seek(TimeSpan.FromSeconds(3));var seek=reader.ReadFrame(reuse:buffer)??throw new Exception("No seek frame");Check(seek.Timestamp>0&&seek.Timestamp<reader.Duration.Ticks);
            using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{reader.ReadFrame(cancel.Token,buffer);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}}
            reader.Seek(reader.Duration-TimeSpan.FromSeconds(Math.Min(.2,reader.Duration.TotalSeconds/2)));var ended=false;for(var n=0;n<64;n++)if(reader.ReadFrame(reuse:buffer)==null){ended=true;break;}Check(ended);
            reader.Seek(TimeSpan.Zero);Check(reader.ReadFrame(reuse:buffer)!=null);
            Exception? threadError=null;var worker=new Thread(()=>{try{reader.ReadFrame();}catch(Exception error){threadError=error;}});worker.Start();Check(worker.Join(3000)&&threadError is InvalidOperationException);
        });
        test("Repeated native reader disposal has no linear handle growth and releases the movie file",()=>{
            using(var warm=new MediaFoundationVideoReader(Path.GetFullPath(clip)))warm.ReadFrame();Thread.Sleep(100);using var process=Process.GetCurrentProcess();process.Refresh();var before=process.HandleCount;
            for(var n=0;n<24;n++){using var reader=new MediaFoundationVideoReader(Path.GetFullPath(clip));reader.ReadFrame();}Thread.Sleep(300);process.Refresh();Check(process.HandleCount-before<16);
            // Use a read-only exclusive lease to prove no decoder retains the source.
            using var source=new FileStream(clip,FileMode.Open,FileAccess.Read,FileShare.None);
        });
    }
}
