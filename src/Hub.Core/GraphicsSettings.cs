using System.Globalization;

namespace Hub.Core;

public sealed class GraphicsChoice
{
    public double? Scale { get; set; }
    public int? TargetFps { get; set; }
    public string Screen { get; set; } = "desktop";
}

public static class GraphicsSettings
{
    public static double[] Scales(string backend) => backend switch
    { "rpcs3" => [.5, 1, 1.5, 2, 3, 4], "xenia" => [1, 2, 3], _ => [1, 2, 3, 4, 6, 8] };
    public static int[] FrameRates(string backend) => backend switch
    { "rpcs3" => [30, 50, 60, 120], "xenia" => [30, 60], _ => [] };
    public static readonly string[] Screens = ["desktop", "1280x720", "1600x900", "1920x1080", "2560x1440", "3840x2160"];
    public static string ScaleKey(string backend) => backend switch
    { "duckstation" => "GPU/ResolutionScale", "pcsx2" => "EmuCore/GS/upscale_multiplier", "ppsspp" => "Graphics/InternalResolution", "rpcs3" => "Video/Resolution Scale", "xenia" => "GPU/draw_resolution_scale_x", _ => throw new HubException("Unknown graphics adapter.") };
    public static void Apply(Dictionary<string,string> settings, string backend, string game, GraphicsChoice choice)
    {
        if (!Screens.Contains(choice.Screen)) throw new HubException("Choose a supported screen size.");
        if (choice.Scale is double scale)
        {
            if (!Scales(backend).Contains(scale)) throw new HubException("This emulator does not support that resolution scale.");
            settings[ScaleKey(backend)] = (backend == "rpcs3" ? scale * 100 : scale).ToString(CultureInfo.InvariantCulture);
            if (backend == "xenia") settings["GPU/draw_resolution_scale_y"] = scale.ToString(CultureInfo.InvariantCulture);
        }
        if (choice.TargetFps is int fps)
        {
            if (!FrameRates(backend).Contains(fps)) throw new HubException("This emulator has no independent frame-rate limit for this game. A compatible game patch is needed.");
            if (backend == "rpcs3") settings["Video/Frame limit"] = fps.ToString(CultureInfo.InvariantCulture);
            else
            {
                // ACV submits at half the emulated vblank cadence; sampled at 30/58 FPS.
                settings["GPU/framerate_limit"] = (game == "acv" ? fps * 2 : fps).ToString(CultureInfo.InvariantCulture);
                settings["GPU/vsync"] = "false";
            }
        }
    }
}
