using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hub.Core;

namespace Hub.App;

public partial class MainWindow
{
    private sealed record MoviePreviewCheck(string Edition, GamePresentationAssets Assets, bool Passed);
    private async Task CheckLocalMoviePreviews(Func<string,Task> capture,List<string> checks)
    {
        if(Environment.GetEnvironmentVariable("HUB_TEST_MOVIE_LIBRARY") is not string root)return;
        var fixtures=new HubPaths(root);var library=new LibraryArtwork(fixtures);
        var previews=JsonData.Read<List<MoviePreviewCheck>>(fixtures.Managed("logs","movie-presentation-smoke.json"));
        foreach(var preview in previews)
        {
            if(!preview.Passed)throw new Exception("Real movie preparation failed");
            var selected=catalog.Editions.Single(e=>e.Id==preview.Edition);
            ClearFilters(this,new());GameList.SelectedItem=rows.Single(r=>r.Id==selected.GameId);EditionBox.SelectedValue=selected.Id;
            var image=library.Merge(new(null,null,LibraryArtwork.Neutral),preview.Assets);
            if(image.Background==null||image.Video==null)throw new Exception("Prepared movie/still missing");
            ++presentationRevision;Width=1280;Height=720;ApplyPresentation(image,true);
            var until=DateTime.UtcNow.AddSeconds(5);while(!videoReady&&failedVideo==null&&DateTime.UtcNow<until)await Task.Delay(60);
            if(!videoReady)throw new Exception("Real converted movie did not open: "+selected.GameId+" "+videoFailureDetail);
            SceneVideo.Play();await Task.Delay(200);
            if(SceneVideo.Position<=TimeSpan.Zero)throw new Exception("Real movie did not advance: "+selected.GameId);
            videoPaused=true;UpdateVideoPlayback();await capture("movie-"+selected.GameId+"-1280");
            Width=960;Height=540;await capture("movie-"+selected.GameId+"-960");
            checks.Add("Real "+selected.GameId+" ISO-derived movie opens/advances and its still shares the 1280/960 scene");
        }
        RefreshDetail();
    }
    private async Task CheckPresentation(Func<string, Task> capture, List<string> checks)
    {
        ClearFilters(this, new()); GameList.SelectedItem = rows.Single(r => r.Id == "acmoa");
        Width = 1280; Height = 720; await capture("unadded-small-cover");
        if (GameTitle.Text != "Armored Core" || GameSubtitle.Text != "Master of Arena" || SceneArt.Source != null || ArtBlur.Source != null) throw new Exception("Unadded presentation or split title");
        if (LibraryNav.Tag as string != "active") throw new Exception("Library navigation underline");
        ShowRuntimes(this, new()); await capture("emulators-navigation");
        if (EmulatorsNav.Tag as string != "active" || LibraryNav.Tag != null) throw new Exception("Emulator navigation underline");
        ShowCollection(this, new()); checks.Add("Native navigation uses icons/underlines; unadded games keep small covers and two-line titles");
        GameList.SelectedItem = rows.Single(r => r.Id == "acfa");
        var fixture = paths.Managed("cache", "presentation-fixture-" + Guid.NewGuid().ToString("N"), "PS3_GAME"); Directory.CreateDirectory(fixture);
        var original = artwork.Get("acfa", "acfa");
        if (original.Background != null)
        {
            var entries = JsonData.Read<Dictionary<string, ArtworkEntry>>(paths.Managed("artwork", "index.json"));
            File.Copy(paths.Managed("artwork", entries["acfa"].Background!), Path.Combine(fixture, "PIC1.PNG"), true);
        }
        else
        {
            var pixels = Enumerable.Repeat(new byte[] { 120, 65, 25, 255 }, 128 * 72).SelectMany(x => x).ToArray();
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(128, 72, 96, 96, PixelFormats.Bgra32, null, pixels, 128 * 4)));
            using var output = File.Create(Path.Combine(fixture, "PIC1.PNG")); encoder.Save(output);
        }
        var source = Path.GetDirectoryName(fixture)!;
        Directory.CreateDirectory(Path.Combine(fixture, "USRDIR")); File.WriteAllText(Path.Combine(fixture, "USRDIR", "EBOOT.BIN"), "Synthetic test data. This is not an executable game.");
        var record = new MediaRecord("presentation-qa-" + Guid.NewGuid().ToString("N"), paths.Portable(source), "ps3-folder", edition!.Id, "synthetic", MediaConfidence.UnknownRevision, "Synthetic presentation fixture; no game executable", [], DateTimeOffset.UtcNow, false);
        var oldMedia = Current.MediaId;
        try
        {
            state.Media.Add(record); Current.MediaId = record.Id;
            automaticAssets[record.Id] = await new GameAssetReader(paths).ReadAsync(record, true);
            RefreshGames(); RefreshDetail(); Width = 1600; Height = 900; await capture("added-scene-1600");
            if (SceneArt.Source == null || SceneCover.Visibility != Visibility.Collapsed || ArtFrame.ActualWidth <= ScenePane.ActualWidth) throw new Exception("Added background must bleed behind library");
            Width = 1280; Height = 720; await capture("added-scene-1280");
            Width = 960; Height = 540; await capture("added-scene-960");
            Width = 820; Height = 500; await capture("added-scene-820");
            if (SceneActions.ActualHeight > ScenePane.ActualHeight || ArtFrame.ActualWidth <= ScenePane.ActualWidth) throw new Exception("Small artwork scene sizing");
            checks.Add("Added artwork spans both panes at 1600/1280/960/820 widths without an inset frame");
            var coverPixels=new byte[384*768*4];
            for(var y=0;y<768;y++)for(var x=0;x<384;x++){var at=(y*384+x)*4;coverPixels[at+(y<256?2:y<512?1:0)]=180;coverPixels[at+3]=255;}
            var highCover=BitmapSource.Create(384,768,96,96,PixelFormats.Bgra32,null,coverPixels,384*4);highCover.Freeze();
            ApplyPresentation(new(highCover,null,LibraryArtwork.Neutral),true);Width=960;Height=540;await capture("added-cover-backdrop-960");
            if(SceneArt.Source is not CroppedBitmap centered||centered.Source!=highCover||centered.SourceRect.Y<=0||Math.Abs(centered.SourceRect.Y+centered.PixelHeight/2-384)>1||SceneArt.Effect==null||SceneCover.Visibility!=Visibility.Collapsed)throw new Exception("Added portrait cover did not supply a centered softened backdrop");
            var centerPixel=new byte[4];centered.CopyPixels(new Int32Rect(centered.PixelWidth/2,centered.PixelHeight/2,1,1),centerPixel,4,0);
            if(centerPixel[1]!=180||centerPixel[0]!=0||centerPixel[2]!=0)throw new Exception("Portrait fallback shows the top rather than the green center band");
            Width=1280;Height=720;LayoutBackdrop();
            if(SceneArt.Source is not CroppedBitmap resized||Math.Abs(resized.SourceRect.Y+resized.PixelHeight/2-384)>1)throw new Exception("Portrait fallback lost its center on resize");
            var tiny=BitmapSource.Create(144,80,96,96,PixelFormats.Bgra32,null,new byte[144*80*4],144*4);
            ApplyPresentation(new(tiny,null,LibraryArtwork.Neutral),true);
            if(SceneArt.Source!=null||SceneCover.Visibility!=Visibility.Visible)throw new Exception("Menu icon was stretched into a backdrop");
            ApplyPresentation(new(highCover,null,LibraryArtwork.Neutral),false);
            if(SceneArt.Source!=null||SceneArt.Effect!=null||SceneCover.Visibility!=Visibility.Visible)throw new Exception("Unadded cover acquired a backdrop");
            checks.Add("Portrait cover fallback crops from its true pixel center across resize; tiny menu icons and unadded games stay small");
            var local = new LibraryArtwork(new HubPaths(paths.Managed("cache", "logo-check")));
            var alpha = new byte[128 * 32 * 4]; for (var y = 10; y < 22; y++) for (var x = 16; x < 112; x++) { var at = (y * 128 + x) * 4; alpha[at] = alpha[at + 1] = alpha[at + 2] = 240; alpha[at + 3] = 255; }
            var logo = BitmapSource.Create(128, 32, 96, 96, PixelFormats.Bgra32, null, alpha, 512);
            if (!LibraryArtwork.IsLogo(logo) || LibraryArtwork.IsLogo(original.Cover)) throw new Exception("Transparent logo validation");
            ApplyPresentation(new(null, null, LibraryArtwork.Neutral, Logo: logo), false);
            if (SceneLogo.Visibility != Visibility.Visible || GameTitle.Visibility != Visibility.Collapsed || GameSubtitle.Visibility != Visibility.Collapsed) throw new Exception("Explicit logo should replace title even before media is added");
            checks.Add("Genuine transparent title logo replaces text for added and unadded games");
            var malicious = new GamePresentationAssets("../private.png", "C:/private.png", null, "../private.mp4", []);
            if (local.Merge(new(null, null, LibraryArtwork.Neutral), malicious) is { Cover: not null } or { Background: not null } or { Video: not null }) throw new Exception("Unsafe extracted asset reference");
            checks.Add("Logo fallback requires real alpha and wide dimensions; extracted asset paths stay managed");
            var video = Path.Combine(fixture, "background.avi"); WriteFixtureVideo(video);
            automaticAssets[record.Id] = await new GameAssetReader(paths).ReadAsync(record, true); UpdateArtwork();
            var timeout = DateTime.UtcNow.AddSeconds(6);
            while (!videoReady && failedVideo == null && DateTime.UtcNow < timeout) await Task.Delay(80);
            if (!videoReady) throw new Exception("Synthetic AVI could not open with Windows media player: " + (videoFailureDetail ?? "timed out") + "; path length " + SceneVideo.Source?.LocalPath.Length);
            videoPaused = true; UpdateVideoPlayback();
            var position = SceneVideo.Position; await Task.Delay(200);
            if (SceneVideo.Position - position > TimeSpan.FromMilliseconds(120)) throw new Exception("Background pause");
            appearance.VideoBackgrounds = false; UpdateVideoPlayback();
            if (SceneVideo.Visibility != Visibility.Collapsed) throw new Exception("Background video preference");
            appearance.VideoBackgrounds = true; videoPaused = false;
            SceneVideo.Play(); await Task.Delay(250);
            if (SceneVideo.Position <= TimeSpan.Zero) throw new Exception("Video timeline did not advance");
            OpenGameSettings(this, new()); position = SceneVideo.Position; await Task.Delay(200);
            if (SceneVideo.Position - position > TimeSpan.FromMilliseconds(120)) throw new Exception("Video must pause behind settings");
            CloseGameSettings(this, new());
            checks.Add("Locally extracted AVI opens and advances; pause/settings/video preference suspend playback");
            // Exercise the actual failure callback with a corrupt clip, not a mocked success state.
            var broken = paths.Managed("artwork", "broken-fixture.mp4"); File.WriteAllText(broken, "not a video");
            ApplyPresentation(new(original.Cover, original.Background, original.Tint, Video: new Uri(broken)), true);
            timeout = DateTime.UtcNow.AddSeconds(6);
            while (failedVideo == null && DateTime.UtcNow < timeout) await Task.Delay(80);
            if (failedVideo == null || SceneVideo.Visibility != Visibility.Collapsed) throw new Exception("Broken video must fall back to still artwork");
            checks.Add("Corrupt video fails quietly to a still image");
        }
        finally
        {
            state.Media.Remove(record); Current.MediaId = oldMedia; automaticAssets.Remove(record.Id); assetReads.Remove(record.Id);
            RefreshGames(); RefreshDetail(); Save();
        }
        Width = 1320; Height = 880; await capture("presentation-final-unadded");
    }
    private static void WriteFixtureVideo(string file)
    {
        // Small generated RGB test clip. No copyrighted movie or decoder dependency.
        using var stream = File.Create(file); using var w = new BinaryWriter(stream, Encoding.ASCII);
        void Four(string text) => w.Write(Encoding.ASCII.GetBytes(text));
        void Chunk(string name, Action body) { Four(name); var sizeAt = stream.Position; w.Write(0); var start = stream.Position; body(); var size = stream.Position - start; var end = stream.Position; stream.Position = sizeAt; w.Write((int)size); stream.Position = end; if ((size & 1) != 0) w.Write((byte)0); }
        Chunk("RIFF", () => {
            Four("AVI "); Chunk("LIST", () => {
                Four("hdrl"); Chunk("avih", () => { foreach (var n in new uint[] { 33333, 64*64*3*30, 0, 0x10, 90, 0, 1, 64*64*3, 64, 64, 0, 0, 0, 0 }) w.Write(n); });
                Chunk("LIST", () => { Four("strl"); Chunk("strh", () => {
                    Four("vids"); Four("DIB "); w.Write(0u); w.Write((ushort)0); w.Write((ushort)0); foreach (var n in new uint[]{0,1,30,0,90,64*64*3,uint.MaxValue,0}) w.Write(n);
                    w.Write((short)0); w.Write((short)0); w.Write((short)64); w.Write((short)64);
                }); Chunk("strf", () => { w.Write(40); w.Write(64); w.Write(64); w.Write((ushort)1); w.Write((ushort)24); foreach(var n in new uint[]{0,64*64*3,0,0,0,0})w.Write(n); }); });
            });
            Chunk("LIST", () => { Four("movi"); for (var frame=0;frame<90;frame++) Chunk("00db", () => { for(var pixel=0;pixel<64*64;pixel++) { w.Write((byte)(100+frame)); w.Write((byte)60); w.Write((byte)(pixel%64*3)); } }); });
            Chunk("idx1", () => { for(var i=0;i<90;i++) { Four("00db"); w.Write(0x10); w.Write(4+i*(8+64*64*3)); w.Write(64*64*3); } });
        });
    }
}
