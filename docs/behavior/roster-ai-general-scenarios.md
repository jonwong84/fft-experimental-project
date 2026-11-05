# Roster general preparation and movement — ZOD-288/289

Implemented 2026-11-05 from managed base
`532b1919e956578123e758cdea0386311e74b064`, following
[ZOD-287](../architecture/zod-287-roster-general-search.md). Read-only fft_decomp
was checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/roster-general-source-v1.json) records 29
functions across eight files. No original or C program was executed.

## Fresh general entry — ZOD-288

`RosterAiGeneralPreparation.Evaluate` owns one fresh
[roster preparation](roster-ai-preparation.md). Its false-default context requires
no direct player control and no special decision behavior. It derives source
manual/general admission from restored live HP, team, list summaries, available
retreat support and updated retained support policy. Initial configured settings
are overwritten in this non-special automatic domain; none are assumed manual.

Blue floor(HP*128/maxHP) below 45 requires the unimplemented healer/crystal focus
branch and rejects. Exactly 45 admits ordinary enemy focus. Critical plus
available support rejects conditional retreat before FadingLife dispatch.
Non-Blue MostlySupport with available actor support policy rejects FadingLife;
a retained unavailable flag can admit general search despite MostlySupport.
The first minimum-share ally override and non-Blue failure propagation remain
owned by preparation. A Critical actor can enter general search when every ally
is Critical and support becomes unavailable. No excluded policy is approximated.
Rejection publishes no partial diagnostics or RNG; it does not execute the policy.

Accepted entry derives nearest eligible enemy through the shared roster query,
including first source-slot ties and actor fallback. It owns the actual legal
roster origins and source target-distance rows. Self-focus clears all queried
distances to zero. Forward movement permits ally transit and enemy blockers;
reverse target propagation clears unit records, giving Manhattan distance in
this flat domain. Diagnostic ANY includes actor and all considered allies and
enemies. Proximity remains a separate imminent-enemy sum against the captured
actor delay and restored live CT. These outputs do not execute an ability search.

## General scenarios — ZOD-289

`RosterAiGeneralScenarios.Evaluate` owns that entire fresh prefix and one ordered
scan of the selected movement mask, using the shared private whole-roster
projection/clock/scoring kernels. It accepts owned live inputs and initialized
lists, never a caller-populated preparation or cached simulation. Per-tile
diagnostics use a roster outcome with no invented pair target. Each tile starts
from the same restored live state; only the decision RNG chains between tiles.

`AiScenarioLayout` now owns the private filter, destination, reach-adjustment,
mask and priority-provenance arithmetic shared with existing pair scenarios.
Old public pair APIs, guards, command drivers and streams remain unchanged.
Scores include every allied/enemy team term in source order. Tiles retain
negated signed-16 score cells and byte proximity; packed comparison and last
tile ties match source. Out-of-reach non-Blue adjustment occurs after all tile
simulations; Blue consumes no adjustment draw. Scenario 2 precedes actor insertion
into scenario 1, permitting overlap. Unscored cells retain unknown priorities.
D0's selected movement baseline is freshly written; later cast-origin readers
must establish their own written/retained/unread provenance.

For the literal 8x4 map with actor (1,1), enemy (3,1), ally (1,2) and Move 1,
M0 is (1,0), (0,1), (1,1), (2,1). Distance 2 is within reach 2. Each tile's
absolute score is 485; proximity is 7,7,8,9. D1 is (0,1), the later of the two
equal best tiles. Melee D0 is (2,1); a Stone occurrence raises D0's bound to 3,
choosing (0,1). Post-ACT retains reach but copies D1. Starting word 1/count 7,
one refresh and four tile draws end at word 4182499122/count 12.

Move the enemy to (7,1): only (2,1) receives a score. The actor belongs to both
scenario masks with unknown priority. Refresh, one tile and non-Blue adjustment
end at word 662824084/count 10; adjustment raw 10113 has modulo 5. Seed 20 gives
raw 12502/modulo 0 and halves reach to 1. Blue ends after two draws at word
2524885223/count 9. Already-moved or zero-Move empty filters retain the origin.
With no enemies, self-focus makes every target distance zero; the last tile wins
and D0's negative bound uses unrestricted fallback, without inventing a target.

## Validation and limits

Eighteen hand-authored independent source descriptions cover policy boundaries,
focus/masks/ties and literal RNG. Three new Python tests check those descriptions,
input isolation and malformed/tampered claims; they do not run managed code.
Eight new managed cases cover those literals, pair parity, false-default context,
restored delays, all enemy teams, per-tile parity, exact/late draw and tick
exhaustion, unknown priorities, either actor, collection reordering and immutable
replay. Existing complete-turn/command/recovery regressions remain unchanged.

Locked restore and Release build pass with zero warnings/errors. **4,343 .NET
cases** (1,483 Framework, 1,115 AI) and **554 Python tests** pass. Pinned source/
prepared-data, guide, local-link and coverage checks pass. The
[implementation receipt](../evidence/ai/roster-general-implementation-v1.json)
records checks; seven full TRX reports and logs are ignored under
`scratch/roster-scenarios-287-289/`. Use the existing
[pinned Python dependencies](accumulate-ai.md#validation). No historical snapshot
or UI validation was repeated. All nine owner drafts, including AbilityToggles,
are unchanged; no Framework/Content/host/editor or project/lock/package changes.

Living ordinary compact-16, positive-Speed/MP0, stable teams, raw Move <=124 and
strict flat geometry remain explicit. No actual grants/effect bindings are
inferred by these summary/scenario queries. Action/effect/target traversal,
rank insertion, lottery, roster AI command/turn execution, retreat/FadingLife/
healer/special/control policies, other statuses/maps, corpse lifecycle, automatic
facing, terminal routing and original frame RNG remain open. Owner drafts,
host/editor, broad replacement interfaces and ZOD-106 remain separate/deferred.
ZOD-290 next traces roster cast-origin priority reads and supported-action search.
