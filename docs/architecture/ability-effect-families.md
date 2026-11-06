# Shared ability effect families — ZOD-333

Source inventory 2026-11-05, managed base `3f194e6`, clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Source receipt](../evidence/combat/ability-formula-source-v1.json): ten files and
17 focused function fingerprints. This inventories dispatch and data boundaries;
it does not complete the arithmetic/ordering trace or managed port of every rule.

## Reuse established by source and prepared data

`action_calculate_target_result` dispatches through `s_formulas[formula - 1]`:
100 one-based slots, including five empty handlers at 0x11/13/18/19/46. Zero and
out-of-range formula bytes fall back to weapon formula1. These are observed
compatibility limits, not proposed managed catalog capacities.

The edition/hash-verified executable has 368 ordinary secondary slots, including
special/unused entries and slot0. They contain 82 distinct raw formula-byte values
(including five rows with formula0). This is neither the number of playable
abilities nor a count of implemented mechanics. Formula presence in this table
does not establish campaign reachability, AI usability or complete weapon/item
coverage.

Several examples establish useful implementation families:

- Formula0x08 (`formula_magical_damage`) serves 41 ordinary records. Fire/Fire2/
  Fire3/Fire4 and the corresponding Ice/Bolt tiers share it; Y supplies power,
  while element, geometry, MP/charge and presentation remain separate data.
- Formula0x0C (`formula_magical_heal`) serves six records, including Cure1–4.
  It shares MA/Y, strengthening, compatibility and Faith machinery with other
  magic, but its healing/undead behavior differs from 0x08's damage path.
- Formula0x37 (`formula_damage_random_y_times_pa`) serves Dash, Throw Stone,
  Cat Kick and Tail Swing. Y values4/2/8/15 and geometry differ. The existing
  neutral managed Dash/Stone rules already share actual random-PA resolution;
  Cat Kick/Tail Swing are inventory examples, not newly supported actions.
- Formula0x36 (`formula_add_pa_y`) serves Accumulate and another PA-increase
  record. The source shares the handler; supported targeting/state domains still
  need independent validation before admitting the second record.
- Formula0x0A serves38 records, 0x0B22, and 0x4E25. These are distinct dispatch
  entries with different accuracy/Faith/defense paths; similar-looking arithmetic
  alone does not justify collapsing their behavior.

The ignored full inventory includes each dispatch symbol and each ordinary
secondary record's formula, X/Y, geometry, flags, element, status, CT and MP.
Generated catalogs and decoded records stay in scratch. The committed note and
source receipt record conclusions and provenance instead of shipping that data.

## Separate effect arithmetic, admission, AI and presentation

The ordinary secondary record selects formula/X/Y and execution flags.
`ability_range_set` separately reads range, area, vertical and weapon/shape flags.
`ai_ability_entry_load` and `ai_ability_outcome_evaluate` load metadata and select
target/search policy. `ai_unit_abilities_init` counts primary AI evasion/Silence
flags, while secondary flags determine exhaustive origin and consumed-aim policy.
This distinction matters for retail Dash: AI Evadeable is set, combat Evadeable
is clear. [Prepared binding](../development/dash-content-binding.md) preserves both.

`formula_get_random` supplies midpoint0x4000 during AI/preview and real rand during
execution. Execution-only knockback branches remain separate. Shared arithmetic
must preserve widths, order and the source RNG contract for each use.
`animation_ability_attack_select` reads the separate ability-animation table;
animation differences do not create a new damage formula.

Formula/X/Y is not a complete effect definition in every case. Song and Dance
delegate to helpers that switch on ability identity to choose HP/MP/stat/CT/status
results. Future semantic definitions must preserve those result selectors. Other
special rules, inventory dependencies and reaction/status hooks stay explicit.

Menu dispatch also changes formula selection: ordinary/default, monster, Math and
Draw Out paths consult secondary data; Item uses item data; Throw selects0x63;
Jump selects0x64; Charge sets charge power and falls through to the weapon formula.
Weapon procs can re-enter a spell path. An inventory of ordinary rows alone cannot
declare those paths covered or unused.

The owner has agreed to formula-family implementations in C# and individual
ability definitions in validated JSON, with proposed Formulas/CombatRules
namespaces. [ZOD-419 equipment selection](equipment-effect-selection.md) traces
the shared equipment picker and its distinct break/steal/Mighty Sword callers.
Its per-ability slot policies become explicit typed content rather than new
ability-specific classes; the source hand choice and failure paths stay intact.
The broad namespace/content migration is not implemented by that source study.

## Formula reassignment requirement

The owner wants to assign Dash the formula normally used by Repeating Fist.
At the pinned reference revision above, Dash uses0x37 and
[`formula_repeating_fist`](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/formulas.c#L785)
is0x32. The latter reads both X and Y, calls the shared physical-modifier and
critical-hit steps, adds the prepared XA/YA terms, then applies its random factor.
It does not call0x37's damage-and-two-Brave-roll-knockback helper. Its critical
step can itself request knockback. This is a distinct full formula path, not
an alias for0x37 with a different power value. This focused check does not
complete0x32's width/modifier/RNG/admission trace or implement its handler.

Formula IDs should select reusable implementations with typed parameters. A
formula change requires a valid parameter definition for the destination formula;
do not silently reinterpret one generic Power field or copy another ability's
complete content record. A raw compatibility formula-byte edit and a semantic
formula-plus-parameters edit are different operations and need explicit handling.
Range, targeting restrictions, cost/timing, presentation and independently owned
execution flags remain separate. Formula-owned critical/knockback/result behavior
must follow the selected implementation; retaining a special Dash-only tail
would defeat the reassignment. A separately configurable custom extra effect
would require an explicit rule and ordering, not an implicit retained behavior.

Current `TargetedAbilityDefinition`/schema1 companions and bounded Dash/Stone
profiles intentionally admit fixed effects. Actual execution still selects their
closed paths, and Dash's AI projection directly calls `DashFormula`. They do
**not** support this reassignment today. Extend the model/package and shared
execution/AI owners together, preserving existing schema1 content and regressions.
Reject unsupported formulas or incomplete parameter/context combinations at
validation; do not silently execute Dash's original formula instead.

Required refactor proof: keep Dash's identity/targeting/presentation constant,
select a supported0x32 parameter definition in custom content, and verify that
actual execution, preview and AI all use it with their correct RNG contracts.
Keep original Dash/Throw Stone definitions and behavior unchanged. No new
ability-specific RepeatingFist class, editor work or runtime C dependency is
needed to express the assignment. The supported0x32 handler and its source trace
are necessary before the example can execute.

## Pre-formula setup boundary

The owner supplied the annotated FFHacktics
[Pre Formula Setup (FDC)](https://ffhacktics.com/wiki/Pre_Formula_Setup_%28FDC%29)
disassembly after direct wiki access returned403. It maps to
`action_calculate_target_result` at0x8018B34C in the pinned reference above.
The formula call returns at0x8018B97C. The
[ZOD-419 source receipt](../evidence/combat/equipment-selection-source-v1.json)
already pins this function and its full source file. This comparison checks
source/annotation correspondence; it is not original/C execution or a completed
port of reaction, item, Jump or other setup branches.

The general calculation boundary must retain these responsibilities:

- Copy the command and establish actor/target context. Non-primary reaction
  handling and pending knockback can exit before ordinary formula dispatch.
- Select the strike's weapon/item context, copy source definitions and initial
  Faith values, then initialize result work. `action_clear_current_data` preserves
  reaction_id and last_received_attack, sets hit/accuracy defaults and clears
  status work/canceled statuses. `data_clear` additionally makes the attacker
  result not-hit. A whole-record reset would lose source-retained information.
- Resolve the action menu and any pending proc or effective-ability substitution.
  Ordinary/default/monster/Math/Draw Out paths read the selected ability formula;
  Item uses item data; Throw selects0x63; Jump selects0x64; Attack/Charge use the
  weapon formula, with Charge contributing its power. Geomancy and Draw Out
  have their own effective-ability selection. Compatibility assignments must
  respect this delivery-dependent precedence.
- Load the effective status-application record, including its selection flags,
  and prepare canceled-status eligibility before entering the formula. Formula3
  forces the empty status record here. The original maps formula0 or values
  outside1..0x64 to weapon formula1; unsupported implemented handlers within that
  range still need explicit managed admission, not an accidental substitute.
- Run evasion reactions before formula dispatch. A pre-formula miss skips the
  handler. Formula-specific evasion remains inside applicable handlers.
- After the handler, run applicable formula-specific aftermath and common result
  finalization. This routine calculates results; later result application owns
  authoritative HP/MP/status/equipment mutation.

The C return value is not a boolean saying a formula ran. It is1 on the
pre-formula evasion-reaction miss path, and0 otherwise, including the reaction
and knockback early exits. Managed diagnostics must distinguish those paths.

Primary AI metadata also contributes actual miss-display team/dodge flags in
this setup. That does not make the primary AI Evadeable bit the execution
evasion switch. Keep field meanings and consumers explicit when consolidating
the owner drafts; naming a metadata group does not prove it has only one consumer.

For the shared model, an ability references a reusable status-application set
with stable identity, membership and application flags. Individual status
behavior is separate. Canceled-status preparation belongs to shared setup even
when a selected formula does not normally apply statuses. Status membership is
not automatically applied merely because the ability references the set.

The refactor should compose preparation, selected formula/shared combat rules,
finalization and application through explicit state and execution/preview/AI
context. Preserve required retained facts, source order and RNG boundaries.
Neither RAM addresses nor mutable C globals become runtime dependencies. This
records the calculation boundary; it does not introduce a broad public interface
or activate unsupported reaction/item/status families.

## Navigation buckets and next development step

The tool groups all100 dispatch slots once into eleven navigation buckets:
weapon/delivery; Faith magic; equipment/theft/stat attacks; Song/Dance; multi-hit
magic; Draw Out/Elemental; physical/recovery/self buffs; special results;
consumable recovery; monster/other effects; empty handlers. These buckets aid
source navigation and are not proposed runtime interfaces. Exact handler identity
and independently traced common subroutines determine actual reuse.
The multi-hit navigation bucket also contains the related single-strike0x5F:
0x5E/0x5F share a damage chain, while strike setup treats their IDs differently.
Grouping nearby mechanics does not make their delivery rules interchangeable.

Build shared effect handlers and reusable targeting/result rules, with immutable
ability parameters and separate presentation references. Validate each distinct
formula/rule combination and its exceptions. Preserve original source regressions
and add catalog coverage so every required ability is accounted for. A shared
formula does not by itself enable unsupported area, charge, element, status or
targeting behavior. Avoid creating a new AI coordinator for each ability or one
universal formula interface before concrete consumers justify it.

[ZOD-343](../development/shared-magic-calculation.md) now consolidates the supported
Faith calculation behind one entry while retaining its tested rule helpers.
Formula selection and target geometry remain independent. The owner-specified
future cone has rows1/3/5 ahead of the actor (nine tiles), rotated with attack
direction; it is recorded for later targeting work and is not implemented here.

[ZOD-334](zod-334-faith-magic.md) now traces the shared Faith-based 0x08/0x0C
pipeline; [ZOD-335/336](../behavior/faith-magic-amounts.md) implement its first pure
signed/Faith and already-hit amount slices. [ZOD-337–339](../behavior/magic-elements-and-routing.md)
now add guarded weather/element composition and raw HP routing.
[ZOD-340](../behavior/magic-conditional-proc.md) now handles bounded proc decisions;
[ZOD-341](../behavior/magic-result-finalization.md) now finalizes bounded result data;
[ZOD-342/344–347](../behavior/magic-evasion.md) now add bounded magic evasion and
fresh formula entry; [ZOD-348–357](../behavior/magic-current-inputs-and-casts.md) now add current-input,
resource/timing and supplied ready-cast queries. ZOD-358 next traces original
targeting geometry; live state/command/AI activation remains separate. The wider acceptance still needs the following work: Cover MA/Faith representation, arithmetic widths,
modifier order, evasion, element/undead rules, charging/MP, area/vertical targeting
and actual-versus-AI evaluation. Design shared primitives and data requirements
before implementing a representative spell. Broader mechanics remain deferred
until their own evidence and acceptance boundaries are selected.

Reproduce the inventory (standard-library Python; fresh scratch output):

```powershell
python tools/verification/inventory_ability_formulas.py --reference C:/repositories/fft_decomp --prepared-executable scratch/all-data-extraction/SCUS_942.21 --output scratch/ability-formulas-new.json
```

Seven synthetic Python tests cover dispatch indexing/admission, fallback bytes,
shared-formula parameter preservation, record widths, bucket coverage and scratch
output confinement. The live audit verifies the pinned source hashes and prepared
edition/hash before inventorying. No original/C code is executed or linked.
