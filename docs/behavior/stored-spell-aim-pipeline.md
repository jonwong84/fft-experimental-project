# Stored spell aims and pure casting phases

Source is pinned to clean read-only `fft_decomp`
`11464943bbdbd2a0af022d90cb7f10455cf911df`, rechecked from managed base
`befa904548dc596c887a17c14d5a962451c35820`. The existing
[follow source receipt](../evidence/combat/spell-follow-source-v1.json) covers
19 files/26 functions; the [resource receipt](../evidence/combat/magic-resource-source-v1.json)
covers12 files/10 functions. Fresh fingerprint audits and their normalized
descriptions pass under ignored `scratch/spell-aim-364-369/`. No C/original code
is executed. The [prior trace](spell-follow-and-stored-aim.md) owns source detail.

The selected six children separate centre resolution, cast integration, blue
choice classification, certified confirmation capture, immutable charge planning
and ready-plan calculation. They reuse existing geometry, charge, MP and formula
owners. No commands, live state/RNG adoption, global clock, automatic facing,
status/corpse/Golem/terminal/frame RNG or magic AI activation are added. Source
slot reuse and runtime cursor preference remain caller-certified boundaries.
Owner drafts, AbilityToggles, JobStats, host/editor, broad interfaces and ZOD-106
remain separate. The custom forward1/3/5 cone remains unimplemented.

## ZOD-364

`SpellAimResolver` resolves exact targeting type6 from a known matching stable-ID
anchor's current position; all other byte types, including YES7, use stored
signed16 coordinates narrowed to unsigned8. It validates the admitted original
map dimensions/sheets, bounds and blocked centre. Untargetable centres remain
valid at this stage. It does not check anchor activity/status/team or current
caster range, and never falls back to the old tile when anchor facts are missing.

The positive profile certifies an active valid ordinary nondirectional actor and
ability. Original slot-index validation belongs before stable-ID adaptation;
the managed roster has no fixed21-unit cap. Unknown/mismatched anchor data reject
explicitly. This primitive returns only a centre, not green panels or a cast.
Three managed cases pass13 applicable original centre literals, identity/knowledge
guards and map/collision boundaries. Directional/invalid original-slot/actor cases
remain source descriptions outside this admitted primitive.

## ZOD-365

`StoredSpellCastQuery` finds a unique current anchor from certified complete
roster metadata, resolves the centre with `SpellAimResolver`, then delegates the
existing `OrdinarySpellTargeting.ResolveCast` and `MagicCastQuery` owners. It does
not recheck blue range or spend MP a second time. Inactive anchors may omit formula
snapshots while retaining explicit position data; they can centre bystanders.
Missing/duplicate anchors, unknown position, unsupported selected targets and late
RNG exhaustion return the incoming checkpoint with no cast outcome. Six cumulative
managed cases pass current-versus-stored position, order,34/68 damage, exact RNG,
inactive/Jump eligibility, replay and atomic-failure vectors.

## ZOD-366

`SpellTargetChoiceQuery` derives an opaque choice from the same immutable spell
and caster plus existing blue panels. Named geometry flags `NoFollowTarget` and
`Auto` default false, preserving all old constructors. Auto/range0 returns
NoTargeting before blue filtering; otherwise empty panels mean None, then the
NoFollowTarget bit chooses TileOnly versus UnitOrTile. No MP is read or proposed.
This admits only the known ordinary nonweapon slice; Auto's NoTargeting is data,
not automatic command activation. Nine cumulative managed cases pass four admitted
source choice literals, early Auto priority and unknown/inactive context guards.

## ZOD-367

`SpellAimCapture` captures an admitted player confirmation into an opaque
`ConfirmedSpellAim` retaining the original definition and caster. It requires
unchanged blue selection and a caller-certified offered answer; non-tile answers
also require the source-preferred current cursor occupant or known empty. It
preserves exact5/6/7 and the previous ignored ID, including empty-unit fallback5.
Cursor selection excludes inactive/Crystal/Treasure/Jump but does not add combat's
mount exclusion. Runtime source/rider/list preference remains explicitly certified,
not guessed from combat source order. Auto NoTargeting capture remains outside
this manual-selection slice. Twelve cumulative managed cases pass six player
confirmation literals, bound definitions, context/range/occupancy guards and
cursor-versus-combat eligibility. No UI, command or live state is created.

## ZOD-368

`MagicChargePlanQuery` prepares opaque immutable `PendingMagicCast` data from the
confirmed definition/aim and certified stable roster identities. Preparation
requires an active actor and, for exact6, an active anchor; it does not substitute
combat eligibility or re-read aim geometry. Tile/YES7 preparation does not read
anchor facts. Current Short/Non-Charge supports feed the existing low-seven-bit
setup owner. Mime and Persevere remain outside this bounded plan.

One explicit blocked/allowed `Tick` delegates `MagicCharge.Tick`, returning a new
plan and readiness data without changing the old plan. Counter0 remains unready
while blocked. Preparation/countdown neither reads MP affordability nor spends MP,
starts statuses, emits events or advances a global clock. The plan retains no stale
MA/Faith/MP snapshot. Fifteen cumulative managed cases pass source charge vectors,
active-identity distinctions, guards, zero-MP planning and deterministic ticking.

## ZOD-369

`MagicReadyPlanQuery` admits only a counter0 plan with known unblocked current
turn status and stable current identities. It derives charge readiness and earlier
blue admission from the opaque plan; callers still certify preceding action-state
bookkeeping, reactions, current anchor position and no earlier target effects.
It reads the current caster snapshot, prepares MP once, then delegates stored-aim
resolution, green panels, source-order targets and the existing formula owner.

The result exposes either a resource block diagnostic or a complete candidate
cast. It exposes no separate partially payable MP proposal on a later geometry,
context or RNG failure. All failures retain the incoming RNG and no cast outcome.
Current MP, MA, Half MP and anchor positions are used at readiness, not cached at
charge start. Tile/YES7 remain at the captured tile; exact6 follows current position.
The query is replayable calculation data, not one-use command or publication authority.

[19 new phase descriptions](../fixtures/behavior/spell-aim-pipeline-contract-v1.json)
cover confirmation through ready resolution, with literal prior source cast/RNG
vectors and explicit managed admission guards. They include current-MA rounding
(68/137), inactive anchors, Short/Non-Charge, Preview, current Half MP, zero-MP
planning, failed budget, unknown identity/position, blocked readiness and late
rollback. The independent Python composition executes no C# or original code.
Twenty-four new managed cases pass, including these19 descriptions; the focused
magic suite passes 132 cases. Eight new independent Python cases pass.

Automatic NoTargeting capture, runtime cursor preference/slot lifecycle, complete
scheduling, performance repeats, no-target/fizzle publication, all live state and
RNG adoption, commands, grants/import activation, and magic AI remain separate.
[ZOD-370–375](living-magic-projections.md) now trace ordinary result application
and implement bounded private living HP/MP/RNG proposals. [ZOD-376–381](action-outcomes-and-rewards.md)
trace execution/reward phases and add reward calculations; [ZOD-382–384](growth-and-job-unlocks.md)
add raw growth/unlock calculations before derived refresh and live integration. No original quirks are
silently repaired.

Charge plans use actual execution setup. AiSimulation in the ready query evaluates
an already-ready actual plan; it does not replace the separate original AI
candidate-delay quirk in `MagicCharge.CalculateOrdinaryAiDelay`.

## Final validation

Locked restore and Release build pass with zero warnings/errors; all **4,694 .NET
cases** and **703 Python tests** pass. The focused magic suite passes 132 cases.
Fresh source/prepared-data, algorithm-guide, coverage, local-link, layout and
preservation audits pass. [Implementation receipt](../evidence/combat/spell-aim-pipeline-implementation-v1.json)
records milestone bases and ignored logs. All 26 new public types have matching
files; all pre-batch formula/geometry/resource bodies, managed tests, behavior
fixtures, owner drafts and JobStats are preserved. Only two optional named flags
extend the existing geometry definition. No project/dependency, Content, AI or
host changes; no original/C execution or UI review.
