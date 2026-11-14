namespace Zodiac.Framework;

/// <summary>Opaque raw amount work. A null amount records element cancellation before the Faith step.</summary>
public sealed record FaithMagicElementAmount
{
    public FaithMagicAmountKind Kind { get; }
    public short ModifiedXa { get; }
    public short StoredProduct { get; }
    public MagicDamageElementOutcome? Elements { get; }
    public FaithMagicAmount? Amount { get; }
    public bool Nullified => Elements?.Nullified == true;
    internal FaithMagicElementAmount(FaithMagicAmountKind kind, short xa, short product,
        MagicDamageElementOutcome? elements, FaithMagicAmount? amount)
    { Kind=kind; ModifiedXa=xa; StoredProduct=product; Elements=elements; Amount=amount; }
}
