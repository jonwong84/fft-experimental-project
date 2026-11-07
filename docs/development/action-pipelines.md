# Following player actions and AI decisions — ZOD-423

This owner-selected organization pass follows the supplied FFHacktics targeting
and preparation routines and the completed [ZOD-418 search study](../behavior/ordinary-spell-aim-search.md).
The reference is read-only `11464943bbdbd2a0af022d90cb7f10455cf911df`;
managed baseline is `230faf2`. This pass reorganizes existing supported behavior.
It does not enable additional ability families or an autonomous magic AI.

## Start here

- Player spell selection and actual commands:
  [OrdinaryMagicBattle.Choice / Apply](../../src/Zodiac.Framework/Battle/Combat/Magic/Execution/OrdinaryMagicBattle.cs).
- Complete supported physical-roster decision:
  [AiDecisionMaker.Decide](../../src/Zodiac.Framework.Ai/Decision/AiDecisionMaker.cs).
- Checked actual AI command:
  [AiCommandExecutor.Execute](../../src/Zodiac.Framework.Ai/Execution/AiCommandExecutor.cs).
  Complete turn: [AiTurnController.Execute / Resume](../../src/Zodiac.Framework.Ai/Execution/AiTurnController.cs).
- One ordinary spell hypothesis at the current origin:
  [OrdinarySpellAiCandidateQuery.Evaluate](../../src/Zodiac.Framework.Ai/OrdinarySpells/OrdinarySpellAiCandidateQuery.cs).
  This produces diagnostics, not an executable command or complete search.
- Potential ordinary spell aims at the current origin:
  [OrdinarySpellAiAimQuery.Evaluate](../../src/Zodiac.Framework.Ai/OrdinarySpells/OrdinarySpellAiAimQuery.cs).
  [ZOD-424](../behavior/ordinary-spell-aim-enumeration.md) adds fresh source-ordered
  unit/tile lists; candidate evaluation/ranking remain separate.

## Walk one player cast

The host's [OrdinaryMagicInteraction](../../src/Fft.Client/Presentation/OrdinaryMagicInteraction.cs)
handles input/selection modes. `OrdinaryMagicBattle.Choice` delegates to
`SpellTargetChoiceQuery.Evaluate`, which computes selectable range and the
choice mode. Confirming a cast submits `OrdinaryMagicCommand.Cast` through
`OrdinaryMagicBattle.Apply`; command admission and atomic rejection remain there.

The cast branch now reads
`OrdinaryMagicCastPreparation.PrepareAndQueue`. Open
[that file](../../src/Zodiac.Framework/Battle/Combat/Magic/Execution/OrdinaryMagicCastPreparation.cs)
to follow learned grants and resource availability, choice/aim capture, shared
`CombatRules.OrdinaryMagicPreparation`, ACT commitment and pending-plan creation.
An immediate plan calls `OrdinaryMagicCastExecution.Resolve`; a delayed plan is
ticked by the existing `OrdinaryMagicBattle.Advance` lifecycle before resolution.

At readiness, [OrdinaryMagicCastExecution](../../src/Zodiac.Framework/Battle/Combat/Magic/Execution/OrdinaryMagicCastExecution.cs)
checks resources, resolves the stored aim, builds the affected area, collects
recipients and invokes `MagicCastQuery.ResolveTargets`. The shared
[FaithMagicFormula](../../src/Zodiac.Framework/Battle/Combat/Magic/FaithMagicFormula.cs)
owns formula calculation/evasion/result finalization. Execution then applies
results, adopts RNG and performs its admitted rewards/learning work.

The [targeting source map](../../src/Zodiac.Framework/Battle/Combat/Targeting/README.md)
links each concrete stage. `OrdinarySpellTargeting` retains its public range,
area, collection and pure resolution APIs. Their implementations now have
separate internal owners in `Geometry/` and `Resolution/`; choice/capture types
live in `Selection/`. Runtime flow and error ordering are preserved.

## Walk one AI decision

`AiDecisionMaker.Decide` reads in this order: fresh preparation/movement prefix,
ability occurrences through `AiAbilityEvaluator`, per-occurrence candidate
evaluation, best-candidate insertion and lottery. Each next calculation receives
the preceding decision RNG. The returned plan has no command authority.
`AiCommandExecutor` obtains a fresh decision inside Framework's checked command
callback, then publishes one admitted actual command. `AiTurnController` adds
continuations and turn completion with explicit facing.

For an ordinary spell hypothesis, start at `OrdinarySpellAiCandidateQuery` and
follow [its source map](../../src/Zodiac.Framework.Ai/OrdinarySpells/README.md):
fresh work/selection, private simulation, shared primary preparation, charge
clock, stored-aim hit work, accounting and absolute scoring. The file groups
make those responsibilities visible without changing namespaces or APIs.
Ready-hit diagnostics remain separately named and require supplied movement
opportunities. The subsequent `OrdinarySpellAiAimQuery` lists potential aims
through `Selection/OrdinarySpellAiAimEnumeration`, including completed working
usage and deadline groups. These queries do not compose a ranked magic search
or enter the physical roster's full decision/command pipeline yet.

## Original routine mapping and corrections

- `target_tile_selection_start`,0x800744C8: presentation/controller transition
  after range classification. The wiki's Set Target Boxes Red is not geometry.
  Its Stop Game Flow helper disables camera panning; the two Store AT-list-ID
  helpers actually show/hide the cursor. Keep this role in host presentation.
- `target_range_set_for_action`,0x8017A8C0: menu-dependent selectable range and
  choice mode. The bounded default-menu counterpart is `SpellTargetChoiceQuery`.
- `ability_range_set`,0x8017A290: ordinary selectable range; its current geometry
  counterpart is `SpellTargetGeometry.Range`. It is not a skillset-data loader.
- `panels_spread`,0x80179518, and neighbor helper0x8017963C: shared in-place
  residual-budget spread. Passes scan Y/X; neighbors use +X,+Y,-X,-Y and update
  both layers. Zero passes clear frontier marks only. The current admitted
  geometry uses the already-verified equivalent Manhattan construction; this
  refactor does not replace it with a new general spread algorithm.
- `target_area_set_for_action`,0x8017AAF8, and `ability_area_set`,0x8017B874:
  affected panels after an aim is chosen. `SpellTargetGeometry.Area` owns the
  current ordinary subset. Return1 from the dispatcher means no panels were
  marked, not Mathematics specifically. Range and area result enums differ.
- `action_prepare`,0x8017C45C, and wrapper0x8017C3DC: shared primary preparation
  and action-state proposal, currently `OrdinaryMagicPreparation`. Jump/items/
  other menu branches remain separately gated.
- `origin_aim_tiles_evaluate`,0x8019CD9C: original candidate enumeration, not the
  stored-aim resolver. [The search study](../behavior/ordinary-spell-aim-search.md)
  records its masks, ordering and ranking boundaries. The subsequent ZOD-424
  query lists bounded potential aims without evaluating or ranking them.
- `ai_action_simulate_and_score`,0x8019D37C, wrapper0x8019DCBC and strikes0x8019DD8C:
  the supported spell subset is divided among simulation, hit work, accounting
  and scoring. Original suspended snapshots/frame work remain documented gates.

Source fields and flags retain their distinct meanings: cardinal range is not a
height lock; the untargetable-panel filter is not full obstacle trajectory
validation; the source unit-panel loop's caster-only quirk is not corrected.
The wiki's proposed residual-budget damage falloff is not activated.

## Where to modify a mechanic

For range/height/area geometry, open `SpellTargetGeometry`. For following and
choice policy, open `SpellTargetChoiceQuery`, `SpellAimCapture` and
`SpellAimResolver`. For recipient ordering/team mutations, open
`SpellTargetCollection`. For an ordinary cast's actual lifecycle, open
`OrdinaryMagicCastPreparation` and `OrdinaryMagicCastExecution`. Formula math
and combat result rules stay in their existing shared owners, independent of
those targeting stages. AI usefulness belongs in `Selection/`; candidate timing
and hypotheses in `Simulation/`; accounting/absolute scores in `Scoring/`.

Every port should link a pinned source symbol, state its supported inputs and
preserve independently checkable behavior. Source addresses aid navigation;
they do not require reproducing raw memory layouts or one class per assembly
routine. Existing compatibility APIs remain navigable forwarding entries.

## Validation and limits

Forty existing files move byte-for-byte into the selected feature folders.
Four internal owners extract existing method bodies: panel geometry, recipient
collection, pure cast resolution and actual preparation/queueing. Public types,
signatures, namespaces, content schemas, input immutability, command authority,
RNG, event order and admission limits are preserved. Existing tests protect
observable behavior; no tests freeze the new private type arrangement.
Historical evidence/fixtures retain their original paths and hashes. Current
source maps and links follow the moves. The handoff and implementation receipt
record the completed validation.

Locked restore and Release build pass with zero warnings/errors. All5,094 .NET
tests and782 Python tests pass. An independent extraction audit checks six
method bodies, all forty unchanged moves and the six public targeting method
signatures. The programmed Basic Skill window retains all121 steps/two journal
replays; Fire retains all four gameplay/replay checkpoints and final hash. These
are native programmed routes, not physical input or original-game equivalence.
All ten owner blobs and the approved formatting are preserved, including the
moved `SpellTargetChoiceQuery` file. See the
[implementation/move receipt](../evidence/maintenance/action-pathways-implementation-v1.json).

Owner drafts, JobStats, int MP costs, equipment, statuses, new shapes/cone,
automatic facing, general namespace migration, editor, saves and frame RNG
remain separate. This is a bounded navigation pass, not a complete AI port.
