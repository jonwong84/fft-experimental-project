# Fresh ranked roster Throw Stone entry — ZOD-301

Implemented 2026-11-05 from `9f02cc1`, following the
[selected design](../architecture/zod-301-roster-stone-entry.md). Reference
`11464943bbdbd2a0af022d90cb7f10455cf911df` remains read-only and clean.
[Source receipt](../evidence/ai/roster-stone-entry-source-v1.json) pins 69 functions
across 26 files. No original/C program is executed.

`RosterThrowStoneAiEntry.Evaluate` owns one fresh general prefix and one selected
structural Stone occurrence. Duplicate IDs keep their indexes; absent/other
kinds reject. Other entries affect preparation only. Post-ACT unusable entries
skip before invocation; entered entries check the real typed effect, actor grant,
exact all-unit/ground bindings, PA1..99 and complete actual sprite heights, even
when the resulting scan is empty. The original instant direct-enemy/consume-aim
policy remains explicit. The query provides no command authority.

All active useful enemies contribute range4 origins, independent of focus or
imminent proximity. The shared private priority binder resolves every selected
entry key from fresh writes or retained knowledge with current proximity. Missing
keys fail the query. Origins minimize signed priority*65536+proximity; complete
ties choose the last map tile. Remaining aims use layer/Y/X order. Actor origin
substitutes ACT-first/D1; other origins use Move-first to that origin.

The computed trajectory owner checks every initial occupant before each aim.
Blocked shots retain the aim without RNG; clear aims run one fresh candidate
then consume the aim even at zero accuracy or a losing score. Search stops when
either count reaches zero. Diagnostic visits retain trajectory, candidate index,
remaining aims and the supported RNG after each visit. Final counts distinguish
all-blocked exhaustion from consumed targets.

The private candidate owner shares exact typed setup admission, Stone midpoint
formula, explicit-target state, whole-roster clock and all-team scoring with the
supplied-candidate API. No movement/prefix is repeated. Each hypothesis restores
the live baseline, including prior targets' HP and collision boxes, while RNG
chains. Complete candidate rank/hit/final-proximity ties keep first, separately
from origin ties. Best compares with owned D0; no ranked insertion or lottery.
Success publishes exactly fresh movement writes into a successor workspace.
Any failure returns original workspace/RNG without partial outcome.

Eleven independent [fresh source tapes](../fixtures/behavior/roster-stone-entry-contract-v1.json)
cover one prefix, origin/aim ordering, blocked retry, zero-accuracy consumption,
all-blocked and unknown-key cases, post-ACT, already-moved, duplicate index,
restored KO blockers and restored HP across multiple targets. Eight managed
cases add real retained-workspace provenance, exact map identity, empty/skip
binding checks, complete rank ties, replay, late second-candidate RNG exhaustion
and a corpse-clock tick failure after a successful tick-free prefix.

Validation: locked restore, zero-warning/error Release build, **4,392 .NET cases**
(1,164 AI, 1,483 Framework), **572 Python tests**, source/prepared SDK/guide,
coverage/links and all nine owner-draft checks. Logs:
`scratch/roster-stone-entry-301/`;
[implementation receipt](../evidence/ai/roster-stone-entry-implementation-v1.json).

ZOD-302 next traces one full supported roster list through composition, rank
admission/reset, lottery and workspace publication before selecting children.
Full roster commands/turns, other abilities/policies/statuses/maps, complete
corpse lifecycle, automatic facing, terminal/frame RNG and all documented
host/editor/owner-draft/interface/ZOD-106 deferrals remain separate.
