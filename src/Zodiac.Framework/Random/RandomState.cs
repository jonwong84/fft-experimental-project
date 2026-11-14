using Tessera.Engine;

namespace Zodiac.Framework.Random;

/// <summary>The full unsigned BIOS RNG word and the number of outputs consumed since seeding.</summary>
public readonly record struct RandomState(uint Value, ulong DrawCount);
