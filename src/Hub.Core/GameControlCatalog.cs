namespace Hub.Core;

public sealed record ControlScheme(string Id,string Name,string Notes,string[] Sources,Dictionary<string,string> Actions);

// These are the game's inputs, not an emulator's interchangeable button names.
// The source is the selected game's manual. In-game custom layouts cannot be
// detected from emulator input configuration, so the user declares that layout.
public static class GameControlCatalog
{
    private const string ManualRoot="https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/";
    private static readonly Dictionary<string,string> Manuals=new()
    {
        ["ac1"]="004_ARMORED_CORE.pdf",["acpp"]="005_ARMORED_CORE_PROJECT_PHANTASMA.pdf",["acmoa"]="008_ARMORED_CORE_MASTER_OF_ARENA.pdf",
        ["ac2"]="015_ARMORED_CORE_2.pdf",["ac2aa"]="017_ARMORED_CORE_%202_ANOTHER_AGE.pdf",["ac3"]="020_ARMORED_CORE_3.pdf",["acsl"]="024_ARMORED_CORE_3_SILENT_LINE.pdf",
        ["acn"]="031_ARMORED_CORE_NEXUS.pdf",["acnb"]="035_ARMORED_CORE_NINE_BREAKER.pdf",["aclr"]="043_ARMORED_CORE_LAST_RAVEN.pdf",["acff"]="040_ARMORED_CORE_FORMULA_FRONT.pdf",
        ["ac4"]="054_ARMORED_CORE_4_PS4.pdf",["acfa"]="064_ARMORED_CORE_for_Answer_PS3.pdf",["acv"]="087_ARMORED_CORE_V_PS3.pdf",["acvd"]="091_ARMORED_CORE_VERDICT_DAY_PS3.pdf",
        ["ac3-psp"]="074_ARMORED_CORE_3_Portable.pdf",["acsl-psp"]="077_ARMORED_CORE_SILENT_LINE_Portable.pdf",["aclr-psp"]="081_ARMORED_CORE_LAST_RAVEN_Portable.pdf",["acff-psp"]="045_ARMORED_CORE_FORMULA_FRONT_INTERNATIONAL.pdf"
    };
    public static ControlScheme[] Schemes(Edition edition)
    {
        var ids=edition.Platform=="psp" ? edition.GameId=="acff" ? new[]{"original","ai"}:new[]{"original"} : edition.GameId switch
        {
            "ac1" or "acpp" or "acmoa" or "ac2" or "ac2aa" or "ac3" or "acsl"=>["original","analog"],
            "acn" or "acnb" or "aclr"=>["type-a","type-b"],
            "ac4"=>["type-a","type-b"],"acfa"=>["type-a","type-b","simple"],
            "acff"=>["ai","manual"],_=>["original"]
        };
        var automatic=Resolve(edition,new());
        return new[]{automatic with {Id="auto",Name="Game default · "+automatic.Name}}.Concat(ids.Select(id=>Resolve(edition,new(){GameScheme=id}))).ToArray();
    }
    public static ControlScheme Resolve(Edition edition,ControlChoice choice,bool analogPatch=false)
    {
        var id=choice.GameScheme??"auto";
        if(id=="auto")id=edition.Platform=="psp"?"original":edition.GameId switch {"acn" or "acnb" or "aclr" or "ac4" or "acfa"=>"type-a","acff"=>"ai",_=>analogPatch&&Early(edition)?"analog":"original"};
        var actions=ControlProfiles.Buttons(Backend(edition)).ToDictionary(k=>k,k=>Unused(k));
        string name;string note="Match this to the game's own control layout. This selection labels actions; it does not edit your save or the in-game layout.";
        if(edition.Platform=="psp")
        {
            name=id=="ai"?"AI battle view":"Portable original";Portable(actions);
            if(edition.GameId=="acff")
            {
                Set(actions,("Circle","Left arm weapon"),("Triangle","Cycle weapon"),("Down","Use inside part"),("Up","Purge weapon modifier"),("Select","No default combat action"));
                note+=" Formula Front International / Extreme Battle supports manual combat; select Manual in the game. AI battle view has different camera controls. Vertical aiming is automatic in manual mode.";
            }
            if(id=="ai")Ai(actions,false);
        }
        else if(Early(edition))
        {
            name=id=="analog"?"Original + True Analogs":"Original digital";Classic(actions,edition);
            if(id=="analog"){Sticks(actions);note+=" True Analogs changes movement and camera sticks; weapon buttons keep the game's original layout. Enable the matching patch in Patches.";}
        }
        else if(edition.GameId is "acn" or "acnb" or "aclr"||edition.GameId=="acff"&&id=="manual")
        {
            name=id=="type-b"?"Type B · classic":"Type A · dual sticks";
            if(id=="type-b")Classic(actions,edition);else LatePs2(actions);
            if(edition.GameId=="acff"){Set(actions,("Select","Help (menus) / no combat action"));note+=" Manual combat is available in AC Test → Action. Regular battles use AI control.";}
        }
        else if(edition.GameId is "ac4" or "acfa")
        {
            name=id=="simple"?"Simple":edition.GameId=="acfa"?id=="type-b"?"Regular B":"Regular A":id=="type-b"?"Type B":"Type A";Gen4(actions,edition.GameId,id,edition.Platform=="x360");
            if(edition.GameId=="acfa")note+=" For Answer offers Simple, Regular A and Regular B; check the layout chosen during new-game setup.";
            if(edition.GameId=="acfa"&&id=="simple"&&edition.Platform=="ps3")note+=" The official PS3 Simple table lists R1 as right weapon, while the Xbox table lists RB as shoulder weapon. That PS3 entry needs confirmation against the game's KEY ASSIGN screen.";
        }
        else if(edition.GameId is "acv" or "acvd"){name="Original battle controls";Gen5(actions,edition.GameId);note+=" Combat and Scan modes give the triggers different actions.";}
        else if(edition.GameId=="acff"){name="AI battle view";Ai(actions,true);note+=" Formula Front battles are AI driven. AC Test → Action is the separate manual-control mode.";}
        else{name="Unknown game layout";note="Game action labels have not been researched for this title. Native input names are shown.";}
        var manualKey=edition.Platform=="psp"?edition.GameId+"-psp":edition.GameId;
        var sources=Manuals.TryGetValue(manualKey,out var manual)?new[]{ManualRoot+manual}:Array.Empty<string>();
        if(edition.Platform=="x360"&&sources.Length>0)sources=[sources[0].Replace("_PS4.pdf","_Xbox360.pdf").Replace("_PS3.pdf","_Xbox360.pdf")];
        if(edition.Region!="JP"&&edition.Platform=="ps1")
        {
            var western=edition.GameId switch {"ac1"=>"Armored%20Core%20%28USA%29.pdf","acpp"=>"Armored%20Core%20-%20Project%20Phantasma%20%28USA%29.pdf","acmoa"=>"Armored%20Core%20-%20Master%20of%20Arena%20%28USA%29.pdf",_=>null};
            if(western!=null)sources=[..sources,"https://www.videogamemanual.com/ps1/"+western];
        }
        if(edition.Region!="JP"&&edition.GameId=="ac2")sources=[..sources,"https://www.scribd.com/document/649095851/Armored-Core-2-USA"];
        return new(id,name,note,sources,actions);
    }
    public static string NativeButton(Edition edition,string key)=>edition.Platform=="x360"?key switch {"Cross"=>"A","Circle"=>"B","Square"=>"X","Triangle"=>"Y","L1"=>"LB","R1"=>"RB","L2"=>"LT","R2"=>"RT","L3"=>"LS click","R3"=>"RS click","Select"=>"Back",_=>ControlProfiles.RpcName(key)}:edition.Platform=="psp"?key switch {"L1"=>"L","R1"=>"R",_=>ControlProfiles.RpcName(key)}:ControlProfiles.RpcName(key);
    private static string Backend(Edition e)=>e.Platform switch {"ps1"=>"duckstation","ps2"=>"pcsx2","psp"=>"ppsspp","ps3"=>"rpcs3","x360"=>"xenia",_=>"pcsx2"};
    private static bool Early(Edition e)=>e.GameId is "ac1" or "acpp" or "acmoa" or "ac2" or "ac2aa" or "ac3" or "acsl";
    private static string Unused(string key)=>key is "Up" or "Down" or "Left" or "Right"?"Menu navigation / no combat action":"No default combat action";
    private static void Set(Dictionary<string,string> a,params (string Key,string Action)[] values){foreach(var (key,value) in values)if(a.ContainsKey(key))a[key]=value;}
    private static void Sticks(Dictionary<string,string>a)=>Set(a,("LUp","Move forward"),("LDown","Move backward"),("LLeft","Strafe left"),("LRight","Strafe right"),("RUp","Look up"),("RDown","Look down"),("RLeft","Turn left"),("RRight","Turn right"));
    private static void Classic(Dictionary<string,string>a,Edition e)
    {
        Set(a,("Up","Move forward"),("Down","Move backward"),("Left","Turn left"),("Right","Turn right"),("L1","Strafe left"),("R1","Strafe right"),("L2","Look up"),("R2","Look down"),("Cross","Boost / jump"),("Square","Fire selected weapon"),("Circle","Left arm weapon / interact"),("Triangle","Cycle weapon"),("Start","Pause"),("Select","Map"));
        if(e.Platform!="ps1")Set(a,("L3","Toggle extension"),("R3",e.GameId is "ac2" or "ac2aa"?"Overboost":"Overboost / Exceed Orbit"));
        if(e.Region=="JP"&&e.GameId is "ac1" or "acpp" or "acmoa" or "ac2" or "ac2aa")Set(a,("Start","Map"),("Select","Pause"));
    }
    private static void LatePs2(Dictionary<string,string>a){Sticks(a);Set(a,("L1","Left arm weapon / interact"),("R1","Fire selected weapon"),("L2","Boost / jump"),("R2","Cycle weapon"),("Square","Reset camera"),("Cross","Use inside part"),("Triangle","Purge weapon modifier"),("L3","Toggle extension"),("R3","Overboost / Exceed Orbit"),("Start","Pause"),("Select","Map"));}
    private static void Portable(Dictionary<string,string>a)=>Set(a,("LUp","Move forward"),("LDown","Move backward"),("LLeft","Turn left"),("LRight","Turn right"),("L1","Strafe left"),("R1","Strafe right"),("Triangle","Look up"),("Circle","Look down"),("Cross","Boost / jump"),("Square","Fire selected weapon"),("Up","Cycle weapon"),("Down","Left arm weapon / interact"),("Left","Overboost / Exceed Orbit"),("Right","Toggle extension"),("Start","Pause"),("Select","Map"));
    private static void Gen4(Dictionary<string,string>a,string game,string id,bool xbox)
    {
        Sticks(a);Set(a,("Cross","Right arm weapon"),("Square","Left arm weapon"),("Circle","Shoulder weapon"),("Triangle",game=="acfa"?"Overboost / Assault Armor modifier":"Overboost"),("L1","Switch left weapon / reload (hold)"),("R1","Switch right weapon / reload (hold)"),("L2","Boost"),("R2","Quick boost"),("L3",game=="acfa"?"Change lock mode":"Target lock / lock mode (hold)"),("R3","Reset camera"),("Start","Pause"));
        if(id=="type-b")Set(a,("L1","Left arm weapon"),("R1","Right arm weapon"),("Cross","Switch right weapon / reload (hold)"),("Square","Switch left weapon / reload (hold)"));
        if(id=="simple")Set(a,("Circle","Switch right weapon / reload (hold)"),("Cross","Right arm weapon"),("Square","Left arm weapon"),("Triangle","Switch left weapon / reload (hold)"),("L1","Overboost / Assault Armor modifier"),("L2","Boost / jump"),("R1","Right arm weapon"),("LUp","Move forward / boost when fully tilted"),("LDown","Move backward / boost when fully tilted"),("LLeft","Turn left"),("LRight","Turn right"),("RLeft","No default combat action"),("RRight","No default combat action"),("L3","No default combat action"));
        if(id=="simple")Set(a,("R1",xbox?"Shoulder weapon":"Right arm weapon (manual; verify in game)"));
    }
    private static void Gen5(Dictionary<string,string>a,string game){Sticks(a);Set(a,("Cross","Jump / Boost Drive"),("Circle","Deploy recon"),("Square","High boost / Boost Charge (hold)"),("Triangle",game=="acv"?"Ultimate weapon / switch bay modifier":"Switch weapon bay modifier"),("L1","Toggle booster"),("L2","Left arm weapon / Spot (Scan Mode)"),("R1","Shoulder weapon"),("R2","Right arm weapon / Scan (Scan Mode)"),("L3","Glide boost"),("R3","Change Combat / Scan mode"),("Start","Pause"),("Select","Open menu"),("Left","Buzzer"),("Right","Text chat"),("Down","Purge weapon modifier"));}
    private static void Ai(Dictionary<string,string>a,bool ps2)
    {
        foreach(var key in a.Keys.ToArray())a[key]=Unused(key);
        Set(a,("Up","Cycle battle display"),("Down","Change display opacity"),("Left","Decrease battle speed"),("Right","Increase battle speed"),("Circle","Active camera view"),("Cross","Behind camera view"),("Triangle","Manual camera view"),("Square","Versus camera view"),("L1","Zoom out"),("R1","Zoom in"),("LUp","Rotate camera up"),("LDown","Rotate camera down"),("LLeft","Rotate camera left"),("LRight","Rotate camera right"),("Start","Pause / key guide"));
        if(ps2)Set(a,("Select","Help (menus) / no combat action"));
    }
}
