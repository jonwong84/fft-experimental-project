# Bounded multi-unit roster AI replay — ZOD-316

Source/managed base `720966b`; read-only reference
`11464943bbdbd2a0af022d90cb7f10455cf911df`, clean. The
[independent fixture](../fixtures/behavior/roster-replay-contract-v1.json) composes
three six-turn scenarios using original ordinary CT, selection, formula, push
and choice-refund descriptions. [Source receipt](../evidence/ai/roster-replay-source-v1.json).

Public AddCt/Advance creates every managed session. The complete-turn driver
stops at PickUnit; the test explicitly advances the real clock between turns.
Already-ready peers are selected at the same tick, highest CT then first source
order. The selected unit takes CT modulo100, resets choices and consumes its
real upkeep draw. Only when no peer is ready does Speed advance CT, capped254.
No hypothetical state or detached pair becomes authority.

The scenarios cover mixed Attack/Accumulate/Stone, retained Attack/Stone/END,
ACT-first reselection, PA changes, real damage/pushes and all-ally empty ranking.
Priority knowledge follows actual accepted writes between different actors;
every turn compares all unit positions, HP, PA, CT, choices, facing, KO, RNG,
tick and priority cells. Clock events compare literal tick count, actor,
pre/post CT, upkeep roll and full RNG checkpoint. Reversed collection storage
produces the same states, command decisions and events. Original inputs remain
unchanged, and the sixth turn does not advance a seventh clock.

Source helper HP admission was corrected from the old fixture-only50 cap to the
original unsigned16-bit HP domain, independently corroborated by unit.h's hp/
max_hp fields and existing sword rules. The new 500HP scenarios exercise this
without changing production bounds. Existing source descriptions remain equal.

Three managed scenario cases verify eighteen actual turns; two Python cases
verify source composition, unchanged inputs and tampered-clock rejection.
This is a deterministic bounded proof, not an observed original encounter or
terminal policy. Fresh corpse-domain stops remain explicit, automatic facing,
full corpse/terminal/frame RNG, host/editor, broad interfaces and ZOD-106 deferred.

The next selected ability is [Dash](../architecture/zod-317-dash.md) under ZOD-038,
reusing the established authority/clock/rank boundaries.
