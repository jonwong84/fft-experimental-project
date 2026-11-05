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

[ZOD-334](zod-334-faith-magic.md) now traces the shared Faith-based 0x08/0x0C
pipeline; [ZOD-335/336](../behavior/faith-magic-amounts.md) implement its first pure
signed/Faith and already-hit amount slices. [ZOD-337–339](../behavior/magic-elements-and-routing.md)
now add guarded weather/element composition and raw HP routing.
[ZOD-340](../behavior/magic-conditional-proc.md) now handles bounded proc decisions;
[ZOD-341](../behavior/magic-result-finalization.md) now finalizes bounded result data;
ZOD-342 next traces magical evasion/admission. The wider acceptance still needs the following work: Cover MA/Faith representation, arithmetic widths,
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
