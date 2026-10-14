using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Hub.Core;

namespace Hub.App;
public partial class MainWindow
{
    private async Task CheckAchievementAccount(Func<string,Task> capture,List<string> checks)
    {
        var preference=paths.Managed("config","achievement-preferences.json");var original=System.IO.File.Exists(preference)?System.IO.File.ReadAllBytes(preference):null;var offline=state.Offline;
        static IEnumerable<DependencyObject> Descend(DependencyObject node)
        {yield return node;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var found in Descend(child))yield return found;}
        try
        {
            new AchievementPreferences().Save(paths);state.Offline=true;CloseGameSettings(this,new());Width=1280;Height=800;ShowAchievementAccount(this,new());
            var controls=Descend(UtilityContent).OfType<Control>().ToList();var password=controls.OfType<PasswordBox>().Single();
            if(AutomationProperties.GetName(password)!="RetroAchievements password · never saved"||password.Password.Length!=0||controls.OfType<Button>().Single(b=>b.Content as string is "Sign in" or "Switch account").IsEnabled)throw new Exception("Offline native account field semantics");
            var enabled=controls.OfType<CheckBox>().Single(b=>b.Content as string=="RetroAchievements enabled for supported games");enabled.IsChecked=false;enabled.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            if(AchievementPreferences.Load(paths).Enabled)throw new Exception("Native achievement global opt-out did not persist");await capture("achievement-account-offline-1280");
            Width=960;Height=540;ShowAchievementAccount(this,new());await capture("achievement-account-offline-960");
            if(UtilityView.ViewportHeight<80||Descend(UtilityContent).OfType<PasswordBox>().Single().ActualWidth>UtilityView.ActualWidth)throw new Exception("Native achievement account narrow layout");
            var signIn=Descend(UtilityContent).OfType<Button>().Single(b=>b.Content as string is "Sign in" or "Switch account");
            if(signIn.TransformToAncestor(UtilityView).Transform(new Point(0,signIn.ActualHeight)).Y>UtilityView.ViewportHeight)throw new Exception("Native achievement sign-in needs scrolling at 960x540");
            ShowCollection(this,new());GameList.SelectedItem=rows.First(r=>r.Id=="ac1");EditionBox.SelectedValue=catalog.Editions.First(e=>e.GameId=="ac1"&&e.Platform=="ps1"&&e.Region=="US").Id;OpenGameSettings(this,new());DetailsTabs.SelectedItem=AchievementsTab;RefreshAchievements();
            var perGame=AchievementPanel.Children.OfType<CheckBox>().Single(c=>c.Content as string=="Use RetroAchievements for this edition");perGame.IsChecked=false;perGame.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            if(AchievementPreferences.Load(paths).GameEnabled.GetValueOrDefault(edition!.Id,true)||AchievementPanel.Children.OfType<Button>().Any(b=>(b.Content as string)?.Contains("emulator",StringComparison.OrdinalIgnoreCase)==true))throw new Exception("Achievement edition toggle/manual-settings regression");
            checks.Add("Native achievement account has named password/username fields, offline sign-in disabled, persisted global and per-edition opt-outs, no emulator-settings route, and usable 1280/960 layouts");
        }
        finally
        {
            state.Offline=offline;if(original==null)System.IO.File.Delete(preference);else System.IO.File.WriteAllBytes(preference,original);CloseGameSettings(this,new());ShowCollection(this,new());Width=1280;Height=768;
        }
    }
}
