# Baseline coverage entries

Generated from `baseline-coverage.json` by `tools/verification/verify_baseline_coverage.py --write-view`.
Edit the JSON, then regenerate this view. See [the inventory guide](README.md) for status/evidence rules.

Target: **SCUS-94221**. Snapshot: 2026-10-26; managed base `d0f53cbf0f470b4f3a51ba384898558f29cbe1a9`; reference `11464943bbdbd2a0af022d90cb7f10455cf911df`.
These entries are planning scopes, not a feature-completion percentage or a complete record/encounter catalog.

## Simulation

### COV-SIM-001 — PS1 RNG algorithm and gameplay draw ordering

Status: **partial**; scope: **baseline**; ticket: **ZOD-038**; owner area: Zodiac.Framework simulation.

FFT BIOS RNG algorithm/state is implemented with supplied-BIOS vectors; draw ordering is covered only for bounded CT/sword paths.

Evidence: [docs/behavior/bios-rng-verification.md](../../docs/behavior/bios-rng-verification.md), [docs/behavior/rng-state-and-ordering.md](../../docs/behavior/rng-state-and-ordering.md), [docs/architecture/zod-040-event-script-investigation.md](../../docs/architecture/zod-040-event-script-investigation.md).

Source entry points (pinned revision above; not full traces): [include/fft/random.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/random.h), [src/battle/turn/clock.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/turn/clock.c), [src/lib/event/script/control.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/control.c).

Current implementation: [src/Zodiac.Framework/FftBiosRandom.cs](../../src/Zodiac.Framework/FftBiosRandom.cs).

Existing bounded checks: [tests/Zodiac.Framework.Tests/FftBiosRandomTests.cs](../../tests/Zodiac.Framework.Tests/FftBiosRandomTests.cs), [tests/Zodiac.Framework.Tests/RandomReplayRunnerTests.cs](../../tests/Zodiac.Framework.Tests/RandomReplayRunnerTests.cs).

Manual checkpoint (pending-original-observation): Observe controlled original battle inputs and record incoming RNG state/draw order.

Remaining gaps:

- Whole-game consumers and seed lifecycles remain open; the event interpreter uses a separate RNG, whose algorithm/seed/order is not covered by the BIOS proof.

Next action: Extend ordering traces one new rule/caller at a time.

### COV-SIM-002 — CT, readiness, turn choices and AT prediction

Status: **partial**; scope: **baseline**; ticket: **ZOD-036**; owner area: Zodiac.Framework simulation.

Positive-Speed living ordinary units, CT refunds and explicit move/action/Wait are implemented; full scheduler is absent.

Evidence: [docs/behavior/ct-turn-implementation.md](../../docs/behavior/ct-turn-implementation.md).

Source entry points (pinned revision above; not full traces): [src/battle/turn/clock.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/turn/clock.c), [include/fft/turn.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/turn.h).

Current implementation: [src/Zodiac.Framework/TurnCycle.cs](../../src/Zodiac.Framework/TurnCycle.cs), [src/Zodiac.Framework/TurnBattleState.cs](../../src/Zodiac.Framework/TurnBattleState.cs).

Existing bounded checks: [tests/Zodiac.Framework.Tests/TurnCycleTests.cs](../../tests/Zodiac.Framework.Tests/TurnCycleTests.cs), [tests/Zodiac.Framework.Tests/TurnBattleSimulatorTests.cs](../../tests/Zodiac.Framework.Tests/TurnBattleSimulatorTests.cs).

Manual checkpoint (pending-original-observation): Compare readiness, ties, Wait refunds and charged/status turns with original observations.

Remaining gaps:

- Statuses, charge timers, Mimes, presence changes and AT-list prediction remain unsupported.

Next action: Select one status/charge interaction after a source trace.

### COV-SIM-003 — Ordinary movement and terrain costs

Status: **partial**; scope: **baseline**; ticket: **ZOD-037**; owner area: Zodiac.Framework simulation.

Flat, terrain-cost and restricted adjacent-step policies exist; M1 uses the restricted ordinary policy.

Evidence: [docs/behavior/adjacent-step-implementation.md](../../docs/behavior/adjacent-step-implementation.md), [docs/behavior/terrain-policy-integration.md](../../docs/behavior/terrain-policy-integration.md).

Source entry points (pinned revision above; not full traces): [src/battle/map/move_range.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/move_range.c), [src/battle/units/movement.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/units/movement.c).

Current implementation: [src/Zodiac.Framework/OrdinaryAdjacentStepRange.cs](../../src/Zodiac.Framework/OrdinaryAdjacentStepRange.cs).

Existing bounded checks: [tests/Zodiac.Framework.Tests/OrdinaryAdjacentStepTests.cs](../../tests/Zodiac.Framework.Tests/OrdinaryAdjacentStepTests.cs), [tests/Zodiac.Framework.Tests/TerrainAwareMovementRangeTests.cs](../../tests/Zodiac.Framework.Tests/TerrainAwareMovementRangeTests.cs).

Manual checkpoint (pending-original-observation): Compare legal destinations and chosen paths for flat and elevation cases.

Remaining gaps:

- Full slopes, horizontal jumps, ceilings, water, multi-layer transitions and special movement modes remain open.

Next action: Trace one movement family with callers and regression vectors.

### COV-SIM-004 — Occupancy, allegiance, mounts and special movement

Status: **absent**; scope: **baseline**; ticket: **ZOD-037**; owner area: Zodiac.Framework simulation.

M1 blocks other-unit occupancy in its supported domain; no general allegiance/mount/fly/teleport rule set exists.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/battle/map/move_range.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/move_range.c), [include/fft/move.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/move.h).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Observe ally/enemy traversal, riding and one selected special movement mode.

Remaining gaps:

- Mode-specific movement and stopping rules, rider/mount state coupling and teleport outcomes need separate traces.

Next action: Break ZOD-037 into separate ordinary-geometry and special-mode children.

### COV-SIM-005 — Targeting, hit/evasion and immediate sword damage

Status: **partial**; scope: **baseline**; ticket: **ZOD-038**; owner area: Zodiac.Framework simulation.

One range-1 neutral empty-proc sword with explicit effective stats, neutral zodiac and no equipment evade is executable.

Evidence: [docs/behavior/bounded-sword-implementation.md](../../docs/behavior/bounded-sword-implementation.md).

Source entry points (pinned revision above; not full traces): [src/battle/combat/targets.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/targets.c), [src/battle/combat/resolution.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/resolution.c), [src/battle/combat/formula_core.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/formula_core.c).

Current implementation: [src/Zodiac.Framework/AttackBattleSimulator.cs](../../src/Zodiac.Framework/AttackBattleSimulator.cs), [src/Zodiac.Framework/PhysicalWeaponEvasion.cs](../../src/Zodiac.Framework/PhysicalWeaponEvasion.cs).

Existing bounded checks: [tests/Zodiac.Framework.Tests/AttackBattleSimulatorTests.cs](../../tests/Zodiac.Framework.Tests/AttackBattleSimulatorTests.cs), [tests/Zodiac.Framework.Tests/AttackFixtureV3Tests.cs](../../tests/Zodiac.Framework.Tests/AttackFixtureV3Tests.cs).

Manual checkpoint (pending-original-observation): Compare controlled original hit/miss/critical/damage and resulting facing/HP.

Remaining gaps:

- Other weapons, affinities, equipment evade, zodiac interactions, line of sight and area targets remain open.

Next action: Choose one additional action/target family under ZOD-038.

### COV-SIM-006 — Abilities, magic, recovery and action-formula catalog

Status: **absent**; scope: **baseline**; ticket: **ZOD-038**; owner area: Zodiac.Framework simulation.

Metadata models/imports do not execute the original action catalog.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/battle/combat/formulas.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/formulas.c), [include/fft/formula.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/formula.h), [include/fft/ability.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/ability.h).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Compare one selected original ability with exact target/stats/RNG preconditions.

Remaining gaps:

- Formula and action-family enumeration, arithmetic envelopes, MP costs, target rules and effects are not implemented.

Next action: Inventory formula families before translating one bounded ability.

### COV-SIM-007 — Status durations, triggers, reactions and supports

Status: **absent**; scope: **baseline**; ticket: **ZOD-036**; owner area: Zodiac.Framework simulation.

Only resulting Healthy/Critical/Dead projection exists in the sword slice.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [include/fft/status.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/status.h), [include/fft/reaction.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/reaction.h), [src/battle/turn/clock.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/turn/clock.c), [src/battle/combat/resolution.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/resolution.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Observe one status lifecycle and one reaction/support interaction.

Remaining gaps:

- Full statuses, timers, charge interactions and trigger ordering need independent traces.

Next action: Create a child for a single complete status lifecycle.

### COV-SIM-008 — Original knockback, KO scheduling, revival and pickups

Status: **absent**; scope: **baseline**; ticket: **ZOD-036**; owner area: Zodiac.Framework simulation.

M1 stops at first KO; original knockback and post-KO lifecycle are unsupported.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/battle/combat/resolution.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/resolution.c), [src/battle/combat/pickups.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/pickups.c), [include/fft/crystal.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/crystal.h).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Record original knockback eligibility/destination and KO-to-crystal/treasure or revival behavior.

Remaining gaps:

- Eligibility/critical rolls, collision/fall, countdown ordering, revival and pickup effects remain open.

Next action: Trace original behavior first; keep the planned knockback modification in COV-PRO-003.

### COV-SIM-009 — Enemy, ally and automatic-battle AI

Status: **absent**; scope: **baseline**; ticket: **ZOD-038**; owner area: Zodiac.Framework simulation.

Playable sample controls both units locally; no AI policy executes.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/battle/ai/turn.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/ai/turn.c), [src/battle/ai/evaluation.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/ai/evaluation.c), [include/fft/ai.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/ai.h).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Observe selected mandatory AI turns and distinguish legal choices from exact policy matching.

Remaining gaps:

- Decision families, scoring, tie/RNG ordering and special unit behavior remain unimplemented.

Next action: Select one reproducible AI decision and its required action domain.

### COV-SIM-010 — XP, JP, growth, learned abilities and job unlocks

Status: **absent**; scope: **baseline**; ticket: **ZOD-039**; owner area: Zodiac.Framework simulation.

Job/ability metadata and base-job setup do not implement persistent progression.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [src/main/job_progress.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/job_progress.c), [src/main/unit_stats.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/unit_stats.c), [src/battle/combat/outcomes.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/outcomes.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Compare one award/level/job-unlock chain and roster persistence.

Remaining gaps:

- Growth arithmetic, awards, spillover, learning costs, unlock requirements and level changes need traces.

Next action: Split progression into award, learning and stat-rebuild children.

### COV-SIM-011 — Original victory/loss, rewards and post-battle party changes

Status: **absent**; scope: **baseline**; ticket: **ZOD-041**; owner area: Zodiac.Framework simulation.

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

Status: **partial**; scope: **baseline**; ticket: **ZOD-039**; owner area: Zodiac.Content definitions and offline import.

Typed metadata, reference validation and selected Squire-family original import exist.

Evidence: [docs/behavior/original-metadata-import.md](../../docs/behavior/original-metadata-import.md), [docs/architecture/zod-014-typed-definitions.md](../../docs/architecture/zod-014-typed-definitions.md).

Source entry points (pinned revision above; not full traces): [include/fft/job.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/job.h), [include/fft/skillset.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/skillset.h), [src/main/party.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/party.c).

Current implementation: [src/Zodiac.Content/Definitions/JobDefinition.cs](../../src/Zodiac.Content/Definitions/JobDefinition.cs), [src/Zodiac.AssetTool/OriginalMetadataRecords.cs](../../src/Zodiac.AssetTool/OriginalMetadataRecords.cs).

Existing bounded checks: [tests/Zodiac.AssetTool.Tests/OriginalMetadataRecordsTests.cs](../../tests/Zodiac.AssetTool.Tests/OriginalMetadataRecordsTests.cs), [tests/Zodiac.Content.Tests/Definitions/DefinitionTests.cs](../../tests/Zodiac.Content.Tests/Definitions/DefinitionTests.cs).

Manual checkpoint (pending-original-observation): Inspect one original job/ability menu against imported records.

Remaining gaps:

- Full catalog rows and field-to-runtime meaning are not enumerated; imported metadata does not imply executable effects.

Next action: Expand one record family with full provenance and unsupported-field diagnostics.

### COV-CAT-002 — Items, equipment and effective combat stats

Status: **partial**; scope: **baseline**; ticket: **ZOD-039**; owner area: Zodiac.Content definitions and offline import.

Selected sword/Attack import and schema-2 raw/effective evade representation exist; full equipment stat derivation is absent.

Evidence: [docs/behavior/retail-sword-representation.md](../../docs/behavior/retail-sword-representation.md), [docs/behavior/base-job-battle-setup.md](../../docs/behavior/base-job-battle-setup.md).

Source entry points (pinned revision above; not full traces): [include/fft/item.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/item.h), [src/main/unit_loadout.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/unit_loadout.c), [src/main/unit_stats.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/unit_stats.c).

Current implementation: [src/Zodiac.AssetTool/OriginalSwordRecords.cs](../../src/Zodiac.AssetTool/OriginalSwordRecords.cs), [src/Zodiac.Content/BattleSetup/AttackBattleSetup.cs](../../src/Zodiac.Content/BattleSetup/AttackBattleSetup.cs).

Existing bounded checks: [tests/Zodiac.AssetTool.Tests/OriginalSwordRecordsTests.cs](../../tests/Zodiac.AssetTool.Tests/OriginalSwordRecordsTests.cs), [tests/Zodiac.Content.Tests/AttackBattleSetupTests.cs](../../tests/Zodiac.Content.Tests/AttackBattleSetupTests.cs).

Manual checkpoint (pending-original-observation): Compare selected equipment fields, legal loadouts and resulting displayed/effective stats.

Remaining gaps:

- Remaining item categories, equip restrictions, two-handed/dual wield, affinities and complete stat rebuilding are absent.

Next action: Separate raw import completeness from effective-stat implementation.

### COV-CAT-003 — Characters, monsters, party records and ENTD encounters

Status: **absent**; scope: **baseline**; ticket: **ZOD-039**; owner area: Zodiac.Content definitions and offline import.

No general original party/ENTD definition-to-runtime path; sample units are invented.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [include/fft/unit_party.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/unit_party.h), [include/fft/unit_entd.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/unit_entd.h), [src/main/encounter.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/encounter.c), [src/main/party.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/party.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Inspect a selected encounter roster and its generated runtime units.

Remaining gaps:

- Record inventory, flags, growth/randomization, allegiances and persistent/battle projection are unresolved.

Next action: Trace one ordinary ENTD record and required party/runtime inputs.

### COV-CAT-004 — Maps, terrain and scene definitions

Status: **partial**; scope: **baseline**; ticket: **ZOD-039**; owner area: Zodiac.Content definitions and offline import.

One frozen MAP003 base scene and both native terrain layers can be prepared/loaded; combat remains restricted to synthetic height-10 terrain.

Evidence: [docs/development/prepared-map-slice.md](../../docs/development/prepared-map-slice.md), [docs/architecture/zod-029-map003-source-map.md](../../docs/architecture/zod-029-map003-source-map.md).

Source entry points (pinned revision above; not full traces): [include/fft/battle_map.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/battle_map.h), [src/battle/map/terrain.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/terrain.c), [src/battle/map/gns.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/gns.c).

Current implementation: [src/Zodiac.AssetTool/OriginalBattleMap.cs](../../src/Zodiac.AssetTool/OriginalBattleMap.cs), [src/Zodiac.Content/PreparedBattleMap.cs](../../src/Zodiac.Content/PreparedBattleMap.cs).

Existing bounded checks: [tests/Zodiac.AssetTool.Tests/OriginalBattleMapTests.cs](../../tests/Zodiac.AssetTool.Tests/OriginalBattleMapTests.cs), [tests/Zodiac.Content.Tests/PreparedBattleMapTests.cs](../../tests/Zodiac.Content.Tests/PreparedBattleMapTests.cs).

Manual checkpoint (pending-original-observation): Compare terrain alignment and selected day/weather variants with original captures.

Remaining gaps:

- Complete map/variant inventory and native-terrain combat support are not established.

Next action: Enumerate required map variants separately from geometry rendering.

### COV-CAT-005 — Dialogue, names, descriptions and localized text

Status: **absent**; scope: **baseline**; ticket: **ZOD-039**; owner area: Zodiac.Content definitions and offline import.

Synthetic TinyFont and labels do not provide original text banks, encoding or fonts.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md).

Source entry points (pinned revision above; not full traces): [include/fft/text.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/text.h), [src/lib/event/dialog/banks.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/dialog/banks.c), [src/lib/menu/text/entries.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/menu/text/entries.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): Compare one title/menu/dialogue string, line break and control-code behavior.

Remaining gaps:

- US encoding/control-code map, compressed text conversion, bank references and glyph resources need inventory.

Next action: Choose one original text-bank decode and prepared text contract.

### COV-CAT-006 — Stores, loot, recruit and world-content tables

Status: **absent**; scope: **baseline**; ticket: **ZOD-039**; owner area: Zodiac.Content definitions and offline import.

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

ZOD-064 prepares authored raw/processed blocks; ZOD-065 projects validated Content records into a pure Core-owned conditional-event executor with immutable state/traces, replay and atomic arithmetic/budget rejection. No retail event or scene/campaign lifecycle executes. ZOD-066 traces verified original event 401: fallback activation, all 13 opcode kinds and EventEnd continuation; structural inspection only. ZOD-071 adds a separate non-tutorial speed-one timed profile with immutable waits, explicit ticks, exact resumption and atomic failures; synchronous capability remains unchanged. ZOD-072 selects a scene-owned task/barrier protocol. ZOD-073 implements its pure ledger/single-consumer proof with explicit ordered facts/ticks, stable identities/revisions, terminal tombstones, live scene-wide polling and atomic failures; 17 specification regressions plus 40 boundary cases pass. ZOD-074 integrates E5 classes 4/54/55/56 in a separate BATTLE synthetic profile with F1/conditional operations, shared explicit ticks, atomic VM/ledger rollback, exact combined traces and retained scene tasks across interpreter exit/restart. Old profiles still reject E5. The standalone event API still takes explicit task facts; no original event playback. ZOD-075 selects condition 8's post-readiness logical banner effect: exact reveal/hold/hide/exit-yield, explicit Circle sample, managed authority and atomic composition; its specification remains historical. ZOD-076 implements the post-readiness logical condition-8 driver and atomic adapter, generating managed termination facts from explicit Circle/ticks with retained scene lifetime and full rollback. All 24 contract regressions plus 47 boundary cases pass; original loading, graphics and opcode-78 admission remain absent. ZOD-077 traces verified banner-0 image/palette/geometry and readiness, with summarized hashes and 20 unexecuted authored presentation expectations; selects a prepared RGB8 CPU preview, without implementing rendering/loading or original startup. ZOD-078 prepares condition-8/image-0 artwork from verified local inputs, strictly loads immutable six-piece data and previews accepted effect snapshots with a preloaded RGB8 CPU/Windows host. All 20 authored expectations have regressions; two preparations and two finite Windows capture sets match, with exact CPU/GPU readback and prepared-only runtime paths. Original comparison, parity and loading latency remain open. A source-bound saved-state study now identifies the captured banner as event 4, not common BattleStart event 401. All eight selected frozen states agree on current/cache IDs and a unique full archive payload match after the four-byte loader marker. The interpreter uses saved $fp for command offset: earliest state waits at 731; target banner command 804 has condition 8/hold 60 and the active worker matches the task-56 wait at 807. Scenario-active and correctly decoded packed flags support the scenario path. Two reports repeat; eleven authored verifier regressions pass and all 163 Python tests pass. No emulator/original instruction execution, runtime change or .NET rerun; last full managed count remains 1,494. Event entry/conditional selection, common event-401 activation and complete playback remain open. ZOD-080 now traces bounded opcode-78 admission and selects a distinct event-before-worker transaction. Retained original frames 9001/9002/9003 show parent wait 807 with live worker, wait 807 with cleared worker, then wait 813 with a new condition. Existing effect-first APIs remain unchanged. Twenty-eight authored expectations are unexecuted; ZOD-081 implements typed ordered Core behavior and ZOD-082 then admits the separate authored preloaded profile. No runtime/profile change, emulator run or suite rerun; original loading duration and full event playback remain open. ZOD-081 now implements typed ordered event/banner execution in Zodiac.Framework. All 27 authored Core expectations and 33 additional boundary cases pass, including H+31 task clear/H+32 parent resumption, newborn no-step, shared budgets, post-poll facts, protected identities and atomic late-failure rollback. Legacy effect-first APIs/traces remain unchanged. Locked restore, zero-warning Release build, all 1,554 .NET and 163 Python tests pass. BOP-028 and authored profile/preparation/Content/host integration remain ZOD-082; no original event, loading duration or graphics change. ZOD-082 now completes BOP-028 through a separate authored ordered-banner format/profile, raw/processed byte conversion, strict prepared Content loading/projection and preloaded Windows composition. Condition and hold decode as separate unsigned bytes. Old JSON/profiles/exports remain closed; unsupported/unreachable records, retail provenance and original events 4/401 fail. Two raw finite Windows runs repeat all 17 files; processed final state/trace/16 PNGs agree; task clear 91, parent exit 92. Skipped admission has zero tasks and finishes at tick 1; missing event/artwork fails before output. All 16 legacy RGB8 files repeat. Locked restore, zero-warning Release build, 1,629 .NET tests (75 new) and 163 Python tests pass. No new original capture, emulator execution, graphics algorithm, loader duration or full original playback is admitted. ZOD-083 selects condition-0 Ready as a separate dispatcher/child source contract, excluding condition 1. Verified image 0/palette 2/six full-corner glyphs need a distinct presentation contract. Two independent retained event-4 states bind command 810/wait 813, slots 1/3/4, resident code/tables/VRAM and queued scales 48/128; two study reports repeat. Selected source-derived child/dispatcher/event clear timing is R+8/R+9/R+10, not measured original duration. Thirty-two authored cases remain unexecuted; ZOD-084 implements the pure aggregate, ZOD-085 selects presentation and ZOD-086 integrates a separate authored prepared profile. No runtime/profile/test change or emulator run; last full tests remain 1,629 .NET / 163 Python. ZOD-084 now implements the pure typed condition-0 Ready aggregate with event/dispatcher/child order, first-yield draw initialization and no birth sample/double-step. Child clears R+8, named dispatcher R+9, waiting event R+10; protected identities, retained tombstones, explicit owner cancellation and full late-failure rollback are covered. All 28 historical Core cases and 25 additional boundaries execute, including full state/trace replay independent of snapshot reads. Locked restore, zero-warning Release build, 1,682 .NET and 163 Python tests pass. No graphics, Content/profile, emulator or original-duration equivalence change; four presentation/host contract cases remain ZOD-085/086 gates. ZOD-085 now selects a separate Ready RGB5 CPU policy with scaled four-corner triangle interpolation, opaque writes for 14 nonzero STP-clear bank-2 entries and average blending for its one STP entry. Explicit point/dither-off/zero-window/mask-off state, safe prepared bounds and resource-ready-before-scene ownership are documented. Two read-only source/queued-state/math reports repeat, with 97,776 channel comparisons and 3,072 average-component checks. Thirty-two authored presentation expectations remain unexecuted; ZOD-086 is now ready for the separate authored preparation/host implementation. No runtime renderer, test implementation, emulator or original Ready scanout/timing comparison changed. All 163 Python regressions pass; last full .NET validation remains ZOD-084 at 1,682 tests. ZOD-086 now implements the separate prepared Ready artwork/event profiles, verified offline conversion, strict immutable loading and accepted-snapshot RGB5 CPU/Windows composition. All 32 RPR and RDY-029..032 cases have checks; 112 new .NET tests bring the total to 1,794, with 163 Python tests passing, locked restore and zero-warning Release build. Two preparations/runs repeat, raw/processed state/trace/pixels agree, and 12 finite scenarios produce 626 exact CPU/GPU/independent-model PNGs. Final isolated host runs recreate resources without changing the 72 hold-60 artifacts. Actual readiness failures create no check output; legacy RGB8/RGB5/ordered artifacts remain byte-identical (16/16/17 files). No emulator/original Ready scanout or timing comparison; the owner approved the prepared gallery on 2026-10-25; original comparison remains open. ZOD-087 now owns bounded original Ready reference binding and displayed comparison. ZOD-087 compares six source-bound displayed Ready anchors (8/10/60/64/66/67) with actual prepared C# composition over isolated transparent-palette controls at zero tolerance. Each visible frame checks 1,202 opaque and 77 background-sensitive STP pixels; empty yield matches. Two final runs repeat 96 content artifacts. Worker/code/table/VRAM/event-4 and same-history two-buffer context bind queued to displayed work. No renderer/runtime change. Locked restores, zero-warning builds, 1,794 .NET and 179 Python tests pass. The owner approved the original gallery on 2026-10-25: Visually, they look identical to my eyes. Birth/next-frame endpoints remain unsupported inside interrupt handling and are ZOD-088; original duration, hardware, map rendering and full event playback remain open. ZOD-088 closes the two missing displayed Ready birth anchors through checked BIOS timer-2 event/TCB save/restore context. Actual prepared C# ticks 0/1 (scales 48/56) match exact original scanouts with 1,202 opaque / 77 STP samples each. Two finite runs repeat 32 content artifacts and both retained interrupted states. The original main classifier remains closed; all 16 old Ready contexts and seven C# outputs are unchanged. Locked restores, zero-warning Release builds, 1,794 .NET / 192 Python tests pass; 13 new authored IRQ/receipt checks. No runtime, audio, timing or general interrupt claim. The owner approved the new two-row gallery on 2026-10-25: That looks correct. All eight selected Ready scanouts now have visual approval. ZOD-089 now composes a bounded authored general/E5/Ready/E5/exit scene with one consumer, clock and retained ledger. Both resources preload; births discard same-tick Circle; failures roll back all phases. The closed descriptor does not import original bytecode. All 74 new managed regressions pass; totals 1,868 .NET / 197 Python with locked restore and zero-warning Release build. Eight Windows scenarios produce 1,257 exact CPU/GPU/independent-model PNGs; natural runs repeat 164 files and three preparations repeat both manifest/descriptor files. Actual readiness failures create no output. Legacy RGB8/ordered/Ready retains 16/17/72 files. The owner approved the authored sequence gallery on 2026-10-25: This looks great.; no new emulator capture, combined original scanout or full startup claim. ZOD-090 traces both verified post-Ready tails, dark-screen release/barrier, EventEnd cleanup/scenario chaining, event-frame idle observation and turn/input ownership gates. Two retained endpoints (9003/9072) still have event 4 at barrier 813 and held task 55; dispatcher clear does not prove player control. Ten resident functions and 24 source files are fingerprinted; two offline reports repeat exactly. All 208 Python tests pass, including 11 new authored checks. Production C# is unchanged; last full .NET validation remains 1,868 tests, not rerun. No new original capture or player-control/timing claim. ZOD-091 selects one authored intro-to-battle ownership/input bridge with twelve explicitly unexecuted acceptance cases. ZOD-091 now implements the selected authored introduction-to-battle bridge. One pure gate opens only at accepted intro event exit with terminal workers; battle state/CT/RNG/journal stay frozen through transition. Held keyboard/mouse is baselined and queued intro input discarded. The existing battle host, rules, router, renderer and save schema are reused. All twelve IBH cases have checks. Locked restore, zero-warning Release build and 1,909 .NET / 215 Python tests pass (41/7 new). Six native scenarios yield 1,049 exact CPU/GPU/independent-model intro PNGs, 36 unchanged historical battle PNG comparisons and 170 repeated artifacts. Actual missing/corrupt/graphics readiness negatives create no new output; standalone battle/sequence preserve 6/164 files. An isolated prepared-only run repeats all 170 artifacts. Programmed focus/latch probes are not physical input/timing evidence. The owner approved the authored gallery on 2026-10-25; no original EventEnd/startup or player-control timing claim. ZOD-092 traces the selected origin-style DarkScreen worker shared by events 4/401: untextured flat translucent quads, original approximate sqrt delays, strict overshoot/extra yields and same-instance task-54/55 signaling. Three retained holding states bind both packet banks/material and fourteen resident functions; two offline source reports repeat exactly. All 288 calculated delays match; exact sqrt would change six. Source-derived Holding birth is tick 55 and first flash/parent observation 56; release R clears at R+56, parent observes R+57. These are post-readiness logical transactions, not original duration measurements. All 229 Python tests pass (14 new); production C# is unchanged and prior 1,909 .NET tests were not rerun. Thirty future DSC cases remain unexecuted; no new original capture or entry/release pixel comparison. ZOD-093 now implements a separate typed authored pure DarkScreen worker and atomic event-before-worker adapter on the existing ledger. It preserves pre-update submissions, strict overshoot/extra yields, same-instance phases/revisions, no-match release, terminal history and full late-failure rollback. Holding/first flash are 55/56; release 100 clears 156 and parent observes 157. Locked restore, zero-warning Release build and all 1,967 .NET / 229 Python tests pass (58 new managed checks). Logical DSC-001..024 portions have checks using authored synthetic delay data; actual prepared readiness, GPU recreation, DSC-025..029 and new native output comparisons remain pending. Client/Content profiles are unchanged; no original capture or visual/timing equivalence claim. ZOD-094 implements verified offline dark-screen math conversion, strict separate prepared loading and a standalone untextured RGB5 Windows preview. All 288 delays and 117 actual C# projections match original-table arithmetic; 957 final native PNGs match CPU/GPU/independent model. Two preparations repeat both files; repeated and isolated normal runs repeat all 159 artifacts. Readiness negatives create no new output; all 455 historical native artifacts remain unchanged. Locked restore, zero-warning Release build and 2,031 .NET / 240 Python tests pass (64/11 new). Integer-sample/top-left coverage is provisional; original entry/release pixels, duration and combined startup/integration remain open. The owner approved the authored gallery on 2026-10-25: That looks good. This approval update reruns no implementation suites; original entry/release pixel/timing equivalence remains open. ZOD-095 now compares thirteen source-bound original DarkScreen entry/hold/release scanouts with actual prepared C# at zero tolerance. Eleven nonempty anchors cover 446,578 samples, including a 24-pixel tail; two bind zero coverage. Material-only controls retain geometry and require exact full CPU/other subsystem/GPU-register equality with RAM confined to selected RGB/E1 bytes. Earlier degenerate controls remain excluded, with no CPU exception in the final policy. Two final runs repeat 118 artifacts; nine host negatives preserve output. Locked restores, zero-warning Release builds and 2,031 .NET / 255 Python tests pass (15 new Python). Selected edge/overlap coverage agrees; the owner approved scratch/dark-screen-095-f/review.html on 2026-10-25: This looks good. No implementation suites rerun for this approval; recorded counts retain the ZOD-095 run. Missing endpoints/full animation, original duration, hardware/map and combined startup remain open. ZOD-096 now specifies the authored nine-operation DarkScreen/general/Ready/release/exit composition with one clock/consumer/ledger, three retained task identities, full rollback and all-terminal battle ownership. Two frozen original DMA chains confirm dark-before-display order; event401 camera work is retained/excluded. Forty source files and fifteen resident ranges are fingerprinted; two offline reports repeat exactly. All 269 Python tests pass (14 new authored inspector checks); the prior 2,031 .NET count is retained without rerun. Production C# is unchanged; all forty future cases remain unexecuted (24 pure ZOD-097, 16 prepared/host/manual ZOD-098). No new emulator, capture, combined displayed pixels or original duration claim. ZOD-097 now implements the pure nine-operation DarkScreen/general/Ready/release/exit aggregate using unchanged workers with one clock/consumer/ledger, three retained identities, post-poll facts, newborn input rules and complete late rollback. Accepted synthetic exit with all three terminal is the only availability gate. Natural 60/60, zero, maximum and specified skip schedules exit at 275/155/665/127. All DSI-001..024 plus 56 boundaries pass, including independent per-tick cells/draws/displays/barriers, variable authored delays, replay and binding/VM/identity/budget guards. Locked restore, zero-warning Release build and 2,111 .NET / 269 Python tests pass (80 new managed). Existing worker/executor/engine/Content/client source and historical contracts remain unchanged. DSI-025..040, prepared/Windows/battle-input integration and a new gallery remain ZOD-098. No native/emulator/original-duration claim. ZOD-098 adds closed prepared three-resource introduction loading, ordered RGB5 composition and the existing Windows battle/input handoff. Seven finite native runs cover 2,054 exact CPU/GPU/model frames, repeated/prepared-only state/trace/PNG equality and unchanged battle outputs. The owner approved this authored gallery on 2026-10-26; no original startup/duration claim.

Evidence: [docs/architecture/zod-040-event-script-investigation.md](../../docs/architecture/zod-040-event-script-investigation.md), [docs/development/prepared-scenario-event.md](../../docs/development/prepared-scenario-event.md), [docs/behavior/conditional-event-execution.md](../../docs/behavior/conditional-event-execution.md), [docs/architecture/zod-066-battle-start-event.md](../../docs/architecture/zod-066-battle-start-event.md), [docs/evidence/events/battle-start-0401.json](../../docs/evidence/events/battle-start-0401.json), [docs/behavior/timed-event-waits.md](../../docs/behavior/timed-event-waits.md), [docs/architecture/zod-072-event-task-contract.md](../../docs/architecture/zod-072-event-task-contract.md), [docs/evidence/events/task-barrier-contract-v1.json](../../docs/evidence/events/task-barrier-contract-v1.json), [docs/behavior/scene-task-barriers.md](../../docs/behavior/scene-task-barriers.md), [docs/behavior/scene-event-barriers.md](../../docs/behavior/scene-event-barriers.md), [docs/fixtures/events/task-barrier-sequence-v1.json](../../docs/fixtures/events/task-barrier-sequence-v1.json), [docs/architecture/zod-075-general-banner-effect.md](../../docs/architecture/zod-075-general-banner-effect.md), [docs/evidence/events/general-banner-effect-contract-v1.json](../../docs/evidence/events/general-banner-effect-contract-v1.json), [docs/behavior/general-banner-effect.md](../../docs/behavior/general-banner-effect.md), [docs/architecture/zod-077-general-banner-assets.md](../../docs/architecture/zod-077-general-banner-assets.md), [docs/evidence/events/general-banner-assets-v1.json](../../docs/evidence/events/general-banner-assets-v1.json), [docs/evidence/events/general-banner-presentation-contract-v1.json](../../docs/evidence/events/general-banner-presentation-contract-v1.json), [docs/development/prepared-general-banner.md](../../docs/development/prepared-general-banner.md), [docs/development/original-banner-event-identity.md](../../docs/development/original-banner-event-identity.md), [docs/evidence/visual/original-banner-event-identity-v1.json](../../docs/evidence/visual/original-banner-event-identity-v1.json), [docs/architecture/zod-080-banner-opcode-admission.md](../../docs/architecture/zod-080-banner-opcode-admission.md), [docs/evidence/events/banner-opcode-admission-v1.json](../../docs/evidence/events/banner-opcode-admission-v1.json), [docs/evidence/events/banner-opcode-admission-contract-v1.json](../../docs/evidence/events/banner-opcode-admission-contract-v1.json), [docs/development/ordered-banner-transactions.md](../../docs/development/ordered-banner-transactions.md), [docs/development/prepared-ordered-banner-event.md](../../docs/development/prepared-ordered-banner-event.md), [docs/evidence/events/ordered-banner-profile-validation-v1.json](../../docs/evidence/events/ordered-banner-profile-validation-v1.json), [docs/architecture/zod-083-ready-display.md](../../docs/architecture/zod-083-ready-display.md), [docs/evidence/events/ready-display-source-v1.json](../../docs/evidence/events/ready-display-source-v1.json), [docs/evidence/events/ready-display-contract-v1.json](../../docs/evidence/events/ready-display-contract-v1.json), [docs/development/ready-display-transactions.md](../../docs/development/ready-display-transactions.md), [docs/architecture/zod-085-ready-rendering.md](../../docs/architecture/zod-085-ready-rendering.md), [docs/evidence/events/ready-rendering-source-v1.json](../../docs/evidence/events/ready-rendering-source-v1.json), [docs/evidence/events/ready-presentation-contract-v1.json](../../docs/evidence/events/ready-presentation-contract-v1.json), [docs/development/prepared-ready-event.md](../../docs/development/prepared-ready-event.md), [docs/evidence/events/ready-profile-validation-v1.json](../../docs/evidence/events/ready-profile-validation-v1.json), [docs/development/displayed-ready-comparison.md](../../docs/development/displayed-ready-comparison.md), [docs/evidence/visual/displayed-ready-v1.json](../../docs/evidence/visual/displayed-ready-v1.json), [docs/development/ready-birth-comparison.md](../../docs/development/ready-birth-comparison.md), [docs/evidence/visual/ready-birth-v1.json](../../docs/evidence/visual/ready-birth-v1.json), [docs/development/banner-ready-sequence.md](../../docs/development/banner-ready-sequence.md), [docs/evidence/events/banner-ready-sequence-v1.json](../../docs/evidence/events/banner-ready-sequence-v1.json), [docs/architecture/zod-090-ready-battle-handoff.md](../../docs/architecture/zod-090-ready-battle-handoff.md), [docs/evidence/events/ready-battle-handoff-v1.json](../../docs/evidence/events/ready-battle-handoff-v1.json), [docs/evidence/events/intro-battle-contract-v1.json](../../docs/evidence/events/intro-battle-contract-v1.json), [docs/development/introduction-battle-handoff.md](../../docs/development/introduction-battle-handoff.md), [docs/evidence/events/intro-battle-handoff-v1.json](../../docs/evidence/events/intro-battle-handoff-v1.json), [docs/architecture/zod-092-dark-screen-transition.md](../../docs/architecture/zod-092-dark-screen-transition.md), [docs/evidence/events/dark-screen-source-v1.json](../../docs/evidence/events/dark-screen-source-v1.json), [docs/evidence/events/dark-screen-contract-v1.json](../../docs/evidence/events/dark-screen-contract-v1.json), [docs/behavior/dark-screen-worker.md](../../docs/behavior/dark-screen-worker.md), [docs/evidence/events/dark-screen-worker-v1.json](../../docs/evidence/events/dark-screen-worker-v1.json), [docs/development/prepared-dark-screen.md](../../docs/development/prepared-dark-screen.md), [docs/evidence/events/prepared-dark-screen-v1.json](../../docs/evidence/events/prepared-dark-screen-v1.json), [docs/development/original-dark-screen-comparison.md](../../docs/development/original-dark-screen-comparison.md), [docs/evidence/events/original-dark-screen-v1.json](../../docs/evidence/events/original-dark-screen-v1.json), [docs/architecture/zod-096-combined-dark-screen-introduction.md](../../docs/architecture/zod-096-combined-dark-screen-introduction.md), [docs/evidence/events/dark-screen-intro-source-v1.json](../../docs/evidence/events/dark-screen-intro-source-v1.json), [docs/evidence/events/dark-screen-intro-contract-v1.json](../../docs/evidence/events/dark-screen-intro-contract-v1.json), [docs/behavior/dark-screen-introduction.md](../../docs/behavior/dark-screen-introduction.md), [docs/evidence/events/dark-screen-intro-worker-v1.json](../../docs/evidence/events/dark-screen-intro-worker-v1.json), [docs/development/prepared-dark-screen-introduction.md](../../docs/development/prepared-dark-screen-introduction.md), [docs/evidence/events/prepared-dark-screen-introduction-v1.json](../../docs/evidence/events/prepared-dark-screen-introduction-v1.json).

Source entry points (pinned revision above; not full traces): [src/battle/event/interpreter.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/interpreter.c), [src/world/event/interpreter.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/event/interpreter.c), [src/lib/event/script/control.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/control.c), [src/wldcore/map/location_script.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/wldcore/map/location_script.c), [src/lib/menu/command_prompts.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/menu/command_prompts.c), [src/battle/event/frame.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/event/frame.c), [src/event/attack/scenario_setup.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/attack/scenario_setup.c), [src/lib/thread/scheduler.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/thread/scheduler.c), [include/fft/thread.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/thread.h), [src/event/require/entry_conditions.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/entry_conditions.c), [src/event/require/conditions.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/conditions.c), [src/event/require/screen_support.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/screen_support.c), [src/lib/event/scene/menu_overlays.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/menu_overlays.c), [src/lib/menu/display.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/menu/display.c), [src/lib/event/scene/control.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/scene/control.c), [include/fft/script_instruction.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/script_instruction.h), [include/psx/pad.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/psx/pad.h), [target/event.yaml](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/target/event.yaml), [include/fft/gfx.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/gfx.h), [include/psx/libgpu.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/psx/libgpu.h), [src/battle/frame.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/frame.c), [src/battle/entry.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/entry.c), [src/platform/psyq/libgpu/SetDefDrawEnv.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/platform/psyq/libgpu/SetDefDrawEnv.c), [src/event/require/party_changes.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/party_changes.c), [include/fft/after_battle.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/after_battle.h).

Current implementation: [src/Zodiac.AssetTool/ScenarioBlockInspector.cs](../../src/Zodiac.AssetTool/ScenarioBlockInspector.cs), [src/Zodiac.AssetTool.Cli/ScenarioEventExporter.cs](../../src/Zodiac.AssetTool.Cli/ScenarioEventExporter.cs), [src/Zodiac.Content/Events/PreparedScenarioEvent.cs](../../src/Zodiac.Content/Events/PreparedScenarioEvent.cs), [src/Zodiac.Framework/Events/ConditionalEventProgram.cs](../../src/Zodiac.Framework/Events/ConditionalEventProgram.cs), [src/Zodiac.Framework/Events/ConditionalEventState.cs](../../src/Zodiac.Framework/Events/ConditionalEventState.cs), [src/Zodiac.Framework/Events/ConditionalEventExecutor.cs](../../src/Zodiac.Framework/Events/ConditionalEventExecutor.cs), [src/Zodiac.Framework/Events/TimedEventProgram.cs](../../src/Zodiac.Framework/Events/TimedEventProgram.cs), [src/Zodiac.Framework/Events/TimedEventState.cs](../../src/Zodiac.Framework/Events/TimedEventState.cs), [src/Zodiac.Framework/Events/TimedEventExecutor.cs](../../src/Zodiac.Framework/Events/TimedEventExecutor.cs), [src/Zodiac.Framework/Events/SceneTaskState.cs](../../src/Zodiac.Framework/Events/SceneTaskState.cs), [src/Zodiac.Framework/Events/SceneTaskResult.cs](../../src/Zodiac.Framework/Events/SceneTaskResult.cs), [src/Zodiac.Framework/Events/SceneTaskLedger.cs](../../src/Zodiac.Framework/Events/SceneTaskLedger.cs), [src/Zodiac.Framework/Events/SceneEventProgram.cs](../../src/Zodiac.Framework/Events/SceneEventProgram.cs), [src/Zodiac.Framework/Events/SceneEventState.cs](../../src/Zodiac.Framework/Events/SceneEventState.cs), [src/Zodiac.Framework/Events/SceneEventExecutor.cs](../../src/Zodiac.Framework/Events/SceneEventExecutor.cs), [src/Zodiac.Framework/Events/SceneEventResult.cs](../../src/Zodiac.Framework/Events/SceneEventResult.cs), [src/Zodiac.Framework/Events/GeneralBannerState.cs](../../src/Zodiac.Framework/Events/GeneralBannerState.cs), [src/Zodiac.Framework/Events/BannerSceneResult.cs](../../src/Zodiac.Framework/Events/BannerSceneResult.cs), [src/Zodiac.Framework/Events/BannerSceneExecutor.cs](../../src/Zodiac.Framework/Events/BannerSceneExecutor.cs), [src/Zodiac.AssetTool/GeneralBannerDecoder.cs](../../src/Zodiac.AssetTool/GeneralBannerDecoder.cs), [src/Zodiac.AssetTool.Cli/PrepareGeneralBannerCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareGeneralBannerCommand.cs), [src/Zodiac.Content/PreparedGeneralBanner.cs](../../src/Zodiac.Content/PreparedGeneralBanner.cs), [src/Fft.Client/Presentation/GeneralBannerPresentation.cs](../../src/Fft.Client/Presentation/GeneralBannerPresentation.cs), [src/Fft.Client/Presentation/GeneralBannerPreview.cs](../../src/Fft.Client/Presentation/GeneralBannerPreview.cs), [tools/verification/inspect_banner_event.py](../../tools/verification/inspect_banner_event.py), [src/Zodiac.Framework/Events/OrderedBannerProgram.cs](../../src/Zodiac.Framework/Events/OrderedBannerProgram.cs), [src/Zodiac.Framework/Events/OrderedBannerResult.cs](../../src/Zodiac.Framework/Events/OrderedBannerResult.cs), [src/Zodiac.Framework/Events/OrderedBannerExecutor.cs](../../src/Zodiac.Framework/Events/OrderedBannerExecutor.cs), [src/Zodiac.Content/Events/PreparedOrderedBannerEvent.cs](../../src/Zodiac.Content/Events/PreparedOrderedBannerEvent.cs), [src/Zodiac.AssetTool.Cli/PrepareOrderedBannerEventCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareOrderedBannerEventCommand.cs), [src/Fft.Client/Presentation/OrderedBannerPreview.cs](../../src/Fft.Client/Presentation/OrderedBannerPreview.cs), [src/Zodiac.Framework/Events/ReadyDisplayState.cs](../../src/Zodiac.Framework/Events/ReadyDisplayState.cs), [src/Zodiac.Framework/Events/ReadySceneProgram.cs](../../src/Zodiac.Framework/Events/ReadySceneProgram.cs), [src/Zodiac.Framework/Events/ReadySceneResult.cs](../../src/Zodiac.Framework/Events/ReadySceneResult.cs), [src/Zodiac.Framework/Events/ReadySceneExecutor.cs](../../src/Zodiac.Framework/Events/ReadySceneExecutor.cs), [src/Zodiac.AssetTool/ReadyArtworkDecoder.cs](../../src/Zodiac.AssetTool/ReadyArtworkDecoder.cs), [src/Zodiac.AssetTool.Cli/ReadyEventExporter.cs](../../src/Zodiac.AssetTool.Cli/ReadyEventExporter.cs), [src/Zodiac.AssetTool.Cli/PrepareReadyCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareReadyCommand.cs), [src/Zodiac.Content/PreparedReadyArtwork.cs](../../src/Zodiac.Content/PreparedReadyArtwork.cs), [src/Zodiac.Content/Events/PreparedReadyEvent.cs](../../src/Zodiac.Content/Events/PreparedReadyEvent.cs), [src/Fft.Client/Presentation/ReadyPresentation.cs](../../src/Fft.Client/Presentation/ReadyPresentation.cs), [src/Fft.Client/Presentation/ReadyPreview.cs](../../src/Fft.Client/Presentation/ReadyPreview.cs), [tools/verification/compare_ready_display.py](../../tools/verification/compare_ready_display.py), [tools/banner-blend-check/ReadySequence.cs](../../tools/banner-blend-check/ReadySequence.cs), [tools/verification/inspect_ready_interrupt.py](../../tools/verification/inspect_ready_interrupt.py), [tools/verification/compare_ready_birth.py](../../tools/verification/compare_ready_birth.py), [src/Zodiac.Framework/Events/BannerReadySequence.cs](../../src/Zodiac.Framework/Events/BannerReadySequence.cs), [src/Zodiac.Content/Events/PreparedBannerReadySequence.cs](../../src/Zodiac.Content/Events/PreparedBannerReadySequence.cs), [src/Zodiac.AssetTool.Cli/PrepareBannerReadySequenceCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareBannerReadySequenceCommand.cs), [src/Fft.Client/Presentation/BannerReadyPreview.cs](../../src/Fft.Client/Presentation/BannerReadyPreview.cs), [src/Fft.Client/Presentation/IntroductionBattleSession.cs](../../src/Fft.Client/Presentation/IntroductionBattleSession.cs), [src/Fft.Client/Presentation/PlayableBattleWindow.Introduction.cs](../../src/Fft.Client/Presentation/PlayableBattleWindow.Introduction.cs), [src/Fft.Client/Presentation/BattleInputBuffer.cs](../../src/Fft.Client/Presentation/BattleInputBuffer.cs), [src/Zodiac.Framework/Events/BattleIntroduction.cs](../../src/Zodiac.Framework/Events/BattleIntroduction.cs), [src/Zodiac.Framework/Events/DarkScreenState.cs](../../src/Zodiac.Framework/Events/DarkScreenState.cs), [src/Zodiac.Framework/Events/DarkScreenProgram.cs](../../src/Zodiac.Framework/Events/DarkScreenProgram.cs), [src/Zodiac.Framework/Events/DarkScreenResult.cs](../../src/Zodiac.Framework/Events/DarkScreenResult.cs), [src/Zodiac.Framework/Events/DarkScreenExecutor.cs](../../src/Zodiac.Framework/Events/DarkScreenExecutor.cs), [src/Zodiac.AssetTool/DarkScreenDecoder.cs](../../src/Zodiac.AssetTool/DarkScreenDecoder.cs), [src/Zodiac.AssetTool.Cli/PrepareDarkScreenCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareDarkScreenCommand.cs), [src/Zodiac.Content/PreparedDarkScreen.cs](../../src/Zodiac.Content/PreparedDarkScreen.cs), [src/Fft.Client/Presentation/DarkScreenPresentation.cs](../../src/Fft.Client/Presentation/DarkScreenPresentation.cs), [src/Fft.Client/Presentation/DarkScreenPreview.cs](../../src/Fft.Client/Presentation/DarkScreenPreview.cs), [tools/banner-blend-check/DarkScreenSequence.cs](../../tools/banner-blend-check/DarkScreenSequence.cs), [tools/verification/inspect_dark_screen_reference.py](../../tools/verification/inspect_dark_screen_reference.py), [tools/verification/compare_dark_screen_reference.py](../../tools/verification/compare_dark_screen_reference.py), [tools/verification/inspect_dark_screen_intro.py](../../tools/verification/inspect_dark_screen_intro.py), [src/Zodiac.Framework/Events/DarkScreenIntroduction.cs](../../src/Zodiac.Framework/Events/DarkScreenIntroduction.cs), [src/Zodiac.Content/Events/PreparedDarkScreenIntroduction.cs](../../src/Zodiac.Content/Events/PreparedDarkScreenIntroduction.cs), [src/Fft.Client/Presentation/DarkScreenIntroductionSession.cs](../../src/Fft.Client/Presentation/DarkScreenIntroductionSession.cs).

Existing bounded checks: [tests/Zodiac.AssetTool.Tests/ScenarioBlockInspectorTests.cs](../../tests/Zodiac.AssetTool.Tests/ScenarioBlockInspectorTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/ScenarioEventExporterTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/ScenarioEventExporterTests.cs), [tests/Zodiac.Content.Tests/PreparedScenarioEventTests.cs](../../tests/Zodiac.Content.Tests/PreparedScenarioEventTests.cs), [tests/Zodiac.Framework.Tests/ConditionalEventExecutorTests.cs](../../tests/Zodiac.Framework.Tests/ConditionalEventExecutorTests.cs), [tests/Zodiac.Content.Tests/PreparedScenarioExecutionTests.cs](../../tests/Zodiac.Content.Tests/PreparedScenarioExecutionTests.cs), [tests/Zodiac.Framework.Tests/TimedEventExecutorTests.cs](../../tests/Zodiac.Framework.Tests/TimedEventExecutorTests.cs), [tests/Zodiac.Content.Tests/PreparedTimedScenarioTests.cs](../../tests/Zodiac.Content.Tests/PreparedTimedScenarioTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/TimedScenarioEventExporterTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/TimedScenarioEventExporterTests.cs), [tests/Zodiac.Framework.Tests/SceneTaskContractTests.cs](../../tests/Zodiac.Framework.Tests/SceneTaskContractTests.cs), [tests/Zodiac.Framework.Tests/SceneTaskLedgerTests.cs](../../tests/Zodiac.Framework.Tests/SceneTaskLedgerTests.cs), [tests/Zodiac.Framework.Tests/SceneEventExecutorTests.cs](../../tests/Zodiac.Framework.Tests/SceneEventExecutorTests.cs), [tests/Zodiac.Content.Tests/PreparedSceneEventTests.cs](../../tests/Zodiac.Content.Tests/PreparedSceneEventTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/SceneScenarioEventExporterTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/SceneScenarioEventExporterTests.cs), [tests/Zodiac.Framework.Tests/GeneralBannerContractTests.cs](../../tests/Zodiac.Framework.Tests/GeneralBannerContractTests.cs), [tests/Zodiac.Framework.Tests/BannerSceneBoundaryTests.cs](../../tests/Zodiac.Framework.Tests/BannerSceneBoundaryTests.cs), [tests/Zodiac.AssetTool.Tests/GeneralBannerDecoderTests.cs](../../tests/Zodiac.AssetTool.Tests/GeneralBannerDecoderTests.cs), [tests/Zodiac.Content.Tests/PreparedGeneralBannerTests.cs](../../tests/Zodiac.Content.Tests/PreparedGeneralBannerTests.cs), [tests/Fft.Client.Tests/GeneralBannerPresentationTests.cs](../../tests/Fft.Client.Tests/GeneralBannerPresentationTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/PrepareGeneralBannerCommandTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/PrepareGeneralBannerCommandTests.cs), [tools/verification/test_banner_event.py](../../tools/verification/test_banner_event.py), [tests/Zodiac.Framework.Tests/OrderedBannerContractTests.cs](../../tests/Zodiac.Framework.Tests/OrderedBannerContractTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/OrderedBannerEventExporterTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/OrderedBannerEventExporterTests.cs), [tests/Zodiac.Content.Tests/PreparedOrderedBannerEventTests.cs](../../tests/Zodiac.Content.Tests/PreparedOrderedBannerEventTests.cs), [tests/Fft.Client.Tests/OrderedBannerSessionTests.cs](../../tests/Fft.Client.Tests/OrderedBannerSessionTests.cs), [tests/Zodiac.Framework.Tests/ReadySceneContractTests.cs](../../tests/Zodiac.Framework.Tests/ReadySceneContractTests.cs), [tests/Zodiac.AssetTool.Tests/ReadyArtworkDecoderTests.cs](../../tests/Zodiac.AssetTool.Tests/ReadyArtworkDecoderTests.cs), [tests/Zodiac.Content.Tests/PreparedReadyTests.cs](../../tests/Zodiac.Content.Tests/PreparedReadyTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/ReadyPreparationTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/ReadyPreparationTests.cs), [tests/Fft.Client.Tests/ReadyPresentationTests.cs](../../tests/Fft.Client.Tests/ReadyPresentationTests.cs), [tools/verification/test_ready_display.py](../../tools/verification/test_ready_display.py), [tools/verification/test_ready_interrupt.py](../../tools/verification/test_ready_interrupt.py), [tests/Zodiac.Framework.Tests/BannerReadySequenceTests.cs](../../tests/Zodiac.Framework.Tests/BannerReadySequenceTests.cs), [tests/Zodiac.Content.Tests/PreparedBannerReadySequenceTests.cs](../../tests/Zodiac.Content.Tests/PreparedBannerReadySequenceTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/BannerReadyPreparationTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/BannerReadyPreparationTests.cs), [tests/Fft.Client.Tests/BannerReadySessionTests.cs](../../tests/Fft.Client.Tests/BannerReadySessionTests.cs), [tools/verification/test_banner_ready_sequence.py](../../tools/verification/test_banner_ready_sequence.py), [tools/verification/test_ready_handoff.py](../../tools/verification/test_ready_handoff.py), [tests/Zodiac.Framework.Tests/BattleIntroductionTests.cs](../../tests/Zodiac.Framework.Tests/BattleIntroductionTests.cs), [tests/Fft.Client.Tests/IntroductionBattleSessionTests.cs](../../tests/Fft.Client.Tests/IntroductionBattleSessionTests.cs), [tools/verification/test_intro_battle.py](../../tools/verification/test_intro_battle.py), [tools/verification/test_dark_screen.py](../../tools/verification/test_dark_screen.py), [tests/Zodiac.Framework.Tests/DarkScreenContractTests.cs](../../tests/Zodiac.Framework.Tests/DarkScreenContractTests.cs), [tests/Zodiac.AssetTool.Tests/DarkScreenDecoderTests.cs](../../tests/Zodiac.AssetTool.Tests/DarkScreenDecoderTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/DarkScreenPreparationTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/DarkScreenPreparationTests.cs), [tests/Zodiac.Content.Tests/PreparedDarkScreenTests.cs](../../tests/Zodiac.Content.Tests/PreparedDarkScreenTests.cs), [tests/Fft.Client.Tests/DarkScreenPresentationTests.cs](../../tests/Fft.Client.Tests/DarkScreenPresentationTests.cs), [tools/verification/test_dark_screen_preview.py](../../tools/verification/test_dark_screen_preview.py), [tools/verification/test_dark_screen_reference.py](../../tools/verification/test_dark_screen_reference.py), [tools/verification/test_dark_screen_intro.py](../../tools/verification/test_dark_screen_intro.py), [tests/Zodiac.Framework.Tests/DarkScreenIntroductionContractTests.cs](../../tests/Zodiac.Framework.Tests/DarkScreenIntroductionContractTests.cs), [tests/Zodiac.Framework.Tests/DarkScreenIntroductionBoundaryTests.cs](../../tests/Zodiac.Framework.Tests/DarkScreenIntroductionBoundaryTests.cs), [tests/Fft.Client.Tests/DarkScreenIntroductionSessionTests.cs](../../tests/Fft.Client.Tests/DarkScreenIntroductionSessionTests.cs), [tools/verification/verify_dark_screen_introduction.py](../../tools/verification/verify_dark_screen_introduction.py).

Manual checkpoint (pending-original-observation): Capture one controlled branch/dialogue event with explicit initial state and timing. Separately review ZOD-087 original/control/C# gallery at scratch/ready-displayed-087-e/review.html; six anchors match exactly and the owner approved the gallery on 2026-10-25: Visually, they look identical to my eyes. Birth frames remain missing. ZOD-088 closes the missing birth rows with exact original/control/C# comparisons at scratch/ready-birth-088-b/review.html; the owner separately approved this two-row gallery on 2026-10-25: That looks correct. Review ZOD-089 authored general-banner→Ready playback at scratch/banner-ready-089-review/review.html; independent full-frame checks pass, and the owner approved it on 2026-10-25: This looks great. ZOD-090 reuses retained Ready birth and dispatcher-clear states for source/gate observation only; no new visual approval or player-control observation. ZOD-091 will need its own native introduction-to-battle review. Review scratch/intro-battle-091-review/review.html: natural/skipped authored flow, clean intro-to-battle switch and no automatic first turn. The owner approved this authored gallery on 2026-10-25: This looks good. No original startup or physical input timing approval is inferred. ZOD-092 reuses three retained holding states for source/math/material binding only; no new visual approval, original entry/release scanout or duration observation. ZOD-093 is a headless logical milestone with no new native gallery or capture. Its full managed regression run does not replace later flat-quad visual validation. Review scratch/dark-screen-094-review/review.html: entering tiles, full hold and collapsing return to checker across normal/earliest/longest authored holds. The owner approved the ZOD-094 authored gallery on 2026-10-25: That looks good. No original entry/release comparison claim; no implementation suites rerun for approval. Review ZOD-095 original/control/C# rows at scratch/dark-screen-095-f/review.html, especially overlapping entry tiles, held flash, release and 24-pixel tail; the owner approved these thirteen selected rows on 2026-10-25: This looks good. Empty ticks 2/213 have no visible coverage. No original duration or full-startup approval is inferred. ZOD-096 is a source/design milestone with no new gallery. Its queued-layer observations do not establish combined displayed pixels; ZOD-098 will need a separate authored composition/battle-handoff gallery and approval. ZOD-097 is a headless pure implementation with no new visual approval. Its complete managed suite does not execute the sixteen future prepared/host/manual cases; ZOD-098 will need its own finite native checks and owner gallery. ZOD-098: review scratch/dark-screen-intro-098-review/review.html for entry, dark under general/Ready, hidden gaps, release and battle switch. The owner approved this authored gallery on 2026-10-26: Looks good to me. Original startup/duration approval is not inferred.

Remaining gaps:

- Original startup/loading latency, other draw-state variants, camera workers, retail opcode-78 admission, EventEnd, tutorial/World/speed changes and full original scheduler/visual equivalence remain open. Selected Ready/DarkScreen displayed proofs are bounded by their linked evidence.
- Selected banner states are now identified as event 4. The earlier event-entry/conditional selection and common BattleStart event 401 activation remain unobserved; complete event playback is unsupported.
- Typed ordered execution and the authored preloaded profile are proven; broader scene workers and original event playback remain unsupported. Native startup duration, additional scene capabilities and complete event playback remain separate.
- Eight selected Ready displayed anchors are proven separately; original Ready loading/completion/input/display duration, other variants and full startup remain open.
- Thirteen selected DarkScreen scanouts match exactly; birth/transition/final yield/clear/parent endpoints, full animation, other styles, original duration and combined prepared/Windows scene integration remain open.

Next action: ZOD-098 authored composition is approved. Select the next bounded milestone through the backlog; original startup/duration, missing endpoints, physical timing and whole-game fidelity remain open.

Bounded follow-ups: ZOD-080, ZOD-081, ZOD-082, ZOD-083, ZOD-084, ZOD-085, ZOD-086, ZOD-087, ZOD-088, ZOD-089, ZOD-090, ZOD-091, ZOD-092, ZOD-093, ZOD-094, ZOD-095, ZOD-096, ZOD-097, ZOD-098.

## Screens

### COV-UI-001 — Opening/title, New Game/Continue and name/birthday screens

Status: **absent**; scope: **baseline**; ticket: **ZOD-042**; owner area: Fft.Client presentation and application commands.

No original title/new-game/continue/name/birthday shell. ZOD-102 proves only an authored headless Home/New/live Resume/World/Party and confirmation model; no native host exists. No-argument client still prints a bootstrap line.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md), [docs/development/synthetic-world-navigation.md](../../docs/development/synthetic-world-navigation.md), [docs/evidence/navigation/synthetic-world-navigation-v1.json](../../docs/evidence/navigation/synthetic-world-navigation-v1.json).

Source entry points (pinned revision above; not full traces): [src/open/title.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/open/title.c), [src/open/prologue.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/open/prologue.c), [src/world/name_entry.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/name_entry.c).

Current implementation: [src/Fft.Client/Navigation/WorldMenuSession.cs](../../src/Fft.Client/Navigation/WorldMenuSession.cs).

Existing bounded checks: [tests/Fft.Client.Tests/WorldMenuSessionTests.cs](../../tests/Fft.Client.Tests/WorldMenuSessionTests.cs).

Manual checkpoint (pending-original-observation): Navigate title/new game/continue, confirm/cancel name and birthday.

Remaining gaps:

- Original flow, assets, focus, input semantics and Windows presentation design are absent.

Next action: Implement the separate authored Windows navigation host in ZOD-103 before ZOD-069. Retain original title/assets/flow, input timing and name/birthday as source-traced baseline work under ZOD-042.

Bounded follow-ups: ZOD-102, ZOD-103, ZOD-069.

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

No player-facing slot UI. ZOD-068 supplies the tested synthetic checkpoint service; ZOD-102 supplies headless authored navigation and safe modal decisions. ZOD-103 native navigation is still required before ZOD-069. Complete original availability remains unenumerated.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md), [docs/architecture/zod-067-campaign-save-state.md](../../docs/architecture/zod-067-campaign-save-state.md), [docs/development/synthetic-world-navigation.md](../../docs/development/synthetic-world-navigation.md), [docs/evidence/navigation/synthetic-world-navigation-v1.json](../../docs/evidence/navigation/synthetic-world-navigation-v1.json).

Source entry points (pinned revision above; not full traces): [src/world/card_save.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/card_save.c), [include/fft/save.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/save.h), [src/event/card/entry.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/card/entry.c).

Automated baseline check: **none identified for this scope**; add a scenario regression when implementing the next action.

Manual checkpoint (pending-original-observation): At legal original save locations, save/continue through Windows UI and test overwrite/cancel/failure recovery.

Remaining gaps:

- Native host/device/visual checks and slot list/confirmation/error/recovery screens remain unimplemented; original checkpoint availability needs separate source/observation evidence.

Next action: Implement ZOD-103, then ZOD-069 using the existing service; full ZOD-042 completion is not the slot proof prerequisite.

Bounded follow-ups: ZOD-068, ZOD-069, ZOD-102, ZOD-103.

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

Fixed prepared MAP003/Chocobo scene renders with declared neutral-lighting and modern depth/cutout approximations. ZOD-033 adds strict optional headless authored/same-environment PNG comparison, checkpoint/tick/view/turn-projection bindings, regional RGBA diagnostics and zero-tolerance synthetic Windows plans. All 22 Python tests pass; two 120-frame synthetic captures and reports match. No original rendered comparison or full simulation equivalence is claimed. The existing RetroArch 1.22.2/Beetle PSX software installation now has an isolated native capture profile. Two input-disconnected finite boots reach introductory game content with matching raw screenshots and unchanged installed settings; the owner subsequently captured the target banner/state, and two four-frame state restores produce identical banner scene images. Exact original/managed timing/context, BSV playback and original comparisons remain open. All 30 Python tests pass, with 8 new profile/isolation checks. A separate full-visible observed banner profile now records explicit measured borders and context bindings. Two diagnostic reports/review pages match. Manual inspection finds consistent apparent text placement; strict equality fails at 1,460/1,577 managed-background-invariant samples (original equal or darker by up to 16 RGB levels). No fidelity threshold is selected or runtime changed. All 52 Python tests pass (22 new). A subsequent developer precision study reads the frozen GPU dtd=0/displayed VRAM and exactly matches all 1,577 prior core samples with source-informed five-bit arithmetic, quantized vertical colors and zero-padded emulator output. Two reports match; all 66 Python tests pass (14 new). No runtime profile is changed, and installed-DLL source identity/per-primitive draw-state provenance remain unverified. A subsequent bounded Client five-bit profile now uses explicit per-draw dither/origin state. Frozen DMA-chain reconstruction identifies the REQUIRE dark-screen mode before all six banner packets. Actual native captures match all 1,577 selected original glyph pixels exactly, two capture sets repeat, and all 16 legacy RGB8 capture files retain historical bytes. The dither-on negative control distinguishes draw-state and pixel failures. Locked restore, zero-warning Release build, 1,418 .NET tests (30 new) and 81 Python tests (15 new) pass; timing, background-sensitive blending and broader integration remain open. Original saved-task continuation now identifies the entry as reveal counter 18 and reproduces late reveal, sampled hold boundaries, every hide iteration and natural exit. Two 17-checkpoint capture sets repeat PNGs, exit states and task/quad observations. Eighteen metadata-only managed replay cases match observed phases/counters/crops, with task exit at checkpoint 91 and later slot reuse rejected. Locked restore, zero-warning Release build, 1,436 .NET tests and 92 Python tests pass. Full reveal/loading, physical Circle input, event identity, screenshot/task latency and background blending/coverage remain open. A controlled transparent-palette original state probe now isolates a matched background at the full-visible anchor: all 28 non-GPU subsystems and GPU registers match, and all 58,312 pixels outside the sample footprint stay unchanged. Actual C# ComposeOver output matches all 3,128 affected pixels, including 1,551 previously excluded background-sensitive samples. Two original/probe/C# capture sets repeat, default checker GPU captures retain all 16 historical files, and zero-warning builds with 1,450 .NET and 104 Python tests pass. This is one intentionally altered-state control, not managed scene rendering or general PS1 fidelity; partial pixels/timing, early reveal/loading/input and event identity remain open. Source-bound displayed hide comparisons now decode the pre-submission VSync stack, pending environments and two-buffer order, then bind the front surface to a hash-bound earlier submission. All five original/probe PNG crops match saved scanout. Actual C# matches 6,917 affected temporal samples across four visible hide anchors and preserves the fully hidden background; controlled states retain all 28 non-GPU subsystems/GPU registers. Two runs repeat. Locked restores, zero-warning builds, 1,450 .NET tests and 116 Python tests pass. This does not establish a universal frame delay, early reveal/loading/input, event identity or managed scene rendering. Early reveal/readiness observations now classify four loader yields and the first pre-reveal yield. Sixteen metadata-only managed replay cases match source counters/crops. Actual C# matches nine of eleven displayed frames; reveal 4/13 differ at 21/14 blue-channel pixels by +8 original minus C#. Isolation, scanout, geometry and C#/Python checks pass. Two runs repeat 107 artifacts. Locked restores, zero-warning builds, 1,466 .NET and 130 Python tests pass. No runtime arithmetic or tolerance change; exact early-reveal fidelity remains open. A bounded triangle color trace now seeds TL/TR/BL at TL and TR/BL/BR at BL using the source-derived diagonal. Actual corrected C# matches all eleven retained original reveal frames, changing only the earlier 21/14 blue-channel pixels. Two runs repeat 57 artifacts; full-visible/hide CPU outputs remain unchanged. Two five-bit Windows runs preserve fifteen prior PNGs; only receipt policy text changes. RGB8 preserves all sixteen files. Locked restores, zero-warning builds, 1,486 .NET and 135 Python tests pass. No simulation, loading, schema or dependency changes; no general hardware/rasterizer proof. A paired owner-operated USB-controller reveal Circle observation now binds neutral reveal count 5 to Circle-only saved input, unchanged count and the shared extra exit yield. Two no-input continuations clear the task on the next source update with repeated state/PNG bytes; two inspection reports repeat. Three metadata-only C# replay cases match retained 5/0/0 progress, extra yield and completion. Eight authored Python checks reject invalid witnesses. Locked restore, zero-warning build, 1,489 .NET and 143 Python tests pass. No runtime changes; physical hold/hide input, BSV playback, USB/display latency and event identity remain open. A subsequent physical hold-Circle pair verifies Holding count 1 -> extra exit yield -> cleared task at source frames 9000/9001/9002, retaining 20/1/0 progress. Two dedicated three-frontend-frame continuations repeat state/PNG bytes and two inspector reports repeat. A four-frame continuation is correctly rejected after slot reuse at frame 9003. Six combined metadata-only C# checkpoints and ten authored Python witness checks pass; latest zero-warning Release build has 1,492 passing .NET tests, and all 145 Python tests pass. No runtime changes; hide-phase physical input, BSV playback, USB/display latency and event identity remain open. A subsequent owner-operated hide-phase Circle pair verifies source frames 6435/6436 and logical ticks 81/82: neutral input becomes Circle-only while the same active task advances hide count 1 -> 2, alternates buffer and queues six exact 8-row crops. No skip yield or exit cause occurs. Two inspector reports repeat; two additional metadata-only C# checkpoints match the unchanged driver. Seven authored Python hide-witness checks pass; latest zero-warning Release build has 1,494 passing .NET tests and all 152 Python tests pass. No emulator ran for this pair inspection; subsequent natural completion, BSV playback, USB/display latency and original event identity remain unverified. A source-bound saved-state study now identifies the captured banner as event 4, not common BattleStart event 401. All eight selected frozen states agree on current/cache IDs and a unique full archive payload match after the four-byte loader marker. The interpreter uses saved $fp for command offset: earliest state waits at 731; target banner command 804 has condition 8/hold 60 and the active worker matches the task-56 wait at 807. Scenario-active and correctly decoded packed flags support the scenario path. Two reports repeat; eleven authored verifier regressions pass and all 163 Python tests pass. No emulator/original instruction execution, runtime change or .NET rerun; last full managed count remains 1,494. Event entry/conditional selection, common event-401 activation and complete playback remain open. ZOD-080 now traces bounded opcode-78 admission and selects a distinct event-before-worker transaction. Retained original frames 9001/9002/9003 show parent wait 807 with live worker, wait 807 with cleared worker, then wait 813 with a new condition. Existing effect-first APIs remain unchanged. Twenty-eight authored expectations are unexecuted; ZOD-081 implements typed ordered Core behavior and ZOD-082 then admits the separate authored preloaded profile. No runtime/profile change, emulator run or suite rerun; original loading duration and full event playback remain open. ZOD-081 now implements typed ordered event/banner execution in Zodiac.Framework. All 27 authored Core expectations and 33 additional boundary cases pass, including H+31 task clear/H+32 parent resumption, newborn no-step, shared budgets, post-poll facts, protected identities and atomic late-failure rollback. Legacy effect-first APIs/traces remain unchanged. Locked restore, zero-warning Release build, all 1,554 .NET and 163 Python tests pass. BOP-028 and authored profile/preparation/Content/host integration remain ZOD-082; no original event, loading duration or graphics change. ZOD-082 now completes BOP-028 through a separate authored ordered-banner format/profile, raw/processed byte conversion, strict prepared Content loading/projection and preloaded Windows composition. Condition and hold decode as separate unsigned bytes. Old JSON/profiles/exports remain closed; unsupported/unreachable records, retail provenance and original events 4/401 fail. Two raw finite Windows runs repeat all 17 files; processed final state/trace/16 PNGs agree; task clear 91, parent exit 92. Skipped admission has zero tasks and finishes at tick 1; missing event/artwork fails before output. All 16 legacy RGB8 files repeat. Locked restore, zero-warning Release build, 1,629 .NET tests (75 new) and 163 Python tests pass. No new original capture, emulator execution, graphics algorithm, loader duration or full original playback is admitted. ZOD-083 selects condition-0 Ready as a separate dispatcher/child source contract, excluding condition 1. Verified image 0/palette 2/six full-corner glyphs need a distinct presentation contract. Two independent retained event-4 states bind command 810/wait 813, slots 1/3/4, resident code/tables/VRAM and queued scales 48/128; two study reports repeat. Selected source-derived child/dispatcher/event clear timing is R+8/R+9/R+10, not measured original duration. Thirty-two authored cases remain unexecuted; ZOD-084 implements the pure aggregate, ZOD-085 selects presentation and ZOD-086 integrates a separate authored prepared profile. No runtime/profile/test change or emulator run; last full tests remain 1,629 .NET / 163 Python. ZOD-084 now implements the pure typed condition-0 Ready aggregate with event/dispatcher/child order, first-yield draw initialization and no birth sample/double-step. Child clears R+8, named dispatcher R+9, waiting event R+10; protected identities, retained tombstones, explicit owner cancellation and full late-failure rollback are covered. All 28 historical Core cases and 25 additional boundaries execute, including full state/trace replay independent of snapshot reads. Locked restore, zero-warning Release build, 1,682 .NET and 163 Python tests pass. No graphics, Content/profile, emulator or original-duration equivalence change; four presentation/host contract cases remain ZOD-085/086 gates. ZOD-085 now selects a separate Ready RGB5 CPU policy with scaled four-corner triangle interpolation, opaque writes for 14 nonzero STP-clear bank-2 entries and average blending for its one STP entry. Explicit point/dither-off/zero-window/mask-off state, safe prepared bounds and resource-ready-before-scene ownership are documented. Two read-only source/queued-state/math reports repeat, with 97,776 channel comparisons and 3,072 average-component checks. Thirty-two authored presentation expectations remain unexecuted; ZOD-086 is now ready for the separate authored preparation/host implementation. No runtime renderer, test implementation, emulator or original Ready scanout/timing comparison changed. All 163 Python regressions pass; last full .NET validation remains ZOD-084 at 1,682 tests. ZOD-086 now implements the separate prepared Ready artwork/event profiles, verified offline conversion, strict immutable loading and accepted-snapshot RGB5 CPU/Windows composition. All 32 RPR and RDY-029..032 cases have checks; 112 new .NET tests bring the total to 1,794, with 163 Python tests passing, locked restore and zero-warning Release build. Two preparations/runs repeat, raw/processed state/trace/pixels agree, and 12 finite scenarios produce 626 exact CPU/GPU/independent-model PNGs. Final isolated host runs recreate resources without changing the 72 hold-60 artifacts. Actual readiness failures create no check output; legacy RGB8/RGB5/ordered artifacts remain byte-identical (16/16/17 files). No emulator/original Ready scanout or timing comparison; the owner approved the prepared gallery on 2026-10-25; original comparison remains open. ZOD-087 now owns bounded original Ready reference binding and displayed comparison. ZOD-087 compares six source-bound displayed Ready anchors (8/10/60/64/66/67) with actual prepared C# composition over isolated transparent-palette controls at zero tolerance. Each visible frame checks 1,202 opaque and 77 background-sensitive STP pixels; empty yield matches. Two final runs repeat 96 content artifacts. Worker/code/table/VRAM/event-4 and same-history two-buffer context bind queued to displayed work. No renderer/runtime change. Locked restores, zero-warning builds, 1,794 .NET and 179 Python tests pass. The owner approved the original gallery on 2026-10-25: Visually, they look identical to my eyes. Birth/next-frame endpoints remain unsupported inside interrupt handling and are ZOD-088; original duration, hardware, map rendering and full event playback remain open. ZOD-088 closes the two missing displayed Ready birth anchors through checked BIOS timer-2 event/TCB save/restore context. Actual prepared C# ticks 0/1 (scales 48/56) match exact original scanouts with 1,202 opaque / 77 STP samples each. Two finite runs repeat 32 content artifacts and both retained interrupted states. The original main classifier remains closed; all 16 old Ready contexts and seven C# outputs are unchanged. Locked restores, zero-warning Release builds, 1,794 .NET / 192 Python tests pass; 13 new authored IRQ/receipt checks. No runtime, audio, timing or general interrupt claim. The owner approved the new two-row gallery on 2026-10-25: That looks correct. All eight selected Ready scanouts now have visual approval. ZOD-089 now composes a bounded authored general/E5/Ready/E5/exit scene with one consumer, clock and retained ledger. Both resources preload; births discard same-tick Circle; failures roll back all phases. The closed descriptor does not import original bytecode. All 74 new managed regressions pass; totals 1,868 .NET / 197 Python with locked restore and zero-warning Release build. Eight Windows scenarios produce 1,257 exact CPU/GPU/independent-model PNGs; natural runs repeat 164 files and three preparations repeat both manifest/descriptor files. Actual readiness failures create no output. Legacy RGB8/ordered/Ready retains 16/17/72 files. The owner approved the authored sequence gallery on 2026-10-25: This looks great.; no new emulator capture, combined original scanout or full startup claim. ZOD-090 traces both verified post-Ready tails, dark-screen release/barrier, EventEnd cleanup/scenario chaining, event-frame idle observation and turn/input ownership gates. Two retained endpoints (9003/9072) still have event 4 at barrier 813 and held task 55; dispatcher clear does not prove player control. Ten resident functions and 24 source files are fingerprinted; two offline reports repeat exactly. All 208 Python tests pass, including 11 new authored checks. Production C# is unchanged; last full .NET validation remains 1,868 tests, not rerun. No new original capture or player-control/timing claim. ZOD-091 selects one authored intro-to-battle ownership/input bridge with twelve explicitly unexecuted acceptance cases. ZOD-091 now implements the selected authored introduction-to-battle bridge. One pure gate opens only at accepted intro event exit with terminal workers; battle state/CT/RNG/journal stay frozen through transition. Held keyboard/mouse is baselined and queued intro input discarded. The existing battle host, rules, router, renderer and save schema are reused. All twelve IBH cases have checks. Locked restore, zero-warning Release build and 1,909 .NET / 215 Python tests pass (41/7 new). Six native scenarios yield 1,049 exact CPU/GPU/independent-model intro PNGs, 36 unchanged historical battle PNG comparisons and 170 repeated artifacts. Actual missing/corrupt/graphics readiness negatives create no new output; standalone battle/sequence preserve 6/164 files. An isolated prepared-only run repeats all 170 artifacts. Programmed focus/latch probes are not physical input/timing evidence. The owner approved the authored gallery on 2026-10-25; no original EventEnd/startup or player-control timing claim. ZOD-092 traces the selected origin-style DarkScreen worker shared by events 4/401: untextured flat translucent quads, original approximate sqrt delays, strict overshoot/extra yields and same-instance task-54/55 signaling. Three retained holding states bind both packet banks/material and fourteen resident functions; two offline source reports repeat exactly. All 288 calculated delays match; exact sqrt would change six. Source-derived Holding birth is tick 55 and first flash/parent observation 56; release R clears at R+56, parent observes R+57. These are post-readiness logical transactions, not original duration measurements. All 229 Python tests pass (14 new); production C# is unchanged and prior 1,909 .NET tests were not rerun. Thirty future DSC cases remain unexecuted; no new original capture or entry/release pixel comparison. ZOD-093 now implements a separate typed authored pure DarkScreen worker and atomic event-before-worker adapter on the existing ledger. It preserves pre-update submissions, strict overshoot/extra yields, same-instance phases/revisions, no-match release, terminal history and full late-failure rollback. Holding/first flash are 55/56; release 100 clears 156 and parent observes 157. Locked restore, zero-warning Release build and all 1,967 .NET / 229 Python tests pass (58 new managed checks). Logical DSC-001..024 portions have checks using authored synthetic delay data; actual prepared readiness, GPU recreation, DSC-025..029 and new native output comparisons remain pending. Client/Content profiles are unchanged; no original capture or visual/timing equivalence claim. ZOD-094 implements verified offline dark-screen math conversion, strict separate prepared loading and a standalone untextured RGB5 Windows preview. All 288 delays and 117 actual C# projections match original-table arithmetic; 957 final native PNGs match CPU/GPU/independent model. Two preparations repeat both files; repeated and isolated normal runs repeat all 159 artifacts. Readiness negatives create no new output; all 455 historical native artifacts remain unchanged. Locked restore, zero-warning Release build and 2,031 .NET / 240 Python tests pass (64/11 new). Integer-sample/top-left coverage is provisional; original entry/release pixels, duration and combined startup/integration remain open. The owner approved the authored gallery on 2026-10-25: That looks good. This approval update reruns no implementation suites; original entry/release pixel/timing equivalence remains open. ZOD-095 now compares thirteen source-bound original DarkScreen entry/hold/release scanouts with actual prepared C# at zero tolerance. Eleven nonempty anchors cover 446,578 samples, including a 24-pixel tail; two bind zero coverage. Material-only controls retain geometry and require exact full CPU/other subsystem/GPU-register equality with RAM confined to selected RGB/E1 bytes. Earlier degenerate controls remain excluded, with no CPU exception in the final policy. Two final runs repeat 118 artifacts; nine host negatives preserve output. Locked restores, zero-warning Release builds and 2,031 .NET / 255 Python tests pass (15 new Python). Selected edge/overlap coverage agrees; the owner approved scratch/dark-screen-095-f/review.html on 2026-10-25: This looks good. No implementation suites rerun for this approval; recorded counts retain the ZOD-095 run. Missing endpoints/full animation, original duration, hardware/map and combined startup remain open. ZOD-096 now specifies the authored nine-operation DarkScreen/general/Ready/release/exit composition with one clock/consumer/ledger, three retained task identities, full rollback and all-terminal battle ownership. Two frozen original DMA chains confirm dark-before-display order; event401 camera work is retained/excluded. Forty source files and fifteen resident ranges are fingerprinted; two offline reports repeat exactly. All 269 Python tests pass (14 new authored inspector checks); the prior 2,031 .NET count is retained without rerun. Production C# is unchanged; all forty future cases remain unexecuted (24 pure ZOD-097, 16 prepared/host/manual ZOD-098). No new emulator, capture, combined displayed pixels or original duration claim. ZOD-097 now implements the pure nine-operation DarkScreen/general/Ready/release/exit aggregate using unchanged workers with one clock/consumer/ledger, three retained identities, post-poll facts, newborn input rules and complete late rollback. Accepted synthetic exit with all three terminal is the only availability gate. Natural 60/60, zero, maximum and specified skip schedules exit at 275/155/665/127. All DSI-001..024 plus 56 boundaries pass, including independent per-tick cells/draws/displays/barriers, variable authored delays, replay and binding/VM/identity/budget guards. Locked restore, zero-warning Release build and 2,111 .NET / 269 Python tests pass (80 new managed). Existing worker/executor/engine/Content/client source and historical contracts remain unchanged. DSI-025..040, prepared/Windows/battle-input integration and a new gallery remain ZOD-098. No native/emulator/original-duration claim. ZOD-098 adds closed prepared three-resource introduction loading, ordered RGB5 composition and the existing Windows battle/input handoff. Seven finite native runs cover 2,054 exact CPU/GPU/model frames, repeated/prepared-only state/trace/PNG equality and unchanged battle outputs. The owner approved this authored gallery on 2026-10-26; no original startup/duration claim.

Evidence: [docs/development/prepared-scene.md](../../docs/development/prepared-scene.md), [docs/development/visual-comparison-checkpoints.md](../../docs/development/visual-comparison-checkpoints.md), [docs/evidence/visual/synthetic-checkpoint-comparison-v1.json](../../docs/evidence/visual/synthetic-checkpoint-comparison-v1.json), [docs/development/retroarch-reference-capture.md](../../docs/development/retroarch-reference-capture.md), [docs/evidence/visual/retroarch-reference-readiness-v1.json](../../docs/evidence/visual/retroarch-reference-readiness-v1.json), [docs/evidence/visual/original-banner-state-v1.json](../../docs/evidence/visual/original-banner-state-v1.json), [docs/development/observed-general-banner-comparison.md](../../docs/development/observed-general-banner-comparison.md), [docs/evidence/visual/observed-general-banner-comparison-v1.json](../../docs/evidence/visual/observed-general-banner-comparison-v1.json), [docs/architecture/general-banner-native-color-study.md](../../docs/architecture/general-banner-native-color-study.md), [docs/evidence/visual/general-banner-precision-study-v1.json](../../docs/evidence/visual/general-banner-precision-study-v1.json), [docs/development/five-bit-general-banner.md](../../docs/development/five-bit-general-banner.md), [docs/evidence/visual/five-bit-general-banner-v1.json](../../docs/evidence/visual/five-bit-general-banner-v1.json), [docs/development/original-banner-continuation.md](../../docs/development/original-banner-continuation.md), [docs/evidence/visual/original-banner-continuation-v1.json](../../docs/evidence/visual/original-banner-continuation-v1.json), [docs/development/general-banner-background-blending.md](../../docs/development/general-banner-background-blending.md), [docs/evidence/visual/general-banner-background-blending-v1.json](../../docs/evidence/visual/general-banner-background-blending-v1.json), [docs/development/displayed-banner-hide.md](../../docs/development/displayed-banner-hide.md), [docs/evidence/visual/displayed-banner-hide-v1.json](../../docs/evidence/visual/displayed-banner-hide-v1.json), [docs/development/early-banner-reveal.md](../../docs/development/early-banner-reveal.md), [docs/evidence/visual/early-banner-reveal-v1.json](../../docs/evidence/visual/early-banner-reveal-v1.json), [docs/development/banner-triangle-colors.md](../../docs/development/banner-triangle-colors.md), [docs/evidence/visual/banner-triangle-colors-v1.json](../../docs/evidence/visual/banner-triangle-colors-v1.json), [docs/development/original-banner-circle-input.md](../../docs/development/original-banner-circle-input.md), [docs/evidence/visual/original-banner-circle-reveal-v1.json](../../docs/evidence/visual/original-banner-circle-reveal-v1.json), [docs/evidence/visual/original-banner-circle-hold-v1.json](../../docs/evidence/visual/original-banner-circle-hold-v1.json), [docs/evidence/visual/original-banner-circle-hide-v1.json](../../docs/evidence/visual/original-banner-circle-hide-v1.json), [docs/development/original-banner-event-identity.md](../../docs/development/original-banner-event-identity.md), [docs/evidence/visual/original-banner-event-identity-v1.json](../../docs/evidence/visual/original-banner-event-identity-v1.json), [docs/architecture/zod-080-banner-opcode-admission.md](../../docs/architecture/zod-080-banner-opcode-admission.md), [docs/evidence/events/banner-opcode-admission-v1.json](../../docs/evidence/events/banner-opcode-admission-v1.json), [docs/evidence/events/banner-opcode-admission-contract-v1.json](../../docs/evidence/events/banner-opcode-admission-contract-v1.json), [docs/development/ordered-banner-transactions.md](../../docs/development/ordered-banner-transactions.md), [docs/development/prepared-ordered-banner-event.md](../../docs/development/prepared-ordered-banner-event.md), [docs/evidence/events/ordered-banner-profile-validation-v1.json](../../docs/evidence/events/ordered-banner-profile-validation-v1.json), [docs/architecture/zod-083-ready-display.md](../../docs/architecture/zod-083-ready-display.md), [docs/evidence/events/ready-display-source-v1.json](../../docs/evidence/events/ready-display-source-v1.json), [docs/evidence/events/ready-display-contract-v1.json](../../docs/evidence/events/ready-display-contract-v1.json), [docs/development/ready-display-transactions.md](../../docs/development/ready-display-transactions.md), [docs/architecture/zod-085-ready-rendering.md](../../docs/architecture/zod-085-ready-rendering.md), [docs/evidence/events/ready-rendering-source-v1.json](../../docs/evidence/events/ready-rendering-source-v1.json), [docs/evidence/events/ready-presentation-contract-v1.json](../../docs/evidence/events/ready-presentation-contract-v1.json), [docs/development/prepared-ready-event.md](../../docs/development/prepared-ready-event.md), [docs/evidence/events/ready-profile-validation-v1.json](../../docs/evidence/events/ready-profile-validation-v1.json), [docs/development/displayed-ready-comparison.md](../../docs/development/displayed-ready-comparison.md), [docs/evidence/visual/displayed-ready-v1.json](../../docs/evidence/visual/displayed-ready-v1.json), [docs/development/ready-birth-comparison.md](../../docs/development/ready-birth-comparison.md), [docs/evidence/visual/ready-birth-v1.json](../../docs/evidence/visual/ready-birth-v1.json), [docs/development/banner-ready-sequence.md](../../docs/development/banner-ready-sequence.md), [docs/evidence/events/banner-ready-sequence-v1.json](../../docs/evidence/events/banner-ready-sequence-v1.json), [docs/architecture/zod-090-ready-battle-handoff.md](../../docs/architecture/zod-090-ready-battle-handoff.md), [docs/evidence/events/ready-battle-handoff-v1.json](../../docs/evidence/events/ready-battle-handoff-v1.json), [docs/evidence/events/intro-battle-contract-v1.json](../../docs/evidence/events/intro-battle-contract-v1.json), [docs/development/introduction-battle-handoff.md](../../docs/development/introduction-battle-handoff.md), [docs/evidence/events/intro-battle-handoff-v1.json](../../docs/evidence/events/intro-battle-handoff-v1.json), [docs/architecture/zod-092-dark-screen-transition.md](../../docs/architecture/zod-092-dark-screen-transition.md), [docs/evidence/events/dark-screen-source-v1.json](../../docs/evidence/events/dark-screen-source-v1.json), [docs/evidence/events/dark-screen-contract-v1.json](../../docs/evidence/events/dark-screen-contract-v1.json), [docs/behavior/dark-screen-worker.md](../../docs/behavior/dark-screen-worker.md), [docs/evidence/events/dark-screen-worker-v1.json](../../docs/evidence/events/dark-screen-worker-v1.json), [docs/development/prepared-dark-screen.md](../../docs/development/prepared-dark-screen.md), [docs/evidence/events/prepared-dark-screen-v1.json](../../docs/evidence/events/prepared-dark-screen-v1.json), [docs/development/original-dark-screen-comparison.md](../../docs/development/original-dark-screen-comparison.md), [docs/evidence/events/original-dark-screen-v1.json](../../docs/evidence/events/original-dark-screen-v1.json), [docs/architecture/zod-096-combined-dark-screen-introduction.md](../../docs/architecture/zod-096-combined-dark-screen-introduction.md), [docs/evidence/events/dark-screen-intro-source-v1.json](../../docs/evidence/events/dark-screen-intro-source-v1.json), [docs/evidence/events/dark-screen-intro-contract-v1.json](../../docs/evidence/events/dark-screen-intro-contract-v1.json), [docs/behavior/dark-screen-introduction.md](../../docs/behavior/dark-screen-introduction.md), [docs/evidence/events/dark-screen-intro-worker-v1.json](../../docs/evidence/events/dark-screen-intro-worker-v1.json), [docs/development/prepared-dark-screen-introduction.md](../../docs/development/prepared-dark-screen-introduction.md), [docs/evidence/events/prepared-dark-screen-introduction-v1.json](../../docs/evidence/events/prepared-dark-screen-introduction-v1.json).

Source entry points (pinned revision above; not full traces): [src/battle/map/runtime.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/runtime.c), [src/battle/map/light.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/light.c), [src/battle/map/palettes.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/palettes.c), [src/lib/thread/scheduler.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/thread/scheduler.c), [src/event/require/entry_conditions.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/entry_conditions.c), [src/event/require/conditions.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/conditions.c), [src/event/require/screen_support.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/screen_support.c), [src/event/require/party_changes.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/party_changes.c), [include/fft/after_battle.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/after_battle.h), [include/fft/script_instruction.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/script_instruction.h).

Current implementation: [src/Fft.Client/Presentation/PreparedSceneGeometry.cs](../../src/Fft.Client/Presentation/PreparedSceneGeometry.cs), [src/Fft.Client/Presentation/PreparedSceneWindow.cs](../../src/Fft.Client/Presentation/PreparedSceneWindow.cs), [tools/verification/compare_visual_checkpoints.py](../../tools/verification/compare_visual_checkpoints.py), [tools/verification/prepare_synthetic_visual_comparison.py](../../tools/verification/prepare_synthetic_visual_comparison.py), [tools/verification/prepare_retroarch_reference.py](../../tools/verification/prepare_retroarch_reference.py), [tools/verification/verify_retroarch_boot.py](../../tools/verification/verify_retroarch_boot.py), [tools/verification/compare_observed_banner.py](../../tools/verification/compare_observed_banner.py), [tools/verification/prepare_observed_banner_comparison.py](../../tools/verification/prepare_observed_banner_comparison.py), [tools/verification/analyze_banner_precision.py](../../tools/verification/analyze_banner_precision.py), [src/Fft.Client/Presentation/FiveBitGeneralBannerPresentation.cs](../../src/Fft.Client/Presentation/FiveBitGeneralBannerPresentation.cs), [src/Fft.Client/Presentation/BannerPreviewOptions.cs](../../src/Fft.Client/Presentation/BannerPreviewOptions.cs), [tools/verification/trace_banner_draw_state.py](../../tools/verification/trace_banner_draw_state.py), [tools/verification/compare_native_banner.py](../../tools/verification/compare_native_banner.py), [tools/verification/capture_banner_continuation.py](../../tools/verification/capture_banner_continuation.py), [tools/verification/compare_banner_background.py](../../tools/verification/compare_banner_background.py), [tools/banner-blend-check/Program.cs](../../tools/banner-blend-check/Program.cs), [tools/verification/compare_banner_hide.py](../../tools/verification/compare_banner_hide.py), [tools/verification/compare_banner_reveal.py](../../tools/verification/compare_banner_reveal.py), [tools/verification/inspect_banner_readiness.py](../../tools/verification/inspect_banner_readiness.py), [tools/verification/verify_banner_triangle_colors.py](../../tools/verification/verify_banner_triangle_colors.py), [tools/verification/inspect_banner_circle.py](../../tools/verification/inspect_banner_circle.py), [tools/verification/inspect_banner_hide_circle.py](../../tools/verification/inspect_banner_hide_circle.py), [tools/verification/inspect_banner_event.py](../../tools/verification/inspect_banner_event.py), [src/Zodiac.Framework/Events/OrderedBannerProgram.cs](../../src/Zodiac.Framework/Events/OrderedBannerProgram.cs), [src/Zodiac.Framework/Events/OrderedBannerResult.cs](../../src/Zodiac.Framework/Events/OrderedBannerResult.cs), [src/Zodiac.Framework/Events/OrderedBannerExecutor.cs](../../src/Zodiac.Framework/Events/OrderedBannerExecutor.cs), [src/Zodiac.Content/Events/PreparedOrderedBannerEvent.cs](../../src/Zodiac.Content/Events/PreparedOrderedBannerEvent.cs), [src/Zodiac.AssetTool.Cli/PrepareOrderedBannerEventCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareOrderedBannerEventCommand.cs), [src/Fft.Client/Presentation/OrderedBannerPreview.cs](../../src/Fft.Client/Presentation/OrderedBannerPreview.cs), [src/Zodiac.Framework/Events/ReadyDisplayState.cs](../../src/Zodiac.Framework/Events/ReadyDisplayState.cs), [src/Zodiac.Framework/Events/ReadySceneProgram.cs](../../src/Zodiac.Framework/Events/ReadySceneProgram.cs), [src/Zodiac.Framework/Events/ReadySceneResult.cs](../../src/Zodiac.Framework/Events/ReadySceneResult.cs), [src/Zodiac.Framework/Events/ReadySceneExecutor.cs](../../src/Zodiac.Framework/Events/ReadySceneExecutor.cs), [src/Zodiac.AssetTool/ReadyArtworkDecoder.cs](../../src/Zodiac.AssetTool/ReadyArtworkDecoder.cs), [src/Zodiac.AssetTool.Cli/ReadyEventExporter.cs](../../src/Zodiac.AssetTool.Cli/ReadyEventExporter.cs), [src/Zodiac.AssetTool.Cli/PrepareReadyCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareReadyCommand.cs), [src/Zodiac.Content/PreparedReadyArtwork.cs](../../src/Zodiac.Content/PreparedReadyArtwork.cs), [src/Zodiac.Content/Events/PreparedReadyEvent.cs](../../src/Zodiac.Content/Events/PreparedReadyEvent.cs), [src/Fft.Client/Presentation/ReadyPresentation.cs](../../src/Fft.Client/Presentation/ReadyPresentation.cs), [src/Fft.Client/Presentation/ReadyPreview.cs](../../src/Fft.Client/Presentation/ReadyPreview.cs), [tools/verification/compare_ready_display.py](../../tools/verification/compare_ready_display.py), [tools/banner-blend-check/ReadySequence.cs](../../tools/banner-blend-check/ReadySequence.cs), [tools/verification/inspect_ready_interrupt.py](../../tools/verification/inspect_ready_interrupt.py), [tools/verification/compare_ready_birth.py](../../tools/verification/compare_ready_birth.py), [src/Zodiac.Framework/Events/BannerReadySequence.cs](../../src/Zodiac.Framework/Events/BannerReadySequence.cs), [src/Zodiac.Content/Events/PreparedBannerReadySequence.cs](../../src/Zodiac.Content/Events/PreparedBannerReadySequence.cs), [src/Zodiac.AssetTool.Cli/PrepareBannerReadySequenceCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareBannerReadySequenceCommand.cs), [src/Fft.Client/Presentation/BannerReadyPreview.cs](../../src/Fft.Client/Presentation/BannerReadyPreview.cs), [src/Fft.Client/Presentation/IntroductionBattleSession.cs](../../src/Fft.Client/Presentation/IntroductionBattleSession.cs), [src/Fft.Client/Presentation/PlayableBattleWindow.Introduction.cs](../../src/Fft.Client/Presentation/PlayableBattleWindow.Introduction.cs), [src/Fft.Client/Presentation/BattleInputBuffer.cs](../../src/Fft.Client/Presentation/BattleInputBuffer.cs), [src/Zodiac.AssetTool/DarkScreenDecoder.cs](../../src/Zodiac.AssetTool/DarkScreenDecoder.cs), [src/Zodiac.AssetTool.Cli/PrepareDarkScreenCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareDarkScreenCommand.cs), [src/Zodiac.Content/PreparedDarkScreen.cs](../../src/Zodiac.Content/PreparedDarkScreen.cs), [src/Fft.Client/Presentation/DarkScreenPresentation.cs](../../src/Fft.Client/Presentation/DarkScreenPresentation.cs), [src/Fft.Client/Presentation/DarkScreenPreview.cs](../../src/Fft.Client/Presentation/DarkScreenPreview.cs), [tools/banner-blend-check/DarkScreenSequence.cs](../../tools/banner-blend-check/DarkScreenSequence.cs), [tools/verification/inspect_dark_screen_reference.py](../../tools/verification/inspect_dark_screen_reference.py), [tools/verification/compare_dark_screen_reference.py](../../tools/verification/compare_dark_screen_reference.py), [tools/verification/inspect_dark_screen_intro.py](../../tools/verification/inspect_dark_screen_intro.py), [src/Zodiac.Framework/Events/DarkScreenIntroduction.cs](../../src/Zodiac.Framework/Events/DarkScreenIntroduction.cs), [src/Zodiac.Content/Events/PreparedDarkScreenIntroduction.cs](../../src/Zodiac.Content/Events/PreparedDarkScreenIntroduction.cs), [src/Fft.Client/Presentation/DarkScreenIntroductionSession.cs](../../src/Fft.Client/Presentation/DarkScreenIntroductionSession.cs).

Existing bounded checks: [tests/Fft.Client.Tests/PreparedSceneGeometryTests.cs](../../tests/Fft.Client.Tests/PreparedSceneGeometryTests.cs), [tools/verification/test_visual_checkpoints.py](../../tools/verification/test_visual_checkpoints.py), [tools/verification/test_retroarch_reference.py](../../tools/verification/test_retroarch_reference.py), [tools/verification/test_observed_banner.py](../../tools/verification/test_observed_banner.py), [tools/verification/test_banner_precision.py](../../tools/verification/test_banner_precision.py), [tests/Fft.Client.Tests/FiveBitGeneralBannerPresentationTests.cs](../../tests/Fft.Client.Tests/FiveBitGeneralBannerPresentationTests.cs), [tools/verification/test_native_banner.py](../../tools/verification/test_native_banner.py), [tests/Fft.Client.Tests/ObservedBannerContinuationTests.cs](../../tests/Fft.Client.Tests/ObservedBannerContinuationTests.cs), [tools/verification/test_banner_continuation.py](../../tools/verification/test_banner_continuation.py), [tests/Fft.Client.Tests/BannerBackgroundCompositionTests.cs](../../tests/Fft.Client.Tests/BannerBackgroundCompositionTests.cs), [tools/verification/test_banner_background.py](../../tools/verification/test_banner_background.py), [tools/verification/test_banner_hide.py](../../tools/verification/test_banner_hide.py), [tests/Fft.Client.Tests/ObservedBannerRevealTests.cs](../../tests/Fft.Client.Tests/ObservedBannerRevealTests.cs), [tools/verification/test_banner_reveal.py](../../tools/verification/test_banner_reveal.py), [tests/Fft.Client.Tests/BannerTriangleColorTests.cs](../../tests/Fft.Client.Tests/BannerTriangleColorTests.cs), [tools/verification/test_banner_triangle_colors.py](../../tools/verification/test_banner_triangle_colors.py), [tests/Fft.Client.Tests/ObservedBannerCircleTests.cs](../../tests/Fft.Client.Tests/ObservedBannerCircleTests.cs), [tools/verification/test_banner_circle.py](../../tools/verification/test_banner_circle.py), [tools/verification/test_banner_hide_circle.py](../../tools/verification/test_banner_hide_circle.py), [tools/verification/test_banner_event.py](../../tools/verification/test_banner_event.py), [tests/Zodiac.Framework.Tests/OrderedBannerContractTests.cs](../../tests/Zodiac.Framework.Tests/OrderedBannerContractTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/OrderedBannerEventExporterTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/OrderedBannerEventExporterTests.cs), [tests/Zodiac.Content.Tests/PreparedOrderedBannerEventTests.cs](../../tests/Zodiac.Content.Tests/PreparedOrderedBannerEventTests.cs), [tests/Fft.Client.Tests/OrderedBannerSessionTests.cs](../../tests/Fft.Client.Tests/OrderedBannerSessionTests.cs), [tests/Zodiac.Framework.Tests/ReadySceneContractTests.cs](../../tests/Zodiac.Framework.Tests/ReadySceneContractTests.cs), [tests/Zodiac.AssetTool.Tests/ReadyArtworkDecoderTests.cs](../../tests/Zodiac.AssetTool.Tests/ReadyArtworkDecoderTests.cs), [tests/Zodiac.Content.Tests/PreparedReadyTests.cs](../../tests/Zodiac.Content.Tests/PreparedReadyTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/ReadyPreparationTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/ReadyPreparationTests.cs), [tests/Fft.Client.Tests/ReadyPresentationTests.cs](../../tests/Fft.Client.Tests/ReadyPresentationTests.cs), [tools/verification/test_ready_display.py](../../tools/verification/test_ready_display.py), [tools/verification/test_ready_interrupt.py](../../tools/verification/test_ready_interrupt.py), [tests/Zodiac.Framework.Tests/BannerReadySequenceTests.cs](../../tests/Zodiac.Framework.Tests/BannerReadySequenceTests.cs), [tests/Zodiac.Content.Tests/PreparedBannerReadySequenceTests.cs](../../tests/Zodiac.Content.Tests/PreparedBannerReadySequenceTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/BannerReadyPreparationTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/BannerReadyPreparationTests.cs), [tests/Fft.Client.Tests/BannerReadySessionTests.cs](../../tests/Fft.Client.Tests/BannerReadySessionTests.cs), [tools/verification/test_banner_ready_sequence.py](../../tools/verification/test_banner_ready_sequence.py), [tools/verification/test_ready_handoff.py](../../tools/verification/test_ready_handoff.py), [tests/Zodiac.Framework.Tests/BattleIntroductionTests.cs](../../tests/Zodiac.Framework.Tests/BattleIntroductionTests.cs), [tests/Fft.Client.Tests/IntroductionBattleSessionTests.cs](../../tests/Fft.Client.Tests/IntroductionBattleSessionTests.cs), [tools/verification/test_intro_battle.py](../../tools/verification/test_intro_battle.py), [tools/verification/test_dark_screen.py](../../tools/verification/test_dark_screen.py), [tests/Zodiac.Framework.Tests/DarkScreenContractTests.cs](../../tests/Zodiac.Framework.Tests/DarkScreenContractTests.cs), [tests/Zodiac.AssetTool.Tests/DarkScreenDecoderTests.cs](../../tests/Zodiac.AssetTool.Tests/DarkScreenDecoderTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/DarkScreenPreparationTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/DarkScreenPreparationTests.cs), [tests/Zodiac.Content.Tests/PreparedDarkScreenTests.cs](../../tests/Zodiac.Content.Tests/PreparedDarkScreenTests.cs), [tests/Fft.Client.Tests/DarkScreenPresentationTests.cs](../../tests/Fft.Client.Tests/DarkScreenPresentationTests.cs), [tools/verification/test_dark_screen_preview.py](../../tools/verification/test_dark_screen_preview.py), [tools/verification/test_dark_screen_reference.py](../../tools/verification/test_dark_screen_reference.py), [tools/verification/test_dark_screen_intro.py](../../tools/verification/test_dark_screen_intro.py), [tests/Zodiac.Framework.Tests/DarkScreenIntroductionContractTests.cs](../../tests/Zodiac.Framework.Tests/DarkScreenIntroductionContractTests.cs), [tests/Zodiac.Framework.Tests/DarkScreenIntroductionBoundaryTests.cs](../../tests/Zodiac.Framework.Tests/DarkScreenIntroductionBoundaryTests.cs), [tests/Fft.Client.Tests/DarkScreenIntroductionSessionTests.cs](../../tests/Fft.Client.Tests/DarkScreenIntroductionSessionTests.cs), [tools/verification/verify_dark_screen_introduction.py](../../tools/verification/verify_dark_screen_introduction.py).

Manual checkpoint (bounded-native-foreground-observed-wider-fidelity-pending): Two finite native Windows runs with CPU/GPU readback at 15 checkpoints; inspect hold PNG and compare exact foreground with explicit original draw state. Retain raw regional/background differences and negative dither control. Inspect the original continuation gallery through hide/exit; phases come from saved task counters, and screenshot latency is uncalibrated. Compare the new original / transparent-palette control / actual C# gallery; the owner confirmed the original and C# views look identical on 2026-10-24; wider fidelity remains pending. The owner confirmed the displayed hide-frame gallery looks correct on 2026-10-24, including the small tail and fully hidden frame. Approval covers these five displayed hide anchors; early reveal/loading, physical Circle input, event identity and broader scene rendering remain open. The owner visually approved scratch/banner-reveal-d/review.html on 2026-10-24, stating that the gallery looks good. This approval covers the displayed early-reveal gallery; the two measured pixel differences remain failures and their cause remains open. The triangle color follow-up closes those two measured differences at zero tolerance. The owner visually approved the corrected gallery scratch/banner-triangle-colors-a/review.html on 2026-10-24, stating: Looks good to me. Approval covers the displayed corrected early-reveal gallery; physical Circle input, original event identity and wider rendering fidelity remain open. A subsequent owner-operated physical reveal Circle press is supported by paired original saved input/phase fields and repeated no-input task exit. This is a behavioral checkpoint; no new gallery approval or USB/display latency claim. The prepared neutral hold checkpoint in private slot 1 still needs a physical press observation. The owner subsequently performed the hold-Circle sequence; the frozen neutral/pressed states and repeated next-update exit verify 20/1/0 progress. No new visual-latency or gallery-approval claim. The owner subsequently performed the hide-Circle sequence in slot 2. Frozen neutral/pressed states show normal hide progress 1 -> 2 despite Circle-only input; no new gallery approval or display-timing claim. Separately review ZOD-087 original/control/C# gallery at scratch/ready-displayed-087-e/review.html; six anchors match exactly and the owner approved the gallery on 2026-10-25: Visually, they look identical to my eyes. Birth frames remain missing. ZOD-088 closes the missing birth rows with exact original/control/C# comparisons at scratch/ready-birth-088-b/review.html; the owner separately approved this two-row gallery on 2026-10-25: That looks correct. Review ZOD-089 authored general-banner→Ready playback at scratch/banner-ready-089-review/review.html; independent full-frame checks pass, and the owner approved it on 2026-10-25: This looks great. ZOD-090 reuses retained Ready birth and dispatcher-clear states for source/gate observation only; no new visual approval or player-control observation. ZOD-091 will need its own native introduction-to-battle review. Review scratch/intro-battle-091-review/review.html: natural/skipped authored flow, clean intro-to-battle switch and no automatic first turn. The owner approved this authored gallery on 2026-10-25: This looks good. No original startup or physical input timing approval is inferred. ZOD-092 reuses three retained holding states for source/math/material binding only; no new visual approval, original entry/release scanout or duration observation. ZOD-093 is a headless logical milestone with no new native gallery or capture. Its full managed regression run does not replace later flat-quad visual validation. Review scratch/dark-screen-094-review/review.html: entering tiles, full hold and collapsing return to checker across normal/earliest/longest authored holds. The owner approved the ZOD-094 authored gallery on 2026-10-25: That looks good. No original entry/release comparison claim; no implementation suites rerun for approval. Review ZOD-095 original/control/C# rows at scratch/dark-screen-095-f/review.html, especially overlapping entry tiles, held flash, release and 24-pixel tail; the owner approved these thirteen selected rows on 2026-10-25: This looks good. Empty ticks 2/213 have no visible coverage. No original duration or full-startup approval is inferred. ZOD-096 is a source/design milestone with no new gallery. Its queued-layer observations do not establish combined displayed pixels; ZOD-098 will need a separate authored composition/battle-handoff gallery and approval. ZOD-097 is a headless pure implementation with no new visual approval. Its complete managed suite does not execute the sixteen future prepared/host/manual cases; ZOD-098 will need its own finite native checks and owner gallery. ZOD-098: review scratch/dark-screen-intro-098-review/review.html for entry, dark under general/Ready, hidden gaps, release and battle switch. The owner approved this authored gallery on 2026-10-26: Looks good to me. Original startup/duration approval is not inferred.

Remaining gaps:

- Selected banner/Ready/DarkScreen anchors have exact original/emulator comparisons; broader visual equivalence, PS1 effects/order variants, missing endpoints and physical display timing remain pending.
- Physical reveal Circle has one bounded paired observation; hide now has one bounded ignored-Circle observation; repeatable BSV playback, later natural completion after that press and USB/display timing remain unverified.
- Selected banner states are now identified as event 4. The earlier event-entry/conditional selection and common BattleStart event 401 activation remain unobserved; complete event playback is unsupported.
- Typed ordered execution and the authored preloaded profile are proven; broader scene workers and original event playback remain unsupported. Native startup duration, additional scene capabilities and complete event playback remain separate.
- Eight selected Ready displayed anchors are proven separately; original Ready loading/completion/input/display duration, other variants and full startup remain open.
- Thirteen selected DarkScreen scanouts match exactly; birth/transition/final yield/clear/parent endpoints, full animation, other styles, original duration and combined prepared/Windows scene integration remain open.

Next action: ZOD-098 authored composition is approved. Select the next bounded milestone through the backlog; original startup/duration, missing endpoints, physical timing and whole-game fidelity remain open.

Bounded follow-ups: ZOD-080, ZOD-081, ZOD-082, ZOD-083, ZOD-084, ZOD-085, ZOD-086, ZOD-087, ZOD-088, ZOD-089, ZOD-090, ZOD-091, ZOD-092, ZOD-093, ZOD-094, ZOD-095, ZOD-096, ZOD-097, ZOD-098.

### COV-PRE-002 — Unit sprites, SHP/SEQ animation and battle poses

Status: **partial**; scope: **baseline**; ticket: **ZOD-044**; owner area: Fft.Client platform adapters.

A selected six-piece held Chocobo pose is prepared/loaded/rendered; no general sequence playback.

Evidence: [docs/development/prepared-sprite-pose.md](../../docs/development/prepared-sprite-pose.md), [docs/architecture/zod-027-chocobo-idle-pose.md](../../docs/architecture/zod-027-chocobo-idle-pose.md).

Source entry points (pinned revision above; not full traces): [include/fft/animation.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/animation.h), [src/battle/units/animation_policy.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/units/animation_policy.c), [src/battle/units/animation_motion.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/units/animation_motion.c).

Current implementation: [src/Zodiac.AssetTool/OrdinaryUnitPoseDecoder.cs](../../src/Zodiac.AssetTool/OrdinaryUnitPoseDecoder.cs), [src/Zodiac.Content/PreparedSpritePose.cs](../../src/Zodiac.Content/PreparedSpritePose.cs).

Existing bounded checks: [tests/Zodiac.AssetTool.Tests/OrdinaryUnitPoseTests.cs](../../tests/Zodiac.AssetTool.Tests/OrdinaryUnitPoseTests.cs), [tests/Zodiac.Content.Tests/PreparedSpritePoseTests.cs](../../tests/Zodiac.Content.Tests/PreparedSpritePoseTests.cs).

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

Status: **partial**; scope: **baseline**; ticket: **ZOD-044**; owner area: Fft.Client platform adapters.

ZOD-078 prepares condition-8/image-0 artwork from verified local inputs, strictly loads immutable six-piece data and previews accepted effect snapshots with a preloaded RGB8 CPU/Windows host. All 20 authored expectations have regressions; two preparations and two finite Windows capture sets match, with exact CPU/GPU readback and prepared-only runtime paths. Original comparison, parity and loading latency remain open. Other font/menu/world/effect/audio families remain unsupported.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md), [docs/architecture/zod-077-general-banner-assets.md](../../docs/architecture/zod-077-general-banner-assets.md), [docs/evidence/events/general-banner-assets-v1.json](../../docs/evidence/events/general-banner-assets-v1.json), [docs/development/prepared-general-banner.md](../../docs/development/prepared-general-banner.md).

Source entry points (pinned revision above; not full traces): [src/world/text/font_rendering.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/text/font_rendering.c), [src/lib/event/dialog/typewriter.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/dialog/typewriter.c), [src/lib/menu/unit_view/portraits.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/menu/unit_view/portraits.c), [src/event/require/conditions.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/conditions.c).

Current implementation: [src/Zodiac.AssetTool/GeneralBannerDecoder.cs](../../src/Zodiac.AssetTool/GeneralBannerDecoder.cs), [src/Zodiac.AssetTool.Cli/PrepareGeneralBannerCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareGeneralBannerCommand.cs), [src/Zodiac.Content/PreparedGeneralBanner.cs](../../src/Zodiac.Content/PreparedGeneralBanner.cs), [src/Fft.Client/Presentation/GeneralBannerPresentation.cs](../../src/Fft.Client/Presentation/GeneralBannerPresentation.cs), [src/Fft.Client/Presentation/GeneralBannerPreview.cs](../../src/Fft.Client/Presentation/GeneralBannerPreview.cs).

Existing bounded checks: [tests/Zodiac.AssetTool.Tests/GeneralBannerDecoderTests.cs](../../tests/Zodiac.AssetTool.Tests/GeneralBannerDecoderTests.cs), [tests/Zodiac.Content.Tests/PreparedGeneralBannerTests.cs](../../tests/Zodiac.Content.Tests/PreparedGeneralBannerTests.cs), [tests/Fft.Client.Tests/GeneralBannerPresentationTests.cs](../../tests/Fft.Client.Tests/GeneralBannerPresentationTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/PrepareGeneralBannerCommandTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/PrepareGeneralBannerCommandTests.cs).

Manual checkpoint (pending-original-observation): Compare original glyphs, menu/dialogue layout, portraits, typewriter timing and clipping.

Remaining gaps:

- Original resources, glyph decoding, portrait variants and text control semantics remain unprepared.

Next action: Review the bounded preview, then separately select the next source-traced scene/resource capability; original startup/opcode integration and original capture comparison remain open.

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

Status: **partial**; scope: **baseline**; ticket: **ZOD-044**; owner area: Zodiac.AssetTool preparation and Zodiac.Content loading.

Verified SCUS-94221 input and ordinary data-file extraction/inventory are implemented.

Evidence: [docs/asset-inventory-poc.md](../../docs/asset-inventory-poc.md).

Source entry points (pinned revision above; not full traces): [target/main.yaml](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/target/main.yaml), [src/main/file_requests.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/file_requests.c).

Current implementation: [src/Zodiac.AssetTool/Mode2RawImageReader.cs](../../src/Zodiac.AssetTool/Mode2RawImageReader.cs), [src/Zodiac.AssetTool/IsoDataFileExtractor.cs](../../src/Zodiac.AssetTool/IsoDataFileExtractor.cs).

Existing bounded checks: [tests/Zodiac.AssetTool.Tests/IsoDataFileExtractorTests.cs](../../tests/Zodiac.AssetTool.Tests/IsoDataFileExtractorTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/ExtractCommandTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/ExtractCommandTests.cs).

Manual checkpoint (pending-original-observation): Repeat local edition verification/import and confirm runtime works with source media unavailable.

Remaining gaps:

- Extraction is not decoding or complete resource-role inventory; mixed XA/STR payloads remain unsupported.

Next action: Assign conversion/load/provenance state to each required resource family.

### COV-AST-002 — Prepared manifests, integrity and disk-only runtime loading

Status: **partial**; scope: **baseline**; ticket: **ZOD-044**; owner area: Zodiac.AssetTool preparation and Zodiac.Content loading.

Versioned manifest and hash-verified loose loading exist for selected assets; no universal decoder/package manager.

Evidence: [docs/adr/0003-offline-asset-import-and-loose-runtime-content.md](../../docs/adr/0003-offline-asset-import-and-loose-runtime-content.md).

Source entry points (pinned revision above; not full traces): [src/main/file_loading.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/file_loading.c), [src/battle/map/loading.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/loading.c).

Current implementation: [src/Zodiac.Content/LooseAssetStore.cs](../../src/Zodiac.Content/LooseAssetStore.cs), [src/Zodiac.Content/PreparedAssetManifest.cs](../../src/Zodiac.Content/PreparedAssetManifest.cs).

Existing bounded checks: [tests/Zodiac.Content.Tests/PreparedAssetManifestTests.cs](../../tests/Zodiac.Content.Tests/PreparedAssetManifestTests.cs).

Manual checkpoint (pending-original-observation): Run a selected prepared scene with BIN/reference checkout unavailable and check missing/corrupt dependency diagnostics.

Remaining gaps:

- Complete asset dependency closure and startup selection are not established.

Next action: Record required logical IDs/formats for each chosen scenario.

### COV-AST-003 — Prepared map, sprite, shape and sequence converters

Status: **partial**; scope: **baseline**; ticket: **ZOD-044**; owner area: Zodiac.AssetTool preparation and Zodiac.Content loading.

One frozen MAP003 variant and one held Chocobo pose have bounded offline converters.

Evidence: [docs/development/prepared-map-slice.md](../../docs/development/prepared-map-slice.md), [docs/development/prepared-sprite-pose.md](../../docs/development/prepared-sprite-pose.md).

Source entry points (pinned revision above; not full traces): [src/battle/map/gns.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/gns.c), [include/fft/sprite.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/sprite.h), [include/fft/animation.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/animation.h).

Current implementation: [src/Zodiac.AssetTool.Cli/PrepareMapCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareMapCommand.cs), [src/Zodiac.AssetTool.Cli/PrepareSpritePoseCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareSpritePoseCommand.cs).

Existing bounded checks: [tests/Zodiac.AssetTool.Cli.Tests/PrepareMapCommandTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/PrepareMapCommandTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/PrepareSpritePoseCommandTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/PrepareSpritePoseCommandTests.cs).

Manual checkpoint (pending-original-observation): Repeat each selected conversion, compare input/output hashes and inspect independently matched original captures.

Remaining gaps:

- Other map variants, sheets, animation commands and special overlays/effects are outside current converters.

Next action: Expand one family/profile with explicit unsupported inputs and bounded fixtures.

### COV-AST-004 — Remaining fonts, menus, world, effects, audio and mixed streams

Status: **partial**; scope: **baseline**; ticket: **ZOD-044**; owner area: Zodiac.AssetTool preparation and Zodiac.Content loading.

ZOD-078 prepares condition-8/image-0 artwork from verified local inputs, strictly loads immutable six-piece data and previews accepted effect snapshots with a preloaded RGB8 CPU/Windows host. All 20 authored expectations have regressions; two preparations and two finite Windows capture sets match, with exact CPU/GPU readback and prepared-only runtime paths. Original comparison, parity and loading latency remain open. Other font/menu/world/effect/audio families remain unsupported.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md), [docs/architecture/zod-077-general-banner-assets.md](../../docs/architecture/zod-077-general-banner-assets.md), [docs/evidence/events/general-banner-assets-v1.json](../../docs/evidence/events/general-banner-assets-v1.json), [docs/development/prepared-general-banner.md](../../docs/development/prepared-general-banner.md).

Source entry points (pinned revision above; not full traces): [src/main/file_requests.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/file_requests.c), [src/battle/effects/resources.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/effects/resources.c), [src/world/screen/resources.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/screen/resources.c), [src/lib/event/dialog/backreferences.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/dialog/backreferences.c), [src/event/require/conditions.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/conditions.c).

Current implementation: [src/Zodiac.AssetTool/GeneralBannerDecoder.cs](../../src/Zodiac.AssetTool/GeneralBannerDecoder.cs), [src/Zodiac.AssetTool.Cli/PrepareGeneralBannerCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareGeneralBannerCommand.cs), [src/Zodiac.Content/PreparedGeneralBanner.cs](../../src/Zodiac.Content/PreparedGeneralBanner.cs), [src/Fft.Client/Presentation/GeneralBannerPresentation.cs](../../src/Fft.Client/Presentation/GeneralBannerPresentation.cs), [src/Fft.Client/Presentation/GeneralBannerPreview.cs](../../src/Fft.Client/Presentation/GeneralBannerPreview.cs).

Existing bounded checks: [tests/Zodiac.AssetTool.Tests/GeneralBannerDecoderTests.cs](../../tests/Zodiac.AssetTool.Tests/GeneralBannerDecoderTests.cs), [tests/Zodiac.Content.Tests/PreparedGeneralBannerTests.cs](../../tests/Zodiac.Content.Tests/PreparedGeneralBannerTests.cs), [tests/Fft.Client.Tests/GeneralBannerPresentationTests.cs](../../tests/Fft.Client.Tests/GeneralBannerPresentationTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/PrepareGeneralBannerCommandTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/PrepareGeneralBannerCommandTests.cs).

Manual checkpoint (pending-original-observation): Check each chosen family for repeatable conversion and actual runtime presentation with BIN absent.

Remaining gaps:

- Per-file role, decoder support, dependencies and byte provenance are unresolved for the remaining families; historical disc counts do not establish support.

Next action: Review the bounded preview, then separately select the next source-traced scene/resource capability; original startup/opcode integration and original capture comparison remain open.

## Persistence

### COV-SAV-001 — Bounded battle snapshot DTOs, migration and safe file writes

Status: **slice-proven**; scope: **platform-replacement**; ticket: **ZOD-035**; owner area: FFT persistence/application and Windows storage.

Schema 2/profile-content v1 restores 13 command boundaries, active turns, RNG, CT, HP/status and both outcomes; synthetic v1 migration and safe replacement are tested.

Evidence: [docs/development/two-unit-battle-saves.md](../../docs/development/two-unit-battle-saves.md).

Source entry points (pinned revision above; not full traces): [include/fft/save.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/save.h).

Current implementation: [src/Zodiac.Content/Saves/TwoUnitBattleSave.cs](../../src/Zodiac.Content/Saves/TwoUnitBattleSave.cs), [src/Zodiac.Content/Saves/TwoUnitBattleSave.Files.cs](../../src/Zodiac.Content/Saves/TwoUnitBattleSave.Files.cs).

Existing bounded checks: [tests/Zodiac.Content.Tests/Saves/TwoUnitBattleSaveTests.cs](../../tests/Zodiac.Content.Tests/Saves/TwoUnitBattleSaveTests.cs), [tests/Zodiac.Content.Tests/Saves/TwoUnitBattleSaveFileTests.cs](../../tests/Zodiac.Content.Tests/Saves/TwoUnitBattleSaveFileTests.cs), [tests/Fft.Client.Tests/BattleSaveCheckTests.cs](../../tests/Fft.Client.Tests/BattleSaveCheckTests.cs).

Manual checkpoint (pending-original-observation): Use the headless save check now; later test player-facing save/continue at original legal campaign checkpoints.

Remaining gaps:

- This is a managed synthetic contract, not original save availability, full campaign state, a memory-card format or save UI.

Next action: Carry its DTO/validation/atomic-write pattern into separately traced campaign persistence.

### COV-SAV-002 — Campaign/party/inventory/world saves and content-version policy

Status: **partial**; scope: **platform-replacement**; ticket: **ZOD-053**; owner area: FFT persistence/application and Windows storage.

ZOD-067 retains historical source save-family/lifetime/restore-order context. ZOD-068 proves only invented RNG-free idle-world state, fresh-definition restore, strict DTO/checksum/summary validation, schema-1 migration and next-command state/events. Original campaign serialization and RNG continuation remain unsupported.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md), [docs/architecture/zod-067-campaign-save-state.md](../../docs/architecture/zod-067-campaign-save-state.md), [docs/development/synthetic-world-checkpoint.md](../../docs/development/synthetic-world-checkpoint.md), [docs/evidence/saves/synthetic-world-checkpoint-v1.json](../../docs/evidence/saves/synthetic-world-checkpoint-v1.json).

Source entry points (pinned revision above; not full traces): [include/fft/save.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/save.h), [src/main/save_state.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/save_state.c), [src/world/card_save.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/card_save.c).

Current implementation: [src/Zodiac.Framework/Campaign/WorldCheckpoint.cs](../../src/Zodiac.Framework/Campaign/WorldCheckpoint.cs), [src/Zodiac.Content/Samples/SyntheticWorldCheckpoint.cs](../../src/Zodiac.Content/Samples/SyntheticWorldCheckpoint.cs), [src/Zodiac.Content/Saves/WorldCheckpointSave.cs](../../src/Zodiac.Content/Saves/WorldCheckpointSave.cs).

Existing bounded checks: [tests/Zodiac.Framework.Tests/WorldCheckpointTests.cs](../../tests/Zodiac.Framework.Tests/WorldCheckpointTests.cs), [tests/Zodiac.Content.Tests/Saves/WorldCheckpointSaveTests.cs](../../tests/Zodiac.Content.Tests/Saves/WorldCheckpointSaveTests.cs), [tools/verification/verify_world_checkpoint.py](../../tools/verification/verify_world_checkpoint.py).

Manual checkpoint (pending-original-observation): Save/continue at selected legal campaign points and compare party, flags, inventories, calendar and RNG-sensitive behavior.

Remaining gaps:

- Complete retail checkpoint enumeration, unmodeled fields/side content, original observations, content-version rules and explicit native RNG continuation/seed policy remain open.

Next action: Retain ZOD-053 for original campaign fields/checkpoints, content compatibility and RNG policy. Expose the bounded proof through ZOD-069 after ZOD-103 native navigation.

Bounded follow-ups: ZOD-068, ZOD-069, ZOD-102, ZOD-103.

### COV-SAV-003 — Windows save storage, metadata, backups and recovery

Status: **partial**; scope: **platform-replacement**; ticket: **ZOD-053**; owner area: FFT persistence/application and Windows storage.

ZOD-068 proves ten safe headless synthetic slots with a per-user default, validated metadata, content revisions and exclusive locks, flushed sibling promotion/backups, candidate-only confirmed load and explicit validated recovery. Injected publishing faults and actual Windows sharing failure preserve prior files/live state. No slot UI or full-system durability claim.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md), [docs/architecture/zod-067-campaign-save-state.md](../../docs/architecture/zod-067-campaign-save-state.md), [docs/development/synthetic-world-checkpoint.md](../../docs/development/synthetic-world-checkpoint.md), [docs/evidence/saves/synthetic-world-checkpoint-v1.json](../../docs/evidence/saves/synthetic-world-checkpoint-v1.json).

Source entry points (pinned revision above; not full traces): [src/world/card_save.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/card_save.c), [include/fft/save.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/save.h).

Current implementation: [src/Zodiac.Content/Saves/WorldSaveSlotStore.cs](../../src/Zodiac.Content/Saves/WorldSaveSlotStore.cs), [src/Zodiac.Content/Saves/WorldCheckpointService.cs](../../src/Zodiac.Content/Saves/WorldCheckpointService.cs), [src/Fft.Client/WorldCheckpointCheck.cs](../../src/Fft.Client/WorldCheckpointCheck.cs).

Existing bounded checks: [tests/Zodiac.Content.Tests/Saves/WorldSaveSlotStoreTests.cs](../../tests/Zodiac.Content.Tests/Saves/WorldSaveSlotStoreTests.cs), [tests/Fft.Client.Tests/WorldCheckpointCheckTests.cs](../../tests/Fft.Client.Tests/WorldCheckpointCheckTests.cs), [tools/verification/verify_world_checkpoint.py](../../tools/verification/verify_world_checkpoint.py).

Manual checkpoint (pending-original-observation): After ZOD-069 implements menus, review actual overwrite/discard/cancel/recovery decisions and error presentation. Headless storage failures and recovery are automated in ZOD-068.

Remaining gaps:

- Native slot/menu presentation remains unimplemented; original campaign compatibility, broader storage environments and power-loss durability require later scope and evidence.

Next action: Implement ZOD-103 native navigation, then ZOD-069 slot UI; retain ZOD-053 for full campaign persistence and broader storage validation.

Bounded follow-ups: ZOD-068, ZOD-069, ZOD-102, ZOD-103.

## Validation

### COV-VAL-001 — Managed source vectors, deterministic replays and scenario regressions

Status: **partial**; scope: **baseline**; ticket: **ZOD-046**; owner area: Development validation.

Movement/clock/sword/scenario/save checks exist; last recorded baseline is 954 passing tests at managed commit 7990b5d.

Evidence: [docs/behavior/fixture-runner.md](../../docs/behavior/fixture-runner.md), [docs/behavior/two-unit-battle-scenario.md](../../docs/behavior/two-unit-battle-scenario.md), [docs/development/two-unit-battle-saves.md](../../docs/development/two-unit-battle-saves.md).

Source entry points (pinned revision above; not full traces): [src/battle/turn/clock.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/turn/clock.c), [src/battle/combat/formulas.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/formulas.c).

Current implementation: [tests/Zodiac.Framework.Tests/Fixtures/MovementFixtureRunner.cs](../../tests/Zodiac.Framework.Tests/Fixtures/MovementFixtureRunner.cs), [tests/Zodiac.Framework.Tests/Fixtures/AttackFixtureRunner.cs](../../tests/Zodiac.Framework.Tests/Fixtures/AttackFixtureRunner.cs).

Existing bounded checks: [tests/Zodiac.Framework.Tests/Fixtures/MovementFixtureRunnerTests.cs](../../tests/Zodiac.Framework.Tests/Fixtures/MovementFixtureRunnerTests.cs), [tests/Zodiac.Framework.Tests/AttackFixtureV3Tests.cs](../../tests/Zodiac.Framework.Tests/AttackFixtureV3Tests.cs).

Manual checkpoint (pending-original-observation): Compare critical supported behavior against independent original/emulator observations, keeping source-derived and synthetic expectations distinct.

Remaining gaps:

- Most original systems lack regressions; passing source-derived tests is not complete original equivalence.

Next action: Add fixture/scenario evidence when each row's supported domain expands.

### COV-VAL-002 — Independent original sprite/scene/audio capture and comparison

Status: **partial**; scope: **baseline**; ticket: **ZOD-026**; owner area: Development validation.

ZOD-026 executes the verified original SCUS-94221 Chocobo attack decoder/helper instructions under Unicorn 2.1.4. Two independent-execution captures/reports match the historical managed decoded-byte digest; wrong-hash and existing-output gates pass. No original game, palette/frame/camera/timing or rendered scene comparison is claimed. ZOD-033 adds strict optional headless authored/same-environment PNG comparison, checkpoint/tick/view/turn-projection bindings, regional RGBA diagnostics and zero-tolerance synthetic Windows plans. All 22 Python tests pass; two 120-frame synthetic captures and reports match. No original rendered comparison or full simulation equivalence is claimed. The existing RetroArch 1.22.2/Beetle PSX software installation now has an isolated native capture profile. Two input-disconnected finite boots reach introductory game content with matching raw screenshots and unchanged installed settings; the owner subsequently captured the target banner/state, and two four-frame state restores produce identical banner scene images. Exact original/managed timing/context, BSV playback and original comparisons remain open. All 30 Python tests pass, with 8 new profile/isolation checks. A separate full-visible observed banner profile now records explicit measured borders and context bindings. Two diagnostic reports/review pages match. Manual inspection finds consistent apparent text placement; strict equality fails at 1,460/1,577 managed-background-invariant samples (original equal or darker by up to 16 RGB levels). No fidelity threshold is selected or runtime changed. All 52 Python tests pass (22 new). A subsequent developer precision study reads the frozen GPU dtd=0/displayed VRAM and exactly matches all 1,577 prior core samples with source-informed five-bit arithmetic, quantized vertical colors and zero-padded emulator output. Two reports match; all 66 Python tests pass (14 new). No runtime profile is changed, and installed-DLL source identity/per-primitive draw-state provenance remain unverified. A subsequent bounded Client five-bit profile now uses explicit per-draw dither/origin state. Frozen DMA-chain reconstruction identifies the REQUIRE dark-screen mode before all six banner packets. Actual native captures match all 1,577 selected original glyph pixels exactly, two capture sets repeat, and all 16 legacy RGB8 capture files retain historical bytes. The dither-on negative control distinguishes draw-state and pixel failures. Locked restore, zero-warning Release build, 1,418 .NET tests (30 new) and 81 Python tests (15 new) pass; timing, background-sensitive blending and broader integration remain open. Original saved-task continuation now identifies the entry as reveal counter 18 and reproduces late reveal, sampled hold boundaries, every hide iteration and natural exit. Two 17-checkpoint capture sets repeat PNGs, exit states and task/quad observations. Eighteen metadata-only managed replay cases match observed phases/counters/crops, with task exit at checkpoint 91 and later slot reuse rejected. Locked restore, zero-warning Release build, 1,436 .NET tests and 92 Python tests pass. Full reveal/loading, physical Circle input, event identity, screenshot/task latency and background blending/coverage remain open. A controlled transparent-palette original state probe now isolates a matched background at the full-visible anchor: all 28 non-GPU subsystems and GPU registers match, and all 58,312 pixels outside the sample footprint stay unchanged. Actual C# ComposeOver output matches all 3,128 affected pixels, including 1,551 previously excluded background-sensitive samples. Two original/probe/C# capture sets repeat, default checker GPU captures retain all 16 historical files, and zero-warning builds with 1,450 .NET and 104 Python tests pass. This is one intentionally altered-state control, not managed scene rendering or general PS1 fidelity; partial pixels/timing, early reveal/loading/input and event identity remain open. Source-bound displayed hide comparisons now decode the pre-submission VSync stack, pending environments and two-buffer order, then bind the front surface to a hash-bound earlier submission. All five original/probe PNG crops match saved scanout. Actual C# matches 6,917 affected temporal samples across four visible hide anchors and preserves the fully hidden background; controlled states retain all 28 non-GPU subsystems/GPU registers. Two runs repeat. Locked restores, zero-warning builds, 1,450 .NET tests and 116 Python tests pass. This does not establish a universal frame delay, early reveal/loading/input, event identity or managed scene rendering. Early reveal/readiness observations now classify four loader yields and the first pre-reveal yield. Sixteen metadata-only managed replay cases match source counters/crops. Actual C# matches nine of eleven displayed frames; reveal 4/13 differ at 21/14 blue-channel pixels by +8 original minus C#. Isolation, scanout, geometry and C#/Python checks pass. Two runs repeat 107 artifacts. Locked restores, zero-warning builds, 1,466 .NET and 130 Python tests pass. No runtime arithmetic or tolerance change; exact early-reveal fidelity remains open. A bounded triangle color trace now seeds TL/TR/BL at TL and TR/BL/BR at BL using the source-derived diagonal. Actual corrected C# matches all eleven retained original reveal frames, changing only the earlier 21/14 blue-channel pixels. Two runs repeat 57 artifacts; full-visible/hide CPU outputs remain unchanged. Two five-bit Windows runs preserve fifteen prior PNGs; only receipt policy text changes. RGB8 preserves all sixteen files. Locked restores, zero-warning builds, 1,486 .NET and 135 Python tests pass. No simulation, loading, schema or dependency changes; no general hardware/rasterizer proof. A paired owner-operated USB-controller reveal Circle observation now binds neutral reveal count 5 to Circle-only saved input, unchanged count and the shared extra exit yield. Two no-input continuations clear the task on the next source update with repeated state/PNG bytes; two inspection reports repeat. Three metadata-only C# replay cases match retained 5/0/0 progress, extra yield and completion. Eight authored Python checks reject invalid witnesses. Locked restore, zero-warning build, 1,489 .NET and 143 Python tests pass. No runtime changes; physical hold/hide input, BSV playback, USB/display latency and event identity remain open. A subsequent physical hold-Circle pair verifies Holding count 1 -> extra exit yield -> cleared task at source frames 9000/9001/9002, retaining 20/1/0 progress. Two dedicated three-frontend-frame continuations repeat state/PNG bytes and two inspector reports repeat. A four-frame continuation is correctly rejected after slot reuse at frame 9003. Six combined metadata-only C# checkpoints and ten authored Python witness checks pass; latest zero-warning Release build has 1,492 passing .NET tests, and all 145 Python tests pass. No runtime changes; hide-phase physical input, BSV playback, USB/display latency and event identity remain open. A subsequent owner-operated hide-phase Circle pair verifies source frames 6435/6436 and logical ticks 81/82: neutral input becomes Circle-only while the same active task advances hide count 1 -> 2, alternates buffer and queues six exact 8-row crops. No skip yield or exit cause occurs. Two inspector reports repeat; two additional metadata-only C# checkpoints match the unchanged driver. Seven authored Python hide-witness checks pass; latest zero-warning Release build has 1,494 passing .NET tests and all 152 Python tests pass. No emulator ran for this pair inspection; subsequent natural completion, BSV playback, USB/display latency and original event identity remain unverified. A source-bound saved-state study now identifies the captured banner as event 4, not common BattleStart event 401. All eight selected frozen states agree on current/cache IDs and a unique full archive payload match after the four-byte loader marker. The interpreter uses saved $fp for command offset: earliest state waits at 731; target banner command 804 has condition 8/hold 60 and the active worker matches the task-56 wait at 807. Scenario-active and correctly decoded packed flags support the scenario path. Two reports repeat; eleven authored verifier regressions pass and all 163 Python tests pass. No emulator/original instruction execution, runtime change or .NET rerun; last full managed count remains 1,494. Event entry/conditional selection, common event-401 activation and complete playback remain open. ZOD-080 now traces bounded opcode-78 admission and selects a distinct event-before-worker transaction. Retained original frames 9001/9002/9003 show parent wait 807 with live worker, wait 807 with cleared worker, then wait 813 with a new condition. Existing effect-first APIs remain unchanged. Twenty-eight authored expectations are unexecuted; ZOD-081 implements typed ordered Core behavior and ZOD-082 then admits the separate authored preloaded profile. No runtime/profile change, emulator run or suite rerun; original loading duration and full event playback remain open. ZOD-081 now implements typed ordered event/banner execution in Zodiac.Framework. All 27 authored Core expectations and 33 additional boundary cases pass, including H+31 task clear/H+32 parent resumption, newborn no-step, shared budgets, post-poll facts, protected identities and atomic late-failure rollback. Legacy effect-first APIs/traces remain unchanged. Locked restore, zero-warning Release build, all 1,554 .NET and 163 Python tests pass. BOP-028 and authored profile/preparation/Content/host integration remain ZOD-082; no original event, loading duration or graphics change. ZOD-082 now completes BOP-028 through a separate authored ordered-banner format/profile, raw/processed byte conversion, strict prepared Content loading/projection and preloaded Windows composition. Condition and hold decode as separate unsigned bytes. Old JSON/profiles/exports remain closed; unsupported/unreachable records, retail provenance and original events 4/401 fail. Two raw finite Windows runs repeat all 17 files; processed final state/trace/16 PNGs agree; task clear 91, parent exit 92. Skipped admission has zero tasks and finishes at tick 1; missing event/artwork fails before output. All 16 legacy RGB8 files repeat. Locked restore, zero-warning Release build, 1,629 .NET tests (75 new) and 163 Python tests pass. No new original capture, emulator execution, graphics algorithm, loader duration or full original playback is admitted. ZOD-083 selects condition-0 Ready as a separate dispatcher/child source contract, excluding condition 1. Verified image 0/palette 2/six full-corner glyphs need a distinct presentation contract. Two independent retained event-4 states bind command 810/wait 813, slots 1/3/4, resident code/tables/VRAM and queued scales 48/128; two study reports repeat. Selected source-derived child/dispatcher/event clear timing is R+8/R+9/R+10, not measured original duration. Thirty-two authored cases remain unexecuted; ZOD-084 implements the pure aggregate, ZOD-085 selects presentation and ZOD-086 integrates a separate authored prepared profile. No runtime/profile/test change or emulator run; last full tests remain 1,629 .NET / 163 Python. ZOD-084 now implements the pure typed condition-0 Ready aggregate with event/dispatcher/child order, first-yield draw initialization and no birth sample/double-step. Child clears R+8, named dispatcher R+9, waiting event R+10; protected identities, retained tombstones, explicit owner cancellation and full late-failure rollback are covered. All 28 historical Core cases and 25 additional boundaries execute, including full state/trace replay independent of snapshot reads. Locked restore, zero-warning Release build, 1,682 .NET and 163 Python tests pass. No graphics, Content/profile, emulator or original-duration equivalence change; four presentation/host contract cases remain ZOD-085/086 gates. ZOD-085 now selects a separate Ready RGB5 CPU policy with scaled four-corner triangle interpolation, opaque writes for 14 nonzero STP-clear bank-2 entries and average blending for its one STP entry. Explicit point/dither-off/zero-window/mask-off state, safe prepared bounds and resource-ready-before-scene ownership are documented. Two read-only source/queued-state/math reports repeat, with 97,776 channel comparisons and 3,072 average-component checks. Thirty-two authored presentation expectations remain unexecuted; ZOD-086 is now ready for the separate authored preparation/host implementation. No runtime renderer, test implementation, emulator or original Ready scanout/timing comparison changed. All 163 Python regressions pass; last full .NET validation remains ZOD-084 at 1,682 tests. ZOD-086 now implements the separate prepared Ready artwork/event profiles, verified offline conversion, strict immutable loading and accepted-snapshot RGB5 CPU/Windows composition. All 32 RPR and RDY-029..032 cases have checks; 112 new .NET tests bring the total to 1,794, with 163 Python tests passing, locked restore and zero-warning Release build. Two preparations/runs repeat, raw/processed state/trace/pixels agree, and 12 finite scenarios produce 626 exact CPU/GPU/independent-model PNGs. Final isolated host runs recreate resources without changing the 72 hold-60 artifacts. Actual readiness failures create no check output; legacy RGB8/RGB5/ordered artifacts remain byte-identical (16/16/17 files). No emulator/original Ready scanout or timing comparison; the owner approved the prepared gallery on 2026-10-25; original comparison remains open. ZOD-087 now owns bounded original Ready reference binding and displayed comparison. ZOD-087 compares six source-bound displayed Ready anchors (8/10/60/64/66/67) with actual prepared C# composition over isolated transparent-palette controls at zero tolerance. Each visible frame checks 1,202 opaque and 77 background-sensitive STP pixels; empty yield matches. Two final runs repeat 96 content artifacts. Worker/code/table/VRAM/event-4 and same-history two-buffer context bind queued to displayed work. No renderer/runtime change. Locked restores, zero-warning builds, 1,794 .NET and 179 Python tests pass. The owner approved the original gallery on 2026-10-25: Visually, they look identical to my eyes. Birth/next-frame endpoints remain unsupported inside interrupt handling and are ZOD-088; original duration, hardware, map rendering and full event playback remain open. ZOD-088 closes the two missing displayed Ready birth anchors through checked BIOS timer-2 event/TCB save/restore context. Actual prepared C# ticks 0/1 (scales 48/56) match exact original scanouts with 1,202 opaque / 77 STP samples each. Two finite runs repeat 32 content artifacts and both retained interrupted states. The original main classifier remains closed; all 16 old Ready contexts and seven C# outputs are unchanged. Locked restores, zero-warning Release builds, 1,794 .NET / 192 Python tests pass; 13 new authored IRQ/receipt checks. No runtime, audio, timing or general interrupt claim. The owner approved the new two-row gallery on 2026-10-25: That looks correct. All eight selected Ready scanouts now have visual approval. ZOD-089 now composes a bounded authored general/E5/Ready/E5/exit scene with one consumer, clock and retained ledger. Both resources preload; births discard same-tick Circle; failures roll back all phases. The closed descriptor does not import original bytecode. All 74 new managed regressions pass; totals 1,868 .NET / 197 Python with locked restore and zero-warning Release build. Eight Windows scenarios produce 1,257 exact CPU/GPU/independent-model PNGs; natural runs repeat 164 files and three preparations repeat both manifest/descriptor files. Actual readiness failures create no output. Legacy RGB8/ordered/Ready retains 16/17/72 files. The owner approved the authored sequence gallery on 2026-10-25: This looks great.; no new emulator capture, combined original scanout or full startup claim. ZOD-090 traces both verified post-Ready tails, dark-screen release/barrier, EventEnd cleanup/scenario chaining, event-frame idle observation and turn/input ownership gates. Two retained endpoints (9003/9072) still have event 4 at barrier 813 and held task 55; dispatcher clear does not prove player control. Ten resident functions and 24 source files are fingerprinted; two offline reports repeat exactly. All 208 Python tests pass, including 11 new authored checks. Production C# is unchanged; last full .NET validation remains 1,868 tests, not rerun. No new original capture or player-control/timing claim. ZOD-091 selects one authored intro-to-battle ownership/input bridge with twelve explicitly unexecuted acceptance cases. ZOD-091 now implements the selected authored introduction-to-battle bridge. One pure gate opens only at accepted intro event exit with terminal workers; battle state/CT/RNG/journal stay frozen through transition. Held keyboard/mouse is baselined and queued intro input discarded. The existing battle host, rules, router, renderer and save schema are reused. All twelve IBH cases have checks. Locked restore, zero-warning Release build and 1,909 .NET / 215 Python tests pass (41/7 new). Six native scenarios yield 1,049 exact CPU/GPU/independent-model intro PNGs, 36 unchanged historical battle PNG comparisons and 170 repeated artifacts. Actual missing/corrupt/graphics readiness negatives create no new output; standalone battle/sequence preserve 6/164 files. An isolated prepared-only run repeats all 170 artifacts. Programmed focus/latch probes are not physical input/timing evidence. The owner approved the authored gallery on 2026-10-25; no original EventEnd/startup or player-control timing claim. ZOD-092 traces the selected origin-style DarkScreen worker shared by events 4/401: untextured flat translucent quads, original approximate sqrt delays, strict overshoot/extra yields and same-instance task-54/55 signaling. Three retained holding states bind both packet banks/material and fourteen resident functions; two offline source reports repeat exactly. All 288 calculated delays match; exact sqrt would change six. Source-derived Holding birth is tick 55 and first flash/parent observation 56; release R clears at R+56, parent observes R+57. These are post-readiness logical transactions, not original duration measurements. All 229 Python tests pass (14 new); production C# is unchanged and prior 1,909 .NET tests were not rerun. Thirty future DSC cases remain unexecuted; no new original capture or entry/release pixel comparison. ZOD-093 now implements a separate typed authored pure DarkScreen worker and atomic event-before-worker adapter on the existing ledger. It preserves pre-update submissions, strict overshoot/extra yields, same-instance phases/revisions, no-match release, terminal history and full late-failure rollback. Holding/first flash are 55/56; release 100 clears 156 and parent observes 157. Locked restore, zero-warning Release build and all 1,967 .NET / 229 Python tests pass (58 new managed checks). Logical DSC-001..024 portions have checks using authored synthetic delay data; actual prepared readiness, GPU recreation, DSC-025..029 and new native output comparisons remain pending. Client/Content profiles are unchanged; no original capture or visual/timing equivalence claim. ZOD-094 implements verified offline dark-screen math conversion, strict separate prepared loading and a standalone untextured RGB5 Windows preview. All 288 delays and 117 actual C# projections match original-table arithmetic; 957 final native PNGs match CPU/GPU/independent model. Two preparations repeat both files; repeated and isolated normal runs repeat all 159 artifacts. Readiness negatives create no new output; all 455 historical native artifacts remain unchanged. Locked restore, zero-warning Release build and 2,031 .NET / 240 Python tests pass (64/11 new). Integer-sample/top-left coverage is provisional; original entry/release pixels, duration and combined startup/integration remain open. The owner approved the authored gallery on 2026-10-25: That looks good. This approval update reruns no implementation suites; original entry/release pixel/timing equivalence remains open. ZOD-095 now compares thirteen source-bound original DarkScreen entry/hold/release scanouts with actual prepared C# at zero tolerance. Eleven nonempty anchors cover 446,578 samples, including a 24-pixel tail; two bind zero coverage. Material-only controls retain geometry and require exact full CPU/other subsystem/GPU-register equality with RAM confined to selected RGB/E1 bytes. Earlier degenerate controls remain excluded, with no CPU exception in the final policy. Two final runs repeat 118 artifacts; nine host negatives preserve output. Locked restores, zero-warning Release builds and 2,031 .NET / 255 Python tests pass (15 new Python). Selected edge/overlap coverage agrees; the owner approved scratch/dark-screen-095-f/review.html on 2026-10-25: This looks good. No implementation suites rerun for this approval; recorded counts retain the ZOD-095 run. Missing endpoints/full animation, original duration, hardware/map and combined startup remain open. ZOD-096 now specifies the authored nine-operation DarkScreen/general/Ready/release/exit composition with one clock/consumer/ledger, three retained task identities, full rollback and all-terminal battle ownership. Two frozen original DMA chains confirm dark-before-display order; event401 camera work is retained/excluded. Forty source files and fifteen resident ranges are fingerprinted; two offline reports repeat exactly. All 269 Python tests pass (14 new authored inspector checks); the prior 2,031 .NET count is retained without rerun. Production C# is unchanged; all forty future cases remain unexecuted (24 pure ZOD-097, 16 prepared/host/manual ZOD-098). No new emulator, capture, combined displayed pixels or original duration claim. ZOD-097 now implements the pure nine-operation DarkScreen/general/Ready/release/exit aggregate using unchanged workers with one clock/consumer/ledger, three retained identities, post-poll facts, newborn input rules and complete late rollback. Accepted synthetic exit with all three terminal is the only availability gate. Natural 60/60, zero, maximum and specified skip schedules exit at 275/155/665/127. All DSI-001..024 plus 56 boundaries pass, including independent per-tick cells/draws/displays/barriers, variable authored delays, replay and binding/VM/identity/budget guards. Locked restore, zero-warning Release build and 2,111 .NET / 269 Python tests pass (80 new managed). Existing worker/executor/engine/Content/client source and historical contracts remain unchanged. DSI-025..040, prepared/Windows/battle-input integration and a new gallery remain ZOD-098. No native/emulator/original-duration claim. ZOD-098 adds closed prepared three-resource introduction loading, ordered RGB5 composition and the existing Windows battle/input handoff. Seven finite native runs cover 2,054 exact CPU/GPU/model frames, repeated/prepared-only state/trace/PNG equality and unchanged battle outputs. The owner approved this authored gallery on 2026-10-26; no original startup/duration claim.

Evidence: [PROJECT_PLAN.md](../../PROJECT_PLAN.md), [docs/development/sprite-executable-reference.md](../../docs/development/sprite-executable-reference.md), [docs/evidence/sprites/chocobo-attack-executable-v1.json](../../docs/evidence/sprites/chocobo-attack-executable-v1.json), [docs/development/visual-comparison-checkpoints.md](../../docs/development/visual-comparison-checkpoints.md), [docs/evidence/visual/synthetic-checkpoint-comparison-v1.json](../../docs/evidence/visual/synthetic-checkpoint-comparison-v1.json), [docs/development/retroarch-reference-capture.md](../../docs/development/retroarch-reference-capture.md), [docs/evidence/visual/retroarch-reference-readiness-v1.json](../../docs/evidence/visual/retroarch-reference-readiness-v1.json), [docs/evidence/visual/original-banner-state-v1.json](../../docs/evidence/visual/original-banner-state-v1.json), [docs/development/observed-general-banner-comparison.md](../../docs/development/observed-general-banner-comparison.md), [docs/evidence/visual/observed-general-banner-comparison-v1.json](../../docs/evidence/visual/observed-general-banner-comparison-v1.json), [docs/architecture/general-banner-native-color-study.md](../../docs/architecture/general-banner-native-color-study.md), [docs/evidence/visual/general-banner-precision-study-v1.json](../../docs/evidence/visual/general-banner-precision-study-v1.json), [docs/development/five-bit-general-banner.md](../../docs/development/five-bit-general-banner.md), [docs/evidence/visual/five-bit-general-banner-v1.json](../../docs/evidence/visual/five-bit-general-banner-v1.json), [docs/development/original-banner-continuation.md](../../docs/development/original-banner-continuation.md), [docs/evidence/visual/original-banner-continuation-v1.json](../../docs/evidence/visual/original-banner-continuation-v1.json), [docs/development/general-banner-background-blending.md](../../docs/development/general-banner-background-blending.md), [docs/evidence/visual/general-banner-background-blending-v1.json](../../docs/evidence/visual/general-banner-background-blending-v1.json), [docs/development/displayed-banner-hide.md](../../docs/development/displayed-banner-hide.md), [docs/evidence/visual/displayed-banner-hide-v1.json](../../docs/evidence/visual/displayed-banner-hide-v1.json), [docs/development/early-banner-reveal.md](../../docs/development/early-banner-reveal.md), [docs/evidence/visual/early-banner-reveal-v1.json](../../docs/evidence/visual/early-banner-reveal-v1.json), [docs/development/banner-triangle-colors.md](../../docs/development/banner-triangle-colors.md), [docs/evidence/visual/banner-triangle-colors-v1.json](../../docs/evidence/visual/banner-triangle-colors-v1.json), [docs/development/original-banner-circle-input.md](../../docs/development/original-banner-circle-input.md), [docs/evidence/visual/original-banner-circle-reveal-v1.json](../../docs/evidence/visual/original-banner-circle-reveal-v1.json), [docs/evidence/visual/original-banner-circle-hold-v1.json](../../docs/evidence/visual/original-banner-circle-hold-v1.json), [docs/evidence/visual/original-banner-circle-hide-v1.json](../../docs/evidence/visual/original-banner-circle-hide-v1.json), [docs/development/original-banner-event-identity.md](../../docs/development/original-banner-event-identity.md), [docs/evidence/visual/original-banner-event-identity-v1.json](../../docs/evidence/visual/original-banner-event-identity-v1.json), [docs/architecture/zod-080-banner-opcode-admission.md](../../docs/architecture/zod-080-banner-opcode-admission.md), [docs/evidence/events/banner-opcode-admission-v1.json](../../docs/evidence/events/banner-opcode-admission-v1.json), [docs/evidence/events/banner-opcode-admission-contract-v1.json](../../docs/evidence/events/banner-opcode-admission-contract-v1.json), [docs/development/ordered-banner-transactions.md](../../docs/development/ordered-banner-transactions.md), [docs/development/prepared-ordered-banner-event.md](../../docs/development/prepared-ordered-banner-event.md), [docs/evidence/events/ordered-banner-profile-validation-v1.json](../../docs/evidence/events/ordered-banner-profile-validation-v1.json), [docs/architecture/zod-083-ready-display.md](../../docs/architecture/zod-083-ready-display.md), [docs/evidence/events/ready-display-source-v1.json](../../docs/evidence/events/ready-display-source-v1.json), [docs/evidence/events/ready-display-contract-v1.json](../../docs/evidence/events/ready-display-contract-v1.json), [docs/development/ready-display-transactions.md](../../docs/development/ready-display-transactions.md), [docs/architecture/zod-085-ready-rendering.md](../../docs/architecture/zod-085-ready-rendering.md), [docs/evidence/events/ready-rendering-source-v1.json](../../docs/evidence/events/ready-rendering-source-v1.json), [docs/evidence/events/ready-presentation-contract-v1.json](../../docs/evidence/events/ready-presentation-contract-v1.json), [docs/development/prepared-ready-event.md](../../docs/development/prepared-ready-event.md), [docs/evidence/events/ready-profile-validation-v1.json](../../docs/evidence/events/ready-profile-validation-v1.json), [docs/development/displayed-ready-comparison.md](../../docs/development/displayed-ready-comparison.md), [docs/evidence/visual/displayed-ready-v1.json](../../docs/evidence/visual/displayed-ready-v1.json), [docs/development/ready-birth-comparison.md](../../docs/development/ready-birth-comparison.md), [docs/evidence/visual/ready-birth-v1.json](../../docs/evidence/visual/ready-birth-v1.json), [docs/development/banner-ready-sequence.md](../../docs/development/banner-ready-sequence.md), [docs/evidence/events/banner-ready-sequence-v1.json](../../docs/evidence/events/banner-ready-sequence-v1.json), [docs/architecture/zod-090-ready-battle-handoff.md](../../docs/architecture/zod-090-ready-battle-handoff.md), [docs/evidence/events/ready-battle-handoff-v1.json](../../docs/evidence/events/ready-battle-handoff-v1.json), [docs/evidence/events/intro-battle-contract-v1.json](../../docs/evidence/events/intro-battle-contract-v1.json), [docs/development/introduction-battle-handoff.md](../../docs/development/introduction-battle-handoff.md), [docs/evidence/events/intro-battle-handoff-v1.json](../../docs/evidence/events/intro-battle-handoff-v1.json), [docs/architecture/zod-092-dark-screen-transition.md](../../docs/architecture/zod-092-dark-screen-transition.md), [docs/evidence/events/dark-screen-source-v1.json](../../docs/evidence/events/dark-screen-source-v1.json), [docs/evidence/events/dark-screen-contract-v1.json](../../docs/evidence/events/dark-screen-contract-v1.json), [docs/behavior/dark-screen-worker.md](../../docs/behavior/dark-screen-worker.md), [docs/evidence/events/dark-screen-worker-v1.json](../../docs/evidence/events/dark-screen-worker-v1.json), [docs/development/prepared-dark-screen.md](../../docs/development/prepared-dark-screen.md), [docs/evidence/events/prepared-dark-screen-v1.json](../../docs/evidence/events/prepared-dark-screen-v1.json), [docs/development/original-dark-screen-comparison.md](../../docs/development/original-dark-screen-comparison.md), [docs/evidence/events/original-dark-screen-v1.json](../../docs/evidence/events/original-dark-screen-v1.json), [docs/architecture/zod-096-combined-dark-screen-introduction.md](../../docs/architecture/zod-096-combined-dark-screen-introduction.md), [docs/evidence/events/dark-screen-intro-source-v1.json](../../docs/evidence/events/dark-screen-intro-source-v1.json), [docs/evidence/events/dark-screen-intro-contract-v1.json](../../docs/evidence/events/dark-screen-intro-contract-v1.json), [docs/behavior/dark-screen-introduction.md](../../docs/behavior/dark-screen-introduction.md), [docs/evidence/events/dark-screen-intro-worker-v1.json](../../docs/evidence/events/dark-screen-intro-worker-v1.json), [docs/development/prepared-dark-screen-introduction.md](../../docs/development/prepared-dark-screen-introduction.md), [docs/evidence/events/prepared-dark-screen-introduction-v1.json](../../docs/evidence/events/prepared-dark-screen-introduction-v1.json).

Source entry points (pinned revision above; not full traces): [src/battle/units/sprite_display.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/units/sprite_display.c), [src/battle/map/runtime.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/map/runtime.c), [src/battle/units/sp2.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/units/sp2.c), [src/lib/thread/scheduler.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/thread/scheduler.c), [src/event/require/entry_conditions.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/entry_conditions.c), [src/event/require/conditions.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/conditions.c), [src/event/require/screen_support.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/screen_support.c), [src/event/require/party_changes.c](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/require/party_changes.c), [include/fft/after_battle.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/after_battle.h), [include/fft/script_instruction.h](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/script_instruction.h).

Current implementation: [tools/verification/verify_sprite_attack.py](../../tools/verification/verify_sprite_attack.py), [tools/verification/compare_visual_checkpoints.py](../../tools/verification/compare_visual_checkpoints.py), [tools/verification/prepare_synthetic_visual_comparison.py](../../tools/verification/prepare_synthetic_visual_comparison.py), [tools/verification/prepare_retroarch_reference.py](../../tools/verification/prepare_retroarch_reference.py), [tools/verification/verify_retroarch_boot.py](../../tools/verification/verify_retroarch_boot.py), [tools/verification/compare_observed_banner.py](../../tools/verification/compare_observed_banner.py), [tools/verification/prepare_observed_banner_comparison.py](../../tools/verification/prepare_observed_banner_comparison.py), [tools/verification/analyze_banner_precision.py](../../tools/verification/analyze_banner_precision.py), [src/Fft.Client/Presentation/FiveBitGeneralBannerPresentation.cs](../../src/Fft.Client/Presentation/FiveBitGeneralBannerPresentation.cs), [src/Fft.Client/Presentation/BannerPreviewOptions.cs](../../src/Fft.Client/Presentation/BannerPreviewOptions.cs), [tools/verification/trace_banner_draw_state.py](../../tools/verification/trace_banner_draw_state.py), [tools/verification/compare_native_banner.py](../../tools/verification/compare_native_banner.py), [tools/verification/capture_banner_continuation.py](../../tools/verification/capture_banner_continuation.py), [tools/verification/compare_banner_background.py](../../tools/verification/compare_banner_background.py), [tools/banner-blend-check/Program.cs](../../tools/banner-blend-check/Program.cs), [tools/verification/compare_banner_hide.py](../../tools/verification/compare_banner_hide.py), [tools/verification/compare_banner_reveal.py](../../tools/verification/compare_banner_reveal.py), [tools/verification/inspect_banner_readiness.py](../../tools/verification/inspect_banner_readiness.py), [tools/verification/verify_banner_triangle_colors.py](../../tools/verification/verify_banner_triangle_colors.py), [tools/verification/inspect_banner_circle.py](../../tools/verification/inspect_banner_circle.py), [tools/verification/inspect_banner_hide_circle.py](../../tools/verification/inspect_banner_hide_circle.py), [tools/verification/inspect_banner_event.py](../../tools/verification/inspect_banner_event.py), [src/Zodiac.Framework/Events/OrderedBannerProgram.cs](../../src/Zodiac.Framework/Events/OrderedBannerProgram.cs), [src/Zodiac.Framework/Events/OrderedBannerResult.cs](../../src/Zodiac.Framework/Events/OrderedBannerResult.cs), [src/Zodiac.Framework/Events/OrderedBannerExecutor.cs](../../src/Zodiac.Framework/Events/OrderedBannerExecutor.cs), [src/Zodiac.Content/Events/PreparedOrderedBannerEvent.cs](../../src/Zodiac.Content/Events/PreparedOrderedBannerEvent.cs), [src/Zodiac.AssetTool.Cli/PrepareOrderedBannerEventCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareOrderedBannerEventCommand.cs), [src/Fft.Client/Presentation/OrderedBannerPreview.cs](../../src/Fft.Client/Presentation/OrderedBannerPreview.cs), [src/Zodiac.Framework/Events/ReadyDisplayState.cs](../../src/Zodiac.Framework/Events/ReadyDisplayState.cs), [src/Zodiac.Framework/Events/ReadySceneProgram.cs](../../src/Zodiac.Framework/Events/ReadySceneProgram.cs), [src/Zodiac.Framework/Events/ReadySceneResult.cs](../../src/Zodiac.Framework/Events/ReadySceneResult.cs), [src/Zodiac.Framework/Events/ReadySceneExecutor.cs](../../src/Zodiac.Framework/Events/ReadySceneExecutor.cs), [src/Zodiac.AssetTool/ReadyArtworkDecoder.cs](../../src/Zodiac.AssetTool/ReadyArtworkDecoder.cs), [src/Zodiac.AssetTool.Cli/ReadyEventExporter.cs](../../src/Zodiac.AssetTool.Cli/ReadyEventExporter.cs), [src/Zodiac.AssetTool.Cli/PrepareReadyCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareReadyCommand.cs), [src/Zodiac.Content/PreparedReadyArtwork.cs](../../src/Zodiac.Content/PreparedReadyArtwork.cs), [src/Zodiac.Content/Events/PreparedReadyEvent.cs](../../src/Zodiac.Content/Events/PreparedReadyEvent.cs), [src/Fft.Client/Presentation/ReadyPresentation.cs](../../src/Fft.Client/Presentation/ReadyPresentation.cs), [src/Fft.Client/Presentation/ReadyPreview.cs](../../src/Fft.Client/Presentation/ReadyPreview.cs), [tools/verification/compare_ready_display.py](../../tools/verification/compare_ready_display.py), [tools/banner-blend-check/ReadySequence.cs](../../tools/banner-blend-check/ReadySequence.cs), [tools/verification/inspect_ready_interrupt.py](../../tools/verification/inspect_ready_interrupt.py), [tools/verification/compare_ready_birth.py](../../tools/verification/compare_ready_birth.py), [src/Zodiac.Framework/Events/BannerReadySequence.cs](../../src/Zodiac.Framework/Events/BannerReadySequence.cs), [src/Zodiac.Content/Events/PreparedBannerReadySequence.cs](../../src/Zodiac.Content/Events/PreparedBannerReadySequence.cs), [src/Zodiac.AssetTool.Cli/PrepareBannerReadySequenceCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareBannerReadySequenceCommand.cs), [src/Fft.Client/Presentation/BannerReadyPreview.cs](../../src/Fft.Client/Presentation/BannerReadyPreview.cs), [src/Fft.Client/Presentation/IntroductionBattleSession.cs](../../src/Fft.Client/Presentation/IntroductionBattleSession.cs), [src/Fft.Client/Presentation/PlayableBattleWindow.Introduction.cs](../../src/Fft.Client/Presentation/PlayableBattleWindow.Introduction.cs), [src/Fft.Client/Presentation/BattleInputBuffer.cs](../../src/Fft.Client/Presentation/BattleInputBuffer.cs), [src/Zodiac.AssetTool/DarkScreenDecoder.cs](../../src/Zodiac.AssetTool/DarkScreenDecoder.cs), [src/Zodiac.AssetTool.Cli/PrepareDarkScreenCommand.cs](../../src/Zodiac.AssetTool.Cli/PrepareDarkScreenCommand.cs), [src/Zodiac.Content/PreparedDarkScreen.cs](../../src/Zodiac.Content/PreparedDarkScreen.cs), [src/Fft.Client/Presentation/DarkScreenPresentation.cs](../../src/Fft.Client/Presentation/DarkScreenPresentation.cs), [src/Fft.Client/Presentation/DarkScreenPreview.cs](../../src/Fft.Client/Presentation/DarkScreenPreview.cs), [tools/banner-blend-check/DarkScreenSequence.cs](../../tools/banner-blend-check/DarkScreenSequence.cs), [tools/verification/inspect_dark_screen_reference.py](../../tools/verification/inspect_dark_screen_reference.py), [tools/verification/compare_dark_screen_reference.py](../../tools/verification/compare_dark_screen_reference.py), [tools/verification/inspect_dark_screen_intro.py](../../tools/verification/inspect_dark_screen_intro.py), [src/Zodiac.Framework/Events/DarkScreenIntroduction.cs](../../src/Zodiac.Framework/Events/DarkScreenIntroduction.cs), [src/Zodiac.Content/Events/PreparedDarkScreenIntroduction.cs](../../src/Zodiac.Content/Events/PreparedDarkScreenIntroduction.cs), [src/Fft.Client/Presentation/DarkScreenIntroductionSession.cs](../../src/Fft.Client/Presentation/DarkScreenIntroductionSession.cs).

Existing bounded checks: [tools/verification/verify_sprite_attack.py](../../tools/verification/verify_sprite_attack.py), [tools/verification/test_visual_checkpoints.py](../../tools/verification/test_visual_checkpoints.py), [tools/verification/test_retroarch_reference.py](../../tools/verification/test_retroarch_reference.py), [tools/verification/test_observed_banner.py](../../tools/verification/test_observed_banner.py), [tools/verification/test_banner_precision.py](../../tools/verification/test_banner_precision.py), [tests/Fft.Client.Tests/FiveBitGeneralBannerPresentationTests.cs](../../tests/Fft.Client.Tests/FiveBitGeneralBannerPresentationTests.cs), [tools/verification/test_native_banner.py](../../tools/verification/test_native_banner.py), [tests/Fft.Client.Tests/ObservedBannerContinuationTests.cs](../../tests/Fft.Client.Tests/ObservedBannerContinuationTests.cs), [tools/verification/test_banner_continuation.py](../../tools/verification/test_banner_continuation.py), [tests/Fft.Client.Tests/BannerBackgroundCompositionTests.cs](../../tests/Fft.Client.Tests/BannerBackgroundCompositionTests.cs), [tools/verification/test_banner_background.py](../../tools/verification/test_banner_background.py), [tools/verification/test_banner_hide.py](../../tools/verification/test_banner_hide.py), [tests/Fft.Client.Tests/ObservedBannerRevealTests.cs](../../tests/Fft.Client.Tests/ObservedBannerRevealTests.cs), [tools/verification/test_banner_reveal.py](../../tools/verification/test_banner_reveal.py), [tests/Fft.Client.Tests/BannerTriangleColorTests.cs](../../tests/Fft.Client.Tests/BannerTriangleColorTests.cs), [tools/verification/test_banner_triangle_colors.py](../../tools/verification/test_banner_triangle_colors.py), [tests/Fft.Client.Tests/ObservedBannerCircleTests.cs](../../tests/Fft.Client.Tests/ObservedBannerCircleTests.cs), [tools/verification/test_banner_circle.py](../../tools/verification/test_banner_circle.py), [tools/verification/test_banner_hide_circle.py](../../tools/verification/test_banner_hide_circle.py), [tools/verification/test_banner_event.py](../../tools/verification/test_banner_event.py), [tests/Zodiac.Framework.Tests/OrderedBannerContractTests.cs](../../tests/Zodiac.Framework.Tests/OrderedBannerContractTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/OrderedBannerEventExporterTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/OrderedBannerEventExporterTests.cs), [tests/Zodiac.Content.Tests/PreparedOrderedBannerEventTests.cs](../../tests/Zodiac.Content.Tests/PreparedOrderedBannerEventTests.cs), [tests/Fft.Client.Tests/OrderedBannerSessionTests.cs](../../tests/Fft.Client.Tests/OrderedBannerSessionTests.cs), [tests/Zodiac.Framework.Tests/ReadySceneContractTests.cs](../../tests/Zodiac.Framework.Tests/ReadySceneContractTests.cs), [tests/Zodiac.AssetTool.Tests/ReadyArtworkDecoderTests.cs](../../tests/Zodiac.AssetTool.Tests/ReadyArtworkDecoderTests.cs), [tests/Zodiac.Content.Tests/PreparedReadyTests.cs](../../tests/Zodiac.Content.Tests/PreparedReadyTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/ReadyPreparationTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/ReadyPreparationTests.cs), [tests/Fft.Client.Tests/ReadyPresentationTests.cs](../../tests/Fft.Client.Tests/ReadyPresentationTests.cs), [tools/verification/test_ready_display.py](../../tools/verification/test_ready_display.py), [tools/verification/test_ready_interrupt.py](../../tools/verification/test_ready_interrupt.py), [tests/Zodiac.Framework.Tests/BannerReadySequenceTests.cs](../../tests/Zodiac.Framework.Tests/BannerReadySequenceTests.cs), [tests/Zodiac.Content.Tests/PreparedBannerReadySequenceTests.cs](../../tests/Zodiac.Content.Tests/PreparedBannerReadySequenceTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/BannerReadyPreparationTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/BannerReadyPreparationTests.cs), [tests/Fft.Client.Tests/BannerReadySessionTests.cs](../../tests/Fft.Client.Tests/BannerReadySessionTests.cs), [tools/verification/test_banner_ready_sequence.py](../../tools/verification/test_banner_ready_sequence.py), [tools/verification/test_ready_handoff.py](../../tools/verification/test_ready_handoff.py), [tests/Zodiac.Framework.Tests/BattleIntroductionTests.cs](../../tests/Zodiac.Framework.Tests/BattleIntroductionTests.cs), [tests/Fft.Client.Tests/IntroductionBattleSessionTests.cs](../../tests/Fft.Client.Tests/IntroductionBattleSessionTests.cs), [tools/verification/test_intro_battle.py](../../tools/verification/test_intro_battle.py), [tools/verification/test_dark_screen.py](../../tools/verification/test_dark_screen.py), [tests/Zodiac.Framework.Tests/DarkScreenContractTests.cs](../../tests/Zodiac.Framework.Tests/DarkScreenContractTests.cs), [tests/Zodiac.AssetTool.Tests/DarkScreenDecoderTests.cs](../../tests/Zodiac.AssetTool.Tests/DarkScreenDecoderTests.cs), [tests/Zodiac.AssetTool.Cli.Tests/DarkScreenPreparationTests.cs](../../tests/Zodiac.AssetTool.Cli.Tests/DarkScreenPreparationTests.cs), [tests/Zodiac.Content.Tests/PreparedDarkScreenTests.cs](../../tests/Zodiac.Content.Tests/PreparedDarkScreenTests.cs), [tests/Fft.Client.Tests/DarkScreenPresentationTests.cs](../../tests/Fft.Client.Tests/DarkScreenPresentationTests.cs), [tools/verification/test_dark_screen_preview.py](../../tools/verification/test_dark_screen_preview.py), [tools/verification/test_dark_screen_reference.py](../../tools/verification/test_dark_screen_reference.py), [tools/verification/test_dark_screen_intro.py](../../tools/verification/test_dark_screen_intro.py), [tests/Zodiac.Framework.Tests/DarkScreenIntroductionContractTests.cs](../../tests/Zodiac.Framework.Tests/DarkScreenIntroductionContractTests.cs), [tests/Zodiac.Framework.Tests/DarkScreenIntroductionBoundaryTests.cs](../../tests/Zodiac.Framework.Tests/DarkScreenIntroductionBoundaryTests.cs), [tests/Fft.Client.Tests/DarkScreenIntroductionSessionTests.cs](../../tests/Fft.Client.Tests/DarkScreenIntroductionSessionTests.cs), [tools/verification/verify_dark_screen_introduction.py](../../tools/verification/verify_dark_screen_introduction.py).

Manual checkpoint (bounded-native-foreground-observed-wider-fidelity-pending): Two finite native Windows runs with CPU/GPU readback at 15 checkpoints; inspect hold PNG and compare exact foreground with explicit original draw state. Retain raw regional/background differences and negative dither control. Inspect the original continuation gallery through hide/exit; phases come from saved task counters, and screenshot latency is uncalibrated. Compare the new original / transparent-palette control / actual C# gallery; the owner confirmed the original and C# views look identical on 2026-10-24; wider fidelity remains pending. The owner confirmed the displayed hide-frame gallery looks correct on 2026-10-24, including the small tail and fully hidden frame. Approval covers these five displayed hide anchors; early reveal/loading, physical Circle input, event identity and broader scene rendering remain open. The owner visually approved scratch/banner-reveal-d/review.html on 2026-10-24, stating that the gallery looks good. This approval covers the displayed early-reveal gallery; the two measured pixel differences remain failures and their cause remains open. The triangle color follow-up closes those two measured differences at zero tolerance. The owner visually approved the corrected gallery scratch/banner-triangle-colors-a/review.html on 2026-10-24, stating: Looks good to me. Approval covers the displayed corrected early-reveal gallery; physical Circle input, original event identity and wider rendering fidelity remain open. A subsequent owner-operated physical reveal Circle press is supported by paired original saved input/phase fields and repeated no-input task exit. This is a behavioral checkpoint; no new gallery approval or USB/display latency claim. The prepared neutral hold checkpoint in private slot 1 still needs a physical press observation. The owner subsequently performed the hold-Circle sequence; the frozen neutral/pressed states and repeated next-update exit verify 20/1/0 progress. No new visual-latency or gallery-approval claim. The owner subsequently performed the hide-Circle sequence in slot 2. Frozen neutral/pressed states show normal hide progress 1 -> 2 despite Circle-only input; no new gallery approval or display-timing claim. Separately review ZOD-087 original/control/C# gallery at scratch/ready-displayed-087-e/review.html; six anchors match exactly and the owner approved the gallery on 2026-10-25: Visually, they look identical to my eyes. Birth frames remain missing. ZOD-088 closes the missing birth rows with exact original/control/C# comparisons at scratch/ready-birth-088-b/review.html; the owner separately approved this two-row gallery on 2026-10-25: That looks correct. Review ZOD-089 authored general-banner→Ready playback at scratch/banner-ready-089-review/review.html; independent full-frame checks pass, and the owner approved it on 2026-10-25: This looks great. ZOD-090 reuses retained Ready birth and dispatcher-clear states for source/gate observation only; no new visual approval or player-control observation. ZOD-091 will need its own native introduction-to-battle review. Review scratch/intro-battle-091-review/review.html: natural/skipped authored flow, clean intro-to-battle switch and no automatic first turn. The owner approved this authored gallery on 2026-10-25: This looks good. No original startup or physical input timing approval is inferred. ZOD-092 reuses three retained holding states for source/math/material binding only; no new visual approval, original entry/release scanout or duration observation. ZOD-093 is a headless logical milestone with no new native gallery or capture. Its full managed regression run does not replace later flat-quad visual validation. Review scratch/dark-screen-094-review/review.html: entering tiles, full hold and collapsing return to checker across normal/earliest/longest authored holds. The owner approved the ZOD-094 authored gallery on 2026-10-25: That looks good. No original entry/release comparison claim; no implementation suites rerun for approval. Review ZOD-095 original/control/C# rows at scratch/dark-screen-095-f/review.html, especially overlapping entry tiles, held flash, release and 24-pixel tail; the owner approved these thirteen selected rows on 2026-10-25: This looks good. Empty ticks 2/213 have no visible coverage. No original duration or full-startup approval is inferred. ZOD-096 is a source/design milestone with no new gallery. Its queued-layer observations do not establish combined displayed pixels; ZOD-098 will need a separate authored composition/battle-handoff gallery and approval. ZOD-097 is a headless pure implementation with no new visual approval. Its complete managed suite does not execute the sixteen future prepared/host/manual cases; ZOD-098 will need its own finite native checks and owner gallery. ZOD-098: review scratch/dark-screen-intro-098-review/review.html for entry, dark under general/Ready, hidden gaps, release and battle switch. The owner approved this authored gallery on 2026-10-26: Looks good to me. Original startup/duration approval is not inferred.

Remaining gaps:

- Matched original rendered scene captures, palette/frame/camera/timing controls, ZOD-033 thresholds and human visual review remain open; the decoder oracle does not establish GPU equivalence.
- Physical reveal Circle has one bounded paired observation; hide now has one bounded ignored-Circle observation; repeatable BSV playback, later natural completion after that press and USB/display timing remain unverified.
- Selected banner states are now identified as event 4. The earlier event-entry/conditional selection and common BattleStart event 401 activation remain unobserved; complete event playback is unsupported.
- Typed ordered execution and the authored preloaded profile are proven; broader scene workers and original event playback remain unsupported. Native startup duration, additional scene capabilities and complete event playback remain separate.
- Eight selected Ready displayed anchors are proven separately; original Ready loading/completion/input/display duration, other variants and full startup remain open.
- Thirteen selected DarkScreen scanouts match exactly; birth/transition/final yield/clear/parent endpoints, full animation, other styles, original duration and combined prepared/Windows scene integration remain open.

Next action: ZOD-098 authored composition is approved. Select the next bounded milestone through the backlog; original startup/duration, missing endpoints, physical timing and whole-game fidelity remain open.

Bounded follow-ups: ZOD-080, ZOD-081, ZOD-082, ZOD-083, ZOD-084, ZOD-085, ZOD-086, ZOD-087, ZOD-088, ZOD-089, ZOD-090, ZOD-091, ZOD-092, ZOD-093, ZOD-094, ZOD-095, ZOD-096, ZOD-097, ZOD-098.

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

Current implementation: [Directory.Build.targets](../../Directory.Build.targets), [src/Tessera.Engine/GridBounds.cs](../../src/Tessera.Engine/GridBounds.cs), [src/Tessera.Engine/IDeterministicRandom.cs](../../src/Tessera.Engine/IDeterministicRandom.cs).

Existing bounded checks: [tests/Zodiac.Framework.Tests/EngineIndependenceTests.cs](../../tests/Zodiac.Framework.Tests/EngineIndependenceTests.cs), [tests/Tessera.Engine.Tests/RelayScenarioTests.cs](../../tests/Tessera.Engine.Tests/RelayScenarioTests.cs).

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

Current implementation: [src/Zodiac.Content/ContentPackages/ContentPackage.cs](../../src/Zodiac.Content/ContentPackages/ContentPackage.cs).

Existing bounded checks: [tests/Zodiac.Content.Tests/ContentPackages/ContentPackageTests.cs](../../tests/Zodiac.Content.Tests/ContentPackages/ContentPackageTests.cs).

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

Current implementation: [src/Zodiac.Content/ContentPackages/ContentPackage.cs](../../src/Zodiac.Content/ContentPackages/ContentPackage.cs), [src/Zodiac.AssetTool.Cli/ValidateContentCommand.cs](../../src/Zodiac.AssetTool.Cli/ValidateContentCommand.cs).

Existing bounded checks: [tests/Zodiac.Content.Tests/ContentPackages/ContentPackageTests.cs](../../tests/Zodiac.Content.Tests/ContentPackages/ContentPackageTests.cs).

Manual checkpoint (pending-editor-workflow): Inspect an original job, author a separate supported change, validate/export and use it in the supported battle path.

Remaining gaps:

- Editor UI, draft/undo/unsaved-state handling and general text/graphics/script/audio authoring remain future work.

Next action: ZOD-047 can start its first bounded job/ability editor; broader authoring remains ZOD-048.
