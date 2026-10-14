using System.Runtime.InteropServices;
using System.IO;

namespace Hub.Core;

// Every COM reference is a native pointer with one owned
// reference and deterministic Release; no RCWs/finalizers/private WPF internals.
public sealed record MediaFoundationVideoFrame(int Width,int Height,int Stride,long Timestamp,byte[] Pixels);

public sealed unsafe class MediaFoundationVideoReader:IDisposable
{
    public const long MaximumSourceBytes=2L*1024*1024*1024;
    private const uint Video=0xfffffffc,All=0xfffffffe,MediaSource=0xffffffff;
    private static readonly Guid Major=new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"),Subtype=new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5"),FrameSize=new("1652c33d-d6b2-4012-b834-72030849a37d"),DefaultStride=new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6"),VideoMajor=new("73646976-0000-0010-8000-00aa00389b71"),Rgb32=new("00000016-0000-0010-8000-00aa00389b71"),AdvancedProcessing=new("0f81da2c-b537-4672-a8b2-a681b17307a3"),DurationKey=new("6c990d33-bb8e-477a-8598-0d5d96fcd88a"),SourceIid=new("279a808d-aec7-40c8-9c6b-a6b492c78a66"),Buffer2DIid=new("33ae5ea6-4316-436f-8ddd-d73d22f829ec");
    private nint reader,byteStream,mediaSource;private bool started,com;private readonly int owner=Environment.CurrentManagedThreadId;private int width,height,stride;private FileStream? sourceLock;
    public TimeSpan Duration{get;private set;}
    public int Width=>width;public int Height=>height;
    public MediaFoundationVideoReader(string path,int maximumWidth=960)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Video previews require Windows Media Foundation.");
        if(!Path.IsPathFullyQualified(path)||path.StartsWith("\\\\",StringComparison.Ordinal)||!File.Exists(path))throw new ArgumentException("Choose an existing local video file.");
        if(maximumWidth is <64 or >4096)throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        SafeFiles.NoLinks(path);
        nint attributes=0,type=0,native=0,resolver=0,resolved=0;
        try
        {
            sourceLock=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(sourceLock.Length is <1 or >MaximumSourceBytes)throw new InvalidDataException("Video previews need a nonempty local file smaller than 2 GB.");
            Check(CoInitializeEx(0,0));com=true;Check(MFStartup(0x20070,1));started=true;
            Check(MFCreateAttributes(out attributes,1));Set32(attributes,AdvancedProcessing,1);
            Check(MFCreateFile(1,0,0,path,out byteStream));
            Check(MFCreateSourceResolver(out resolver));uint objectType=0;
            fixed(char* name=path)Check(((delegate* unmanaged[Stdcall]<nint,nint,char*,uint,nint,uint*,nint*,int>)Slot(resolver,4))(resolver,byteStream,name,1,0,&objectType,&resolved));
            var iid=SourceIid;nint queriedSource=0;Check(((delegate* unmanaged[Stdcall]<nint,Guid*,nint*,int>)Slot(resolved,0))(resolved,&iid,&queriedSource));mediaSource=queriedSource;
            Check(MFCreateSourceReaderFromMediaSource(mediaSource,attributes,out reader));
            Check(((delegate* unmanaged[Stdcall]<nint,uint,int,int>)Slot(reader,4))(reader,All,0));
            Check(((delegate* unmanaged[Stdcall]<nint,uint,int,int>)Slot(reader,4))(reader,Video,1));
            Check(((delegate* unmanaged[Stdcall]<nint,uint,uint,nint*,int>)Slot(reader,5))(reader,Video,0,&native));
            var size=Get64(native,FrameSize);var nativeWidth=checked((int)(size>>32));var nativeHeight=checked((int)(size&uint.MaxValue));
            ValidateSize(nativeWidth,nativeHeight);
            Check(MFCreateMediaType(out type));SetGuid(type,Major,VideoMajor);SetGuid(type,Subtype,Rgb32);
            if(nativeWidth>maximumWidth)
            {
                var outputHeight=Math.Max(2,(int)((long)nativeHeight*maximumWidth/nativeWidth)&~1);
                Set64(type,FrameSize,((ulong)maximumWidth<<32)|(uint)outputHeight);
            }
            Check(((delegate* unmanaged[Stdcall]<nint,uint,nint,nint,int>)Slot(reader,7))(reader,Video,0,type));
            ReadFormat();var key=DurationKey;PropVariant value=default;
            try{var result=((delegate* unmanaged[Stdcall]<nint,uint,Guid*,PropVariant*,int>)Slot(reader,12))(reader,MediaSource,&key,&value);if(result>=0&&(value.Type is 20 or 21)&&value.Value>=0)Duration=TimeSpan.FromTicks(value.Value);}
            finally{PropVariantClear(ref value);}
        }
        catch{Release(ref resolved);Release(ref resolver);Release(ref native);Release(ref type);Release(ref attributes);Dispose();throw;}
        finally{Release(ref resolved);Release(ref resolver);Release(ref native);Release(ref type);Release(ref attributes);}
    }
    public void Seek(TimeSpan position)
    {
        AssertOwner();if(position<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(position));var format=Guid.Empty;var value=new PropVariant{Type=20,Value=position.Ticks};
        Check(((delegate* unmanaged[Stdcall]<nint,Guid*,PropVariant*,int>)Slot(reader,8))(reader,&format,&value));
    }
    /// <summary>Returns top-down BGRX pixels. A reuse buffer stays caller-owned;
    /// do not reuse it until the UI has copied the previous frame synchronously.</summary>
    public MediaFoundationVideoFrame? ReadFrame(CancellationToken cancellation=default,byte[]? reuse=null)
    {
        AssertOwner();
        for(var attempt=0;attempt<32;attempt++)
        {
            cancellation.ThrowIfCancellationRequested();nint sample=0,buffer=0,buffer2D=0;uint actual=0,flags=0;long time=0;
            try
            {
                Check(((delegate* unmanaged[Stdcall]<nint,uint,uint,uint*,uint*,long*,nint*,int>)Slot(reader,9))(reader,Video,0,&actual,&flags,&time,&sample));
                cancellation.ThrowIfCancellationRequested();if((flags&1)!=0)throw new InvalidDataException("The video decoder reported an error.");
                if((flags&0x20)!=0)ReadFormat();if((flags&2)!=0)return null;if(sample==0)continue;
                Check(((delegate* unmanaged[Stdcall]<nint,nint*,int>)Slot(sample,41))(sample,&buffer));
                var size=checked(width*height*4);var pixels=reuse is { } bytes&&bytes.Length==size?bytes:new byte[size];var iid=Buffer2DIid;
                var query=((delegate* unmanaged[Stdcall]<nint,Guid*,nint*,int>)Slot(buffer,0))(buffer,&iid,&buffer2D);
                if(query>=0)
                {
                    nint scanline=0,start=0;int pitch=0;uint length=0;bool locked=false;
                    try
                    {
                        Check(((delegate* unmanaged[Stdcall]<nint,uint,nint*,int*,nint*,uint*,int>)Slot(buffer2D,10))(buffer2D,1,&scanline,&pitch,&start,&length));locked=true;
                        CopyRows(scanline,pitch,start,length,pixels);
                    }
                    finally{if(locked)Check(((delegate* unmanaged[Stdcall]<nint,int>)Slot(buffer2D,4))(buffer2D));}
                }
                else
                {
                    nint start=0;uint maximum=0,length=0;bool locked=false;
                    try
                    {
                        Check(((delegate* unmanaged[Stdcall]<nint,nint*,uint*,uint*,int>)Slot(buffer,3))(buffer,&start,&maximum,&length));locked=true;
                        var scanline=stride<0?start+checked((height-1)*-stride):start;CopyRows(scanline,stride,start,length,pixels);
                    }
                    finally{if(locked)Check(((delegate* unmanaged[Stdcall]<nint,int>)Slot(buffer,4))(buffer));}
                }
                return new(width,height,width*4,time,pixels);
            }
            finally{Release(ref buffer2D);Release(ref buffer);Release(ref sample);}
        }
        throw new InvalidDataException("The decoder did not produce a video frame.");
    }
    private void CopyRows(nint scanline,int pitch,nint start,uint length,byte[] pixels)
    {
        var rowBytes=checked(width*4);if(Math.Abs((long)pitch)<rowBytes)throw new InvalidDataException("The decoded stride is invalid.");
        var end=checked((long)start+length);
        for(var row=0;row<height;row++)
        {
            var pointer=checked((long)scanline+(long)row*pitch);
            if(pointer<(long)start||pointer>end-rowBytes)throw new InvalidDataException("The decoded rows exceed their buffer.");
            Marshal.Copy((nint)pointer,pixels,row*rowBytes,rowBytes);
        }
    }
    private void ReadFormat()
    {
        nint type=0;
        try
        {
            Check(((delegate* unmanaged[Stdcall]<nint,uint,nint*,int>)Slot(reader,6))(reader,Video,&type));
            var size=Get64(type,FrameSize);width=checked((int)(size>>32));height=checked((int)(size&uint.MaxValue));ValidateSize(width,height);
            var key=DefaultStride;uint raw=0;var result=((delegate* unmanaged[Stdcall]<nint,Guid*,uint*,int>)Slot(type,7))(type,&key,&raw);
            stride=result>=0?unchecked((int)raw):width*4;
        }
        finally{Release(ref type);}
    }
    private static void ValidateSize(int width,int height){if(width is <1 or >8192||height is <1 or >8192||(long)width*height>16*1024*1024)throw new InvalidDataException("Video dimensions exceed the preview limit.");}
    private void AssertOwner(){if(Environment.CurrentManagedThreadId!=owner)throw new InvalidOperationException("The decoder belongs to one worker thread.");if(reader==0)throw new ObjectDisposedException(nameof(MediaFoundationVideoReader));}
    public void Dispose()
    {
        if(Environment.CurrentManagedThreadId!=owner)throw new InvalidOperationException("Release the decoder on its owning worker.");
        try
        {
            if(reader!=0)((delegate* unmanaged[Stdcall]<nint,uint,int>)Slot(reader,10))(reader,All);
            if(mediaSource!=0)((delegate* unmanaged[Stdcall]<nint,int>)Slot(mediaSource,12))(mediaSource);
        }
        finally{Release(ref reader);Release(ref mediaSource);if(byteStream!=0)((delegate* unmanaged[Stdcall]<nint,int>)Slot(byteStream,17))(byteStream);Release(ref byteStream);sourceLock?.Dispose();sourceLock=null;if(started){MFShutdown();started=false;}if(com){CoUninitialize();com=false;}}
    }
    private static nint Slot(nint obj,int index)=>Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj),index*IntPtr.Size);
    private static void Release(ref nint obj){if(obj!=0){((delegate* unmanaged[Stdcall]<nint,uint>)Slot(obj,2))(obj);obj=0;}}
    private static void Check(int result){if(result<0)Marshal.ThrowExceptionForHR(result);}
    private static void Set32(nint obj,Guid key,uint value)=>Check(((delegate* unmanaged[Stdcall]<nint,Guid*,uint,int>)Slot(obj,21))(obj,&key,value));
    private static void Set64(nint obj,Guid key,ulong value)=>Check(((delegate* unmanaged[Stdcall]<nint,Guid*,ulong,int>)Slot(obj,22))(obj,&key,value));
    private static ulong Get64(nint obj,Guid key){ulong value=0;Check(((delegate* unmanaged[Stdcall]<nint,Guid*,ulong*,int>)Slot(obj,8))(obj,&key,&value));return value;}
    private static void SetGuid(nint obj,Guid key,Guid value)=>Check(((delegate* unmanaged[Stdcall]<nint,Guid*,Guid*,int>)Slot(obj,24))(obj,&key,&value));
    [StructLayout(LayoutKind.Explicit,Size=24)]private struct PropVariant{[FieldOffset(0)]public ushort Type;[FieldOffset(8)]public long Value;}
    [DllImport("ole32.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int CoInitializeEx(nint reserved,uint model);
    [DllImport("ole32.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern void CoUninitialize();
    [DllImport("ole32.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int PropVariantClear(ref PropVariant value);
    [DllImport("mfplat.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int MFStartup(uint version,uint flags);
    [DllImport("mfplat.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int MFShutdown();
    [DllImport("mfplat.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int MFCreateAttributes(out nint attributes,uint initialSize);
    [DllImport("mfplat.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int MFCreateMediaType(out nint mediaType);
    [DllImport("mfplat.dll",CharSet=CharSet.Unicode),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int MFCreateFile(uint access,uint mode,uint flags,string path,out nint byteStream);
    [DllImport("mf.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int MFCreateSourceResolver(out nint resolver);
    [DllImport("mfreadwrite.dll"),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int MFCreateSourceReaderFromMediaSource(nint source,nint attributes,out nint reader);
}
