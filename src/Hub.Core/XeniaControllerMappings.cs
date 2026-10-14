namespace Hub.Core;

// SDL reserves "xinput" for all Windows XInput devices. The raw indices below are
// SDL2's documented XInput joystick order, not a guessed USB device GUID.
public static class XeniaControllerMappings
{
    private static readonly Dictionary<string,string> Outputs=new()
    {
        ["Cross"]="a",["Circle"]="b",["Square"]="x",["Triangle"]="y",["Start"]="start",["Select"]="back",
        ["L1"]="leftshoulder",["R1"]="rightshoulder",["L2"]="lefttrigger",["R2"]="righttrigger",["L3"]="leftstick",["R3"]="rightstick",
        ["Up"]="dpup",["Down"]="dpdown",["Left"]="dpleft",["Right"]="dpright",
        ["LLeft"]="-leftx",["LRight"]="+leftx",["LUp"]="-lefty",["LDown"]="+lefty",
        ["RLeft"]="-rightx",["RRight"]="+rightx",["RUp"]="-righty",["RDown"]="+righty"
    };
    private static readonly Dictionary<string,string> Inputs=new()
    {
        ["A"]="b0",["B"]="b1",["X"]="b2",["Y"]="b3",["LeftShoulder"]="b4",["RightShoulder"]="b5",["Back"]="b6",["Start"]="b7",["LeftStick"]="b8",["RightStick"]="b9",
        ["DPadUp"]="h0.1",["DPadRight"]="h0.2",["DPadDown"]="h0.4",["DPadLeft"]="h0.8",
        ["-LeftX"]="-a0",["+LeftX"]="+a0",["-LeftY"]="-a1",["+LeftY"]="+a1",["+LeftTrigger"]="a2",
        ["-RightX"]="-a3",["+RightX"]="+a3",["-RightY"]="-a4",["+RightY"]="+a4",["+RightTrigger"]="a5"
    };
    public static string Create(Dictionary<string,string> controller)
    {
        ControlProfiles.Validate(controller,false);
        var bindings=new Dictionary<string,string>(ControlProfiles.Pad);foreach(var(key,value)in controller)bindings[key]=value;
        return "xinput,Armored Core Hub XInput,"+string.Join(',',Outputs.Select(pair=>pair.Value+":"+Inputs[bindings[pair.Key]]))+",guide:b10,platform:Windows,\n";
    }
    public static void ApplyEnvironment(Dictionary<string,string> environment,string mapping)
    {
        if(string.IsNullOrWhiteSpace(mapping)||!mapping.Split('\n').Where(l=>l.Length>0&&!l.StartsWith('#')).All(l=>l.StartsWith("xinput,",StringComparison.Ordinal)))return;
        // Set only on the launched emulator. Force the reserved mapping's driver instead
        // of SDL's raw/HID driver choosing another device-specific mapping first.
        environment["SDL_JOYSTICK_HIDAPI"]="0";
        environment["SDL_JOYSTICK_RAWINPUT"]="0";
        environment["SDL_JOYSTICK_WGI"]="0";
        environment["SDL_XINPUT_ENABLED"]="1";
        environment["SDL_XINPUT_USE_OLD_JOYSTICK_MAPPING"]="0";
    }
}
