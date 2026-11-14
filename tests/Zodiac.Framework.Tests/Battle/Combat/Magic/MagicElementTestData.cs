using System.Text.Json;

namespace Zodiac.Framework.Tests;

internal static class MagicElementTestData
{
    internal static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","magic-elements-contract-v1.json")));
    internal static MagicDamageElementContext Context(JsonElement d) => new(OriginalElementEncoding.FromByte(d.GetProperty("elements").GetByte()),
        (MagicDamageWeather)d.GetProperty("weather").GetInt32(),new(OriginalElementEncoding.FromByte(d.GetProperty("absorb").GetByte()),
            OriginalElementEncoding.FromByte(d.GetProperty("nullify").GetByte()),OriginalElementEncoding.FromByte(d.GetProperty("half").GetByte()),OriginalElementEncoding.FromByte(d.GetProperty("weak").GetByte())),new(true,true,true));
    private static bool Flag(JsonElement d,string name) => d.TryGetProperty(name,out var value) && value.GetBoolean();
    internal static FaithMagicAmountInput Input(JsonElement d)
    {
        var m=d.GetProperty("modifiers");
        return new(Enum.Parse<FaithMagicAmountKind>(d.GetProperty("kind").GetString()!),d.GetProperty("magicAttack").GetByte(),d.GetProperty("power").GetByte(),
            FaithMagicArithmeticTests.Faith(d.GetProperty("caster")),FaithMagicArithmeticTests.Faith(d.GetProperty("target")),
            new(Flag(m,"strengthensElement"),Flag(m,"magicAttackUp"),Flag(m,"magicDefenseUp"),Flag(m,"targetShell"),Flag(m,"targetChickenOrFrog"),
                m.TryGetProperty("compatibility",out var c) ? Enum.Parse<MagicCompatibility>(c.GetString()!) : MagicCompatibility.Neutral));
    }
}
