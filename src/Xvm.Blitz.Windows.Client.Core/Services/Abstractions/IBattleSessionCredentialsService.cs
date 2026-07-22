namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface IBattleSessionCredentialsService
{
    Task SaveSecretKey(string secretKey);

    Task<string?> LoadSecretKey();

    Task Clear();
}
