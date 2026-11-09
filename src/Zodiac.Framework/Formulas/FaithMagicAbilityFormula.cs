using Zodiac.Framework.Random;

namespace Zodiac.Framework.Formulas;

/// <summary>Shared0x08/0x0C amount and HP-channel dispatch; existing owners retain signed stores and modifier ordering.</summary>
internal static class FaithMagicAbilityFormula
{
    public static AbilityFormulaResult Evaluate(FaithMagicAmountKind kind, AbilityFormulaParameters parameters,
        AbilityFormulaContext context, RandomState rng)
    {
        var magic = context.Magic!;
        var input = new FaithMagicAmountInput(kind, magic.MagicalAttack, (byte)parameters.Y,
            magic.AttackerFaith, magic.TargetFaith, magic.Modifiers);
        var projection = FaithMagicAmountFormula.ProjectWithElementsAfterHit(input, magic.Elements);
        var hp = FaithMagicHpRouting.Route(projection, magic.TargetIsUndead);
        return new(hp.HpDamage, false, false, rng)
        {
            Healing = hp.HpHealing,
            MagicProjection = projection,
            MagicHp = hp
        };
    }
}
