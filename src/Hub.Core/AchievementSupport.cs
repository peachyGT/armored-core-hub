namespace Hub.Core;

public sealed record AchievementSupportInfo(string Provider,int? GameId,bool HasPublishedSet,string Notes,string[] Sources);
public static class AchievementSupport
{
    public const string Checked="2026-10-11";
    private const string Series="https://retroachievements.org/hub/4841";
    private static readonly Dictionary<string,(int Id,bool Published)> Retro=new()
    {
        ["ac1-ps1"]=(11348,true),["acpp-ps1"]=(8081,true),["acmoa-ps1"]=(8148,true),["ac2-ps2"]=(3316,true),["ac3-ps2"]=(3308,true),
        ["ac2aa-ps2"]=(19247,false),["acsl-ps2"]=(21875,false),["acn-ps2"]=(19245,false),["aclr-ps2"]=(19244,false),
        ["ac3-psp"]=(2217,false),["acsl-psp"]=(2213,false),["aclr-psp"]=(2201,false),
        ["acff-ps2"]=(27005,false),["acff-psp"]=(2196,false),["acnb-ps2"]=(19246,false)
    };
    public static AchievementSupportInfo Get(Edition edition)
    {
        if(edition.Platform=="x360")return new("Xenia",null,true,"Xbox achievements are tracked automatically in the local Xenia profile. Artwork and definitions come from the game's supplied data; there is no Xbox Live sign-in or invented PSN trophy grade.",["https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/user_tracker.cc"]);
        if(edition.Platform=="ps3")return edition.GameId is "ac4" or "acfa"?new("RPCS3",null,false,"This PS3 release has no trophy set. The Xbox 360 edition has separate achievements.",["https://www.fromsoftware.jp/jp/manual.html"]):new("RPCS3",null,true,"PS3 trophies are tracked automatically in the local RPCS3 profile. Game-supplied trophy metadata determines the actual list and grades.",["https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Loader/TROPUSR.cpp"]);
        var key=edition.GameId+"-"+edition.Platform;var found=Retro.TryGetValue(key,out var value);
        var notes=found&&value.Published?"A RetroAchievements set is published. The emulator identifies supported copies by its achievement hash at launch; a set ID alone does not verify this edition.":"No published RetroAchievements set was listed for this title/platform when checked on "+Checked+". Integration remains ready for supported sets; this does not create achievements.";
        if(edition.GameId=="ac3"&&edition.Platform=="ps2")notes+=" The official supported-hash list includes USA stock and VanLaser analog/inverted v1.64 images. Other patches and versions are not automatically confirmed.";
        if(edition.GameId=="ac2"&&edition.Platform=="ps2")notes+=" The inspected supported-hash list contains the USA stock copy; other regions and analog-patched executables remain unconfirmed.";
        if(edition.Region!="US"&&found&&value.Published)notes+=" This region is not assumed compatible; check the provider's supported hashes.";
        var sources=found?new[]{"https://retroachievements.org/game/"+value.Id,Series}:new[]{Series};
        return new("RetroAchievements",found?value.Id:null,found&&value.Published,notes,sources);
    }
    public static bool NativeRetroBackend(string backend)=>backend is "duckstation" or "pcsx2" or "ppsspp";
    public static int ConsoleId(Edition e)=>e.Platform switch{"ps1"=>12,"ps2"=>21,"psp"=>41,_=>-1};
}

public sealed class AchievementPreferences
{
    public bool Enabled {get;set;}=true;
    public bool Hardcore {get;set;}
    public Dictionary<string,bool> GameEnabled {get;set;}=[];
    public Dictionary<string,bool> GameHardcore {get;set;}=[];
    public static AchievementPreferences Load(HubPaths paths)
    {
        var file=paths.Managed("config","achievement-preferences.json");if(!File.Exists(file))return new();
        try
        {
            if(new FileInfo(file).Length>128*1024)return new();var prefs=JsonData.ReadBounded<AchievementPreferences>(file,128*1024);
            if(prefs.GameEnabled==null||prefs.GameHardcore==null||prefs.GameEnabled.Count>1000||prefs.GameHardcore.Count>1000)return new();
            return prefs;
        }
        catch(Exception e)when(e is System.Text.Json.JsonException or IOException or UnauthorizedAccessException or HubException){return new();}
    }
    public void Save(HubPaths paths)
    {
        if(GameEnabled.Count>1000||GameHardcore.Count>1000)throw new HubException("There are too many achievement overrides.");foreach(var id in GameEnabled.Keys.Concat(GameHardcore.Keys))SafeFiles.Id(id);
        JsonData.WriteAtomic(paths.Managed("config","achievement-preferences.json"),this);
    }
    public bool Tracking(Edition e)=>Enabled&&GameEnabled.GetValueOrDefault(e.Id,true);
    public bool Challenge(Edition e)=>Tracking(e)&&GameHardcore.GetValueOrDefault(e.Id,Hardcore);
    public static void Apply(Dictionary<string,string> settings,string backend,Edition edition,HubPaths paths,bool offline)
    {
        if(!AchievementSupport.NativeRetroBackend(backend))return;var prefs=Load(paths);var enabled=prefs.Tracking(edition)&&!offline;var hardcore=enabled&&prefs.Challenge(edition);
        if(backend=="ppsspp")
        {settings["Achievements/AchievementsEnable"]=enabled?"true":"false";settings["Achievements/AchievementsChallengeMode"]=hardcore?"true":"false";settings["Achievements/AchievementsEnableRAIntegration"]="false";}
        else
        {var section=backend=="duckstation"?"Cheevos":"Achievements";settings[section+"/Enabled"]=enabled?"true":"false";settings[section+"/ChallengeMode"]=hardcore?"true":"false";if(backend=="duckstation")settings[section+"/UseRAIntegration"]="false";}
    }
}
