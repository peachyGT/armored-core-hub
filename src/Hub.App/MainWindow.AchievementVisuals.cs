using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hub.Core;

namespace Hub.App;
public partial class MainWindow
{
    private readonly Dictionary<(string Icon,bool Locked),BitmapSource> achievementImages=[];
    private BitmapSource? AchievementImage(AchievementEntry item)
    {
        if(item.Icon==null||item.Hidden&&!item.Unlocked)return null;
        var cacheKey=(item.Icon,!item.Unlocked);
        if(achievementImages.TryGetValue(cacheKey,out var cached))return cached;
        try
        {
            var file=new AchievementIconStore(paths).TryPath(item.Icon);if(file==null)return null;
            using var input=File.OpenRead(file);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=128;bitmap.StreamSource=input;bitmap.EndInit();bitmap.Freeze();BitmapSource image=bitmap;
            if(!item.Unlocked)
            {
                var source=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);var stride=source.PixelWidth*4;var pixels=new byte[stride*source.PixelHeight];source.CopyPixels(pixels,stride,0);
                for(var p=0;p<pixels.Length;p+=4)
                {
                    var dim=(byte)((pixels[p+2]*.2126+pixels[p+1]*.7152+pixels[p]*.0722)*.26);
                    pixels[p]=pixels[p+1]=pixels[p+2]=dim;
                }
                image=BitmapSource.Create(source.PixelWidth,source.PixelHeight,96,96,PixelFormats.Bgra32,null,pixels,stride);image.Freeze();
            }
            if(achievementImages.Count>=128)achievementImages.Remove(achievementImages.Keys.First());achievementImages[cacheKey]=image;return image;
        }
        catch(Exception e)when(e is IOException or InvalidDataException or NotSupportedException or ArgumentException or HubException){return null;}
    }
    private Border AchievementTile(AchievementEntry item)
    {
        var secret=item.Hidden&&!item.Unlocked;var title=secret?"Secret achievement":item.Title;var description=secret?"Unlock this achievement to reveal its details.":item.Description;
        var status=item.Unlocked?"Unlocked":"Locked";var grade=item.Grade??(item.Points>0?item.Points+" points":"");var label=status+" · "+title+(grade.Length>0?" · "+grade:"")+(item.Hardcore?" · Hardcore":"");
        var tile=new Border{Width=164,MinHeight=150,Padding=new(10),Margin=new(0,0,12,12),BorderThickness=new(1),BorderBrush=Brushes.Transparent,Background=(Brush)FindResource("Ground"),Focusable=true,FocusVisualStyle=null,Tag=item};
        AutomationProperties.SetName(tile,label);AutomationProperties.SetHelpText(tile,description.Length>0?description:"No description supplied by this achievement provider.");
        var content=new StackPanel();tile.Child=content;
        var art=new Border{Width=68,Height=68,Background=SystemParameters.HighContrast?SystemColors.WindowBrush:Brushes.Black,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,0,0,10)};
        if(AchievementImage(item) is {} image)art.Child=new Image{Source=image,Stretch=Stretch.Uniform};
        else
        {
            var color=(Brush)FindResource("Muted");
            if(item.Unlocked&&!SystemParameters.HighContrast)color=item.Grade switch{"Bronze"=>new SolidColorBrush(Color.FromRgb(210,158,114)),"Silver"=>new SolidColorBrush(Color.FromRgb(207,215,223)),"Gold"=>new SolidColorBrush(Color.FromRgb(229,201,134)),"Platinum"=>new SolidColorBrush(Color.FromRgb(179,223,232)),_=>(Brush)FindResource("Ink")};
            art.Child=new System.Windows.Shapes.Path{Data=(Geometry)FindResource("TrophyGeometry"),Stroke=color,StrokeThickness=1.5,Width=30,Height=30,Stretch=Stretch.Uniform,Opacity=item.Unlocked||SystemParameters.HighContrast?1:.5,ToolTip=secret?"Secret achievement":"Artwork unavailable"};
        }
        content.Children.Add(art);
        content.Children.Add(new TextBlock{Text=title,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,MaxHeight=40,TextTrimming=TextTrimming.CharacterEllipsis,Foreground=(Brush)FindResource("Ink")});
        content.Children.Add(new TextBlock{Text=status+(grade.Length>0?" · "+grade:""),FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=(Brush)FindResource("Muted"),Margin=new(0,5,0,0)});
        var detail=new StackPanel{MaxWidth=340};detail.Children.Add(new TextBlock{Text=title,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
        detail.Children.Add(new TextBlock{Text=description.Length>0?description:"No description supplied by this achievement provider.",TextWrapping=TextWrapping.Wrap,Margin=new(0,6,0,0)});
        if(item.Unlocked&&!string.IsNullOrWhiteSpace(item.EarnedAt))detail.Children.Add(new TextBlock{Text="Earned "+item.EarnedAt,Margin=new(0,6,0,0),TextWrapping=TextWrapping.Wrap});
        var tooltip=new ToolTip{Content=detail,Background=(Brush)FindResource("Raised"),Foreground=(Brush)FindResource("Ink"),BorderBrush=(Brush)FindResource("Rule"),Padding=new(12),PlacementTarget=tile,Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom};
        tile.ToolTip=tooltip;ToolTipService.SetInitialShowDelay(tile,350);ToolTipService.SetShowDuration(tile,30000);
        tile.MouseEnter+=(_,_)=>{if(!tile.IsKeyboardFocused)tile.BorderBrush=(Brush)FindResource("Rule");};
        tile.MouseLeave+=(_,_)=>{if(!tile.IsKeyboardFocused)tile.BorderBrush=Brushes.Transparent;};
        tile.GotKeyboardFocus+=(_,_)=>{tile.BorderBrush=(Brush)FindResource("Accent");tile.BringIntoView();tooltip.IsOpen=true;};
        tile.LostKeyboardFocus+=(_,_)=>{tile.BorderBrush=Brushes.Transparent;tooltip.IsOpen=false;};
        tile.KeyDown+=(_,e)=>{if(e.Key==Key.Escape&&tooltip.IsOpen){tooltip.IsOpen=false;e.Handled=true;}};
        return tile;
    }
}
