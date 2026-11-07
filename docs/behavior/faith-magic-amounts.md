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
The next useful supplied page is Magic Attack Up routine, followed by Magic
Defense Up routine, Target's Status Affecting XA (Magical), and Compatibility.

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
