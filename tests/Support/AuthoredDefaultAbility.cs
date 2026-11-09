using Zodiac.Content.BattleSetup;
using Zodiac.Framework;

namespace TestSupport;

// Invented setup; deliberately not exported original assets or a reference-edition claim.
internal static class AuthoredDefaultAbility
{
    internal const string Fire = "test:spell/fire";
    internal const string Wizard = "test:job/wizard";
    internal static DefaultAbilityPackageDocument Document()
    {
        string[] names = ["squire", "chemist", "knight", "archer", "monk", "priest", "wizard",
            "time", "summoner", "thief", "mediator", "oracle", "geomancer", "lancer", "samurai", "ninja", "calculator", "bard", "dancer", "mime"];
        string Id(string name) => "test:job/" + name;
        Dictionary<string, JobPointBalance> Career() => names.ToDictionary(Id, _ => new JobPointBalance(0, 0, 0));
        var career = Career(); career[Wizard] = new(50, 100, 1);
        uint Raw(int n) => checked((uint)(n * 16384));
        DefaultAbilityPreparedUnit Unit(string id, int order, int x, SpellTargetTeam team, int hp, byte speed, byte ma, string[] spells) =>
            new(id, id, order, x, 2, team, Wizard, 10, 95, 70, 70, MagicZodiacSign.Aries, true, false,
                new(Raw(hp), Raw(75), Raw(speed), Raw(6), Raw(ma)), new(6, ma, speed), (ushort)hp, 75, new(career), spells);
        return new("fft.ordinary-magic-battle", 1, "ordinary-human-bare-v1", "authored-ordinary", 57, 7, 5, 10,
            Id("squire"), Id("bard"), Id("dancer"), [100, 200, 350, 550, 800, 1150, 1550, 2100],
            [new(Fire, "Fire", 16, new(FaithMagicAmountKind.Damage, 14, new ElementToggles { Fire = true }, true), 6, 4, true, new(4, 1, 1))],
            [new(Wizard, 80, new(12, 9, 100, 60, 50), new(100, 100, 100, 100, 100), 3, 3, "test:skillset/magic")],
            names.Select(n => new DefaultAbilityPreparedUnlock(Id(n), [])).ToArray(),
            [Unit("mage", 0, 0, SpellTargetTeam.Blue, 100, 10, 8, [Fire]), Unit("guard", 1, 4, SpellTargetTeam.Red, 200, 8, 5, [])]);
    }
}
