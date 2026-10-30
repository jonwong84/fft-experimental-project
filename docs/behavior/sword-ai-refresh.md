# Ordinary AI refresh query — ZOD-159

Implemented 2026-10-28 against read-only clean fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base
`9ab9176cd2fdf622babaf5d7194e7f9fe6cb1602`. The
[ZOD-156 contract](../architecture/zod-156-ai-refresh-prefix.md)
and [source receipt](../evidence/ai/refresh-contract-v1.json) retain the traced
callers, arithmetic, save/restore behavior and source fingerprints.

`SwordAiRefresh.Evaluate` in `Zodiac.Framework.Ai` admits two living stable
ordinary opponents with an active Move-used/ACT-unused actor. Existing candidate
and score certificates provide the closed geometry, slot order, team and
effect-free clock context. A false-default refresh profile additionally requires
fresh refresh entry, both units targetable and zero cached MP-ability modifiers.
Exactly two immutable policy rows supply known ConserveCt and
TeamSupportUnavailable booleans by stable ID. Missing/duplicate/unknown rows reject.

The query constructs fresh private units at their existing positions, applies
the unused-ACT CT refund/cap, and invokes the private simulation clock once. The
source act-first/skillset-zero branch skips movement/pickup and action simulation;
there is no movement search, formula, scoring or placeholder candidate. One
upkeep draw survives the hypothetical simulation. Already-ready peers are checked
before adding CT; original TieKey breaks equal readiness, independent of input order.

Accepted outputs contain diagnostic clock units, first movement opportunity,
upkeep raw value, temporary end CT/ticks and progressed hypothetical RNG. Named
boolean flags derive enemy/Considered and the strict HP thresholds while retaining
the supplied policies. Living, targetable, zero-MP-cache admission establishes
false dead/Reraise, transient Dead/Petrified/Jump, untargetable and MP-constrained
facts. Unrelated source decision flags are outside the output. HP below half
preserves division first: HP24/max49 is false. The strict three-quarter comparison
uses cross products in the supported UInt16 HP domain.

Live battle/turn RNG, CT, choices, HP/status, facing, position and ticks remain
unchanged. Every rejected result returns the input decision RNG, a typed reason
and no partial outcome. Tick/draw exhaustion is checked only when work occurs;
an already-ready peer needs no tick, even at UInt64 MAX.

Eighteen focused managed cases exercise all six source clock/counter vectors,
odd HP and equality boundaries, retained policies and derived flags, either actor,
collection-order/replay isolation, carried RNG and admission failures. These
execute the new C# query; the original Python vectors remain offline descriptions.

This query grants no command/session/receipt authority and has no score or rank.
Existing selection/execution APIs and GeneralSelectionEntryContext remain intact.
The earlier actor-delay cache, later ability/team/target policy preparation and
fresh refresh-to-selection composition require further source study (ZOD-160).
Broader search, continuation, frame-interleaved BIOS RNG, original observations
and playable-host/editor activation remain outside scope.

Validation: locked restore, zero-warning/error Release build and **3,863 .NET /
504 Python tests** pass, including the owner's uncommitted content drafts; those
files are excluded from this milestone's commit. Full .NET results are under
ignored `scratch/ai-refresh-159/full-trx/`; Python, build and clean pinned source
audit logs are in `scratch/ai-refresh-159/`. No C/original execution or UI review.
