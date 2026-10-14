using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hub.Core;

namespace Hub.App;
public partial class MainWindow
{
    private async Task CheckAchievementLibrary(Func<string, Task> capture, List<string> checks)
    {
        var chosen = catalog.Editions.First(e => e.GameId == "acvd" && e.Platform == "ps3");
        var file = paths.Managed("achievements", chosen.Id, "ui-fixture.json");
        var snapshot = new AchievementSnapshot(chosen.Id, "RPCS3", "Synthetic UI account", "fixture", DateTimeOffset.UtcNow,
            [new("0", "Synthetic completion", "UI fixture only", 0, true, null, Grade: "Platinum"), new("1", "Synthetic gold", "UI fixture only", 0, false, null, Grade: "Gold"), new("2", "Synthetic silver", "UI fixture only", 0, true, null, Grade: "Silver"), new("3", "Synthetic bronze", "UI fixture only", 0, false, null, Grade: "Bronze")]);
        JsonData.WriteAtomic(file, snapshot);
        try
        {
            CloseGameSettings(this, new()); Width = 1280; Height = 800;
            ShowAchievementLibrary(this, new()); await capture("achievements-library-1280");
            if (AchievementsNav.Tag as string != "active" || LibraryNav.Tag != null || !AchievementSummary(snapshot).Contains("Platinum 1 / 1")) throw new Exception("Achievement collection navigation/grades");
            ShowAchievementPage(chosen.Id); Width = 960; Height = 540; await capture("achievements-game-960");
            if (UtilityView.Visibility != Visibility.Visible || UtilityView.ViewportHeight < 80) throw new Exception("Achievement game viewport");
            Width = 820; await capture("achievements-navigation-820");
            var achievementRight = AchievementsNav.TranslatePoint(new Point(AchievementsNav.ActualWidth, 0), this).X;
            var settingsLeft = SettingsNav.TranslatePoint(new Point(), this).X;
            if (achievementRight >= settingsLeft || EmulatorsNavLabel.Visibility != Visibility.Collapsed || HelpNavLabel.Visibility != Visibility.Collapsed) throw new Exception("Achievement navigation overlaps actions");
            checks.Add("Collection and selected-game trophy navigation, grade counts and 820/960/1280 layouts work");
        }
        finally { File.Delete(file); ShowCollection(this, new()); Width = 1280; Height = 768; }
    }
}
