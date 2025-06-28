namespace Xvm.Blitz.Windows.Client.Core.Services.Abstractions;

public interface ISecretsStorageService
{
    Task Save(byte[] data);

    Task<byte[]?> Load();

    Task Clear();
}