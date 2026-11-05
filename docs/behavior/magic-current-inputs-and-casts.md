# Current magic inputs and cast queries — ZOD-348–357

Read-only source `11464943bbdbd2a0af022d90cb7f10455cf911df`, clean on2026-11-05.
Managed entry `61a0a3b16ec8fe51f593a16134b76ebd24b5cd3a`.
[Current-input receipt](../evidence/combat/magic-current-input-source-v1.json)
pins12 files/16 functions; [56 independent descriptions](../fixtures/behavior/magic-current-input-contract-v1.json)
cover33 compatibility relations,12 projections and11 current-input formula queries.
No original or C code is executed. Checkpoint validation is recorded as children
complete; source descriptions are not managed execution receipts.

## ZOD-348

`formula_store_ma_and_y` reads current effective `attributes[MAGIC_ATTACK]`, not
base or equipment-only MA. `action_calculate_target_result` copies current `faith`,
not `original_faith`, into pair work. `formula_calculate_faith` then applies current
Faith followed by Innocent independently to each copy; the unit's stored Faith
is not overwritten by that calculation. All three inputs are unsigned bytes.
The existing arithmetic preserves full storage widths and signed stores; reading
a current byte must not recompute or clamp it according to a normal stat limit.

`initial_stats_calculate` and `unit_equipment_attributes_set` are upstream sources
of base/equipment/effective attributes. `stat_delta_apply` updates base MA with
its minimum/cap, then recomputes effective MA with equipment; a Faith delta instead
updates current Faith directly within its own limits. These update paths explain
why original Faith or a job definition is the wrong input to the formula. This
ticket traces them; it does not port stat mutation, equipment refresh or jobs.

Strengthening is a nonempty intersection of current caster strengthen affinity
and spell elements. Current caster Magic Attack Up and target Magic Defense Up,
Shell, and the single Chicken-or-Frog step feed the existing amount owner.
Healing bypasses the damage defense/element/evasion chains as already traced.
Transparent comes from the caster; Abandon and evade-clearing statuses from the
target. Action restriction remains a caller-supplied classified environment fact,
with Don't Act also preventing action. Equipment contributions remain resolved
slot/type data, not a runtime original-data lookup. No owner toggle draft is wired in.

Zodiac classification uses absolute sign difference with the pinned twelve-entry
table: differences3/9 are Bad,4/8 Good,6 depends on identity, the rest Neutral.
Serpentarius on either side returns first, preserving its special no-floor path.
Opposition becomes Bad if either unit has the monster flag; otherwise equal
**pairs of male/female bits** mean Worst and unequal pairs Best. Do not normalize
the two bits to a single assumed sex or treat every monster as an ordinary male.
Unknown signs must be rejected rather than indexing outside the source table.

The existing sword `AttackUnitState` is intentionally narrow, lacks current MA/
Faith and enforces different state limits. Extending it casually would broaden
already-proven Attack/Stone/Dash domains. The selected child instead captures an
immutable magic input snapshot and keeps calculation/result publication separate.
It is a projection contract, not a replacement for the eventual complete battle
unit or a second mutable combat authority. Identical unit IDs must describe the
same current unit; self-targeting with a consistent snapshot remains possible.

Selected next work: compatibility classification (349), current-input projection
(350), pure current-pair query (351). Then separately trace resource/charge
preparation (352), implement MP admission (353), charge setup (354), charge tick
query (355), ready-cast preparation (356) and supplied ordered target resolution
(357). Every implementation child must validate its own traced boundaries.
Scheduling charge and spending MP at resolution are separate source phases.

Remaining limits include living/non-Petrify/Wall/no-Golem/empty-status/fresh result,
nonzero ordinary ability, no earlier reaction/reflection/pending work, Fire-no-Oil
and Earth-no-Float/rider. Unsupported context rejects without results or RNG
adoption. No live HP/status/MP publication, full charge scheduler, target finder,
cone geometry, prepared spell grants, AI activation or host/editor is added by
this trace. Owner drafts, JobStats, automatic facing, corpse/terminal/frame RNG,
broad interfaces and ZOD-106 remain separate/deferred.

## ZOD-349

`FaithMagicInputs.ClassifyCompatibility` now maps explicit `MagicZodiacIdentity`
to the existing amount enum. Three focused managed cases pass33 literal relations
and unknown-sign guards, including both independent sex bits, monster precedence,
reverse differences and Serpentarius. The existing arithmetic and source fixtures
are unchanged. Logs: `scratch/magic-inputs-348-357/focused-349.log`.

## ZOD-350

`MagicCurrentUnit` captures immutable current-byte/stat/status/support/affinity and
resolved equipment facts. It does not extend the sword state or perform stat
refresh. `FaithMagicInputs.Project` derives amount, element, Undead and evade
inputs, taking strengthening by intersection and Chicken/Frog once. Healing
ignores damage weather and equipment. Unsupported ordinary actor/target status,
Fire/Oil, Earth/Float/rider and inconsistent same-ID context return no projection.
Missing required facts and unknown enums throw; no field is synthesized from IDs.
Don't Act additionally disables the supplied action-unrestricted fact. No live
state, RNG or command is changed. Five cumulative focused cases pass33 relations,
12 projections and self-target/immutability/argument guards; two cases are new.
Logs: `scratch/magic-inputs-348-357/focused-350.log`.

## ZOD-351

`FaithMagicInputs.Resolve` now accepts a current immutable pair and spell inputs,
projects them once and delegates to the existing shared formula entry. Remaining
fresh/nonzero/no-earlier-effects/finalizer certificates remain explicit. Rejection
returns original RNG and no partial frame/evade/proc data. Eight cumulative input
cases (three new) and all76 focused magic cases pass, including11 literal current
queries, mode/RNG continuation, replay and unsupported current status. All prior
amount/proc/finalizer expectations remain unchanged. Logs:
`scratch/magic-inputs-348-357/focused-351.log`. No publisher or AI entry is connected.

## ZOD-352

[Resource source receipt](../evidence/combat/magic-resource-source-v1.json) pins12
files/10 functions at the same clean reference, including `action_prepare`,
`action_init_current_data`, `mp_check_and_consume`, `action_resolve_strike`, clock
steps and AI loading/simulation. [59 descriptions](../fixtures/behavior/magic-resource-contract-v1.json)
separate32 MP checks,10 execution setups, seven AI delays and10 counter steps.
Eight focused Python cases independently validate them before implementation.

`action_prepare` schedules CT before resolution: low seven bits, then Short Charge
ceiling `(ct+1)/2`, then Non-Charge zero. Persevere skips both. The plan alone does
not spend spell MP. `action_init_current_data` later calls the MP helper once,
before strike/target calculations. AI scratch simulation calls that same initializer.
MP must not be charged again for each target or ordinary strike in the same action.
The supplied-order query will cover one certified ordinary strike only; discovery,
original panel ordering, multiple strikes and reactions remain separate.

The MP helper resets recorded cost, performs primary action-state bookkeeping,
then restriction (Don't Act overrides), Frog exceptions (IDs0/29/367, never Charge),
then Silence for Default/Monster menus within signed ability ID0..367. Only Default
non-Mime abilities read/pay cost. Half MP floors. Cost is recorded **before** budget
failure. The MP field is unsigned16 but is read through signed16: high-bit values
fail even a zero cost. Preview skips subtraction only; AI and execution subtract
from their respective state. A zero-cost successful store still occurs. Early
failures and Mime/other-menu bypasses leave recorded cost0. The pure helper will
require prior action-state bookkeeping to be certified; it will not implement
KEEP_PERFORMING or erase Charging/Defending/Jump state itself.

The AI candidate CT path is a distinct original quirk: it reads the full raw CT
byte, tests Monster Skill to halve with a floor, else Defend to zero, and bypasses
those changes for Persevere. It does not use execution's Short Charge/Non-Charge
bits, rounding or high-bit mask. Both calculations must remain separate. This
does not activate AI planning for magic.

Charge ticking is one decrement unless the resolved wait-menu block predicate is
true or the counter is0/255. Zero is ready only when unblocked;255 means no charge.
Haste/Slow bits do not accelerate this decrement. Source classification includes
inactive/frozen/mounted/dead states and the original unreachable Sleep branch;
the bounded helper takes the already-classified predicate and does not repair it.
It will emit no event, advance no global clock, and perform no repeated Song/Dance
or terminal/status work.

## ZOD-353

`MagicResources.EvaluateMp` now returns source block reason, recorded cost and
candidate MP after certified action-state bookkeeping. It distinguishes accepted
query context from a source-unusable action. Default/Monster/Charge/Other are
explicit menu classifications, not inferred formula identities. The original
signed ID0..367 branch is a compatibility rule rather than a managed catalog cap.
`SubtractionPerformed` means the source store runs, including cost0; it does not
mean MP changed. Two focused cases pass all32 literal MP descriptions, immutable
input and argument/bookkeeping guards. No MP or status is published. Logs:
`scratch/magic-inputs-348-357/focused-353.log`.

## ZOD-354

`MagicCharge.CalculateSetup` now returns execution setup data with mask, ceiling,
support order and Persevere. `CalculateOrdinaryAiDelay` separately preserves the
ordinary AI entry quirk (full byte, Monster Skill floor, else Defend). Neither
method spends MP or publishes action status. Four cumulative resource cases pass
all49 MP/setup/AI descriptions; two cases are new. These are distinct source
phases, not alternate rounding options for one algorithm. Logs:
`scratch/magic-inputs-348-357/focused-354.log`.

## ZOD-355

`MagicCharge.Tick` now returns one immutable counter/readiness/change query from
the classified source wait-menu block fact. None255 remains None, active counters
decrement once, zero stays ready only when unblocked, and blocked counters retain
their bits. It does not select a ready unit, reset the counter after resolution,
repeat performances, emit events or advance the global clock. Five cumulative
resource cases pass all59 descriptions; one counter case is new. Logs:
`scratch/magic-inputs-348-357/focused-355.log`.

## ZOD-356

`MagicCastQuery.PrepareReady` now creates opaque immutable `PreparedMagicCast`
data only after positive resolution-readiness, completed action-state bookkeeping,
no-Hamedo, fresh-result and one ordinary Default-menu strike certificates. This
accepts a completed positive-CT spell; it does not confuse raw CT with remaining
delay or spend MP while scheduling. Mime, Persevere, unsupported actor states,
nonordinary IDs and uncertified result work are excluded. Existing represented
Charging/Performing/Defending states must already be clear; unrepresented Jump
and source bookkeeping fields must also be certified by the caller. No implicit
KEEP_PERFORMING or unit mutation occurs here.

The cast definition has a stable logical ID and explicit compatible formula/cost/
charge parameters. It is query input, not a new runtime importer or a complete
spell catalog. Source ID1..367 is an original compatibility branch only. A source
MP block returns its recorded cost/candidate unchanged MP without a prepared cast;
unsupported query context returns neither. An affordable action proposes MP once,
including AI scratch subtraction and preview's unchanged MP. Six focused cases
pass mode, readiness, block-detail and immutable-input/argument checks. Logs:
`scratch/magic-inputs-348-357/focused-356.log`. A prepared proposal grants no live
command authority and must not be adopted independently of later validation.
Input MP is the pre-spend snapshot for this action; resolution readiness does not
mean the source MP subtraction has already run.

## ZOD-357

`MagicCastQuery.ResolveTargets` now evaluates an immutable, explicitly supplied
nonempty target order using one prepared ready-cast cost. Eligibility/order and
no-earlier-target-effects certificates are mandatory; discovery, original panel
ordering and geometry are still external. Unique stable unit IDs are checked,
without sorting, silently deduplicating or imposing the original fixed roster
capacity. Duplicate and empty lists remain outside this query's selected domain;
this does not claim original zero-target MP behavior.

All per-target calculations use the shared current-input/formula owner, with
the original mode-specific RNG stream continued in caller order. A consistent
self-target snapshot is privately normalized to the post-cost MP proposal;
conflicting self data rejects. Preview proposes unchanged MP while retaining
evade draws; AI proposes one scratch MP subtraction. An actual evade miss does
not refund cost. This is one certified ordinary Default-menu strike, not a
multi-strike, reflection, reaction or target-selection implementation.

Accepted output contains immutable target frames/diagnostics, one candidate MP/
recorded cost and the final candidate RNG. Unsupported later target context or
counter exhaustion discards **all** private target results and exposes neither
an MP proposal nor an intermediate stream. That is managed query atomicity, not
an instruction to refund already-published retail MP. No state is published by
preparation or resolution. The resulting data is not a trusted command receipt.

[13 further literal cast descriptions](../fixtures/behavior/magic-cast-contract-v1.json)
cover one-cost multi-target behavior, reversed supplied order, all three modes,
early miss, late context/counter failure, duplicate/empty/uncertified lists,
self-healing at counter exhaustion, and Half MP once. Three new managed cases
also check replay/input isolation and conflicting self data; all90 focused magic
cases pass. Eight new Python cases independently check the composed descriptions.

Full ten-ticket validation: locked restore, zero-warning/error Release build,
**4,652 .NET tests** (1,611 Framework, 1,217 AI, 838 Content, 328 CLI) and **673
Python tests**. There are22 new managed cases and24 new Python cases, with128
new source descriptions. Current-input source12/16 and resource source12/10,
prepared empty-status/four-Fire-tier, prior Faith/element/proc/finalizer, algorithm
guide, coverage, local links and preservation checks pass. All pre-batch C# code,
tests/fixtures, owner drafts, JobStats, content/AI/host/project/dependency code
remain unchanged. Source receipts preserve their original pins and provenance.
Seven TRX reports and logs: `scratch/magic-inputs-348-357/`.
[Implementation receipt](../evidence/combat/magic-current-input-implementation-v1.json).

ZOD-358–362 now provide [ordinary map-aim geometry](ordinary-spell-targeting.md)
and derive a source-ordered target list for this existing cast query. Unit-follow,
special shapes, commands, live publication and magic AI remain separate. Next
ZOD-363 traces unit-follow and stored-aim resolution. The agreed future forward
nine-tile1/3/5 cone remains unimplemented and independent of shared arithmetic.
