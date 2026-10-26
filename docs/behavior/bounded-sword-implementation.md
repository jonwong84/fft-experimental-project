# Bounded sword attack implementation (ZOD-022)

Date: 2026-10-23. Edition: North American PS1 `SCUS-94221`. Read-only reference
checkout `C:\repositories\fft_decomp` HEAD verified as
`11464943bbdbd2a0af022d90cb7f10455cf911df`. Every C symbol/path below refers to
that revision. The [reference refresh](reference-refresh-2026-10-22.md) bridges
the historical [attack trace](ordinary-physical-attack.md) to these source paths.

**ZOD-113 correction (2026-10-26):** Settled actor facing now retains the incoming
facing after hits and misses. See the [implementation/migration note](sword-settled-facing-implementation.md).
New-policy v3 fixtures execute the correction; old v3 files remain unchanged as
validated superseded history. Other arithmetic, guards and turn ordering below
are unchanged.

## Implemented scope and content boundary

`Zodiac.Framework.AttackBattleSimulator.Attack` is a pure, immediate, single-target sword
transition over immutable `AttackBattleSnapshot` state. The profile admits ordinary
humans with supplied effective stats, the same zodiac sign, zero MP and equipment
evade, no reaction/support/affinity effects, and a single right-hand sword. It
supports healthy and resulting Critical units; Dead is a representable result.

`BoundedSwordWeapon` contains the selected weapon's explicit ID and byte-range
power. It means an already validated range-1 Striking, formula-1, neutral,
empty-proc sword, not an arbitrary original equipment record. The content adapter
`AttackBattleSetup.ResolveWeapon` validates the selected package dependency and
legacy evade restriction, then projects the selected immutable `SwordDefinition`.
Schema 2's raw evade/capabilities/proc mode stay in the content definition and are
not activated or rewritten by projection. Both empty proc modes have the same
bounded HP effect and draw count; full status execution remains unsupported.
Simulation references neither `Zodiac.Content` nor file/JSON/platform libraries.

Call `AttackBattleSetup.Create` once for startup/content/effective-stat validation,
resolve the actor's weapon explicitly, and pass that projection to `Attack`.
No sword power, metadata dependency or original row is inferred by the simulator.
Direct Core callers select this closed synthetic profile explicitly; omitted
reaction/equipment systems are not represented by arbitrary unknown raw flags.

## Atomic gates and supported geometry

Before publishing any state or event, validate known actor/target IDs, the active
turn, unused action, nonself living target, matching equipped weapon, and the
supported map/unit geometry. Rejections return the exact input snapshot with an
empty event list. Programmer errors such as a null state/command still throw,
consistent with the existing simulation APIs.

The geometry remains ordinary body height 6 with no movement traits, distinct
stoppable/targetable lower-layer positions, and dry flat height-10 unblocked
tiles. Require cardinal adjacency on the same layer; diagonal and two-away
targets are rejected. Source striking/body overlap rules are traced in
`combat/targets.c::range_strike_lunge_calculate` / `height_overlap_validate` and
`combat/preparation.c::unit_action_height_calculate`. General height, slopes,
water, ceilings and layer combat are not enabled by these source helpers.

Before drawing, also require the tile one step beyond the target, away from the
actor, to be outside the map. Source
`combat/formula_core.c::knockback_calculate` / `knockback_destination_check`
therefore cannot queue a critical knockback. The restriction is conservative:
even an existing blocked/occupied tile is rejected, and rejection does not depend
on whether this seed would crit. Implementing knockback or admitting additional
safe destinations requires a separate source-backed expansion.

The chosen atomic gates are managed API contracts; they do not assert that every
original UI cancellation or direct C formula invocation preserves the global RNG.

## Arithmetic, draws and result application

`combat/formulas.c::formula_weapon_damage` coordinates physical evasion and damage.
`combat/formula_core.c::formula_evade_store_physical` admits weapon evade only with
Weapon Guard and no action restriction; those abilities are absent here. Facing
clears class evade for side/back. Four ordered checks still draw accessory,
right-hand, left-hand and class, even with a zero chance; only class can evade in
this profile. Scale each raw value by multiplication/division by 32768. Front class
evade 100 still consumes all four draws before missing.

An important boundary clarification: same-sign zodiac is neutral but
`formula_apply_zodiac_compatibility` still raises nonpositive XA to 1. Thus
`baseXA = max(1, PA)`, including PA 0. Critical is a scaled roll below 4; on a
critical draw a bonus and use `XA = baseXA + floor(rawBonus * baseXA / 32768)`.
The worst critical XA is `2 * max(1, PA) - 1`. Reject a configuration whose worst
product with power exceeds 32767, before any draw. Source stores signed-16 damage
in `formula_store_xa_times_ya_damage` before finalization's positive cap 999; the
bounded envelope makes the managed multiplication equivalent without guessing
overflow behavior. Power 0 gives zero damage but still draws critical/proc.

After a hit, consume the conditional 19% proc draw even for an empty set; both
supported modes add no status-selection draws. `combat/formula_modifiers.c`
contains `formula_calculate_weapon_damage`, `formula_roll_conditional_status_proc`
and the empty status paths. There is no draw-saving shortcut for empty effects or
zero damage. Miss uses four draws, a noncritical hit six and critical hit seven.
Preview/global-stream integration remains separate from this execution API.

`combat/resolution.c::action_finalize_target_result` caps positive damage at 999.
`action_apply_target_result` clamps HP subtraction to 0..maxHP, applies Dead at
newly zero HP and increments unsigned-byte KO count (255 becomes 0). A living
target at `HP <= floor(maxHP / 5)` becomes Critical. Resolved damage is the capped
formula amount; the HP-changed event reports actual HP loss, which can be smaller.
No HP-changed event is emitted for a miss or zero damage. An accepted miss still
consumes the action. The returned settled actor retains its incoming facing;
temporary presentation turning is not published as a simulation change.

Draw-counter exhaustion returns `RandomDrawCountExhausted` atomically. Draws use a
local immutable RNG word/count and publish only on success; the required budget
follows the actual branch (a miss can succeed with four remaining draws).
Battle and turn snapshots receive the same final RNG state/count.

## Turns, ordered events and post-KO boundary

Source `turn/controller.c::action_execute_ability` marks Act before preparation
and results. `turn/clock.c::turn_move_act_taken_set` ends a turn immediately if
Move was already consumed. Events therefore appear as choice consumed, optional
turn ended, attack resolved, optional HP changed, optional status changed, then
optional KO. Existing turn/movement events are carried in `AttackTurnBattleEvent`.
These event types/order are managed presentation/replay contracts, not serialized
PS1 scratch fields. Facing is part of the returned combat snapshot.

An attack alone leaves movement available. Attack then Wait refunds unused Move's
20 CT, capped at 60; Move then Attack ends with no unused-choice refund. Invalid
commands preserve action availability, facing, HP, CT, RNG and all event output.

The combat-aware Move/Wait/Advance wrappers preserve combat projections across
existing turn transitions. Critical alone does not block ordinary turns. A living
actor may Wait after a KO to finish its current turn, but movement, synthetic
ConfirmAct and clock advancement with any Dead unit are explicitly rejected as
`UnsupportedSetup`. There is no ordinary scheduling of dead bodies, death counter,
revival or removal hidden in this ticket. Use these wrappers when holding attack
state; calling the older movement/turn-only API separately does not make it combat
aware. ZOD-023 can define its bounded terminal objective at KO without claiming a
complete death scheduler.

The Move wrapper explicitly uses the existing flat-grid prototype policy, matching
the earlier fixture path. This ticket does not activate or broaden terrain-aware
movement or establish complete original movement behavior.

## Executable fixtures and evidence limits

**Historical source-review follow-up (ZOD-108, 2026-10-26):** At the same pinned reference
revision, turn/controller.c::unit_facing_turn_to_target saves the incoming facing;
reward_grant calls facing_all_after_attack_restore to restore living units.
The then-existing managed sword action instead retained its attack-facing change,
as recorded above. This is an identified settled-state fidelity gap under
ZOD-038, not proven original behavior. ZOD-108 does not change the sword code,
historical fixtures or evidence. The new Throw Stone contract preserves incoming
facing at its settled command boundary; see
[the source trace](../architecture/zod-108-throw-stone-action.md).

**Historical ZOD-112 contract follow-up (2026-10-26):** The completed
[settled-facing investigation](../architecture/zod-112-sword-settled-facing.md)
pins the restoration boundary, exact later-evasion vectors and the historical
fixture/save migration policy. That investigation did not implement the correction;
ZOD-113 subsequently implemented it as described above. Historical fixture files
and earlier execution evidence remain intact.

Fixture schema 3 replaces scenario `weaponIds` with explicit `weapons` records
containing `id` and `power` for this closed profile. The test-only runner validates
every command, definition, expected snapshot and event before replay. It compares
acceptance, all rejection fields, attack outcome, instance identity, full ordered
state/events and unchanged inputs/map. Reports retain selected weapon parameters
with provenance. There is no JSON or fixture dependency in simulation.

Schema 1 movement and schema 2 behavior remain unchanged. Schema 2 attack
descriptions still report not-executed, even if relabeled as scenarios; they lack
an explicit executable weapon projection. Description-only schema 3 also remains
not-executed. Historical evidence is not silently upgraded to execution evidence.

The original checked-in synthetic scenarios under `docs/fixtures/behavior/` covered
the values below. New settled-facing replacements retain these same-action values:

- Normal seed 1: damage 25, HP 50 to 25, six draws, state 2516284547.
- Frontal miss seed 19: HP unchanged, four draws, state 2162199151.
- Side-facing seed 19: damage 25, six draws, state 2071569413.
- Critical seed 57: damage 45, HP 50 to 5/Critical, seven draws, state 3448561432.
- KO at HP 25; separate damage-cap/KO-byte-wrap and PA-zero-floor vectors.
- Atomic diagonal rejection with unchanged state/events/RNG.
- A two-command Move/Attack replay with turn end before damage events.

Direct tests also cover each cardinal direction, HP/5 boundaries, zero power,
critical with zero bonus, class evade 100, signed-16 envelope rejection, counter
exhaustion, inactive/unknown/already-acted/self/dead targets, unsupported knockback,
water/equipment, post-KO gating, repeatability and v1/v2 content projection.
Independently calculated BIOS sequences put raw critical rolls 1310/1311 at the
fifth draw of seeds 22402/27894, and raw evade rolls 3276/3277 at the fourth draw
of seeds 410/3878; tests verify the strict chance boundaries. Expected values were
authored from source plus the separately verified BIOS recurrence, not captured
from the implementation under test.

These are **source-derived** formula/state expectations and **synthetic** managed
API/content/event contracts. Passing managed replays is not execution of original
FFT C or independent original/emulator observation. No retail Broad Sword battle,
full effective-stat derivation, reaction/reward/death system or global retail RNG
stream equivalence is claimed. Imported media/provenance remain ignored under
`scratch/`; runtime uses managed code and prepared content only.

Final validation on Windows: `dotnet restore Fft.sln --locked-mode`, Release build
with zero warnings, and all 618 solution tests passed on 2026-10-23. An independent
read-only review checked source arithmetic and the atomic implementation boundaries.
