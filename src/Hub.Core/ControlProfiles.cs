namespace Hub.Core;

public sealed class ControlChoice
{
    public string KeyboardPreset { get; set; } = "Default";
    public string ControllerPreset { get; set; } = "Default";
    public Dictionary<string,string> Keyboard { get; set; } = [];
    public Dictionary<string,string> Controller { get; set; } = [];
    public CommunityBindings? Community { get; set; }
    public string? XeniaMapping { get; set; }
    // The game's own layout is distinct from the physical input profile. Selecting
    // this field documents the layout; it never silently edits an in-game save.
    public string GameScheme { get; set; } = "auto";
    public List<SavedBindingProfile> Imported { get; set; } = [];
    public string? KeyboardProfileId { get; set; }
    public string? ControllerProfileId { get; set; }
}
public sealed record CommunityBindings(string Name, string Source, string[] Editions, Dictionary<string,string> Keyboard, Dictionary<string,string> Controller,string? XeniaMapping=null,string? GameScheme=null);
public sealed record SavedBindingProfile(string Id, CommunityBindings Profile);

// Console buttons are stable across adapters. Physical inputs are translated at launch,
// rather than storing one emulator's schema in every edition's library record.
public static class ControlProfiles
{
    public static readonly Dictionary<string,string> Pad = new()
    {
        ["Up"]="DPadUp",["Down"]="DPadDown",["Left"]="DPadLeft",["Right"]="DPadRight",
        ["Cross"]="A",["Circle"]="B",["Square"]="X",["Triangle"]="Y",["Start"]="Start",["Select"]="Back",
        ["L1"]="LeftShoulder",["R1"]="RightShoulder",["L2"]="+LeftTrigger",["R2"]="+RightTrigger",["L3"]="LeftStick",["R3"]="RightStick",
        ["LUp"]="-LeftY",["LDown"]="+LeftY",["LLeft"]="-LeftX",["LRight"]="+LeftX",["RUp"]="-RightY",["RDown"]="+RightY",["RLeft"]="-RightX",["RRight"]="+RightX"
    };
    public static readonly string[] Keys = [..Enumerable.Range('A',26).Select(c=>((char)c).ToString()),..Enumerable.Range(0,10).Select(x=>x.ToString()),"Up","Down","Left","Right","Return","Space","Tab","Shift","Control","Backspace"];
    public static string[] Buttons(string backend) => InputDefaults.Keyboard.Keys.Where(k=>backend!="ppsspp" || k is not ("L2" or "R2" or "L3" or "R3" or "RUp" or "RDown" or "RLeft" or "RRight")).ToArray();
    public static Dictionary<string,string> Resolve(string backend, ControlChoice choice, bool keyboard, Edition? edition=null)
    {
        ValidateStored(choice);
        var result = new Dictionary<string,string>(keyboard ? InputDefaults.Keyboard : Pad);
        if (keyboard && backend=="ppsspp")
            foreach(var (k,v) in new Dictionary<string,string>{{"Start","Space"},{"Select","V"},{"Cross","Z"},{"Circle","X"},{"Square","A"},{"Triangle","S"},{"L1","Q"},{"R1","W"},{"LUp","I"},{"LDown","K"},{"LLeft","J"},{"LRight","L"}}) result[k]=v;
        var preset = keyboard ? choice.KeyboardPreset : choice.ControllerPreset;
        var selected = preset switch { "Default" => new Dictionary<string,string>(), "Custom" => keyboard?choice.Keyboard:choice.Controller,
            "Community" => CommunityProfile(choice,edition) is { } community ? keyboard?community.Keyboard:community.Controller : throw new HubException("Choose a community binding profile first."),
            "Imported" => ImportedProfile(choice,keyboard,edition) is { } imported ? keyboard?imported.Keyboard:imported.Controller : throw new HubException("Choose an imported binding profile first."),
            _=>throw new HubException("Unknown control preset.") };
        Validate(selected,keyboard);
        foreach(var (k,v) in selected) result[k]=v;
        return result.Where(x=>Buttons(backend).Contains(x.Key)).ToDictionary();
    }
    public static void Validate(Dictionary<string,string> bindings, bool keyboard)
    {
        if (bindings is null) throw new HubException("The binding profile is incomplete.");
        if (bindings.Any(x=>!Pad.ContainsKey(x.Key) || !(keyboard?Keys:Pad.Values.ToArray()).Contains(x.Value))) throw new HubException("The binding profile contains an unsupported button or input.");
    }
    public static void ValidateStored(ControlChoice choice)
    {
        if (choice is null || choice.Imported is null || choice.GameScheme is null || choice.GameScheme.Length > 64 || choice.GameScheme.Any(char.IsControl)) throw new HubException("The saved control settings are incomplete.");
        if (choice.KeyboardPreset is not ("Default" or "Community" or "Custom" or "Imported") || choice.ControllerPreset is not ("Default" or "Community" or "Custom" or "Imported")) throw new HubException("The saved control preset is unknown.");
        Validate(choice.Keyboard, true); Validate(choice.Controller, false);
        if (choice.Imported.Count > 64 || choice.Imported.Any(p=>p is null || string.IsNullOrWhiteSpace(p.Id) || p.Id.Length > 128 || p.Id.Any(char.IsControl)) || choice.Imported.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=choice.Imported.Count) throw new HubException("The saved imported control profiles are damaged.");
        foreach (var saved in choice.Imported) ValidateProfileStructure(saved.Profile);
        if (choice.Community != null) ValidateProfileStructure(choice.Community);
        if (choice.XeniaMapping != null) ValidateSdl(choice.XeniaMapping);
        foreach (var id in new[] { choice.KeyboardProfileId, choice.ControllerProfileId }) if (id is { Length: > 128 } || id?.Any(char.IsControl)==true) throw new HubException("The saved control profile ID is damaged.");
    }
    public static string RpcName(string name) => name switch { "LUp"=>"Left Stick Up","LDown"=>"Left Stick Down","LLeft"=>"Left Stick Left","LRight"=>"Left Stick Right","RUp"=>"Right Stick Up","RDown"=>"Right Stick Down","RLeft"=>"Right Stick Left","RRight"=>"Right Stick Right",_=>name };
    public static CommunityBindings? ImportedProfile(ControlChoice choice,bool keyboard,Edition? edition=null)
    {
        var id=keyboard?choice.KeyboardProfileId:choice.ControllerProfileId;
        var profile=choice.Imported.FirstOrDefault(p=>p.Id==id)?.Profile;
        if(profile!=null&&edition!=null&&!profile.Editions.Contains(edition.Id,StringComparer.Ordinal))throw new HubException("The imported bindings do not support this edition.");
        if(profile?.GameScheme!=null&&edition!=null&&GameControlCatalog.Resolve(edition,choice).Id!=GameControlCatalog.Resolve(edition,new(){GameScheme=profile.GameScheme}).Id)throw new HubException("The imported profile uses another in-game layout. Select its matching game layout before launching.");
        return profile;
    }
    public static CommunityBindings? CommunityProfile(ControlChoice choice,Edition? edition=null)=>choice.Community??(edition==null?null:CommunityControlPresets.Get(edition,choice.GameScheme));
    public static SavedBindingProfile Import(ControlChoice choice,CommunityBindings profile,Edition edition)
    {
        ValidateProfile(profile,edition);
        if(choice.Imported.Count>=64)throw new HubException("This edition already has 64 imported profiles. Remove an unused profile before importing another.");
        var saved=new SavedBindingProfile(Guid.NewGuid().ToString("N"),Clone(profile));choice.Imported.Add(saved);return saved;
    }
    public static void Rename(ControlChoice choice,string id,string name)
    {
        ValidateName(name);var index=choice.Imported.FindIndex(p=>p.Id==id);if(index<0)throw new HubException("The imported profile no longer exists.");
        choice.Imported[index]=choice.Imported[index] with {Profile=choice.Imported[index].Profile with {Name=name.Trim()}};
    }
    public static CommunityBindings Export(ControlChoice choice,string id,Edition edition)
    {
        var profile=choice.Imported.FirstOrDefault(p=>p.Id==id)?.Profile??throw new HubException("The imported profile no longer exists.");ValidateProfile(profile,edition);return Clone(profile);
    }
    public static void Remove(ControlChoice choice,string id)
    {
        choice.Imported.RemoveAll(p=>p.Id==id);
        if(choice.KeyboardProfileId==id){choice.KeyboardProfileId=null;if(choice.KeyboardPreset=="Imported")choice.KeyboardPreset="Default";}
        if(choice.ControllerProfileId==id){choice.ControllerProfileId=null;if(choice.ControllerPreset=="Imported")choice.ControllerPreset="Default";}
    }
    public static void ValidateProfile(CommunityBindings profile,Edition edition)
    {
        ValidateProfileStructure(profile);
        if(!profile.Editions.Contains(edition.Id,StringComparer.Ordinal))throw new HubException("The profile must include this edition's ID.");
        if(profile.GameScheme!=null&&!GameControlCatalog.Schemes(edition).Any(s=>s.Id==profile.GameScheme))throw new HubException("The profile's game layout is not available for this title and platform.");
    }
    private static void ValidateProfileStructure(CommunityBindings profile)
    {
        if(profile is null||profile.Editions is null||profile.Keyboard is null||profile.Controller is null||profile.Source is null)throw new HubException("The binding profile is incomplete.");
        ValidateName(profile.Name);
        if(profile.Editions.Length is <1 or >256 || profile.Editions.Any(id=>string.IsNullOrWhiteSpace(id)||id.Length>128||id.Any(char.IsControl)))throw new HubException("The profile must include valid edition IDs.");
        if(profile.Source.Length>2048||profile.Source.Length>0&&(!Uri.TryCreate(profile.Source,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo)))throw new HubException("Use an HTTPS source URL, or leave the source blank for your own layout.");
        Validate(profile.Keyboard,true);Validate(profile.Controller,false);if(profile.XeniaMapping!=null)ValidateSdl(profile.XeniaMapping);
        if(profile.GameScheme is {Length:>64}||profile.GameScheme!=null&&profile.GameScheme.Any(char.IsControl))throw new HubException("The profile's game layout is invalid.");
    }
    private static void ValidateName(string name){if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>160||name.Any(char.IsControl))throw new HubException("Enter a profile name of 1 to 160 characters on one line.");}
    private static CommunityBindings Clone(CommunityBindings p)=>p with {Editions=[..p.Editions],Keyboard=new(p.Keyboard),Controller=new(p.Controller)};
    public static ControlChoice ForLaunch(Edition edition,ControlChoice choice,bool analogPatch)
    {
        if(choice.GameScheme!="auto")return choice;
        return new(){KeyboardPreset=choice.KeyboardPreset,ControllerPreset=choice.ControllerPreset,Keyboard=choice.Keyboard,Controller=choice.Controller,Community=choice.Community,XeniaMapping=choice.XeniaMapping,GameScheme=GameControlCatalog.Resolve(edition,choice,analogPatch).Id,Imported=choice.Imported,KeyboardProfileId=choice.KeyboardProfileId,ControllerProfileId=choice.ControllerProfileId};
    }
    public static void MigrateLegacy(ControlChoice choice)
    {
        ValidateStored(choice);
        if(choice.Community is not {} legacy)return;
        // Earlier versions overwrote the single Community slot on import. Retain
        // that exact layout as an imported entry rather than losing the user's work.
        var saved=new SavedBindingProfile(Guid.NewGuid().ToString("N"),Clone(legacy));choice.Imported.Add(saved);
        if(choice.KeyboardPreset=="Community"){choice.KeyboardProfileId=saved.Id;choice.KeyboardPreset="Imported";}
        if(choice.ControllerPreset=="Community"){choice.ControllerProfileId=saved.Id;choice.ControllerPreset="Imported";}
        choice.Community=null;
    }
    public static void RpcApply(Dictionary<string,string> settings, ControlChoice choice, Edition? edition=null)
    {
        var keyboard=Resolve("rpcs3",choice,true,edition);var pad=Resolve("rpcs3",choice,false,edition);
        foreach(var key in settings.Keys.Where(k=>k.EndsWith("/Handler",StringComparison.Ordinal)).ToArray())
        {
            var prefix=key[..^7]; var physical=settings[key]=="XInput";
            foreach(var (button,value) in physical?pad:keyboard)
                settings[prefix+"Config/"+RpcName(button)]=physical ? value switch {
                    "DPadUp"=>"Up","DPadDown"=>"Down","DPadLeft"=>"Left","DPadRight"=>"Right","LeftShoulder"=>"LB","RightShoulder"=>"RB",
                    "+LeftTrigger"=>"LT","+RightTrigger"=>"RT","LeftStick"=>"LS","RightStick"=>"RS","-LeftX"=>"LS X-","+LeftX"=>"LS X+",
                    "-LeftY"=>"LS Y+","+LeftY"=>"LS Y-","-RightX"=>"RS X-","+RightX"=>"RS X+","-RightY"=>"RS Y+","+RightY"=>"RS Y-",_=>value } : value;
        }
    }
    public static Dictionary<string,string> Psp(ControlChoice choice, Edition? edition=null)
    {
        // Preserve upstream defaults (including generic gamepads) when no profile was chosen.
        if (choice.KeyboardPreset=="Default" && choice.ControllerPreset=="Default") return [];
        var keyboard=Resolve("ppsspp",choice,true,edition);var pad=Resolve("ppsspp",choice,false,edition);var result=new Dictionary<string,string>();
        foreach(var (button,key) in keyboard)
        {
            var name=button switch {"L1"=>"L","R1"=>"R","LUp"=>"An.Up","LDown"=>"An.Down","LLeft"=>"An.Left","LRight"=>"An.Right",_=>button};
            var code=PspPad(pad[button]);
            result["ControlMapping/"+name]="1-"+PspKey(key)+","+string.Join(',',Enumerable.Range(20,4).Select(i=>i+"-"+code));
        }
        result["ControlMapping/Pause"]="1-111"; return result;
    }
    private static int PspKey(string key) => key.Length==1 && key[0] is >= 'A' and <= 'Z' ? key[0]-'A'+29 : key.Length==1 && char.IsDigit(key[0]) ? key[0]-'0'+7 : key switch {"Up"=>19,"Down"=>20,"Left"=>21,"Right"=>22,"Return"=>66,"Space"=>62,"Tab"=>61,"Shift"=>59,"Control"=>113,"Backspace"=>67,_=>throw new HubException("Unsupported PSP key.")};
    private static int PspPad(string key) => key switch {"A"=>96,"B"=>97,"X"=>99,"Y"=>100,"Start"=>108,"Back"=>109,"LeftShoulder"=>102,"RightShoulder"=>103,"LeftStick"=>106,"RightStick"=>107,"DPadUp"=>19,"DPadDown"=>20,"DPadLeft"=>21,"DPadRight"=>22,"-LeftX"=>4001,"+LeftX"=>4000,"-LeftY"=>4002,"+LeftY"=>4003,"-RightX"=>4023,"+RightX"=>4022,"-RightY"=>4028,"+RightY"=>4029,"+LeftTrigger"=>4034,"+RightTrigger"=>4036,_=>throw new HubException("Unsupported PSP button.")};
    public static void XeniaApply(Dictionary<string,string> settings, ControlChoice choice, Edition? edition=null)
    {
        if (choice.ControllerPreset!="Default") {ValidateSdl(XeniaMap(choice,edition));settings["HID/hid"]="sdl";}
        foreach(var (button,key) in Resolve("xenia",choice,true,edition))
        {
            var name=button switch {"Cross"=>"a","Circle"=>"b","Square"=>"x","Triangle"=>"y","Select"=>"back","L1"=>"left_shoulder","R1"=>"right_shoulder","L2"=>"left_trigger","R2"=>"right_trigger","L3"=>"left_thumb","R3"=>"right_thumb","Up"=>"dpad_up","Down"=>"dpad_down","Left"=>"dpad_left","Right"=>"dpad_right","LUp"=>"left_thumb_up","LDown"=>"left_thumb_down","LLeft"=>"left_thumb_left","LRight"=>"left_thumb_right","RUp"=>"right_thumb_up","RDown"=>"right_thumb_down","RLeft"=>"right_thumb_left","RRight"=>"right_thumb_right",_=>button.ToLowerInvariant()};
            settings["HID.Key/keybind_"+name]=key switch {"Return"=>"0x0D","Space"=>"0x20","Up"=>"0x26","Down"=>"0x28","Left"=>"0x25","Right"=>"0x27","Tab"=>"0x09","Shift"=>"0x10","Control"=>"0x11","Backspace"=>"0x08",_=>key};
        }
    }
    public static string XeniaMap(ControlChoice choice,Edition? edition=null)
    {
        var raw=choice.ControllerPreset switch {"Community"=>CommunityProfile(choice,edition)?.XeniaMapping,"Imported"=>ImportedProfile(choice,false,edition)?.XeniaMapping,_=>choice.XeniaMapping};
        if(!string.IsNullOrWhiteSpace(raw))return raw;
        // Every adapter stores the same physical inputs. SDL's named XInput
        // fallback expresses these without guessing a hardware-specific GUID.
        return XeniaControllerMappings.Create(Resolve("xenia",choice,false,edition));
    }
    public static void ValidateSdl(string text)
    {
        if(text.Length is <40 or >256*1024)throw new HubException("Choose an SDL controller mapping file (up to 256 KB).");
        var count=0;
        foreach(var raw in text.Split('\n'))
        {
            var line=raw.Trim();if(line.Length==0||line.StartsWith('#'))continue;
            // Linear matching keeps a hostile local mapping from blocking the UI thread.
            bool valid;
            try { valid=line.Length<=4096 && System.Text.RegularExpressions.Regex.IsMatch(line,@"^(?:[0-9a-fA-F]{32}|xinput),[^,\r\n]+,(?:[+\-]?[a-zA-Z0-9_]+:[a-zA-Z0-9_+~.\- ]+,?)+$",System.Text.RegularExpressions.RegexOptions.NonBacktracking|System.Text.RegularExpressions.RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)); }
            catch(System.Text.RegularExpressions.RegexMatchTimeoutException){valid=false;}
            if(!valid)throw new HubException("An SDL mapping line is malformed. Keep the device GUID, name and button mappings on one line.");
            count++;
        }
        if(count==0)throw new HubException("No SDL controller mappings were found.");
    }
}
