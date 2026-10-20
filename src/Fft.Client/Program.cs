using Fft.Content;
using Fft.Core;
using System.Text.Json;

if (args.Length == 0)
{
    Console.WriteLine("FFT Native C# reimplementation bootstrap.");
    return 0;
}

if (args.Length != 4 || args[0] != "--content-root" || args[2] != "--asset-id")
{
    Console.Error.WriteLine("Usage: Fft.Client [--content-root <directory> --asset-id <namespace:path>]");
    return 2;
}

try
{
    var assetId = new AssetId(args[3]);
    LooseAssetStore content = LooseAssetStore.Load(args[1]);
    byte[] asset = content.ReadAllBytes(assetId);
    Console.WriteLine($"Loaded prepared asset '{assetId}' ({asset.Length} bytes; manifest size and SHA-256 verified).");
    return 0;
}
catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or UnauthorizedAccessException or KeyNotFoundException)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
