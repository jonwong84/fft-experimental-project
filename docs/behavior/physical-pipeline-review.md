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
