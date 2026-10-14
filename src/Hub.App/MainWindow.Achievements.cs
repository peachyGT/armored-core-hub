using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using Hub.Core;

namespace Hub.App;
public partial class MainWindow
{
    private void RefreshAchievements()
    {
        if(edition==null)return;var selected=edition;var backend=Backend.Id;var service=new AchievementService(paths);var support=AchievementSupport.Get(selected);var prefs=AchievementPreferences.Load(paths);
        AchievementPanel.Children.Clear();AchievementPanel.Children.Add(new TextBlock{Text="Achievements",FontSize=24});AchievementPanel.Children.Add(Body(support.Notes));
        void Button(string name,RoutedEventHandler action,bool enabled=true){var b=new Button{Content=name,HorizontalAlignment=HorizontalAlignment.Left,IsEnabled=enabled};b.Click+=action;AchievementPanel.Children.Add(b);}
        if(AchievementSupport.NativeRetroBackend(backend))
        {
            var tracking=new CheckBox{Content="Use RetroAchievements for this edition",IsChecked=prefs.GameEnabled.GetValueOrDefault(selected.Id,true),Margin=new(0,0,0,10)};
            tracking.Click+=(_,_)=>{var p=AchievementPreferences.Load(paths);p.GameEnabled[selected.Id]=tracking.IsChecked==true;p.Save(paths);RefreshAchievements();StatusText.Text="Achievement preference saved · applies next launch";};AchievementPanel.Children.Add(tracking);
            var hardcore=new CheckBox{Content="Hardcore mode for this edition",IsChecked=prefs.GameHardcore.GetValueOrDefault(selected.Id,prefs.Hardcore),IsEnabled=prefs.Enabled&&tracking.IsChecked==true,Margin=new(0,0,0,4)};
            hardcore.Click+=(_,_)=>{var p=AchievementPreferences.Load(paths);p.GameHardcore[selected.Id]=hardcore.IsChecked==true;p.Save(paths);StatusText.Text="Hardcore preference saved · the emulator enforces eligible settings";};AchievementPanel.Children.Add(hardcore);
            AchievementPanel.Children.Add(Body("Standard tracking is enabled by default. Hardcore is optional and restricts save states, cheats and some patches; supported hashes and provider rules still apply."));
            var account=NativeAchievementAccounts.Load(paths);AchievementPanel.Children.Add(Body(state.Offline?"Offline · cached progress remains available; online tracking resumes next online launch.":account==null?"Sign in once in the Hub to enable online tracking in DuckStation, PCSX2 and PPSSPP.":"Signed in as "+account.Username+(prefs.Enabled?"":" · global RetroAchievements tracking is disabled")));
            Button(account==null?"Sign in to RetroAchievements…":"Achievement account and defaults…",ShowAchievementAccount);
            if(support.GameId is {} id)
            {
                Button("View achievement set and supported copies",(_,_)=>OpenUrl("https://retroachievements.org/game/"+id));
                Button("Refresh achievements",async(_,_)=>await Work("Refresh achievements",ct=>service.SyncNativeAsync(selected,state.Offline,ct)),!state.Offline&&account!=null);
            }
        }
        else if(support.HasPublishedSet)
        {
            AchievementPanel.Children.Add(Body("Native console achievements are enabled automatically. Your local emulator profile records progress without an online account."));
            if(backend=="xenia")
            {
                var save=new SaveService(paths).Live(selected.SaveFamily,backend);
                try
                {
                    var profiles=XeniaProfiles.ExistingLocal(save);var valid=profiles.Where(p=>p.IsValid).ToList();
                    if(valid.Count==0)AchievementPanel.Children.Add(Body(profiles.Count==0?"A local Hub player is created automatically for fresh save storage on the next launch.":"Existing local player data needs repair. It is preserved; damaged accounts are not replaced."));
                    else
                    {
                        var choice=new ComboBox{ItemsSource=valid.Select(p=>new Choice(p.Id,p.Gamertag+" · "+p.Id)).ToList(),DisplayMemberPath="Label",SelectedValuePath="Id",MaxWidth=500,HorizontalAlignment=HorizontalAlignment.Left,MinWidth=260};
                        AchievementPanel.Children.Add(new TextBlock{Text="Local Xbox player · applies next launch",Margin=new(0,8,0,0)});AutomationProperties.SetName(choice,"Local Xbox player");
                        var selectedBuild=state.Builds.FirstOrDefault(b=>b.Id==state.Selections.GetValueOrDefault(selected.Id)?.BuildId)??state.Builds.Where(b=>b.BackendId=="xenia").OrderByDescending(b=>b.InstalledAt).FirstOrDefault();
                        var nativeConfig=selectedBuild!=null?paths.Managed("runtime","sessions",selected.Id,selectedBuild.Id,"xenia-canary.config.toml"):paths.Managed("config","uninstalled-xenia-profile.ini");
                        try{choice.SelectedValue=XeniaProfiles.SelectLocal(save,nativeConfig);}catch(HubException){choice.SelectedIndex=-1;}
                        choice.SelectionChanged+=(_,_)=>{if(choice.SelectedValue is not string id)return;try{XeniaProfiles.ChooseLocal(paths,save,id);StatusText.Text="Local player selected · applies next launch";}catch(HubException ex){StatusText.Text=ex.Message;}};AchievementPanel.Children.Add(choice);
                        if(valid.Count>1)AchievementPanel.Children.Add(Body("Choose the player whose saves and achievements you want to use. Other players remain available."));
                    }
                }
                catch(Exception ex)when(ex is System.IO.IOException or UnauthorizedAccessException or HubException){AchievementPanel.Children.Add(Body("Local player data could not be read. Open diagnostics or repair the imported profile; existing data is preserved."));}
            }
            Button("Refresh local progress",async(_,_)=>{var source=AchievementGameMedia(selected);await Work("Read achievements",ct=>Task.Run(()=>service.ReadLocal(selected,backend,source),ct));});
        }
        var snapshots=service.Cached(selected.Id);
        if(snapshots.Count>0){AchievementPanel.Children.Add(Body(string.Join("\n",snapshots.Select(AchievementSummary))));Button("View this game's achievements",ShowGameAchievements);Button("Open achievement records",(_,_)=>OpenFolder(paths.Managed("achievements",selected.Id)));}
    }
    private void ShowAchievementAccount(object sender,RoutedEventArgs e)
    {
        if(operation!=null||!ConfirmAdvanced())return;Utility("Achievement account","Sign in once for PS1, PS2 and PSP tracking. PS3 trophies and Xbox 360 achievements use local profiles.");SetNavigation("Achievements");
        var prefs=AchievementPreferences.Load(paths);var account=NativeAchievementAccounts.Load(paths);
        var enabled=new CheckBox{Content="RetroAchievements enabled for supported games",IsChecked=prefs.Enabled,Margin=new(0,4,0,10)};enabled.Click+=(_,_)=>{var p=AchievementPreferences.Load(paths);p.Enabled=enabled.IsChecked==true;p.Save(paths);RefreshAchievements();};
        var challenge=new CheckBox{Content="Hardcore mode by default",IsChecked=prefs.Hardcore,Margin=new(0,0,0,4)};challenge.Click+=(_,_)=>{var p=AchievementPreferences.Load(paths);p.Hardcore=challenge.IsChecked==true;p.Save(paths);RefreshAchievements();};
        var form=new StackPanel{MaxWidth=460,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,8,0,12)};UtilityContent.Children.Add(form);
        void Note(string text){var note=Body(text);note.Margin=new(0,0,0,4);form.Children.Add(note);}
        if(account!=null)Note("Signed in as "+account.Username);
        if(state.Offline)Note("Offline mode is on. Turn it off in the Hub to sign in or refresh progress.");
        if(launcher.IsRunning)Note("Close the running game before changing the account. Tracking preferences apply next launch.");
        var username=new TextBox{Text=account?.Username??"",MaxLength=64,MinWidth=260};var password=new PasswordBox{MaxLength=1024,Margin=new(0,4,0,10),Padding=new(8),MinHeight=34,Background=(Brush)FindResource("Surface"),Foreground=(Brush)FindResource("Ink"),BorderBrush=(Brush)FindResource("Rule")};
        void Field(string label,Control box){form.Children.Add(new TextBlock{Text=label,Margin=new(0,8,0,0)});AutomationProperties.SetName(box,label);form.Children.Add(box);}
        Field("RetroAchievements username",username);Field("RetroAchievements password · never saved",password);
        var signIn=new Button{Content=account==null?"Sign in":"Switch account",HorizontalAlignment=HorizontalAlignment.Left,IsEnabled=!state.Offline&&!launcher.IsRunning};signIn.Click+=async(_,_)=>
        {
            if(launcher.IsRunning){StatusText.Text="Close the game before changing the account.";password.Clear();return;}
            var user=username.Text.Trim();var secret=password.Password;password.Clear();var selected=edition;var signedIn=false;
            await Work("Sign in to RetroAchievements",async ct=>{using var client=new NativeRetroClient(paths);await client.SignInAsync(user,secret,state.Offline,ct);signedIn=true;});secret="";
            if(signedIn&&selected!=null&&AchievementSupport.Get(selected).HasPublishedSet&&AchievementSupport.Get(selected).Provider=="RetroAchievements")await Work("Load achievements",ct=>new AchievementService(paths).SyncNativeAsync(selected,state.Offline,ct));
            ShowAchievementAccount(this,new());
        };form.Children.Add(signIn);
        if(account!=null)
        {
            var signOut=new Button{Content="Sign out",HorizontalAlignment=HorizontalAlignment.Left,IsEnabled=!launcher.IsRunning};signOut.Click+=async(_,_)=>{password.Clear();if(launcher.IsRunning){StatusText.Text="Close the game before signing out.";return;}await Work("Sign out of RetroAchievements",ct=>Task.Run(()=>NativeAchievementAccounts.SignOut(paths),ct));ShowAchievementAccount(this,new());};form.Children.Add(signOut);
        }
        UtilityContent.Children.Add(new TextBlock{Text="Tracking defaults",FontSize=20,Margin=new(0,6,0,4)});
        UtilityContent.Children.Add(enabled);UtilityContent.Children.Add(challenge);
        UtilityContent.Children.Add(Body("Hardcore is off by default. Each game's achievement settings can override these defaults. Disabling RetroAchievements leaves cached records available."));
        UtilityContent.Children.Add(Body("Only a native login token is stored, protected by your Windows account. Web API keys are separate and are not needed. A moved Hub on another Windows account needs a fresh sign-in."));
        ActionButton("Create a RetroAchievements account",(_,_)=>OpenUrl("https://retroachievements.org/createaccount.php"));ActionButton("View achievements",ShowAchievementLibrary);
    }
}
