using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hub.Core;

// Native offline Account and CON-directory header serialization, independently
// implemented from Canary's X_XAMACCOUNTINFO/ProfileManager/XContentMetadata.
// Format source: https://github.com/xenia-canary/xenia-canary (X_XAMACCOUNTINFO, ProfileManager, XContentMetadata).
internal static class XeniaLocalAccount
{
    private const int AccountInfoSize=0x17c,EncryptedSize=AccountInfoSize+0x18;
    private static readonly byte[] RetailKey=Convert.FromHexString("E1BC159C73B1EAE9AB3170F3AD47EBF3");
    private static readonly byte[] DevkitKey=Convert.FromHexString("DAB69AD98E28764F977EE2487E4F3F68");
    internal static byte[] Create(string gamertag)
    {
        var plain=new byte[AccountInfoSize+8];plain.AsSpan(0,8).Fill(0xfd);
        Encoding.BigEndianUnicode.GetBytes(gamertag).CopyTo(plain,8+8);
        var hash=HMACSHA1.HashData(RetailKey,plain);var key=HMACSHA1.HashData(RetailKey,hash.AsSpan(0,16));
        Crypt(plain,key.AsSpan(0,16));var output=new byte[EncryptedSize];hash.AsSpan(0,16).CopyTo(output);plain.CopyTo(output,16);return output;
    }
    internal static string ReadGamertag(string file)
    {
        SafeFiles.NoLinks(file);using var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(input.Length is <EncryptedSize or >4096)throw new HubException("The local Xbox player Account is damaged. Its files were kept.");
        var data=new byte[EncryptedSize];input.ReadExactly(data);
        foreach(var secret in new[]{RetailKey,DevkitKey})
        {
            var plain=data.AsSpan(16).ToArray();var key=HMACSHA1.HashData(secret,data.AsSpan(0,16));Crypt(plain,key.AsSpan(0,16));
            if(!CryptographicOperations.FixedTimeEquals(data.AsSpan(0,16),HMACSHA1.HashData(secret,plain).AsSpan(0,16)))continue;
            var name=Encoding.BigEndianUnicode.GetString(plain,16,32).Split('\0')[0];
            if(name.Length>0&&!name.Any(char.IsControl))return name;
        }
        throw new HubException("The local Xbox player Account failed its native integrity check. Its files were kept.");
    }
    internal static byte[] Header(ulong xuid,string gamertag)
    {
        // XContentHeader=0x344, metadata=0x93d6, custom name=42;
        // ContentManager rounds this CON directory header to 0x1000 bytes.
        var data=new byte[0xa000];"CON "u8.CopyTo(data);BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x340),0x971a);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x344),0x00010000); // Profile
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x348),2);
        BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(0x34c),EncryptedSize);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x360),0xfffe07d1); // Dashboard
        BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(0x371),xuid);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x39d),1);
        BinaryPrimitives.WriteUInt64BigEndian(data.AsSpan(0x3a1),EncryptedSize);
        Encoding.BigEndianUnicode.GetBytes(gamertag).CopyTo(data,0x411);return data;
    }
    private static void Crypt(Span<byte> bytes,ReadOnlySpan<byte> key)
    {
        Span<byte> box=stackalloc byte[256];for(var n=0;n<256;n++)box[n]=(byte)n;
        var j=0;for(var n=0;n<256;n++){j=(j+box[n]+key[n%16])&255;(box[n],box[j])=(box[j],box[n]);}
        var i=0;j=0;for(var n=0;n<bytes.Length;n++){i=(i+1)&255;j=(j+box[i])&255;(box[i],box[j])=(box[j],box[i]);bytes[n]^=box[(box[i]+box[j])&255];}
    }
}
