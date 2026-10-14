using System.Text;
using System.Text.Json;

namespace Hub.Core;

public sealed class LaunchPlanner(HubPaths paths, Catalog catalog)
{
    public List<Diagnostic> Diagnose(string editionId, HubState state)
    {
        var e = catalog.Editions.Single(x => x.Id == editionId); var choice = Selection(e, state);
        var profile = catalog.Profiles.SingleOrDefault(x => x.Id == (choice.ProfileId ?? e.PreferredProfile) && x.EditionId == e.Id);
        if (profile == null) return [new("Configuration", "error", "The selected profile is unavailable. Choose a supported profile.", "profile")];
        var backend = catalog.Backends.Single(x => x.Id == profile.BackendId);
        var media = state.Media.FirstOrDefault(x => x.Id == choice.MediaId && x.EditionId == e.Id);
        var build = SelectBuild(e, state);
        var result = new List<Diagnostic>();
        if (media == null) result.Add(new("Game media", "missing", "Import this edition from your collection.", "media"));
        else
        {
            var path = paths.Resolve(media.Path); var exists = File.Exists(path) || Directory.Exists(path);
            result.Add(new("Game media", exists ? "ready" : "missing", exists ? ConfidenceText(media.Confidence) : "The registered file is unavailable. Reconnect its drive or locate it again.", exists ? null : "media"));
            if (exists && media.Format == "ps3-folder" && !File.Exists(Path.Combine(path, "PS3_GAME", "USRDIR", "EBOOT.BIN")) && !File.Exists(Path.Combine(path, "USRDIR", "EBOOT.BIN"))) result.Add(new("Disc structure", "error", "EBOOT.BIN is missing. Re-import a complete decrypted disc folder.", "media"));
            if (exists && media.Format == "ps3-iso")
            {
                try
                {
                    var disc = Ps3Disc.Inspect(path);
                    if (disc == null || disc.Problem != null) result.Add(new("Disc structure", "error", disc?.Problem ?? "The PS3 ISO could not be read.", "media"));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HubException)
                { result.Add(new("Disc structure", "error", "The PS3 ISO could not be read. Reconnect its drive or import it again.", "media")); }
            }
        }
        var runtimeExists = build != null && ExecutableExists(build);
        result.Add(new("Runtime", runtimeExists ? "ready" : "missing", runtimeExists ? backend.Name + " · " + build!.Version : "Set up " + backend.Name + " for this edition.", runtimeExists ? null : "runtime"));
        if (build != null && RecommendedBuilds.Issue(profile, build) is { } issue)
            result.Add(new("Compatibility repair", "error", issue.Message + " Use Repair setup to select the recommended build. Your saves and current build are kept.", "compatibility"));
        if (media != null && build != null && backend.MinimumBuildsByFormat?.GetValueOrDefault(media.Format) is string minimum
            && !(BuildVersion.Compare(build.Version, minimum) is >= 0))
            result.Add(profile.BuildRecommendation?.Preparation == "ps3-iso9660" && media.Format == "ps3-iso"
                ? new("Game preparation", "error", "This emulator build needs a game folder. Repair setup prepares a copy from your ISO and keeps the original.", "compatibility")
                : new("Emulator version", "error", "This disc format needs " + backend.Name + " " + minimum + " or newer. Install the recommended version.", "runtime"));
        var firmware = PickFirmware(backend, e, state,f=>File.Exists(paths.Resolve(f.Path)));
        var firmwareReady = backend.FirmwareClass == "none" || firmware != null && File.Exists(paths.Resolve(firmware.Path));
        result.Add(new("System software", firmwareReady ? "ready" : "missing",
            backend.FirmwareClass == "none" ? "No separate system software needed." : firmwareReady ? firmware!.Label : "Import " + (e.Platform == "ps3" ? "official PS3 system software." : "a BIOS dump from your console."), firmwareReady ? null : "firmware"));
        result.Add(new("Compatibility", "notice", profile.TestStatus switch
        {
            "locally-tested" => "Tested build: " + profile.TestedBuild,
            "locally-sampled" => "Opening gameplay checked on " + profile.TestedBuild + "; later missions remain untested.",
            _ => "Upstream-informed profile; gameplay has not been tested by this Hub project."
        }));
        result.Add(new("Save protection", "ready", "A restore point is created before every launch."));
        result.Add(new("Updates", state.Offline ? "notice" : "ready", state.Offline ? "Offline mode: installed builds and cached assets remain usable." : "Updates require your selection; working builds stay pinned."));
        return result;
    }
    public static string ConfidenceText(MediaConfidence value) => value switch
    { MediaConfidence.VerifiedExactDump => "Verified exact dump", MediaConfidence.RecognizedUnverified => "Recognized but unverified", MediaConfidence.ModifiedImage => "Modified image / unknown revision", _ => "Unknown revision · edition selected manually" };
    public static GameSelection Selection(Edition edition, HubState state)
    {
        if (!state.Selections.TryGetValue(edition.Id, out var result)) result = new() {EditionId = edition.Id, ProfileId = edition.PreferredProfile};
        return result;
    }
    // A build installation is shared by editions; only a deliberate edition choice is pinned.
    // Reading readiness must not require a setup click, mutate state or replace an explicit pin.
    public InstalledBuild? SelectBuild(Edition edition, HubState state)
    {
        var choice = Selection(edition, state);
        var profile = catalog.Profiles.SingleOrDefault(p => p.Id == (choice.ProfileId ?? edition.PreferredProfile) && p.EditionId == edition.Id);
        if (profile == null) return null;
        var backend = catalog.Backends.Single(b => b.Id == profile.BackendId);
        if (choice.BuildId != null)
            return state.Builds.FirstOrDefault(b => b.Id == choice.BuildId && b.BackendId == backend.Id);
        var release = RecommendedBuilds.For(profile, backend);
        var media = state.Media.FirstOrDefault(m => m.Id == choice.MediaId && m.EditionId == edition.Id);
        var candidates = state.Builds.Where(b => b.BackendId == backend.Id && ExecutableExists(b))
            .OrderByDescending(b => release != null && b.ArchiveSha256 == release.Sha256)
            .ThenByDescending(b => release != null && b.Version == release.Version)
            .ThenByDescending(b => b.InstalledAt).ThenBy(b => b.Id, StringComparer.Ordinal).ToArray();
        bool SupportsMedia(InstalledBuild build) => media == null || backend.Formats.Contains(media.Format)
            && (backend.MinimumBuildsByFormat?.GetValueOrDefault(media.Format) is not string minimum
                || BuildVersion.Compare(build.Version, minimum) is >= 0);
        // An edition recommendation may need ISO-to-folder preparation. Detect the installed
        // recommendation and expose that existing repair, rather than installing a regressed build.
        var recommended = candidates.FirstOrDefault(b => release != null
            && (b.ArchiveSha256 == release.Sha256 || b.Version == release.Version)
            && RecommendedBuilds.Issue(profile, b) == null
            && (SupportsMedia(b) || media?.Format == "ps3-iso" && profile.BuildRecommendation?.Preparation == "ps3-iso9660"));
        return recommended ?? candidates.FirstOrDefault(b => RecommendedBuilds.Issue(profile, b) == null && SupportsMedia(b))
            // Keep an actionable compatibility/format diagnostic when an installed build needs repair.
            ?? candidates.FirstOrDefault();
    }
    private bool ExecutableExists(InstalledBuild build)
    {
        try { return File.Exists(SafeFiles.Within(paths.Resolve(build.Path), build.Executable)); }
        catch (Exception ex) when (ex is HubException or IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }
    public static FirmwareRecord? PickFirmware(Backend backend, Edition edition, HubState state,Func<FirmwareRecord,bool>? available=null) => FirmwareService.Choose(backend.FirmwareClass,edition,state.Firmware,available);

    public LaunchPlan Resolve(string editionId, HubState state)
    {
        var diagnostics = Diagnose(editionId, state); var failure = diagnostics.FirstOrDefault(d => d.Status is "missing" or "error");
        if (failure != null) throw new HubException(failure.Message);
        var edition = catalog.Editions.Single(x => x.Id == editionId); var selection = Selection(edition, state);
        var profile = catalog.Profiles.Single(x => x.Id == (selection.ProfileId ?? edition.PreferredProfile) && x.EditionId == edition.Id);
        var backend = catalog.Backends.Single(x => x.Id == profile.BackendId);
        var build = SelectBuild(edition, state) ?? throw new HubException("Set up " + backend.Name + " for this edition.");
        var media = state.Media.Single(x => x.Id == selection.MediaId && x.EditionId == edition.Id);
        var localPatches = new LocalPatchService(paths,catalog).Selected(selection,media);
        var analogActive=new LocalPatchService(paths,catalog).AnalogActive(localPatches);
        var controls=ControlProfiles.ForLaunch(edition,selection.Controls,analogActive);
        if(localPatches.FirstOrDefault(p=>p.Kind=="ppf") is {} imagePatch)
            media=state.Media.SingleOrDefault(m=>m.Id==imagePatch.DerivedMediaId && m.EditionId==edition.Id) ?? throw new HubException("The patched copy is missing. Re-import its patch.");
        if (!backend.Formats.Contains(media.Format)) throw new HubException("The selected runtime does not support this media format.");
        var enhancementId = selection.EnhancementId ?? profile.DefaultEnhancement;
        var enhancement = catalog.Enhancements.SingleOrDefault(x => x.Id == enhancementId && x.ProfileId == profile.Id) ?? throw new HubException("Choose an enhancement for the current compatibility profile.");
        var settings = new Dictionary<string, string>(profile.Settings);
        foreach (var (key, value) in enhancement.Settings) settings[key] = value;
        var patches = CatalogStore.ResolvePatches(catalog, profile.RequiredPatches.Concat(selection.PatchIds));
        foreach (var patch in patches)
        {
            if (!patch.Editions.Contains(edition.Id)) throw new HubException("A selected patch targets another edition.");
            if (patch.GameplayChanging && !selection.PatchIds.Contains(patch.Id)) throw new HubException("A gameplay-changing patch requires explicit selection.");
            if (patch.Method == "research-only" || patch.Redistribution == "unconfirmed") throw new HubException(patch.Name + " is documented but not yet approved for installation.");
            if (patch.SourceHashes.Length == 0 || !patch.SourceHashes.Contains(media.Fingerprint, StringComparer.OrdinalIgnoreCase)) throw new HubException("A selected patch does not match this exact media hash.");
            if (patch.Method != "configuration") throw new HubException("Prepare a derived patched image with the patch manager before launching this module.");
            foreach (var (key, value) in patch.Settings) settings[key] = value;
        }
        foreach (var (key, value) in selection.AdvancedSettings)
        {
            if (!profile.Settings.ContainsKey(key)) throw new HubException("Unknown advanced setting: " + key);
            settings[key] = value;
        }
        ValidateSettings(settings);
        GraphicsSettings.Apply(settings, backend.Id, edition.GameId, selection.Graphics);
        AchievementPreferences.Apply(settings,backend.Id,edition,paths,state.Offline);
        var extensions = new List<InputExtension>();
        foreach (var id in selection.ExtensionIds)
        {
            var module = catalog.Extensions.SingleOrDefault(x => x.Id == id) ?? throw new HubException("The input extension is unavailable.");
            if (!module.Editions.Contains(edition.Id) || !module.RequiredHashes.Contains(media.Fingerprint) || !module.RequiredBuilds.Contains(build.Id)) throw new HubException("This input extension does not match the exact game and runtime build.");
            if (module.Dependencies.Except(selection.ExtensionIds).Any()) throw new HubException("An input extension dependency is missing.");
            if (!new[] {"PreLaunch", "PostLaunch", "RuntimeExtension", "Shutdown"}.Contains(module.Phase)) throw new HubException("Invalid input extension lifecycle phase.");
            extensions.Add(module);
        }
        var home = paths.Managed("runtime", "sessions", edition.Id, build.Id);
        var exeDir = Path.GetDirectoryName(Path.Combine(home, build.Executable))!;
        var save = new SaveService(paths).Live(edition.SaveFamily, backend.Id);
        var firmware = PickFirmware(backend, edition, state,f=>File.Exists(paths.Resolve(f.Path)));
        var args = new List<string>(); var configs = new Dictionary<string, string>();
        var environment = new Dictionary<string, string>(); var mediaPath = paths.Resolve(media.Path);
        switch (backend.Id)
        {
            case "duckstation":
                configs[Path.Combine(exeDir, "portable.txt")] = "";
                settings["Main/SettingsVersion"] = "3"; settings["Main/SetupWizardIncomplete"] = "false";
                settings["Main/ConfirmPowerOff"] = "false"; settings["AutoUpdater/CheckAtStartup"] = "false";
                settings["BIOS/SearchDirectory"] = Path.GetDirectoryName(paths.Resolve(firmware!.Path))!;
                settings["BIOS/PathNTSCU"] = Path.GetFileName(firmware.Path); settings["BIOS/PathNTSCJ"] = Path.GetFileName(firmware.Path); settings["BIOS/PathPAL"] = Path.GetFileName(firmware.Path);
                settings["MemoryCards/Directory"] = save; settings["MemoryCards/Card1Type"] = "Shared"; settings["MemoryCards/Card1Path"] = Path.Combine(save, "shared.mcd");
                AddSdlMapping(settings, "Pad1", analogActive ? "AnalogController" : "DigitalController", analogActive, controls,edition); ApplyControls(settings, edition.Id, "Controller", "Pad", "InputSources");
                if(analogActive)settings["Pad1/Analog"]="Keyboard/H";
                // Current DuckStation uses Pad1; retained older builds use Controller1.
                // Write equivalent sections, including user overrides, for both schemas.
                foreach(var (key,value) in settings.Where(x=>x.Key.StartsWith("Pad1/",StringComparison.Ordinal)).ToArray())
                    settings["Controller1/"+key[5..]]=value;
                configs[Path.Combine(exeDir, "settings.ini")] = ControllerIni(settings, "Pad1", analogActive, controls,edition, "Controller1");
                args.AddRange(["-batch", "-nogui", selection.Fullscreen ? "-fullscreen" : "-nofullscreen", "--", mediaPath]); break;
            case "pcsx2":
                // -datapath appends a PCSX2 subdirectory in the recommended build.
                // Portable mode instead reads the inis directory beside this session.
                configs[Path.Combine(exeDir, "portable.txt")] = "";
                settings["UI/SettingsVersion"] = "1"; settings["UI/SetupWizardIncomplete"] = "false"; settings["AutoUpdater/CheckAtStartup"] = "false";
                settings["Folders/Bios"] = Path.GetDirectoryName(paths.Resolve(firmware!.Path))!;
                settings["Filenames/BIOS"] = Path.GetFileName(firmware.Path); settings["Folders/MemoryCards"] = save;
                settings["MemoryCards/Slot1_Enable"] = "true"; settings["MemoryCards/Slot1_Filename"] = "shared.ps2";
                settings["MemoryCards/Slot2_Enable"] = "false"; AddSdlMapping(settings, "Pad1", "DualShock2", true, controls,edition); ApplyControls(settings, edition.Id, "Pad", "InputSources");
                if(localPatches.Any(p=>p.Kind=="pnach"))
                {
                    settings["EmuCore/EnableCheats"]="true";
                    var cheats=Path.Combine(exeDir,"hub-cheats");settings["Folders/Cheats"]=cheats;
                    foreach(var group in localPatches.Where(p=>p.Kind=="pnach").GroupBy(p=>p.Crc))
                        configs[Path.Combine(cheats,group.Key+".pnach")]=string.Join("\n",group.Select(p=>new LocalPatchService(paths,catalog).PnachText(p)));
                }
                configs[Path.Combine(exeDir, "inis", "PCSX2.ini")] = ControllerIni(settings, "Pad1", true, controls,edition);
                var sections=localPatches.Where(p=>p.Kind=="pnach").SelectMany(p=>p.EnabledSections??new LocalPatchService(paths,catalog).Sections(p)).Distinct().ToArray();
                if(sections.Length>0)configs[Path.Combine(exeDir,"inis","PCSX2.ini")]+="\n[Cheats]\n"+string.Join('\n',sections.Select(s=>"Enable="+s))+"\n";
                args.AddRange(["-portable", "-batch", "-nogui", selection.Fullscreen ? "-fullscreen" : "-nofullscreen", "--", mediaPath]); break;
            case "ppsspp":
                configs[Path.Combine(exeDir, "installed.txt")] = save;
                settings["General/FirstRun"] = "false"; settings["General/CheckForNewVersion"] = "false";
                var pspConfig = Path.Combine(exeDir, "hub.ini"); configs[pspConfig] = Ini(settings);
                var pspControls = ControlProfiles.Psp(controls,edition);
                foreach(var (key,value) in Controls(edition.Id)) pspControls[key]=value;
                if (pspControls.Keys.Any(k => !k.StartsWith("ControlMapping/", StringComparison.Ordinal))) throw new HubException("A PSP controller override is outside ControlMapping.");
                // Loading even an empty dedicated file restores upstream keyboard + XInput + generic-pad defaults.
                var controlFile = Path.Combine(exeDir, "controls.ini"); configs[controlFile] = Ini(pspControls); args.Add("--controlconfig=" + controlFile);
                // Stable PPSSPP uses installed.txt for the memory-stick path; --memstick is a later development option.
                args.AddRange(["--config=" + pspConfig, "--pause-menu-exit", selection.Fullscreen ? "--fullscreen" : "--windowed", mediaPath]); break;
            case "rpcs3":
                settings["Miscellaneous/Start games in fullscreen mode"]=selection.Fullscreen?"true":"false";
                var config = Path.Combine(exeDir, "hub.yml"); configs[config] = Yaml(settings);
                configs[Path.Combine(exeDir, "config", "vfs.yml")] = "\"/dev_hdd0/\": " + JsonSerializer.Serialize(Path.Combine(save, "dev_hdd0").Replace('\\', '/') + "/") + "\n";
                var ps3Controls = InputDefaults.RpcS3(BuildVersion.Compare(build.Version, "0.0.43-20250") is >= 0, InputDefaults.ConnectedXInputSlots());
                ControlProfiles.RpcApply(ps3Controls, controls,edition);
                ApplyControls(ps3Controls, edition.Id, "Player ");
                configs[Path.Combine(exeDir, "config", "input_configs", "global", "Default.yml")] = Yaml(ps3Controls);
                configs[Path.Combine(exeDir, "GuiConfigs", "CurrentSettings.ini")] = "[main_window]\ninfoBoxEnabledWelcome=false\ninfoBoxEnabledInstallPUP=false\ninfoBoxEnabledInstallPKG=false\nconfirmationBoxExitGame=false\nconfirmationBoxBootGame=false\n[meta]\ncheckUpdateStart=false\n";
                var boot = media.Format == "ps3-iso" ? mediaPath : File.Exists(Path.Combine(mediaPath, "PS3_GAME", "USRDIR", "EBOOT.BIN")) ? Path.Combine(mediaPath, "PS3_GAME", "USRDIR", "EBOOT.BIN") : Path.Combine(mediaPath, "USRDIR", "EBOOT.BIN");
                args.AddRange(["--no-gui", "--config", config]); if (selection.Fullscreen) args.Add("--fullscreen"); args.Add(boot); break;
            case "xenia":
                InputDefaults.Xenia(settings); ControlProfiles.XeniaApply(settings, controls,edition); ApplyControls(settings, edition.Id, "HID/", "HID.Key/");
                if(controls.ControllerPreset!="Default")
                {
                    var map=Path.Combine(exeDir,"hub-controllerdb.txt");settings["SDL/mappings_file"]=map;configs[map]=ControlProfiles.XeniaMap(controls,edition);
                    XeniaControllerMappings.ApplyEnvironment(environment,configs[map]);
                }
                settings["Storage/content_root"] = save; settings["Storage/storage_root"] = exeDir;
                var localProfile = XeniaProfiles.SelectLocal(save, Path.Combine(exeDir, "xenia-canary.config.toml"));
                if (localProfile != null) settings["Profiles/logged_profile_slot_0_xuid"] = localProfile;
                configs[Path.Combine(exeDir, "xenia-canary.config.toml")] = Toml(settings);
                // An explicit config contains profile overrides, not historical
                // defaults. Without --config, Canary migrates a missing defaults_date
                // from version zero and can silently undo intentional settings.
                args.Add("--config=" + Path.Combine(exeDir, "xenia-canary.config.toml"));
                args.Add("--storage_root=" + exeDir); args.Add("--content_root=" + save); args.Add("--fullscreen=" + selection.Fullscreen.ToString().ToLowerInvariant());
                args.Add(media.Format == "x360-folder" ? Path.Combine(mediaPath, "default.xex") : mediaPath); break;
            default: throw new HubException("No installed adapter supports this backend.");
        }
        return new(edition.GameId, edition.Id, media.Id, profile.Id, build.Id, Path.Combine(exeDir, Path.GetFileName(build.Executable)), exeDir, args, environment, configs, save, extensions, profile.Warnings.Concat(enhancement.Risk.Length > 0 ? [enhancement.Risk] : []).ToArray());
    }
    private static void AddSdlMapping(Dictionary<string, string> settings, string section, string type, bool analog, ControlChoice controls,Edition edition)
    {
        settings[section + "/Type"] = type; settings["InputSources/SDL"] = "true";
        // Native XInput indices are independent of SDL joystick enumeration. Analog
        // keyboards can occupy SDL-0 before a wireless gamepad is connected.
        settings["InputSources/XInput"] = "true";
        foreach (var (name, button) in new Dictionary<string,string>{{"Up","DPadUp"},{"Down","DPadDown"},{"Left","DPadLeft"},{"Right","DPadRight"},{"Triangle","Y"},{"Circle","B"},{"Cross","A"},{"Square","X"},{"L1","LeftShoulder"},{"R1","RightShoulder"},{"L2","+LeftTrigger"},{"R2","+RightTrigger"},{"Start","Start"},{"Select","Back"}}) settings[section+"/"+name]="SDL-0/"+button;
        if (analog) foreach (var (name, axis) in new Dictionary<string,string>{{"LUp","-LeftY"},{"LDown","+LeftY"},{"LLeft","-LeftX"},{"LRight","+LeftX"},{"RUp","-RightY"},{"RDown","+RightY"},{"RLeft","-RightX"},{"RRight","+RightX"},{"L3","LeftStick"},{"R3","RightStick"}}) settings[section+"/"+name]="SDL-0/"+axis;
        foreach(var (button,value) in ControlProfiles.Resolve("pcsx2",controls,false,edition)) settings[section+"/"+button]="SDL-0/"+value;
    }
    private static string ControllerIni(Dictionary<string,string> settings, string section, bool analog, ControlChoice controls,Edition edition, string? legacySection = null)
    {
        // Both upstream INI readers use repeated keys for alternative bindings, not chords.
        // SDL-0 handles a controller connected after launch; keyboard remains available.
        var extra = new StringBuilder();
        foreach(var target in legacySection is null ? new[]{section} : new[]{section,legacySection})
        {
            extra.Append('[').Append(target).AppendLine("]");
            foreach (var (button,key) in ControlProfiles.Resolve("pcsx2",controls,true,edition))
                if (analog || button is not ("LUp" or "LDown" or "LLeft" or "LRight" or "RUp" or "RDown" or "RLeft" or "RRight" or "L3" or "R3"))
                    extra.Append(button).Append("=Keyboard/").AppendLine(key);
            foreach(var (key,value) in settings.Where(x=>x.Key.StartsWith(target+"/",StringComparison.Ordinal) && x.Value.StartsWith("SDL-0/",StringComparison.Ordinal)))
            {
                var button=value[6..] switch {"FaceSouth"=>"A","FaceEast"=>"B","FaceWest"=>"X","FaceNorth"=>"Y",var name=>name};
                for(var slot=0;slot<4;slot++)
                    extra.Append(key[(target.Length+1)..]).Append("=XInput-").Append(slot).Append('/').AppendLine(button);
            }
        }
        // PCSX2's SDL3 adapter uses positional face names; its XInput adapter
        // and DuckStation retain A/B/X/Y. Translate SDL only, after alternatives.
        if(analog && legacySection == null)
            foreach(var key in settings.Keys.Where(k=>k.StartsWith(section+"/",StringComparison.Ordinal)).ToArray())
                settings[key]=settings[key] switch
                {
                    "SDL-0/A"=>"SDL-0/FaceSouth", "SDL-0/B"=>"SDL-0/FaceEast",
                    "SDL-0/X"=>"SDL-0/FaceWest", "SDL-0/Y"=>"SDL-0/FaceNorth", _=>settings[key]
                };
        return Ini(settings) + extra;
    }
    private Dictionary<string,string> Controls(string editionId)
    {
        var file = paths.Managed("config", "controls", editionId, "controller-overrides.json");
        var values = File.Exists(file) ? JsonData.Read<Dictionary<string,string>>(file) : [];
        ValidateSettings(values); return values;
    }
    private void ApplyControls(Dictionary<string,string> settings, string editionId, params string[] prefixes)
    {
        foreach (var (key,value) in Controls(editionId))
        {
            if (!prefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal))) throw new HubException("A controller override is outside the allowed input sections.");
            var resolved = key.StartsWith("Player 1/", StringComparison.Ordinal) ? "Player 1 Input/" + key[9..]
                : key.StartsWith("Controller1/", StringComparison.Ordinal) ? "Pad1/" + key[12..] : key;
            settings[resolved] = value;
        }
    }
    private static void ValidateSettings(Dictionary<string, string> settings)
    {
        foreach (var (key,value) in settings)
        {
            if (!key.Contains('/') || key.IndexOfAny(['\r','\n','[',']']) >= 0 || value.IndexOfAny(['\r','\n','\0']) >= 0) throw new HubException("An invalid configuration value was rejected.");
            if ((key.EndsWith("ResolutionScale") || key.EndsWith("upscale_multiplier") || key.EndsWith("InternalResolution")) && (!double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var scale) || !double.IsFinite(scale) || scale < 1 || scale > 8)) throw new HubException("Resolution scaling must be between 1 and 8.");
        }
    }
    public static string Ini(Dictionary<string,string> settings)
    {
        var b = new StringBuilder();
        foreach (var g in settings.GroupBy(x => x.Key[..x.Key.LastIndexOf('/')]))
        { b.Append('[').Append(g.Key).AppendLine("]"); foreach(var x in g) b.Append(x.Key[(x.Key.LastIndexOf('/')+1)..]).Append('=').AppendLine(x.Value); b.AppendLine(); }
        return b.ToString();
    }
    public static string Yaml(Dictionary<string,string> settings)
    {
        var root = new Dictionary<string,object>();
        foreach (var (key,value) in settings)
        {
            var parts=key.Split('/'); var node=root;
            foreach(var part in parts[..^1])
            {
                if (!node.TryGetValue(part,out var child)) node[part]=child=new Dictionary<string,object>();
                node=child as Dictionary<string,object> ?? throw new HubException("Conflicting configuration paths.");
            }
            if (node.ContainsKey(parts[^1])) throw new HubException("Conflicting configuration paths.");
            node[parts[^1]]=value;
        }
        var b=new StringBuilder();
        void Write(Dictionary<string,object> node,int depth)
        {
            foreach(var (key,value) in node)
            {
                b.Append(' ',depth*2).Append(JsonSerializer.Serialize(key)).Append(':');
                if(value is Dictionary<string,object> child){b.AppendLine();Write(child,depth+1);}
                else b.Append(' ').AppendLine(Scalar((string)value));
            }
        }
        Write(root,0); return b.ToString();
    }
    private static string Toml(Dictionary<string,string> settings)
    {
        var b=new StringBuilder();foreach(var g in settings.GroupBy(x=>x.Key[..x.Key.IndexOf('/')]))
        {
            b.Append('[').Append(g.Key).AppendLine("]");
            foreach(var x in g)
            {
                var key=x.Key[(x.Key.IndexOf('/')+1)..];
                // XUIDs are fixed-width hexadecimal strings, even when all their
                // digits happen to be decimal. Preserve leading zeroes and type.
                var identity=g.Key=="Profiles" && key.StartsWith("logged_profile_slot_",StringComparison.Ordinal) && key.EndsWith("_xuid",StringComparison.Ordinal);
                b.Append(key).Append(" = ").AppendLine(identity?JsonSerializer.Serialize(x.Value):Scalar(x.Value));
            }
        }
        return b.ToString();
    }
    private static string Scalar(string value) => value is "true" or "false" || double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out _) ? value : JsonSerializer.Serialize(value.Replace('\\','/'));
}
