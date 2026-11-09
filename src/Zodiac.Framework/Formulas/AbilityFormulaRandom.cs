using Zodiac.Framework.Random;

namespace Zodiac.Framework.Formulas;

/// <summary>Private work for one formula evaluation; never owns evasion or decision-search draws.</summary>
internal sealed class AbilityFormulaRandom(AbilityFormulaMode mode, RandomState entry)
{
    public RandomState State { get; private set; } = entry;

    public uint Next()
    {
        var draw = new RandomStateGenerator().Next(State);
        State = draw.State;
        return draw.Value;
    }

    public uint FormulaRoll()
    {
        if (mode == AbilityFormulaMode.Execution)
        {
            return Next();
        }
        return 16384;
    }
}
