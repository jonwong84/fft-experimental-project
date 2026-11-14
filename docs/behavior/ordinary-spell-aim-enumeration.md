# Fixed-origin ordinary spell aim lists — ZOD-424

The subsequent [ZOD-425 search](ordinary-spell-aim-ranking.md) now evaluates
and ranks these potential aims. It also corrects the exhaustive-scan consume
diagnostic using the outer caller; the rest of this document describes the
original aim-list milestone and its boundaries.

`DefaultAbilityAiAimQuery.Evaluate(state, catalog, request)` lists potential aims
for one owned, learned primary spell slot at the actor's current origin. Start at
[the public query](../../src/Zodiac.Framework.Ai/DefaultAbilities/DefaultAbilityAiAimQuery.cs)
and follow [the selection owner](../../src/Zodiac.Framework.Ai/DefaultAbilities/Selection/DefaultAbilityAiAimEnumeration.cs).
Each call freshly binds the definition/catalog, current turn, slot, MP, usefulness
and selectable range. Returned records have no command or cached evaluation
authority. There is no RNG input, formula calculation, preparation, MP spending,
ACT commitment, turn advance or live-state adoption.

Managed baseline is `4900745a59684143cd2ca2bd81f503febf1c67fa`; read-only reference
is `11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/spell-aim-enumeration-source-v1.json) retains the
ZOD-418 nine-file/twenty-four-function trace and adds four source functions:
`ability_considerations_set`, `reachable_tiles_build`,
`ai_unit_turn_delay_calculate` and `unit_can_move_after_delay`. No original game
or C code was executed for this ticket.

## Policy and pass order

The [earlier search study](ordinary-spell-aim-search.md) describes the broader
caller. One additional initialization step matters: after
`DefaultAbilityAiParameters.InitializeUsage`, source `ability_considerations_set`
completes the working ConsumeAim flag. ForceConsumeAim wins. Otherwise, unless
ExhaustiveOrigins is set, it consumes when considered CT reaches the actor's
predicted next turn or the ability has no effect on self. An adverse self-effect
does not mean no effect. The short-range evadeable exhaustive rule precedes the
zero-area force rule. Static primary metadata's fourth byte remains separate.
The new query completes this policy locally; existing supplied-aim queries and
parameter diagnostics retain their established contracts.

For admitted living ordinary units, delay is ceil(max(0,100-CT)/Speed), using
shared roster arithmetic. A move opportunity requires nonzero considered CT and
delay <= considered CT. Raw considered CT131 remains131, independent of actual
preparation's masked counter3. Already-used movement and the unit's Move range
do not suppress this prediction.

With FollowTarget, ConsumeAim yields one unit pass. Otherwise two complementary
passes appear: useful units moving by the deadline, then those still at the
deadline. Empty unit passes remain diagnostic entries for the source calls.
Unit passes precede tiles. Zero-area following abilities stop after unit passes.
For tile alternatives an enemy-targeting ability excludes useful enemies with
a move opportunity; moving allies are not universally excluded. No remaining
useful target means no tile pass. No useful targets at all means an accepted
empty pass list.

## Masks and geometry

For unit mode, aim masks start at useful units' panels. Tile mode unions safe
source diamond row templates of radius Area around useful units, then XOR-removes
their own-layer centers when FollowTarget is set. Useful memberships retain
source slot order; candidate aims scan layer, Y, X. Unit anchors are looked up
at their current panel. Overlapping target areas do not restore removed centers.
Masks intersect source walkable panels and Framework's freshly computed
selectable range; area coverage can reach a target beyond direct cast range.

`reachable_tiles_build` defines walkable aims by nonblocked, non-cross-section
terrain. Occupancy and movement distance constrain movement origins separately.
Consequently, occupied panels and panels beyond Move can be valid aim centers.
The query uses the ordinary encounter's explicit authored dry flat ground
certificate; player movement destinations do not substitute for this mask.

Current admission requires a single lower sheet, dimensions <=16, nonzero range
and Range+Area <=7. This conservative sum gate keeps both source origin and aim
templates within the explicit radius0..7 table. Source radius8..15 table overreads
and radius>=16 special behavior are not replaced with wider normal diamonds.
Primary FollowTarget requires a secondary unit-or-tile choice. A primary that
does not follow may choose tiles even when the player UI offers both; secondary
NoFollowTarget can also explicitly restrict that choice.

## Validation and remaining work

Thirty managed scenarios cover CT equality/instant/high-bit cases, completed
usage precedence, ally/enemy tile exclusions, empty lists, source slot versus
map scan order, reversed input collections, union/XOR overlap, clipped corners,
bit0/X15, occupied out-of-Move centers, owned grants and atomic rejection.
The [literal source fixture](../fixtures/behavior/spell-aim-enumeration-v1.json)
adds seven working-policy, five deadline and four mask descriptions. Four Python
tests independently check these using the earlier source row-template model,
including mutation detection and input isolation. The
[implementation receipt](../evidence/ai/spell-aim-enumeration-implementation-v1.json)
records full regression and preservation checks.

This profile remains living, ordinary bare-human, neutral-modifier empty-status
HP work. Unsupported statuses/corpse rosters, useful self-target alternatives,
trajectory and special shape branches reject. Reflect is not representable in
the admitted current-status model; Reflectable metadata is allowed without
activating a reflected search. Uncertified ground, zero-range origin special
cases, inconsistent follow choices and unsafe dimensions/radii reject.

These are potential passes, not the exact sequence after evaluation: source
simulation can consume aims or stop early. ZOD-425 will freshly enumerate,
evaluate and compare candidates at this one origin using existing simulation
and shared rank owners, carrying one decision RNG and respecting source early
exits. Multiple movement origins, full ability-list dispatch, lottery, command
execution and autonomous magic turns remain separate. Owner drafts, JobStats,
equipment selectors, general statuses, Golem, frame RNG, editor and custom cone
work are unchanged.
