# Shared Faith magic arithmetic and amounts — ZOD-335/336

## Supplied formula0x08 trace cross-check — ZOD-436 support

On 2026-11-07 the owner supplied the FFHacktics formula0x08 call trace.
[Supporting receipt](../evidence/combat/formula08-supplied-trace-v1.json) pins
clean read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`, the current
managed inputs and the verified prepared BATTLE.BIN. All28 supplied instruction
words at `0x80188d84`–`0x80188df0` match, including eight direct calls and three
early exits. The listed return location `0x8018b97c` is the return address of
the indirect formula dispatch at `0x8018b974`, rather than a separate direct
call to this formula.

The verified sequence maps to these shared C# owners:

1. `FaithMagicFormula.Resolve` coordinates fresh entry and `MagicEvasion`.
2. `FaithMagicAmountFormula` loads explicit MA/Y inputs, strengthens XA, applies
   magical support/status/zodiac modifiers and stores the signed16 product.
3. `MagicDamageElements.Transform` handles stored weather/affinity work before
   `FaithMagicArithmetic.ScaleStored` applies Faith to a surviving amount.
4. `FaithMagicHpRouting` handles absorption, then `MagicConditionalProc` decides
   whether to attempt the status side effect. The current admitted empty-status
   record preserves hit/accuracy/miss without changing a status set.
5. `MagicResultFinalization` models the later bounded caller finalization.
   The source performs this after returning from the formula.

The three `bne r2,r0` instructions branch to the epilogue on **nonzero**:
evasion stops the amount work, elemental cancellation stops the Faith/proc tail,
and a failed conditional proc skips only the status applicator. Zero continues
each respective path. The source status wrapper preserves hit, unsigned16
accuracy and miss type around its attempt; a failed status attempt does not by
itself erase the damage result.

The wiki's Faith summary must not be read as two integer divisions by100.
Pinned source uses stored signed16 damage * target Faith * caster Faith /10000,
dividing once before its next signed16 store. Amount5 with Faith90/90 becomes4;
two sequential divisions would produce3. Existing managed arithmetic and literal
vectors already preserve the single division.

Managed pure argument/amount projection can run privately before evasion for
validation. A formula-entry miss exposes no precomputed amount, element marker
or proc work, and the coordinator preserves source-visible tail/draw ordering.
This mapping does not imply a literal translation of instruction execution order.
The existing source/prepared audits pass44 arithmetic and24 entry descriptions,
including eight representative Cure/Fire tier bindings. No runtime, tests,
fixtures or historical evidence were changed for this cross-check; no original
or C code was executed. All46 focused magic-contract Python tests pass, and all
ten owner draft blobs are unchanged.

This verifies the supplied top-level trace, **not completion of ZOD-436**.
The [magical-evade wrapper and dependencies](magic-evasion.md#supplied-magical-evade-wrapper--zod-436-support)
and the magical-modifier wrapper below have now also been supplied and checked.
Their individual modifier pages, followed by Elemental XA * YA and Elemental
Absorb and status roll, can support the remaining review. Store MA and Y and
Faith already have explicit source-backed
arithmetic descriptions; their wiki pages can still provide independent checks.
General status sets, reactions/Reflect/Golem and other documented deferrals
retain their current admission boundaries.

## Supplied magical-modifier wrapper — ZOD-436 support

The owner's Magical Support/Status/Compat listing matches all14 instruction
words at `0x80188744`–`0x80188778` in the verified prepared BATTLE.BIN. The same
[formula0x08 supporting receipt](../evidence/combat/formula08-supplied-trace-v1.json)
records four direct calls, all nine matching listed return addresses and helper
function fingerprints at reference `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The ordered dependencies are:

1. `0x80186204`, `formula_apply_magic_attack_up`: attacker Magic Attack Up,
   XA *4/3, then signed16 store.
2. `0x8018631c`, `formula_apply_magic_defense_up`: target Magic Defense Up,
   XA *2/3, then signed16 store.
3. `0x80186460`, `formula_apply_target_magical_statuses`: Shell applies *2/3,
   then Chicken-or-Frog applies *3/2 once. Each active step stores separately.
4. `0x80184964`, `formula_apply_zodiac_compatibility`: classified compatibility,
   including its unsigned16 XA*25 temporary, neutral floor and Serpentarius
   early-return exception.

These modify the shared work value XA, rather than changing the unit's current
MA or directly writing final HP damage. Formula0x08 strengthens XA before this
wrapper; multiplication by Y, weather/affinities and Faith follow it. The wrapper
has no branch or RNG call. Its other verified callers do not imply support for
those formula families in the managed runtime.

The percentage labels are summaries of exact fractions, not multipliers1.33 or
0.67. In particular, Magic Defense Up **and** Shell each run when both are set.
XA7 becomes4 after the first *2/3, then2 after the second; a combined *4/9 would
produce3. Chicken and Frog share one test and do not grant two separate bonuses.
`FaithMagicAmountFormula.PrepareProduct` already preserves this order and each
signed16 store. Formula0x0C keeps its distinct healing branch, bypassing the target
defense/status steps rather than reusing this entire damage wrapper.

The existing source/prepared audit passes15 Faith, seven stored-product and22
amount descriptions, including the literal full modifier chain and healing
defense bypass, plus eight prepared Cure/Fire bindings. All11 focused .NET amount/
arithmetic tests and eight Python tests pass. No runtime or fixture change is
required; all ten owner draft blobs remain unchanged and ZOD-436 remains open.

### Supplied Magic Attack Up helper

The owner's helper listing matches all20 instruction words at
`0x80186204`–`0x80186250` and all seven listed return locations. The supporting
receipt records these checks against the same prepared BATTLE.BIN/reference.
The routine reads the attacker's support byte`0x90`, mask`0x04`. A clear bit
branches to return, leaving XA unchanged; the branch delay slot only loads a
register constant. A set bit reads signed16 work XA at `0x801938ce`, multiplies
by4, divides by3 with signed truncation toward zero, then stores its low16 bits.

The `0x55555556` multiply-high and sign correction implement that integer
division exactly; they are not a floating-point approximation of33%. An offline
integer comparison over all65,536 signed16 inputs matches truncation followed by
the store, including negative inputs and wrap. For example, XA7 becomes9,
XA-2 becomes-2, and XA32767 has quotient43689 stored as-21847. These arithmetic
checks do not admit arbitrary signed values into the bounded battle pipeline.
No original or C code was executed by the comparison.

`FaithMagicAmountFormula` already conditionally uses `ScaleXa(xa,4,3)` with an
unchecked signed16 store. This changes shared work XA, not the unit's current MA,
Y, final HP or RNG. The routine contains no calls; its warning about preserving
`r31` before adding a `jal` concerns the MIPS calling convention and imposes no
corresponding C# restriction. All11 focused .NET amount/arithmetic tests and eight
Python tests pass; all ten owner draft blobs remain unchanged. No runtime or
fixture change is required.

### Supplied Magic Defense Up helper

The owner's listing matches all20 instruction words at `0x8018631c`–`0x80186368`
and both listed returns (`0x8018875c`, `0x80189010`). The same supporting receipt
records this check at the pinned source and prepared-byte baseline. This helper
reads the **target's** support byte`0x90`, mask`0x02`. If the bit is clear, `beq`
skips to return without changing XA. Otherwise it loads signed16 work XA,
multiplies by2, divides by3 with truncation toward zero, then stores signed16.
It does not change unit stats or draw RNG.

The multiply-high/sign-correction sequence matches that exact division for
all65,536 signed16 inputs in an offline integer comparison. Every quotient fits
signed16. XA7 becomes4, XA-2 becomes-1, and XA-1 becomes0: this is neither a 66%
multiplier nor rounding toward negative infinity. The instruction annotation
saying the sign "should be0" must not exclude negative work values; the signed
correction handles them. No original or C execution is claimed.

The existing `ScaleXa(xa,2,3)` in the damage branch agrees. Shell still runs as
its own later reduction, and formula0x0C healing continues to bypass both target
defenses. The leaf-routine `r31` warning is MIPS calling-convention guidance,
not a C# limitation. All11 focused .NET amount/arithmetic tests and eight Python
tests pass; all ten owner draft blobs remain unchanged. No runtime or fixture
change is required.

### Supplied target-status XA helper

Target's Status Affecting XA (Magical) matches all38 instruction words at
`0x80186460`–`0x801864f4` and both listed returns (`0x80188764`, `0x80189018`).
The same supporting receipt records the exact words, status masks, branch
targets and offline arithmetic checks against the pinned reference/prepared bytes.

Shell checks target status byte`0x5b`, mask`0x10`. When set, it loads signed16
work XA, applies *2/3 with truncation toward zero and stores signed16. The routine
then checks target byte`0x5a`, mask`0x06` for Chicken or Frog. Either or both bits
cause **one** later signed16 reload, *3/2 step and signed16 store. Both `beq`
branches skip their respective step when the masked value is zero.

The Frog/Chicken sequence multiplies by3, adds the logical sign bit, then uses
an arithmetic right shift. This implements division by2 toward zero, rather
than general upward rounding. The Shell step uses the previously checked signed
multiply-high correction for division by3. An offline comparison across all
65,536 signed16 inputs and all eight Shell/Frog/Chicken flag combinations passes
524,288 cases, including narrowing after each active step. This is arithmetic
evidence, not original/C execution or new admission of those battle states.

Shell's stored result feeds Frog/Chicken; the two steps cannot be collapsed into
an identity multiplier. For XA7, Shell stores4 and Frog/Chicken then stores6.
With no Shell, Frog plus Chicken still gives10 once, rather than two bonuses.
`FaithMagicAmountFormula` already preserves the separate steps, and
`FaithMagicInputs` supplies `t.Chicken || t.Frog`. The routine changes work XA
only, without status/unit-stat writes, RNG or calls. The MIPS leaf-call warning
does not impose a C# restriction. All19 focused .NET amount/arithmetic/current-input
tests and eight Python tests pass; all ten owner draft blobs remain unchanged.
No runtime or fixture change is required.

### Supplied original Compatibility routine

Owner direction: use the **original** routine and retain its observable quirks.
secondadvent's rewrite is deferred for a much later explicit review. The supplied
attachment links that patch but contains only the original instruction listing;
no rewrite analysis, behavioral equivalence or adoption is claimed here.

All112 original instruction words at `0x80184964`–`0x80184b20` and all17 listed
return locations match the verified prepared BATTLE.BIN. The supporting receipt
also checks the12-byte table at `0x8018f600`:
`00 00 00 01 02 00 03 00 02 01 00 00`. The abbreviated `0x8018f00` address in
the supplied notes is a typo; the listing's actual load uses `0x8018f600`.
The pinned decompilation remains `11464943bbdbd2a0af022d90cb7f10455cf911df`.

For valid signs0–12, either Serpentarius exits immediately. Otherwise absolute
sign difference indexes the table: differences3/9 are Bad,4/8 Good,6 resolves
by unit flags, and the rest Neutral. At opposition, either monster gives Bad;
otherwise equal masked male/female flag pairs give Worst and differing pairs
give Best. The comparison uses both bits (`0xc0`), not a single male/female test.
An offline table/raw-flag comparison against the managed classification agrees
for all10,816 valid-sign/monster/male/female combinations, including neither or
both sex flags. Raw sign nibbles13–15 remain outside managed admission.

The arithmetic preserves the original operations rather than applying the
summary percentages as one multiplier:

- Load signed16 XA, form **unsigned16** `XA*25`, then divide that temporary by100
  for Good/Bad or50 for Best/Worst. The original unsigned multiply-high constant
  `0x51eb851f` implements both divisions exactly over all65,536 temporary values.
- Add or subtract the adjustment, store signed16, then floor the reloaded result
  at1. Neutral also reaches this floor. Serpentarius skips the entire sequence
  and floor, so it must remain distinguishable from ordinary Neutral.

For XA7, Bad gives6 and Worst4; direct75%/50% multiplication would instead give5/3.
XA2622 wraps the temporary to14 before division. XA0 becomes1 under Neutral but
stays0 under Serpentarius. The offline comparison checks all65,536 signed inputs
across six compatibility modes (393,216 cases), including wrapping and the floor.
These are integer/source-description checks, not original/C execution or expanded
battle admission. The routine changes work XA only and draws no RNG.

`FaithMagicInputs.ClassifyCompatibility` and
`FaithMagicAmountFormula.ApplyCompatibility` already preserve these behaviors;
no runtime or fixture change is needed. All19 focused .NET tests and16 Python
tests pass; all ten owner draft blobs are unchanged.
The unrelated owner edit in `.circleci/config.yml` is excluded from this milestone.
This completes the supplied-document checklist for the four magical-modifier
dependencies, not ZOD-436's broader shared-consumer review. The supplied
[Elemental XA * YA wrapper](magic-elements-and-routing.md#supplied-elemental-xa--ya-wrapper--zod-436-support)
and its multiplication/weather/effective-weather/ability-element helpers are now
checked too, along with common affinity modification, nullification and pending-
proc/knockback cleanup. The supplied Faith Calculation below is now checked too;
the full shared-consumer review remains open.

## Supplied Faith Calculation

The owner's listing matches all62 instruction words at `0x80187150`–`0x80187244`
and all eight listed returns, including formula0x08 damage and0x0C healing. The
same supporting receipt pins the source, prepared bytes, Faith input type and
managed arithmetic at `11464943bbdbd2a0af022d90cb7f10455cf911df`.

For each unit, the helper reads current status byte`0x5c`: Faith bit`0x80` first
sets the **working copy** to100, then Innocent bit`0x40` sets it to0. Innocent
therefore wins if both are present. The attacker copy is byte`0x801938d3`, target
copy`0x801938d2`; the routine does not write either unit's actual Faith stat.
The receipt checks20 literal stored-byte/status descriptions. `FaithValue.Effective`
already preserves this precedence independently for both units.

The arithmetic loads signed16 stored HP damage, multiplies by unsigned-byte
target Faith and then attacker Faith, and performs **one signed division by10000**.
The prose's two `/100` terms cannot be implemented as sequential integer
divisions: damage5 with Faith90/90 gives4, while sequential divisions would give3.
The source's `0x68db8bad` multiply-high, arithmetic shift12 and sign correction
implement truncation toward zero. The sign correction uses0 or-1, not0 or1.
The final signed16 damage store is in the return delay slot.

Even full-byte Faith255/255 keeps the intermediate signed product in32 bits:
the envelope is -2,130,739,200 through2,130,674,175. An offline division proof
checks both endpoints of each of426,141 quotient intervals (852,282 checks),
using monotonicity within each sign half and the checked -1/0 bridge to cover
every integer numerator in that envelope. This is not execution of the original
or C code or a claim to run billions of tests. The unchecked signed16 store
retains output wrap; the routine introduces no cap, hit/type change, absorption,
HP publication or RNG.

`FaithMagicArithmetic.ScaleStored` already matches the single division and
store. Zero effective Faith makes the damage amount0 without nullifying a hit;
formula0x08 can still reach its later absorption/conditional-proc tail. Broader
current-status admission and the final result cap remain separate. All19 focused
.NET amount/arithmetic/current-input tests and eight Python tests pass; all ten
owner draft blobs and the unrelated CI edit are unchanged. No runtime or fixture
change is needed. The supplied [Elemental Absorb and status roll wrapper](magic-conditional-proc.md#supplied-absorptionproc-wrapper--zod-436-support)
at `0x0018871c` and its absorption/conditional-proc/inner helpers are now checked
too, including the shared Pass/Fail listing. The preserving status applicator at
`0x80187eb4` is the next useful listing. ZOD-436 remains open.

## Original implementation checkpoint

Implemented from `8982dd4` after the [ZOD-334 trace](../architecture/zod-334-faith-magic.md),
read-only reference `11464943bbdbd2a0af022d90cb7f10455cf911df`.
This adds pure formula work; it does not enable Fire/Cure commands, magic AI
entries, charging, target search or HP publication.

## ZOD-335: signed stores and Faith

`FaithValue` holds a raw byte and explicit current Faith/Innocent facts. Effective
Faith gives Innocent precedence over Faith status independently for caster and
target. It does not mutate either unit or define campaign Faith limits.
`FaithMagicArithmetic.MultiplyStored` retains the signed16 product store.
`ScaleStored` multiplies that stored value by both effective Faith bytes, divides
once toward zero by10000, then narrows to signed16. It preserves overflow instead
of silently introducing a wider formula or early clamp.

Seven literal product and15 Faith descriptions include negative values, full-byte
Faith, status combinations and output wrapping. Source-derived values are checked
independently in Python and exercised by managed tests. No RNG or I/O enters the
production implementation.

## ZOD-336: one parameterized amount pipeline

`FaithMagicAmountFormula.ProjectAfterHit` accepts the effect family (damage0x08 or
healing0x0C), byte MA/power, Faith inputs and explicit modifier facts. It uses the
same shared arithmetic for all power tiers. There are no Fire/Cure-specific
classes or ID switches, and no new general effect interface.

Both branches strengthen XA, apply Magic Attack Up and classified zodiac
compatibility, store product and scale by Faith. Damage applies Magic Defense Up,
Shell and Chicken/Frog between attacker and zodiac steps. Healing bypasses those
target defenses. Each modifier truncates at its original store. Neutral floors
zero XA to1; Serpentarius returns without that floor. Compatibility classification
from actual sign/sex/monster state remains the caller's responsibility.

Damage requires `DamageHasNoWeatherOrTargetElementChanges` in its explicit profile.
Absent certification rejects before output. This covers the omitted intermediate
steps rather than treating an unknown affinity/weather as neutral. Healing does
not run those source steps and needs no such certificate. Undefined family or
compatibility enum values and null inputs reject.

Output records modified XA, signed stored product, raw signed amount and effective
Faith. It intentionally precedes absorption, undead routing, conditional procs,
result eligibility/caps and live HP/MP/turn changes. A raw amount above999 stays
above999 here; negative wrapped values are preserved. The helper owns no RNG and
does not skip or authorize the missing parts of actual/AI spell evaluation.

22 amount descriptions cover four Cure and four Fire power tiers, modifier-order
rounding, healing defense bypass, all six classified compatibility modes, zero
power/MA, product wrap and uncapped raw amounts. The prepared audit verifies the
eight representative formula/X/Y rows after edition/size/hash checks. Other tier
differences (charge, MP, element, area, animation) remain data for their later
owners, rather than assumed support from the amount calculation.

## Validation and boundaries

Locked restore, clean Release build, **4,573 .NET cases** (1,532 Framework) and
**611 Python tests** pass. Eleven new Framework cases exercise44 source descriptions
and admission guards; eight Python cases check independent arithmetic, tampered
values and input boundaries. Live audit pins14 source files/36 functions and eight
prepared tiers. Coverage, guide, links and owner-draft checks pass.
[Implementation receipt](../evidence/combat/faith-magic-implementation-v1.json);
ignored logs under `scratch/faith-magic-334-336/`.

No existing snapshots, live stats, content packages, metadata, AI dispatch, command
APIs, projects, dependencies, fixtures or owner drafts are rewritten. The new
source fixture describes this new slice. Authoring remains typed C#/editor/import
through common validation into prepared JSON and immutable runtime definitions.
No original/C execution, host/editor activation or manual UI checkpoint applies.
[ZOD-337–339](magic-elements-and-routing.md) now add an element-aware composition
path and raw routing. This original API and its strict neutral certificate remain
unchanged. [ZOD-340](magic-conditional-proc.md) now handles bounded proc decisions;
[ZOD-341](magic-result-finalization.md) now finalizes bounded result data; [Magic evasion and fresh formula entry](magic-evasion.md) are now implemented;
[Current-input and ready-cast queries](magic-current-inputs-and-casts.md) now
compose these owners; ZOD-358 next traces original targeting geometry. Other documented deferrals remain.
