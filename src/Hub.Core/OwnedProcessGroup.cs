using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Hub.Core;

// Windows closes the job handle if the Hub exits unexpectedly, ending its owned
// children too. Never discovers or terminates processes by executable name.
public sealed class OwnedProcessGroup:IDisposable
{
    private SafeFileHandle? job;
    public OwnedProcessGroup()
    {
        if(!OperatingSystem.IsWindows())return;
        job=CreateJobObjectW(0,null);if(job.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());
        var info=new ExtendedLimits();info.Basic.Flags=0x2000;
        if(!SetInformationJobObject(job,9,ref info,(uint)Marshal.SizeOf<ExtendedLimits>())){var error=Marshal.GetLastWin32Error();job.Dispose();throw new Win32Exception(error);}
    }
    public void Add(Process process)
    {
        if(job==null)return;
        if(!AssignProcessToJobObject(job,process.SafeHandle))
        {
            var error=Marshal.GetLastWin32Error();
            if(process.HasExited)return;
            try{process.Kill(true);}catch(InvalidOperationException){}
            throw new HubException("The Hub could not safely own this emulator/helper process.",new Win32Exception(error));
        }
    }
    public void Dispose(){job?.Dispose();job=null;}
    [StructLayout(LayoutKind.Sequential)]private struct BasicLimits{public long ProcessTime,JobTime;public uint Flags;public nuint MinimumWorkingSet,MaximumWorkingSet;public uint ActiveProcesses;public nuint Affinity;public uint Priority,Scheduling;}
    [StructLayout(LayoutKind.Sequential)]private struct IoCounters{public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes;}
    [StructLayout(LayoutKind.Sequential)]private struct ExtendedLimits{public BasicLimits Basic;public IoCounters Io;public nuint ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true),DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern SafeFileHandle CreateJobObjectW(nint attributes,string? name);
    [DllImport("kernel32.dll",SetLastError=true),DefaultDllImportSearchPaths(DllImportSearchPath.System32)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetInformationJobObject(SafeFileHandle job,int informationClass,ref ExtendedLimits information,uint length);
    [DllImport("kernel32.dll",SetLastError=true),DefaultDllImportSearchPaths(DllImportSearchPath.System32)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool AssignProcessToJobObject(SafeFileHandle job,SafeProcessHandle process);
}
