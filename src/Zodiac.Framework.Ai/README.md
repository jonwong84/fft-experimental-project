# AI source layout

ZOD-219 groups the current bounded implementations by their supported decision
family. Source and test projects use matching folders:

- `Shared/`: hypothetical clocks/state, movement prefix, scoring, ranking and
  lottery kernels, immutable retained-priority knowledge and semantic ordinary
  human ability-list construction. Ordered roster projections separate live/
  cached membership, eligible-enemy nearest focus, active target masks and
  imminent-enemy proximity writes. Existing admitted pair preparation/proximity
  consumes these shared queries; wider read-only inputs grant no command authority.
  See [roster targeting](../../docs/behavior/ai-roster-targeting.md).
  `RosterAiRefresh`/`Preparation` own fresh whole-roster clocks, supported-list
  summaries, retained team policy and restored target facts. The isolated
  `RosterAiMovementBaseline` uses shared private projection/clock/absolute score
  kernels without inventing a pair target or accepting cached simulations.
  [Boundaries](../../docs/behavior/roster-ai-preparation.md) remain explicit;
  `RosterAiGeneralPreparation`/`GeneralScenarios` now derive support-aware general
  admission/focus and roster movement masks/destinations through shared private
  `AiScenarioLayout` arithmetic. [Policy and provenance limits](../../docs/behavior/roster-ai-general-scenarios.md)
  stay explicit. `RosterAiCastOrigins` now binds fresh/retained ranked keys
  across every active enemy through the existing workspace; exhaustive Attack
  and self map entries read no ranked keys. Full action traversal and live
  roster AI commands remain future work.
  These serve more than one family.
- `Sword/`: the original ordinary-sword pilot, its preparation, scenarios,
  scans, selection and execution adapters. `RosterSwordAiCandidate` now shares
  formula/clock/scoring and explicit selected-target projection for one isolated
  roster hypothesis. [Candidate boundaries](../../docs/behavior/roster-sword-ai-candidate.md)
  preserve old pair guards and peer state. `RosterSwordAiAttackEntry` now owns
  [fresh exhaustive roster Attack](../../docs/behavior/roster-sword-ai-attack.md),
  with map origin/aim order, first-complete ties and D0 comparison. Stone/list
  selection remains open. `RosterThrowStoneAiCandidate` projects one supplied
  [certified-clear roster Stone candidate](../../docs/behavior/roster-throw-stone-ai-candidate.md)
  with real typed grants and shared formula/state/clock/scoring; computed
  trajectories now exist as `RosterAiDirectTrajectory`, using explicit complete
  sprite heights and exact SDK normalization/samples across all occupants.
  `RosterThrowStoneAiEntry` now owns one fresh selected ranked Stone occurrence
  with computed blockage, consumed aims and transactional workspace/RNG.
  `RosterBasicSkillAiSelection` now owns a complete supported roster decision
  through one prefix, private entry continuations, stable eight-slot ranking
  and source lottery. Roster AI command/turn integration remains open.
- `ThrowStone/`: one Throw Stone formula/candidate/scan, used by pair selection.
- `AttackThrowStone/`: the closed two-entry list, shared pair preparation,
  movement/scenario branches, ranking/selection and actual command adapters.
- `Accumulate/`: roster source-order allied usefulness and one isolated fresh
  self occurrence now share private filter/PA/choice/clock/scoring kernels with
  the pair owners. [Entry and binding limits](../../docs/behavior/roster-ai-action-foundation.md)
  preserve ally-only NotValuable behavior and the incomplete list-wide stream.
  Also owns self PA projection/scoring, certified three-action-list
  preparation, a separate usefulness filter and an isolated map-target entry.
- `BasicSkill/`: supplied-scenario Attack/Accumulate/Throw Stone composition and
  terminal selection using those shared entry/score/rank/lottery owners.
  Fresh Move/ACT/END execution owns one selection inside the Framework transaction,
  including actual self PA and retained Attack/Stone/Wait continuations. Post-ACT
  preparation, derived explicit reach branches and remaining Move/END settlement
  also reuse shared kernels. Supplied and derived complete turns share automatic
  post-ACT reach and exact partial-progress recovery. The derived entry owns
  pre-ACT S/D0/D1 through Shared/AiMovementScenarios and rejects unknown cast
  priorities unless proved unread. Workspace-aware entries can also resolve
  priorities from earlier owned writes, preserving unknowns and conservative
  forgetting. They publish knowledge with accepted queries/commands and freeze
  it in exact turn recovery. Arbitrary event/loader history and broader search
  remain separate work.

Namespaces remain `Zodiac.Framework.Ai` and `Zodiac.Framework.Ai.Tests`; moving
a file adds no dependency or access. The assembly still references Framework,
and Framework does not reference AI. Content's outer battle-setup adapter now
references AI to supply semantic lists from immutable metadata and explicit
known slots; AI never references Content or performs file I/O. Canonical list
preparation replaces its legacy list assertion while retaining its four-entry
guard. Separate `BasicSkillAiListPreparation`/`Scenarios`/`Composition`/`Selection`
owners admit missing/repeated/reordered supported self/Stone occurrences.
`BasicSkillAiListCommandExecution` and `BasicSkillAiListTurnExecution` privately
own fresh selection, actual typed proposals, post-ACT settlement and exact-session
recovery. They reuse entry/movement/rank/lottery, Framework continuation/facing
and common stage-routing owners; no public cached plan grants authority.
See the [variable-list boundary](../../docs/behavior/variable-basic-skill-ai.md).
General actions/rosters/maps/policies remain open. Folders describe current code ownership,
not permanent original-game table limits. `SwordAi` names still identify the
restricted sword implementation; moving them is not a claim of general weapon
support. Avoid adding a folder for each ticket or duplicating shared kernels
for each future ability.

Production entry points are in the family's execution files. Pure selectors
return diagnostics and plans; they grant no command, receipt or RNG authority.
`AttackThrowStoneAiCommandExecution` owns one fresh pair decision and derives
reach/order internally. Earlier explicit-entry adapters remain supported.
`AttackThrowStoneAiTurnExecution` composes a complete admitted living-pair turn
and exposes exact partial-progress recovery. NonBlue and explicitly certified
Blue AI-controlled profiles share these owners; Blue low-HP and player-controlled
policies remain unsupported.
`BasicSkillAiActFirstExecution` owns fresh supplied-scenario selection and one
actual Attack/Throw Stone/Accumulate ACT in the same Framework callback. It
issues no predicted-D1 Move or continuation; both choices must initially be unused.
Framework owns current-session validation, verified RNG adoption, actual rules,
paths, continuations and facing receipts. The RNG implementation lives under
`Zodiac.Framework/Random`; its generic contract is under
`Tessera.Engine/Interfaces`. The owner's `RandomStateGenerator` name retains
the original verified arithmetic and algorithm ID; it is not a different RNG.

Start with the [original algorithm guide](../../docs/architecture/original-ai-algorithm.md)
for policy/source coverage and the [completion roadmap](../../docs/development/ai-completion-roadmap.md)
for remaining breadth. Historical evidence records paths at its pinned managed
revision. Current coverage links follow this layout; old hash receipts must not
be regenerated merely because files moved.

Exact roster projectile sampling now belongs to Framework `FlatRosterDirectTrajectory`;
the AI facade preserves compact16/legal-origin admission and between-unit semantics.
Actual roster Stone uses the live to-target mode with interception. Neither pure
geometry nor public AI plans grant command authority.
