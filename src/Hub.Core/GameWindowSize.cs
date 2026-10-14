using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Hub.Core;

internal static class GameWindowSize
{
    public static async Task ApplyAsync(Process process, GraphicsChoice graphics, bool fullscreen, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows() || fullscreen || graphics.Screen=="desktop") return;
        if (!GraphicsSettings.Screens.Contains(graphics.Screen)) throw new HubException("Unsupported game window size.");
        var size=graphics.Screen.Split('x').Select(int.Parse).ToArray();
        for(var i=0;i<100 && !process.HasExited;i++)
        {
            ct.ThrowIfCancellationRequested();process.Refresh();var hwnd=process.MainWindowHandle;
            if(hwnd!=IntPtr.Zero)
            {
                GetWindowThreadProcessId(hwnd,out var owner);if(owner!=process.Id)return;
                var rect=new Rect{Right=size[0],Bottom=size[1]};AdjustWindowRect(ref rect,GetWindowLong(hwnd,-16),false);
                var width=Math.Min(rect.Right-rect.Left,GetSystemMetrics(0));var height=Math.Min(rect.Bottom-rect.Top,GetSystemMetrics(1));
                SetWindowPos(hwnd,IntPtr.Zero,0,0,width,height,0x0002|0x0004|0x0010);return;
            }
            await Task.Delay(100,ct);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h,out int pid);
    [DllImport("user32.dll")] private static extern uint GetWindowLong(IntPtr h,int index);
    [DllImport("user32.dll")] private static extern bool AdjustWindowRect(ref Rect rect,uint style,bool menu);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
}
