using System.Security.Cryptography;
using System.Text.Json;

namespace Hub.Core;

public sealed record SignedCatalog(string PayloadBase64, string SignatureBase64, string KeyId);
public sealed class ManifestUpdates(HubPaths paths)
{
    public Catalog Install(SignedCatalog envelope, string trustedPublicKeyPem, string trustedKeyId)
    {
        if (envelope.KeyId != trustedKeyId) throw new HubException("This manifest was signed by an unknown publisher.");
        byte[] payload, signature;
        try { payload = Convert.FromBase64String(envelope.PayloadBase64); signature = Convert.FromBase64String(envelope.SignatureBase64); }
        catch (FormatException e) { throw new HubException("The signed manifest is malformed.", e); }
        using var rsa = RSA.Create(); rsa.ImportFromPem(trustedPublicKeyPem);
        if (!rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) throw new HubException("Manifest signature verification failed. Your catalog was not changed.");
        var catalog = JsonSerializer.Deserialize<Catalog>(payload, JsonData.Options) ?? throw new HubException("The manifest is empty.");
        CatalogStore.Validate(catalog);
        var path = paths.Managed("manifests", "catalog.json");
        if (File.Exists(path))
        {
            var current = JsonData.Read<Catalog>(path); var comparison = BuildVersion.Compare(catalog.Version, current.Version);
            if (comparison == null || comparison <= 0) throw new HubException("The manifest is not newer. Use explicit rollback to restore an earlier catalog.");
        }
        JsonData.WriteAtomic(path, catalog); return catalog;
    }
    public Catalog Rollback()
    {
        var path = paths.Managed("manifests", "catalog.json"); var previous = path + ".previous";
        if (!File.Exists(previous)) throw new HubException("There is no previous catalog to restore.");
        var catalog = JsonData.Read<Catalog>(previous); CatalogStore.Validate(catalog); JsonData.WriteAtomic(path, catalog); return catalog;
    }
}
