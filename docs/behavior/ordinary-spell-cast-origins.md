# Ordinary spell cast origins — ZOD-438

`DefaultAbilityAiOriginQuery.Evaluate` now enumerates potential cast origins and
selectable aims for one explicitly selected movement scenario. It owns fresh
slot/list binding and movement preparation internally. It accepts no cached
movement outcome, retained score or execution token.

Managed base: `ae4274d26d5e8c3b572151c76d14fbb437ad2a6f`. Read-only decompilation:
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source study](ordinary-spell-movement-origins.md) and
[movement baseline query](ordinary-spell-movement-baselines.md) remain the source
and admission foundations. The
[implementation receipt](../evidence/ai/spell-cast-origins-implementation-v1.json)
records source/prepared checks, managed regressions and owner preservation.
No reference C or original executable ran.

## Entry point and routine sequence

Start at
[`DefaultAbilityAiOriginQuery`](../../src/Zodiac.Framework.Ai/DefaultAbilities/DefaultAbilityAiOriginQuery.cs).
Its request selects `Main` (source scenario 1) or `Complement` (scenario 2). This
diagnostic choice does not implement the source dispatcher's conditions for
entering those scenarios.

1. `DefaultAbilityAiWork` binds the current definition, actor and owned primary
   slot once. Shared `DefaultAbilityAiAimEnumeration.BuildPotentialPasses` derives
   completed working usage, deadline groups and walkable aim masks before
   selectable-range intersection. The original `Build` continues to clip those
   masks at the current actor origin for existing fixed-origin consumers.
2. Internal `DefaultAbilityAiMovementQuery.Build` reuses that same fresh work
   owner. It runs one refresh and movement scan with one decision RNG. Its
   public query and admission remain intact; no physical snapshot is fabricated.
3. `AiCastAimTilesBuild` completes each pass's cast-origin mask by intersecting
   the chosen scenario with useful non-actor target masks. Unit passes use
   Range; tile passes use Range plus Area. The aim mask remains independent of
   movement reach and may include occupied panels outside Move.
4. Ranked origins use shared `AiRosterCastGeometry.BindFresh`. Every read
   requires a known fresh signed priority. Keys minimize `priority * 65536 +
   foe proximity`, with higher layer/Y/X winning exact ties. Non-exhaustive
   abilities always use this mode, even for small possibility counts. Exhaustive
   origins instead scan layer/Y/X ascending without reading priorities.
5. `OriginDestinationFinalize` keeps cast origin and final movement destination
   separate. At the actor's tile it marks ACT-before-MOVE and substitutes D1;
   both source candidate 1 and candidate 2 use that same scenario destination.
   Other origins are MOVE-before-ACT with destination equal to cast origin.
6. `OriginAimTilesBuild` implements only the mask portion of
   `origin_aim_tiles_evaluate`: it calls shared Framework
   `DefaultAbilityTargeting.Range` at that cast origin, then intersects the
   pass-wide aims in source map order. Unit anchors retain their source unit IDs.

`origin_next_take` mutates the source mask/count. This potential enumerator
reports the equivalent initial visit order without consuming aims or simulating
the candidate-dependent exits. The source's origin priority is bound before
actor-origin destination substitution; destination proximity is a separate
future best-comparison input.

## Counts, unknown values and boundaries

The query preserves ZOD-437's certified offensive normal general domain and
ZOD-424's nonzero range, safe Range+Area at most 7, consistent FollowTarget and
dry flat lower-layer constraints. Useful self paths, arcs, direct trajectories,
special shapes, mixed/healer lists and retained workspace remain gated.
Canonical typed coordinates represent the admitted source coordinate word with
its fourth byte zero; general raw padding values are not accepted.

Both raw pass aim count and cast-origin count must fit an unsigned byte without
aliasing. A genuine 256-panel union rejects explicitly; the implementation does
not reinterpret the source's wrapped zero counter. Empty masks are normal.
Ranked mode reads no priorities when its aim count is zero. Exhaustive mode
does not require priority reads for origin visitation.

Fresh movement can leave the actor's priority unwritten, including when it
belongs to both scenario masks. If a ranked origin mask includes that tile,
the entire query rejects. A mask that excludes the unknown tile can proceed.
No zero or new score is substituted for unread workspace. The complement can
also yield empty origin masks without consulting unknown cells.

Results contain pass-wide masks, initial origin order, ACT/Move order,
origin-bound priority provenance and each origin's independent selectable aims.
Even when a pass requests consumption, the same aim may appear at several
potential origins. No formula, MP spending, processing-done check, consumed-aim
mutation or candidate ranking occurs. Enumeration draws no RNG after movement.
Any unsupported radius, count, unknown read or late movement-clock failure
discards all partial diagnostics and restores the entry decision RNG. The live
battle, choices, learned slots and resources remain unchanged on success too.

## Validation and next milestone

Twelve new managed cases cover unit versus tile origin masks, ranked reverse
ties, exhaustive order, actor-origin D1 substitution, distinct origin/destination
proximity, occupied/out-of-Move aims, per-origin range intersections, preserved
potential aims, empty/enemy-free masks, both scenarios' unknown reads, replay,
late failure, unsafe radii and a real source counter alias.

The Release build passes with zero warnings/errors; all 5,217 .NET and 802 Python
tests pass. See the receipt for seven project reports and source/prepared audits.

In the two-unit example, unit passes have 8 potential origins and tile passes 12.
Tile aims are (4,1), (3,2), (5,2), (4,3). The first ranked tile origin (1,4)
can select (3,2) and (4,3), while the actor's current origin can select only (3,2).
The actor-origin key has foe proximity 6 while its D1 destination (0,4) has 4.
The entire query retains the existing 15 movement-preparation draws, ending
seed 39090480 from 57. A 16-unit, 16-by16 scenario unions all 256 aim panels and
rejects transactionally after its private movement scan.

[ZOD-439](ordinary-spell-origin-evaluation.md) now evaluates one freshly
enumerated origin/pass through shared simulation, preserving ACT/Move order,
one RNG, baseline admission and unresolved-origin exit. Cross-origin evaluation/consumption, broad dispatch,
insertion/lottery, commands and autonomous magic remain later milestones.
General statuses, equipment/terrain/mounts, Golem, terminal/frame RNG, editor
and custom cone work retain their gates. This pure diagnostic entry is not
connected to the host; no new native presentation claim is made.
