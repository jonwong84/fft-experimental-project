# Synthetic battle-save migration fixtures

These fixtures contain only invented `sample:*` content. They are independently
specified projections of the fifth completed command in
[`SyntheticTwoUnitBattle`](../../../src/Zodiac.Content/Samples/SyntheticTwoUnitBattle.cs),
checked against the existing source-traced/synthetic scenario vectors. No bytes
come from a PS1 memory card, BIN, prepared original asset or serialized runtime graph.

At this checkpoint both units have HP 25, A is at `(0,1)` and B at `(1,1)`, both
CT values are zero, tick is 10 and RNG is `2937909499/14`. B owns the active turn
with action consumed and movement available. A has both choices consumed. The
scenario remains in progress.

- `two-unit-active-v1.json`: synthetic predecessor with implicit BIOS RNG algorithm.
- `two-unit-active-v2.json`: current schema with explicit algorithm ID.

Both have profile/content version 1 and a checksum over the compact snapshot DTO
field order. They were written independently of the C# save encoder; tests load
them and compare every runtime projection against the fifth-command checkpoint,
then verify the current-schema export/reload. All thirteen command boundaries
also have continuation/event regressions using freshly composed definitions.

See the [save contract](../../development/two-unit-battle-saves.md) for checksum,
migration, validation and atomic-write policy. The read-only reference checkout
HEAD on 2026-10-23 was `11464943bbdbd2a0af022d90cb7f10455cf911df`; no new C behavior
or original save format is translated by these fixtures.
