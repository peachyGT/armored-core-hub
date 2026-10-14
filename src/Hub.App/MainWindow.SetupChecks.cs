using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hub.Core;

namespace Hub.App;

public partial class MainWindow
{
    private async Task CheckSetup(Func<string,Task> capture,List<string> checks)
    {
        var saved=JsonData.Read<HubState>(paths.Managed("config","state.json"));
        try
        {
            ClearFilters(this,new());GameList.SelectedItem=rows.Single(r=>r.Id=="ac2");
            EditionBox.SelectedValue="ac2-ps2-slus20014";
            var selected=edition??throw new Exception("AC2 setup fixture edition unavailable");
            var pup=paths.Managed("cache","ui-setup","other-console.pup");Directory.CreateDirectory(Path.GetDirectoryName(pup)!);File.WriteAllText(pup,"synthetic fixture");
            state.Firmware=[new("setup-other","ps3-firmware",paths.Portable(pup),"","PlayStation 3 system software",false,"Global")];
            state.Builds=[];state.Media=[];Current.BuildId=null;Current.MediaId=null;
            Width=960;Height=640;ShowSetup(this,new());
            if(setupChecklist?.Ready!=false||setupChecklist.Requirements.Any(r=>r.Complete)||setupChecklist.Requirements.Single(r=>r.Id=="firmware").State!="Missing")throw new Exception("PS2 setup shows unrelated firmware or a false ready check");
            if(!UtilityContent.Children.OfType<Button>().Any(b=>b.Content as string=="Add game files…"))throw new Exception("Setup lacks contextual game import");
            await capture("setup-ps2-missing-960");checks.Add("Selected-game setup excludes unrelated PS3 firmware and points to missing game files");
            var mediaFile=paths.Managed("cache","ui-setup","disc.iso");File.WriteAllText(mediaFile,"synthetic fixture");var runtimeHome=paths.Managed("cache","ui-setup","runtime");Directory.CreateDirectory(runtimeHome);File.WriteAllText(Path.Combine(runtimeHome,Backend.Executable),"synthetic fixture");
            var release=RecommendedBuilds.For(Profile,Backend)!;state.Builds=[new("setup-build",Backend.Id,release.Version,paths.Portable(runtimeHome),Backend.Executable,release.Sha256,"","fixture",DateTimeOffset.UtcNow)];
            state.Media=[new("setup-media",paths.Portable(mediaFile),"iso",selected.Id,"",MediaConfidence.UnknownRevision,"fixture",[],DateTimeOffset.UtcNow,false)];Current.MediaId="setup-media";
            ShowSetup(this,new());
            if(setupChecklist?.NextRepair!="firmware"||setupChecklist.Requirements.Count(r=>r.Complete)!=2||!UtilityContent.Children.OfType<Button>().Any(b=>b.Content as string=="Import system files…"))throw new Exception("Setup does not advance to BIOS import");
            await capture("setup-ps2-bios-needed-960");
            var bios=paths.Managed("cache","ui-setup","bios.bin");File.WriteAllText(bios,"synthetic fixture");state.Firmware.Add(new("setup-bios","ps2-bios",paths.Portable(bios),"","PS2 v1.60 · US · structurally recognized",false,"US","0160AC20020207"));
            ShowSetup(this,new());
            if(setupChecklist?.Ready!=true||setupChecklist.Requirements.Any(r=>!r.Complete)||!UtilityContent.Children.OfType<Button>().Any(b=>b.Content as string=="Play"))throw new Exception("Setup does not become ready with all three available requirements");
            await capture("setup-ps2-ready-960");checks.Add("Setup refreshes from two available requirements to three green checks with Play and return actions");
            Width=820;await capture("setup-ps2-ready-820");
        }
        finally
        {
            state.Media=saved.Media;state.Builds=saved.Builds;state.Firmware=saved.Firmware;state.Selections=saved.Selections;state.PreferredEditions=saved.PreferredEditions;
            Save();RefreshGames();RefreshDetail();ShowCollection(this,new());
        }
    }
}
