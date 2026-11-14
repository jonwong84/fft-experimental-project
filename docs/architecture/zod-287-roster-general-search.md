# Roster general-search admission and movement — ZOD-287

Source/design, 2026-11-05; managed base
`532b1919e956578123e758cdea0386311e74b064`. Read-only fft_decomp checked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/roster-general-source-v1.json) pins the functions
and file fingerprints. No original or C program is executed.

## Admission precedes general movement

`ai_command_build` owns refresh, list preparation, actual reachable rows, team
support and `decision_target_consideration_init`, then `action_select`. General
search is not implied by a unit being non-Blue. Ordinary represented None/Critical
units exclude status-imposed selection, but Critical plus available retreat
support takes conditional retreat before configured autobattle dispatch.

With no special decision behavior and no directly controlled Blue actor,
target consideration overwrites the initial automatic setting. Blue uses manual
and chooses healer/crystal focus when floor(HP*128/maxHP) < 45; otherwise it
sets every live targetable member and chooses the nearest eligible enemy.
Non-Blue chooses manual/enemy focus when MostlySupport is clear or the actor's
updated TeamSupportUnavailable is set. Otherwise it chooses FadingLife, whose
ally-peril/recovery path cannot be replaced with ordinary general search.
The first minimum-share ally was already cleared by list preparation; equal
support-heavy shares can therefore admit the first ally but exclude another.

Special behavior can preserve an existing setting/focus through ConserveCT,
configured modes, valid targets or the single-enemy condition; Invite can clear
it. Directly controlled Blue preserves its configured setting and returns before
automatic focus selection. These branches need explicit future owners, not a
fabricated manual-setting certificate. The selected domain explicitly requires
no direct control or special behavior. Existing stable-team and effect-free
profiles exclude Invite, charging, mounts, inert statuses and other hidden effects.

Nearest eligible enemy scans original source order, retaining the first
Manhattan-distance tie. No enemy defaults to the actor. Focus sets FocusTarget,
clears StayNear and copies live coordinates. A self focus zeroes the whole target
distance grid, unlike Manhattan distance to the actor. The general ANY active
mask includes actor and all considered allies/enemies; it is not an enemy mask.
Proximity separately sums imminent enemies against the captured actor delay,
using restored live target delays rather than hypothetical refresh CT.

## Scenario order, widths and provenance

`general_select` builds scenarios once, seeds the move-only baseline from D0,
checks charging, selects ANY targets, searches abilities, then ranks/falls back.
This batch stops before ability/effect/target traversal, rank insertion, lottery
and command selection. Reporting the ANY mask early is diagnostic only and
cannot perform or authorize a later search.

For a focused enemy use enemy reach Move+1; for actor fallback use ally reach
Move. In the supported dry, cost-one flat rectangle, reverse target propagation
clears all movement unit records; its distances are Manhattan even when forward
movement is blocked by enemies. `FlatRosterMovement` already owns that distinction.
Map/source row order is layer, y, x. Reachable actor origin is always retained.

With distance D and reach R, in-reach scenario 1 filters M0 to distance <= Move+D;
out-of-reach filters to <= D-1. An empty filter copies M0. Each selected tile
runs one fresh isolated whole-roster movement hypothesis, chaining decision RNG
in source map order; live state is restored between tiles. The stored priority
is the negated signed-16 absolute score. Destination ordering packs that signed
priority in the high half and byte foe proximity in the low half. Priority-first
then prefers distance; distance-first then prefers packed priority. Complete ties
choose the last scanned tile, unlike nearest-unit ties.

Only after all tile simulations, an out-of-reach non-Blue actor draws once and
halves reach iff raw % 7 == 0; Blue draws nothing here. D1 chooses priority-first
within effective reach, falling back to unrestricted distance-first. In reach,
D1 uses unrestricted priority-first. Construct scenario 2 as M0 minus the scored
mask before OR-ing actor into scenario 1. Actor can therefore occur in both masks.
When ACT is unused, focus exists and target is in reach, D0 searches distance
<= D-1 with the ordinary fallback; ranged against an enemy raises that floor to
3. Otherwise D0 copies D1. HasRanged survives post-ACT list usability clearing.
No reflected/stay-near/crystal overrides occur in this selected domain.

Only score-mask cells receive priority writes; all M0 cells receive proximity.
Unscored priority remains unknown, including a reinserted actor, and cannot be
silently zeroed or rescored. D0 is selected from the scored mask, so its baseline
has a fresh value. Future cast-origin readers must establish written/retained/
unread provenance separately. The old pair APIs and priority owners remain intact.

## Selected children

- **ZOD-288:** a fresh roster general preparation owner derives support-aware
  admission, nearest focus, reaches, legal movement and diagnostic ANY/proximity
  facts through the existing roster owners. Require explicit no-direct-control
  and no-special-behavior facts; reject low-HP Blue healer focus, conditional
  retreat and FadingLife without publishing partial output or RNG.
- **ZOD-289:** own that fresh prefix plus source scenario filtering, chained
  multi-unit tile scoring, reach adjustment, destination ties, masks and priority
  provenance. Extract reusable private mask/destination arithmetic from the
  existing pair scenario kernel rather than faking pair results or accepting
  public cached simulations. Preserve before/after Move and post-ACT choices.

The living ordinary compact-16, positive-Speed/MP0, stable-team, raw Move <=124
and flat-ground boundaries remain. Supported list summaries do not imply actual
gameplay grants or effect bindings. Full action/effect/target traversal, other
policies/statuses/maps, corpse lifecycle, automatic facing, terminal routing,
frame RNG, host/editor activation, owner drafts, broad replacement interfaces
and ZOD-106 remain separate/deferred. ZOD-290 should trace roster cast-origin
priority reads and supported-action search before choosing its next child.

## Implemented follow-up

[ZOD-288/289](../behavior/roster-ai-general-scenarios.md) implement the selected
fresh prefix and general movement scenarios. Eighteen independent source
descriptions, eight new managed cases and three new Python tests cover admission,
focus, masks/provenance, source order, pair parity and atomic replay. Full
validation passes with 4,343 .NET cases, 554 Python tests, zero-warning/error
Release build and pinned source/prepared/guide/coverage checks. No original/C
execution, roster action search or host/editor activation is claimed.
