using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GsiHost.Services;

/// <summary>
/// DPAPI-encrypted (CurrentUser scope) per-user store for the Spotify client identity.
/// Post-UND-47 the only payload is the public <c>client_id</c>; PKCE removes the
/// secret half. Files written by older builds are still readable — extra JSON fields
/// (<c>ClientSecret</c>) are ignored at deserialization. The on-disk path and
/// <c>v1</c> additional-entropy tag are kept stable so a tester upgrading does not
/// have to re-prompt.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsProtectedSpotifySecretStore : ISpotifySecretStore
{
    private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("UndefaultIt.SpotifySecrets.v1");

    public WindowsProtectedSpotifySecretStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var root = string.IsNullOrWhiteSpace(appData)
            ? AppContext.BaseDirectory
            : Path.Combine(appData, "UndefaultIt");
        FilePath = Path.Combine(root, "spotify-secrets.bin");
    }

    public string FilePath { get; }

    public bool Exists()
    {
        return File.Exists(FilePath);
    }

    public SpotifyLocalSecrets? TryLoad()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var protectedBytes = File.ReadAllBytes(FilePath);
            if (protectedBytes.Length == 0)
            {
                return null;
            }

            var jsonBytes = ProtectedData.Unprotect(protectedBytes, AdditionalEntropy, DataProtectionScope.CurrentUser);
            var secrets = JsonSerializer.Deserialize<SpotifyLocalSecrets>(jsonBytes);
            if (string.IsNullOrWhiteSpace(secrets?.ClientId))
            {
                return null;
            }

            return secrets with
            {
                ClientId = secrets.ClientId.Trim()
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Save(SpotifyLocalSecrets secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        var payload = new SpotifyLocalSecrets(
            secrets.ClientId.Trim());
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var protectedBytes = ProtectedData.Protect(jsonBytes, AdditionalEntropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, protectedBytes);
    }

    public void Delete()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }
}
