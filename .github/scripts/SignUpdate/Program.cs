using System.Security.Cryptography;
using System.Text.Json;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: SignUpdate <exe-path> <private-key-pem-path>");
    return 1;
}

var exePath = Path.GetFullPath(args[0]);
var privateKeyPath = Path.GetFullPath(args[1]);

if (!File.Exists(exePath))
{
    Console.Error.WriteLine($"EXE not found: {exePath}");
    return 1;
}

if (!File.Exists(privateKeyPath))
{
    Console.Error.WriteLine($"Private key not found: {privateKeyPath}");
    return 1;
}

var privatePem = await File.ReadAllTextAsync(privateKeyPath);
using var rsa = RSA.Create();
rsa.ImportFromPem(privatePem);

await using var stream = File.OpenRead(exePath);
var hash = await SHA256.HashDataAsync(stream);
stream.Position = 0;
var signature = rsa.SignData(stream, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

var manifestPath = exePath + ".sig.json";
var manifest = new Dictionary<string, string>
{
    ["sha256"] = Convert.ToHexString(hash).ToLowerInvariant(),
    ["signature"] = Convert.ToBase64String(signature)
};

await File.WriteAllTextAsync(
    manifestPath,
    JsonSerializer.Serialize(manifest));

Console.WriteLine($"sha256={manifest["sha256"]}");
Console.WriteLine($"manifest={manifestPath}");
return 0;
