using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions;
using Xvm.Blitz.Windows.Client.Core.Services.Abstractions.Authorization;

namespace Xvm.Blitz.Windows.Client.Core.Services;

public class AuthorizationService(
    ISecretsStorageService secretsStorage,
    IOpenIdAuthClient openIdAuthClient,
    ILogger<AuthorizationService> logger) : IAuthorizationService
{
    public const int OpenIdCallbackPort = 17890;

    private static readonly object Sync = new();

    private static string? _accessToken;

    private static string? _refreshToken;

    private static DateTimeOffset? _lestaExpiresAt;

    private static DateTimeOffset? _expiresAt;

    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    private static readonly SemaphoreSlim OpenIdLoginLock = new(1, 1);

    private static CancellationTokenSource? _openIdLoginCts;

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);

    public bool HasOpenIdSession => !string.IsNullOrWhiteSpace(_accessToken);

    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var secretBytes = await secretsStorage.Load();
            if (secretBytes is null)
                return false;

            var json = Encoding.UTF8.GetString(secretBytes);
            var secret = JsonSerializer.Deserialize<AuthCredentialsSecret>(json);
            if (secret is null)
                return false;

            lock (Sync)
            {
                _accessToken = string.IsNullOrWhiteSpace(secret.AccessToken) ? null : secret.AccessToken;
                _refreshToken = string.IsNullOrWhiteSpace(secret.RefreshToken) ? null : secret.RefreshToken;
                _lestaExpiresAt = secret.LestaExpiresAt;
                _expiresAt = secret.ExpiresAt ?? (_accessToken is null ? null : TryGetJwtExpiry(_accessToken));
            }

            if (!IsAuthenticated)
                return false;

            await EnsureValidAccessTokenAsync(CancellationToken.None);

            return IsAuthenticated;
        }
        catch (Exception)
        {
            await secretsStorage.Clear();
            ClearInMemory();

            return false;
        }
    }

    public async Task<bool> LoginWithOpenIdAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource linkedCts;
        lock (Sync)
        {
            _openIdLoginCts?.Cancel();
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromMinutes(5));
            _openIdLoginCts = linkedCts;
        }

        await OpenIdLoginLock.WaitAsync(cancellationToken);
        try
        {
            if (linkedCts.IsCancellationRequested)
                return false;

            var prefix = $"http://127.0.0.1:{OpenIdCallbackPort}/";
            using var listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
            }
            catch (HttpListenerException exception) when (exception.ErrorCode is 183 or 32)
            {
                logger.LogWarning(exception, "Порт OpenID callback занят, повторная попытка");
                await Task.Delay(500, linkedCts.Token);
                listener.Start();
            }

            try
            {
                var loginUri = openIdAuthClient.GetLoginUri();
                Process.Start(new ProcessStartInfo(loginUri.AbsoluteUri) { UseShellExecute = true });

                var context = await listener.GetContextAsync().WaitAsync(linkedCts.Token);
                var accessToken = context.Request.QueryString["access_token"];
                var refreshToken = context.Request.QueryString["refresh_token"];
                var lestaExpiresAtRaw = context.Request.QueryString["lesta_expires_at"];
                var expiresAtRaw = context.Request.QueryString["expires_at"];

                await WriteCallbackResponseAsync(context.Response);

                if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
                {
                    logger.LogWarning("OpenID callback без токенов");

                    return false;
                }

                DateTimeOffset? lestaExpiresAt = null;
                if (long.TryParse(lestaExpiresAtRaw, out var lestaUnixSeconds))
                    lestaExpiresAt = DateTimeOffset.FromUnixTimeSeconds(lestaUnixSeconds);

                DateTimeOffset? expiresAt = null;
                if (long.TryParse(expiresAtRaw, out var expiresUnixSeconds))
                    expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUnixSeconds);
                expiresAt ??= TryGetJwtExpiry(accessToken);

                lock (Sync)
                {
                    _accessToken = accessToken;
                    _refreshToken = refreshToken;
                    _lestaExpiresAt = lestaExpiresAt;
                    _expiresAt = expiresAt;
                }

                await PersistAsync();
                logger.LogInformation("OpenID авторизация успешна");

                return true;
            }
            finally
            {
                if (listener.IsListening)
                    listener.Stop();
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Ожидание OpenID callback отменено или истекло");

            return false;
        }
        catch (HttpListenerException exception) when (exception.ErrorCode is 183 or 32)
        {
            logger.LogError(exception, "Порт {Port} занят другим процессом. Закройте другие экземпляры клиента и повторите вход", OpenIdCallbackPort);

            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Ошибка OpenID авторизации");

            return false;
        }
        finally
        {
            OpenIdLoginLock.Release();
            lock (Sync)
            {
                if (ReferenceEquals(_openIdLoginCts, linkedCts))
                    _openIdLoginCts = null;
            }

            linkedCts.Dispose();
        }
    }

    public async Task<bool> ApplyAuthHeadersAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        await EnsureValidAccessTokenAsync(cancellationToken);

        httpClient.DefaultRequestHeaders.Remove("Authorization");

        if (string.IsNullOrWhiteSpace(_accessToken))
            return false;

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        return true;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await EnsureValidAccessTokenAsync(cancellationToken);

        lock (Sync)
            return _accessToken;
    }

    public long? TryGetLestaAccountId()
    {
        string? accessToken;
        lock (Sync)
            accessToken = _accessToken;

        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        return TryGetJwtLongClaim(accessToken, "lesta_account_id");
    }

    public async Task Logout()
    {
        string? accessToken;
        lock (Sync)
        {
            accessToken = _accessToken;
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            try
            {
                await openIdAuthClient.LogoutAsync(accessToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Не удалось выполнить OpenID logout на сервере");
            }
        }

        ClearInMemory();
        await secretsStorage.Clear();
        logger.LogInformation("Signed out, secrets removed");
    }

    private async Task EnsureValidAccessTokenAsync(CancellationToken cancellationToken)
    {
        string? accessToken;
        string? refreshToken;
        lock (Sync)
        {
            accessToken = _accessToken;
            refreshToken = _refreshToken;
        }

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            return;

        if (!IsAccessTokenExpiringSoon(accessToken))
            return;

        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            lock (Sync)
            {
                accessToken = _accessToken;
                refreshToken = _refreshToken;
            }

            if (string.IsNullOrWhiteSpace(accessToken) ||
                string.IsNullOrWhiteSpace(refreshToken) ||
                !IsAccessTokenExpiringSoon(accessToken))
            {
                return;
            }

            var refreshed = await openIdAuthClient.RefreshAsync(refreshToken, cancellationToken);
            if (refreshed is null)
            {
                logger.LogWarning("Не удалось обновить OpenID токены");
                ClearInMemory();
                await secretsStorage.Clear();

                return;
            }

            lock (Sync)
            {
                _accessToken = refreshed.AccessToken;
                _refreshToken = refreshed.RefreshToken;
                _lestaExpiresAt = refreshed.LestaExpiresAt;
                _expiresAt = refreshed.ExpiresAt ?? TryGetJwtExpiry(refreshed.AccessToken);
            }

            await PersistAsync();
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private async Task PersistAsync()
    {
        AuthCredentialsSecret secret;
        lock (Sync)
        {
            secret = new AuthCredentialsSecret(_accessToken, _refreshToken, _lestaExpiresAt, _expiresAt);
        }

        await secretsStorage.Save(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(secret)));
    }

    private static void ClearInMemory()
    {
        lock (Sync)
        {
            _accessToken = null;
            _refreshToken = null;
            _lestaExpiresAt = null;
            _expiresAt = null;
        }
    }

    private static bool IsAccessTokenExpiringSoon(string accessToken)
    {
        DateTimeOffset? exp;
        lock (Sync)
            exp = _expiresAt;

        exp ??= TryGetJwtExpiry(accessToken);
        if (exp is null)
            return true;

        return exp.Value - DateTimeOffset.UtcNow <= TimeSpan.FromMinutes(2);
    }

    private static DateTimeOffset? TryGetJwtExpiry(string accessToken)
    {
        try
        {
            using var document = ParseJwtPayload(accessToken);
            if (document is null || !document.RootElement.TryGetProperty("exp", out var expElement))
                return null;

            return DateTimeOffset.FromUnixTimeSeconds(expElement.GetInt64());
        }
        catch
        {
            return null;
        }
    }

    private static long? TryGetJwtLongClaim(string accessToken, string claimName)
    {
        try
        {
            using var document = ParseJwtPayload(accessToken);
            if (document is null || !document.RootElement.TryGetProperty(claimName, out var claimElement))
                return null;

            return claimElement.ValueKind switch
            {
                JsonValueKind.Number when claimElement.TryGetInt64(out var number) => number,
                JsonValueKind.String when long.TryParse(
                    claimElement.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed) => parsed,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static JsonDocument? ParseJwtPayload(string accessToken)
    {
        var parts = accessToken.Split('.');
        if (parts.Length < 2)
            return null;

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2:
                payload += "==";
                break;
            case 3:
                payload += "=";
                break;
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        return JsonDocument.Parse(json);
    }

    private static async Task WriteCallbackResponseAsync(HttpListenerResponse response)
    {
        const string html =
            """
            <!DOCTYPE html>
            <html lang="ru">
            <head><meta charset="utf-8"><title>XVM Blitz</title></head>
            <body style="font-family: sans-serif; text-align: center; margin-top: 48px;">
              <h2>Авторизация выполнена</h2>
              <p>Можно закрыть это окно и вернуться в XVM Blitz.</p>
            </body>
            </html>
            """;

        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = 200;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.OutputStream.Close();
    }
}
