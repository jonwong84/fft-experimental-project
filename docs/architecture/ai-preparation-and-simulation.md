# Shared ordinary preparation and AI simulation (ZOD-422)

The owner selected this bounded refactor after supplying FFHacktics disassembly
for preparation, strike simulation, candidate scoring and the snapshot helpers.
Ordinary live casting and AI candidate evaluation now use one immutable primary
preparation proposal. Existing AI admission, simulation, strike processing, hit
accounting and absolute scoring retain separate owners. This is an incremental
refactor of the supported ordinary spell path, not completion of all original AI.

## Source and ordering

The read-only reference is `11464943bbdbd2a0af022d90cb7f10455cf911df`.
[The source receipt](../evidence/ai/ordinary-preparation-source-v1.json) pins eight
files and nine functions. Neither C nor the original game was executed.

- `action_prepare`, `0x8017C45C`, in `src/battle/combat/preparation.c` validates
  actor/followed-unit identity, resolves command-menu substitutions, prepares
  charge timing and performs phase-dependent state changes. Target type6 follows
  a unit; it is not a map-target flag. Validation here does not establish all
  action legality. Phase0 still writes a prepared destination command; nonzero
  phases store ordinary charge CT, and odd phases commit the applicable effects.
  The traced Mimic caller uses phase1, not the wiki's speculative0x0D.
- `action_commit_preparation`, `0x8017C3DC`, calls phase1 and clears action state
  only for an immediate primary action. It is shared with AI and actual actions,
  despite the wiki's AT-preview label. Result codes are invalid=-1, charge=0,
  immediate=1, equipment-change=2 and Jump=3; Defend also returns0.
- Ordinary timing masks raw CT with0x7F, rounds Short Charge upward when halving,
  then applies Non-Charge. Persevere bypasses both. The distinct AI entry-loading
  delay remains in `OrdinarySpellAiParameters`; it must not replace actual setup.
  Jump, Charge, Item, Throw, Math, Geomancy and Draw Out have separate command-menu
  paths. No new support for those paths is introduced here.
- `status_clear_action_state`, `0x80183DE0`, delegates to
  `status_action_state_set` in `src/main/unit_state.c`: it clears Charging, Jump,
  Defending and Performing and sets the unit's stored charge counter to0xFF.
  A pending ordinary plan's remaining counter0 means ready; it is not the
  original unit's separate no-charge sentinel.
- `ai_action_simulate_and_score`, `0x8019D37C`, owns candidate action/move order,
  hypothetical turn completion, charge-clock work and state scoring.
  `action_hits_simulate`, `0x8019DCBC`, owns conditional target/strike accounting;
  `strikes_simulate`, `0x8019DD8C`, owns strike processing and eligible reactions.
  The existing event cap permits at most255 clock calls. An unresolved charge
  can still receive a score with hit255. An immediate useful hit still advances
  one clock event before scoring.

## Managed responsibilities

- `Zodiac.Framework.CombatRules.OrdinaryMagicPreparation` accepts the existing
  certified `MagicChargePlanRequest` and returns a prepared plan and proposed
  caster state. It validates active identities and derives timing from the
  selected definition/current supports. It proposes Charging for a delayed
  primary cast, or cleared action statuses for an immediate one, preserving
  unrelated status values. It spends no MP and draws no RNG. Callers separately
  commit ACT usage using their existing turn-state owners.
- `MagicChargePlanQuery.Prepare` remains a compatible plan-only adapter. Existing
  public signatures and timing behavior are preserved.
- `OrdinaryMagicBattle.Queue` consumes the shared proposal before queuing or
  immediately resolving the spell. MP remains spent at the existing ready-hit
  boundary, not preparation.
- `OrdinarySpellAiWork` owns fresh admission, lists, usefulness and initial aim
  capture. Its candidate preparation delegates to the same combat rule. The
  ready-hit diagnostic still captures an aim without scheduling another action.
- `OrdinarySpellAiCandidateQuery` owns public admission and translates unsupported
  work to an atomic rejection. `OrdinarySpellAiActionSimulation` privately orchestrates
  preparation, the existing clock, ready strikes and scoring. It applies the
  proposed caster state only to immutable hypothetical units.
- `OrdinarySpellAiHitSimulation` has its own file and retains stored-aim resolution,
  formula projection and private result application. `OrdinarySpellAiAccounting`
  and `OrdinarySpellAiScoring` remain the existing accounting/scoring owners.

No command-family interfaces, raw PS1 memory structures or per-ability handlers
are added. Current original IDs, metadata ownership, formulas, evasion draws,
target order, source widths, events, schemas and command authority remain intact.

## Baseline and suspended-status boundary

The supplied transfer routines map to `simulation_unit_state_save` at0x8019E160
and `simulation_unit_state_restore` at0x8019E378. Mode0 saves/restores selected
baseline fields, action records and team Golem values. Max HP/MP are not saved.
The four bytes beginning at Faith are Faith, the transparency-removal flag and
current HP, not maximum HP.

Nonzero save mode stores only active identity, death counter, current statuses
and Death Sentence CT in compact records, then restores those fields from the
baseline. Nonzero restore mode reinstates those simulated fields. Other
simulated values remain in place across suspension. These helpers do not save
or rewind RNG or the whole AI workspace. The original VSync thresholds control
suspension; they are separate from gameplay charge ticks.

The current managed queries use private immutable values and do not suspend.
Their ordinary NotValuable outcomes retain decision RNG, while unsupported
contexts/counter exhaustion return the entry RNG with no partial outcome.
Porting the original suspension lifecycle, frame-interleaved RNG, reactions,
status handlers/display flags, pickups and broader clocks remains separately
gated. Do not replace that future lifecycle with a whole-world rollback.

## Supplied Defending wrapper (2026-11-07)

The owner's AI Simulation - Inflict defending listing at0x80183E00 matches
all15 instruction words in the hash-verified prepared BATTLE.BIN. The
[wrapper receipt](../evidence/ai/defending-wrapper-trace-v1.json) pins reference
`11464943bbdbd2a0af022d90cb7f10455cf911df`, the relevant source functions and
three executable caller sites. The routine is `status_inflict_defending`,
not an AI-only status setter: both actual command handlers and
`ai_action_simulate_and_score` call it.

Unit ID is multiplied by448 to obtain the battle-unit pointer. The call at
0x80183E20 reaches `status_action_state_set` at0x8005E7A8, with action-state
selector7 supplied in the delay slot. This is `STATUS_ACTION_STATE_DEFENDING`,
not a raw status bitmask. The wrapper consumes no RNG and always returns0;
that return does not report whether a status changed.

The shared setter adds inflicted Defending, removes inflicted Charging, Jump
and Performing, and sets the stored charge counter to0xFF. `status_unit_change`
rebuilds current status as inflicted|innate, so innate action statuses are not
erased by that removal. Changes to current action-status bits queue display
updates only during execution; preview/AI status mutation itself still happens.
Simply setting a Defending boolean would omit the charge/action-state effects.

The wrapper does not consume Act, set accuracy or end a turn. Its AI caller
sets candidate hit100 and Act consumed before hypothetical `turn_end`; the
actual player/AI command handlers separately mark Act and advance the controller.
Current managed physical decision admission requires `NoDefendSupport` and does
not select this action. The supplied trace adds evidence for ZOD-430 without
activating general Defend/status support. No runtime code or historical fixture
changed; this source-only confirmation does not require another gameplay test run.

## Supplied movement/pickup simulation (2026-11-07)

The owner's Moving on crystal/treasure listing at0x8019DB80 matches all75
instruction words in the hash-verified prepared BATTLE.BIN. The
[movement/pickup receipt](../evidence/ai/movement-pickup-trace-v1.json) pins
reference `11464943bbdbd2a0af022d90cb7f10455cf911df`, four source functions,
coordinate layout, classification/policy enums and metadata. The coordinate
helper call and both listed returns0x8019D484/0x8019D494 match the executable.

`movement_pickup_simulate` first compares the full32-bit candidate and cached
acting-unit coordinate words. Equality skips every update, including pickups
and marking Move. The canonical packed fields are X/layer/Y/zero; the raw
comparison also includes that final byte. On a changed word,
`ai_acting_unit_place` at0x8019F258 writes X, Y and the terrain-layer bit to
the actor. Facing and unrelated packed position bits remain intact; this
helper does not update cached `acting_unit_coords` or perform movement search.
Its three source assignments are available without requiring another wiki page.

Crystal kind1 restores current HP and MP to their respective maxima, then
clears Critical in both current and inflicted statuses. The latter byte at
unit offset0x1BD is `inflicted_status[2]`, not pending current-action infliction.
Any nonzero pickup then adds2 priority if the actor is noncritical after that
recovery and the configured policy is not0x11. Thus a previously Critical
actor can receive both crystal recovery and the bonus. Treasure does not heal
here and cannot earn the bonus while the actor remains Critical. The priority
store retains signed16 wrap; the bonus is separate from later absolute HP/status
scoring. Every changed destination marks `movement_taken=1`, even without pickup.

The pinned enum names policy0x11 `AI_AUTOBATTLE_RETREAT`, unlike the pasted
Conserve CT annotation. It is separate from `AI_TARGET_CONSERVE_CT` flag0x04.
The address comments also describe workspace offsets0x18/0xE30 as subtraction;
the actual addresses are base0x8019F3C4 plus those positive offsets. The caller
classifies a present occupant through `ai_tile_pickup_update`: None0, Crystal1,
Treasure2; the later treasure test wins if both source status bits are present.

The supplied routine consumes no RNG, grants no inventory and does not remove
the pickup. Those actual game systems must not be inferred from this AI
hypothesis. Its two caller sites place it after ACT in the admitted ACT-first
branch or before ACT in Move-first, so recovery ordering can affect later work.
General pickup simulation remains outside current effect-free managed profiles.
This adds source evidence for ZOD-430 without changing runtime code or repeating
unchanged gameplay tests. The owner's local AI turn-controller edit is preserved.

## Supplied End of Turn routine (2026-11-07)

The owner's listing at0x80183C6C matches all41 instruction words in the
hash-verified prepared BATTLE.BIN. All six stated return locations match calls
to `turn_end`; they include actual command/controller, between-turn, consumed
choice and AI simulation paths. The routine is shared, not AI-only. The
[turn-end receipt](../evidence/ai/turn-end-trace-v1.json) pins reference
`11464943bbdbd2a0af022d90cb7f10455cf911df`, relevant source/metadata, current
managed consumers and those caller addresses.

An absent unit (active battle ID0xFF) or unit without `has_turn` returns1 without
changes. Otherwise the byte CT is loaded into an unsigned16 intermediate:

- Add20 only if Move was not taken and Don't Move is absent.
- Add20 only if Act was not taken and both Don't Act and Performing are absent.
- Cap the combined value at60, store CT, and clear `has_turn`.

The additions precede the byte store; even raw CT255 plus40 clamps to60 rather
than wrapping to39. Restrictions do not earn refunds for their blocked choices.
At CT0, two unused unrestricted choices leave40; either single restriction
leaves20, and Don't Move together with Don't Act/Performing leaves0. Performing
only suppresses the Act refund here. These status cases are source descriptions,
not newly supported managed turn inputs.

The leaf leaves movement/action flags intact, does not subtract100 CT, does
not clean action statuses or advance the clock, and consumes no RNG. Its always1
return is not proof of a transition. Caller `turn_move_act_taken_set` separately
marks choices and uses `turn_should_end` before invoking it. The latter's
action-restriction decision must not replace this leaf's explicit refund gates.

Current `TurnCycle` and shared AI effect-free projections preserve the ordinary
refund/cap arithmetic. Their status-free active units have already reduced CT
to0..99; absent/raw-CT255/status cases do not expand that admission. Managed
inactive commands are rejected atomically, whereas this source leaf silently
returns1 without mutation. That remains an intentional API-boundary distinction.
No runtime correction was needed; all18 existing refund/cap and repeated-Wait
completion cases pass. The next supplied clock input is the
In between turn control routine; full ZOD-430 review remains open.

## Verification

The subsequent supplied [between-turn controller trace](../behavior/between-turn-clock-review.md)
checks all546 instructions,20 valid dispatch entries and18 calls/12 helper
targets. It records corrected phase labels, separate internal/event/frame
budgets, current managed limits and the next useful helper documentation.
This remains supporting evidence for the open ZOD-430 review.

Existing ordinary AI regressions retain immediate/delayed ordering, excluded
target draws, hit accounting, literal scores, unresolved hit255, event limits and
late-failure isolation. Added public tests compare live/AI preparation for CT0,
CT4 and raw CT131; check deterministic replay and unchanged input; and exercise
Short Charge rounding, Non-Charge precedence, preserved unrelated statuses and
rejection without a partial proposal. The existing plan-only admission tests
continue to cover inactive identities, profiles, duplicate IDs and Mime.

Validation results are recorded in the current handoff. Owner drafts, immutable
JobStats and int MP costs remain preserved. ZOD-421 physical delivery ownership
and ZOD-418 ordinary aim search remain the next separately scoped work.
