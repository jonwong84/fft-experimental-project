# Ordinary spell movement baselines — ZOD-437

`DefaultAbilityAiMovementQuery.Evaluate` now derives fresh move-only scenario
diagnostics from an owned ordinary spell slot. It shares existing geometry,
clock, absolute scoring, target/proximity and scenario-selection owners. Its
output distinguishes D0, D1, the baseline rank and explicit priority writes.
It grants no spell-candidate, decision or command authority.

The managed base is `48fe3c097c2095486acd9d816e771e4a5738738f`; the clean read-only
reference is `11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[origin study](ordinary-spell-movement-origins.md) remains the source map for
widths, masks, ties and deferred branches. The
[implementation receipt](../evidence/ai/spell-movement-baselines-implementation-v1.json)
pins the source and final validation. No reference C or original executable ran.

## Entry point and flow

Start at
[`DefaultAbilityAiMovementQuery`](../../src/Zodiac.Framework.Ai/DefaultAbilities/DefaultAbilityAiMovementQuery.cs):

1. Existing `DefaultAbilityAiWork` freshly binds the definition, actor, owned
   primary slot and initialized roster lists. `NormalFocusedScenario` certifies
   the normal focused scenario without special/stay-near/reflection overrides.
   This is a hypothetical scenario entry, not automatic policy dispatch.
2. `DecisionFlagsRefresh` runs one effect-free clock event privately, retaining
   diagnostics and decision RNG. Target facts use live positions/teams/delays,
   with actor delay cached before refresh, as in admitted physical preparation.
   No simulated CT or movement is adopted live.
3. Shared `AiRosterNearest.FindEligibleEnemy` derives focus with source-order
   ties and actor fallback. Framework's new `DefaultAbilityMovement.QueryFocused`
   exposes the existing flat kernel's target-distance rows. The original
   `Query` signature, geometry and command consumers are preserved.
4. `ScenariosBuild` calls shared `AiScenarioLayout.Partition`, then
   `ReachableTilesScore`. Each tile starts from the original immutable battle
   while RNG continues from the previous hypothesis. Placement changes only
   the private actor occupant; no MP is spent and no ability executes.
5. Shared `AiRosterProximity` and `AiScenarioLayout.Finish` choose destinations
   and masks. Non-Blue out-of-reach adjustment draws once after scoring; Blue
   does not draw. `RankBaselineSeed` uses D0's written priority, returning
   decided false, signed 16-bit priority and hit 100.

Enemy reach includes structural bare Attack range 1 and currently affordable
initialized enemy-targeting primary entries. The source adds Move to the
smallest range-plus-area; reach is not the selected spell's range alone. A
usable trajectory entry also controls the existing enemy range floor. Disabled
initialized Frog remains in scoring statistics, without contributing usable
reach. The ability diagnostic retains initialized usage; aim-pass consumption
belongs to the later search stage.

`DefaultAbilityAiClock.RunMoveOnly` shares the existing clock core. It refunds
20 CT for unused ACT and 20 for unused Move, capped at 60, then advances to one
ready-unit event. Existing public `Run` still requires ACT to be marked and
retains spell/charge behavior. Clocks preserve source order, overflow checks
and upkeep RNG. A moved hypothesis consumes Move privately; an origin hypothesis
retains its incoming movement flag.

## Bounds and provenance

The first adapter admits certified offensive normal general scenarios: at most
16 active living bare humans, canonical source-slot turn tie keys, no pending
actions or learning report, an owned
available enemy-targeting slot, and a noncritical actor outside the Blue
healer-focus HP threshold. Actor lists with ally-targeting, random-use, status,
equipment, stat or MP effects reject. Mixed/healer lists and retreat policy need
separate support. Existing work guards statuses, equipment/RSM and special
selected shapes. The profile grants no automatic control of a player unit.

Movement requires the definition's explicit dry flat cost1 ground, one lower
layer, at most 16 by 16 panels and Move at most 124. Allies may be traversed but
cannot be destinations; enemies block traversal. Focus-distance propagation
clears occupants and uses the existing flat kernel independently of occupied
movement cells. Terrain, water, mounts, upper layers, traps, crystals, Golem
and frame/VSync continuations remain outside this domain.

The output reports `ScoreMask`, scenario1/2, each hypothesis's clock/state/RNG,
and `AiMovementCellProvenance` for reachable cells. Missing `WrittenPriority`
stays null. Scenario2 is complemented before actor reinsertion into scenario1,
so an unscored actor origin can appear in both. No retained workspace is
accepted or reconstructed. Only D0's known fresh write seeds the baseline.
Coordinates remain canonical typed lower-layer coordinates; this does not
broaden the source's raw fourth-byte comparison.

Every failure discards all diagnostics and restores incoming decision RNG,
including failure after refresh and an earlier scored tile. Input battle,
learned slots, resources and live turn RNG remain unchanged on success too.
Known independent flags are immutable booleans. Ranks, counts, phases and opaque
unknown bits retain their meanings; owner drafts remain unchanged.

## Validation and next work

Sixteen new managed cases cover coordinate ties, D0/D1 separation, baseline
narrowing including -32768, overlapping masks and unknown priorities, occupancy,
consumed Move, CT refunds/cap, both non-Blue reach branches, tick exhaustion,
late RNG exhaustion, replay and definition/profile rejection.

The two-unit in-reach case writes 14 cells, selects D0=(2,3,0), D1=(0,4,0), and
ranks priority 16/hit 100. One refresh plus 14 scoring events consumes 15 draws,
ending seed 39090480 from 57. The out-of-reach non-Blue case writes 5 cells then
draws the adjustment: seed 57 gives raw 19852, halves reach 4 to 2 and chooses (3,2,0);
seed 0 gives raw 16045, retains reach 4 and chooses (2,2,0). An independent affine
LCG calculation checked those RNG literals.

[ZOD-438](ordinary-spell-cast-origins.md) now enumerates fresh pass-specific cast
origins using these diagnostics, source masks and known priority provenance,
rejecting unread priorities and count aliases. Moved-origin
casting, shared aim consumption during evaluation, full list dispatch,
insertion/lottery, magic commands and autonomous turns remain later milestones.
Healer policies, general statuses, terrain, terminal/frame RNG, editor and cone
targeting retain their gates. Native presentation was not rerun for this pure
diagnostic entry, which is not connected to the host.
