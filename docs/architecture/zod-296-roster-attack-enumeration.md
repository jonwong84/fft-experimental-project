# Fresh ordinary roster Attack enumeration — ZOD-296

Source/design, 2026-11-05, managed base
`6094d3b06635dcfd0b659ae4a7192363786c1df5`. Read-only fft_decomp checked clean
at `11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/roster-attack-source-v1.json) pins functions/files.
No original or C program is executed. This implements the bounded child from
[ZOD-294](zod-294-roster-sword-candidate.md), following the detailed
[ordinary Attack scan](zod-130-ai-scan-contract.md) and
[roster search](zod-290-roster-action-search.md) traces.

## Source pass and ordering

`general_select` owns one movement scenario build, then ANY active targets and
source list traversal. The first initialized human entry is Attack. This child
isolates that occurrence after a privately owned fresh general prefix; other
listed actions can affect preparation but their outcomes are not run here.
After ACT, Attack is unusable and its outcome gate skips all action work.

The ordinary sword summary supplies tile targeting, range1/area0/CT0, exhaustive
origin scan and no trajectory/reflect/follow-target/map/random bits. Keep an
explicit ordinary Attack AI-policy fact in addition to the actual typed/equipped
weapon. A fresh owner derives its empty best record, original masks and active
membership rather than asking the caller to certify supplied results.

`useful_targets_filter` keeps every active ordinary enemy and clears allies.
`unit_can_move_by_charge_deadline` returns zero for CT0, so already-ready enemies
remain ability targets; imminent proximity membership is a separate set.
`ai_cast_aim_tiles_build` intersects scenario1 with the union of range1 diamonds
around all useful enemies. Aim panels are those enemy positions. Original input
is restored between candidates, so projected KO cannot remove a later target.

`origin_reachable_evaluate` clears aim consumption and walks layer/y/x origins.
Attack reads no ranked-origin priority key and never calls `origin_next_take`.
For each origin, `origin_destination_finalize` substitutes D1 and ACT-first only
when that origin is the actor's original tile; otherwise the origin itself is
the destination and Move-first. `origin_aim_tiles_evaluate` intersects actual
range and visits target panels in layer/y/x order, **not unit source-slot order**.
On this dry flat distinct-unit range1 domain that is every cardinal adjacent
enemy panel. No line/arc filter is invoked by this Attack policy.

One formula/projection/clock evaluation occurs per origin/panel. Keep supported
NotValuable draws and continue scanning; no aim is consumed on zero accuracy,
and no early exit is permitted for hit100, KO or a good incumbent. Equal ranks
use lower current foe proximity of the final destination; complete ties retain
the earliest evaluated candidate. Every compact16 absolute score is safely
above the fresh source -32767 sentinel, so an absent managed incumbent is exact.
Compare the final best rank with D0's owned move baseline without adding a ranked
array or lottery. Unknown unscored cells remain unknown and unread.

## Managed boundary

`RosterSwordAiAttackEntry` will own one fresh general prefix and this first
occurrence. Reuse shared roster cast geometry and the private selected-target
candidate evaluator. Bind the sword/numeric envelope once for entered scans,
including empty scans; do not require an invocation for post-ACT skips. Never
refresh or rebuild scenarios per target, accept a public cached projection,
fake a pair target or call actual combat for a prediction. Failures return the
whole query's incoming RNG, with no partial prefix/candidates.

Require existing living ordinary compact16, stable teams, MP0/positive Speed,
flat geometry, general-policy admission and normal scoring. Definition storage
remains uncapped. Stone, list composition/ranked lottery, roster AI commands,
other abilities/policies/statuses/maps, full corpse lifecycle, automatic facing,
terminal routing, frame RNG, owner drafts, host/editor, broad replacement
interfaces and ZOD-106 remain separate/deferred.

## Validation

[Implementation](../behavior/roster-sword-ai-attack.md) passes ten independent
source tapes, eight new managed cases and three new Python tests. Full validation
passes with 4,371 .NET cases, 563 Python tests, zero-warning/error Release build
and pinned source/prepared/guide/coverage audits. No original/C execution,
full-list selection or roster AI command activation is claimed.
