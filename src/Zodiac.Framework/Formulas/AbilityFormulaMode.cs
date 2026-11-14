namespace Zodiac.Framework.Formulas;

/// <summary>
/// Tells the calculation what it's crunching the damage/healing value for.
/// Execution means that the value is for the execution of an action or ability.
/// Preview means that the value will be displayed in the game's targeting window before the action is executed.
/// Ai means that the value is being used for evaluation by the AI decision engine.
///
/// This matters because Execution can run RNG to produce critical hits and a chance for knockback.
/// Preview and Ai don't consume formula RNG, and calculate a fixed amount, even when the formula
/// uses a random power value, like Dash and Throw Stone. Evasion and proc checks have separate rules.
/// </summary>
public enum AbilityFormulaMode
{
    Execution,
    Preview,
    Ai
}
