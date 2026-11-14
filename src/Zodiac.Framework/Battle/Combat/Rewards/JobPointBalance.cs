namespace Zodiac.Framework;

/// <summary>Numeric job-slot projection; unlock recomputation and career storage are still required.</summary>
public sealed record JobPointBalance(ushort Current, ushort Total, int JobLevel);
