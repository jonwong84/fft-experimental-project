# Supplied roster Throw Stone candidate — ZOD-298

Implemented 2026-11-05 from managed base `d6e72e1`, following
[ZOD-297](../architecture/zod-297-roster-stone-search.md). Reference checkout
remains read-only and clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/roster-stone-source-v1.json) pins 66 functions
across 21 files. No C or original program was run.

`RosterThrowStoneAiCandidate.Evaluate` accepts one supplied `ThrowStoneAiCandidate`,
the existing typed `TargetedAbilityBattleProfile`, complete living roster
simulation and explicit normal human/unchanged-equipment/zero-Golem facts.
It requires a clear direct trajectory certificate for this candidate's actual
ACT-first or Move-first origin, including terrain, actual sprite heights and
every other occupant. It does not compute collision; false rejects before draws.
The public pair command and formula entry retain their two-unit guards.

The typed effect must exist and the actor must actually hold its grant. Profile
ground and all unit grant bindings must match the whole roster; an AI ability
entry never grants an action. Every unit retains the old PA1..99 envelope and
zero equipment PA bonus. Candidate admission retains compact16, active UNIT_TURN,
unused ACT, enemy target, supported flat ground and initial legal movement
endpoints. Move uses the existing cap124 geometry, not general-prefix admission.

Both formula entries share the same private admitted Stone kernel. It consumes
four evasion draws, uses fixed midpoint damage2*PA with no damage draw, and never
executes Brave/knockback work. Positive accuracy applies full normal HP damage;
zero accuracy is NotValuable with no clock or score. Front includes either axis
on an exact diagonal. Range is1..4 from the order-specific formula origin.

The existing explicit-target state builder changes only the named target's HP,
status and death counter. Actor placement and choice/CT settlement, complete
source-order clock and all-team scoring reuse existing owners. Every peer keeps
its own HP, facing, PA and original choices; projected KO cannot change initial
movement endpoints. Supported RNG is returned separately from immutable live
state. Formula, upkeep or tick exhaustion restores the entire supplied RNG and
publishes neither partial projection nor score.

Fourteen independent [source tapes](../fixtures/behavior/roster-stone-contract-v1.json)
cover both orders, PA1/99, range4/diagonal evasion, partial/zero accuracy,
peer HP, all-team score signs, selected KO, ready ties and repeated corpse picks.
The seven managed cases additionally check exact grants/membership, real pair
parity and unchanged command rejection, movement/range/turn boundaries, both
actors, reordered storage, isolation/replay, compact16 versus uncapped profile
storage and exact/late RNG/tick exhaustion. Three Python cases check the source
descriptions and ranked-search control-flow tapes independently of managed code.

Validation: locked restore, clean Release build, **4,378 .NET cases** (1,150 AI,
1,483 Framework), **566 Python tests**, pinned source/prepared data, guide,
coverage, links and unchanged owner-draft checks. Logs/receipts are under ignored
`scratch/roster-stone-297-298/`; [implementation receipt](../evidence/ai/roster-stone-implementation-v1.json).

ZOD-299 next resolves computed trajectory admission and missing sprite/SDK
inputs before selecting isolated fresh ranked Stone search. No public cached
outcome, search/list/lottery, live command authority or full original equivalence
is claimed. Other abilities/policies/statuses/maps, complete corpse lifecycle,
automatic facing, terminal/frame RNG, host/editor, owner drafts, replacement
interfaces and ZOD-106 retain their deferrals.
