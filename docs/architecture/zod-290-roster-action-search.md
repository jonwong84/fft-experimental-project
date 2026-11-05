# Roster cast priorities and supported-action search — ZOD-290

Source/design, 2026-11-05, managed base
`ddf9e628f15c399b5413b6c474019f02b84b19f1`. Read-only fft_decomp checked clean
at `11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/roster-action-source-v1.json) pins files and
function bodies. No original or C program is executed. Prior
[general scenarios](zod-287-roster-general-search.md),
[variable lists](zod-264-variable-action-lists.md) and
[Accumulate study](zod-229-accumulate-ai.md) remain prerequisites.

## From one prefix to distinct search passes

`general_select` seeds one D0 movement baseline, then calls ANY active selection
and `ability_list_evaluate`. Each occurrence is loaded in source order, including
the terminal unusable Frog entry. Usability is checked before copying command
payload or restoring active targets. Repeated entries are independent; absent
entries do no work. Skipped entries retain the prior command payload. Post-ACT
retains summaries/reach but clears every actor entry before action work.

`ai_ability_outcome_evaluate` restores the active mask for each usable entry.
Attack and Stone classify ordinary enemies Useful and ordinary allies Adverse.
The filter clears non-Useful members before building range masks. With stable
ordinary units, source masks must include **all** active enemies, including
different non-actor teams; nearest focus is only a movement-policy target.

Attack is exhaustive: `origin_reachable_evaluate` disables aim consumption and
visits origins in layer/y/x order without reading `tile_priority`. Stone takes
ranked origins: `origin_next_take` reads every remaining origin's signed-16
priority, packs it with current byte proximity and takes the last complete tie.
Its initial mask is scenario1 intersected with the union of range-4 diamonds
around useful enemy tiles. That first ranking reads every initial key, even if
aim consumption later avoids some origins. Direct-path checks occur later and
cannot justify omitting an initial key. Attack's range-1 mask is diagnostic,
not a reason to demand otherwise unread priority values. Self map-target work
does not enter this ranked/exhaustive origin path at all.

Fresh score-mask writes replace retained workspace values; an unscored actor
reinserted into scenario1 may need a retained value when Stone can reach any
eligible enemy. Use the existing map-owned immutable `AiPriorityWorkspace`.
Unknown never means zero; do not rescore a missing cell or substitute a focused
pair. All fresh numeric writes publish only on whole-query success; failure
returns incoming workspace and RNG. Event/loader/unmodeled writers still require
Forget; no reconstruction of original workspace history is introduced.

The future action scanner must consume and order all aim tiles, distinguish
unit/tile and first/second CT-group passes, preserve origin finalization
(actor origin substitutes D1), per-strike formula/score order, strict baseline
admission and shared ranked insertion. Eight-slot lottery and retained terminal
payload still belong to later full-list selection. No repeated pair decisions
can stand in for roster search.

## Accumulate exposes a new roster boundary

Retail Accumulate is the allied-stat/map-target branch, before ordinary origin
scanning. `useful_targets_filter` scans active source slots. Each ordinary ally
draws one coin: even Useful, odd None; enemies are Adverse without drawing.
Other allies draw even beyond range zero. PA 99 does not suppress a coin.
The retained target mask contains only Useful units; no actor-only coin shortcut
is valid on a roster.

Any useful ally admits the map-target pass, whose actual command still targets
the actor. If only another ally is Useful, the self strike has no valuable hit:
the candidate restores its baseline and returns NotValuable **before** turn-end
clock advancement. There are zero formula draws and no candidate upkeep draw in
that case. ACT-first is attempted before Move-first when D1 differs, even though
both attempts are NotValuable. If no ally is Useful, neither order is attempted.

When self is Useful, each order starts from restored actual PA, applies the
existing zero-bonus PA+1/clamp-99 formula, consumes hypothetical ACT, retains
facing, then runs one whole-roster effect-free clock. The self strike credits
+3 at hit 100 even at PA 99. Absolute HP/status/equipment terms include every
unit with its source-order team sign; PA itself is not an absolute score term.
Equal candidate ranks retain ACT-first. D0 remains the move-only comparison
baseline; D1 remains the map-entry destination. These are different owners.

Bind a selected self occurrence to the existing typed
`ImmediateAbilityBattleProfile`: complete roster zero-bonus unit bindings,
known executable effect and actor grant, current PA 1..99. Repeated occurrences
share the membership check but not their coins. Omitted AI actions do not imply
missing gameplay grants. Do not invoke an authoritative command to obtain a
prediction. Existing pair profiles and their capacity guards stay unchanged.
Attack/Stone candidates additionally need exact weapon/effect/grant and damage
width admission across the roster before any future broadening; this batch
does not relax their two-unit simulators.

## Selected children — ZOD-291–293

- **ZOD-291:** fresh roster cast-origin/provenance binding using one general
  scenario prefix and the existing immutable priority workspace. Diagnose
  exhaustive Attack origins and ranked Stone origins across all active enemies;
  require only actual ranked reads, preserve unknown/fresh/retained values,
  current proximity and whole-query publication. No target/formula search.
- **ZOD-292:** separately admitted roster Accumulate usefulness with complete
  executable/grant/numeric validation and explicit retail-entry context. Reuse
  the pair filter through a private source-order active/enemy kernel. Preserve
  ally coins, inactive members, empty masks, source order and atomic failure.
- **ZOD-293:** isolated selected self occurrence owning one fresh general prefix,
  one usefulness pass and zero/one/two candidate attempts. Share private choice/
  placement/clock arithmetic with move-only and old pair self projection; score
  all units with +3, preserve self-unuseful early rejection, strict comparison,
  post-ACT skip and immutable replay. No list-wide stream, rank/lottery or command.

Living ordinary compact16, stable teams, MP0/positive Speed, raw Move <=124,
strict flat geometry and source general-policy admission remain. Other
abilities/policies/statuses/maps, corpse lifecycle, automatic facing, terminal
routing, original frame RNG, owner drafts, host/editor, broad replacement
interfaces and ZOD-106 stay separate/deferred. ZOD-294 should trace and select
the roster ordinary sword candidate/effect boundary before full action traversal.

## Implemented follow-up

[ZOD-291–293](../behavior/roster-ai-action-foundation.md) implement the selected
cast-provenance owner, roster self filter and isolated fresh self entry through
shared private kernels. Nineteen independent descriptions, 12 new managed cases
and three new Python tests cover source ordering, bounds, retained priorities,
ally-only usefulness, PA/clocks/scores, pair parity and immutable replay. Full
validation passes with 4,355 .NET cases, 557 Python tests, zero-warning/error
Release build and pinned source/prepared/guide/coverage audits. No original/C
execution, full action traversal or roster AI command activation is claimed.
