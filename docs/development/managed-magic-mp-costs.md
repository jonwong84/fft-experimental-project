# Managed magic MP costs

The owner requested `int` MP costs on 2026-11-05. `MagicCastDefinition.MpCost`,
`MagicMpAbility.BaseCost`, both resource/cast `RecordedCost` fields and the local
cost calculation now use nonnegative signed32 integers. This agrees with the
existing managed immediate/targeted content definitions and permits costs above255
without narrowing them between definition, preparation and result.

Both public query entry points reject negative costs. Half MP still floors by
halving the nonnegative cost; recorded cost is retained on insufficient-MP failure.
The affordability comparison precedes subtraction, so even `int.MaxValue` does not
wrap into a payable value. Preview retains MP; execution/AI propose one subtraction
per prepared cast across all its targets. No resource or RNG state is published.

This changes the managed representation, not original file packing. Clean source
`11464943bbdbd2a0af022d90cb7f10455cf911df` was rechecked: the secondary ability
record, action-work cost and `mp_check_and_consume` local cost are byte fields.
The existing [resource source receipt](../evidence/combat/magic-resource-source-v1.json)
pins those definitions/functions. Original data readers retain their byte fields;
imported values promote losslessly into managed integers. Costs above255 are
managed-content capability, not a claim that retail records contained such values.
Original byte-domain behavior fixtures and their independent Python descriptions
remain unchanged.

Current unit MP remains unsigned16, with the source-compatible signed16 read for
affordability. This change widens cost alone. Charge bytes, ability IDs, formula
arithmetic, geometry, owner drafts/JobStats and deferred activation work are
unchanged. ZOD-364 remains next; this owner-requested adjustment does not close it.

[Managed tests](../../tests/Zodiac.Framework.Tests/Battle/Combat/Magic/MagicMpCostTests.cs)
cover256/513 costs, Half MP flooring, Execute/AI/Preview multi-target casts,
`int.MaxValue` failure and negative admission. Existing fixture readers now use
`GetInt32()` for cost fields. The [validation receipt](../evidence/combat/managed-mp-costs-v1.json)
records checks and ignored report locations.

Validation: locked restore, Release build with zero warnings/errors, all **4,670
.NET tests** and **695 Python tests** passed. The focused magic suite passes108
cases. Prior source/prepared-data checks, coverage, local links and preservation
checks pass. Reports are under ignored `scratch/managed-mp-costs/`. No original/C
execution or UI review was performed.
