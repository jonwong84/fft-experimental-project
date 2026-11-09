namespace Zodiac.Framework;

/// <summary>Four independent boolean element sets; a default affinity value is neutral.</summary>
public readonly record struct MagicElementAffinities
{
    private readonly ElementToggles? absorb, nullify, half, weak;
    public ElementToggles Absorb { get => absorb ?? ElementToggles.None; init => absorb = value; }
    public ElementToggles Nullify { get => nullify ?? ElementToggles.None; init => nullify = value; }
    public ElementToggles Half { get => half ?? ElementToggles.None; init => half = value; }
    public ElementToggles Weak { get => weak ?? ElementToggles.None; init => weak = value; }

    public MagicElementAffinities(ElementToggles? Absorb = null, ElementToggles? Nullify = null,
        ElementToggles? Half = null, ElementToggles? Weak = null)
    {
        absorb = Absorb;
        nullify = Nullify;
        half = Half;
        weak = Weak;
    }

    public bool Equals(MagicElementAffinities other) => Absorb == other.Absorb && Nullify == other.Nullify && Half == other.Half && Weak == other.Weak;
    public override int GetHashCode() => HashCode.Combine(Absorb, Nullify, Half, Weak);
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasCustomElements => Absorb.HasCustomElements || Nullify.HasCustomElements || Half.HasCustomElements || Weak.HasCustomElements;
}
