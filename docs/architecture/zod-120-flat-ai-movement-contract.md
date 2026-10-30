# ZOD-120 — Flat ordinary AI movement and path composition

Date: 2026-10-26. Managed base: b2f488d (clean on entry). Read-only local
`C:/repositories/fft_decomp` was freshly checked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The [evidence](../evidence/ai/flat-movement-contract-v1.json)
pins 15 source/data-declaration files and 42 functions by normalized UTF-8/LF
hash, with line spans for navigation. Symbols and revision own these findings.

This source/design spike adds an offline description and regression checks.
It does not translate production movement, execute the C reference, or establish
observed original-game equivalence. The [48 vectors](../fixtures/behavior/flat-ai-movement-contract-v1.json)
are source-derived closed-domain expectations in five families. All 18 FMP
production implementation cases remain **unexecuted** in this historical artifact.

## Admitted domain and data

Exactly two distinct living ordinary unmounted units occupy an open rectangular
map, one lower layer, width/depth 1..16, flat height 10, no half-height, slope,
ceiling, water, blocked/cross-section/untargetable/cannot-stop flags, traps or
pickups. Both have body height 6, NORMAL movement, no movement modifiers or status
effects affecting traversal, and stable explicitly certified opposing teams.
No mount/partner, Jump action, crystal/treasure, Charm/Invite or team changes.
Every tile has **certified effective movement cost 1** for both units.

The existing sword profile certifies much of this geometry but does not certify
source cost-table values or constrain maps to 16. Its units may have empty
`TerrainCosts`; do not silently reinterpret those dictionaries. The proposed
movement query needs a separate explicit cost/profile certification boundary.
This is a compatibility profile limit, not a new global engine/map-size limit.

`spread_unit_to_scratchpad_store` resolves movement modes before selecting a
64-byte cost row. Source NORMAL is **7**, FLY is **0**; the zero test for standing
on an enemy is therefore not an ordinary-unit permission. Move and Jump are
bytes. Move is capped at `MOVE_PATHFIND_RANGE=124`; Jump is capped at 7, with
horizontal jump allowance `min(Jump,7)>>1`. No managed unbounded-int coercion.

Optional read-only prepared-file verification confirms two data records without
committing their raw bytes: the `g_move_spread_preset_table` pointer order in
BATTLE.BIN at 0x8018f500 is East/North/West/South, and default cost-table row 0,
Natural surface 0 at 0x8005ea50 is 1 in SCUS_942.21. Full file identities,
load addresses, slice bounds/hashes and decoded values are in the evidence.
Weather substitution only affects zero-cost entries, so this Natural cost is
stable here. These observations do not certify other surfaces/tables/weather.
Original data and generated inventories remain under ignored scratch/.

## Physical reach and occupancy

`move_range_build` seeds the source panel with remaining range `cappedMove+1`
and max height delta 0. It scans layer/y/x, spreads with the E/N/W/S presets,
and accepts positive remaining budgets. Physical spreading uses the live
frontier; newly queued later cells may spread in the same pass. On this domain,
each cardinal step subtracts 1 and larger remaining budgets replace smaller ones.
Equal-budget height preference is immaterial because every delta is zero.

The living opposing unit clears ENTERABLE and MOUNTABLE for its ground tile.
NORMAL clears STAND_ON_UNIT. The horizontal jump body check also rejects crossing
this enemy: doubled ground/standing height is 20, mover size 6, and the enemy
standing midpoint 23 lies strictly between 20 and 26. Raising Jump does not
permit jumping across that unit at equal height. Empty flat jumps add no reach
beyond the adjacent-step unit-cost graph because they charge the same span.

Consequently the admitted physical set is exactly the cardinal graph-distance
ball of radius capped Move with the enemy tile removed, excluding the origin.
`range_tile_flags_set` deliberately omits the actor's tile as a Move destination.
The corresponding residual is `cappedMove+1-shortestDistance`; unvisited cells
have no positive range. A one-panel-wide corridor can be disconnected by the
enemy, irrespective of Move or Jump.

Example: actor (0,1), enemy (1,1), destination (2,1) on a 3x3 rectangle. The
legal distance is **4**, not 2. Move 2 rejects the endpoint; Move 4 permits the
detour (0,1) → (0,2) → (1,2) → (2,2) → (2,1).

Source enemy classification compares the mover's initial team flags against
the other unit's current flags; some AI target classification reverses that
initial/current pairing. Stable opposing certification makes them agree here.
Do not generalize this proof to allies, multiple bodies, Float/Fly, dead units,
uneven terrain, changing teams, or mounts.

## AI origins, reverse distances and ordering

`reachable_tiles_build` copies physical destination marks, excludes other present
units, and **adds the actor origin**. When movement is already taken, only the
origin remains. Acting alone does not prevent movement. This query is separate
from command authorization: physical geometric ranges/paths can still be
described after a Move; they cannot authorize a second Move.

`ai_move_propagate_from_target` is different from physical reach. It sets Move
124, clears **all unit records**, removes the actor seed, and seeds the target
with remaining 125. Its mode-1 propagation subtracts the departed tile's cost,
which reverses the forward entering-cost rule. It snapshots each frontier pass,
and can suspend only between completed passes on its search budget. These
scratchpad/resumption and frame-budget details need not become managed query
state or wall-clock dependencies in this small synchronous domain.

With no occupied cells in that pass, reverse distance is Manhattan throughout
the open cost-1 rectangle, including through the enemy. The maximum distance is
30, below its 124 limit. This reduction is not a general replacement for source
pathfinding. In the detour example the actor's distance **to the enemy is 1**
even though walking beyond the enemy requires four steps.

`target_distance_grid_fill` updates every x in each scenario-0 reachable row,
including noncandidate and occupied cells. Rows with no candidate are untouched;
the fixture omits those rows rather than inventing their previous contents or
zeroes. A moved actor at (2,2) therefore yields only row y=2, for every x.

Ordinary short evadeable sword origins in `origin_reachable_evaluate` scan
layer/y/x. The ranked alternative `origin_next_take` uses signed tile priority
then proximity, with later cells winning equal keys. `destination_select` also
scans layer/y/x and replaces on equal key/distance, selecting the last tie.
Priority-first and distance-first are distinct modes; the fixture reuses the
ZOD-116 isolated arithmetic description, sorting supplied cells first. This
does not execute search ranking, target selection, or a complete original AI.

`origin_destination_finalize` chooses Act-before-Move when the cast origin equals
the actor's original tile and substitutes the selected scenario destination;
a different cast origin means Move-before-Act. `ai_move_origins_evaluate` evaluates
Act-before-Move first when allowed, then distinct movement origins with resumable
stages. `ai_command_build` skips Move for identical coordinates, respects consumed
choices, and redecides after Act. The six dispatch vectors exercise these isolated
flags; they do not claim to reproduce the candidate simulation or its clock.

## Selected paths and facing

`move_path_build_to_tile` requires a marked destination, valid range cache for
the same present unit and a permitted endpoint; the origin/occupied enemy fail.
It backtraces range predecessors, starting with preferred predecessor direction
East. `path_selected_candidate_save` minimizes extra span, then maximum height
delta. On equal zero span/delta it prefers the **previous backtrace direction**
if available, otherwise the first eligible direction E/N/W/S. Carry that chosen
direction through each backward step. Choosing a forward BFS predecessor or
inferring direction from endpoint displacement does not implement these ties.

Every reached cell in this closed graph has an adjacent shortest predecessor.
`path_neighbors_spread` therefore succeeds before horizontal-jump backtrace
fallback. The resulting path consists of adjacent steps even with high Jump.
`path_steps_encode` emits one byte per step, without straight-run compression.
Packed direction ordinals are East=0, West=1, South=2, North=3 (bits 6..7).
Length-minus-one, slow, climb, mount and higher-layer bits are all zero here:
East 0, West 64, South 128, North 192. +y is North, -y is South. Map names
explicitly; these ordinals differ from spread order and managed BattleFacing.

Literal examples: (0,0) → (1,1) selects North then East, bytes [192,0], final
heading East. (2,2) → (1,1) selects South then West, bytes [128,64], heading West.
The detour above selects [192,0,0,128], heading South. A three-panel eastward
walk is [0,0,0], not one encoded long jump.

Controller `battle_state_select_move_tile_update` builds the real selected path
for an AI Move after its brief presentation wait; it does not teleport directly
to the requested coordinates. `move_step_start_walk` publishes each step's facing
except ACTION_EXECUTE knockback. `move_settle_on_tile` and `move_confirm` publish
the settled tile/facing and then movement/turn completion and subsequent events.
Final voluntary walking facing is the final step heading.

In contrast, AI `movement_pickup_simulate` invokes `ai_acting_unit_place`, which
changes x/y/layer **without rotating facing or constructing an execution path**.
It sets movement_taken only when coordinates differ. Do not put real path heading
into candidate preview placement. Neither query should overwrite combat stats,
consume a choice, or synthesize turn-end upkeep. The future execution integration
must update facing before issuing the ZOD-119 completion receipt; current
FacingTurnBattle.Move deliberately preserves supplied facing and is unchanged.

After a real push, rebuild using the settled actor/enemy coordinates. The enemy's
old tile becomes free and its new tile blocks traversal. A killed enemy leaves
this living-opponent profile; do not treat it as a living blocker or invent the
general corpse rule. Both actors use the same geometric contract.

No gameplay RNG is used by the admitted range, selected path or reverse-distance
helpers. Teleport's random roll is excluded. Surrounding reach randomization,
rank lottery, sword projection draws and candidate turn-end upkeep still belong
to the future decision runner; this contract does not claim zero draws for them.

## Managed comparison and next implementation

These are static source comparisons, not newly executed C# parity tests:

- `Tessera.Engine.FlatGridMovementRange` and the Zodiac default compute an
  unobstructed Manhattan set. Rejecting only an occupied endpoint in
  BattleSimulator permits the behind-enemy endpoint at Move 2 in the example.
- `TerrainAwareMovementRange` with explicit effective cost 1 and the enemy in
  traversalBlocked computes the correct admitted set. Its priority queue and
  W/E/S/N iteration do not prove the original selected path or tie behavior.
- `OrdinaryAdjacentStepRange` plus that blocked set also has the correct admitted
  membership. Neither API retains ordered original paths or execution heading.
- Frozen-set iteration is not an original AI scan contract. No existing Move,
  AttackBattleSimulator, FacingTurnBattle, content/scenario/save/native path is
  switched by this spike. Cost-map admission and source byte caps differ too.

**Recommend ZOD-121:** add one pure, opt-in closed flat movement query. Require
explicit certified cost-1 geometry and stable opposing relation, reject unsupported
source-width inputs/traits, and return deterministic y/x physical destinations,
residuals, AI origins, whole selected reverse-distance rows, and optional exact
physical path/packed steps/final execution heading. Query either living actor
from current settled coordinates; keep active-turn gating elsewhere. A moved flag
filters AI origins only. Return typed unsupported/invalid-destination outcomes;
do not expose partial results on failure or mutate state/RNG.

Execute FMP-001..018 with literal source vectors, independent shortest-distance
checks, both actors/post-push and actual managed prototype comparisons. Path
heading is returned as data only. Any later command adapter must separately
preserve real execution rotation versus AI preview placement, correct turn/receipt
ordering, RNG and replay semantics. Candidate clock, full selector/search/roster,
IBattleDecisionPolicy implementation, general terrain and runtime activation are
separate milestones. Editor redesign and ZOD-106 remain deferred.

## Validation and reproduction

Nineteen new Python tests exercise literal paths/headings/packed bytes, all
predecessor subsets/permutations, an independent Floyd-Warshall oracle on small
rectangles, flags/rows/dispatch, caps, post-push, immutable descriptions, inventory
and synthetic metadata corruption. No compiled C or retail execution is involved.
Set TEMP/TMP to writable ignored scratch/ when running in the Windows sandbox:

```powershell
$env:TEMP = Join-Path (Get-Location) 'scratch'
$env:TMP = $env:TEMP
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_flat_movement_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --overlay scratch/all-data-extraction/BATTLE.BIN --executable scratch/all-data-extraction/SCUS_942.21 --output scratch/flat-movement-120-source-final-a.json
```

Repeat the audit with a new output filename; its exclusive-create output avoids
overwriting reports. The prepared inputs/reference are optional: the Python suite
validates fixtures, 68 historical files and 21 managed baseline files without
them. The full pinned audit additionally checks 15 files / 42 functions and both
prepared metadata records. Final counts/report fingerprints are in the evidence.
Last implementation validation remains ZOD-119's 3,415 .NET / 359 Python tests;
no .NET rerun is required for this source-only spike. No new headless/native,
original/emulator, editor or battle-save execution.
