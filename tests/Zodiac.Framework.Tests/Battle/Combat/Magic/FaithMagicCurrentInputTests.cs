using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

internal static class MagicCurrentTestData
{
    internal static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "magic-current-input-contract-v1.json")));
    internal static MagicZodiacIdentity Identity(JsonElement d) => new((MagicZodiacSign)d.GetProperty("sign").GetInt32(),
        d.GetProperty("male").GetBoolean(), d.GetProperty("female").GetBoolean(), d.GetProperty("monster").GetBoolean());
    internal static bool Flag(JsonElement d, string name) => d.TryGetProperty(name, out var v) && v.GetBoolean();
}

[Trait("Ruleset", "OriginalFft")]
public sealed class FaithMagicCurrentInputTests
{
    [Fact]
    public void Thirty_three_literal_compatibilities_preserve_sign_table_and_exact_identity_flags()
    {
        using var doc = MagicCurrentTestData.Fixture();
        foreach (var r in doc.RootElement.GetProperty("compatibility").EnumerateArray())
            Assert.Equal(Enum.Parse<MagicCompatibility>(r.GetProperty("expected").GetString()!),
                FaithMagicInputs.ClassifyCompatibility(MagicCurrentTestData.Identity(r.GetProperty("caster")), MagicCurrentTestData.Identity(r.GetProperty("target"))));
    }

    [Theory] [InlineData(13)] [InlineData(255)]
    public void Unknown_signs_reject_without_indexing_the_source_table(int sign)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FaithMagicInputs.ClassifyCompatibility(new((MagicZodiacSign)sign), new(MagicZodiacSign.Aries)));
        Assert.Throws<ArgumentOutOfRangeException>(() => FaithMagicInputs.ClassifyCompatibility(new(MagicZodiacSign.Serpentarius), new((MagicZodiacSign)sign)));
    }
}
