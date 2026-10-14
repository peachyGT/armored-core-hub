using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hub.Core;

public static class CatalogStore
{
    public static Catalog Load(string folder)
    {
        var catalog = JsonData.Read<Catalog>(Path.Combine(folder, "catalog.json"));
        Validate(catalog); return catalog;
    }
    public static void Validate(Catalog c)
    {
        if (c.SchemaVersion != 1) throw new HubException("Unsupported catalog schema. Update the Hub before importing this catalog.");
        Unique(c.Games.Select(x => x.Id)); Unique(c.Editions.Select(x => x.Id)); Unique(c.Backends.Select(x => x.Id));
        Unique(c.Profiles.Select(x => x.Id)); Unique(c.Enhancements.Select(x => x.Id)); Unique(c.Patches.Select(x => x.Id)); Unique(c.Extensions.Select(x => x.Id));
        if (c.Games.Any(x => x.Id == "ac6" || x.Title.Contains("Fires of Rubicon", StringComparison.OrdinalIgnoreCase) || x.Title.Contains("Mobile", StringComparison.OrdinalIgnoreCase)))
            throw new HubException("Native Windows games do not belong in this catalog.");
        foreach (var e in c.Editions)
        {
            Require(c.Games.Any(x => x.Id == e.GameId), "Edition has no game: " + e.Id);
            Require(new[] {"ps1", "ps2", "psp", "ps3", "x360"}.Contains(e.Platform), "Unsupported platform: " + e.Platform);
            SafeFiles.Id(e.SaveFamily);
            foreach (var p in e.AlternateProfiles.Prepend(e.PreferredProfile)) Require(c.Profiles.Any(x => x.Id == p && x.EditionId == e.Id), "Edition profile is missing: " + p);
            foreach (var h in e.Hashes)
            {
                ValidateHash(h.Algorithm, h.Value);
                Require(h.Format == null || e.Formats.Contains(h.Format), "Reference hash format does not belong to the edition.");
                Require(h.Size == null || h.Size > 0, "Invalid reference media size.");
            }
        }
        foreach (var p in c.Profiles)
        {
            var e = c.Editions.SingleOrDefault(x => x.Id == p.EditionId);
            Require(e != null && c.Backends.Any(x => x.Id == p.BackendId && x.Platform == e.Platform), "Profile backend/platform mismatch: " + p.Id);
            foreach (var patch in p.RequiredPatches.Concat(p.OptionalPatches)) Require(c.Patches.Any(x => x.Id == patch && x.Editions.Contains(p.EditionId)), "Missing/incompatible patch: " + patch);
            Require(p.Enhancements.Contains(p.DefaultEnhancement), "Missing default enhancement: " + p.Id);
            foreach (var id in p.Enhancements) Require(c.Enhancements.Any(x => x.Id == id && x.ProfileId == p.Id), "Missing enhancement: " + id);
            if (p.TestStatus is "locally-tested" or "locally-sampled") Require(!string.IsNullOrWhiteSpace(p.TestedBuild), "Tested profile needs an exact build.");
            if (p.BuildRecommendation is { } recommendation)
            {
                var backend = c.Backends.Single(b => b.Id == p.BackendId);
                ValidateHash("sha256", recommendation.Release.Sha256);
                Require(Uri.TryCreate(recommendation.Release.Uri, UriKind.Absolute, out var releaseUri) && releaseUri.Scheme == "https"
                    && releaseUri.Host == "github.com" && releaseUri.AbsolutePath.Contains("/releases/download/"), "Profile builds require an official release URL.");
                Require(recommendation.Formats.Length > 0 && recommendation.Formats.All(f => e!.Formats.Contains(f) && backend.Formats.Contains(f)), "Invalid profile build format.");
                Require(recommendation.Preparation == null || recommendation.Preparation == "ps3-iso9660" && backend.Id == "rpcs3" && recommendation.Formats.Contains("ps3-folder"), "Unsupported media preparation.");
                foreach (var f in recommendation.Formats)
                    if (backend.MinimumBuildsByFormat?.GetValueOrDefault(f) is string minimum)
                        Require(BuildVersion.Compare(recommendation.Release.Version, minimum) is >= 0, "Recommended build cannot read its declared format.");
                foreach (var issue in recommendation.Issues)
                    Require(BuildVersion.Compare(issue.First, issue.Last) is <= 0 && !string.IsNullOrWhiteSpace(issue.Message), "Invalid affected-build range.");
            }
        }
        foreach (var p in c.Patches)
        {
            foreach (var id in p.Editions) Require(c.Editions.Any(e => e.Id == id), "Patch edition missing: " + id);
            foreach (var id in p.Dependencies.Concat(p.Incompatibilities)) Require(c.Patches.Any(x => x.Id == id), "Patch reference missing: " + id);
        }
        foreach (var patch in c.Patches) ResolvePatches(c, [patch.Id]);
        foreach (var b in c.Backends)
        {
            if (b.Recommended != null) ValidateHash("sha256", b.Recommended.Sha256);
            Require(Uri.TryCreate(b.ReleaseEndpoint, UriKind.Absolute, out var uri) && uri.Scheme == "https", "Release endpoints require HTTPS.");
            foreach (var (format, minimum) in b.MinimumBuildsByFormat ?? [])
                Require(b.Formats.Contains(format) && BuildVersion.Compare(minimum, "0.0.0") is >= 0, "Invalid minimum build for a media format.");
        }
        foreach (var p in c.Providers) ProviderDownloader.ValidateProvider(p);
    }
    public static List<PatchModule> ResolvePatches(Catalog c, IEnumerable<string> ids)
    {
        var done = new HashSet<string>(); var active = new HashSet<string>(); var ordered = new List<PatchModule>();
        void Visit(string id)
        {
            if (done.Contains(id)) return;
            if (!active.Add(id)) throw new HubException("Patch dependency cycle: " + id);
            var patch = c.Patches.SingleOrDefault(x => x.Id == id) ?? throw new HubException("Unknown patch: " + id);
            foreach (var dep in patch.Dependencies) Visit(dep);
            active.Remove(id); done.Add(id); ordered.Add(patch);
        }
        foreach (var id in ids) Visit(id);
        foreach (var patch in ordered) if (patch.Incompatibilities.Any(done.Contains)) throw new HubException("Incompatible patches: " + patch.Name);
        return ordered;
    }
    public static void ValidateHash(string algorithm, string value)
    {
        var length = algorithm.ToLowerInvariant() switch { "sha256" => 64, "sha1" => 40, "md5" => 32, _ => throw new HubException("Unsupported hash algorithm.") };
        Require(value.Length == length && value.All(Uri.IsHexDigit), "Invalid " + algorithm + " hash.");
    }
    private static void Unique(IEnumerable<string> ids)
    { var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); foreach (var id in ids) if (!set.Add(SafeFiles.Id(id))) throw new HubException("Duplicate identifier: " + id); }
    private static void Require(bool condition, string message) { if (!condition) throw new HubException(message); }
}

public static class BuildVersion
{
    // Rolling commit ids are identities, not semantic versions. Do not pretend hashes have temporal ordering.
    public static int? Compare(string left, string right)
    {
        if (left == right) return 0;
        static (int[] Numbers, string Suffix)? Parse(string value)
        {
            var m = Regex.Match(value, @"^v?(\d+)\.(\d+)(?:\.(\d+))?(?:-(\d+))?(.*)$");
            if (!m.Success) return null;
            return (Enumerable.Range(1, 4).Select(i => int.TryParse(m.Groups[i].Value, out var n) ? n : 0).ToArray(), m.Groups[5].Value);
        }
        var a = Parse(left); var b = Parse(right); if (a == null || b == null) return null;
        for (var i = 0; i < 4; i++) { var v = a.Value.Numbers[i].CompareTo(b.Value.Numbers[i]); if (v != 0) return v; }
        if (a.Value.Suffix == b.Value.Suffix) return 0;
        if (a.Value.Suffix.Length == 0) return 1; if (b.Value.Suffix.Length == 0) return -1;
        return StringComparer.Ordinal.Compare(a.Value.Suffix, b.Value.Suffix);
    }
}
