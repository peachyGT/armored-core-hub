using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;
namespace Hub.Core;

// Native emulator authentication stays outside launch plans and diagnostic JSON.
// Only the achievement section is retained, encrypted for this Windows account.
public static class EmulatorAccounts
{
    public static string? ConfigFile(string backend)=>backend switch{"duckstation"=>"settings.ini","pcsx2"=>"inis/PCSX2.ini","ppsspp"=>"hub.ini",_=>null};
    private static string Section(string backend)=>backend=="duckstation"?"Cheevos":"Achievements";
    public static string Filter(string text,string backend)
    {
        var active=false;var lines=new List<string>();
        foreach(var raw in text.Split('\n'))
        {
            var line=raw.Trim();if(line.StartsWith('[')){active=line=="["+Section(backend)+"]";continue;}
            if(!active)continue;var split=line.IndexOf('=');if(split<1)continue;var key=line[..split].Trim();
            if(key.Contains("Host",StringComparison.OrdinalIgnoreCase)||key.Contains("Password",StringComparison.OrdinalIgnoreCase)||key.Contains("File",StringComparison.OrdinalIgnoreCase)||line.Length>2048)continue;
            if(key.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_'))lines.Add(line);
        }
        return lines.Count==0?"":"["+Section(backend)+"]\n"+string.Join('\n',lines)+"\n";
    }
    public static void Capture(HubPaths paths,string home,string backend)
    {
        if(!OperatingSystem.IsWindows()||ConfigFile(backend) is not string relative)return;
        var file=SafeFiles.Within(home,relative);if(!File.Exists(file)||new FileInfo(file).Length>2*1024*1024)return;
        var text=Filter(File.ReadAllText(file),backend);if(text.Length==0)return;
        var target=paths.Managed("config","private-achievements",backend+".bin");Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var plain=Encoding.UTF8.GetBytes(text);byte[] encrypted;try{encrypted=Crypt(plain,true);}finally{CryptographicOperations.ZeroMemory(plain);}var temp=target+".tmp";
        try{File.WriteAllBytes(temp,encrypted);File.Move(temp,target,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static string Restore(HubPaths paths,string backend)
    {
        var file=paths.Managed("config","private-achievements",backend+".bin");if(!OperatingSystem.IsWindows()||!File.Exists(file))return "";
        if(new FileInfo(file).Length>64*1024)throw new HubException("The saved emulator sign-in is damaged.");
        try{var plain=Crypt(File.ReadAllBytes(file),false);try{return Filter(Encoding.UTF8.GetString(plain),backend);}finally{CryptographicOperations.ZeroMemory(plain);}}
        catch(HubException){return "";} // A moved Hub on another Windows account needs a fresh sign-in.
    }
    internal static byte[] Crypt(byte[] bytes,bool protect)
    {
        var input=new Blob{Length=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};Blob output=default;
        try
        {
            Marshal.Copy(bytes,0,input.Data,bytes.Length);
            var ok=protect?CryptProtectData(ref input,null,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok)throw new HubException("Windows could not protect the emulator sign-in.");
            var result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;
        }
        finally
        {
            if(input.Data!=IntPtr.Zero){Marshal.Copy(new byte[input.Length],0,input.Data,input.Length);Marshal.FreeHGlobal(input.Data);}
            if(output.Data!=IntPtr.Zero){Marshal.Copy(new byte[output.Length],0,output.Data,output.Length);LocalFree(output.Data);}
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Blob{public int Length;public IntPtr Data;}
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true)] private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
