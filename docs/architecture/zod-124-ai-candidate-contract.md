# ZOD-124 — One supplied sword AI candidate: placement and clock

Completed source/design spike, 2026-10-27. Reference checkout was freshly checked
clean and read only at **11464943bbdbd2a0af022d90cb7f10455cf911df**. Symbols and
whole-file/function identities are pinned in [evidence](../evidence/ai/candidate-contract-v1.json).
The [44 vectors](../fixtures/behavior/ai-candidate-contract-v1.json) are offline
source descriptions or explicitly classified managed design proposals. They do
not execute C, the original game, or a production candidate evaluator. The 16 ACP
implementation cases remain unexecuted. Existing managed APIs remain unchanged.

## Source path and order

`ai_command_build` enters `ai_simulation_begin`, which sets AI simulation mode,
clears Hamedo state and backs up the mimic attacker. `ai_move_origins_evaluate`
and `origin_aim_tiles_evaluate` feed `ai_action_evaluate`, which calls
`ai_action_simulate_and_score`. Ranking after that call is outside this contract.
The original branches can place the actor before the baseline snapshot; callers
such as `origin_reachable_evaluate` restore `acting_unit_coords` afterward. A
candidate baseline is therefore not necessarily the owner's original actor tile.
The proposed managed query must preserve its complete input independently of
those source workspace conventions.

The scorer saves selected unit state, clears move-opportunity scratch and resets
priority/base-hit. For the admitted immediate ordinary Attack:

1. Move-before-Act invokes `movement_pickup_simulate`, prepares/resolves the
   action, then marks Act. Act-before-Move resolves/marks Act before that movement
   helper. Formula range/evade use the origin appropriate to the order.
2. Different packed coordinates invoke `ai_acting_unit_place` and mark Move.
   Same coordinates leave the incoming Move flag alone. That helper writes x,
   y and terrain layer only; incoming facing remains unchanged. No path heading,
   real Move command, movement receipt or facing completion belongs here.
3. `action_hits_simulate` counts target/strike pairs with positive accuracy and
   requires a valuable target. In this one-enemy slice zero accuracy returns 0,
   restores baseline immediately, and skips Move after an Act-first failure,
   turn-end, clock and scoring. Positive accuracy applies the full projected
   normal damage even at 50%; it is not probability-weighted HP damage.
4. A valuable action sets Act, calls `turn_end` explicitly, saves the three clock
   globals, and calls the clock at least once before testing whether anyone is
   Charging/Performing. No such statuses exist in this slice, so the first clock
   event ends the simulation. It restores clock globals, scores, then restores
   selected baseline fields. This spike specifies the input to scoring, not a new
   score/rank implementation.

Do not generalize this sequence to move-only/Defend: the source's Act-first
movement helper is inside its nonzero/non-Defend skillset branch. No normalization
of that branch, delayed abilities or reaction dispatch is authorized here.

## HP and status boundary

ZOD-117 supplies hit percent, capped normal damage, projected HP and four real
evade draws. Positive accuracy invokes `action_apply_target_result`. Its signed
16-bit damage is subtracted in signed 32-bit arithmetic, clamped to 0..max HP,
then stored as unsigned 16-bit HP. With ordinary unblocked status records, HP0
inflicts Dead; otherwise HP <= floor(maxHP/5) inflicts Critical and larger HP
removes Critical. A zero-accuracy pair is skipped before applying this result.
`status_resolve_unit_changes` / `action_inflictions_apply` apply cancellation data
and `status_ct_set`: newly Dead replaces Critical and sets death counter 3 for
the admitted ordinary unit. Formation-save/load or immortal units would use 255
and are excluded. No KO count increment occurs outside execute mode.

Fresh metadata-only reads of the hash-verified prepared SCUS executable establish
that Dead and Critical do not freeze CT and that Dead cancels Critical. The table
address, source layout, EXE/table hashes and decoded booleans are in evidence; raw
records remain ignored. This is source/data evidence, not an original execution.
Innate/immunity/blocking statuses, statuses beyond None/Critical/Dead, Reraise,
Undead and other status interactions are outside the closed profile.

## First event and CT ownership

Entry certificate: original `UNIT_TURN`, resume state `PICK_UNIT`, current clock
unit equals the actor; exactly that actor has a turn. No pending charged actions,
mimes, timed statuses, poison/regen, terrain poison, Transparent, Defending,
Chicken, Brave<10, mounted units, scripted effects or other upkeep are present.
The original slot order maps explicitly to stable tie keys; collection order
must not silently become the tie breaker. Both units are initially living.

`turn_end` is a no-op for absent/not-active units. Otherwise its unsigned 16-bit CT
temporary adds 20 per unused unrestricted choice, caps at 60, stores byte CT and
clears has_turn. The supplied sword action always consumes Act. A new move or
an incoming Move flag suppresses the Move refund; staying with unused Move adds 20.
Don't Move, Don't Act and Performing suppress corresponding refunds in source;
they are rejected by this proposal rather than silently treated as ordinary.

The clock traverses the effect-free end-turn phases and resumes **PickUnit before
AddCt**. Highest CT >=100 wins; equal CT selects the lower original slot. A pick
stores CT%100 (CT255/Quick has a separate reset and is excluded), then consumes one
real `rand()` in `upkeep_action_build`, even if nothing visibly changes. A living
ordinary unit resets Move/Act, receives a ready event, and the AI marks its move
opportunity and clears has_turn. It does not execute that unit's turn. No active
unit is exposed in the projected result.

If nobody is ready, the intervening empty charge/status phases lead to AddCt.
Byte CT + byte Speed uses unsigned 16-bit temporaries and caps at 254. Positive byte Speed
1..255 guarantees a living survivor is picked within 100 CT additions. A projected
Dead target still gains CT and can be selected: modulo and an upkeep draw occur,
but it receives no turn and the same clock call continues. Death-counter decay /
crystal/treasure are guarded by execute mode, so counter 3 stays 3 here. With two
units there is at most one dead pick per addition plus an initial pick: at most
102 upkeep draws, plus 4 formula draws. This bound is conservative. General source
clock safety limits are 1201 counted phase steps per call and a candidate counter
that breaks at 256 **before** calling the clock (at most 255 calls); neither is a
new generic managed scheduler. The admitted path reaches its first ready event
well inside those phase limits.

Examples: a target already at 100 CT can become ready without adding a tick.
Equal 95 CT / Speed 5 selects original slot 0. A Dead target at 100 can consume an
upkeep draw before the living actor reaches 100. Speed 1 actor with CT0 versus
Speed 255 Dead target with CT0 takes 100 additions and 101 upkeep draws. Skipping
that corpse would preserve HP scores but lose decision RNG and projected CT.

## Copy, restore and scratch map

`simulation_unit_state_save/restore(BASELINE)` visit compact-indexed present slots
only. They preserve selected state, not a complete memory snapshot:

- Active battle ID, death counter and five current-status bytes; all 16 timed CT
  bytes; team flags; seven equipment bytes; Brave.
- The four bytes starting at Faith: Faith, transparent-removal flag and HP16.
  MP16 is separate. Max HP/MP are not saved.
- Three base attribute bytes; **four** bytes starting at effective attributes:
  Speed/PA/MA **and CT**. Three bytes starting at x include both packed position
  bytes, including facing/layer/y; coordinate-only placement still preserves face.
- Charged ability CT, has_turn/Move/Act (three bytes), mount info; eight bytes from
  auto-battle setting include target, initial team flags and five inflicted-status
  bytes. The separate action-command copy is 20 **bytes**, using halfword copies;
  team Golem values are separately backed up.

`SIMULATED_STATUS` saves only compact identity/death/current statuses and Death
Sentence CT, temporarily restores their baseline values, and resumes those
simulated statuses later. Other simulated fields remain in place during that
source yield. Wall-clock scanline suspension is source scheduling machinery;
the bounded managed proposal runs deterministically without it. No save/resume
format or generic serializer is introduced.

The scorer separately restores `g_turn_clock_state`, resume state and turn-unit
ID after clock work. It retains rank, hit accounting and move-opportunity results
as decision scratch. Action records/work pointers, pending phase flags, rewards,
Hamedo and other globals are not all covered by that baseline. Outer simulation
end restores the mimic attacker and execute mode, **not RNG**. The managed query
must use private state and publish no authoritative effects, rather than copying
the source's partial global snapshot as if it were a complete transaction.

## Proposed additive C# boundary (ZOD-125)

Add an opt-in `SwordAiCandidateProjection.Evaluate` companion, with immutable
candidate/profile/result types as needed. Inputs: original AttackBattleSnapshot,
equipped bounded sword, existing flat sword/movement certifications, one supplied
actor/target/destination/order, and explicit decision RNG. Carry original slot/tie
order and the above effect-free clock certificate. It enumerates no candidates.

Validate the original living two-unit state, current unused action, supported
statuses/stats/weapon/profile, original slot/clock mapping and destination first.
ZOD-121 may certify allowed origins on this original state. Prior Move permits
stay only. Action origin is destination for Move-first, original tile for
Act-first. ZOD-117 may run on a privately staged active snapshot with that origin
and supplied RNG; all existing authority/range checks remain effective. Do not
call real MoveAlongPath, Attack, FinishFacing or scenario commands. Never invoke
the living-opponent movement validator on the **post-projection** KO state.

Return three distinct dispositions:

- `Rejected`: unsupported, invalid or terminal input, unavailable action,
  counter exhaustion or failed private staging. No outcome, no events/receipts,
  unchanged input and exact incoming decision RNG. Validate all admission checks
  before draws. On late RNG/tick overflow discard all staged progress atomically.
- `NotValuable`: supported formula with zero accuracy. No scored outcome or clock;
  return the four advanced formula draws. This is an evaluated candidate, not an
  unsupported-profile rejection.
- `Projected`: immutable scalar/unit projection carrying position and original
  facings, target HP/status/death counter, turn-end CT, first-event CT/choices,
  move-opportunity identity, elapsed hypothetical ticks and advanced decision RNG.
  Keep it separate from AttackBattleSnapshot/TurnCycleState: source restores clock
  globals separately and clears ready has_turn, so it is not an executable battle
  snapshot or next command authorization. No rank score in this follow-up.

Initial terminal/dead states are rejected explicitly. A newly lethal projection
is supported, retains the ordinary survivor/corpse clock behavior above, and
reports `TargetKnockedOut` without invoking an authored first-KO host terminal
policy or claiming universal victory.

The offline proposed vectors use supplied normalized formula values and placement
certificates. They do not replace production profile/geometry validation. Source
order/status/clock vectors exercise isolated admitted kernels; only the production
follow-up will execute the composite API and ACP-001..016. Identity for managed
revision/lineage counters stays untouched: this query issues no command receipts.
Hypothetical tick counts and explicit RNG draw counts reject overflow rather than
wrap. Source unsigned 32-bit BIOS word wrap remains intentional.

RNG ownership is explicit: each accepted evaluation starts with the supplied
decision RNG; the owner passes its returned RNG to the next candidate even when
discarding spatial/HP/CT output. Replay with identical input/RNG must match;
branches share no hidden mutable generator. No authoritative decision runner or
RNG commit protocol is activated here. Live state including its RNG remains
unchanged until a separately scoped owner integrates decision progress.

## Validation and deferred work

Run `tools/verification/verify_ai_candidate_contract.py` with `--reference
C:/repositories/fft_decomp --check-managed-baseline --executable
scratch/all-data-extraction/SCUS_942.21 --output <new scratch JSON path>`. Offline
unit tests need neither the decomp nor prepared EXE. Run the full Python discovery
suite from the repository root with writable scratch TEMP/TMP. Evidence records
the suite and repeated source/data/history audit results.

No C# file changed or .NET test rerun in this spike. Last implementation baseline
is ZOD-123: 3,696 .NET / 396 Python tests. Historical fixtures/evidence/APIs remain
unchanged. General clocks, abilities/reactions/terrain, roster/search/scoring,
policy selection, decision RNG integration and replaceable AI interface remain
future work. Editor UI redesign and ZOD-106 battle checkpoints stay deferred.
