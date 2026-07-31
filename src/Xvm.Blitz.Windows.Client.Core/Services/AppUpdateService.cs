using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Helpers;
using Xvm.Blitz.Windows.Client.Core.Models;
using Xvm.Blitz.Windows.Client.Core.Security;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public class AppUpdateService(
    HttpClient httpClient,
    UpdateIntegrityVerifier updateIntegrityVerifier,
    ILogger<AppUpdateService> logger) : IAppUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<GetAppUpdateResponseDto?> GetLatestVersion(
        string currentVersion,
        ClientPlatform platform,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUri = $"v1/releases?current_version={Uri.EscapeDataString(currentVersion)}&platform={platform}";

            var response = await httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Failed to get app update info. Status code: {StatusCode}",
                    response.StatusCode);

                return null;
            }

            var updateInfo = await response.Content.ReadFromJsonAsync<GetAppUpdateResponseDto>(
                JsonOptions,
                cancellationToken);

            if (updateInfo != null)
            {
                logger.LogInformation(
                    "App update info received: Version={Version}, Platform={Platform}",
                    updateInfo.Version,
                    updateInfo.Platform);
            }

            return updateInfo;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Error getting app update information");
            return null;
        }
    }

    public async Task DownloadAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri)
            || downloadUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("URL обновления должен использовать HTTPS.");

        var safeDestinationPath = NormalizeAndValidateExePath(
            destinationPath,
            mustBeUnderDirectory: AppDataPaths.UpdatesFolder);

        var destinationDirectory = Path.GetDirectoryName(safeDestinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        var tempPath = safeDestinationPath + ".partial";
        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using var response = await httpClient.GetAsync(
                downloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            long downloadedBytes = 0;

            await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = new FileStream(
                             tempPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    downloadedBytes += read;

                    if (totalBytes is > 0)
                        progress?.Report(Math.Clamp(downloadedBytes * 100d / totalBytes.Value, 0, 100));
                    else
                        progress?.Report(0);
                }

                await fileStream.FlushAsync(cancellationToken);
            }

            if (File.Exists(safeDestinationPath))
                File.Delete(safeDestinationPath);

            File.Move(tempPath, safeDestinationPath);
            progress?.Report(100);

            logger.LogInformation(
                "App update downloaded to {DestinationPath}, size={Size} bytes",
                safeDestinationPath,
                downloadedBytes);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // ignored
            }

            throw;
        }
    }

    public Task VerifyIntegrityAsync(
        string filePath,
        GetAppUpdateResponseDto updateInfo,
        CancellationToken cancellationToken = default) =>
        updateIntegrityVerifier.VerifyAsync(filePath, updateInfo, cancellationToken);

    public void ApplyUpdateAndRestart(string downloadedExePath, string currentExePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadedExePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentExePath);

        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Не удалось определить путь к текущему приложению.");

        var sourcePath = NormalizeAndValidateExePath(
            downloadedExePath,
            mustBeUnderDirectory: AppDataPaths.UpdatesFolder);

        var targetPath = NormalizeAndValidateExePath(currentExePath);

        if (!string.Equals(
                Path.GetFullPath(targetPath),
                Path.GetFullPath(processPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Целевой путь обновления не совпадает с текущим процессом.");
        }

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Файл обновления не найден.", sourcePath);

        var processId = Environment.ProcessId;
        const string script =
            "$processId = [int]$env:XVM_UPDATE_PID; " +
            "$source = $env:XVM_UPDATE_SRC; " +
            "$target = $env:XVM_UPDATE_DST; " +
            "if ($processId -le 0) { exit 1 }; " +
            "if ([string]::IsNullOrWhiteSpace($source)) { exit 1 }; " +
            "if ([string]::IsNullOrWhiteSpace($target)) { exit 1 }; " +
            "while (Get-Process -Id $processId -ErrorAction SilentlyContinue) { Start-Sleep -Seconds 1 }; " +
            "Copy-Item -LiteralPath $source -Destination $target -Force; " +
            "Start-Process -FilePath $target; " +
            "Remove-Item -LiteralPath $source -Force -ErrorAction SilentlyContinue";

        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var powerShellPath = Path.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");

        var startInfo = new ProcessStartInfo
        {
            FileName = powerShellPath,
            Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encodedCommand,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory
        };

        startInfo.Environment["XVM_UPDATE_PID"] = processId.ToString();
        startInfo.Environment["XVM_UPDATE_SRC"] = sourcePath;
        startInfo.Environment["XVM_UPDATE_DST"] = targetPath;

        if (Process.Start(startInfo) is null)
            throw new InvalidOperationException("Не удалось запустить установщик обновления.");

        logger.LogInformation(
            "App update apply started in-memory. Pid={Pid}, Source={Source}, Target={Target}",
            processId,
            sourcePath,
            targetPath);
    }

    private static string NormalizeAndValidateExePath(string path, string? mustBeUnderDirectory = null)
    {
        var fullPath = Path.GetFullPath(path);

        if (!Path.IsPathRooted(fullPath))
            throw new InvalidOperationException("Путь обновления должен быть абсолютным.");

        if (!fullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Файл обновления должен иметь расширение .exe.");

        foreach (var character in fullPath)
        {
            if (character is '"' or '\'' or '`' or '$' or ';' or '&' or '|' or '<' or '>' or '^' or '%' or '!'
                or '\0' or '\n' or '\r')
                throw new InvalidOperationException("Путь обновления содержит недопустимые символы.");
        }

        if (mustBeUnderDirectory is not null)
        {
            var root = Path.GetFullPath(mustBeUnderDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Файл обновления должен находиться в каталоге updates.");
        }

        return fullPath;
    }
}
