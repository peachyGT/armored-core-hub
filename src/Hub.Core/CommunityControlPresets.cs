namespace Hub.Core;

// Optional Hub templates: source-supported priorities, with explicitly inferred keyboard
// bindings. Map the selected game's original actions; never change its internal key layout.
public static class CommunityControlPresets
{
    private const string OldSource="https://github.com/VanLaser/analog-controls-for-armored-core-ps2";
    private const string FourthSource="https://gamefaqs.gamespot.com/boards/944263-armored-core-for-answer/46957723";
    private const string FifthSource="https://gamefaqs.gamespot.com/ps3/704812-armored-core-verdict-day/faqs/69136";
    public static CommunityBindings Get(Edition edition,string scheme="auto")
    {
        var original=GameControlCatalog.Resolve(edition,new ControlChoice{GameScheme=scheme});
        var keyboard=new Dictionary<string,string>(InputDefaults.Keyboard);
        var controller=new Dictionary<string,string>(ControlProfiles.Pad);
        // Directions not used for movement remain available for per-game secondary actions.
        foreach(var(k,v) in new Dictionary<string,string>{{"Up","1"},{"Down","2"},{"Left","3"},{"Right","4"},
            {"LUp","W"},{"LDown","S"},{"LLeft","A"},{"LRight","D"},{"RUp","I"},{"RDown","K"},{"RLeft","J"},{"RRight","L"},
            {"Start","Return"},{"Select","Tab"}})keyboard[k]=v;
        var desired=new Dictionary<string,string>();
        foreach(var(button,action) in original.Actions)
        {
            if(!controller.ContainsKey(button))continue;
            var caption=action.ToLowerInvariant();
            if(Keyboard(caption) is {} key)keyboard[button]=key;
            if(Controller(caption) is {} input)desired[button]=input;
        }
        // Important combat actions reserve their requested physical inputs first. Move any
        // displaced secondary action to a freed face button, rather than firing two actions.
        var reserved=desired.Values.ToHashSet(StringComparer.Ordinal);
        var freed=desired.Keys.Select(k=>controller[k]).Where(v=>!reserved.Contains(v)).Distinct().ToList();
        foreach(var(button,input) in desired)controller[button]=input;
        foreach(var button in original.Actions.Where(a=>a.Value!="No default combat action" && !a.Value.StartsWith("Menu navigation",StringComparison.Ordinal)).Select(a=>a.Key).Where(controller.ContainsKey).Where(b=>!desired.ContainsKey(b)))
            if(reserved.Contains(controller[button]) && freed.Count>0)
            {controller[button]=freed[0];reserved.Add(freed[0]);freed.RemoveAt(0);}
        ControlProfiles.Validate(keyboard,true);ControlProfiles.Validate(controller,false);
        var generation=edition.GameId is "ac4" or "acfa" ? 4 : edition.GameId is "acv" or "acvd" ? 5 : 3;
        var source=generation==4?FourthSource:generation==5?FifthSource:OldSource;
        if(original.Id=="ai")source=original.Sources.FirstOrDefault()??source;
        return new("Community · "+(original.Id=="ai"?"AI camera · ":"Modern ")+original.Name+" (inferred)",source,[edition.Id],keyboard,controller,null,original.Id);
    }
    public static string Description(Edition edition,string scheme="auto")
    {
        if(GameControlCatalog.Resolve(edition,new(){GameScheme=scheme}).Id=="ai")
            return "Optional Formula Front AI battle-view layout: the controller keeps the game's camera, zoom, display and speed commands. The keyboard uses WASD for camera rotation and Enter to pause. Menu inputs follow the original console buttons. This profile does not add manual weapons or movement; select the separate Manual layout for AC Test / manual battle controls. Keyboard placement is inferred from the manual's actions.";
        var note="Optional modern layout adapted to the selected in-game scheme: left stick/WASD movement, right stick/IJKL camera, LT/RT arm weapons, LB boost/jump, RB quick boost or weapon cycle. Keyboard bindings and exact physical permutations are inferred; community sources support the priorities, not one universal consensus. Confirm the same scheme in the game's own settings. Menu commands follow the original console buttons and may move with combat bindings.";
        if(edition.Platform=="psp")note+=" PSP camera directions are digital unless a matching analog patch is installed; choosing this profile does not install one. Confirm purging and menu navigation in-game.";
        else if(edition.GameId is "ac1" or "acpp" or "acmoa" or "ac2" or "ac2aa" or "ac3" or "acsl")note+=" Original stick-to-button movement is digital; a matching enabled True Analogs patch provides actual analog processing. Original menus may use the right stick for horizontal navigation.";
        return note;
    }
    private static string? Keyboard(string action)
    {
        if(action=="pause")return "Return";
        if(action=="map"||action=="open menu")return "Tab";
        if(action.StartsWith("move forward"))return "W";
        if(action.StartsWith("move backward"))return "S";
        if(action.StartsWith("strafe left")||action.StartsWith("move left"))return "A";
        if(action.StartsWith("strafe right")||action.StartsWith("move right"))return "D";
        if(action.StartsWith("turn left"))return "J";
        if(action.StartsWith("turn right"))return "L";
        if(action.StartsWith("look up"))return "I";
        if(action.StartsWith("look down"))return "K";
        if(Arm(action,"right"))return "R";
        if(Arm(action,"left"))return "F";
        if(action.StartsWith("quick boost")||action.StartsWith("high boost"))return "Shift";
        if(action.StartsWith("boost")&&!action.Contains("toggle")||action.StartsWith("jump"))return "Space";
        if(action.StartsWith("switch left"))return "Q";
        if(action.StartsWith("switch right")||action.StartsWith("cycle")&&action.Contains("weapon")||action.StartsWith("change weapon"))return "E";
        if(action.StartsWith("swap weapon")||action.Contains("switch")&&action.Contains("bay modifier"))return "Q";
        if(action.StartsWith("overboost")||action.StartsWith("glide boost"))return "Control";
        if(action.Contains("shoulder weapon"))return "C";
        if(action.StartsWith("toggle boost"))return "B";
        if(action.Contains("scan")&&action.Contains("mode"))return "V";
        if(action.StartsWith("deploy recon"))return "Z";
        if(action.Contains("inside")&&(action.Contains("weapon")||action.Contains("part")))return "Z";
        if(action.Contains("extension"))return "C";
        if(action.StartsWith("reset camera"))return "B";
        if(action.Contains("target lock")||action.Contains("lock mode"))return "V";
        if(action.StartsWith("purge"))return "X";
        return null;
    }
    private static string? Controller(string action)
    {
        if(action.StartsWith("move forward"))return "-LeftY";
        if(action.StartsWith("move backward"))return "+LeftY";
        if(action.StartsWith("strafe left")||action.StartsWith("move left"))return "-LeftX";
        if(action.StartsWith("strafe right")||action.StartsWith("move right"))return "+LeftX";
        if(action.StartsWith("turn left"))return "-RightX";
        if(action.StartsWith("turn right"))return "+RightX";
        if(action.StartsWith("look up"))return "-RightY";
        if(action.StartsWith("look down"))return "+RightY";
        if(Arm(action,"right"))return "+RightTrigger";
        if(Arm(action,"left"))return "+LeftTrigger";
        if(action.StartsWith("quick boost")||action.StartsWith("high boost")||action.StartsWith("cycle")&&action.Contains("weapon")||action.StartsWith("change weapon"))return "RightShoulder";
        if(action.StartsWith("boost")&&!action.Contains("toggle")||action.StartsWith("jump"))return "LeftShoulder";
        return null;
    }
    private static bool Arm(string action,string side)=>(action.Contains(side)||side=="right"&&action.Contains("selected"))&&action.Contains("weapon")
        && !action.Contains("switch")&&!action.Contains("change")&&!action.Contains("cycle")&&!action.Contains("purge")
        && (action.StartsWith(side+" arm")||action.StartsWith("fire "+side)||action.StartsWith("use "+side)||side=="right"&&action.StartsWith("fire selected"));
}
