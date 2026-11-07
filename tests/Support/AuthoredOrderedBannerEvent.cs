using System.Buffers.Binary;
using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Content.Events;
using Zodiac.Framework;

namespace Fft.Tests.Support;

/// <summary>Independently authored zero/display/barrier/add/exit program, never a retail event slice.</summary>
internal static class AuthoredOrderedBannerEvent
{
    internal static readonly AssetId Id = new("sample:event/banner-admission-v1");
    internal static readonly AssetId Artwork = new("sample:banner/test");
    internal static readonly AssetId Owner = new("sample:owner/banner");
    internal static byte[] Code(int hold = 60) => [0xbe, 3, 0, 0x78, 8, (byte)hold, 0xe5, 56, 0, 0xb0, 3, 0, 7, 0, 0xd4, 1, 0xdb];
    internal static byte[] Block(byte[]? code = null, bool processed = false)
    {
        code ??= Code();
        var result = new byte[8192];
        BinaryPrimitives.WriteUInt32LittleEndian(result, processed ? 0xf2f2f2f2 : (uint)(4 + code.Length));
        code.CopyTo(result, 4);
        result[4 + code.Length] = 0xfe;
        return result;
    }
    internal static PreparedOrderedBannerEvent Definition(int hold = 60) => new(Id, Artwork, Owner,
        new("synthetic", "SCUS-94221", "11464943bbdbd2a0af022d90cb7f10455cf911df",
            Convert.ToHexStringLower(SHA256.HashData(Block(Code(hold)))), "raw", 21),
        [new(4, OrderedScenarioOperation.ZeroVariable, 3), new(7, OrderedScenarioOperation.DisplayConditions, 8, hold),
         new(10, OrderedScenarioOperation.WaitForTaskClass, 56), new(13, OrderedScenarioOperation.AddImmediate, 3, 7),
         new(18, OrderedScenarioOperation.ForwardTargetExit, 1), new(20, OrderedScenarioOperation.SearchEnd)]);
    internal static void Write(string root, PreparedOrderedBannerEvent? definition = null)
    {
        definition ??= Definition();
        Directory.CreateDirectory(root);
        byte[] bytes = definition.ToUtf8();
        File.WriteAllBytes(Path.Combine(root, "event.json"), bytes);
        new PreparedAssetManifest([new(definition.Id, "event.json", bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)))])
            .SaveToFile(Path.Combine(root, PreparedAssetManifest.FileName));
    }
}
