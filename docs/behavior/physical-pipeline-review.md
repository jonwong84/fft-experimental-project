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
