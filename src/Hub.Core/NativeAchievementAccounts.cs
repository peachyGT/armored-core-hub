using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hub.Core;

public sealed class NativeAchievementAccount
{
    public string Username {get;init;}="";
    public string Token {get;init;}="";
    public DateTimeOffset SignedInAt {get;init;}
    public override string ToString()=>Username; // Never expose a token through diagnostic interpolation.
}
public static class NativeAchievementAccounts
{
    private const string AccountPath="config/private-achievements/native-ra.bin";
    private const string SignedOutPath="config/private-achievements/signed-out.json";
    public static void Validate(NativeAchievementAccount account)
    {
        if(string.IsNullOrWhiteSpace(account.Username)||account.Username.Length>64||account.Username.Any(c=>char.IsControl(c)||c=='=')||account.Token is null||account.Token.Length is <16 or >256||!account.Token.All(char.IsAsciiLetterOrDigit))throw new HubException("RetroAchievements returned invalid account details. Sign in again.");
    }
    public static NativeAchievementAccount? Load(HubPaths paths)
    {
        if(!OperatingSystem.IsWindows())return null;var file=paths.Managed(AccountPath);
        try
        {
            if(File.Exists(paths.Managed(SignedOutPath)))return null;
            if(File.Exists(file))
            {
                if(new FileInfo(file).Length>16*1024)return null;var decrypted=EmulatorAccounts.Crypt(ReadPrivate(file,16*1024),false);
                try{var account=JsonSerializer.Deserialize<NativeAchievementAccount>(decrypted,JsonData.Options);if(account==null)return null;Validate(account);return account;}finally{CryptographicOperations.ZeroMemory(decrypted);}
            }
            var legacy=new List<NativeAchievementAccount>();
            foreach(var backend in new[]{"duckstation","pcsx2","ppsspp"})
            {
                var values=Values(EmulatorAccounts.Restore(paths,backend));var username=values.GetValueOrDefault(backend=="ppsspp"?"AchievementsUserName":"Username","");var token=values.GetValueOrDefault(backend=="ppsspp"?"AchievementsToken":"Token","");
                if(backend=="duckstation"&&username.Length>0&&token.Length>0)try{token=DecodeDuckToken(token,username);}catch(Exception e)when(e is FormatException or CryptographicException){continue;}
                var account=new NativeAchievementAccount{Username=username,Token=token,SignedInAt=DateTimeOffset.UtcNow};try{Validate(account);legacy.Add(account);}catch(HubException){}
            }
            return legacy.Select(a=>a.Username).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1?legacy.FirstOrDefault():null;
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or HubException or JsonException){return null;}
    }
    public static void Save(HubPaths paths,NativeAchievementAccount account)
    {
        Validate(account);if(!OperatingSystem.IsWindows())throw new HubException("Saving a sign-in requires Windows account protection.");var data=JsonSerializer.SerializeToUtf8Bytes(account,JsonData.Options);
        try{WritePrivate(paths.Managed(AccountPath),EmulatorAccounts.Crypt(data,true));}finally{CryptographicOperations.ZeroMemory(data);}
        var signedOut=paths.Managed(SignedOutPath);if(File.Exists(signedOut))File.Delete(signedOut);
    }
    public static void SignOut(HubPaths paths)
    {
        // Tombstone first: even an interrupted cleanup must not resurrect an old native login.
        JsonData.WriteAtomic(paths.Managed(SignedOutPath),new{signedOut=true});
        foreach(var relative in new[]{AccountPath,"config/private-achievements/duckstation.bin","config/private-achievements/pcsx2.bin","config/private-achievements/ppsspp.bin"})
        {var file=paths.Managed(relative);if(File.Exists(file))File.Delete(file);}
        var sessions=paths.Managed("runtime","sessions");
        if(Directory.Exists(sessions))foreach(var edition in Directory.EnumerateDirectories(sessions).Take(1000))
        {
            SafeFiles.NoLinks(edition);foreach(var home in Directory.EnumerateDirectories(edition).Take(100))
            {SafeFiles.NoLinks(home);foreach(var backend in new[]{"duckstation","pcsx2","ppsspp"})if(File.Exists(SafeFiles.Within(home,EmulatorAccounts.ConfigFile(backend)!)))CleanSessionSecrets(paths,backend,home,paths.Managed("saves"));}
        }
        var saves=paths.Managed("saves");if(Directory.Exists(saves))foreach(var file in SafeFiles.Files(saves).Where(f=>Path.GetFileName(f).Equals("ppsspp_retroachievements.dat",StringComparison.OrdinalIgnoreCase)).Take(10000))File.Delete(file);
    }
    public static string EncodeDuckToken(string token,string username)
    {
        var key=DuckKey(username);var plain=Encoding.UTF8.GetBytes(token);using var aes=Aes.Create();aes.Key=key[..16];aes.IV=key[16..];aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.Zeros;
        try{return Convert.ToBase64String(aes.EncryptCbc(plain,aes.IV,PaddingMode.Zeros));}finally{CryptographicOperations.ZeroMemory(key);CryptographicOperations.ZeroMemory(plain);}
    }
    public static string DecodeDuckToken(string token,string username)
    {
        var key=DuckKey(username);using var aes=Aes.Create();aes.Key=key[..16];aes.IV=key[16..];aes.Mode=CipherMode.CBC;
        try{var plain=aes.DecryptCbc(Convert.FromBase64String(token),aes.IV,PaddingMode.None);try{return Encoding.UTF8.GetString(plain).TrimEnd('\0');}finally{CryptographicOperations.ZeroMemory(plain);}}finally{CryptographicOperations.ZeroMemory(key);}
    }
    private static byte[] DuckKey(string username){var key=SHA256.HashData(Encoding.UTF8.GetBytes(username));for(var n=0;n<100;n++){var next=SHA256.HashData(key);CryptographicOperations.ZeroMemory(key);key=next;}return key;}
    public static string MergeConfig(HubPaths paths,string backend,Edition edition,string contents,bool includeSecrets=true)
    {
        if(!AchievementSupport.NativeRetroBackend(backend))return contents;var account=includeSecrets?Load(paths):null;var values=new Dictionary<string,string>();
        if(backend=="ppsspp")
        {values["AchievementsUserName"]=account?.Username??"";values["AchievementsToken"]="";values["AchievementsHost"]="";}
        else
        {values["Username"]=account?.Username??"";values["Token"]=backend=="duckstation"&&account!=null?EncodeDuckToken(account.Token,account.Username):"";values["LoginTimestamp"]=account?.SignedInAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)??"";values["Host"]="";}
        return MergeSection(contents,backend=="duckstation"?"Cheevos":"Achievements",values);
    }
    public static void WriteSessionSecrets(HubPaths paths,string backend,string workingDirectory,string saveDirectory)
    {
        if(!AchievementSupport.NativeRetroBackend(backend))return;CheckOwned(paths,workingDirectory);CheckOwned(paths,saveDirectory);var account=Load(paths);
        if(backend=="pcsx2")
        {
            var file=SafeFiles.Within(workingDirectory,"inis/secrets.ini");var old=File.Exists(file)&&new FileInfo(file).Length<=64*1024?ReadPrivateText(file,64*1024):"";
            WritePrivate(file,Encoding.UTF8.GetBytes(MergeSection(old,"Achievements",new(){{"Token",account?.Token??""}})));
        }
        else if(backend=="ppsspp")
        {
            var file=SafeFiles.Within(saveDirectory,"PSP/SYSTEM/ppsspp_retroachievements.dat");
            if(account!=null)WritePrivate(file,Encoding.UTF8.GetBytes(account.Token));else if(File.Exists(file))File.Delete(file);
        }
    }
    public static void CleanSessionSecrets(HubPaths paths,string backend,string workingDirectory,string saveDirectory)
    {
        if(!AchievementSupport.NativeRetroBackend(backend))return;CheckOwned(paths,workingDirectory);CheckOwned(paths,saveDirectory);
        if(EmulatorAccounts.ConfigFile(backend) is {} relative)
        {
            var file=SafeFiles.Within(workingDirectory,relative);if(File.Exists(file)&&new FileInfo(file).Length<=2*1024*1024)
            {
                var section=backend=="duckstation"?"Cheevos":"Achievements";var keys=backend=="ppsspp"?new Dictionary<string,string>{{"AchievementsToken",""}}:new(){{"Token",""}};
                WritePrivate(file,Encoding.UTF8.GetBytes(MergeSection(ReadPrivateText(file,2*1024*1024),section,keys)));
            }
        }
        var secret=backend=="pcsx2"?SafeFiles.Within(workingDirectory,"inis/secrets.ini"):backend=="ppsspp"?SafeFiles.Within(saveDirectory,"PSP/SYSTEM/ppsspp_retroachievements.dat"):null;
        if(secret!=null&&File.Exists(secret))
        {
            if(backend=="pcsx2"&&new FileInfo(secret).Length<=64*1024)WritePrivate(secret,Encoding.UTF8.GetBytes(MergeSection(ReadPrivateText(secret,64*1024),"Achievements",new(){{"Token",""}})));
            else File.Delete(secret);
        }
    }
    private static void CheckOwned(HubPaths paths,string root){if(!SafeFiles.IsWithin(paths.Root,root))throw new HubException("Achievement secrets must stay in the Hub's runtime/save folders.");SafeFiles.NoLinks(root);}
    private static Dictionary<string,string> Values(string text)=>text.Split('\n').Select(l=>l.Trim()).Where(l=>!l.StartsWith('[')&&l.Contains('=')).Select(l=>l.Split('=',2)).GroupBy(p=>p[0].Trim(),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Last()[1].Trim(),StringComparer.OrdinalIgnoreCase);
    internal static string MergeSection(string text,string section,Dictionary<string,string> values)
    {
        var lines=new List<string>();var active=false;
        foreach(var raw in text.Split('\n'))
        {
            var line=raw.Trim();if(line.StartsWith('['))active=line.Equals("["+section+"]",StringComparison.OrdinalIgnoreCase);
            var split=line.IndexOf('=');if(active&&split>0&&values.Keys.Contains(line[..split].Trim(),StringComparer.OrdinalIgnoreCase))continue;lines.Add(raw.TrimEnd('\r'));
        }
        // Appending one authoritative set avoids duplicate keys from a prior native sign-in.
        return string.Join('\n',lines)+"\n["+section+"]\n"+string.Join('\n',values.Select(v=>v.Key+"="+v.Value))+"\n";
    }
    private static void WritePrivate(string file,byte[] bytes)
    {
        SafeFiles.NoLinks(file);Directory.CreateDirectory(Path.GetDirectoryName(file)!);var temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllBytes(temporary,bytes);File.Move(temporary,file,true);}finally{if(File.Exists(temporary))File.Delete(temporary);CryptographicOperations.ZeroMemory(bytes);}
    }
    private static byte[] ReadPrivate(string file,int maximum)
    {
        SafeFiles.NoLinks(file);using var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);if(input.Length>maximum)throw new HubException("The saved achievement configuration exceeds its size limit.");var bytes=new byte[(int)input.Length];input.ReadExactly(bytes);return bytes;
    }
    private static string ReadPrivateText(string file,int maximum){var bytes=ReadPrivate(file,maximum);try{return Encoding.UTF8.GetString(bytes);}finally{CryptographicOperations.ZeroMemory(bytes);}}
}
