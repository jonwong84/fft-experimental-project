# Shared Faith magic trace — ZOD-334

Source study 2026-11-05 from managed base
`8982dd467db70a5f5f49e9bb67a5d0793b23210d`; read-only fft_decomp clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Receipt](../evidence/combat/faith-magic-source-v1.json): 14 files/36 functions,
including data declarations, dispatch, arithmetic, targeting, MP/charge and AI
boundaries. [44 independent descriptions](../fixtures/behavior/faith-magic-contract-v1.json)
cover signed stores, Faith/status priority and shared amount chains. The prepared
executable is checked by edition/size/hash before verifying eight Cure/Fire
formula/X/Y records. No original/C code is executed.

## Callers, data and arithmetic widths

`action_calculate_target_result` copies the ability's secondary record, initializes
attacker/target Faith from unit state, selects the formula through the menu path,
runs evasion reactions and dispatches the hit through `s_formulas[formula - 1]`.
This trace concerns0x08 damage and0x0C healing, whose formula dispatch is shared
across41 and six ordinary secondary records respectively. It does not make every
targeting/policy variant executable.

`unit_stats_t.attributes[UNIT_ATTRIBUTE_MAGIC_ATTACK]` and Faith are unsigned bytes;
secondary X/Y are bytes. `action_work_t.xa/ya` and `action_data_t.hp_damage` /
`hp_healing` are signed halfwords. `formula_store_ma_and_y` loads MA/Y into XA/YA.
Every modifier stores XA again. `formula_store_xa_times_ya_damage` narrows XA*YA
to a signed halfword **before** Faith. Preserve this store, including negative
wrapped results. Final result limits are later; do not clamp these steps to999.

`formula_calculate_faith` changes each copied Faith independently: Faith status
sets100, then Innocent sets0, so Innocent wins when both are present. It evaluates
stored signed HP amount * target Faith * caster Faith /10000, dividing once with
signed truncation toward zero, then stores another signed halfword. Sequential
division by100 changes results: amount5 with both Faith90 gives4, rather than3.
The full signed-halfword times two unsigned-byte product fits signed32 bits;
out-of-range Faith-scaled results still narrow at the store. The raw arithmetic
slice preserves byte storage values, rather than asserting normal gameplay stat
limits or admitting those values into a battle.

## Distinct chains with shared arithmetic

Formula0x08 runs magical evasion before the amount chain. After an admitted hit:

1. MA/Y, elemental strengthening on XA (*5/4).
2. Magic Attack Up (*4/3), Magic Defense Up (*2/3), Shell (*2/3), Chicken-or-Frog
   (*3/2 once), then zodiac compatibility. Each step truncates/stores separately.
3. Store signed16 XA*Y; weather modifies this stored amount, then target element
   affinity/nullification runs. Only a surviving hit continues.
4. Faith scaling, absorption/result routing, conditional proc/status application.

Formula0x0C loads MA/Y and strengthens XA, then uses
`formula_calculate_magical_xa_times_ya`: Magic Attack Up, zodiac, stored product.
It applies Faith, then undead reversal. It bypasses Magic Defense Up, Shell,
Chicken/Frog, damage weather/target-element steps and the damage proc branch.
Sharing the entire defensive pipeline between damage and healing would be wrong.

`formula_apply_zodiac_compatibility` classifies sign difference and opposition by
sex/monster flags; Serpentarius returns early. Good/bad add/subtract unsigned16
XA*25 /100, best/worst /50, then XA floors at1. Neutral also reaches the floor;
Serpentarius skips it. The amount slice takes an explicitly classified relation;
it does not infer signs, sex or monster state from unit IDs.

Weather/element steps remain separate source-traced work: storms weaken fire and
strengthen lightning; snowstorms strengthen ice. Affinities mark absorption,
nullify, then apply half and weak in order. Oil removal changes XA after stored
damage has already been computed, so that XA change does not double this damage.
Float nullifies earth, with rider/mount handling. These quirks are preserved in
the trace and must not be replaced by an assumed elemental multiplier.

## Result, RNG, resources and target boundaries

`formula_evade_store_magical` loads accessory/shield magic evades, leaves class
contribution zero and treats monsters specially. The shared evasion machinery
applies status modifiers; secondary Evadeable controls whether final checks run.
The per-source rolls and early failure ordering belong to admission, not amount
arithmetic. Reactions/Reflect can act before formula dispatch.

`formula_get_random` returns0x4000 for AI/preview, real rand for execution.
The damage conditional-proc inner function draws only during execution, even
when the status record ultimately has no effect. Preview shows a conditional
proc while AI excludes it. Healing's amount chain has no such proc. A pure raw
amount result cannot certify the RNG or status behavior of a complete spell.

Undead reversal makes healing damage against undead and clears canceled statuses
in the first status byte; otherwise it moves stored damage into the healing field.
`action_finalize_target_result` independently checks Dead/Petrify/Wall eligibility,
accuracy, upper999 caps and Golem interactions. Applying raw amount to HP before
these stages would skip observable rules. All remain outside the initial slice.

`mp_check_and_consume` checks Silence/Frog constraints, computes Half MP for the
default-menu non-Mime path, stores effective cost before insufficient-MP rejection
and deducts MP outside preview (including scratch AI state). AI state restoration
must retain its separate ownership rules. `unit_ability_ct_set` masks CT with0x7f;
the battle clock decrements pending charge and routes completion. AI entry loading
reads secondary CT and MP, with its own support adjustments. No charge/resource
rule is implied by calculating a raw spell amount.

`ability_range_set` uses range/area/vertical and execution flags independently of
formula math. Weapon range, self exclusion, cardinal/unit-panel masks, vertical
filtering and untargetable-panel handling require their own tests. AI targeting
then chooses unit/map/reflected/Math branches. Current sword/Dash/Stone admission
guards remain unchanged; this batch adds no magic entry to their owned lists.

## Selected children

**ZOD-335:** implement reusable signed product/Faith arithmetic and immutable Faith
input, preserving status priority and raw widths. **ZOD-336:** implement shared
already-hit amount projection for0x08/0x0C with explicit MA/Y, modifier facts and
classified compatibility. Damage requires unchanged intervening weather/target
element steps. Healing's different chain remains explicit. Both are completed in
the [amount implementation](../behavior/faith-magic-amounts.md).

[ZOD-337–339](../behavior/magic-elements-and-routing.md) now implement guarded
weather/element transforms, composition and raw result routing.
[ZOD-340](../behavior/magic-conditional-proc.md) now handles the isolated proc/RNG
decision; [ZOD-341](../behavior/magic-result-finalization.md) now finalizes bounded
result data. [Magic evasion and entry](../behavior/magic-evasion.md) are now bounded
implementations; [ZOD-348–357](../behavior/magic-current-inputs-and-casts.md) now add current-input,
resource/timing and supplied ready-cast queries. ZOD-358 next traces original
targeting geometry; live state/command/AI activation remains separate. The original selected-child scope was:
independently verify and implement weather/target-element
transformations on the stored damage, including cancellation/absorption and the
Oil/Float boundaries, before connecting complete spell effects. Live MA/Faith
state, learned grants, MP/charge, full target search, undead/status/reactions and
actual command integration need later selected children. Owner drafts, JobStats,
host/editor, automatic facing, corpse/terminal/frame RNG, broad interfaces and
ZOD-106 remain deferred.
