using System.IO;
using System.ComponentModel;
using System.Windows;
using Hub.Core;

namespace Hub.App;
public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplyContrast();
        SystemParameters.StaticPropertyChanged += SystemPreferencesChanged;
        DispatcherUnhandledException += (_, args) => { MessageBox.Show(args.Exception.Message,"Armored Core Hub",MessageBoxButton.OK,MessageBoxImage.Error); args.Handled=true; };
        try
        {
            var root=AppContext.BaseDirectory;
            var rootIndex=Array.IndexOf(e.Args,"--root");if(rootIndex>=0&&rootIndex+1<e.Args.Length)root=Path.GetFullPath(e.Args[rootIndex+1]);
            var paths=new HubPaths(root);
            var identity=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(paths.Root.ToUpperInvariant())));
            instance=new Mutex(true,"Local\\ArmoredCoreHub-"+identity,out var first);
            if(!first){MessageBox.Show("This portable Hub is already open.","Armored Core Hub");Shutdown();return;}
            if(!File.Exists(paths.Managed("manifests","catalog.json")))File.Copy(Path.Combine(AppContext.BaseDirectory,"manifests","catalog.json"),paths.Managed("manifests","catalog.json"));
            var window=new MainWindow(paths,e.Args);MainWindow=window;window.Show();
        }
        catch(Exception ex){MessageBox.Show("The Hub could not initialize its portable storage. Extract it to a writable folder.\n\n"+ex.Message,"Armored Core Hub",MessageBoxButton.OK,MessageBoxImage.Error);Shutdown(1);}
    }
    protected override void OnExit(ExitEventArgs e) { SystemParameters.StaticPropertyChanged -= SystemPreferencesChanged; instance?.Dispose(); base.OnExit(e); }
    private void SystemPreferencesChanged(object? sender,PropertyChangedEventArgs args)
    {
        if(args.PropertyName!=nameof(SystemParameters.HighContrast)||Dispatcher.HasShutdownStarted)return;
        _=Dispatcher.InvokeAsync(()=>{if(!Dispatcher.HasShutdownStarted)ApplyContrast();});
    }
    private void ApplyContrast()
    {
        var normal = new Dictionary<string,string> {["Ground"]="#151310",["Surface"]="#24221E",["Raised"]="#38362F",["Ink"]="#F1EEE4",["Muted"]="#C1BDB1",["Rule"]="#49473F",["Accent"]="#EBE5D3",["Good"]="#B9CCA7",["InputInk"]="#24221E",["InputSurface"]="#ECE8DF"};
        foreach(var (key,color) in normal)
        {
            var brush=SystemParameters.HighContrast ? key switch
            {
                "Ground" or "Surface" or "Raised" or "InputSurface" => SystemColors.WindowBrush,
                "Accent" or "Rule" or "Good" => SystemColors.WindowTextBrush,
                _ => SystemColors.WindowTextBrush
            } : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
            if(brush.CanFreeze)brush.Freeze();Resources[key]=brush;
        }
    }
}
