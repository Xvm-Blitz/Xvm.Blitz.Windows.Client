using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Models;

namespace Xvm.Blitz.Windows.Client.Core.Security;

public sealed class UpdateIntegrityVerifier(HttpClient httpClient, ILogger<UpdateIntegrityVerifier> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task VerifyAsync(
        string filePath,
        GetAppUpdateResponseDto updateInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(updateInfo);

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Файл обновления не найден.", filePath);

        var (sha256Hex, signatureBase64) = await ResolveManifestAsync(updateInfo, cancellationToken);

        if (string.IsNullOrWhiteSpace(sha256Hex) || string.IsNullOrWhiteSpace(signatureBase64))
            throw new InvalidOperationException("Для обновления не найдены sha256 и подпись.");

        var expectedHash = Convert.FromHexString(sha256Hex.Trim());
        if (expectedHash.Length != SHA256.HashSizeInBytes)
            throw new InvalidOperationException("Некорректный формат sha256 обновления.");

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(signatureBase64.Trim());
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Некорректный формат подписи обновления.", exception);
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var actualHash = await SHA256.HashDataAsync(stream, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            throw new InvalidOperationException("Хеш скачанного обновления не совпадает с подписанным.");

        stream.Position = 0;
        using var rsa = RSA.Create();
        rsa.ImportFromPem(UpdateSigningPublicKey.Pem);

        var isValid = rsa.VerifyData(
            stream,
            signature,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss);

        if (!isValid)
            throw new InvalidOperationException("Подпись обновления недействительна.");

        logger.LogInformation("Update integrity verified for {FilePath}", filePath);
    }

    private async Task<(string? Sha256, string? Signature)> ResolveManifestAsync(
        GetAppUpdateResponseDto updateInfo,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(updateInfo.Sha256)
            && !string.IsNullOrWhiteSpace(updateInfo.Signature))
        {
            return (updateInfo.Sha256, updateInfo.Signature);
        }

        if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl)
            || !Uri.TryCreate(updateInfo.DownloadUrl, UriKind.Absolute, out var downloadUri)
            || downloadUri.Scheme != Uri.UriSchemeHttps)
        {
            return (updateInfo.Sha256, updateInfo.Signature);
        }

        var manifestUri = new Uri(downloadUri.AbsoluteUri + ".sig.json", UriKind.Absolute);
        try
        {
            using var response = await httpClient.GetAsync(manifestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Update signature manifest not found at {ManifestUrl}. Status={StatusCode}",
                    manifestUri,
                    response.StatusCode);
                return (updateInfo.Sha256, updateInfo.Signature);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateSignatureManifestDto>(
                stream,
                JsonOptions,
                cancellationToken);

            return (
                Coalesce(updateInfo.Sha256, manifest?.Sha256),
                Coalesce(updateInfo.Signature, manifest?.Signature));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Failed to download update signature manifest from {ManifestUrl}", manifestUri);
            return (updateInfo.Sha256, updateInfo.Signature);
        }
    }

    private static string? Coalesce(string? primary, string? fallback) =>
        !string.IsNullOrWhiteSpace(primary) ? primary : fallback;

    private sealed record UpdateSignatureManifestDto(
        [property: JsonPropertyName("sha256")] string? Sha256,
        [property: JsonPropertyName("signature")] string? Signature);
}
