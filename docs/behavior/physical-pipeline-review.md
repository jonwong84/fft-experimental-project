# Throw Stone/Dash retrospective review — ZOD-427

This completes the first bounded batch of the
[ZOD-428 review programme](../development/documentation-review-programme.md).
The admitted actual, preview and AI paths were retraced against the supplied
FFHacktics formula0x37 call sequence and the read-only decompilation. One
confirmed shared-formula defect was fixed. Other reviewed branches retain their
explicit domain limits; the wider retrospective remains active.

Managed review baseline: `e62a4d75d77f2c8b8887adba4748d228b52a1df7`.
Reference: `11464943bbdbd2a0af022d90cb7f10455cf911df`, freshly checked clean.
The [source receipt](../evidence/combat/physical-pipeline-review-source-v1.json)
pins 14 files and 47 functions. Fingerprints identify the inputs to the bounded
branch review; they do not claim every branch of each dependency was reviewed.
The prepared SCUS_942.21 was checked at 356,352 bytes and SHA-256
`287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562`.
Neither reference C nor the original game was executed.

## Supplied formula0x37 trace confirmation

The owner's repeated disassembly was checked against pinned `target/battle.yaml`
and the hash-verified prepared BATTLE.BIN, loaded at0x80067000. All five JAL
targets match the named decompilation helpers, in the supplied order: physical
evasion, PA/Y loading, Attack Up/Martial Arts, the physical-modifier cluster,
then damage/knockback. See the [address-check receipt](../evidence/combat/formula37-call-trace-v1.json).
The wiki's00189F84 notation corresponds to virtual address0x80189F84; the
prepared file offset is0x122F84, not00189F84.

At0x80189F94, `bne v0,zero` branches to the handler's end when evasion returns
nonzero. The non-evaded path falls through into the remaining helper calls.
The annotation "If Attack is not evaded" describes that fallthrough.

The stated return location0x8018B97C is correct. The caller's indirect dispatch
is `jalr v0` at0x8018B974, its delay slot is0x8018B978, and the saved return
address is0x8018B97C. That instruction starts the formula<7 aftermath check in
`action_calculate_target_result`, the routine labeled Pre Formula Setup by the
wiki. The caller then reaches applicable aftermath and result finalization;
authoritative result application remains later. This confirmation changes no
gameplay code and adds no original/C execution claim.

## Formula0x37 dependency coverage follow-up

The owner requested an explicit account of the routines called by Dash and
Throw Stone's shared formula. All five direct helpers are already source-traced
in the ZOD-427 receipt. A fresh check of its 14 files/47 functions against clean
reference `11464943bbdbd2a0af022d90cb7f10455cf911df` matches every fingerprint.
The [dependency inventory](../evidence/combat/formula37-dependency-review-v1.json)
also records prepared instruction hashes and direct calls for the handler and
all five helpers. Source tracing, supplied documentation checks and managed
runtime admission are distinct coverage claims:

- **Physical Evade Calculation (`0x80188510`):** the eight-stage wrapper,
  physical equipment loader, final hit and per-source roll have supplied
  cross-checks below. The shared Transparent, Abandon, target-status, restriction
  and RNG helpers also have later magic-review evidence. Current physical
  execution admits its narrower base100/class/facing inputs; broader physical
  equipment, support, status and weather inputs are still guarded.
- **Store PA and Y (`0x80185e04`):** a source-traced leaf with no calls. It loads
  effective PA and ability Y into the working operands. Existing shared formula
  code consumes those values; a separate wiki listing is not a prerequisite.
- **Attack Up and Martial Arts (`0x8018614c`):** a source-traced leaf with no
  calls. It applies Attack Up `4/3`, then Martial Arts `3/2` when the selected
  weapon type is unarmed. It has no Two Hands step. These modifiers remain
  excluded by current physical admission. The supplied listing is now checked
  below, including the instruction-level conditions and stores.
- **Cluster of Physical Routines (`0x80188adc`):** four ordered calls: attacker
  Berserk/Frog, target Defense Up, target physical statuses, then original zodiac
  compatibility. Attack Up/Martial Arts is already applied by its caller; the
  cluster name does not mean Defense Up is skipped. All four callees are pinned
  in the source review. The supplied wrapper is checked below; its broader
  inputs remain outside current admission.
- **Damage and Knockback, alternative (`0x8018691c`):** calls the formula RNG
  helper twice, Pass/Fail once and knockback calculation conditionally. Damage
  uses random `1..Y`; only execution enters the strict two-Brave comparison.
  Current actual/preview/AI code and regressions cover the admitted damage and
  flat, single-target displacement path. Full landing/fall/terrain/mount behavior
  is still separate; recording `knockback_calculate` does not claim all of it is
  implemented.

The supplied **Damage and Knockback Routine (alternative)** is now checked below.
Its supplied [Knockback Calculation dependency](knockback-calculation.md) is now
checked too, including its Knockback 2 destination checker. The tile-unit lookup
wrapper is next useful; shared Pass/Fail is already checked and the formula RNG
source is pinned.
These pages add independent annotations; they do not block the currently
admitted source-traced behavior. No runtime or owner-definition change is
required by this coverage check, and no broader review group is closed.
All 186 focused .NET Dash/Stone/formula-assignment tests and two Python Dash
tests pass. The prepared Dash record and 32 source-derived descriptions also
pass; all ten owner draft blobs and the committed disabled CI gate are unchanged.

## Supplied Attack Up and Martial Arts

The owner's listing matches all 46 instruction words at
`0x8018614c`–`0x80186200` and all eight listed caller return addresses. The
[trace receipt](../evidence/combat/attack-up-martial-arts-trace-v1.json) pins clean
reference `11464943bbdbd2a0af022d90cb7f10455cf911df`, the prepared BATTLE.BIN and
source/header/metadata fingerprints. This is a leaf routine with no calls or RNG.

Attack Up is support byte `0x90`, bit `0x10`. When present, signed 16-bit XA is
multiplied by `4/3`, truncated toward zero and stored back to 16 bits. The
multiply-high constant is `0x55555556`; the note's nine-hex-digit value is a typo.

Martial Arts is support byte `0x91`, bit `0x20`, and additionally requires the
current action's selected weapon **type** to equal 0 (unarmed). The weapon ID is
read from work byte `0x801938d8`; type comes from primary item record
`0x80062eb8 + weaponId * 12 + 5`. This is not a category-flags test at offset 3
or an inference from the actor's current equipment. The summary omits this type
condition. When eligible, the helper reloads signed XA, multiplies by `3/2`,
truncates toward zero and stores the low 16 bits again.

Both bits can apply, in that order, subject to the weapon-type gate. The
intermediate store matters: XA 5 becomes 6, then 9, whereas replacing the pair
with a single doubling gives 10. Signed arithmetic also does not implement an
unconditional upward rounding rule: XA -5 becomes -6, then -9. Zero stays zero;
there is no minimum floor here. High-width inputs retain each signed reload and
low-16-bit wrap rather than receiving a new clamp.

Offline checks compare the instruction arithmetic with signed truncation/store
for all 65,536 XA inputs across eight support/type combinations (524,288 cases),
plus eight literal boundary descriptions. The pinned C helper agrees. There is
no Two Hands, critical, zodiac, YA update or final damage write in this routine.

Current physical profiles still exclude these support modifiers. No runtime,
fixture or owner-definition change is required, and this does not activate all
eight caller families. All 186 focused .NET Dash/Stone/formula-assignment tests
and two Python Dash tests pass; all ten owner drafts and the disabled CI gate
remain unchanged. The supplied **Cluster of Physical Routines** at `0x80188adc`
is now checked below. Its three status/support children are checked too;
the supplied alternative damage/knockback helper is checked below too.

## Supplied physical-modifier cluster

The owner's Cluster of Physical Routines listing matches all 14 words at
`0x80188adc`–`0x80188b10`, all four call targets and all six listed caller returns
in the verified prepared BATTLE.BIN. The
[trace receipt](../evidence/combat/physical-modifier-cluster-trace-v1.json)
pins clean reference `11464943bbdbd2a0af022d90cb7f10455cf911df`, this wrapper and
its four source callees. Their fingerprints still match the earlier physical
review. The wrapper calls each helper unconditionally and in this order:

1. **Attacker Berserk/Frog (`0x80186254`)**: Berserk applies `3/2` to XA;
   Frog then overwrites XA with 1. The supplied listing is now checked below.
2. **Defense Up (`0x801862cc`)**: the target's support applies a separate `2/3`
   step. The source name `without_supports` excludes the earlier attacker
   Attack Up/Martial Arts stage, not this target support. The supplied leaf
   listing is now checked below.
3. **Target physical statuses (`0x8018636c`)**: Protect applies `2/3`, then
   Sleep **or** Charging applies `3/2` once, then Chicken **or** Frog applies
   `3/2` once. The two positive groups are independent stages; the summary must
   not be interpreted as one combined boost whenever any of the four is set.
   The supplied Target XA affecting Statuses (Physical) listing is checked below.
4. **Original Compatibility (`0x80184964`)**: reuse the
   [supplied original routine](faith-magic-amounts.md#supplied-original-compatibility-routine),
   already checked in the magic review. All 112 instruction words were rechecked
   against the same prepared file. No duplicate listing is needed. The original
   rounding, stored widths and final floor remain; secondadvent stays deferred.

The wrapper itself has no condition, RNG call or direct XA store; the children
own those details. Separate stores and order must be retained rather than
collapsed into one multiplier. For example, attacker Frog's intermediate XA 1
can become 0 at Defense Up before ordinary compatibility floors it back to 1.
Serpentarius's early return skips that floor. Frog is therefore not a blanket
guarantee about the final XA under every combination.

These child details are source-traced coverage; the supplied Berserk/Frog,
Defense Up and Target XA affecting Statuses (Physical) listings are checked
below. Current physical support/status/general-zodiac
inputs remain guarded. This milestone changes documentation/evidence only;
runtime tests were not repeated after the preceding passing 186 .NET/two Python
physical validation. The source/prepared Dash audit and all ten owner draft
checks pass again, with the disabled CI gate unchanged.

## Supplied attacker Berserk/Frog helper

The owner's listing matches all 30 words at `0x80186254`–`0x801862c8` and all
four listed return addresses. The
[trace receipt](../evidence/combat/attacker-berserk-frog-trace-v1.json) pins clean
reference `11464943bbdbd2a0af022d90cb7f10455cf911df`, the verified prepared
BATTLE.BIN, source and status layout. Return `0x80188aec` belongs to the
physical-modifier cluster, correcting the supplied Magical XA * YA label.

Both checks read the attacker's status byte `0x5a`: Berserk mask `0x08`, then
Frog mask `0x02`. Berserk loads signed 16-bit working XA, multiplies by `3/2`,
truncates toward zero and stores the low 16 bits. XA 5 becomes 7 and -5 becomes
-7. The sign correction is needed for negative odd products; it does not mean
positive results round upward. Large values retain the original narrowing.

Frog is checked independently after the Berserk store and overwrites XA with 1.
Thus Frog wins within this helper when both bits are set, including after a
Berserk result wrapped. The Frog branch delay slot assigns register v0 to 1
even when the branch skips its store; that assignment alone does not change XA.
This remains a void helper, not a hit/result flag API.

All 65,536 signed XA inputs across four status combinations agree with signed
division/store (262,144 offline checks), plus eight literal boundaries. The
pinned C helper agrees. The input is working XA, which may already contain
attacker-support modifiers, rather than a fresh PA load. There is no RNG, other
call, YA change, final damage write, target defense or zodiac step here.

Frog's 1 is an intermediate value: later cluster stages still run. Current
physical status inputs remain excluded, so no runtime or fixture change is
needed. Source/prepared Dash and owner-preservation checks pass; runtime tests
were not repeated after the prior 186 .NET/two Python physical validation.
The supplied Defense Up and Target XA affecting Statuses (Physical) listings
are now checked below.

## Supplied Defense Up helper

The owner's listing matches all 20 words at `0x801862cc`–`0x80186318` and all
three listed caller return addresses. The
[trace receipt](../evidence/combat/defense-up-trace-v1.json) pins clean reference
`11464943bbdbd2a0af022d90cb7f10455cf911df`, prepared BATTLE.BIN, source and layouts.

Target support byte `0x90`, bit `0x08`, gates the XA load/store. With the bit
clear, the helper leaves XA unchanged. When set, signed 16-bit XA is multiplied
by `2/3`, truncated toward zero and stored as a halfword. The constant
`0x55555556`, signed multiply-high and sign correction implement that exact
division. Literal 66% would differ: XA 99 becomes 66 here, rather than 65.
Negative XA -5 becomes -3, rather than rounding down to -4.

All 65,536 signed inputs agree with signed division, including extremes
-32768 → -21845 and 32767 → 21844. Nine literal descriptions also retain the
disabled gate, zero and small operands. There is no minimum clamp: XA 1 becomes
0. Later compatibility retains its own floor and exceptions.

This leaf makes no calls and consumes no RNG. It does not update YA, final HP
work, Protect or compatibility. The physical support selector remains distinct
from the already checked magical Defense Up selector, despite their shared
arithmetic. Current physical admission still excludes the support. No runtime,
fixture or owner definition changed; runtime tests were not repeated after the
prior physical validation. Source/prepared Dash and owner-preservation checks
pass. The supplied **Target XA affecting Statuses (Physical)** at `0x8018636c`
is now checked below, leaving alternative damage/knockback next.

## Supplied Target XA affecting Statuses (Physical)

This is the exact routine called Target Physical Statuses in the earlier notes.
The owner's listing matches all 61 words at `0x8018636c`–`0x8018645c` and all
three listed caller returns. The
[trace receipt](../evidence/combat/target-physical-statuses-trace-v1.json)
pins clean reference `11464943bbdbd2a0af022d90cb7f10455cf911df`, verified prepared
BATTLE.BIN, source and field layouts.

The ordered stages are Protect (target byte `0x5b`, bit `0x20`) at exact `2/3`,
Sleep (`0x5c`, bit `0x10`) **or** Charging (`0x58`, bit `0x08`) at `3/2` once,
then Frog/Chicken (`0x5a`, mask `0x06`) at `3/2` once. Setting both bits within
either pair does not double that pair's boost. The Sleep BNE takes the boost
path when Sleep is present; the "not sleeping" annotation describes fallthrough
to the Charging check.

Each enabled stage loads signed 16-bit XA, truncates toward zero and stores the
low 16 bits before the next stage. Exact `2/3` is not literal 66%. XA 5 with all
five status bits set becomes 3, then 4, then 6. With the two boost groups and no
Protect, it becomes 7, then 10. A combined fraction would lose those truncations.
High-width values preserve the separate wraps and signed reloads.

Offline checks cover all 65,536 signed XA inputs across all 32 combinations of
the five status bits (2,097,152 comparisons), plus 11 literal boundaries. The
instruction arithmetic agrees with the pinned C helper, including both paired
OR conditions and sequential stores. This leaf makes no calls, consumes no RNG
and has no minimum clamp, YA update or final damage write. Target Frog is part
of a vulnerability boost here; the earlier attacker Frog helper remains separate.

Current physical status inputs remain excluded. No runtime, fixture or owner
definition changed; runtime tests were not repeated after the prior physical
validation. Source/prepared Dash and all ten owner-preservation checks pass.
The supplied **Damage and Knockback Routine (alternative)** at `0x8018691c`
is now checked below; original Compatibility is already checked.

## Supplied damage and knockback helper

All 52 words at `0x8018691c`–`0x801869e8`, four call sites and the listed return
`0x80189fbc` match the prepared BATTLE.BIN. The
[trace receipt](../evidence/combat/random-pa-damage-knockback-trace-v1.json)
pins clean reference `11464943bbdbd2a0af022d90cb7f10455cf911df`, this helper and
its three distinct dependencies. No runtime or original/C execution is added.

Damage first calls `formula_get_random` at `0x8018eea0`. It loads the unsigned
ability Y byte at `0x801938fa` directly, rather than working YA, and computes
`factor = raw * Y / 32768 + 1`. The XA load at `0x801938ce` is unsigned 16-bit;
the product's low 16 bits are stored as HP damage. Attack type is assigned
`0x80`, not ORed with a previous type. The HP damage store at `0x80186980` is the
action-mode branch's delay slot, so it runs in every mode.

For positive Y, the factor spans 1 through Y. For Y 0 it remains 1, preserving
the already supported zero-Y behavior. The intermediate raw/Y and raw/Brave
products are nonnegative and fit signed 32 bits for 15-bit raw and byte operands.
The negative-correction sequences implement truncation, not a generic operation
to make a product positive. Final damage still narrows independently; this
helper neither caps at 999 nor applies live HP changes.

In execution, the raw draw order is damage, attacker Brave, then target Brave.
The second `formula_get_random` call produces the attacker threshold
`actorRaw * actorBrave / 32768`. `random_roll_pass_fail` at `0x8005e0cc` then
draws with maximum target Brave and that threshold; the threshold shift is in
the call delay slot. Return 0 means the target's scaled roll is strictly below
the attacker threshold and falls through to `knockback_calculate` at
`0x801843ec`. A nonzero return skips that call. Equality never triggers it.
Both Brave draws happen even when the threshold is 0 or the later landing is
unusable. Zero maximum also still draws and scales to 0, though zero Brave is
outside current managed admission.

For any nonzero action mode, the formula RNG helper returns midpoint 16384
without drawing. The damage is stored, then the mode branch skips the entire
Brave/knockback tail. This helper consumes no draws in preview/AI. Earlier
physical evasion retains its separate mode/draw contract; do not infer a whole
action's draw count from this tail alone. Zero damage or a future lethal result
does not provide an execution shortcut before these draws.

The three dependencies are accounted for as follows:

- **Random Process (`0x8018eea0`)**: source-traced `formula_get_random`, including
  its nonexecution midpoint. Its separate supplied listing is an optional direct
  check; no repeat of root BIOS RNG is needed.
- **Pass/Fail Roll (`0x8005e0cc`)**: its supplied instructions and signed scaling
  were already checked in the [proc review](magic-conditional-proc.md#supplied-passfail-roll).
- **Knockback Calculation (`0x801843ec`)**: source-traced and represented within
  the admitted flat, single-target displacement path. A successful comparison
  merely invokes it; target eligibility and landing can still prevent staging
  movement. Its [supplied listing](knockback-calculation.md) now matches 137 words,
  two calls and both listed returns, preserving target/tile gates, Y-axis ties
  and pending kind/effect stores. Knockback 2 at `0x80184610` also matches its
  178 words/two calls/one return, preserving equal heights, headroom and exact
  empty-tile checks. The `0x8017dc88` tile-unit wrapper is next useful. Full
  terrain/fall/mount behavior and its descendants remain separate.

Ten factor, six stored-product and six Brave descriptions pass, alongside the
existing 32 Dash descriptions and prepared record. Current neutral/signed-safe
calculation and mode handling agree; no runtime, fixture or owner definition
changed. Runtime tests were not repeated after the prior physical validation.
All ten owner-preservation checks and the disabled CI gate remain unchanged.

## Supplied physical-evasion wrapper (2026-11-07)

The owner's full Physical Evade Calculation excerpt confirms the eight calls
in `formula_evade_calculate_physical`: load physical equipment/class values;
Concentrate; Dark/Confuse (including Transparent); Abandon; target statuses;
bow weather; facing; then `evade_roll`. The
[helper trace receipt](../evidence/combat/physical-evade-call-trace-v1.json)
pins the source/metadata and verifies all eight call targets and all twenty
listed caller return addresses against the hash-verified prepared BATTLE.BIN.
Caller-address verification does not implement those other formulas.

The four evade bytes are transient values for this actor/target calculation,
not four authored ability flags. They start from target equipment/class and
base hit100. Dark or Confusion causes one halving even when both are present;
Abandon and Defending have their own subsequent integer halvings. Bow/crossbow
night and storm steps each multiply the current base by3/4 with truncation.
Facing removes class evade from side/back and both hand evades from back.
Physical chance is `floor(raw * baseHit / 32768) < evade`; keeping the bytes
unchanged while reducing base hit raises their effective evasion probabilities.
Displayed accuracy combines separately truncated factors, rather than using one
roll against that displayed percentage.

The wiki's final "Calculate Hit %" call goes to `evade_roll` at0x80188488.
That wrapper calls `formula_evade_calculate_final_hit_percent` at0x80185814,
then returns1 iff the hit flag is clear. Zero rounded accuracy alone does not
establish that return value. For example, base12/four evades10 yields four
display factors16 and accuracy0, yet AI/preview can retain hit=true and return0
before later result finalization. The four real evade draws still occur;
random-evade flag changes are execution-only. Nonzero abilities without the
secondary Evadeable flag bypass the final check; basic Attack ID0 is separate.

The current `PhysicalDamageRules` remains a narrower base100, zero-equipment,
class/facing path. `MagicEvasion` has richer magic-only preparation and the same
source tail ancestry, but its value/prepared types deliberately force class0
and omit physical Concentrate, Dark/Confuse, weather and facing preparation.
It must not be reused directly as full physical evasion. A future shared tail
must preserve four contributions, source hit-versus-accuracy semantics, labels,
ordered draws and early exits while retaining distinct physical/magic loaders.
This excerpt requires no current-domain runtime correction or activation of
deferred equipment/status support. The supplied equipment loader is recorded
below, followed by the supplied final hit calculation.

## Supplied physical equipment-evasion loader (2026-11-07)

The owner's Equipment Evasion Setting (Physical) listing at0x80184F9C matches
all138 instruction words in the hash-verified prepared BATTLE.BIN. The
[equipment trace receipt](../evidence/combat/physical-equipment-evade-trace-v1.json)
pins reference `11464943bbdbd2a0af022d90cb7f10455cf911df`, the loader,
action-restriction classifier, relevant callers and metadata. Both listed return
locations,0x80188520 and0x80188578, match calls to this loader.

The loader starts base hit at100, clears the three equipment contributions,
and loads class physical evasion from the target. Monsters then return with
class evasion intact. Human accessory evasion uses the primary item's accessory
type flag and secondary-table index. Each hand independently selects weapon
evasion when its weapon type flag and eligible Weapon Guard are present;
otherwise it selects a valid shield's physical evasion, or leaves0. This is
precedence, not addition or a maximum: an eligible weapon with evade0 still
wins over the shield branch. These are separate classified source fields,
not a claim that a normal hand equips a weapon and shield simultaneously.

Weapon Guard eligibility requires reaction-set4 bit0x40 and an exactly-zero
`unit_get_action_restriction` result. The helper also returns1 for inability
to act and2 for restricted deep water; it is not merely a boolean test for
Don't Act. Item lookup uses category flags and secondary indices rather than
item-ID ranges. Primary records are12 bytes (type at3, secondary index at4);
accessory/shield secondary records are2 bytes, and weapon records are8 bytes
with evasion at5. This loader and its restriction helper consume no RNG.

Two annotations need care. The left-hand clear actually writes0x801938E0,
not the comment's0x801838E0. The second caller is named
`formula_evade_calculate_physical_without_weather` in the pinned decompilation;
formula0x05 calls it. The wiki's Charge/"seems to never be called" label does
not establish source reachability or justify removing the branch.

The existing `PhysicalWeaponEvasion.GetContribution` correctly implements the
weapon-only gate for its documented human/weapon inputs. It does not implement
this complete equipment loader. No admitted runtime defect was found here;
general equipment/status integration remains deferred. All eight existing
`PhysicalWeaponEvasionTests` pass; no gameplay code changed.

## Supplied final hit-percentage routine (2026-11-07)

The owner's Calculate Final Hit % listing at0x80185814 matches all162
instruction words in the hash-verified prepared BATTLE.BIN. The
[final-hit trace receipt](../evidence/combat/final-hit-percent-trace-v1.json)
pins reference `11464943bbdbd2a0af022d90cb7f10455cf911df`, the final routine,
its per-source roll helper, the wrapper and the random comparison helper.
All four calls to `evade_source_roll` at0x801848D8 and the stated caller return
location0x80188498 match the executable.

The flag bypass applies only to nonzero ability IDs; basic Attack ID0 always
reaches the checks. Accessory, right hand, left hand and class evade are compared
with the byte base hit. Equality is sufficient to force hit=false/accuracy0:
the condition is **evade >= base hit**, rather than strictly greater. This path
still reaches the roll helpers, including when base hit is0; it skips the
division section, avoiding a zero denominator there.

Otherwise each remaining-chance factor is independently truncated:
`factor = (baseHit - evade) * 100 / baseHit`. The four factors multiply before
division by1,000,000. That integer result multiplies the preexisting signed16
accuracy, then divides by100 before storage. Base hit is not substituted for
that existing accuracy. With positive byte base hit and all evades below it,
the four-factor product is at most100,000,000 and fits signed32. Preserve both
division stages: base25, evades15/15/15/0 and initial accuracy80 produce4;
combining those final divisions would incorrectly produce5. Rounded accuracy0
does not itself clear hit, as the earlier wrapper example demonstrates.

Roll helpers run in accessory/right/left/class order. Every reached helper
draws, even for zero or certain evasion. Only execution can record a random
evade and stop the remaining helpers; preview/AI retain all four draws. A
pre-established forced miss remains a miss even though preview/AI do not set
random miss labels. These mode details come from the pinned helper and
`random_roll_pass_fail`, not merely the page's summary.

Miss types are1/2/3/4 in that order. The accessory label uses its item ID;
each hand label uses its weapon ID unless that ID is0xFF, then its shield ID.
Class uses0xFF. Label choice does not repeat the loader's item-type/Weapon Guard
selection, so a future equipment integration must preserve the two decisions
separately.

The existing physical base100/class-only path and magic nonzero-ability/class0
path preserve these rules within their admitted domains. All39 existing
`MagicEvasionTests`/`PhysicalFormulaAssignmentTests` cases and18 corresponding
Python tests pass. No gameplay code or historical fixture changed; general
equipment/status support and a shared physical/magic kernel remain separate.

## Supplied per-source evade helper (2026-11-07)

The owner's Attack Evaded Calculations listing at0x801848D8 matches all35
instruction words in the same hash-verified prepared BATTLE.BIN. Both early-exit
branches target0x8018494C, the random-helper call targets0x8005E0CC, and all four
listed returns match the final routine's accessory/right/left/class calls.
The source and normalized function hashes for `evade_source_roll` and
`random_roll_pass_fail` are already pinned in the final-hit trace receipt above
at reference `11464943bbdbd2a0af022d90cb7f10455cf911df`.

The first two inputs are explicitly masked to unsigned16 before calling the
random helper with maximum=base hit and threshold=evade. The helper draws first,
using `floor(raw * baseHit / 32768)`. For positive base hit this is0 through
baseHit-1, not an inclusive upper bound. At base hit0 the scaled draw is0;
the underlying RNG draw still occurs. Evade0 never wins, while evade>=positive
base hit always wins that comparison. The final-hit caller separately handles
forced misses and supplies byte-sized values.

Only a winning comparison during action state0 (Execute) clears hit, stores the
low byte of the supplied miss type/item ID and returns1. All other paths return0
without changing those fields. In particular, preview/AI do not force hit=true:
they preserve any miss already established by the caller. This helper does not
change accuracy or itself use/break the labeled item. The existing bounded C#
paths already preserve the relevant behavior; this confirmation changes no
runtime code or fixture and does not require repeating the preceding tests.
The remaining linked RNG page at0x0005E0CC can confirm its disassembly, but its
source comparison is already pinned; it does not block this helper review.

## Confirmed defect: minimum YA in assigned formula0x32

`formula_repeating_fist` calls `formula_store_pa_and_pa_plus_y_divided_by_two`,
the physical modifier/critical steps, then `formula_store_xa_plus_ya`.
That last helper temporarily puts YA in XA and calls
`formula_apply_zodiac_compatibility`. Even ordinary neutral compatibility floors
a zero term to1. Serpentarius returns before that floor; it is already excluded
from the physical snapshot's sign0..11 domain.

With PA1/Y0, initial YA is0, the neutral helper makes it1, and the additive base
is2. The previous implementation used base1. For X9, AI/preview midpoint selects
factor5: corrected damage is10 rather than5. Execution from seed1 selects
factor2: corrected damage is4 rather than2. Criticals still prepare YA from the
original PA; the floor does not recompute YA from critical-boosted XA.

[PhysicalDamageFormula](../../src/Zodiac.Framework/Formulas/PhysicalDamageFormula.cs)
now applies that mandatory neutral floor. This affects the supported formula0x32
assignment boundary, including a Dash/Throw Stone definition selecting that
formula. Retail Dash/Throw Stone still select0x37; their results and draw counts
are preserved. No broader zodiac/support/status domain was activated.

The earlier Python description and `zero-operands-are-valid` literal repeated
the same base1 assumption. Historical
[v1](../fixtures/behavior/physical-formula-assignment-v1.json) is preserved.
[v2](../fixtures/behavior/physical-formula-assignment-v2.json) explicitly records
the erratum, corrects that vector and adds nine floor/neighbor/mode vectors.
Current managed/Python verification uses v2; historical receipts and Git
snapshots retain their original contents. Nine new/current managed cases failed
before the fix, including both actual delivery regressions, and pass afterward.

## Verified behavior within the admitted domain

- **Formula0x37 pipeline.** Physical evasion precedes PA/Y setup, Attack Up/
  Martial Arts, physical status/defense/compatibility modifiers, then random
  damage and the Brave knockback comparison. The closed physical profile admits
  ordinary same-sign humans with no modeled support/status/equipment modifiers;
  current effective PA is still used. Both named formula helpers forward to the
  shared formula owner. Ability definitions supply formula/X/Y independently
  of their adjacent/projectile delivery.
- **Mode and flag separation.** Both retail records use0x37, zero charge/MP and
  empty status0, with range 1/Y 4 for Dash and 4/Y 2 for Stone. Dash enables vertical
  tolerance1 and bypasses secondary evasion/direct targeting; Stone does not
  enable its vertical0 as a height lock and enables evasion/direct targeting.
  Actual Dash consumes damage plus two Brave draws; Stone adds four physical
  evade draws on a hit. Preview/AI use midpoint16384 for formula randomness,
  skip0x37's execution-only Brave tail, and retain real evasion draws. Primary
  AI Evadeable remains separate from the secondary execution flag; Dash's retail
  primary flag still contributes to AI list statistics.
- **Damage and displacement.** The Brave comparison is strict and independently
  scaled; its draws precede lethal/landing suppression. Shared settlement caps
  positive damage at999, updates HP/Critical/Dead/byte KO count, and suppresses
  displacement on lethal damage, occupied/off-map landings. The dominant axis
  decides direction, with Y winning ties. `KnockbackAttempted` is a diagnostic
  distinct from actual displacement. The source helper requires exactly one
  target and rejects immortality, importance3, self and mounted cases; the
  admitted profile excludes those contexts. Multiple-target knockback remains
  outside this action API.
- **Trajectory.** Shared flat-roster geometry retains source sprite-height
  endpoints, strict collision bounds, sample order and source-order blockers.
  Actual Stone can resolve the first intercepted unit; its AI between-unit
  check excludes both endpoints and skips obstructed aims. Dash uses adjacent
  admission. Range, trajectory and eventual knockback landing are distinct.
- **AI integration.** Formula projection flows into copied-unit HP, the shared
  effect-free clock and absolute score. Entry scans retain initial-roster
  legality, ranked origins, map-ordered aims, instant consumed-aim policy,
  act-first destination substitution and one decision RNG. Strict best rank,
  proximity ties, movement-baseline admission and ranked lottery remain their
  shared owners. The checked executor obtains a fresh decision; movement
  continuations freeze the concrete effect/profile and recheck actual range.
- **Definition/grant ownership.** Original companions retain schema1. Explicit
  custom schema2 can assign formula/X/Y while preserving the selected delivery's
  current flags/range. Learned slot grants and owned metadata are checked
  separately from executable effects and AI preferences. Owner draft flags are
  still inactive. No per-ability damage implementation was added.

## Validation and remaining scope

The corrected current fixture contains 27 source-described formula cases. New
actual adjacent/projectile cases verify target HP46 and exact2/6-draw exits;
new AI cases verify damage10, priority511 and exact1/5-draw formula-plus-clock
exits. Preview, critical, neighboring operands, zero-Y0x37 and evasion-mode
boundaries are covered. Four new Python checks exercise the floor and detect
the former assumption. The [implementation receipt](../evidence/combat/physical-pipeline-review-implementation-v1.json)
records full solution/Python validation, source/retail audits and owner hashes.
The existing Dash auditor also rechecked its 32 formula/actual descriptions and
the 83-function roster source receipt. Retail parameter reads stayed in scratch.

This is source-based review and managed regression evidence. Existing native
presentation observations remain historical; this numerical correction adds no
new whole-game/original-execution or native fidelity claim. Open limits include
general terrain/elevation, multiple targets, broader modifiers/zodiac, status/
equipment/Golem/reactions, automatic facing and full terminal/frame RNG.

The [progress receipt](../evidence/maintenance/documentation-review-progress-v1.json)
records precisely which paths/edges this batch reviewed. Shared movement, turn,
ranking and command dependencies remain subject to their broader programme
reviews; their appearance here does not close those groups. ZOD-429 next reviews
sword Attack and Accumulate mechanics/settlement. ZOD-426 remains queued as a
supporting movement-origin/baseline trace. ZOD-428 remains open.
