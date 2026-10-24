using Xunit;

namespace Fft.Core.Tests;

public sealed class PhysicalWeaponEvasionTests
{
    // formula_evade_store_physical; decomp 611b504d25d4a9b03fab8023d7dbea75fe381d5d.
    // Synthetic values exercise reaction and action-restriction gates independently.
    [Theory]
    [InlineData(9, false, true, 0)]
    [InlineData(9, true, false, 0)]
    [InlineData(9, false, false, 0)]
    [InlineData(9, true, true, 9)]
    [InlineData(0, true, true, 0)]
    [InlineData(255, true, true, 255)]
    public void Raw_weapon_evade_requires_Weapon_Guard_and_no_action_restriction(int raw, bool guard, bool unrestricted, int expected) =>
        Assert.Equal(expected, PhysicalWeaponEvasion.GetContribution(raw, guard, unrestricted));

    [Theory]
    [InlineData(-1)] [InlineData(256)]
    public void Values_outside_source_byte_width_are_rejected(int raw) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalWeaponEvasion.GetContribution(raw, false, false));
}
