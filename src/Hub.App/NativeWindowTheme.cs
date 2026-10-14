using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Hub.App;

internal static class NativeWindowTheme
{
    public static (int Caption, int Text) Apply(Window window)
    {
        if(window.Icon==null)
        {var icon=BitmapFrame.Create(new Uri("pack://application:,,,/Assets/Hub.ico"),BitmapCreateOptions.None,BitmapCacheOption.OnLoad);icon.Freeze();window.Icon=icon;}
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return (0, 0);
        var dark = SystemParameters.HighContrast ? 0 : 1;
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return (0, 0);
        int Set(int attribute, string key)
        {
            var color = ((SolidColorBrush)window.FindResource(key)).Color;
            var value = SystemParameters.HighContrast ? -1 : color.R | color.G << 8 | color.B << 16;
            return DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }
        return (Set(35, "Ground"), Set(36, "Ink"));
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
