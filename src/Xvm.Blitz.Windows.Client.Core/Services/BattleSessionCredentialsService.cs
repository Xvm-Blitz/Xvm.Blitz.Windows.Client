using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public sealed class BattleSessionCredentialsService(ILogger<BattleSessionCredentialsService> logger)
    : IBattleSessionCredentialsService
{
    private static readonly byte[] Entropy = "XvmBlitzSession2026"u8.ToArray();

    private readonly string _secretsFilePath = Path.Combine(AppDataPaths.AppFolder, "xvm_blitz_session_secrets.dat");

    public async Task SaveSecretKey(string secretKey)
    {
        Directory.CreateDirectory(AppDataPaths.AppFolder);
        var payload = JsonSerializer.Serialize(new SessionSecretPayload(secretKey));
        var plainBytes = Encoding.UTF8.GetBytes(payload);
#pragma warning disable CA1416
        var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
        await File.WriteAllBytesAsync(_secretsFilePath, encryptedBytes);
    }

    public async Task<string?> LoadSecretKey()
    {
        try
        {
            if (!File.Exists(_secretsFilePath))
                return null;

            var encryptedBytes = await File.ReadAllBytesAsync(_secretsFilePath);
#pragma warning disable CA1416
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
            var payload = JsonSerializer.Deserialize<SessionSecretPayload>(Encoding.UTF8.GetString(plainBytes));

            return string.IsNullOrWhiteSpace(payload?.SecretKey) ? null : payload.SecretKey;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error loading session secret key");
            await Clear();

            return null;
        }
    }

    public Task Clear()
    {
        try
        {
            if (File.Exists(_secretsFilePath))
                File.Delete(_secretsFilePath);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error deleting session secret key");
        }

        return Task.CompletedTask;
    }

    private sealed record SessionSecretPayload([property: JsonPropertyName("secret_key")] string SecretKey);
}
