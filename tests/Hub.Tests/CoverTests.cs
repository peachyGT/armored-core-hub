using System.Net;
using System.Security.Cryptography;
using Hub.Core;

static class CoverTests
{
    public static void Register(Action<string, Action> test, string scratch, Catalog games)
    {
        void Check(bool value) { if (!value) throw new Exception("Cover fixture assertion failed"); }
        void Reject(Action action) { try { action(); } catch (HubException) { return; } throw new Exception("Invalid cover accepted"); }
        CoverCatalog Load() => JsonData.Read<CoverCatalog>(Path.Combine(AppContext.BaseDirectory, "manifests", "covers.json"));
        test("Every catalog edition resolves to a sourced cover on the same game and platform", () => {
            var c = Load(); c.Validate(games); Check(c.Editions.Count == games.Editions.Count);
            foreach (var e in games.Editions) { var r = c.Resolve(e.Id)!; Check(r.Asset.GameId == e.GameId && r.Asset.Platform == e.Platform); }
            Check(c.Resolve("ac3-psp-uljm05492")!.Asset.Sha256 != c.Resolve(games.Editions.First(e => e.GameId == "ac3" && e.Platform == "ps2").Id)!.Asset.Sha256);
            Check(c.Resolve("acfa-ps3-bljm60066")!.Asset.Sha256 != c.Resolve("acfa-ps3-blus30187")!.Asset.Sha256);
        });
        test("Cover catalog rejects unknown schemas, cycles, platforms and download hosts", () => {
            var c = Load(); c.SchemaVersion = 2; Reject(() => c.Validate(games));
            c = Load(); var e = c.Editions[0]; c.Editions[0] = e with { AssetId = null, FallbackEditionId = e.EditionId, Note = "Loop" }; Reject(() => c.Validate(games));
            c = Load(); c.Editions[0] = c.Editions[0] with { AssetId = c.Assets.First(a => a.Platform == "psp").Id }; Reject(() => c.Validate(games));
            c = Load(); c.Assets[0] = c.Assets[0] with { Uri = "https://unapproved.invalid/cover.jpg" }; Reject(() => c.Validate(games));
        });
        test("Regional and reissue fallbacks stay explicit", () => {
            var c = Load(); var e = c.Editions.First(e => e.FallbackEditionId != null); Check(c.Resolve(e.EditionId)!.Note == e.Note);
            c.Editions[c.Editions.IndexOf(e)] = e with { Note = null }; Reject(() => c.Validate(games));
            Check(c.Resolve("unknown-edition") == null);
        });
        test("Concurrent cover requests deduplicate and cached covers work after relocation offline", () => {
            var bytes = "Synthetic cover fixture"u8.ToArray(); var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var c = Load(); var e = c.Editions.First(x => x.AssetId != null); var index = c.Assets.FindIndex(a => a.Id == e.AssetId);
            c.Assets[index] = c.Assets[index] with { Sha256 = hash, Bytes = bytes.Length };
            var paths = new HubPaths(Path.Combine(scratch, "cover-cache"));
            var handler = new FakeHttp((_, _) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            using var http = new HttpClient(handler);
            var service = new CoverService(c, new(paths, http));
            Task.WhenAll(service.FetchAsync(e.EditionId, false), service.FetchAsync(e.EditionId, false)).GetAwaiter().GetResult();
            Check(handler.Count == 1 && service.Cached(e.EditionId) != null);
            var moved = paths.Root + "-moved"; Directory.Move(paths.Root, moved);
            var offline = new CoverService(c, new(new HubPaths(moved), http));
            Check(offline.FetchAsync(e.EditionId, true).GetAwaiter().GetResult()!.StartsWith(moved)); Check(handler.Count == 1);
            File.WriteAllText(offline.Cached(e.EditionId)!, "corrupt");
            var corrupted = new CoverService(c, new(new HubPaths(moved), http));
            Reject(() => corrupted.FetchAsync(e.EditionId, true).GetAwaiter().GetResult()); Check(corrupted.Cached(e.EditionId) == null);
        });
    }
}
