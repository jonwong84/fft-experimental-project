# ZOD-104 — First executable job ability: Accumulate

Date: 2026-10-26. Managed base: `303d2eaaaae93975b8a283fc47133850b3403e0a`,
clean on entry. Read-only `C:\repositories\fft_decomp` HEAD freshly checked clean:
`11464943bbdbd2a0af022d90cb7f10455cf911df`. All reference paths and symbols below
refer to that commit. Target: North American PS1 `SCUS-94221`.

Select **Accumulate**, ability 146 (`0x92`), as the first executable job ability.
This completes a source/data investigation and implementation contract under
ZOD-038. It changes no production code, schemas, existing profiles or editor UI.
The [evidence and acceptance inventory](../evidence/combat/accumulate-contract-v1.json)
separates completed offline inspections from **32 unexecuted implementation cases**.
ZOD-105 implements the pure content/action proof, ZOD-106 adds persistence, and
ZOD-107 exposes the action in the existing playable Windows battle.

## Verified local data

Re-read the prepared `scratch/all-data-extraction/SCUS_942.21`: 356,352 bytes,
SHA-256 `287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562`.
Its PS-X EXE header loads initialized data at `0x80010000`, payload `0x56800`,
file header `0x800`. Map addresses using `address - 0x80010000 + 0x800`, with
reads confined to that payload. This pass uses the prepared executable, not the
BIN or an executed reference program.

All 22 records in the historical selected-metadata import were independently
compared with those executable bytes and their mapped offsets. Four selected
records establish generic Squire job 74 → Basic Skill skillset 5 → first action
slot Accumulate 146, plus its secondary record. These are ordinary Squire records,
distinct from the special `01 Squire` seen in the earlier FFTPatcher UI pass.
The old provenance retains its historical reference revision; it is not rewritten.

The current `target/main.yaml` labels and current structure declarations give:

- `g_job_data[74]`: file `0x52698`, 48 bytes.
- `g_ability_human_skillset_abilities[5]`: file `0x55311`, 25 bytes.
- `g_ability_data[146]`: file `0x4f880`, 8 bytes (learning/AI metadata).
- `g_ability_range_data[146]`: file `0x50bec`, 14 bytes.
- `g_command_menu_types_by_skillset[5]`: file `0x564b9`, one byte, default menu.
- `g_status_infliction_data[0]`: file `0x547c4`, six bytes, empty type/statuses.

The secondary record decodes to range/AoE/vertical 0; Auto in flags 1;
No Follow Target in flags 2; flags 3/4 and element 0; formula `0x36`, X 0,
Y 1, status-infliction index 0, charge CT 0 and MP cost 0. Raw records stay in
ignored scratch. FFTPatcher's owner-identified original defaults are a useful
cross-reference; the findings here come from this hash-verified prepared input.

## Selection, preparation and targeting

`src/battle/menu/ability_choices.c::skillset_get_unit_ability_data` lists learned
action slots, applying Frog and weapon requirements and deriving MP/CT/menu flags.
It does not exclude Accumulate at the PA cap. **Skillset membership does not prove
that a unit has learned the ability.** The first authored battle must explicitly
grant this action to its actor; a learning/job progression system is outside scope.
The shared content adapter must validate that the grant resolves an action-slot
member and an executable effect, rather than inferring execution from a name or
learning-only `AbilityDefinition`.

`src/battle/combat/targets.c::target_range_set_for_action` returns no manual
targeting for Auto + range 0 without Weapon Range.
`src/battle/turn/controller.c::target_tile_select` then sets the actor's own tile.
`target_area_set_for_action` / the ordinary ability area pass use AoE 0 and do
not remove the caster; `preparation.c::units_on_panels_list` collects the units
on those panels. The first supported dry, flat, lower-layer scene has distinct
unit positions, no mounts or overlapping layers, so the actor is the sole target.
This is not a general targeting/vertical/layer implementation.

`preparation.c::action_prepare` reads `ct & 0x7f` and returns immediate for CT 0.
The primary commit sets `action_taken`; `action_commit_preparation` also clears
Charging/Jump/Defending/Performing for immediate actions. Those statuses are absent
from the selected profile. `action_init_current_data` calls `mp_check_and_consume`:
ordinary action restrictions still apply, but the verified cost is zero.

`controller.c::action_execute_ability` marks Act through `turn_act_taken_set`
before preparation/effect playback. `src/battle/turn/clock.c` leaves Move available
if unused and ends the turn after both choices. Its ordinary refund is 20 for
each unused choice, capped at CT 60. Reuse the existing bounded `ConfirmAct`/
Move/Wait behavior; do not advance the clock automatically while resolving an
ability. A managed command publishes consumption and effects atomically; that
rollback guarantee is a selected API contract, not a claim about every original
menu failure path.

## Formula, widths, application and draws

`preparation.c::strike_data_init` starts at one strike. Formula `0x36` is neither
random multihit nor a weapon-range/Two Swords action. `action_resolve_strike`
calculates the target result and leaves the separate attacker result distinct.
Self-targeting must update one combatant once; it must not lose or double the
change by publishing separate actor/target copies.

`resolution.c::action_calculate_target_result` selects the verified default-menu
secondary formula and dispatches `s_formulas[formula - 1]`. Slot `0x35` is
`formula_core.c::formula_add_pa_y`. That function writes `Y | 0x80` into the
unsigned-byte `action_data_t.pa_change` and sets pseudo-status attack type. With
Y 1 the low-seven-bit amount is 1 and the high bit means increase.

`action_clear_current_data` initializes accuracy 100 and hit 1. The formula
does not perform an accuracy, evade, critical or proc roll. With no status,
reaction, support, mount, mimic, random-fire or pending proc work, neither
the selected preparation nor result path adds a resolution RNG draw. This is
**zero resolution draws within the stated profile**, not zero draws for a whole
turn: `clock.c::upkeep_action_build` still draws when the next turn becomes ready,
as the existing managed scheduler does. Presentation/effect timing is separate.

`resolution.c::action_finalize_target_result` can cancel Dead/Petrify/Wall targets;
those states are excluded here apart from the existing supported HP-derived
Critical projection. The flags do not request reflect, Golem, weapon evasion or
status effects. `action_apply_target_result` applies a hit's delta with
`stat_delta_apply(pa_change, &basePA, UNIT_ATTACK_STAT_MAX, 1)`.

The helper loads the unsigned-byte base into signed-32 working arithmetic, masks
the amount with `0x7f`, adds it, then clamps to 1..99 **before** byte storage.
When the base changed, it recomputes effective PA as base plus the equipment bonus
and caps at 99. If the base was unchanged, it returns without rebuilding effective
PA. The three base/equipment/effective arrays are distinct in `unit_stats_t`.
Thus the bounded zero-bonus profile gives 5 → 6, 98 → 99 and 99 → 99.
An at-cap use still consumes Act and resolves successfully, without a PA-changed
event. These are source-derived expectations, not an executed original comparison.

`formula_core.c::formula_calculate_base_xa` reads effective PA for a later sword
attack. The increase must survive subsequent Move/Wait/Advance and affect that
attack. It is a battle-state mutation, not an edit to `JobDefinition` or the
original package. `src/main/unit_init.c` separately initializes base stats and
rebuilds equipment totals; permanent party growth/after-battle transfer is not
implemented or asserted by this milestone.

Original resolution can use the changed outcome for EXP and later reward work.
Rewards/progression are omitted from this authored battle just as they are from
its existing sword action. Do not call the resulting slice complete Accumulate
compatibility; successful/capped reward behavior remains a separate ZOD-038 gap.

## Selected managed contract

Keep simulation in the existing FFT rule area of `Zodiac.Framework`; the new
calculation should be a small named C# operation with explicit inputs/results.
Keep content validation/projection and offline conversion in their existing
Content/AssetTool boundaries. No Tessera Engine, public contract, dynamic code or
general formula language is needed. The proposed sword-arithmetic extraction was
considered: Accumulate does not share damage arithmetic, so retain the existing
sword resolver and its draw order in this milestone, using its current PA input.

Select a separately identified authored ability-enabled scenario/profile. Existing
sword-only scenarios, definitions and replays keep their behavior. The profile:

- Admits living ordinary units in the existing supported geometry and neutral
  sword domain, with zero equipment PA bonus and no additional modifier systems.
  PA is 1..99. Base and effective PA coincide in this restricted domain; current
  `PhysicalAttack` may represent that changing battle value. Do not implement
  equipment bonuses by increasing an already capped effective value later.
- Carries immutable per-unit action grants and an immutable effect binding linked
  by `AbilityId`. Use a separate versioned immediate-ability companion package,
  following the existing sword/learning-metadata separation. Keep metadata v1
  unchanged. Initially admit only immediate self PA+1; unknown formulas, costs,
  flags, parameter variants or unresolved references fail explicitly.
- Validates any original conversion against the selected secondary/menu/status
  records, preserving raw provenance under scratch. Do not make every metadata
  ability executable, or substitute Accumulate when an unsupported effect loads.
- Keeps grant/job/skillset definitions immutable. Granting the action is explicit
  authored setup, not implicit ownership of all abilities in a job's list.
- Checks the selected sword's worst *reachable* PA envelope at setup. Since PA
  can reach 99, require `(2 * 99 - 1) * power <= 32767` (power at most 166) for
  this first combined profile. Checking only initial PA can admit later overflow.
  The old sword-only profile retains its existing wider PA domain/checks.

The typed command carries actor and ability IDs and has no target parameter.
Validate the known living actor, active turn, unused Act, explicit grant, effect
binding and profile domain before publication. Reject invalid commands with the
same input state, no events or RNG consumption. Reject an ability command after
scenario completion through the existing outcome gate.

On acceptance, consume Act through the shared turn transition, calculate the
clamped PA, and publish one immutable snapshot. Preserve position, facing, HP/MP,
other unit stats, RNG word/count and clock ticks. Emit existing turn events first,
then an ability-resolved event carrying actor/ability and before/after PA, and a
PA-changed event only when different. At cap emit resolution but no change event.
This event shape/order is an authored contract, not a reconstruction of original
display messages. Probe the pure operation for previews; never apply a preview
to live state or consume its Act. Restart uses the original setup PA/grants.

## Persistence and delivery sequence

`AttackUnitState.PhysicalAttack` is currently treated as an effective immutable
input. `TwoUnitBattleDefinition.Restore` rejects changed PA;
`TwoUnitBattleSave.UnitDto` omits PA and the reader reconstructs it from the
starting definition. Saving a boosted battle with that design would lose state.

1. **ZOD-105 — pure content/action/scenario proof:** add the closed immutable
   effect/grant representation, validated companion data and pure self action.
   Exercise later sword damage and deterministic journals. Existing save entry
   points must fail explicitly for ability-enabled states until ZOD-106 supports
   them; never silently serialize away PA. No editor or Windows work.
2. **ZOD-106 — checkpoint and replay continuation:** add explicit current battle
   PA for the new profile in a new battle-save schema. Retain reading schema 1/2
   for their old sword-only profiles. Legacy documents lack PA/action-profile
   information, so reject them against the new profile rather than inventing a
   boost or upgrading their meaning. Preserve schema-2 writer/output behavior
   for old profiles. Validate new PA against initial PA..99, profile/grant/content
   identities and all existing structural/integrity checks. Save definitions
   never come from the document. Fresh-definition restore must reproduce
   uninterrupted state, events, damage and RNG; restart resets PA. No world-save
   slot/schema expansion is selected.
3. **ZOD-107 — existing Windows battle controls:** expose the granted self action,
   current PA and success/capped feedback, while retaining Move/Attack/Wait.
   Add a separate prepared ability-enabled launch/profile; keep the existing
   introduction/world/editor flows intact. The native validation plan must be
   written before execution; include finite repeated checks and owner review.

The full [acceptance inventory](../evidence/combat/accumulate-contract-v1.json)
assigns cases to these three children. Implementation requires appropriate
build/suite runs, not just checking this specification. Further UI polish,
charged magic, general targeting, statuses/reactions, equipment PA bonuses,
AI, XP/JP, learning/progression and original animation/timing stay outside scope.
Existing documented deferrals remain in effect.

## Executed checks and limits

The one-off offline audit script and all original record bytes/reports remain
under ignored scratch. Final reports `scratch/accumulate-104-e/audit.json` and
`scratch/accumulate-104-f/audit.json` match byte-for-byte at SHA-256
`671d6378180b87ea35323047dcf9316984d6263155989aa516340d90123d1cb7`.
They compare all 22 prior imported records, independently check the two added
menu/status records, verify formula-table slot 54, and fingerprint 17 source files.
Earlier a/b and c/d reports are retained as inspection checkpoints before the
turn-clock and ability-menu traces were added.

Reproduce the local audit with the available Python runtime:

```powershell
python scratch/inspect_accumulate_104.py scratch/accumulate-104-new
```

The output directory must be fresh; the script checks input SHA/header and
reference revision/cleanliness before reading records. The script is a local
inspection aid, not a new runtime/tool distribution dependency. The committed
evidence contains identities/expectations, not extracted records.

Evidence/report hashes, the six documented offsets, local links, unique backlog
IDs and all 32 case assignments passed a separate consistency check. The existing
coverage verifier passes all 58 planning entries across nine domains; its readable
view was regenerated. `git diff --check` passes. These are documentation checks,
not execution of the future ability cases.

No managed implementation, original/emulator execution, new native app launch,
visual approval or runtime-equivalence claim was made. No .NET/Python suites were
rerun for this specification; last implementation validation remains **2,349
.NET / 293 Python tests**. The future 32 acceptance cases remain unexecuted.
