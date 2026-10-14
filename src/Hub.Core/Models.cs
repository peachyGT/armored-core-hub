namespace Hub.Core;

public sealed record Evidence(string Url, string Conclusion, string Confidence, string Accessed = "2026-10-09");
public sealed record Game(string Id, string Title, string ShortTitle, int Generation, int Year, bool Optional, string Description, string Mark, string? DisplayTitle = null, string? DisplaySubtitle = null);
public sealed record Edition(string Id, string GameId, string Platform, string Region, string Serial, string Release,
    string[] Formats, string PreferredProfile, string[] AlternateProfiles, string SaveFamily, string[] Structure,
    HashReference[] Hashes, string[] Warnings, Evidence[] Sources, int Disc = 1, string? TitleId = null);
public sealed record HashReference(string Algorithm, string Value, string Release, string Source, string? Format = null, long? Size = null);
public sealed record Backend(string Id, string Name, string Platform, string ProjectUrl, string License,
    string ReleaseEndpoint, string AssetPattern, string Executable, string Channel, string FirmwareClass,
    string[] Formats, ReleaseAsset? Recommended, string LicenseUrl, string[] Arguments, string UpdatePolicy = "manual-immutable",
    Dictionary<string, string>? MinimumBuildsByFormat = null);
public sealed record ReleaseAsset(string Version, string Tag, string Uri, string Sha256, string FileName, string Published, string Provenance);
public sealed record CompatibilityProfile(string Id, string EditionId, string BackendId, string Version,
    string? TestedBuild, string TestStatus, Dictionary<string, string> Settings, string[] RequiredPatches,
    string[] OptionalPatches, string[] Enhancements, string DefaultEnhancement, string[] Warnings,
    Evidence[] Sources, string InputSupport = "controller", string MouseSupport = "future-extension",
    BuildRecommendation? BuildRecommendation = null);
public sealed record BuildRecommendation(ReleaseAsset Release, string[] Formats, string? Preparation, BuildIssue[] Issues);
public sealed record BuildIssue(string First, string Last, string Message);
public sealed record EnhancementProfile(string Id, string Name, string ProfileId, Dictionary<string, string> Settings,
    string Benefit, string Drawback, string HardwareCost, string Risk);
public sealed record PatchModule(string Id, string Name, string Author, string Version, string Category, string Upstream,
    string Redistribution, string[] Editions, string[] SourceHashes, string[] Dependencies, string[] Incompatibilities,
    string Method, Dictionary<string, string> Settings, string? PayloadSha256, string? ProviderId,
    string Benefit, string Drawback, bool GameplayChanging, string UpdateStrategy = "explicit");
public sealed record InputExtension(string Id, string Name, string Version, string[] Editions, string[] RequiredHashes,
    string[] RequiredBuilds, string[] Dependencies, string[] Capabilities, string Phase, string? Executable,
    string[] Arguments, string? Sha256, Dictionary<string, string> Options, bool Experimental = true);
public sealed record FirmwareDefinition(string Id, string Name, long[] Sizes, HashReference[] Hashes, string GuideUrl,
    string RequiredFor, string? OfficialUri = null, string? OfficialSha256 = null);
public sealed record AssetProvider(string Id, string Kind, int Priority, string PrimaryUri, string[] FallbackUris,
    string Sha256, string ExpectedContentType, string License, string[] AllowedHosts,
    int Retries = 2, long MaxBytes = 2147483648, string CachePolicy = "immutable", string? Signature = null);

public sealed class Catalog
{
    public int SchemaVersion { get; set; } = 1;
    public string Version { get; set; } = "1.0.0";
    public List<Game> Games { get; set; } = [];
    public List<Edition> Editions { get; set; } = [];
    public List<Backend> Backends { get; set; } = [];
    public List<CompatibilityProfile> Profiles { get; set; } = [];
    public List<EnhancementProfile> Enhancements { get; set; } = [];
    public List<PatchModule> Patches { get; set; } = [];
    public List<InputExtension> Extensions { get; set; } = [];
    public List<FirmwareDefinition> Firmware { get; set; } = [];
    public List<AssetProvider> Providers { get; set; } = [];
}

public enum MediaConfidence { UnknownRevision, RecognizedUnverified, VerifiedExactDump, ModifiedImage }
public sealed record MediaFile(string RelativeName, long Size, string Sha256, long LastWriteUtcTicks);
public sealed record MediaRecord(string Id, string Path, string Format, string? EditionId, string Fingerprint,
    MediaConfidence Confidence, string Recognition, List<MediaFile> Files, DateTimeOffset ImportedAt, bool Managed);
public sealed record MediaCandidate(string Path, string Format, string? EditionId, string? ProductId, string Reason, string[] Warnings,
    string? Platform = null, string? Problem = null, string? ArchiveSource = null);
public sealed record FirmwareRecord(string Id, string Class, string Path, string Sha256, string Label, bool Verified, string Region, string? RomVersion = null);
public sealed record InstalledBuild(string Id, string BackendId, string Version, string Path, string Executable,
    string ArchiveSha256, string ExecutableSha256, string Source, DateTimeOffset InstalledAt, bool Imported = false);
public sealed class GameSelection
{
    public string EditionId { get; set; } = "";
    public string? MediaId { get; set; }
    public string? ProfileId { get; set; }
    public string? BuildId { get; set; }
    public string? EnhancementId { get; set; }
    public List<string> PatchIds { get; set; } = [];
    public List<string> LocalPatchIds { get; set; } = [];
    public List<string> ExtensionIds { get; set; } = [];
    public Dictionary<string, string> AdvancedSettings { get; set; } = [];
    public bool Fullscreen { get; set; } = true;
    public GraphicsChoice Graphics { get; set; } = new();
    public ControlChoice Controls { get; set; } = new();
    public Dictionary<string, ProfileChoice> ProfileChoices { get; set; } = [];
}
public sealed record ProfileChoice(string? BuildId, string? EnhancementId, List<string> PatchIds, Dictionary<string,string> Settings);
public sealed class HubState
{
    public int SchemaVersion { get; set; } = 1;
    public bool FirstRunComplete { get; set; }
    public bool Offline { get; set; }
    public List<string> ScanRoots { get; set; } = [];
    public List<MediaRecord> Media { get; set; } = [];
    public List<InstalledBuild> Builds { get; set; } = [];
    public List<FirmwareRecord> Firmware { get; set; } = [];
    public Dictionary<string, GameSelection> Selections { get; set; } = [];
    public Dictionary<string, ProviderHealth> ProviderHealth { get; set; } = [];
    public Dictionary<string, string> PreferredEditions { get; set; } = [];
}
public sealed record ProviderHealth(string Status, DateTimeOffset LastChecked, DateTimeOffset? LastSuccess, string? Message);
public sealed record ProgressInfo(string Message, long Completed = 0, long? Total = null, string Unit = "bytes");
public sealed record Diagnostic(string Name, string Status, string Message, string? Repair = null);
public sealed record SaveSnapshot(string Id, string Family, string GameId, string BackendId, string Reason,
    DateTimeOffset CreatedAt, string Path, List<MediaFile> Files, string? SourceGame = null, string? TargetGame = null);
public sealed record LaunchPlan(string GameId, string EditionId, string MediaId, string ProfileId, string BuildId,
    string Executable, string WorkingDirectory, List<string> Arguments, Dictionary<string, string> Environment,
    Dictionary<string, string> ConfigFiles, string SaveDirectory, List<InputExtension> Extensions, string[] Warnings);
public sealed class HubException(string message, Exception? inner = null) : Exception(message, inner);
