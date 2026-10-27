# ZOD-137 — Movement scoring, Attack scan and ranked admission

Source/design checkpoint, 2026-10-27. Read-only reference HEAD:
**11464943bbdbd2a0af022d90cb7f10455cf911df**, clean at inspection.
[Evidence](../evidence/ai/composition-contract-v1.json) reuses ZOD-135's source
receipt and pins six additional inspected functions. This adds no production
composition or original/C execution. ZOD-138 is the bounded C# follow-up.

## Boundary and source ordering

The next query begins immediately before a fresh `turn.c::reachable_tiles_score`
in the general-search branch and stops after the ordinary Attack pass's
`ai_best_rank_insert`. Inputs are the original two-opponent snapshot, explicit
decision RNG at that entry, certified movement scoring mask and already-known
source destinations. It does not generate
the scenario or claim to decide a whole turn. Work before this entry, later
ability entries, lottery, fallback, facing and commands remain outside the query.

`action_select` resets ranked entries/best on fresh entry, not on resume.
`general_select` calls `scenarios_build`, seeds the baseline for scenario 0,
then evaluates scenario 1. `scenarios_build` establishes this important sequence:

1. Filter scenario 1, including the stay-near special case or all-reachable
   fallback when the distance filter finds nothing.
2. Score that **pre-insertion mask S**, once per visited tile, in layer/y/x order.
3. Select scenario-1 destination D1 (or retain its fixed destination). When target
   is out of reach and destination is not fixed, a non-player team draws once for
   `rand()%7` before nearest/fallback selection. Other admitted branches draw none.
4. Prepare scenario 2 and select/copy scenario-0 destination D0.
5. Add the actor's current tile to scenario 1: **Attack mask A = S union {actor}**.
6. `general_select` seeds D0's baseline from the grid, checks charging, selects
   active targets, loads entries and runs the ordinary Attack scan/insertion.

The added actor tile may never have been scored. Therefore A cannot be used as
the movement scoring mask, and membership in A cannot prove D0 has a fresh cell.
D1 is chosen before actor insertion. The narrow follow-up requires **D0 and D1
both in nonempty S**, with every S cell in the original movement `AiOrigins`.
It rejects unvisited/stale-cell baselines; it does not assume a zero-filled grid
or repair source behavior. D0 and D1 may differ. Mask/source-destination selection
remains caller-certified rather than inferred from reachability or asset IDs.

## State, baseline and RNG ownership

`reachable_tiles_score` prepares MoveThenAct/skillset=0 for each S tile, updates
pickup metadata, invokes `ai_action_simulate_and_score` directly, and stores the
negated s16 priority as a u16 pattern. It restores actor placement after the loop.
`simulation_unit_state_save/restore` cover selected unit fields/actions and Golem;
they do not save or restore the random generator. Every tile starts from the same
original unit state, while every completed evaluation retains its RNG progress.

The existing closed profile excludes charges, pickups and upkeep/terrain effects.
Thus each movement tile consumes exactly one upkeep draw, without formula draws.
Its private clock starts at the original tick count, not the previous tile's
predicted tick. CT refunds can still change readiness or overflow admission.
Even though all these movement scores are equal in this effect-free subset,
do not replace the per-tile simulations with one score plus bulk RNG advancement.

Keep each fresh tile evaluation privately. Select D0's already-computed score
from that collection; `rank_baseline_seed` adds **zero** draws. Double-negation
preserves its -160..160 value and sets hit=100/decided=0. Calling the standalone
ZOD-136 public query again for D0 would add an incorrect extra draw.

Admit only a context with **no intervening random work** between this movement
prefix and the supplied Attack entry. This explicit certificate excludes the
non-player out-of-reach reach-halving roll and earlier random-use/other evaluations.
No charge is present; `keep_charging_select` immediately returns. Ordinary
`ai_ability_entry_load` and `target_active_units_select` do not draw. ZOD-130's
ordinary Attack policy excludes random-use gating. Other source branches need a
later implementation; supplying only their final destinations cannot recover RNG.

Run one existing scan on original state, using A and D1, with the stream **after
all |S| movement draws**. The scan builds its original-state cache privately,
evaluates each emitted candidate once, retains losing/NotValuable draws and uses
the same initial state for every candidate. Proximity after a projected KO must
not be recomputed from that projected state. Ordering, masks, comparison and
insertion add no draws. Total supported cost is `|S| + sum(Attack evaluation costs)`.
An inactive target or no Attack origins still retains all movement draws.

This is a source-ordered slice under its certificates, not the entire AI command's
RNG schedule. It must not advance or commit live RNG. On any unsupported input or
exhausted tile/candidate, return the query's original RNG and no partial outcomes,
cache, best or list. Preserve a typed failed stage/index and underlying reason.
This managed transaction is not original C/VSync rewind; no resume API is proposed.

## Minimal C# follow-up: ZOD-138

Use one immutable scenario description containing S, D0 and D1. Copy the supplied
origins, validate them uniquely against unchanged original `AiOrigins`, sort by
layer/y/x, and derive A internally. Do not accept external scores, cached results,
intermediate RNG checkpoints or a pre-populated ranked list as trusted inputs.

Reuse candidate, score, comparison, baseline and ordinary scan profiles. Existing
baseline facts still require explicit certification; fresh-cell provenance is
also enforced structurally by D0 membership and actual private evaluations.
Add named booleans (default false) for original **pre-scoring movement mask**,
**no intervening random work**, and **fresh empty ranked list**. Existing scan
certificates must describe the resulting A/D1 and a freshly reset best. TargetActive
remains a required known bool. Validate original state/action, all profiles,
destinations and weapon/envelope even when no Attack evaluation would be emitted.

Extract a small internal fresh movement-tile evaluator from ZOD-136 if needed.
The standalone baseline wrapper keeps its public checks/behavior. Do not call
that wrapper for non-D0 cells while asserting they are all scenario-0 destinations.
Share private admission/clock/scoring code where useful, without exposing a public
cached-score entry or introducing a general simulation framework.

After the scan, keep explicit absence distinct from a real score of zero or a
negative score. Apply the [ZOD-135 insertion rules](zod-135-ai-rank-admission.md#strict-admission-and-list-insertion):
strict rank-only baseline gate, then strict tail/first-outranked comparison,
stable equal entries, full-payload shifts with no deduplication, unconditional
best reset. Candidate marker=0 and hit<=100 mean baseline priority must be beaten;
proximity cannot rescue an equal baseline score.

The original list capacity is eight. In this fresh-empty-list/one-pass subset,
**only zero or one entry can be admitted, at slot 0**. Every admitted score beats
the reset sentinel in the closed score range. Keep private slots/explicit empties,
but do not invent extra passes or caller-populated outcomes to exercise capacity.
Existing Python rank kernels cover general tail shifts/stable duplicates; this
does not establish C# coverage of multiple real insertions. Duplicate destinations
within the scan still require separate evaluations in source order.

Return immutable movement diagnostics, the D0 baseline reference/index, fresh scan
diagnostics, admission status and private-list result with the final hypothetical
RNG. An empty list means **no admitted Attack**, not a chosen movement command.
Even a single admitted entry still needs the later source lottery's draw schedule;
do not silently choose it or invoke fallback. Ability/roster expansion, replaceable
decision interface, authoritative commands and editor/native activation stay deferred.

## Focused acceptance cases for ZOD-138

These are **unexecuted managed proposals**, not original observations:

- **CMP-001:** Three scored cells excluding actor, four resulting Attack origins;
  all movement cells run before Attack, and D0 is never reevaluated. With seed=1,
  count=7 and four supplied five-draw Attack costs, scan entry is word=662824084 /
  count=10; final word=1343201072 / count=30. This cost example is a stage kernel,
  not proof of those Attack costs for arbitrary geometry.
- **CMP-002:** Actor already in S/stay/already-moved produces no duplicated cell;
  a new destination after Move rejects. D0/D1 can differ within S.
- **CMP-003:** Non-attackable movement cells still run. Inactive/no-candidate scans
  retain the entire prefix; NotValuable candidates retain four draws each.
- **CMP-004:** Real equal-baseline score fails regardless of better proximity.
  No scored best, real zero and negative best remain distinct. A Critical 10/100
  actor versus healthy 100/100 scores -148; damaging the target to 75 scores -116
  and passes the gate. Only zero/one list occupancy is claimed.
- **CMP-005:** Empty/duplicate S, D0 outside S (including a newly added actor bit),
  D1 outside S, unsupported geometry/weapon/context and each false certificate
  reject before evaluation, including filtered-out Attack queries.
- **CMP-006:** Exhaustion inside movement or a later Attack returns original query
  RNG/no partial list; exact-MAX completion succeeds. Each tile starts at original
  ticks, including a late moved-tile overflow after a successful stay evaluation.
- **CMP-007:** Existing scan strict ties, source-order stay-once, separate duplicate
  destinations and pre-KO proximity survive composition without rescoring.
- **CMP-008:** Both actor identities, reversed input order, replay, explicit RNG
  carry and independent branches preserve original snapshots/profiles/live RNG.

## Reproduction

Six focused [offline tests](../../tools/verification/test_ai_composition_contract.py)
check the mask handoff and RNG-stage literals, including supplied Attack costs
and managed rollback descriptions. They execute no production composition. Reuse
the existing rank/scan/clock tests rather than copying their inventories. Run:

```powershell
python -m unittest discover -s tools/verification -p 'test_ai_composition_contract.py'
python tools/verification/verify_ai_composition_contract.py --reference C:/repositories/fft_decomp --output <new scratch report>
```
