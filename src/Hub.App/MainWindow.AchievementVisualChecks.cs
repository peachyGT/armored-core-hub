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
    private async Task CheckAchievementVisuals(Func<string,Task> capture,List<string> checks)
    {
        var pixels=Enumerable.Range(0,16*16).SelectMany(_=>new byte[]{40,150,220,255}).ToArray();var color=BitmapSource.Create(16,16,96,96,PixelFormats.Bgra32,null,pixels,16*4);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(color));using var bytes=new MemoryStream();encoder.Save(bytes);var icons=new AchievementIconStore(paths);var icon=icons.Cache(bytes.ToArray());
        var edition=catalog.Editions.First(e=>e.GameId=="acvd"&&e.Platform=="ps3");var file=paths.Managed("achievements",edition.Id,"visual-fixture.json");
        var locked=new AchievementEntry("0","Locked visual fixture","Provider-style fixture requirement. UI verification only.",10,false,null,Icon:icon);
        var earned=locked with{Id="1",Title="Unlocked visual fixture",Unlocked=true,Grade="Gold"};
        var secret=locked with{Id="2",Title="Spoiler fixture",Description="Spoiler condition",Hidden=true};
        var snapshot=new AchievementSnapshot(edition.Id,"RPCS3","Synthetic visual account","visual-fixture",DateTimeOffset.UtcNow,[locked,earned,secret,new("3","Missing art fixture","UI verification only",0,false,null,Grade:"Bronze")]);
        JsonData.WriteAtomic(file,snapshot);
        try
        {
            CloseGameSettings(this,new());Width=1280;Height=800;ShowAchievementPage(edition.Id);
            var lit=AchievementImage(earned)!;var dim=AchievementImage(locked)!;var litPixels=new byte[lit.PixelWidth*lit.PixelHeight*4];var dimPixels=new byte[dim.PixelWidth*dim.PixelHeight*4];
            new FormatConvertedBitmap(lit,PixelFormats.Bgra32,null,0).CopyPixels(litPixels,lit.PixelWidth*4,0);new FormatConvertedBitmap(dim,PixelFormats.Bgra32,null,0).CopyPixels(dimPixels,dim.PixelWidth*4,0);
            if(dimPixels[0]!=dimPixels[1]||dimPixels[1]!=dimPixels[2]||dimPixels[2]>=litPixels[2]||AchievementImage(secret)!=null||!ReferenceEquals(AchievementImage(earned),lit))throw new Exception("Achievement artwork lock/secret/cache state");
            var tile=AchievementTile(locked);UtilityContent.Children.Add(tile);tile.UpdateLayout();Keyboard.Focus(tile);await Task.Delay(60);
            if(tile.ToolTip is not ToolTip tooltip||!tooltip.IsOpen||!AutomationProperties.GetHelpText(tile).Contains("Provider-style"))throw new Exception("Achievement focus description");
            var hidden=AchievementTile(secret);if(AutomationProperties.GetName(hidden).Contains("Spoiler")||AutomationProperties.GetHelpText(hidden).Contains("Spoiler"))throw new Exception("Secret achievement leaked through accessibility");
            await capture("achievement-icons-and-focus-1280");tooltip.IsOpen=false;UtilityContent.Children.Remove(tile);Width=960;Height=540;await capture("achievement-icons-960");
            if(UtilityView.ViewportHeight<80)throw new Exception("Achievement tile viewport");
            checks.Add("Provider artwork uses cached full color when unlocked and dim grayscale when locked; secrets stay hidden, focus reveals real requirements, and 960/1280 layouts remain usable");
        }
        finally{File.Delete(file);achievementImages.Clear();ShowCollection(this,new());Width=1280;Height=768;}
    }
}
