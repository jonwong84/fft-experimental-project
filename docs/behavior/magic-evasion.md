# Magic evasion and formula entry — ZOD-342, ZOD-344–347

Source study 2026-11-05 from managed base
`c1fcf1ad11d85d753eb31cf7603371f9c24221b6`; read-only fft_decomp clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Receipt](../evidence/combat/magic-evasion-source-v1.json): 13 files/21 functions.
[47 independent descriptions](../fixtures/behavior/magic-evasion-contract-v1.json)
cover eight equipment loads, 15 preparations and 24 ordered roll tapes. No
original, C or managed code is executed by the Python description checker.

## ZOD-342

Source trace and selected implementation children.

`action_calculate_target_result` selects the formula and runs
`evasion_reactions_check` before dispatch. Reflect, pending reactions, Blade Grasp
and Arrow Guard can change that entry. They require separate tracing/implementation
before admitting those contexts. A future bounded entry must positively certify
fresh hit/accuracy100 result work and no earlier reaction/reflection effects;
ordinary primary/no-pending and finalizer scope facts remain necessary.

Formula0x08 calls `formula_evade_calculate_magical` before MA/Y or any amount work.
Formula0x0C bypasses this entire chain. Magical evasion loads equipment, applies
attacker Transparent, target Abandon, target statuses, then final accuracy/rolls.
It does **not** apply physical Concentrate, attacker Darkness/Confusion, facing,
bow weather, Weapon Guard, or physical class evade. Damage weather later in the
amount calculation remains a separate stage.

`formula_evade_store_magical` resets unsigned-byte base hit to100 and all four
unsigned-byte contributions to0. Monsters return there. Other targets load
accessory magic evade only for a primary Accessory type, and each shield slot's
magic evade only for a primary Shield type. Job/class contribution remains0.
There is no weapon magic-evade contribution. Blocking-item labels are a separate
concern: the roll reports the accessory item; each hand reports its weapon if it
is not `ITEM_ID_NONE` (255), otherwise its shield. Item0 is distinct from None.
The label can therefore be a weapon even when the evade came from a shield.

Transparent clears contributions. Abandon halves base hit and writes result
reaction Abandon449 only when `unit_get_action_restriction` returns None. That
function checks Don't Act, mount, deep water, statuses/movement and rider facts;
the pure slice will take its classified result explicitly, not reimplement map
or mount rules. Target Don't Act, Sleep, Stop, Confusion, Charging or Performing
clear contributions; Defending then halves base hit. Both halvings stack to25.
Abandon's result marker survives a later status clear or earlier Transparent.
It is diagnostic result data, not execution of a reaction. Caller-supplied status
and classified restriction facts must describe the same target.

## Accuracy, RNG and ordering

The secondary execution Evadeable flag determines whether final checks run for
these nonzero spell abilities. The primary AI Evadeable metadata is separate.
Preparation still runs when checks are disabled, including Abandon's marker.
Disabled checks leave incoming hit/accuracy unchanged and draw nothing.

If any contribution is at least base hit, final checks set hit=false and
accuracy0. Otherwise each factor is integer `(base-evade)*100/base`; the four
factors multiply, divide by1,000,000, then scale the incoming unsigned16 accuracy
by that percentage/100. Do not combine divisions or add an aggregate accuracy
roll. Products fit signed32 in this domain. An accuracy rounded to0 by this
product does **not** itself clear hit here. This differs from the certain-evade
branch, even though the later result finalizer clears hit for either accuracy0.

Roll order is accessory, right hand, left hand, class. Each calls
`random_roll_pass_fail(base, evade)`, using real `rand()*base/32768`, then tests
execution state. Thus zero contributions still draw, and AI/preview draw four
times. Their random outcomes never clear hit or label a miss; the earlier
certain-evade branch can still have cleared hit. Execution stops at its first
successful evade and records the corresponding miss label/item. The class0
check draws even though it cannot evade. For base100/evade19, raw6225 evades and
6226 does not; this is scaled arithmetic, not modulo.

Zero-accuracy work can still enter the damage formula: seed1981 with three90
evades passes all execution checks, reports accuracy0 with hit still true, then
reaches the amount/proc tail before finalization. By contrast a preview with a
certain contribution still draws four times but skips amount/proc work because
hit is already false. A source-accurate coordinator must test hit, not accuracy,
at that branch.

Managed UInt64 counter exhaustion is not a PS1 rule. The planned pure query must
roll back all private RNG/results on exhaustion. It cannot require room for four
draws in advance: an execution evade can exit successfully on the last available
draw. A later composed query must also roll back earlier evade draws when a proc
tail subsequently exhausts the counter. No live stream is adopted by these APIs.

## Reuse and acceptance

The existing `PhysicalWeaponEvasion` is only a Weapon Guard contribution rule.
Sword/Stone helpers assume physical facing/class domains and their own admitted
states. Reusing their entire pipelines would import incorrect magic rules.
Reuse the verified RNG, existing miss labels, pure amount/proc/finalizer owners
and shared formula coordinator; leave existing physical/AI behavior untouched.
Keep the new magic stages in one cohesive `MagicEvasion` owner rather than
creating per-spell coordinators or a universal formula interface.

Selected bounded children:

- **ZOD-344:** load immutable contributions and blocking-item labels from resolved
  slot/type/byte facts, with monster and None255 behavior; no status or draws.
- **ZOD-345:** prepare base hit/contributions and Abandon diagnostic marker from
  explicit Transparent, classified restriction, target statuses and Defending.
- **ZOD-346:** resolve fresh supplied hit/accuracy through secondary Evadeable,
  exact products/ordered rolls, mode behavior and atomic RNG rejection.
- **ZOD-347:** add a shared formula entry before evasion, retaining the already-hit
  API, fresh/no-earlier-effects certificates, healing bypass, hit-versus-accuracy
  branch semantics and whole-query rollback. Return result/diagnostic data only.

The source-trace milestone passes eight new Python tests (47 descriptions plus
width/mode/tamper guards). Existing managed code is unchanged at ZOD-342; its last
full validation remains 4,618 .NET tests. Later implementation checkpoints and
validation are recorded below as each child completes.

MP/charge, live stats/equipment extraction, prepared spell grants, target geometry
(including the agreed future cone), actual commands/HP/status publication, magic
AI activation, owner drafts, JobStats, host/editor, automatic facing, broader
corpse/Golem/terminal/frame RNG, broad interfaces and ZOD-106 remain deferred.
