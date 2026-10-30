# Synthetic save migration fixtures

The separate `synthetic-world-idle-v1.json` and `synthetic-world-idle-v2.json`
fixtures also contain invented data only. They independently describe the
ZOD-068 Harbor checkpoint: Ada/Borin, courier job, one equipped training blade,
two carried blades, five rations, funds 100, month/day 3/14, progress true and
elapsed seconds 42. The next authored trip reaches Hill with funds 93 and elapsed
seconds 53. Schema 1 implicitly supports only idle-world/no-RNG; schema 2 names
those capabilities. Checksums use compact DTO field order. No original card,
campaign, script, BIN or prepared asset data appears in these fixtures. See the
[world checkpoint proof](../../development/synthetic-world-checkpoint.md).

The battle fixtures contain only invented `sample:*` content. They are independently
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
