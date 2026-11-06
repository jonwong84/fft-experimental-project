using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

internal static class OrdinaryMagicBattleTestData
{
    internal static readonly BattleUnitId Mage = new("mage");
    internal static readonly BattleUnitId Target = new("target");
    internal static readonly AssetId Fire = new("fft.ps1:ability/source-0016");

    internal static OrdinaryMagicBattleDefinition Definition(byte experience = 0, ushort targetHp = 200,
        byte charge = 4, bool enemySpell = false, byte enemySpeed = 8, RandomState? rng = null)
    {
        using var f = GrowthUnlockTestData.Fixture();
        AssetId Job(string id) => new("fft.ps1:job/" + id);
        var jobs = ImmutableArray.Create(new JobUnlockDefinition(Job("squire"), [])).AddRange(
            f.RootElement.GetProperty("unlockDefinitions").EnumerateArray().Select(j => new JobUnlockDefinition(Job(j.GetProperty("job").GetString()!),
                j.GetProperty("requires").EnumerateObject().Select(p => new JobLevelRequirement(Job(p.Name), p.Value.GetByte())).ToImmutableArray())));
        var catalog = new JobUnlockCatalog(jobs, Job("squire"), Job("bard"), Job("dancer"));
        var points = jobs.ToImmutableDictionary(j => j.JobId, _ => new JobPointBalance(0, 0, 0));
        var wizardPoints = points.SetItem(Job("chemist"), new(200, 200, 2)).SetItem(Job("wizard"), new(50, 100, 1));
        uint Raw(int stat, byte multiplier) => (uint)(((long)stat * 1638400 + multiplier - 1) / multiplier);
        var mage = new OrdinaryMagicUnitDefinition(new(Mage, 0, new(0, 2, 0), SpellTargetTeam.Blue, SpellTargetTeam.Blue),
            new(Mage, 8, 70, new(MagicZodiacSign.Aries, IsMale: true), new(), new(), Equipment: OrdinaryMagicBattleDefinition.BareEquipment, Mp: 75),
            new(100, 100, 75, 70), new(Raw(100, 75), Raw(75, 120), Raw(10, 100), Raw(6, 60), Raw(8, 150)),
            new(12, 9, 100, 60, 50), new(75, 120, 100, 60, 150), new(6, 8, 10), 10, experience, 80, Job("wizard"), wizardPoints, [Fire]);
        var target = new OrdinaryMagicUnitDefinition(new(Target, 1, new(4, 2, 0), SpellTargetTeam.Red, SpellTargetTeam.Red),
            new(Target, 5, 70, new(MagicZodiacSign.Aries, IsMale: true), new(), new(), Equipment: OrdinaryMagicBattleDefinition.BareEquipment, Mp: 50),
            new(targetHp, 200, 50, 70, targetHp <= 40), new(Raw(200, 100), Raw(50, 75), Raw(enemySpeed, 100), Raw(8, 90), Raw(5, 80)),
            new(11, 15, 100, 60, 50), new(100, 75, 100, 90, 80), new(8, 5, enemySpeed), 10, 0, 74, Job("squire"),
            points.SetItem(Job("squire"), new(100, 100, 1)), enemySpell ? [Fire] : []);
        var spell = new MagicCastDefinition(Fire, 16, new(FaithMagicAmountKind.Damage, 14, MagicElement.Fire, true),
            6, charge, true, Targeting: new(4, 1, 1));
        return new(new(7, 5, 1, Enumerable.Repeat(new BattleTile(10), 35).ToArray()), [mage, target], [spell], catalog,
            JobPointArithmeticTests.Thresholds, rng ?? new(57, 9));
    }

    internal static OrdinaryMagicBattleState Apply(OrdinaryMagicBattleState state, OrdinaryMagicCommand command)
    {
        var r = OrdinaryMagicBattle.Apply(state, command);
        Xunit.Assert.True(r.Accepted, r.Rejection);
        return r.State;
    }

    internal static OrdinaryMagicBattleState Queued(OrdinaryMagicBattleDefinition definition, bool follow = true,
        TilePosition? aim = null)
    {
        var state = Apply(definition.Start(), new OrdinaryMagicCommand.Advance());
        state = Apply(state, new OrdinaryMagicCommand.Cast(Mage, Fire, aim ?? new(4, 2, 0), follow));
        return Apply(state, new OrdinaryMagicCommand.Wait(Mage));
    }

    internal static OrdinaryMagicBattleState BeforeResolution(OrdinaryMagicBattleDefinition definition,
        bool follow = true, TilePosition? aim = null)
    {
        var state = Apply(Queued(definition, follow, aim), new OrdinaryMagicCommand.Advance());
        return Apply(state, new OrdinaryMagicCommand.Wait(Target));
    }
}
