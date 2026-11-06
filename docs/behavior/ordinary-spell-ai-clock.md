# Ordinary spell AI charge simulation

## Source contract (ZOD-413)

The read-only reference remains clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/ordinary-spell-ai-clock-source-v1.json) pins action
preparation, turn clock, AI evaluation and supporting definitions. This is source
study and independently checked arithmetic, not an original/C execution claim.

`ai_action_simulate_and_score` saves a baseline, prepares the candidate action
and its move order, marks ACT, then calls `turn_end`. Ordinary setup uses the
secondary CT masked to seven bits, Short Charge rounded up, and Non-Charge;
the considered-AI loader's Monster Skill/Defend quirk is a different quantity.
The existing `MagicCharge` owner already implements both arithmetic paths.
Preparation queues a nonzero charge without spending MP. Readiness spends MP
before formula work. An immediate hit executes before ending the turn.

`turn_end` refunds20 for unused Move and20 for unused ACT, capped60. A spell has
used ACT, so the fixed-origin candidate refunds only the unused Move. The clock
resumes ordinary PICK_UNIT before another charge decrement. Highest CT wins,
with source-slot order breaking ties; upkeep consumes a real draw after modulo.
All ordinary ready units at a tick precede TICK_CHARGES/RESOLVE_CHARGE. The first
charge tick can occur at the current tick without an ADD_CT. CT gain caps254.

On UNIT_READY, simulation marks a movement opportunity for an eligible living
unit and directly clears `has_turn`. It does **not** issue Wait or call turn_end,
so there is no new unused-choice refund. Charging does not block ordinary walking.
Dead units still consume upkeep and CT modulo but yield no UNIT_READY or death
counter decay in AI mode. No broader corpse/Undead/Reraise scheduling is implied.

The AI loop calls the clock at least once even after an immediate action. It
stops after a returned event when nothing remains Charging/Performing, or after
the owned charge resolves. The event counter increments before its limit check:
256 stops before that call, so at most255 events are returned. If a charge remains,
source still scores its unchanged HP/MP with hit-percent255. Preserve that
unresolved score explicitly rather than pretending a hit or silently clamping it.
The clock's separate1201-phase guard is not a charge deadline.

At readiness, stored unit targeting follows its current anchor; tile targeting
retains the tile. Green area is recomputed without repeating initial blue range.
All target formulas run in source order before movement-opportunity exclusions.
A followed anchor that had a movement opportunity keeps only itself; tile damage
skips moved targets. Source no-valuable exits retain decision draws while restoring
the simulated state. Successful delayed application clears Charging and scores
immediately after the last pending action, without another unnecessary turn.

## Selected five milestones

ZOD-414 implements the single-charge phase-aware clock; ZOD-415 shares private
fresh admission/stored-hit work; ZOD-416 composes a fixed-origin candidate with
derived timing/opportunities and final score; ZOD-417 adds prepared diagnostics.
No rank insertion, target/origin enumeration, move policy or autonomous magic
turn is activated. Unrelated charges, secondary/special learned rows, timed
statuses, reactions, pickups/Golem, terminal/frame RNG and custom shapes remain
separate. The first candidate profile requires a living caster and the existing
bare-human primary-only context; all-dead immediate clocks stay unsupported.

## Independent validation

Ten literal descriptions cover current-tick charge readiness, tick13 Fire,
tick19 tier4 with an actor turn, immediate-one-event, corpse upkeep, Move refund,
overshoot order and the255-event unresolved boundary. Four Python cases verify
those descriptions and detect changed refund/phase/count expectations. The
formatting edit was explicitly approved and committed as `b7a222d`; the ten other
owner-file blobs remain preservation gates.

## Single-charge clock (ZOD-414)

`OrdinarySpellAiClock.Run` copies source-slot scheduling facts after ACT is
marked, derives the unused-Move refund, and resumes PICK_UNIT. It owns CT cap,
overshoot/tie order, current-tick charge decrements, actual upkeep draws, direct
simulated turn clearing and movement opportunities. Ordinary AI corpses retain
upkeep/modulo without yielding turns or changing counters. Charge0 means the
immediate hit was already evaluated, so one living readiness event is still run.

The profile admits source slots0..20, positive derived Speed1..50, no Quick,
a living caster, one owned charge and effect-free intervening phases. Dynamic
collections retain stable IDs; the21-slot check is a source simulation boundary,
not a catalog/storage size. Positive living Speed ensures an event within the
source per-call phase guard; all-dead immediate simulation is rejected. Upkeep
and tick exhaustion return entry RNG and no partial diagnostics.

Ten fixture cases plus three budget/admission cases pass. They include the
source255-event cap (counter102 at tick35 in the21-unit boundary fixture), an
actor turn during CT10 without Wait refund, corpse upkeep and exact zero-draw
readiness at maximum counters. Clock output is hypothetical data, never a battle
or an adoptable TurnCycleState. The existing player and immediate AI clocks are
unchanged.

## Shared fresh admission and stored hits (ZOD-415)

`OrdinarySpellAiWork` privately owns the same current-state/catalog binding,
initialized lists, considered usage, usefulness and initial aim capture for both
query paths. `OrdinarySpellAiHitWork` owns stored anchor/tile resolution, fresh
green geometry, formula frames, movement exclusions and included HP/MP effects.
It never repeats initial blue range. The current-aim public query now delegates
to these private owners, preserving its output and rollback boundary. No public
cached preparation, frame or score becomes authority.

Four new cases verify that a followed anchor can leave initial range, a stored
tile stays put, excluded collateral still consumes formula draws, and late
failure leaves private inputs unchanged. All1,292 AI cases pass. Considered CT
now delegates to the existing `MagicCharge.CalculateOrdinaryAiDelay` arithmetic
owner; actual setup remains its separate masked/Short-Charge path. No player
command or full-search interface changes are introduced.
