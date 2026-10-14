using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Hub.App;

public partial class MainWindow
{
    private int captionColorResult, captionTextResult;
    private void InitializeWindowChrome()
    {
        var icon=BitmapFrame.Create(new Uri("pack://application:,,,/Assets/Hub.ico"),BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
        icon.Freeze();Icon=icon;
        SourceInitialized += (_, _) => ApplyTitlebarColors();
        Activated += (_, _) => ApplyTitlebarColors();
        Deactivated += (_, _) => ApplyTitlebarColors();
    }

    private void ApplyTitlebarColors() => (captionColorResult, captionTextResult) = NativeWindowTheme.Apply(this);

    private void GameHoverChanged(object sender, MouseEventArgs e)
    {
        if (sender is ListBoxItem item && item.Template.FindName("HoverWash", item) is Border wash)
        {
            var animate = !SystemParameters.HighContrast && SystemParameters.ClientAreaAnimation && (RenderCapability.Tier >> 16) > 0;
            FadeHover(wash, item.IsMouseOver, animate);
            if (item.Template.FindName("HoverLift", item) is Grid lift) LiftHover(HoverTransform(lift), item.IsMouseOver, animate);
        }
    }

    private static TranslateTransform HoverTransform(Grid lift)
    {
        // WPF freezes transforms in shared templates until each item gets its own instance.
        if (lift.RenderTransform is TranslateTransform transform && !transform.IsFrozen) return transform;
        var local = new TranslateTransform(); lift.RenderTransform = local; return local;
    }
    private static void LiftHover(TranslateTransform transform, bool hovered, bool animate)
    {
        var from = transform.Y;
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        transform.Y = hovered && animate ? -3 : 0;
        if (!animate || Math.Abs(from - transform.Y) < .001) return;
        var lift = new DoubleAnimation(from, transform.Y, TimeSpan.FromMilliseconds(140))
        { EasingFunction = new ExponentialEase { Exponent = 4, EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        lift.Completed += (_, _) => transform.BeginAnimation(TranslateTransform.YProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, lift);
    }

    private void DividerPointerDown(object sender, MouseButtonEventArgs e) => LibraryDivider.Tag = null;
    private void DividerKeyDown(object sender, KeyEventArgs e) => LibraryDivider.Tag = "keyboard";
    private void DividerLostFocus(object sender, KeyboardFocusChangedEventArgs e) => LibraryDivider.Tag = null;

    private static void FadeHover(Border wash, bool hovered, bool animate)
    {
        var from = wash.Opacity;
        wash.BeginAnimation(OpacityProperty, null);
        wash.Opacity = hovered ? 1 : 0;
        if (!animate || Math.Abs(from - wash.Opacity) < .001) return;
        var fade = new DoubleAnimation(from, wash.Opacity, TimeSpan.FromMilliseconds(120))
        { EasingFunction = new ExponentialEase { Exponent = 4, EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        // Remove the clock at rest; there is no repeating animation or idle timer.
        fade.Completed += (_, _) => wash.BeginAnimation(OpacityProperty, null);
        wash.BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
    }


}
