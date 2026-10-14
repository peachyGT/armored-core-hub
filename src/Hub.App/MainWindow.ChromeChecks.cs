using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Hub.App;

public partial class MainWindow
{
    private async Task CheckChrome(Func<string, Task> capture, List<string> checks)
    {
        Width = 1280; Height = 720;
        ShowSetup(this, new()); UpdateLayout();
        var more = UtilityContent.Children.OfType<Expander>().Single();
        more.ApplyTemplate();
        var disclosure = (ToggleButton)more.Template.FindName("Disclosure", more);
        if (disclosure.MinHeight < 44 || ((SolidColorBrush)disclosure.Foreground).Color != ((SolidColorBrush)FindResource("Ink")).Color)
            throw new Exception("Help disclosure readability or hit target");
        UtilityView.ScrollToEnd(); await capture("help-collapsed");
        // Use the control's automation contract, not a direct property assignment.
        var expand = (IExpandCollapseProvider)new ExpanderAutomationPeer(more).GetPattern(PatternInterface.ExpandCollapse);
        expand.Expand(); UpdateLayout();
        if (!more.IsExpanded || disclosure.IsChecked != true) throw new Exception("Help expand interaction");
        UtilityView.ScrollToEnd(); await capture("help-expanded");
        expand.Collapse();
        if (more.IsExpanded || disclosure.IsChecked != false) throw new Exception("Help collapse interaction");
        checks.Add("Help disclosure is readable, has a 44-DIP target, and supports expand/collapse automation");
        ShowCollection(this, new());
        ClearFilters(this, new()); UpdateLayout();
        var container = (ListBoxItem)GameList.ItemContainerGenerator.ContainerFromItem(rows[1]);
        container.ApplyTemplate();
        var wash = (Border)container.Template.FindName("HoverWash", container);
        var lift = HoverTransform((Grid)container.Template.FindName("HoverLift", container));
        LiftHover(lift, true, true);
        FadeHover(wash, true, true);
        await WaitForHover(wash, lift, 1, -3);
        if (VisualTreeHelper.HitTest(container, new Point(container.ActualWidth / 2, container.ActualHeight - 1)) == null)
            throw new Exception("Hover lift moved the bottom pointer target");
        await capture("library-hover");
        FadeHover(wash, false, true); LiftHover(lift, false, true);
        await WaitForHover(wash, lift, 0, 0);
        LiftHover(lift, true, false);
        if (lift.Y != 0 || lift.HasAnimatedProperties) throw new Exception("Reduced motion must not move tiles");
        FadeHover(wash, true, false);
        if (wash.Opacity != 1 || wash.HasAnimatedProperties) throw new Exception("Reduced-motion hover");
        FadeHover(wash, false, false);
        checks.Add("Finite hover lift and fade remove animation clocks; reduced motion never moves tiles");
        LibraryDivider.ApplyTemplate();
        var line = (Border)LibraryDivider.Template.FindName("DividerLine", LibraryDivider);
        var handle = (Border)LibraryDivider.Template.FindName("DragHandle", LibraryDivider);
        if (line.Width != 1 || LibraryDivider.ActualWidth < 7 || handle.Width != 13 || handle.Height != 28 || LibraryDivider.FocusVisualStyle != null)
            throw new Exception("Thin divider or resize handle dimensions");
        LibraryDivider.Focus(); LibraryDivider.Tag = "keyboard"; UpdateLayout();
        if (handle.Opacity != 1) throw new Exception("Keyboard divider focus missing");
        DividerPointerDown(LibraryDivider, null!); UpdateLayout();
        if (LibraryDivider.Tag != null) throw new Exception("Pointer divider focus persisted");
        checks.Add("One-DIP divider retains a seven-DIP drag area and visible three-dot handle");
        using var icon = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Hub.ico")).Stream;
        var decoder = new IconBitmapDecoder(icon, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (Icon == null || !new[] {16, 32, 48, 256}.All(s => decoder.Frames.Any(f => f.PixelWidth == s))) throw new Exception("Application icon sizes");
        checks.Add("Application icon contains native frames from 16 through 256 pixels");
        ApplyTitlebarColors();
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !SystemParameters.HighContrast)
        {
            // These attributes are set-only in DWM; GetWindowAttribute cannot verify them.
            if (captionColorResult != 0 || captionTextResult != 0)
                throw new Exception($"Windows title bar color requests failed: {captionColorResult:X8}/{captionTextResult:X8}");
            checks.Add("Windows 11 DWM accepts the native charcoal caption and ivory text colors");
        }
        Width = 960; Height = 540; await capture("chrome-960");
        Width = 1320; Height = 880; await capture("chrome-final");
    }

    private static async Task WaitForHover(Border wash, TranslateTransform lift, double opacity, double y)
    {
        // WPF completion runs on a render/dispatcher tick, not Task.Delay's clock.
        // Allow scheduling delay in hidden/loaded test windows, but require both
        // exact endpoints and removal of both animation clocks within a bound.
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (elapsed.ElapsedMilliseconds < 1500)
        {
            if (wash.Opacity == opacity && lift.Y == y && !wash.HasAnimatedProperties && !lift.HasAnimatedProperties) return;
            await Task.Delay(25);
        }
        throw new Exception($"Hover did not settle: opacity {wash.Opacity} (expected {opacity}), Y {lift.Y} (expected {y}), clocks {wash.HasAnimatedProperties}/{lift.HasAnimatedProperties}");
    }
}
