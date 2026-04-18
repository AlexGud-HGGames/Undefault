using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace GsiHost.Services;

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
            if (string.IsNullOrWhiteSpace(secrets?.ClientId) || string.IsNullOrWhiteSpace(secrets.ClientSecret))
            {
                return null;
            }

            return secrets with
            {
                ClientId = secrets.ClientId.Trim(),
                ClientSecret = secrets.ClientSecret.Trim()
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
            secrets.ClientId.Trim(),
            secrets.ClientSecret.Trim());
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
