using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Hub.Core;

// Read the same bounded ROMDIR/ROMVER records PCSX2 uses, without executing firmware.
internal static class Ps2Bios
{
    internal static string? RomVersion(byte[] bytes)
    {
        for(var table=0;table+16<=Math.Min(bytes.Length,512*1024);table+=16)
        {
            if(!bytes.AsSpan(table,10).SequenceEqual("RESET\0\0\0\0\0"u8))continue;
            long extent=0;string? version=null;var romdir=false;var terminated=false;
            for(var index=0;index<512;index++)
            {
                var at=table+index*16;if(at>bytes.Length-16)break;
                var nameBytes=bytes.AsSpan(at,10);var end=nameBytes.IndexOf((byte)0);
                if(end==0){terminated=true;break;}if(end<0)break;
                var name=Encoding.ASCII.GetString(nameBytes[..end]);
                if(name.Any(c=>c is <' ' or >'~'))break;
                var length=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at+12,4));
                if(length>bytes.Length-extent)break;
                if(name=="ROMDIR")romdir=true;
                if(name=="ROMVER"&&length>=14)
                {
                    var candidate=Encoding.ASCII.GetString(bytes,(int)extent,14);
                    if(Regex.IsMatch(candidate,@"^0[12]\d{2}[ACEHJ][CD]\d{8}$")&&
                        DateOnly.TryParseExact(candidate[6..],"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))version=candidate;
                }
                extent+=(length+15L)&~15L;
            }
            if(terminated&&romdir&&version!=null)return version;
        }
        return null;
    }
}
