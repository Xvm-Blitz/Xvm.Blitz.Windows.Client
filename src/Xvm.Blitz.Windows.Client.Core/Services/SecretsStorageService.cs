using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public class SecretsStorageService : ISecretsStorageService
{
    private static readonly byte[] Entropy = "XvmBlitz2025"u8.ToArray();

    private readonly ILogger<SecretsStorageService> _logger;

    private readonly string _secretsFilePath;

    public SecretsStorageService(ILogger<SecretsStorageService> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(AppDataPaths.AppFolder);
        _secretsFilePath = Path.Combine(AppDataPaths.AppFolder, "xvm_blitz_secrets.dat");
    }

    public async Task Save(byte[] data)
    {
#pragma warning disable CA1416
        var encryptedBytes = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
        await File.WriteAllBytesAsync(_secretsFilePath, encryptedBytes);
    }

    public async Task<byte[]?> Load()
    {
        try
        {
            if (!File.Exists(_secretsFilePath))
            {
                _logger.LogInformation("Secrets file not found");

                return null;
            }

            var encryptedBytes = await File.ReadAllBytesAsync(_secretsFilePath);
#pragma warning disable CA1416
            var plainTextBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
            return plainTextBytes;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error loading secrets");
            await Clear();

            return null;
        }
    }

    public Task Clear()
    {
        try
        {
            if (File.Exists(_secretsFilePath))
            {
                File.Delete(_secretsFilePath);

                _logger.LogInformation("Secrets removed");
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error deleting secrets");
        }

        return Task.CompletedTask;
    }
}