using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class ElementTogglesTests
{
    [Fact]
    public void All_original_element_combinations_round_trip_and_preserve_every_source_intersection()
    {
        var sets = Enumerable.Range(0, 256).Select(value => OriginalElementEncoding.FromByte((byte)value)).ToArray();
        for (int first = 0; first < 256; first++)
        {
            Assert.Equal(first, OriginalElementEncoding.ToByte(sets[first]));
            Assert.Equal(first == 0, sets[first].Neutral);
            for (int second = 0; second < 256; second++)
                Assert.Equal((first & second) != 0, sets[first].Overlaps(sets[second]));
        }
    }

    [Fact]
    public void Immutable_neutral_defaults_keep_affinity_and_current_unit_value_equality()
    {
        var fire = ElementToggles.None with { Fire = true };
        Assert.True(ElementToggles.None.Neutral);
        Assert.False(fire.Neutral);
        var empty = new MagicElementAffinities(new(), new(), new(), new());
        Assert.Equal(default, empty);
        Assert.Equal(default(MagicElementAffinities).GetHashCode(), empty.GetHashCode());
        var weak = empty with { Weak = fire };
        Assert.True(empty.Weak.Neutral);
        Assert.True(weak.Weak.Fire);
        var unit = new MagicCurrentUnit(new("test:unit"), 5, 70, new(MagicZodiacSign.Aries), new(), new());
        Assert.True(unit.Strengthens.Neutral);
        Assert.Equal(unit, unit with { Strengthens = new ElementToggles(), Affinities = empty });
    }

    [Theory]
    [InlineData("Gravity")]
    [InlineData("Poison")]
    [InlineData("Restore")]
    public void Custom_flags_are_representable_but_not_truncated_or_enabled_in_original_rules(string name)
    {
        var custom = new ElementToggles { Gravity = name == "Gravity", Poison = name == "Poison", Restore = name == "Restore" };
        Assert.True(custom.HasCustomElements);
        Assert.False(custom.Neutral);
        Assert.True(custom.Overlaps(custom));
        Assert.Throws<NotSupportedException>(() => OriginalElementEncoding.ToByte(custom));
        Assert.Throws<NotSupportedException>(() => OriginalElementEncoding.ToNames(custom));
        var context = new MagicDamageElementContext(custom, MagicDamageWeather.None, new(), new(true, true, true));
        Assert.Throws<NotSupportedException>(() => MagicDamageElements.Transform(5, context));
        Assert.Throws<NotSupportedException>(() => MagicDamageElements.Transform(5, context with
        {
            Elements = new() { Fire = true }, TargetAffinities = new(Weak: custom)
        }));
        var caster = new MagicCurrentUnit(new("test:caster"), 5, 70, new(MagicZodiacSign.Aries), new(), new());
        var target = caster with { UnitId = new("test:target") };
        var spell = new FaithMagicSpell(FaithMagicAmountKind.Healing, 14, custom, false);
        Assert.False(FaithMagicInputs.Project(caster, target, spell, MagicDamageWeather.None).Accepted);
        Assert.False(FaithMagicInputs.Project(caster with { Strengthens = custom }, target,
            spell with { Elements = ElementToggles.None }, MagicDamageWeather.None).Accepted);
    }
}
