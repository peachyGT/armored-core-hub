using System.Runtime.InteropServices;

namespace Hub.Core;

// Emulator-native bindings only. This is not a mouse hook or an input-injection helper.
public static class InputDefaults
{
    public static readonly Dictionary<string, string> Keyboard = new()
    {
        ["Up"]="Up", ["Down"]="Down", ["Left"]="Left", ["Right"]="Right",
        ["Cross"]="X", ["Circle"]="C", ["Square"]="Z", ["Triangle"]="V",
        ["Start"]="Return", ["Select"]="Space", ["L1"]="Q", ["R1"]="E", ["L2"]="R", ["R2"]="T",
        ["L3"]="F", ["R3"]="G", ["LUp"]="W", ["LDown"]="S", ["LLeft"]="A", ["LRight"]="D",
        ["RUp"]="I", ["RDown"]="K", ["RLeft"]="J", ["RRight"]="L"
    };

    public static int[] ConnectedXInputSlots()
    {
        if (!OperatingSystem.IsWindows()) return [];
        var result = new List<int>();
        try { for (uint i=0;i<4;i++) if (XInputGetState(i, new byte[16]) == 0) result.Add((int)i); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        return result.ToArray();
    }
    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint index, byte[] state);

    public static Dictionary<string,string> RpcS3(bool copilotSupported, int[] slots)
    {
        var result = new Dictionary<string,string>();
        void Player(int number, string handler, string device, bool copilot)
        {
            var prefix = $"Player {number} Input/";
            result[prefix+"Handler"] = handler; result[prefix+"Device"] = device;
            if (copilot) result[prefix+"Config/Device Class Type"] = "40964"; // upstream CELL_PAD_FAKE_TYPE_COPILOT_1
            if (handler != "Keyboard") return; // upstream handler initializes its own physical-button defaults
            foreach (var (button,key) in Keyboard)
            {
                var name = button switch { "LUp"=>"Left Stick Up", "LDown"=>"Left Stick Down", "LLeft"=>"Left Stick Left", "LRight"=>"Left Stick Right",
                    "RUp"=>"Right Stick Up", "RDown"=>"Right Stick Down", "RLeft"=>"Right Stick Left", "RRight"=>"Right Stick Right", _=>button };
                result[prefix+"Config/"+name] = key;
            }
        }
        if (!copilotSupported && slots.Length > 0) Player(1, "XInput", $"XInput Pad #{slots[0]+1}", false);
        else
        {
            Player(1, "Keyboard", "Keyboard", false);
            // Keep keyboard connected as player one. Native co-pilots can connect/disconnect
            // without losing keyboard input or occupying extra in-game player slots.
            if (copilotSupported) for (var i=0;i<4;i++) Player(i+2, "XInput", $"XInput Pad #{i+1}", true);
        }
        return result;
    }

    public static void Xenia(Dictionary<string,string> settings)
    {
        settings["HID/hid"]="any"; settings["HID/keyboard_mode"]="1"; settings["HID/keyboard_user_index"]="0";
        foreach(var (name,key) in new Dictionary<string,string> {
            ["start"]="0x0D", ["back"]="0x20", ["a"]="X", ["b"]="C", ["x"]="Z", ["y"]="V",
            ["dpad_up"]="0x26", ["dpad_down"]="0x28", ["dpad_left"]="0x25", ["dpad_right"]="0x27",
            ["left_thumb_up"]="W", ["left_thumb_down"]="S", ["left_thumb_left"]="A", ["left_thumb_right"]="D",
            ["right_thumb_up"]="I", ["right_thumb_down"]="K", ["right_thumb_left"]="J", ["right_thumb_right"]="L",
            ["left_thumb"]="F", ["right_thumb"]="G", ["left_shoulder"]="Q", ["right_shoulder"]="E", ["left_trigger"]="R", ["right_trigger"]="T" }) settings["HID.Key/keybind_"+name]=key;
    }

    public static string KeyboardGuide(string backend) => backend == "ppsspp"
        ? "Start: Space · Select: V\nD-pad: arrows · Analog stick: I / J / K / L\nCross: Z · Circle: X · Square: A · Triangle: S\nShoulders: Q / W"
        : "Start: Enter · Select / Back: Space\nD-pad: arrows · Left stick: W / A / S / D · Right stick: I / J / K / L\nCross / A: X · Circle / B: C · Square / X: Z · Triangle / Y: V\nShoulders: Q / E · Triggers: R / T · Stick clicks: F / G";
}
