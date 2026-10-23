# Baseline coverage entries

Generated from `baseline-coverage.json` by `tools/verification/verify_baseline_coverage.py --write-view`.
Edit the JSON, then regenerate this view. See [the inventory guide](README.md) for status/evidence rules.

Target: **SCUS-94221**. Snapshot: 2026-10-23; managed base `7990b5d`; reference `11464943bbdbd2a0af022d90cb7f10455cf911df`.
These entries are planning scopes, not a feature-completion percentage or a complete record/encounter catalog.

## Simulation

### COV-SIM-001 — PS1 RNG algorithm and gameplay draw ordering

Status: **partial**; scope: **baseline**; ticket: **ZOD-038**; owner area: Fft.Core simulation.

FFT BIOS RNG algorithm/state is implemented with supplied-BIOS vectors; draw ordering is covered only for bounded CT/sword paths.

Evidence: [docs/behavior/bios-rng-verification.md](../../docs/behavior/bios-rng-verification.md), [docs/behavior/rng-state-and-ordering.md](../../docs/behavior/rng-state-and-ordering.md), [docs/architecture/zod-040-event-script-investigation.md](../../docs/architecture/zod-040-event-script-investigation.md).

Source entry points (pinned revision above; not full traces): [include/fft/random.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/random.h), [src/battle/turn/clock.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/turn/clock.c), [src/lib/event/script/control.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/control.c).

Current implementation: [src/Fft.Core/FftBiosRandom.cs](../../src/Fft.Core/FftBiosRandom.cs).

Existing bounded checks: [tests/Fft.Core.Tests/FftBiosRandomTests.cs](../../tests/Fft.Core.Tests/FftBiosRandomTests.cs), [tests/Fft.Core.Tests/RandomReplayRunnerTests.cs](../../tests/Fft.Core.Tests/RandomReplayRunnerTests.cs).

Manual checkpoint (pending-original-observation): Observe controlled original battle inputs and record incoming RNG state/draw order.

Remaining gaps:

- Whole-game consumers and seed lifecycles remain open; the event interpreter uses a separate RNG, whose algorithm/seed/order is not covered by the BIOS proof.

Next action: Extend ordering traces one new rule/caller at a time.

### COV-SIM-002 — CT, readiness, turn choices and AT prediction

Status: **partial**; scope: **baseline**; ticket: **ZOD-036**; owner area: Fft.Core simulation.

Positive-Speed living ordinary units, CT refunds and explicit move/action/Wait are implemented; full scheduler is absent.

Evidence: [docs/behavior/ct-turn-implementation.md](../../docs/behavior/ct-turn-implementation.md).

Source entry points (pinned revision above; not full traces): [src/battle/turn/clock.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/turn/clock.c), [include/fft/turn.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/turn.h).

Current implementation: [src/Fft.Core/TurnCycle.cs](../../src/Fft.Core/TurnCycle.cs), [src/Fft.Core/TurnBattleState.cs](../../src/Fft.Core/TurnBattleState.cs).

Existing bounded checks: [tests/Fft.Core.Tests/TurnCycleTests.cs](../../tests/Fft.Core.Tests/TurnCycleTests.cs), [tests/Fft.Core.Tests/TurnBattleSimulatorTests.cs](../../tests/Fft.Core.Tests/TurnBattleSimulatorTests.cs).

Manual checkpoint (pending-original-observation): Compare readiness, ties, Wait refunds and charged/status turns with original observations.

Remaining gaps:

- Statuses, charge timers, Mimes, presence changes and AT-list prediction remain unsupported.

Next action: Select one status/charge interaction after a source trace.

### COV-SIM-003 — Ordinary movement and terrain costs

Status: **partial**; scope: **baseline**; ticket: **ZOD-037**; owner area: Fft.Core simulation.

Flat, terrain-cost and restricted adjacent-step policies exist; M1 uses the restricted ordinary policy.

Evidence: [docs/behavior/adjacent-step-implementation.md](../../docs/behavior/adjacent-step-implementation.md), [docs/behavior/terrain-policy-integration.md](../../docs/behavior/terrain-policy-integration.md).

Source entry points (pinned revision above; not full traces): [src/battle/map/move_range.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/move_range.c), [src/battle/units/movement.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/units/movement.c).

Current implementation: [src/Fft.Core/OrdinaryAdjacentStepRange.cs](../../src/Fft.Core/OrdinaryAdjacentStepRange.cs).

Existing bounded checks: [tests/Fft.Core.Tests/OrdinaryAdjacentStepTests.cs](../../tests/Fft.Core.Tests/OrdinaryAdjacentStepTests.cs), [tests/Fft.Core.Tests/TerrainAwareMovementRangeTests.cs](../../tests/Fft.Core.Tests/TerrainAwareMovementRangeTests.cs).

Manual checkpoint (pending-original-observation): Compare legal destinations and chosen paths for flat and elevation cases.

Remaining gaps:

- Full slopes, horizontal jumps, ceilings, water, multi-layer transitions and special movement modes remain open.

Next action: Trace one movement family with callers and regression vectors.

### COV-SIM-004 — Occupancy, allegiance, mounts and special movement

Status: **absent**; scope: **baseline**; ticket: **ZOD-037**; owner area: Fft.Core simulation.

M1 blocks other-unit occupancy in its supported domain; no general allegiance/mount/fly/teleport rule set exists.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/battle/map/move_range.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/move_range.c), [include/fft/move.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/move.h).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Observe ally/enemy traversal, riding and one selected special movement mode.

Remaining gaps:

- Mode-specific movement and stopping rules, rider/mount state coupling and teleport outcomes need separate traces.

Next action: Break ZOD-037 into separate ordinary-geometry and special-mode children.

### COV-SIM-005 — Targeting, hit/evasion and immediate sword damage

Status: **partial**; scope: **baseline**; ticket: **ZOD-038**; owner area: Fft.Core simulation.

One range-1 neutral empty-proc sword with explicit effective stats, neutral zodiac and no equipment evade is executable.

Evidence: [docs/behavior/bounded-sword-implementation.md](../../docs/behavior/bounded-sword-implementation.md).

Source entry points (pinned revision above; not full traces): [src/battle/combat/targets.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/targets.c), [src/battle/combat/resolution.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/resolution.c), [src/battle/combat/formula_core.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/formula_core.c).

Current implementation: [src/Fft.Core/AttackBattleSimulator.cs](../../src/Fft.Core/AttackBattleSimulator.cs), [src/Fft.Core/PhysicalWeaponEvasion.cs](../../src/Fft.Core/PhysicalWeaponEvasion.cs).

Existing bounded checks: [tests/Fft.Core.Tests/AttackBattleSimulatorTests.cs](../../tests/Fft.Core.Tests/AttackBattleSimulatorTests.cs), [tests/Fft.Core.Tests/AttackFixtureV3Tests.cs](../../tests/Fft.Core.Tests/AttackFixtureV3Tests.cs).

Manual checkpoint (pending-original-observation): Compare controlled original hit/miss/critical/damage and resulting facing/HP.

Remaining gaps:

- Other weapons, affinities, equipment evade, zodiac interactions, line of sight and area targets remain open.

Next action: Choose one additional action/target family under ZOD-038.

### COV-SIM-006 — Abilities, magic, recovery and action-formula catalog

Status: **absent**; scope: **baseline**; ticket: **ZOD-038**; owner area: Fft.Core simulation.

Metadata models/imports do not execute the original action catalog.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/battle/combat/formulas.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/formulas.c), [include/fft/formula.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/formula.h), [include/fft/ability.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/ability.h).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Compare one selected original ability with exact target/stats/RNG preconditions.

Remaining gaps:

- Formula and action-family enumeration, arithmetic envelopes, MP costs, target rules and effects are not implemented.

Next action: Inventory formula families before translating one bounded ability.

### COV-SIM-007 — Status durations, triggers, reactions and supports

Status: **absent**; scope: **baseline**; ticket: **ZOD-036**; owner area: Fft.Core simulation.

Only resulting Healthy/Critical/Dead projection exists in the sword slice.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [include/fft/status.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/status.h), [include/fft/reaction.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/reaction.h), [src/battle/turn/clock.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/turn/clock.c), [src/battle/combat/resolution.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/resolution.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Observe one status lifecycle and one reaction/support interaction.

Remaining gaps:

- Full statuses, timers, charge interactions and trigger ordering need independent traces.

Next action: Create a child for a single complete status lifecycle.

### COV-SIM-008 — Original knockback, KO scheduling, revival and pickups

Status: **absent**; scope: **baseline**; ticket: **ZOD-036**; owner area: Fft.Core simulation.

M1 stops at first KO; original knockback and post-KO lifecycle are unsupported.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/battle/combat/resolution.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/resolution.c), [src/battle/combat/pickups.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/pickups.c), [include/fft/crystal.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/crystal.h).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Record original knockback eligibility/destination and KO-to-crystal/treasure or revival behavior.

Remaining gaps:

- Eligibility/critical rolls, collision/fall, countdown ordering, revival and pickup effects remain open.

Next action: Trace original behavior first; keep the planned knockback modification in COV-PRO-003.

### COV-SIM-009 — Enemy, ally and automatic-battle AI

Status: **absent**; scope: **baseline**; ticket: **ZOD-038**; owner area: Fft.Core simulation.

Playable sample controls both units locally; no AI policy executes.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/battle/ai/turn.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/ai/turn.c), [src/battle/ai/evaluation.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/ai/evaluation.c), [include/fft/ai.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/ai.h).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Observe selected mandatory AI turns and distinguish legal choices from exact policy matching.

Remaining gaps:

- Decision families, scoring, tie/RNG ordering and special unit behavior remain unimplemented.

Next action: Select one reproducible AI decision and its required action domain.

### COV-SIM-010 — XP, JP, growth, learned abilities and job unlocks

Status: **absent**; scope: **baseline**; ticket: **ZOD-039**; owner area: Fft.Core simulation.

Job/ability metadata and base-job setup do not implement persistent progression.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/main/job_progress.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/job_progress.c), [src/main/unit_stats.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/unit_stats.c), [src/battle/combat/outcomes.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/outcomes.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Compare one award/level/job-unlock chain and roster persistence.

Remaining gaps:

- Growth arithmetic, awards, spillover, learning costs, unlock requirements and level changes need traces.

Next action: Split progression into award, learning and stat-rebuild children.

### COV-SIM-011 — Original victory/loss, rewards and post-battle party changes

Status: **absent**; scope: **baseline**; ticket: **ZOD-041**; owner area: Fft.Core simulation.

Synthetic first-KO victory is invented and cannot stand in for original battle conditions.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/battle/entry.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/entry.c), [src/event/require/conditions.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/conditions.c), [src/event/require/war_trophies.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/war_trophies.c), [src/event/require/party_changes.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/party_changes.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Complete a selected original battle objective and compare rewards/party/world transitions.

Remaining gaps:

- Original objective/event predicates, failure paths, trophies and recruitment/loss outcomes are absent.

Next action: Inventory one event-to-battle-to-results path.

## Catalog

### COV-CAT-001 — Jobs, skillsets and ability metadata

Status: **partial**; scope: **baseline**; ticket: **ZOD-039**; owner area: Fft.Content definitions and offline import.

Typed metadata, reference validation and selected Squire-family original import exist.

Evidence: [docs/behavior/original-metadata-import.md](../../docs/behavior/original-metadata-import.md), [docs/architecture/zod-014-typed-definitions.md](../../docs/architecture/zod-014-typed-definitions.md).

Source entry points (pinned revision above; not full traces): [include/fft/job.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/job.h), [include/fft/skillset.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/skillset.h), [src/main/party.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/party.c).

Current implementation: [src/Fft.Content/Definitions/JobDefinition.cs](../../src/Fft.Content/Definitions/JobDefinition.cs), [src/Fft.AssetTool/OriginalMetadataRecords.cs](../../src/Fft.AssetTool/OriginalMetadataRecords.cs).

Existing bounded checks: [tests/Fft.AssetTool.Tests/OriginalMetadataRecordsTests.cs](../../tests/Fft.AssetTool.Tests/OriginalMetadataRecordsTests.cs), [tests/Fft.Content.Tests/Definitions/DefinitionTests.cs](../../tests/Fft.Content.Tests/Definitions/DefinitionTests.cs).

Manual checkpoint (pending-original-observation): Inspect one original job/ability menu against imported records.

Remaining gaps:

- Full catalog rows and field-to-runtime meaning are not enumerated; imported metadata does not imply executable effects.

Next action: Expand one record family with full provenance and unsupported-field diagnostics.

### COV-CAT-002 — Items, equipment and effective combat stats

Status: **partial**; scope: **baseline**; ticket: **ZOD-039**; owner area: Fft.Content definitions and offline import.

Selected sword/Attack import and schema-2 raw/effective evade representation exist; full equipment stat derivation is absent.

Evidence: [docs/behavior/retail-sword-representation.md](../../docs/behavior/retail-sword-representation.md), [docs/behavior/base-job-battle-setup.md](../../docs/behavior/base-job-battle-setup.md).

Source entry points (pinned revision above; not full traces): [include/fft/item.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/item.h), [src/main/unit_loadout.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/unit_loadout.c), [src/main/unit_stats.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/unit_stats.c).

Current implementation: [src/Fft.AssetTool/OriginalSwordRecords.cs](../../src/Fft.AssetTool/OriginalSwordRecords.cs), [src/Fft.Content/BattleSetup/AttackBattleSetup.cs](../../src/Fft.Content/BattleSetup/AttackBattleSetup.cs).

Existing bounded checks: [tests/Fft.AssetTool.Tests/OriginalSwordRecordsTests.cs](../../tests/Fft.AssetTool.Tests/OriginalSwordRecordsTests.cs), [tests/Fft.Content.Tests/AttackBattleSetupTests.cs](../../tests/Fft.Content.Tests/AttackBattleSetupTests.cs).

Manual checkpoint (pending-original-observation): Compare selected equipment fields, legal loadouts and resulting displayed/effective stats.

Remaining gaps:

- Remaining item categories, equip restrictions, two-handed/dual wield, affinities and complete stat rebuilding are absent.

Next action: Separate raw import completeness from effective-stat implementation.

### COV-CAT-003 — Characters, monsters, party records and ENTD encounters

Status: **absent**; scope: **baseline**; ticket: **ZOD-039**; owner area: Fft.Content definitions and offline import.

No general original party/ENTD definition-to-runtime path; sample units are invented.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [include/fft/unit_party.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/unit_party.h), [include/fft/unit_entd.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/unit_entd.h), [src/main/encounter.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/encounter.c), [src/main/party.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/party.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Inspect a selected encounter roster and its generated runtime units.

Remaining gaps:

- Record inventory, flags, growth/randomization, allegiances and persistent/battle projection are unresolved.

Next action: Trace one ordinary ENTD record and required party/runtime inputs.

### COV-CAT-004 — Maps, terrain and scene definitions

Status: **partial**; scope: **baseline**; ticket: **ZOD-039**; owner area: Fft.Content definitions and offline import.

One frozen MAP003 base scene and both native terrain layers can be prepared/loaded; combat remains restricted to synthetic height-10 terrain.

Evidence: [docs/development/prepared-map-slice.md](../../docs/development/prepared-map-slice.md), [docs/architecture/zod-029-map003-source-map.md](../../docs/architecture/zod-029-map003-source-map.md).

Source entry points (pinned revision above; not full traces): [include/fft/battle_map.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/battle_map.h), [src/battle/map/terrain.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/terrain.c), [src/battle/map/gns.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/gns.c).

Current implementation: [src/Fft.AssetTool/OriginalBattleMap.cs](../../src/Fft.AssetTool/OriginalBattleMap.cs), [src/Fft.Content/PreparedBattleMap.cs](../../src/Fft.Content/PreparedBattleMap.cs).

Existing bounded checks: [tests/Fft.AssetTool.Tests/OriginalBattleMapTests.cs](../../tests/Fft.AssetTool.Tests/OriginalBattleMapTests.cs), [tests/Fft.Content.Tests/PreparedBattleMapTests.cs](../../tests/Fft.Content.Tests/PreparedBattleMapTests.cs).

Manual checkpoint (pending-original-observation): Compare terrain alignment and selected day/weather variants with original captures.

Remaining gaps:

- Complete map/variant inventory and native-terrain combat support are not established.

Next action: Enumerate required map variants separately from geometry rendering.

### COV-CAT-005 — Dialogue, names, descriptions and localized text

Status: **absent**; scope: **baseline**; ticket: **ZOD-039**; owner area: Fft.Content definitions and offline import.

Synthetic TinyFont and labels do not provide original text banks, encoding or fonts.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [include/fft/text.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/text.h), [src/lib/event/dialog/banks.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/dialog/banks.c), [src/lib/menu/text/entries.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/menu/text/entries.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Compare one title/menu/dialogue string, line break and control-code behavior.

Remaining gaps:

- US encoding/control-code map, compressed text conversion, bank references and glyph resources need inventory.

Next action: Choose one original text-bank decode and prepared text contract.

### COV-CAT-006 — Stores, loot, recruit and world-content tables

Status: **absent**; scope: **baseline**; ticket: **ZOD-039**; owner area: Fft.Content definitions and offline import.

No shared validated catalog for shops, encounters, propositions or rewards.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/world/shop/stock.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/shop/stock.c), [src/world/shop/soldier_office.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/shop/soldier_office.c), [src/wldcore/proposition/data.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/proposition/data.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Inspect one town's available stock/recruits/propositions at a known story state.

Remaining gaps:

- Required table records, conditional availability and cross-references remain unenumerated.

Next action: Inventory dependencies of the first campaign loop.

## Campaign

### COV-CAM-001 — New game, name/birthday, initial party and inventory

Status: **absent**; scope: **baseline**; ticket: **ZOD-041**; owner area: FFT campaign/application.

Bootstrap and synthetic Restart do not create an original campaign.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/main/new_game.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/new_game.c), [src/open/new_game.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/open/new_game.c), [src/open/title.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/open/title.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Start a new original game and record name/birthday, initial resources and first transition.

Remaining gaps:

- Initial campaign state and source-driven roster generation/menus are absent.

Next action: Define the smallest new-game state/use-case child.

### COV-CAM-002 — Main-story progression, flags and required encounter graph

Status: **absent**; scope: **baseline**; ticket: **ZOD-041**; owner area: FFT campaign/application.

No campaign graph or chapter progression is implemented.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [include/fft/script_variable.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/script_variable.h), [src/wldcore/map/location_script.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/map/location_script.c), [src/lib/event/script/scenario.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/scenario.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Track mandatory story transitions through ending with chapter gates and failure/retry branches.

Remaining gaps:

- Exact mandatory node/edge enumeration is unknown; no battle/event count is asserted.

Next action: Inventory stable encounter/event IDs and prerequisites without dumping scripts into Git.

### COV-CAM-003 — World routes, travel/calendar and encounter selection

Status: **absent**; scope: **baseline**; ticket: **ZOD-041**; owner area: FFT campaign/application.

No world-map navigation, date advancement or random encounter runtime.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/wldcore/map/routes.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/map/routes.c), [src/wldcore/calendar_advance_days.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/calendar_advance_days.c), [src/main/encounter.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/encounter.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Travel one known route and compare date, flags, selected encounter and return location.

Remaining gaps:

- Route unlocks, travel ordering, random/rare encounter eligibility and RNG consumers remain open.

Next action: Trace one location-to-encounter round trip.

### COV-CAM-004 — Battle deployment, event triggers and world return

Status: **absent**; scope: **baseline**; ticket: **ZOD-041**; owner area: FFT campaign/application.

Only authored two-unit start/first-KO result exists; it is not an original encounter.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/battle/entry.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/entry.c), [include/fft/deploy.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/deploy.h), [src/battle/event/frame.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/frame.c), [src/wldcore/map/event_transition.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/map/event_transition.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Enter one original encounter, deploy, run its first event, finish and return to world.

Remaining gaps:

- Deployment rules, event barriers/triggers, objective setup and return-state transfer are absent.

Next action: Bound the first event-to-battle-to-world child by exact prerequisites.

### COV-CAM-005 — Optional recruitment and side-event chains

Status: **absent**; scope: **baseline**; ticket: **ZOD-041**; owner area: FFT campaign/application.

No optional campaign content is implemented.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [include/fft/script_variable.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/script_variable.h), [include/fft/location.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/location.h).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Record prerequisite flags and branches for selected Beowulf/Reis, Nelveska and Cloud-related paths.

Remaining gaps:

- Header anchors identify candidate chains, not a complete optional-event catalog; all other reachable optional paths remain to enumerate.

Next action: Extract an edition-scoped optional event/flag index as a separate bounded inventory.

### COV-CAM-006 — Deep Dungeon floors, exits and special encounter progression

Status: **absent**; scope: **baseline**; ticket: **ZOD-041**; owner area: FFT campaign/application.

No dungeon selection, hidden-exit or dungeon encounter progression.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [include/fft/location.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/location.h), [include/fft/script_variable.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/script_variable.h), [src/wldcore/map/routes.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/map/routes.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Observe one floor unlock/entry/exit sequence and completion flags.

Remaining gaps:

- Exact floor/encounter/resource mapping and special visibility/exit rules need traces; no floor count is asserted.

Next action: Inventory the dungeon graph and its simulation/presentation requirements.

### COV-CAM-007 — Bars, rumors, propositions and discoveries

Status: **absent**; scope: **baseline**; ticket: **ZOD-041**; owner area: FFT campaign/application.

No proposition assignment, elapsed days, reports or discovery state.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/wldcore/bar.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/bar.c), [src/wldcore/proposition/dispatch.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/proposition/dispatch.c), [src/wldcore/proposition/results.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/proposition/results.c), [src/wldcore/brave_story/collections.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/brave_story/collections.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Assign one proposition, advance days, collect its report/reward and verify discovery persistence.

Remaining gaps:

- Complete rumor/proposition availability and reward/quirk rules are unimplemented.

Next action: Trace a single dispatch-to-report lifecycle with the retail quirk retained.

### COV-CAM-008 — Monster breeding, poaching and Fur Shop

Status: **absent**; scope: **baseline**; ticket: **ZOD-041**; owner area: FFT campaign/application.

No persistent monster breeding or poached-stock lifecycle.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/world/breeding.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/breeding.c), [src/world/shop/fur.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/shop/fur.c), [include/fft/item.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/item.h).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Observe an egg/hatch or poach-to-stock transaction and save/continue.

Remaining gaps:

- Breeding timing/RNG, eligibility, poach tables and stock transactions remain open.

Next action: Create distinct simulation and campaign-state children for the selected lifecycle.

### COV-CAM-009 — Ending, credits and final epilogue transition

Status: **absent**; scope: **baseline**; ticket: **ZOD-041**; owner area: FFT campaign/application.

No original ending path, movie/audio or final scene execution.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/open/ending.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/open/ending.c), [src/main/open_overlay.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/open_overlay.c), [src/battle/entry.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/entry.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Complete the ending/credits and subsequent epilogue, including skip/timing behavior where supported.

Remaining gaps:

- Cross-program transition, required resources and completion flags need an exact path inventory.

Next action: Record the final acceptance route with source anchors and prepared resource dependencies.

### COV-CAM-010 — Event/script decode, execution and scheduling

Status: **partial**; scope: **baseline**; ticket: **ZOD-040**; owner area: FFT campaign/application.

ZOD-040 now maps script families and one source-derived synthetic conditional event; no managed original-event decoder or interpreter executes.

Evidence: [docs/architecture/zod-040-event-script-investigation.md](../../docs/architecture/zod-040-event-script-investigation.md).

Source entry points (pinned revision above; not full traces): [src/battle/event/interpreter.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/interpreter.c), [src/world/event/interpreter.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/event/interpreter.c), [src/lib/event/script/control.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/control.c), [src/wldcore/map/location_script.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/map/location_script.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Capture one controlled branch/dialogue event with explicit initial state and timing.

Remaining gaps:

- Real event activation/completion, text boundaries, full scheduling/arithmetic and independent original semantics remain unverified.

Next action: Implement ZOD-064's bounded decoder, then ZOD-065's deterministic conditional-event executor; trace real activation/completion in ZOD-066.

Bounded follow-ups: ZOD-064, ZOD-065, ZOD-066.

## Screens

### COV-UI-001 — Opening/title, New Game/Continue and name/birthday screens

Status: **absent**; scope: **baseline**; ticket: **ZOD-042**; owner area: Fft.Client presentation and application commands.

No original game-flow shell; no-argument client prints a bootstrap line.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/open/title.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/open/title.c), [src/open/prologue.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/open/prologue.c), [src/world/name_entry.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/name_entry.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Navigate title/new game/continue, confirm/cancel name and birthday.

Remaining gaps:

- Original flow, assets, focus, input semantics and Windows presentation design are absent.

Next action: Inventory commands and required screen transitions before layout work.

### COV-UI-002 — Battle command menus, selection, targeting and previews

Status: **partial**; scope: **baseline**; ticket: **ZOD-042**; owner area: Fft.Client presentation and application commands.

Synthetic two-unit keyboard/mouse UI supports select/move/attack/wait/advance/restart and legal highlights.

Evidence: [docs/development/playable-battle.md](../../docs/development/playable-battle.md).

Source entry points (pinned revision above; not full traces): [src/battle/turn/controller.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/turn/controller.c), [src/battle/menu/ability_choices.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/menu/ability_choices.c), [src/lib/menu/unit_view/preview_bonuses.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/menu/unit_view/preview_bonuses.c).

Current implementation: [src/Fft.Client/Presentation/BattleInteraction.cs](../../src/Fft.Client/Presentation/BattleInteraction.cs), [src/Fft.Client/Presentation/BattleUiLayout.cs](../../src/Fft.Client/Presentation/BattleUiLayout.cs).

Existing bounded checks: [tests/Fft.Client.Tests/BattleInteractionTests.cs](../../tests/Fft.Client.Tests/BattleInteractionTests.cs), [tests/Fft.Client.Tests/BattleUiInputTests.cs](../../tests/Fft.Client.Tests/BattleUiInputTests.cs).

Manual checkpoint (pending-original-observation): Compare original menu flow/previews and separately test physical input, focus and DPI on Windows.

Remaining gaps:

- Original battle menus, full previews/AT list/status panels and wider native layout are absent.

Next action: Grow supported commands through the same simulation boundary; coordinate enhanced layout with COV-PRO-002.

### COV-UI-003 — Formation, roster, equipment, jobs and learned-ability menus

Status: **absent**; scope: **baseline**; ticket: **ZOD-042**; owner area: Fft.Client presentation and application commands.

No interactive persistent party/job/equipment workflow.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/world/formation/screen.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/formation/screen.c), [src/world/formation/equipment.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/formation/equipment.c), [src/world/formation/abilities.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/formation/abilities.c), [src/event/jobstts/entry.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/jobstts/entry.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Inspect/change a job, learn/equip an ability and alter equipment, then re-enter a battle.

Remaining gaps:

- Stateful use cases, UI assets, sorting, restrictions and navigation remain absent.

Next action: Start with one validated party/job/equipment operation needed by a campaign child.

### COV-UI-004 — World location menus, shops, fitting, Soldier Office and bars

Status: **absent**; scope: **baseline**; ticket: **ZOD-042**; owner area: Fft.Client presentation and application commands.

No location commerce/rumor/proposition UI.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/world/shop/screen.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/shop/screen.c), [src/world/shop/fitting.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/shop/fitting.c), [src/world/shop/soldier_office.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/shop/soldier_office.c), [src/wldcore/bar.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/bar.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Complete one buy/sell/fitting/recruit or bar transaction with cancellation and insufficient funds.

Remaining gaps:

- All menu workflows, prices/stock/eligibility and persistent inventory integration remain absent.

Next action: Separate transaction validation/use cases from UI implementation.

### COV-UI-005 — Brave Story, help/tutorial, status and result screens

Status: **absent**; scope: **baseline**; ticket: **ZOD-042**; owner area: Fft.Client presentation and application commands.

No original records/tutorial/help/result interface.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/wldcore/brave_story/menu.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/brave_story/menu.c), [src/wldcore/tutorial_menus.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/tutorial_menus.c), [src/world/tutorial.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/tutorial.c), [src/event/require/status_panel.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/status_panel.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Open each required screen at a selected known campaign state and verify navigation/text.

Remaining gaps:

- Exact screen/subscreen and help topic inventory is unknown; after-battle results need domain integration.

Next action: Create a screen/operation inventory with text and asset dependencies.

### COV-UI-006 — Windows Save/Load interface and original save availability

Status: **absent**; scope: **platform-replacement**; ticket: **ZOD-042**; owner area: Fft.Client presentation and application commands.

Bounded file API/headless proof exists; no player-facing save selection, metadata or load flow.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/world/card_save.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/card_save.c), [include/fft/save.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/save.h), [src/event/card/entry.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/card/entry.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): At legal original save locations, save/continue through Windows UI and test overwrite/cancel/failure recovery.

Remaining gaps:

- Original availability/checkpoints need tracing; Windows slot/file location, metadata, overwrite, navigation and recovery policy are undecided.

Next action: Add a player-facing Save/Load child with ZOD-053 campaign-state prerequisites.

Bounded follow-ups: ZOD-067.

### COV-UI-007 — Options, text/input settings and settings persistence

Status: **absent**; scope: **baseline**; ticket: **ZOD-042**; owner area: Fft.Client presentation and application commands.

Host controls are fixed; no original options screen or persisted user settings.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/world/options.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/options.c), [src/event/option/game_options.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/option/game_options.c), [include/fft/option.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/option.h).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Change a supported option, cancel/confirm, restart and verify its selected persistence scope.

Remaining gaps:

- Per-campaign versus per-user settings and exact original option behavior remain to classify.

Next action: Trace option effects and decide which Windows settings live outside campaign saves.

### COV-UI-008 — Edition eligibility of sound novels, music/test menus and unused paths

Status: **eligibility-unresolved**; scope: **edition-audit**; ticket: **ZOD-045**; owner area: Fft.Client presentation and application commands.

Source presence is documented; reachability and required resources in SCUS-94221 have not been established here.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/wldcore/sound_novel/menus.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/sound_novel/menus.c), [src/open/music_test.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/open/music_test.c), [src/open/title.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/open/title.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Verify reachable title/treasure menus in the target edition before classifying any candidate as required or intentionally unused.

Remaining gaps:

- Code in the decomp does not prove a reachable baseline feature; edition gates and assets need evidence.

Next action: Create a US reachability/exclusion audit; retain unresolved status until evidence supports exclusion.

## Presentation

### COV-PRE-001 — Map rendering, texture palettes, lighting and transparency

Status: **partial**; scope: **baseline**; ticket: **ZOD-033**; owner area: Fft.Client platform adapters.

Fixed prepared MAP003/Chocobo scene renders with declared neutral-lighting and modern depth/cutout approximations.

Evidence: [docs/development/prepared-scene.md](../../docs/development/prepared-scene.md).

Source entry points (pinned revision above; not full traces): [src/battle/map/runtime.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/runtime.c), [src/battle/map/light.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/light.c), [src/battle/map/palettes.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/palettes.c).

Current implementation: [src/Fft.Client/Presentation/PreparedSceneGeometry.cs](../../src/Fft.Client/Presentation/PreparedSceneGeometry.cs), [src/Fft.Client/Presentation/PreparedSceneWindow.cs](../../src/Fft.Client/Presentation/PreparedSceneWindow.cs).

Existing bounded checks: [tests/Fft.Client.Tests/PreparedSceneGeometryTests.cs](../../tests/Fft.Client.Tests/PreparedSceneGeometryTests.cs).

Manual checkpoint (workstation-observed-original-comparison-pending): Programmed local fixed-scene checks passed; an independent original comparison remains pending.

Remaining gaps:

- Independent original visual equivalence, PS1 blend/order effects and other variants are pending.

Next action: Establish ZOD-026 oracle/capture evidence before ZOD-033 comparison thresholds.

### COV-PRE-002 — Unit sprites, SHP/SEQ animation and battle poses

Status: **partial**; scope: **baseline**; ticket: **ZOD-044**; owner area: Fft.Client platform adapters.

A selected six-piece held Chocobo pose is prepared/loaded/rendered; no general sequence playback.

Evidence: [docs/development/prepared-sprite-pose.md](../../docs/development/prepared-sprite-pose.md), [docs/architecture/zod-027-chocobo-idle-pose.md](../../docs/architecture/zod-027-chocobo-idle-pose.md).

Source entry points (pinned revision above; not full traces): [include/fft/animation.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/animation.h), [src/battle/units/animation_policy.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/units/animation_policy.c), [src/battle/units/animation_motion.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/units/animation_motion.c).

Current implementation: [src/Fft.AssetTool/OrdinaryUnitPoseDecoder.cs](../../src/Fft.AssetTool/OrdinaryUnitPoseDecoder.cs), [src/Fft.Content/PreparedSpritePose.cs](../../src/Fft.Content/PreparedSpritePose.cs).

Existing bounded checks: [tests/Fft.AssetTool.Tests/OrdinaryUnitPoseTests.cs](../../tests/Fft.AssetTool.Tests/OrdinaryUnitPoseTests.cs), [tests/Fft.Content.Tests/PreparedSpritePoseTests.cs](../../tests/Fft.Content.Tests/PreparedSpritePoseTests.cs).

Manual checkpoint (pending-original-observation): Compare selected pose/palette/piece order and later animation timing against original capture.

Remaining gaps:

- Other units/poses, weapon animations, SEQ control flow, animation timing and special sprite families remain open.

Next action: Create a bounded multi-frame sequence playback investigation under ZOD-044.

### COV-PRE-003 — Effects, projectiles, particles, shadows and camera choreography

Status: **absent**; scope: **baseline**; ticket: **ZOD-044**; owner area: Fft.Client platform adapters.

No original effect playback or camera/action animation system.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/battle/effects/runtime.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/effects/runtime.c), [src/battle/effects/playback.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/effects/playback.c), [src/battle/effects/projectile.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/effects/projectile.c), [src/battle/map/camera_control.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/camera_control.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Compare one selected original action effect, shadows and camera transitions at fixed checkpoints.

Remaining gaps:

- Data versus native EFFECT families, timing, transparency, projection and camera semantics need separate inventory.

Next action: Choose a data-only effect family after source/resource classification.

### COV-PRE-004 — Original UI graphics, fonts, portraits and dialogue presentation

Status: **absent**; scope: **baseline**; ticket: **ZOD-044**; owner area: Fft.Client platform adapters.

TinyFont and invented artwork prove the host only.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/world/text/font_rendering.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/text/font_rendering.c), [src/lib/event/dialog/typewriter.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/dialog/typewriter.c), [src/lib/menu/unit_view/portraits.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/menu/unit_view/portraits.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Compare original glyphs, menu/dialogue layout, portraits, typewriter timing and clipping.

Remaining gaps:

- Original resources, glyph decoding, portrait variants and text control semantics remain unprepared.

Next action: Choose one text/font resource and one screen for the first prepared UI slice.

### COV-PRE-005 — Music, sound effects, samples, loops and transitions

Status: **absent**; scope: **baseline**; ticket: **ZOD-043**; owner area: Fft.Client platform adapters.

MonoGame's packaged native audio dependency is not an implemented FFT audio system.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/lib/suzuki/smd_interpreter.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/suzuki/smd_interpreter.c), [src/lib/suzuki/sfx.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/suzuki/sfx.c), [src/main/sound_requests.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/sound_requests.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Compare one prepared music/SFX path, loop point, volume and scene transition with original recordings.

Remaining gaps:

- SMD/sample/SFX decoding and scheduling, backend choice and required audio inventory remain open.

Next action: Start ZOD-043 with a bounded format/backend investigation and synthetic fixtures.

### COV-PRE-006 — Movies, streamed XA audio and opening/ending synchronization

Status: **absent**; scope: **baseline**; ticket: **ZOD-044**; owner area: Fft.Client platform adapters.

No prepared movie/streamed-audio player.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [include/fft/movie.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/movie.h), [src/open/ending.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/open/ending.c), [src/main/cd_read.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/cd_read.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Observe one opening or ending stream with skip, duration and audio/video alignment.

Remaining gaps:

- Sector framing, MDEC/VLC/video/audio conversion, seek/skip and all required streams remain unsupported.

Next action: Split mixed-sector decode from prepared-stream playback; keep BIN access offline.

### COV-PRE-007 — Windows graphics/input host and native interaction

Status: **partial**; scope: **platform-replacement**; ticket: **ZOD-061**; owner area: Fft.Client platform adapters.

Synthetic and fixed prepared hosts run on Windows; bounded mouse/keyboard routing, resize mapping and programmed GPU checks exist.

Evidence: [docs/development/windows-synthetic-host.md](../../docs/development/windows-synthetic-host.md), [docs/development/playable-battle.md](../../docs/development/playable-battle.md).

Source entry points (pinned revision above; not full traces): [include/fft/input.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/input.h), [src/world/screen/pad_input.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/screen/pad_input.c).

Current implementation: [src/Fft.Client/Presentation/BattleInputLatch.cs](../../src/Fft.Client/Presentation/BattleInputLatch.cs), [src/Fft.Client/Presentation/SceneLayout.cs](../../src/Fft.Client/Presentation/SceneLayout.cs).

Existing bounded checks: [tests/Fft.Client.Tests/BattleUiInputTests.cs](../../tests/Fft.Client.Tests/BattleUiInputTests.cs), [tests/Fft.Client.Tests/SceneLayoutTests.cs](../../tests/Fft.Client.Tests/SceneLayoutTests.cs).

Manual checkpoint (pending-original-observation): Test physical keyboard/mouse/controller, focus, native drag/resize, mixed-DPI and resource reset on supported Windows hardware.

Remaining gaps:

- Physical timing, controller/remapping/accessibility and mixed-monitor behavior are not fully validated.

Next action: Preserve shared logical commands while expanding device/navigation coverage.

## Assets

### COV-AST-001 — Edition verification and ordinary-file extraction

Status: **partial**; scope: **baseline**; ticket: **ZOD-044**; owner area: Fft.AssetTool preparation and Fft.Content loading.

Verified SCUS-94221 input and ordinary data-file extraction/inventory are implemented.

Evidence: [docs/asset-inventory-poc.md](../../docs/asset-inventory-poc.md).

Source entry points (pinned revision above; not full traces): [target/main.yaml](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/target/main.yaml), [src/main/file_requests.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/file_requests.c).

Current implementation: [src/Fft.AssetTool/Mode2RawImageReader.cs](../../src/Fft.AssetTool/Mode2RawImageReader.cs), [src/Fft.AssetTool/IsoDataFileExtractor.cs](../../src/Fft.AssetTool/IsoDataFileExtractor.cs).

Existing bounded checks: [tests/Fft.AssetTool.Tests/IsoDataFileExtractorTests.cs](../../tests/Fft.AssetTool.Tests/IsoDataFileExtractorTests.cs), [tests/Fft.AssetTool.Cli.Tests/ExtractCommandTests.cs](../../tests/Fft.AssetTool.Cli.Tests/ExtractCommandTests.cs).

Manual checkpoint (pending-original-observation): Repeat local edition verification/import and confirm runtime works with source media unavailable.

Remaining gaps:

- Extraction is not decoding or complete resource-role inventory; mixed XA/STR payloads remain unsupported.

Next action: Assign conversion/load/provenance state to each required resource family.

### COV-AST-002 — Prepared manifests, integrity and disk-only runtime loading

Status: **partial**; scope: **baseline**; ticket: **ZOD-044**; owner area: Fft.AssetTool preparation and Fft.Content loading.

Versioned manifest and hash-verified loose loading exist for selected assets; no universal decoder/package manager.

Evidence: [docs/adr/0003-offline-asset-import-and-loose-runtime-content.md](../../docs/adr/0003-offline-asset-import-and-loose-runtime-content.md).

Source entry points (pinned revision above; not full traces): [src/main/file_loading.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/file_loading.c), [src/battle/map/loading.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/loading.c).

Current implementation: [src/Fft.Content/LooseAssetStore.cs](../../src/Fft.Content/LooseAssetStore.cs), [src/Fft.Content/PreparedAssetManifest.cs](../../src/Fft.Content/PreparedAssetManifest.cs).

Existing bounded checks: [tests/Fft.Content.Tests/PreparedAssetManifestTests.cs](../../tests/Fft.Content.Tests/PreparedAssetManifestTests.cs).

Manual checkpoint (pending-original-observation): Run a selected prepared scene with BIN/reference checkout unavailable and check missing/corrupt dependency diagnostics.

Remaining gaps:

- Complete asset dependency closure and startup selection are not established.

Next action: Record required logical IDs/formats for each chosen scenario.

### COV-AST-003 — Prepared map, sprite, shape and sequence converters

Status: **partial**; scope: **baseline**; ticket: **ZOD-044**; owner area: Fft.AssetTool preparation and Fft.Content loading.

One frozen MAP003 variant and one held Chocobo pose have bounded offline converters.

Evidence: [docs/development/prepared-map-slice.md](../../docs/development/prepared-map-slice.md), [docs/development/prepared-sprite-pose.md](../../docs/development/prepared-sprite-pose.md).

Source entry points (pinned revision above; not full traces): [src/battle/map/gns.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/gns.c), [include/fft/sprite.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/sprite.h), [include/fft/animation.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/animation.h).

Current implementation: [src/Fft.AssetTool.Cli/PrepareMapCommand.cs](../../src/Fft.AssetTool.Cli/PrepareMapCommand.cs), [src/Fft.AssetTool.Cli/PrepareSpritePoseCommand.cs](../../src/Fft.AssetTool.Cli/PrepareSpritePoseCommand.cs).

Existing bounded checks: [tests/Fft.AssetTool.Cli.Tests/PrepareMapCommandTests.cs](../../tests/Fft.AssetTool.Cli.Tests/PrepareMapCommandTests.cs), [tests/Fft.AssetTool.Cli.Tests/PrepareSpritePoseCommandTests.cs](../../tests/Fft.AssetTool.Cli.Tests/PrepareSpritePoseCommandTests.cs).

Manual checkpoint (pending-original-observation): Repeat each selected conversion, compare input/output hashes and inspect independently matched original captures.

Remaining gaps:

- Other map variants, sheets, animation commands and special overlays/effects are outside current converters.

Next action: Expand one family/profile with explicit unsupported inputs and bounded fixtures.

### COV-AST-004 — Remaining fonts, menus, world, effects, audio and mixed streams

Status: **absent**; scope: **baseline**; ticket: **ZOD-044**; owner area: Fft.AssetTool preparation and Fft.Content loading.

No complete prepared format-family pipeline.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/main/file_requests.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/file_requests.c), [src/battle/effects/resources.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/effects/resources.c), [src/world/screen/resources.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/screen/resources.c), [src/lib/event/dialog/backreferences.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/dialog/backreferences.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Check each chosen family for repeatable conversion and actual runtime presentation with BIN absent.

Remaining gaps:

- Per-file role, decoder support, dependencies and byte provenance are unresolved for the remaining families; historical disc counts do not establish support.

Next action: Build per-family resource subcatalogs without committing generated inventories/media.

## Persistence

### COV-SAV-001 — Bounded battle snapshot DTOs, migration and safe file writes

Status: **slice-proven**; scope: **platform-replacement**; ticket: **ZOD-035**; owner area: FFT persistence/application and Windows storage.

Schema 2/profile-content v1 restores 13 command boundaries, active turns, RNG, CT, HP/status and both outcomes; synthetic v1 migration and safe replacement are tested.

Evidence: [docs/development/two-unit-battle-saves.md](../../docs/development/two-unit-battle-saves.md).

Source entry points (pinned revision above; not full traces): [include/fft/save.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/save.h).

Current implementation: [src/Fft.Content/Saves/TwoUnitBattleSave.cs](../../src/Fft.Content/Saves/TwoUnitBattleSave.cs), [src/Fft.Content/Saves/TwoUnitBattleSave.Files.cs](../../src/Fft.Content/Saves/TwoUnitBattleSave.Files.cs).

Existing bounded checks: [tests/Fft.Content.Tests/Saves/TwoUnitBattleSaveTests.cs](../../tests/Fft.Content.Tests/Saves/TwoUnitBattleSaveTests.cs), [tests/Fft.Content.Tests/Saves/TwoUnitBattleSaveFileTests.cs](../../tests/Fft.Content.Tests/Saves/TwoUnitBattleSaveFileTests.cs), [tests/Fft.Client.Tests/BattleSaveCheckTests.cs](../../tests/Fft.Client.Tests/BattleSaveCheckTests.cs).

Manual checkpoint (pending-original-observation): Use the headless save check now; later test player-facing save/continue at original legal campaign checkpoints.

Remaining gaps:

- This is a managed synthetic contract, not original save availability, full campaign state, a memory-card format or save UI.

Next action: Carry its DTO/validation/atomic-write pattern into separately traced campaign persistence.

### COV-SAV-002 — Campaign/party/inventory/world saves and content-version policy

Status: **absent**; scope: **platform-replacement**; ticket: **ZOD-053**; owner area: FFT persistence/application and Windows storage.

No complete campaign serialization or restore.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [include/fft/save.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/save.h), [src/main/save_state.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/save_state.c), [src/world/card_save.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/card_save.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Save/continue at selected legal campaign points and compare party, flags, inventories, calendar and RNG-sensitive behavior.

Remaining gaps:

- Source fields include party, inventory/poached stock, script variables, options and side-content progress. Exact lifetimes, unknown fields and save checkpoints need tracing; establish which RNG streams are saved, rebuilt or reset on load.

Next action: Inventory logical save state/lifetimes before defining campaign DTOs.

Bounded follow-ups: ZOD-067.

### COV-SAV-003 — Windows save storage, metadata, backups and recovery

Status: **absent**; scope: **platform-replacement**; ticket: **ZOD-053**; owner area: FFT persistence/application and Windows storage.

Caller-path atomic saves exist; no storage-location/slot discovery/backup or recovery policy.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/world/card_save.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/card_save.c), [include/fft/save.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/save.h).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Test overwrite/cancel, missing/corrupt files, access errors and recovery without losing the last valid save.

Remaining gaps:

- Per-user paths, metadata, backup rotation, compatible version selection and concurrent writer policy are undecided.

Next action: Coordinate a storage policy child with COV-UI-006; retain original save availability separately.

Bounded follow-ups: ZOD-067.

## Validation

### COV-VAL-001 — Managed source vectors, deterministic replays and scenario regressions

Status: **partial**; scope: **baseline**; ticket: **ZOD-046**; owner area: Development validation.

Movement/clock/sword/scenario/save checks exist; last recorded baseline is 954 passing tests at managed commit 7990b5d.

Evidence: [docs/behavior/fixture-runner.md](../../docs/behavior/fixture-runner.md), [docs/behavior/two-unit-battle-scenario.md](../../docs/behavior/two-unit-battle-scenario.md), [docs/development/two-unit-battle-saves.md](../../docs/development/two-unit-battle-saves.md).

Source entry points (pinned revision above; not full traces): [src/battle/turn/clock.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/turn/clock.c), [src/battle/combat/formulas.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/formulas.c).

Current implementation: [tests/Fft.Core.Tests/Fixtures/MovementFixtureRunner.cs](../../tests/Fft.Core.Tests/Fixtures/MovementFixtureRunner.cs), [tests/Fft.Core.Tests/Fixtures/AttackFixtureRunner.cs](../../tests/Fft.Core.Tests/Fixtures/AttackFixtureRunner.cs).

Existing bounded checks: [tests/Fft.Core.Tests/Fixtures/MovementFixtureRunnerTests.cs](../../tests/Fft.Core.Tests/Fixtures/MovementFixtureRunnerTests.cs), [tests/Fft.Core.Tests/AttackFixtureV3Tests.cs](../../tests/Fft.Core.Tests/AttackFixtureV3Tests.cs).

Manual checkpoint (pending-original-observation): Compare critical supported behavior against independent original/emulator observations, keeping source-derived and synthetic expectations distinct.

Remaining gaps:

- Most original systems lack regressions; passing source-derived tests is not complete original equivalence.

Next action: Add fixture/scenario evidence when each row's supported domain expands.

### COV-VAL-002 — Independent original sprite/scene/audio capture and comparison

Status: **absent**; scope: **baseline**; ticket: **ZOD-026**; owner area: Development validation.

Source-derived hashes and repeatable workstation captures do not establish an independent original oracle.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/battle/units/sprite_display.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/units/sprite_display.c), [src/battle/map/runtime.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/runtime.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Record edition, environment, state, palette, camera, frame/timing, inputs and capture hashes under ignored scratch.

Remaining gaps:

- No independent comparison is claimed by the current prepared-scene proof; ZOD-033 thresholds and manual review remain open.

Next action: Finish the ZOD-026 workflow or record its exact blocker/fallback.

### COV-VAL-003 — Windows/Linux CI and clean-machine launch

Status: **validation-pending**; scope: **platform-replacement**; ticket: **ZOD-034**; owner area: Development validation.

Independent Windows CI, retained Linux/Sonar, TRX/JUnit and private self-contained folder validation are configured; local headless/GPU runs passed.

Evidence: [docs/development/windows-launch-validation.md](../../docs/development/windows-launch-validation.md).

Source entry points (pinned revision above; not full traces): [include/fft/system.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/system.h).

Current implementation: [.circleci/config.yml](../../.circleci/config.yml), [tools/ci/Publish-WindowsClient.ps1](../../tools/ci/Publish-WindowsClient.ps1).

Existing bounded checks: [tools/ci/Publish-WindowsClient.ps1](../../tools/ci/Publish-WindowsClient.ps1).

Manual checkpoint (pending-original-observation): Observe actual remote CI jobs and a clean Windows 11 x64 graphical launch without SDK/original media.

Remaining gaps:

- Remote job results and clean-machine interactive evidence remain pending; this inventory does not close those gates.

Next action: Record job URLs and clean-machine environment/results before marking ZOD-034 complete.

### COV-VAL-004 — Engine independence, immutable definitions and runtime import boundary

Status: **slice-proven**; scope: **platform-replacement**; ticket: **ZOD-009**; owner area: Development validation.

Grid/bounds/RNG contracts are Zodiac-owned; FFT-specific rules and persistence remain separate, with build reference guard and Relay sample.

Evidence: [docs/adr/0004-zodiac-engine-and-fft-game-boundary.md](../../docs/adr/0004-zodiac-engine-and-fft-game-boundary.md), [samples/Relay/README.md](../../samples/Relay/README.md).

Source entry points (pinned revision above; not full traces): [include/fft/unit.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/unit.h).

Current implementation: [Directory.Build.targets](../../Directory.Build.targets), [src/Zodiac.Engine/GridBounds.cs](../../src/Zodiac.Engine/GridBounds.cs), [src/Zodiac.Engine/IDeterministicRandom.cs](../../src/Zodiac.Engine/IDeterministicRandom.cs).

Existing bounded checks: [tests/Fft.Core.Tests/EngineIndependenceTests.cs](../../tests/Fft.Core.Tests/EngineIndependenceTests.cs), [tests/Zodiac.Engine.Tests/RelayScenarioTests.cs](../../tests/Zodiac.Engine.Tests/RelayScenarioTests.cs).

Manual checkpoint (pending-original-observation): Review dependency graph and run prepared/synthetic clients with reference tools/media unavailable.

Remaining gaps:

- Broader reusable engine extraction requires demonstrated seams; this bounded architectural proof says nothing about complete FFT content.

Next action: Preserve the one-way dependency and extract only independently stated invariants.

## Profiles

### COV-PRO-001 — Original baseline, add-on isolation and later Lion War target

Status: **partial**; scope: **extension**; ticket: **ZOD-049**; owner area: FFT compatibility/content composition.

Typed baseline/add-on metadata composition exists; full content/profile isolation and exact Lion War selection are future work.

Evidence: [docs/adr/0002-compatible-baseline-and-extension-profiles.md](../../docs/adr/0002-compatible-baseline-and-extension-profiles.md), [docs/architecture/zod-016-content-packages.md](../../docs/architecture/zod-016-content-packages.md).

Source entry points (pinned revision above; not full traces): [include/fft/job.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/job.h), [include/fft/item.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/item.h).

Current implementation: [src/Fft.Content/ContentPackages/ContentPackage.cs](../../src/Fft.Content/ContentPackages/ContentPackage.cs).

Existing bounded checks: [tests/Fft.Content.Tests/ContentPackages/ContentPackageTests.cs](../../tests/Fft.Content.Tests/ContentPackages/ContentPackageTests.cs).

Manual checkpoint (pending-original-observation): With add-on files present but disabled, compare original-mode results and selected package/version identity.

Remaining gaps:

- Full profile manifests, selected mechanics, package conflicts and exact Lion War revision/content delta are unresolved.

Next action: Complete ZOD-049 before integrating separately selected ZOD-051/052 content.

### COV-PRO-002 — Widescreen, higher resolution/textures and native Windows UI

Status: **planned**; scope: **extension**; ticket: **ZOD-059**; owner area: FFT compatibility/content composition.

Host resize/letterbox is a bounded facility; selectable geometry rendering resolution, widescreen camera/UI and replacements are not delivered.

Evidence: [docs/architecture/overview.md](../../docs/architecture/overview.md), [docs/development/prepared-scene.md](../../docs/development/prepared-scene.md).

Source entry points (pinned revision above; not full traces): [src/battle/map/camera.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/camera.c), [include/fft/render.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/render.h).

Current implementation: [src/Fft.Client/Presentation/SceneLayout.cs](../../src/Fft.Client/Presentation/SceneLayout.cs).

Existing bounded checks: [tests/Fft.Client.Tests/SceneLayoutTests.cs](../../tests/Fft.Client.Tests/SceneLayoutTests.cs).

Manual checkpoint (pending-original-observation): Compare layout, camera, input mapping and unchanged simulation at each explicitly selected presentation option.

Remaining gaps:

- ZOD-059/060/062/063 remain future presentation-profile work. Required Windows interaction is tracked separately by COV-PRE-007 under ZOD-061; resize alone does not establish widescreen or higher-detail rendering.

Next action: Keep aspect, resolution, extensible UI and replacement artwork distinct; coordinate their layouts/navigation with required ZOD-061 interaction.

### COV-PRO-003 — Planned custom knockback and other rule changes

Status: **planned**; scope: **extension**; ticket: **ZOD-050**; owner area: FFT compatibility/content composition.

User intends a redesigned knockback mechanic; no new mechanic or original knockback implementation has been selected here.

Evidence: [docs/behavior/bounded-sword-implementation.md](../../docs/behavior/bounded-sword-implementation.md), [docs/architecture/overview.md](../../docs/architecture/overview.md).

Source entry points (pinned revision above; not full traces): [src/battle/combat/resolution.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/resolution.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Compare original mode and explicitly selected custom profile for the same commands/seed.

Remaining gaps:

- Redesign rules and compatibility selection are unspecified; no silent change to original defaults is accepted.

Next action: Document the original rule first, then specify a separate custom policy/profile.

### COV-PRO-004 — Optional original memory-card import

Status: **planned**; scope: **optional-interoperability**; ticket: **ZOD-053**; owner area: FFT compatibility/content composition.

The Windows save format replaces card storage; original-card interoperability is optional and unscoped.

Evidence: [docs/development/two-unit-battle-saves.md](../../docs/development/two-unit-battle-saves.md).

Source entry points (pinned revision above; not full traces): [include/fft/save.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/save.h), [src/lib/memcard.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/memcard.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): If separately selected later, import a local supported original save with explicit edition/version checks.

Remaining gaps:

- Original parity, packing, card container handling and import scope have not been selected.

Next action: Do not make optional card import a dependency of native Windows Save/Load.

### COV-PRO-005 — Typed content authoring, validation, export and first editor

Status: **partial**; scope: **extension**; ticket: **ZOD-047**; owner area: FFT content authoring and tools.

Typed metadata/sword schemas, import/export and shared validation exist; no interactive job/ability editor.

Evidence: [docs/architecture/zod-016-content-packages.md](../../docs/architecture/zod-016-content-packages.md), [docs/architecture/zod-014-typed-definitions.md](../../docs/architecture/zod-014-typed-definitions.md).

Source entry points (pinned revision above; not full traces): [include/fft/job.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/job.h).

Current implementation: [src/Fft.Content/ContentPackages/ContentPackage.cs](../../src/Fft.Content/ContentPackages/ContentPackage.cs), [src/Fft.AssetTool.Cli/ValidateContentCommand.cs](../../src/Fft.AssetTool.Cli/ValidateContentCommand.cs).

Existing bounded checks: [tests/Fft.Content.Tests/ContentPackages/ContentPackageTests.cs](../../tests/Fft.Content.Tests/ContentPackages/ContentPackageTests.cs).

Manual checkpoint (pending-editor-workflow): Inspect an original job, author a separate supported change, validate/export and use it in the supported battle path.

Remaining gaps:

- Editor UI, draft/undo/unsaved-state handling and general text/graphics/script/audio authoring remain future work.

Next action: ZOD-047 can start its first bounded job/ability editor; broader authoring remains ZOD-048.
