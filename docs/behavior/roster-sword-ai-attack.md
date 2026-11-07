# Fresh ordinary roster Attack — ZOD-296

Implemented 2026-11-05 from managed base
`6094d3b06635dcfd0b659ae4a7192363786c1df5`, following the
[source/design trace](../architecture/zod-296-roster-attack-enumeration.md).
Read-only fft_decomp was checked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/roster-attack-source-v1.json) pins 55 functions
across sixteen files. No original or C program was executed.

`RosterSwordAiAttackEntry.Evaluate` owns one fresh general prefix and the first
initialized human Attack occurrence. It derives usable/active masks and a fresh
empty best record; only the unrepresented ordinary tile/exhaustive Attack policy
remains an explicit false-default fact. The actual typed/equipped sword and
numeric envelope are checked once for entered scans, including empty ones.
Post-ACT skips action work before weapon invocation checks.

Shared private cast geometry unions all active useful enemy teams' range1 masks,
then intersects scenario1. Origins and target panels both use map layer/y/x
order; target source-slot order is not aim-panel order. The actor origin alone
substitutes D1 and ACT-first. Other origins are Move-first to themselves. Every
eligible origin/panel is evaluated through the shared private candidate owner
without refreshing/rebuilding movement per target or accepting cached results.

Every candidate restores actual input HP/CT/positions; supported RNG continues.
Zero accuracy keeps its four formula draws and does not consume an aim or stop
later origins. A projected KO does not remove a later candidate's target.
Strict rank then lower final-destination proximity can replace best; complete
ties retain the first scored candidate. The final best compares with D0's owned
movement baseline. No ranked array, lottery or command is selected. Attack reads
no ranked-origin priority key, even if a different listed entry would require
an unknown reinserted actor value. Other entries affect preparation only.

On the literal 8x4 Move1 map, actor (1,1), enemy (2,1), ally (1,2) yields one
ACT-first candidate to D1=(0,1). Prefix4 plus candidate5 draws end at word
3210001534/count16 from word1/count7. Score511 beats baseline485. With two enemy
panels, (1,0) is visited before (2,1) even when the latter has the earlier source
slot; a complete score/proximity tie retains the first panel. A Move2 tape visits
origins (2,0), (1,1), (2,2), continuing through Scored/NotValuable/Scored and ending
at word1343201072/count30. Repeated KO hypotheses restore the same initial target.
An enemy outside the imminent-proximity set remains an active instant target;
zero proximity can change D1 to the actor rather than removing that enemy.

Ten independent literal tapes and three new Python tests check source ordering,
draws, restoration, comparisons and malformed/tampered claims. Eight new managed
cases cover those tapes, pair parity, unread keys, post-ACT/empty binding checks,
exact/late RNG and corpse-clock failures, either actor and immutable replay.
Locked restore and Release build pass with zero warnings/errors. **4,371 .NET
cases** (1,483 Framework, 1,143 AI) and **563 Python tests** pass. Source/prepared,
guide/link/coverage audits pass. The
[implementation receipt](../evidence/ai/roster-attack-implementation-v1.json)
records evidence; seven full TRX reports/logs are ignored under
`scratch/roster-attack-296/`. Use the existing
[pinned Python setup](accumulate-ai.md#validation). Historical snapshots and manual
UI validation were not repeated. Nine owner drafts, including AbilityToggles,
are unchanged; no Framework/Content/host/editor/project/lock/package changes.

ZOD-297 next traces the roster Throw Stone effect, trajectory and ranked-aim
boundary before selecting its managed children. Full list composition/lottery,
roster AI commands/turns, other abilities/policies/statuses/maps, full corpse
lifecycle, automatic facing, terminal routing and original frame RNG remain
open. Existing pair APIs/guards/streams are unchanged. Owner drafts, host/editor,
broad replacement interfaces and ZOD-106 remain separate/deferred.
