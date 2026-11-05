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
