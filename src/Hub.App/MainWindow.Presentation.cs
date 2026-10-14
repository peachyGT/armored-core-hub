using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Hub.Core;

namespace Hub.App;

public partial class MainWindow
{
    private readonly CancellationTokenSource presentationLifetime = new();
    private readonly Dictionary<string, Task<GamePresentationAssets>> assetReads = [];
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Task,byte> backgroundReads=new();
    private readonly Dictionary<string, GamePresentationAssets> automaticAssets = [];
    private CancellationTokenSource? selectedAssetRead;
    private string? selectedAssetId;
    private bool presentationClosed;
    private int presentationRevision;
    private bool videoPaused, videoReady;
    private BackgroundVideo? sceneVideo;
    private Uri? requestedSceneVideo;
    private BackgroundVideo SceneVideo=>sceneVideo??throw new InvalidOperationException("No background video has been prepared.");
    private Uri? failedVideo;
    private string? videoFailureDetail;
    private MediaRecord? PresentationMedia => edition == null ? null :
        state.Media.FirstOrDefault(m => m.Id == Current.MediaId && m.EditionId == edition.Id) ?? state.Media.FirstOrDefault(m => m.EditionId == edition.Id);

    private void InitializePresentation()
    {
        SetNavigation("Library");
        Activated += (_, _) => UpdateVideoPlayback(); Deactivated += (_, _) => UpdateVideoPlayback();
        StateChanged += (_, _) => UpdateVideoPlayback();
        Closed += (_, _) =>
        {
            presentationClosed=true;presentationRevision++;CancelAssetRead();presentationLifetime.Cancel();
            coverDownloads?.Cancel();coverDownloads?.Dispose();coverDownloads=null;
            if(sceneVideo!=null)
            {
                sceneVideo.MediaOpened-=VideoOpened;sceneVideo.MediaEnded-=VideoEnded;sceneVideo.MediaFailed-=VideoFailed;
                sceneVideo.Close();sceneVideo.Source=null;ArtFrame.Children.Remove(sceneVideo);sceneVideo=null;
            }
            SceneArt.Source=ArtBlur.Source=SceneCover.Source=SceneLogo.Source=null;
            centeredCoverSource=null;
            assetReads.Clear();automaticAssets.Clear();artwork.ClearCaches();presentationLifetime.Dispose();
        };
    }
    private void SetNavigation(string page)
    {
        LibraryNav.Tag = page == "Library" ? "active" : null;
        AchievementsNav.Tag = page == "Achievements" ? "active" : null;
        EmulatorsNav.Tag = page == "Emulators" ? "active" : null;
        HelpNav.Tag = page is "Help" or "Set up your collection" ? "active" : null;
        SettingsNav.Opacity = page == "Settings" ? 1 : .8;
    }
    private async void UpdateArtwork()
    {
        if (presentationClosed || artwork == null || SceneArt == null) return;
        ++presentationRevision;
        var row = GameList.SelectedItem as GameRow;
        var selected = PresentationMedia;
        if(selectedAssetId!=selected?.Id)CancelAssetRead();
        var chosen = row == null || edition == null ? new Artwork(null, null, LibraryArtwork.Neutral) : EditionArtwork(row.Id, edition.Id);
        ApplyPresentation(artwork.Merge(chosen, selected == null ? null : automaticAssets.GetValueOrDefault(selected.Id)), selected != null);
        if (selected == null || automaticAssets.ContainsKey(selected.Id) || assetReads.ContainsKey(selected.Id)) return;
        var lifetime=CancellationTokenSource.CreateLinkedTokenSource(presentationLifetime.Token);var token=lifetime.Token;
        selectedAssetRead=lifetime;selectedAssetId=selected.Id;
        var read=new MediaPreviewService(paths,catalog,downloader).ReadAsync(selected,state.Offline,token);assetReads[selected.Id]=read;
        backgroundReads.TryAdd(read,0);_=read.ContinueWith(t=>backgroundReads.TryRemove(t,out _),CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
        _=read.ContinueWith(static task=>_ = task.Exception,CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
        try
        {
            var assets = await read.WaitAsync(token);
            if(presentationClosed||token.IsCancellationRequested)return;
            if(automaticAssets.Count>=64&&!automaticAssets.ContainsKey(selected.Id))automaticAssets.Remove(automaticAssets.Keys.First());
            automaticAssets[selected.Id] = assets;
            if(PresentationMedia?.Id!=selected.Id||GameList.SelectedItem is not GameRow currentRow||edition==null)return;
            row=currentRow;chosen=EditionArtwork(row.Id,edition.Id);
            ApplyPresentation(artwork.Merge(chosen, assets), true);
            // Update only this row, preserving selection, scroll position and game settings.
            if (row != null && row.Cover == null && !SystemParameters.HighContrast)
            {
                var updated = artwork.Merge(chosen, assets).Cover;
                var index = rows.IndexOf(row);
                if (updated != null && index >= 0)
                {
                    loading = true; rows[index] = row with { Cover = updated }; GameList.SelectedItem = rows[index]; loading = false;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HubException or System.Text.Json.JsonException or InvalidDataException)
        { if (!presentationClosed && PresentationMedia?.Id==selected.Id) StatusText.Text = "Game artwork could not be read. You can choose a local image in Artwork."; }
        finally
        {
            if(assetReads.GetValueOrDefault(selected.Id)==read)assetReads.Remove(selected.Id);
            if(ReferenceEquals(selectedAssetRead,lifetime)){selectedAssetRead=null;selectedAssetId=null;}
            lifetime.Dispose();
        }
    }
    private void CancelAssetRead()
    {
        var previous=selectedAssetRead;selectedAssetRead=null;selectedAssetId=null;assetReads.Clear();
        previous?.Cancel();previous?.Dispose();
    }
    private void ApplyPresentation(Artwork image, bool added)
    {
        var contrast = SystemParameters.HighContrast;
        var video=added&&!contrast&&appearance.VideoBackgrounds&&SystemParameters.ClientAreaAnimation&&image.Video!=failedVideo?image.Video:null;
        var coverFallback = added && image.Background == null && image.Cover is { PixelWidth: >= 256, PixelHeight: >= 256 } ? image.Cover : null;
        centeredCoverSource = coverFallback;
        var background = added ? image.Background ?? coverFallback : null;
        SceneArt.Source = ArtBlur.Source = background;
        SceneArt.Effect = coverFallback == null ? null : CoverBackdropBlur;
        SceneArt.Opacity = coverFallback == null ? 1 : .62;
        SceneCover.Source = image.Cover;
        SceneCover.Visibility = !contrast && (!added || background == null && video == null) ? Visibility.Visible : Visibility.Collapsed;
        SceneLogo.Source = !contrast ? image.Logo : null;
        SceneLogo.Visibility = SceneLogo.Source == null ? Visibility.Collapsed : Visibility.Visible;
        GameTitle.Text = game?.DisplayTitle ?? game?.Title ?? "";
        GameSubtitle.Text = (game?.DisplaySubtitle ?? "") + (edition?.Platform == "psp" ? " Portable" : "");
        GameSubtitle.Text = GameSubtitle.Text.Trim();
        GameTitle.Visibility = SceneLogo.Source == null ? Visibility.Visible : Visibility.Collapsed;
        GameSubtitle.Visibility = SceneLogo.Source == null && GameSubtitle.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ArtworkLayers.Visibility = SceneShade.Visibility = TitleShade.Visibility = contrast ? Visibility.Collapsed : Visibility.Visible;
        LibraryWash.Background = contrast ? (Brush)FindResource("Ground") : new SolidColorBrush(Color.FromArgb(193, 21, 19, 16));
        AmbientLayer.Visibility = LibraryGlow.Visibility = appearance.Ambient && !contrast ? Visibility.Visible : Visibility.Collapsed;
        AmbientColor.Color = Color.FromArgb(57, image.Tint.R, image.Tint.G, image.Tint.B);
        LibraryAmbientColor.Color = Color.FromArgb(32, image.Tint.R, image.Tint.G, image.Tint.B);
        SetSceneVideo(video);
        UpdateVideoPlayback(); AdaptHeight();
    }
    private static readonly BlurEffect CoverBackdropBlur = FrozenCoverBlur();
    private static BlurEffect FrozenCoverBlur()
    {
        var effect = new BlurEffect { Radius = 18, RenderingBias = RenderingBias.Performance }; effect.Freeze(); return effect;
    }
    private void LayoutBackdrop()
    {
        if (CollectionView.ActualWidth < 1 || CollectionView.ActualHeight < 1) return;
        // Share one scene behind both panes. The soft left edge lies beneath the library,
        // not inside the visible artwork. There is no inset frame or top/right vignette.
        var width = CollectionView.ActualWidth;
        var left = Math.Max(0, LibraryColumn.ActualWidth * .28 - 55);
        Canvas.SetLeft(ArtFrame, left); Canvas.SetTop(ArtFrame, 0);
        ArtFrame.Width = Math.Max(1, width - left); ArtFrame.Height = CollectionView.ActualHeight;
        if (centeredCoverSource != null)
        {
            var rect = CenteredCoverRect(centeredCoverSource, ArtFrame.Width, ArtFrame.Height);
            if (SceneArt.Source is not CroppedBitmap crop || crop.Source != centeredCoverSource || crop.SourceRect != rect)
            {
                var centered = new CroppedBitmap(centeredCoverSource, rect); centered.Freeze();
                SceneArt.Source = ArtBlur.Source = centered;
            }
        }
        SceneCover.Height = Math.Clamp(CollectionView.ActualHeight * .26, 80, 174);
        SceneCover.Width = SceneCover.Height * .75;
        SceneLogo.MaxWidth = Math.Max(180, Math.Min(540, ScenePane.ActualWidth - 60));
    }
    private BitmapSource? centeredCoverSource;
    private static Int32Rect CenteredCoverRect(BitmapSource source, double width, double height)
    {
        var aspect = Math.Max(1, width) / Math.Max(1, height);
        var cropWidth = Math.Min(source.PixelWidth, Math.Max(1, (int)Math.Round(source.PixelHeight * aspect)));
        var cropHeight = Math.Min(source.PixelHeight, Math.Max(1, (int)Math.Round(source.PixelWidth / aspect)));
        return new((source.PixelWidth - cropWidth) / 2, (source.PixelHeight - cropHeight) / 2, cropWidth, cropHeight);
    }
    private void SetSceneVideo(Uri? video)
    {
        if(presentationClosed)return;
        var changed=requestedSceneVideo!=video;requestedSceneVideo=video;
        if(video!=null&&video==failedVideo)return;
        if(sceneVideo==null)
        {
            // Initialize the decoder only when an actual, allowed preview is selected.
            if(video==null||!appearance.VideoBackgrounds||!SystemParameters.ClientAreaAnimation||SystemParameters.HighContrast)return;
            sceneVideo=new(){Stretch=Stretch.UniformToFill,Visibility=Visibility.Collapsed};
            sceneVideo.MediaOpened+=VideoOpened;sceneVideo.MediaEnded+=VideoEnded;sceneVideo.MediaFailed+=VideoFailed;
            ArtFrame.Children.Add(sceneVideo);
        }
        if(video==null){if(videoReady&&sceneVideo.Source!=failedVideo)sceneVideo.Pause();sceneVideo.Visibility=Visibility.Collapsed;return;}
        if(changed)videoPaused=false;
        if(sceneVideo.Source==video)return;
        videoReady=false;failedVideo=null;videoFailureDetail=null;
        sceneVideo.Visibility=Visibility.Collapsed;sceneVideo.Source=video;
        sceneVideo.Play();sceneVideo.Pause();
    }
    private void VideoOpened(object sender, RoutedEventArgs e)
    { if(presentationClosed||sceneVideo?.Source==null)return;videoReady = true; UpdateVideoPlayback(); }
    private void VideoEnded(object sender, RoutedEventArgs e)
    { if(presentationClosed||sceneVideo?.Source==null)return;sceneVideo.Position = TimeSpan.Zero; UpdateVideoPlayback(); }
    private void VideoFailed(object? sender, VideoFailureEventArgs e)
    {
        if(presentationClosed||sceneVideo?.Source==null)return;
        videoFailureDetail = e.ErrorException.GetType().Name + ": " + e.ErrorException.Message + " (" + e.ErrorException.HResult.ToString("X8") + ")";
        failedVideo = sceneVideo.Source; videoReady = false; sceneVideo.Visibility = Visibility.Collapsed;
        sceneVideo.Close(); VideoButton.Visibility = Visibility.Collapsed;
        SceneCover.Visibility = SceneArt.Source == null ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = "This video cannot play with the Windows codecs available. Showing still artwork.";
    }
    private void ToggleVideo(object sender, RoutedEventArgs e)
    { videoPaused = !videoPaused; UpdateVideoPlayback(); }
    private void UpdateVideoPlayback()
    {
        if (presentationClosed) return;
        if(sceneVideo==null){VideoButton.Visibility=Visibility.Collapsed;return;}
        var available = requestedSceneVideo!=null&&sceneVideo.Source==requestedSceneVideo&&sceneVideo.Source!=failedVideo&&appearance.VideoBackgrounds&&SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast;
        var playing = available && videoReady && !videoPaused && IsActive && WindowState != WindowState.Minimized && CollectionView.Visibility == Visibility.Visible && DetailPane.Visibility != Visibility.Visible;
        if (playing) sceneVideo.Play(); else if (videoReady && sceneVideo.Source != failedVideo) sceneVideo.Pause();
        sceneVideo.Visibility = available && videoReady ? Visibility.Visible : Visibility.Collapsed;
        VideoButton.Visibility = available && videoReady && DetailPane.Visibility != Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        VideoButton.Content = videoPaused ? "Resume background" : "Pause background";
    }
}
