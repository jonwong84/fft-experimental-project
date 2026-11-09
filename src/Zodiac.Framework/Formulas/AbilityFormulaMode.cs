namespace Zodiac.Framework.Formulas;

/// <summary>
/// Tells the calculation what it's crunching the damage/healing value for.
/// Execution means that the value is for the execution of an action or ability.
/// Preview means that the value will be displayed in the game's targeting window before the action is executed.
/// Ai means that the value is being used for evaluation by the AI decision engine.
/// </summary>
public enum AbilityFormulaMode
{
    Execution,
    Preview,
    Ai
}
