# AI target selection and scenario retrospective — ZOD-433

Completed 2026-11-07 as a bounded batch of the
[ZOD-428 programme](../development/documentation-review-programme.md), following
the [preparation review](shared-ai-preparation-review.md). No new defect was
confirmed within the admitted target/scenario paths. No runtime code, test,
fixture, owner draft or historical receipt changed.

Managed baseline: `f169b4559a839ccbb2ad82611a65001b839b5ed4`. The read-only
reference was rechecked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [receipt](../evidence/ai/target-scenario-review-v1.json) pins current inputs,
source symbols, independent geometric/literal checks and validation. Fingerprints
include broader dependencies and do not certify all their branches.

## Separate target memberships

`AiTargetRoster` owns copied, immutable, source-ordered facts. Stable IDs are
separate from original slots. `AiRosterEligibility` deliberately distinguishes
live untargetability from cached out-of-play: source refresh calls the live
helper before updating Enemy, and an excluded record retains other old fields.
The Transparent enemy/rider exception uses a resolved four-bit partner lookup;
the managed query never masks a stable ID. Current executable profiles exclude
those mount/control/status cases rather than fabricating their facts.

`AiRosterNearest.FindEligibleEnemy` matches only source ELIGIBLE_ENEMY. It checks
live untargetability, Petrify, cached Enemy/DeadWithoutReraise/Considered, working
membership and Blood Suck. Cached Untargetable does not substitute for the live
helper. Dead with Reraise is not universally excluded. Manhattan X/Y ignores
height; strict improvement preserves the first source-slot tie. No eligible
enemy returns the actor, with a null diagnostic distance. This does not implement
the different healer/crystal, imminent-height or configured focus searches.

`AiRosterActiveTargets.Select` instead rebuilds every active flag from cached
Untargetable, Considered/Confusion and its mode. Confusion bypasses Considered
only. Unknown signed modes take ANY; source target255's actor exclusion is a
separate semantic argument. STATUS accepts Dead/Petrify/Critical/DeathSentence
without a side filter. This method does not rerun live/out-of-play/usefulness
filters or sanitize stale input. Missing specific targets produce an empty mask.

`AiRosterProximity.Evaluate` has a third membership: cached out-of-play, Enemy,
resolved turn delay<=the pre-refresh actor-delay byte, and no disabling status
through that byte. Neither nearest eligibility, Considered nor the active mask
replaces it. Each scenario0 tile receives the byte sum of max(10-Manhattan,0),
ignoring height. Distances9/10/11 contribute1/0/0. Only selected cells are written;
unselected cells remain unknown/retained. The20-enemy source-slot arithmetic
example sums81 on both layers, but does not establish21-active-unit command
admission. The32-unit capacity probe likewise makes no original-battle claim.

## Restored facts, movement and scenarios

`AiOrdinaryPairRoster` and `RosterAiGeneralPreparation` derive target facts from
restored live positions and CT, not the hypothetical next-ready units from
refresh. CT15/Speed5 and CT95/Speed5 retain delays17/1 and origin proximity9,
despite a private refresh returning actor CT40/60 and peer CT0. Supported roster
general entry retains the existing control, healer-focus, retreat and Fading Life
rejections. Self-focus clears target-distance rows, as source `scenarios_build`
does; it does not invent an enemy.

`FlatRosterMovement` separates ally transit, enemy blockers and occupied endpoints.
Its shared flat kernel supplies row-ordered legal origins. Reverse source
`ai_move_propagate_from_target` clears unit records; target-distance estimates
therefore ignore forward occupancy in this certified cost-one rectangle. This
justifies Manhattan target rows here, not on arbitrary terrain. Original17-unit
movement storage,21 battle slots and16 AI compact records remain distinct; the
owning AI profile applies the stronger16-active-unit limit.

`AiScenarioLayout` shares partition/destination arithmetic with pair and roster
owners. In reach, score tiles with target distance<=Move+actor distance; outside
reach, use distance<=actor distance-1. An empty filter copies the full reachable
mask. Movement hypotheses restart from restored live units while chaining only
decision RNG in layer/Y/X order. Outside-reach non-Blue draws rand()%7 after all
tile evaluations and halves reach only on0; Blue draws none there.

The priority cell is negated signed16; its packed signed key is
priority*65536+byte proximity. Priority-first and distance-first selectors retain
the last scanned tile on complete ties, unlike nearest-unit selection. D1 uses
the ordinary destination choice. Pre-ACT in-reach D0 applies distance-1, with an
enemy/ranged floor3, then unrestricted distance-first fallback. Post-ACT and
outside-reach paths copy D1. Scenario2 is the unscored remainder before the actor
is inserted into scenario1, so the origin can belong to both. Its priority is
still unknown if unscored; proximity is not a substitute score.

For actor(1,1), enemy(3,1), ally(1,2), Move1, the source-order mask is
(1,0),(0,1),(1,1),(2,1), with proximity7,7,8,9 and score485 per tile. D1=(0,1),
melee D0=(2,1), ranged D0=(0,1). Moving the enemy to(7,1) scores only(2,1);
the unscored origin belongs to both scenario masks. These literals were
recalculated independently of the managed layout owner.

`AiRosterCastGeometry` intersects scenario origins with useful active enemies'
range; priority binding distinguishes fresh from explicitly retained cells and
rejects unknown ranked reads. These are partial integration edges, not a full
review of origin traversal, rank admission, lottery or checked commands.

## Ordinary magic aim integration

`OrdinarySpellAiAimEnumeration` preserves current-origin useful masks, considered
CT deadlines, unit groups before tile groups, layer/Y/X aim order, and the
Follow-target center XOR. A moving enemy is removed from the tile-target group;
an allied preference does not apply that enemy exclusion. Occupied, walkable aims
outside Move range may cover units outside direct cast range. Movement origins,
cast range, target-area coverage and aim panels are separate concepts.

The admitted single dry sheet, default menu, living ordinary roster, safe
range+area<=7 and no special trajectory/shape avoid broader source branches.
An independent exhaustive check compared the pinned eight row templates against
Manhattan diamonds for radii0..7 and every center/row of a16x16 grid:32,768 row
comparisons matched. Radius8..15 table overreads, cross-sheet/terrain constraints,
Monster Skill, Reflect and useful-self searches remain outside this slice.
Full magic movement-origin study remains ZOD-426; autonomous magic is not enabled.

## Validation and remaining work

Existing source audits pass17 roster-targeting and18 general-scenario descriptions,
plus13 preparation descriptions and the magic aim/pass/deadline checks recorded
in the receipt. The current rebuilt suite passes5,181 .NET tests, including1,384
AI, and794 Python tests. No reference C, original executable or native presentation
was executed. Current tests and fixtures were inspected rather than rewritten
to match the implementation; no new regression was needed without a defect.

Owner controller edit, ten owner draft blobs, immutable JobStats and approved
targeting formatting are preserved. Next is ZOD-434, reviewing rank admission,
lottery and ordinary fallback boundaries. Full origin/command integration,
special policies/maps, status/corpse lifecycle and frame RNG remain separate.
ZOD-432 still requires the final readability walkthrough after full AI coverage.
