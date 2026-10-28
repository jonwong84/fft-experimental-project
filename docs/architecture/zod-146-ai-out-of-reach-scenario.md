# ZOD-146 — Out-of-reach focused sword movement

Source/design checkpoint, 2026-10-28. Read-only clean reference:
**11464943bbdbd2a0af022d90cb7f10455cf911df**. The
[receipt](../evidence/ai/out-of-reach-scenario-contract-v1.json) extends the
[in-reach source study](zod-141-ai-movement-scenario.md). No production change,
C/original execution or managed out-of-reach query is claimed here. ZOD-147 is
the bounded standalone movement implementation follow-up.

## Admission and source team meaning

Keep ZOD-143's two living ordinary flat opponents, original snapshot/ticks,
explicit RNG, fresh comparison and focused-opponent/ordinary-reach/policy facts.
Raw Move remains 0..124, reach=Move+1. Select **d > reach** and exclude reflected
charging, stay-near, ranged/crystal overrides and special policies. In-reach
inputs belong to the existing API; do not silently switch it to another branch.

The snapshot lacks original team bits. Require an explicit semantic team class,
with Unknown rejected and Blue/NonBlue distinguished. `acting_unit_init` stores
`unit->team_flags & UNIT_TEAM_MASK`, where the mask is **0x30**: blue=0,
red=0x10, green=0x20, light blue=0x30. `scenarios_build` tests that masked value
for nonzero. The **0x08 player-controlled flag is irrelevant** to this branch.
Blue autobattle therefore skips the reach draw; a non-blue player-controlled
unit still draws if it enters this original search. Opponent relation, actor
identity, AI control and unit IDs cannot establish this team fact. Earlier notes'
"non-player team" wording is shorthand, not a test of that control flag.

## Filtering, score-cell provenance and random order

1. Derive R and target-distance rows from unchanged original flat movement.
   Ordinary reverse propagation uses range 124 and clears unit records, so the
   previously established Manhattan reduction remains valid on this open map.
2. Filter R at **distance <= d-1** into S. If no tile qualifies, source copies
   all R. S is nonempty. For movable actors it can exclude the actor; for Move=0
   or an already-moved singleton it falls back to the actor tile.
3. Score **only S**, layer/y/x. Every evaluation starts at original ticks/state
   and contributes its actual upkeep draw to the carried private RNG.
4. Blue adds zero draws. NonBlue calls the original generator **once after all
   scoring**, even if the adjustment will not change the destination. Test
   `raw % 7 == 0`, and if true set the positive s32 reach to `reach / 2` with
   integer truncation. Move=0 can therefore reduce reach1 to zero. Do not roll
   before scoring, use a combat probability roll, or skip a non-halving draw.
5. Choose D1 priority-first among S at distance <= effective reach. If no cell
   qualifies, use unrestricted distance-first over S. Preserve signed packed
   priority/proximity, lower distance and **last complete tie** semantics from
   ZOD-141; neither pass requires positive priority.
6. Scenario 2 is R XOR S and copies D1. Since target is out of reach, source
   **copies D1 to D0**; it does not perform the in-reach d-1 D0 search.
7. Add actor to A=S union actor only after scoring/destination selection. Never
   pretend the added actor has a fresh movement priority when actor was not in S.

D0=D1 is always in S, so this selected branch has a fresh baseline cell even
when actor is unscored. Source proximity was prepared for **R before filtering**;
the added actor is in R and has fresh proximity. A later Attack scan may derive
its original-state proximity, but must not read an invented actor movement score
or simulate the added actor merely to fill that cell. Never reuse an old grid's
priority for an unvisited tile. Keep scoring mask S separate from A.

## Source-derived acceptance vectors

These are **unexecuted managed proposals** with authored synthetic state, not
original-game observations. RNG literals use the existing independent BIOS
calculator; movement priorities below are actual closed-profile zero expectations.

- **OOR-001:** 7x1 flat map, actor(6,0), target(1,0), Move3, CT15/95,
  Speed5, HP50/50, ordinary no-effects score context. d5>reach4. R=x3..6;
  S=x3,x4,x5; A=x3..6. Distances in S are 2,3,4; imminent proximity is 8,7,6.
  From word1/count7, three movement draws end word662824084/count10. Blue skips
  adjustment, selects D0=D1=x5, and leaves that RNG unchanged.
- **OOR-002:** Same, NonBlue, seed1/count7. Post-score raw17515 has remainder1,
  no halving, but advances to word3295386429/count11. D0=D1 remains x5.
- **OOR-003:** Same, NonBlue, seed19/count7. Three movement draws end
  word811149646/count10; raw224 has remainder0, reach4 becomes2; end
  word2162199151/count11. D0=D1=x3. Blue with the same seed instead stays at
  word811149646/count10 and chooses x5.
- **OOR-004:** Same geometry, target CT0 makes proximity zero. Priority-first
  then distance chooses x3 before or after halving, but NonBlue still draws.
  This proves that an unchanged destination is not permission to skip RNG.
- **OOR-005:** Move0, actor(3,0), target(1,0), d2>reach1. Filter matches nothing,
  S=R={actor}, D0=D1=actor. Seed1/count7: movement raw16838, then NonBlue
  adjustment raw5758/remainder4, end word2524885223/count9. Blue ends
  word1103527590/count8. An already-moved actor similarly retains raw Move for
  reach while R/S are a singleton; no movement is invented.
- **OOR-006:** Reject Unknown team, in-reach input and unsupported source facts
  before evaluation. A Blue completion may end exactly at MAX with |S| draws;
  NonBlue needs one additional actual draw. Failure at that post-score roll
  restores original query RNG and publishes no S/scores/partial adjustment.
  Existing per-tile tick rollback and original-state replay guarantees remain.

## Bounded C# continuation

Add an explicit out-of-reach entry point to the movement-scenario facility,
sharing pure admission, fresh tile evaluation and destination comparison with
the in-reach implementation. Preserve existing API behavior and immutable result
contracts; use separate out-of-reach diagnostics/wrapper as needed to report
team, effective reach, optional raw adjustment and D1 distance fallback.
The D0 in-reach-fallback flag must not be repurposed: out-of-reach D0 was copied.

Use a typed stage for post-score adjustment exhaustion and retain original
whole-query RNG/no partial result. Neither destination selection nor D0 copy
adds draws/ticks. No public cached score input is introduced. Do not bulk-advance
the movement stream or evaluate extra actor cells. Recheck source before coding.

Integration with the derived selector is a later bounded child. Its current
in-reach entry must keep rejecting this branch until it explicitly owns the
additional team fact, post-score RNG and whole-query failure propagation. Existing
supplied-scenario APIs' NoInterveningRandomWork assertion cannot certify a
NonBlue out-of-reach adjustment. Full AI/abilities, Defend, commands/facing,
live RNG commitment and native/editor activation remain outside this milestone.

## Validation and reproduction

The extended read-only audit verifies the inherited selection/scenario receipts,
plus `move_range.c`, `move.h` and `ai_move_propagate_from_target`. Independent
BIOS calculations checked the listed RNG words/raw values and the distinction
between team and player-control masks. These checks do not execute a fresh
managed out-of-reach scenario. Final source report:
`scratch/ai-out-of-reach-146/final-source.json`.

```powershell
python tools/verification/verify_ai_movement_scenario_contract.py --reference C:/repositories/fft_decomp --out-of-reach --output <new scratch report>
python tools/verification/verify_baseline_coverage.py --reference-root C:/repositories/fft_decomp
```

No production C# changed; the last full .NET validation remains ZOD-145's 3,744
passing cases. All **496 Python tests** passed; log: `scratch/ai-out-of-reach-146/python.txt`.
