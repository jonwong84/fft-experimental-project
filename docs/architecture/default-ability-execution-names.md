# Default ability execution naming — ZOD-445

Owner direction extends the `DefaultAbility` naming to the former
`OrdinaryMagic` family in Framework and its dependent content, client, CLI and
test types. The starting revision is `3733025`, after the
[ZOD-444 pathway rename](default-ability-pathways.md). Both naming changes keep
the existing supported behavior and data formats.

The [rename receipt](../evidence/maintenance/default-ability-execution-rename-v1.json)
records exact before/after paths and normalized hashes, substitution equivalence,
test-case parity and preservation. The clean read-only reference remains
`11464943bbdbd2a0af022d90cb7f10455cf911df`; no original C or executable was run.

## Current names and entry points

The former `OrdinaryMagic` token is now `DefaultAbility` in these families:

- Battle definition/state/result: `DefaultAbilityBattleDefinition`,
  `DefaultAbilityBattleState`, `DefaultAbilityBattleResult`.
- Commands/events/clock: `DefaultAbilityCommand`, `DefaultAbilityEvent`,
  `DefaultAbilityClockPhase`.
- Units and current execution: `DefaultAbilityUnitDefinition`,
  `DefaultAbilityUnitState`, cast preparation/execution and status rules.
- Shared rules: `DefaultAbilityPreparation`, `DefaultAbilityHitApplication`,
  `DefaultAbilityMovement`, learning definitions/reports/execution.
- Prepared content: `PreparedDefaultAbilityBattle` and the
  `DefaultAbilityPackageDocument` / `DefaultAbilityPrepared...` records.
- Import/preparation adapters and player presentation:
  `OriginalDefaultAbilityImporter`, `PrepareDefaultAbilityBattleCommand`,
  `DefaultAbilityInteraction`, `DefaultAbilityBattleHost`,
  `DefaultAbilityBattleWindow` and `DefaultAbilitySnapshot`.

Start actual supported command processing at
[`DefaultAbilityBattle.Apply`](../../src/Zodiac.Framework/Battle/Combat/Magic/Execution/DefaultAbilityBattle.cs).
Its captured state is
[`DefaultAbilityBattleState`](../../src/Zodiac.Framework/Battle/Combat/Magic/Execution/DefaultAbilityBattleState.cs).
Shared primary-action preparation remains
[`DefaultAbilityPreparation.Prepare`](../../src/Zodiac.Framework/CombatRules/DefaultAbilityPreparation.cs).
The AI counterpart still starts at `DefaultAbilityAiContext` and
`DefaultAbilityAiAbilitySearchQuery.Evaluate(context, request, rng)`.

Type and constructor references are updated throughout the repository. The
shared fixture is `AuthoredDefaultAbility`; its three linked project entries
were updated with the file move. C# namespaces and the current Magic execution
folder organization remain unchanged.

## Scope and compatibility

This is an exact `OrdinaryMagic` → `DefaultAbility` name substitution, followed
by current documentation/navigation updates. No C# quoted string literal contains
the old token, so none of those literals needed replacement. Project-link paths
change to follow the moved fixture; prepared assets and their schemas do not.

The renamed battle still implements its existing magic-focused encounter and
learning/movement/execution profile. `MagicCastDefinition`, `PendingMagicCast`,
Faith magic formula types and the supported `Spells` data remain specific to
those mechanics. The new names do not create a general formula dispatcher,
support additional default-menu abilities or connect diagnostic AI search to
the complete-turn controller.

Prepared format/profile/asset IDs, schema versions, fixture data, CLI command
strings, snapshots and human-facing scenario text retain their values. In
particular, existing `fft.ordinary-magic-battle` content remains readable; there
is no content migration. C# callers must use the new type names. No legacy
prefix alias layer is added.

Historical evidence/behavior-fixture payloads and recorded hashes remain
unchanged. Current Markdown symbols/source links and coverage paths use the new
names; historical Markdown retains its recorded prose and follows moved source
links. Earlier receipts still describe their recorded Git snapshots. Use the
ZOD-444 and ZOD-445 rename maps to follow their current counterparts.

## Verification and review

The receipt checks every affected C# and Python code file against the prior Git
content under the exact substitution, with no regenerated expected gameplay
vectors. The three project-link changes are checked the same way. Existing .NET
test classes/display names/outcomes are compared to the ZOD-444 reports under
the same translation; all existing cases remain present.

Full build/test results and path counts are in the receipt/handoff. The owner's
split-layout battle-state formatting is carried forward under the rename; owner
drafts/immutable JobStats, controller formatting and disabled CI remain unchanged.
No native presentation was rerun for this type/path-only change.

The next checkpoint remains owner review. Other menu-use-case implementations,
broader default-ability mechanics, full AI dispatch/insertion/lottery/commands,
deferred domains and the full-port review gates remain separate.
