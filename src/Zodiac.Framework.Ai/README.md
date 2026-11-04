# AI source layout

ZOD-219 groups the current bounded implementations by their supported decision
family. Source and test projects use matching folders:

- `Shared/`: hypothetical clocks/state, movement prefix, scoring, ranking and
  lottery kernels. These serve more than one family.
- `Sword/`: the original ordinary-sword pilot, its preparation, scenarios,
  scans, selection and execution adapters.
- `ThrowStone/`: one Throw Stone formula/candidate/scan, used by pair selection.
- `AttackThrowStone/`: the closed two-entry list, shared pair preparation,
  movement/scenario branches, ranking/selection and actual command adapters.
- `Accumulate/`: self PA projection/scoring, certified three-action-list
  preparation, a separate usefulness filter and an isolated map-target entry.
- `BasicSkill/`: supplied-scenario Attack/Accumulate/Throw Stone composition and
  terminal selection using those shared entry/score/rank/lottery owners.
  Fresh Move/ACT/END execution owns one selection inside the Framework transaction,
  including actual self PA and retained Attack/Stone/Wait continuations. Derived
  scenarios, post-ACT preparation and complete three-entry turns remain separate work.

Namespaces remain `Zodiac.Framework.Ai` and `Zodiac.Framework.Ai.Tests`; moving
a file adds no dependency or access. The assembly still references Framework,
and Framework does not reference AI. Folders describe current code ownership,
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
