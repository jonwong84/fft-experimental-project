# Shared Faith magic arithmetic and amounts — ZOD-335/336

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
unchanged. ZOD-340 next handles proc decisions; other documented deferrals remain.
