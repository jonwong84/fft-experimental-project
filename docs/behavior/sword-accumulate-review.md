# Sword Attack and Accumulate retrospective — ZOD-429

Completed 2026-11-07 as the second bounded batch of the
[ZOD-428 programme](../development/documentation-review-programme.md), following
the [physical/evasion review](physical-pipeline-review.md). No new defect was
confirmed in the admitted mechanics and integration edges. No runtime code,
content definition, owner draft or historical fixture changed.

Managed baseline: `b16c11d` (full revision in the
[review receipt](../evidence/combat/sword-accumulate-review-v1.json)). Read-only
reference `11464943bbdbd2a0af022d90cb7f10455cf911df` was rechecked clean. The
receipt pins eight source/metadata files, 27 functions and 27 managed paths.
Those fingerprints identify review inputs; they do not claim every branch of
the source dependencies or shared managed owners has been reviewed.

## Entry points and source dispatch

- Actual sword actions enter `AttackBattleSimulator.Attack` or `AttackRoster`.
  The legacy overload remains edge-only; explicit flat profiles admit bounded
  critical displacement. `BoundedSwordWeapon` is a validated neutral range-one
  formula1 projection, not an arbitrary equipment record.
- Actual self PA+1 enters `ImmediateAbilityBattleSimulator.Apply`, with explicit
  grants and an `ImmediateAbilityBattleProfile`. `AccumulateFormula` owns the
  bounded arithmetic. This is not a general implementation of every formula36
  parameter or status/equipment context.
- Sword AI enters `SwordAiFormulaProjection` through its pair/roster candidate
  owners, followed by their private clock and absolute scoring. Accumulate AI
  filters usefulness before `AiSelfStatProjection` and candidate scoring. These
  diagnostic results are not authoritative battle mutations or commands.

The prepared BATTLE.BIN hash remains
`7af579c7bcfd46f21f8d6886acb6aae156474dace667553e76079be7c7d6db94`
(1,397,096 bytes; load0x80067000). Formula table0x8018F614 entries1 and0x36
point to `formula_weapon_damage` and `formula_add_pa_y`. Ordered calls in the
sword handler, weapon calculator, physical calculator and critical helper match
the pinned metadata. Formula36 is a leaf without RNG calls.

Prepared SCUS_942.21 remains356,352 bytes, SHA-256
`287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562`.
Fresh byte checks retain Broad Sword19's range1/formula1/power4/raw evade5,
flags0x8E, neutral element and empty type0 proc. Ability0's secondary formula
is13, but Attack dispatch selects the weapon formula. The synthetic power5
sword literals remain synthetic. Raw weapon evade contributes0 without Weapon
Guard; capability flags do not grant support/reaction abilities.

Accumulate146 retains primary flags0x80008009 and secondary formula36/Y1.
Primary Target Allies/Affects Stats/Target Map Tiles/AI Usable describe AI
routing. They do not execute the PA change. Synthetic effect bindings and
learned grants alone do not establish those retail AI properties.

## Sword formula and settlement findings

Physical evasion precedes weapon XA/YA setup, charge/element/support/status/
compatibility preparation, critical, multiplication and conditional proc. The
admitted neutral, uncharged, ordinary-human profile reduces this to current
PA (floored to1 by ordinary neutral zodiac) times selected power. It retains
the four evasion draws, including zero evades and front class evade100.
Side/back class evasion is0 in this profile.

Execution rolls critical at scaled percent<4 and, on a critical, adds
`floor(rawBonus * baseXA / 32768)` to XA. A critical attempts knockback directly;
there is no Brave contest or extra knockback draw for sword Attack. The source
stores multiplication in signed16 before finalization caps positive damage999.
Both execution and AI therefore retain the conservative worst-critical guard
`(2 * max(1, PA) - 1) * power <= 32767`, even when the query cannot crit.
PA0, zero power and a critical with zero bonus are valid within that envelope.

An empty proc does not skip the execution-only19% roll. Supported empty proc
types0/128 cause no additional selection draws or status effects. Miss/normal/
critical resolution consumes4/6/7 draws respectively. AI keeps the four real
evade draws, uses midpoint formula randomness (never critical), omits the
execution-only proc draw, and applies normal damage without probability weighting.
Accuracy0 is not valuable; discarding that candidate retains its supported
decision-stream progress. A full player Attack-preview API remains separate.

Finalization caps damage before comparing it with target HP and cancels lethal
knockback. Actual application clamps HP, assigns Critical at HP<=floor(maxHP/5),
assigns Dead at zero and increments the byte KO count only in execution. AI's
private HP/status projection does not claim execution KO-counter/reward effects.
Resolved damage and actual HP loss remain different diagnostics. Occupied,
off-map and lethal push destinations suppress displacement; the explicit flat
profiles exclude fall damage and the wider terrain/mount/importance cases.

The returned actor keeps its incoming settled facing; temporary attack turning
is restored by the source reward/facing path already recorded in ZOD-112/113.
Act is consumed before effect events, including misses and zero damage.
Movement/turn consequences and atomic rejection are retained managed contracts;
this batch does not claim to revalidate the complete turn/death scheduler.

## Accumulate arithmetic, usefulness and scoring findings

Formula36 writes Y with the increase bit into the byte PA-change field and
marks pseudo-status work. Actual application masks the low seven-bit amount,
changes base PA, clamps1..99, and only on a base change rebuilds effective PA
from base plus equipment bonus. With the explicitly certified zero bonus and
matching base/effective PA1..99, the managed single current PA is equivalent:
1->2,5->6,98->99,99->99. An unchanged base does not repair stale effective
stats in the source; nonzero bonuses and separate/stale base/effective values
are outside this profile, not silently normalized.

PA99 still accepts the action, consumes Act and emits the resolved event while
omitting a PA-changed event. Formula/application uses no RNG. Actual later sword
actions read the updated battle PA rather than immutable job/content values.
The reachable PA99 sword guard uses197*power, so power166 fits (32702) and167
does not (32899). This differs from admitting a high-power sword only at its
initial low PA. Dynamic-PA persistence remains ZOD-106's explicit deferral.

The fresh retail map-target AI entry selects the actor but filters active
source slots first. Each eligible allied slot consumes one `rand() & 1` coin;
even is Useful, odd falls through to None. Ordinary enemies are Adverse without
a coin. The classifier does not inspect PA, including PA99. Another useful ally
can admit the entry pass but cannot make an unuseful actor's self hit valuable.
This is not a once-per-turn coin or an origin-specific reroll.

Each admitted candidate starts from the same original PA; ACT-first and
Move-first do not accumulate one another's hypothetical boost. Their decision
RNG continues through the separately owned hypothetical clocks. The current
effect-free clock cannot branch on PA, so before/after PA can remain diagnostics
outside `AiProjectedUnit`; this is not sufficient for future reactions/charged
physical actions that would consume it.

Included allied stat hits add3 before absolute state scoring, even when PA99
does not change. Hit percent remains100. The source has no direct PA or future
damage score term. Unchanged human equipment contributes seven*51 per unit;
HP contributes floor(HP*128/maxHP), Critical=-32 and Dead=-192, with enemies
negated and signed16 priority narrowed per source slot. The pair examples
remain3 for full/full, -132 for Critical actor versus full enemy and138 for
the reverse. These are absolute state values, not incremental PA benefit.
Broader snapshot/clock, ranks, lottery and command branches remain open.

## Validation, evidence and remaining work

Seven sword literals were independently recalculated from the pinned arithmetic
and BIOS stream: ordinary25/six draws, critical45/seven, frontal miss/four,
PA0 floor, zero critical bonus, zero power with unchanged draw count and cap999.
Their exact raw streams/words are in the receipt. These are offline source
descriptions; they are not original/C execution. Existing audit tools additionally
revalidated18 Accumulate descriptions,13 roster-sword descriptions and35
knockback vectors, including the source fingerprints and prepared Accumulate
records. Their historical production-execution disclaimers remain intact.

The complete current Release tests passed: **5,155 .NET** (including1,358 AI)
and **794 Python**. No new test was needed to mirror unchanged code. Existing
tests exercise literal streams, signed damage gates, fresh/current PA, PA caps,
post-action damage, roster ally coins, scoring, exact draw budgets, atomic failure,
turn events, facing, knockback and content/grant restrictions. All ten owner
blobs and the approved formatting edit remain unchanged. Native presentation
was not rerun. No historical evidence erratum was identified in this batch;
ZOD-427's separate formula32 correction remains recorded there.

ZOD-429 is complete within this stated domain. ZOD-428 remains active, with no
full inventory group closed. ZOD-430 next reviews shared AI baseline/simulation/
clock/strike-accounting boundaries; ZOD-426 remains supporting movement-origin
study. General weapons, equipment/status effects, full player preview, frame RNG,
death/revival scheduling, rewards and editor work retain their existing scopes.
